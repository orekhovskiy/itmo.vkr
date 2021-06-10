using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestNetCoreConsole.GstInteractors
{
    class RtmpSrcToFakeSinkGstInteractor : AbstractGstInteractor
    {
        public override void Interact()
        {
            var rtmpsrc = Gst.ElementFactory.Make("rtmpsrc", "src");
            rtmpsrc.SetProperty("location", new GLib.Value("rtmp://localhost/live live=1"));
            var fakesink = Gst.ElementFactory.Make("fakesink", "sink");

            _pipeline.Add(rtmpsrc, fakesink);
            Gst.Element.Link(rtmpsrc, fakesink);
            Play();
        }
    }
}
