using Microsoft.MixedReality.WebRTC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestNetCoreConsole.GstInteractors
{
    class AppSrcToAutoVideoSinkGstInteractor : AbstractGstInteractor
    {
        private Gst.Element appsrc;
        private Gst.Pad _pad;
        private ulong _time = 0;
        private ulong _dt = (ulong)(TimeSpan.FromSeconds(1).Ticks * 100 / 30);
        private bool _seg = false;
        private int _count = 0;
        private uint _kfcount = 0;
        private byte[] rgbBytes = null;

        readonly object _queueLock = new object();
        Queue<byte[]> _queue = new Queue<byte[]>();

        private void PushData(object o, System.EventArgs args)
        {
            if (!_seg)
            {
                _seg = true;

                _pad.PushEvent(Gst.Event.NewSegment(new Gst.Segment()
                {
                    Rate = 30,
                    AppliedRate = 30,
                    Time = 0,
                    Format = Gst.Format.Default,
                    Duration = ulong.MaxValue
                }));
            }

            ulong mseconds = 0;
            if (appsrc.Clock != null)
                mseconds = appsrc.Clock.Time / 1000000;

            if (_count >= 30 * 3)
            {
                _count = 0;
                // _pad.PushEvent(Gst.Video.Global.VideoEventNewUpstreamForceKeyUnit(_time, true, _kfcount++));
            }


            var data = _queue.Count > 2 ? _queue.Dequeue() : _queue.Peek(); // DrawData(mseconds);
            var buffer = new Gst.Buffer(data);
            buffer.Duration = _dt;
            buffer.Pts = _time;
            // _pad.Push(buffer);
            appsrc.Emit("push-buffer", buffer);
            _time += _dt;
            _count++;

            buffer.Dispose();
        }

        public void HandleFrame(I420AVideoFrame frame)
        {
            int pixelSize = (int)frame.width * (int)frame.height;
            int byteSize = (pixelSize / 2 * 3); // I420 = 12 bits per pixel
            byte[] frameBytes = new byte[byteSize];
            frame.CopyTo(frameBytes);

            rgbBytes = new byte[pixelSize * 3];


            Console.WriteLine($"WebRtc frame received of {frame.width}x{frame.height}");

            // YUV2RGBManaged(frameBytes, rgbBytes, frame.width, frame.height);

            _queue.Enqueue(frameBytes);

        }

        private Gst.Buffer DrawData(ulong mseconds)
        {
            byte[] buffer = new byte[640 * 480 * 4];
            Cairo.ImageSurface img = new Cairo.ImageSurface(buffer, Cairo.Format.Argb32, 640, 480, 640 * 4);
            using (Cairo.Context context = new Cairo.Context(img))
            {
                double dx = (double)(mseconds % 2180) / 5;
                context.SetSourceColor(new Cairo.Color(1.0, 1.0, 0));
                context.Paint();
                context.MoveTo(300, 10 + dx);
                context.LineTo(500 - dx, 400);
                context.LineWidth = 4.0;
                context.SetSourceColor(new Cairo.Color(0, 0, 1.0));
                context.Stroke();
            }
            img.Dispose();
            var gstBuffer = new Gst.Buffer(buffer);
            return gstBuffer;
        }

        public override void Interact()
        {
            throw new NotImplementedException();
        }

        public void Interact(string capss = null, bool waitForEnd = true)
        {
            appsrc = Gst.ElementFactory.Make("appsrc", "source");
            var videoconvert = Gst.ElementFactory.Make("videoconvert", "videoconvert");
            var sink = Gst.ElementFactory.Make("autovideosink", "sink");
            var x264enc = Gst.ElementFactory.Make("x264enc", "x264enc");
            var flvmux = Gst.ElementFactory.Make("flvmux", "flvmux");
            var queue1 = Gst.ElementFactory.Make("queue", "queue1");
            var rtmpsink = Gst.ElementFactory.Make("rtmpsink", "rtmpsink");
            flvmux.SetProperty("streamable", new GLib.Value(true));
            rtmpsink.SetProperty("location", new GLib.Value("rtmp://localhost/live"));

            var caps = Gst.Global.CapsFromString(capss ?? "video/x-raw, format=RGBA, width=640, height=480, framerate=30/1");
            appsrc.SetProperty("caps", new GLib.Value(caps));
            _pipeline.Add(appsrc, videoconvert, x264enc, flvmux, queue1, rtmpsink);
            Gst.Element.Link(appsrc, videoconvert, x264enc, flvmux, queue1, rtmpsink);
            appsrc.SetProperty("stream-type", new GLib.Value(0));
            appsrc.SetProperty("format", new GLib.Value(Gst.Format.Time));
            appsrc.SetProperty("is-live", new GLib.Value(true));
            var needDataDelegate = new Action<object, EventArgs>(PushData);
            appsrc.AddSignalHandler("need-data", needDataDelegate);

            _pad = appsrc.GetStaticPad("src");

            this.Play(waitForEnd);
            // https://searchcode.com/codesearch/view/11976789/
        }


        private static void YUV2RGBManaged(byte[] pYUV, byte[] pRGB, uint width, uint height)
        {
            //returned pixel format is 2yuv - i.e. luminance, y, is represented for every pixel and the u and v are alternated
            //like this (where Cb = u , Cr = y)
            //Y0 Cb Y1 Cr Y2 Cb Y3 

            /*http://msdn.microsoft.com/en-us/library/ms893078.aspx
             * 
             C = 298 * (Y - 16) + 128
             D = U - 128
             E = V - 128
             R = clip(( C           + 409 * E) >> 8)
             G = clip(( C - 100 * D - 208 * E) >> 8)
             B = clip(( C + 516 * D          ) >> 8)

             * here are a whole bunch more formats for doing this...
             * http://stackoverflow.com/questions/3943779/converting-to-yuv-ycbcr-colour-space-many-versions
             */

            for (int r = 0; r < height; r++)
            {
                var pRGBOff = r * width * 3;
                var pYUVOff = r * width * 2;

                //process two pixels at a time
                for (int c = 0; c < width; c += 2)
                {
                    int C1 = 298 * (pYUV[pYUVOff + 1] - 16) + 128;
                    int C2 = 298 * (pYUV[pYUVOff + 3] - 16) + 128;
                    int D = pYUV[pYUVOff + 2] - 128;
                    int E = pYUV[pYUVOff + 0] - 128;

                    int R1 = (C1 + 409 * E) >> 8;
                    int G1 = (C1 - 100 * D - 208 * E) >> 8;
                    int B1 = (C1 + 516 * D) >> 8;

                    int R2 = (C2 + 409 * E) >> 8;
                    int G2 = (C2 - 100 * D - 208 * E) >> 8;
                    int B2 = (298 * C2 + 516 * D) >> 8;

                    //check for overflow
                    //unsurprisingly this takes the bulk of the time.
                    pRGB[pRGBOff + 0] = (byte)(R1 < 0 ? 0 : R1 > 255 ? 255 : R1);
                    pRGB[pRGBOff + 1] = (byte)(G1 < 0 ? 0 : G1 > 255 ? 255 : G1);
                    pRGB[pRGBOff + 2] = (byte)(B1 < 0 ? 0 : B1 > 255 ? 255 : B1);

                    pRGB[pRGBOff + 3] = (byte)(R2 < 0 ? 0 : R2 > 255 ? 255 : R2);
                    pRGB[pRGBOff + 4] = (byte)(G2 < 0 ? 0 : G2 > 255 ? 255 : G2);
                    pRGB[pRGBOff + 5] = (byte)(B2 < 0 ? 0 : B2 > 255 ? 255 : B2);

                    pRGBOff += 6;
                    pYUVOff += 4;
                }
            }
        }
        static void encodeYUV420SP(byte[] yuv420sp, byte[] argb, uint width, uint height)
        {
            var frameSize = width * height;

            var yIndex = 0;
            var uvIndex = frameSize;

            int R, G, B, Y, U, V;
            int index = 0;
            for (int j = 0; j < height; j++)
            {
                for (int i = 0; i < width; i++)
                {

                    // a = (argb[index] & 0xff000000) >> 24; // a is not used obviously
                    R = argb[index * 3];
                    G = argb[index * 3 + 1];
                    B = argb[index * 3 + 2];

                    // well known RGB to YUV algorithm
                    Y = ((66 * R + 129 * G + 25 * B + 128) >> 8) + 16;
                    U = ((-38 * R - 74 * G + 112 * B + 128) >> 8) + 128;
                    V = ((112 * R - 94 * G - 18 * B + 128) >> 8) + 128;

                    // NV21 has a plane of Y and interleaved planes of VU each sampled by a factor of 2
                    //    meaning for every 4 Y pixels there are 1 V and 1 U.  Note the sampling is every other
                    //    pixel AND every other scanline.
                    yuv420sp[yIndex++] = (byte)((Y < 0) ? 0 : ((Y > 255) ? 255 : Y));
                    if (j % 2 == 0 && index % 2 == 0)
                    {
                        yuv420sp[uvIndex++] = (byte)((V < 0) ? 0 : ((V > 255) ? 255 : V));
                        yuv420sp[uvIndex++] = (byte)((U < 0) ? 0 : ((U > 255) ? 255 : U));
                    }

                    index++;
                }
            }
        }

    }
}
