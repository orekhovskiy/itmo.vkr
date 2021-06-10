using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestNetCoreConsole.GstInteractors
{
    class TestSrcToVideoMixerGstInteractor : AbstractGstInteractor
    {
        public override void Interact()
        {
            var source1 = Gst.ElementFactory.Make("videotestsrc", "source1");
            var source2 = Gst.ElementFactory.Make("videotestsrc", "source2");
            source1.SetProperty("pattern", new GLib.Value(0));
            source2.SetProperty("pattern", new GLib.Value(1));
            var filter1 = Gst.ElementFactory.Make("capsfilter", "filter1");
            var filter2 = Gst.ElementFactory.Make("capsfilter", "filter2");
            var csp1 = Gst.ElementFactory.Make("videoconvert", "csp1");
            var csp2 = Gst.ElementFactory.Make("videoconvert", "csp2");
            var videobox1 = Gst.ElementFactory.Make("videobox", "videobox1");
            var videobox2 = Gst.ElementFactory.Make("videobox", "videobox2");
            var filtercaps = Gst.Global.CapsFromString("video/x-raw, format=yuv, width=200, height=100");
            filter1.SetProperty("caps", new GLib.Value(filtercaps));
            filter2.SetProperty("caps", new GLib.Value(filtercaps));
            videobox1.SetProperty("border-alpha", new GLib.Value(0));
            videobox1.SetProperty("top", new GLib.Value(0));
            videobox1.SetProperty("left", new GLib.Value(0));
            videobox2.SetProperty("border-alpha", new GLib.Value(0));
            videobox2.SetProperty("top", new GLib.Value(0));
            videobox2.SetProperty("left", new GLib.Value(-200));
            var mixer = Gst.ElementFactory.Make("videomixer", "mixer");
            var sink = Gst.ElementFactory.Make("autovideosink", "sink");

            _pipeline.Add(source1, filter1, videobox1, mixer, csp1, sink, 
                          source2, filter2, videobox2, csp2);

            if (!Gst.Element.Link(source1, filter1, csp1, videobox1, mixer)||
                !Gst.Element.Link(source2, filter2, csp2, videobox2, mixer)||
                !Gst.Element.Link(mixer, sink))
            {
                Log("Not all elements could be linked.");
            }

            var bus = _pipeline.Bus;
            bus.AddSignalWatch();
            bus.Message += HandleMessage;

            _pipeline.SetState(Gst.State.Playing);
            _loop.Run();

            bus.Unref();
            _pipeline.SetState(Gst.State.Null);
            _pipeline.Unref(); 

            /*var filter1 = Gst.ElementFactory.Make("capsfilter", "filter1");
            var filter2 = Gst.ElementFactory.Make("capsfilter", "filter2");
            var videobox1 = Gst.ElementFactory.Make("videobox", "videobox1");
            var videobox2 = Gst.ElementFactory.Make("videobox", "videobox2");
            var mixer = Gst.ElementFactory.Make("videomixer", "mixer");
            var clrspace = Gst.ElementFactory.Make("videoconvert", "clrspace");
            var sink = Gst.ElementFactory.Make("autovideosink", "sink");

            var mixerSinkPadTemplate = mixer.GetPadTemplate("sink_%u");
            var mixerSinkPad = mixer.RequestPad(mixerSinkPadTemplate);
            var sinkPad = clrspace.GetStaticPad("src");
            sinkPad.Link(mixerSinkPad);
            if (_pipeline == null || source1 == null || source2 == null || filter1 == null || filter2 == null ||
                videobox1 == null || videobox2 == null || mixer == null || clrspace == null || sink == null)
            {
                Log("Not all elements could be initialized.");
                return;
            }

            var filtercaps = Gst.Global.CapsFromString("video/x-raw, format=yuv, width=200, height=100");
            filter1.SetProperty("caps", new GLib.Value(filtercaps));
            filter2.SetProperty("caps", new GLib.Value(filtercaps));
            videobox1.SetProperty("border-alpha", new GLib.Value(0));
            videobox1.SetProperty("top", new GLib.Value(0));
            videobox1.SetProperty("left", new GLib.Value(0));
            videobox2.SetProperty("border-alpha", new GLib.Value(0));
            videobox2.SetProperty("top", new GLib.Value(0));
            videobox2.SetProperty("left", new GLib.Value(-200));

            source1.SetProperty("pattern", new GLib.Value(0));
            source2.SetProperty("pattern", new GLib.Value(1));

            var bus = _pipeline.Bus;
            bus.AddSignalWatch();
            bus.Message += HandleMessage;

            _pipeline.Add(source1, filter1, videobox1, mixer, clrspace, sink, source2, filter2, videobox2);
            if (!Gst.Element.Link(source1, filter1, videobox1, mixer, clrspace, sink) ||
                !Gst.Element.Link(source2, filter2, videobox2, mixer, clrspace, sink))
            {
                Log("Not all elements could be linked.");
            }
            _pipeline.SetState(Gst.State.Playing);
            _loop.Run();

            bus.Unref();
            _pipeline.SetState(Gst.State.Null);
            _pipeline.Unref();*/
        }
    }
}
