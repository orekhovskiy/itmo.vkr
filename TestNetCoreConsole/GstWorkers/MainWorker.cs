using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestNetCoreConsole.GstWorkers
{
    static class MainWorker
    {
        private static Gst.Pipeline _pipeline;
        private static Gst.Element _videomixer;
        private static Gst.Element _queue;
        private static Gst.Element _videoconvert;
        private static Gst.Element _x264enc;
        private static Gst.Element _flvmux;
        private static Gst.Element _queue1;
        private static Gst.Element _rtmpsink;
        public static void Work()
        {
            _pipeline = new Gst.Pipeline("pipeline");
            _videomixer = Gst.ElementFactory.Make("videomixer", "mix");
            _queue = Gst.ElementFactory.Make("queue", "queue");
            _videoconvert = Gst.ElementFactory.Make("videoconvert", "videoconvert");
            _x264enc = Gst.ElementFactory.Make("x264enc", "x264enc");
            _flvmux = Gst.ElementFactory.Make("flvmux", "flvmux");
            _queue1 = Gst.ElementFactory.Make("queue", "queue1");
            _rtmpsink = Gst.ElementFactory.Make("rtmpsink", "rtmpsink");
            _flvmux.SetProperty("streamable", new GLib.Value(true));
            _rtmpsink.SetProperty("location", new GLib.Value("rtmp://localhost/live"));

            _pipeline.Add(_videomixer, _queue, _videoconvert, _x264enc, _flvmux, _queue1, _rtmpsink);
            if (!Gst.Element.Link(_videomixer, _queue, _videoconvert, _x264enc, _flvmux, _queue1, _rtmpsink))
            {
                Log("Not all elements could be linked");
            }
            Play();
        }
        public static void Dispose()
        {
        }

        public static Gst.Pad RequestPad()
        {
            var mixerSinkPadTemplate = _videomixer.GetPadTemplate("sink_%u");
            var pad = _videomixer.RequestPad(mixerSinkPadTemplate);
            return pad;
        }

        /*public static bool ReleasePad()
        {

        }*/

        public static bool AddToPipelineAndLink(params Gst.Element[] elements)
        {
            try
            {
                _pipeline.Add(elements);
                if (!Gst.Element.Link(elements))
                {
                    return false;
                }
                return true;
            }
            catch (Exception e)
            {
                Log(e.Message);
                return false;
            }
        }

        /*public static bool DisposeElements(params string[] elementNames)
        {
            _pipeline.
        }*/

        private static void Log(object msg)
        {
            Debugger.Log(0, "", msg.ToString());
        }
        private static void Play()
        {
            _pipeline.SetState(Gst.State.Playing);
            // Wait until error or EOS
            var bus = _pipeline.Bus;
            var msg = bus.TimedPopFiltered(Gst.Constants.CLOCK_TIME_NONE, Gst.MessageType.Eos | Gst.MessageType.Error);

            if (msg != null)
            {
                GLib.GException error;
                string debug;
                msg.ParseError(out error, out debug);
                Log(error.Message);
            }

            // Free resources
            bus.Unref();
            _pipeline.SetState(Gst.State.Null);
            _pipeline.Unref();
        }
    }
}
