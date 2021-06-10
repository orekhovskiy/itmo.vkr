using System;

namespace TestNetCoreConsole.GstInteractors
{
    public class TestSrcToRtmpGstInteractor : AbstractGstInteractor

    {
        public TestSrcToRtmpGstInteractor() : base() { }

        public override void Interact()
        {
            // gst-launch-1.0 -e videotestsrc ! queue ! videoconvert ! x264enc ! flvmux streamable=true ! queue ! rtmpsink location='rtmp://localhost/live'
            var testsrc = Gst.ElementFactory.Make("videotestsrc", "videotestsrc");
            var queue = Gst.ElementFactory.Make("queue", "queue");
            var videoconvert = Gst.ElementFactory.Make("videoconvert", "videoconvert");
            var x264enc = Gst.ElementFactory.Make("x264enc", "x264enc");
            var flvmux = Gst.ElementFactory.Make("flvmux", "flvmux");
            flvmux.SetProperty("streamable", new GLib.Value(true));
            var queue1 = Gst.ElementFactory.Make("queue", "queue1");
            var rtmpSink = Gst.ElementFactory.Make("rtmpsink", "rtmpsink");
            rtmpSink.SetProperty("location", new GLib.Value("rtmp://localhost/live"));
            _pipeline.Add(testsrc, queue, videoconvert, x264enc, flvmux, queue1, rtmpSink);
            if (!Gst.Element.Link(testsrc, queue, videoconvert, x264enc, flvmux, queue1, rtmpSink))
            {
                Console.WriteLine("Not all elements could be linked.");
                return;
            }

            Play();
        }
    }
}
