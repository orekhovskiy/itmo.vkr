using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestNetCoreConsole.GstWorkers
{
    class AppSrcWorker
    {
        private Gst.Element _appsrc;
        private Gst.Element _videoconvert;

        public AppSrcWorker()
        {
            _appsrc = Gst.ElementFactory.Make("appsrc", "source");
            _videoconvert = Gst.ElementFactory.Make("videoconvert", "videoconvert");

        }

    }
}
