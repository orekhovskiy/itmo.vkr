using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestNetCoreConsole.GstInteractors
{
    class ThreeTestSrctoVideoMixerToRtmpGstInteractor : AbstractGstInteractor
    {
        public override void Interact()
        {
            var rtmpsrc = Gst.ElementFactory.Make("rtmpsrc", "rtmpsrc");
            var flvdemux = Gst.ElementFactory.Make("flvdemux", "flvdemux");
            var avdec_h264 = Gst.ElementFactory.Make("avdec_h264", "avdec_h264");
            var queue = Gst.ElementFactory.Make("queue", "queue");
            var tee = Gst.ElementFactory.Make("tee", "tee");
            flvdemux.SetProperty("streamable", new GLib.Value(true));
            rtmpsrc.SetProperty("location", new GLib.Value("rtmp://localhost/live"));

            _pipeline.Add(rtmpsrc, flvdemux, avdec_h264, queue, tee);
            if (!Gst.Element.Link(rtmpsrc, flvdemux, avdec_h264, queue, tee))
            {
                Log("Not all elements could be linked");
            }

            var source1 = Gst.ElementFactory.Make("videotestsrc", "source1");
            var capsfilter1 = Gst.ElementFactory.Make("capsfilter", "capsfilter1");
            var filtercaps1 = Gst.Global.CapsFromString("video/x-raw, width=200, height=100");
            capsfilter1.SetProperty("caps", new GLib.Value(filtercaps1));
            var alpha1 = Gst.ElementFactory.Make("alpha", "alpha1");
            alpha1.SetProperty("alpha", new GLib.Value(1.0));
            var videobox1 = Gst.ElementFactory.Make("videobox", "videobox1");
            _pipeline.Add(source1, capsfilter1, alpha1, videobox1);
            if (!Gst.Element.Link(source1, capsfilter1, alpha1, videobox1))
            {
                Log("Not all elements could be linked");
            }
            var mixerSinkPadTemplate1 = videomixer.GetPadTemplate("sink_%u");
            var mixerSinkPad1 = videomixer.RequestPad(mixerSinkPadTemplate1);
            mixerSinkPad1.SetProperty("ypos", new GLib.Value(0));
            mixerSinkPad1.SetProperty("xpos", new GLib.Value(0));
            var srcpad1 = videobox1.GetStaticPad("src");
            srcpad1.Link(mixerSinkPad1);

            var source2 = Gst.ElementFactory.Make("videotestsrc", "source2");
            var capsfilter2 = Gst.ElementFactory.Make("capsfilter", "capsfilter2");
            var filtercaps2 = Gst.Global.CapsFromString("video/x-raw, width=200, height=100");
            capsfilter2.SetProperty("caps", new GLib.Value(filtercaps2));
            var alpha2 = Gst.ElementFactory.Make("alpha", "alpha2");
            alpha2.SetProperty("alpha", new GLib.Value(1.0));
            var videobox2 = Gst.ElementFactory.Make("videobox", "videobox2");
            _pipeline.Add(source2, capsfilter2, alpha2, videobox2);
            if (!Gst.Element.Link(source2, capsfilter2, alpha2, videobox2))
            {
                Log("Not all elements could be linked");
            }

            var mixerSinkPadTemplate2 = videomixer.GetPadTemplate("sink_%u");
            var mixerSinkPad2 = videomixer.RequestPad(mixerSinkPadTemplate2);
            mixerSinkPad2.SetProperty("ypos", new GLib.Value(100));
            mixerSinkPad2.SetProperty("xpos", new GLib.Value(0));
            var srcpad2 = videobox2.GetStaticPad("src");
            srcpad2.Link(mixerSinkPad2);

            Play(); 
        }
    }
}
