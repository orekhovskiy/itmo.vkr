using Gst;
using System;
using GLib;

namespace TestNetCoreConsole.GstInteractors
{
    public abstract class AbstractGstInteractor : IDisposable
    {
        protected Pipeline _pipeline;
        protected MainLoop _loop;
        protected bool _isLive;
        protected AbstractGstInteractor()
        {
            Environment.SetEnvironmentVariable("GST_DEBUG", "3");
            Gst.Application.Init();
            _loop = new MainLoop();
            _pipeline = new Pipeline("pipeline");
            GLib.ExceptionManager.UnhandledException += HandleUnhandled;
        }
        protected static void Log(object msg)
        {
            Console.WriteLine(msg.ToString());
        }
        protected void HandleMessage(object o, MessageArgs args)
        {
            var msg = args.Message;
            switch (msg.Type)
            {
                case MessageType.Error:
                    {
                        GLib.GException err;
                        string debug;

                        msg.ParseError(out err, out debug);
                        Console.WriteLine("Error: {0}", err.Message);

                        _pipeline.SetState(State.Ready);
                        _loop.Quit();
                        break;
                    }
                case MessageType.Eos:
                    // end-of-stream
                    _pipeline.SetState(State.Ready);
                    _loop.Quit();
                    break;
                case MessageType.Buffering:
                    {
                        int percent = 0;

                        // If the stream is live, we do not care about buffering.
                        if (_isLive) break;

                        percent = msg.ParseBuffering();
                        Console.WriteLine("Buffering ({0})", percent);
                        // Wait until buffering is complete before start/resume playing
                        if (percent < 100)
                            _pipeline.SetState(State.Paused);
                        else
                            _pipeline.SetState(State.Playing);
                        break;
                    }
                case MessageType.ClockLost:
                    // Get a new clock
                    _pipeline.SetState(State.Paused);
                    _pipeline.SetState(State.Playing);
                    break;
                default:
                    // Unhandled message
                    break;
            }
        }
        protected void Play(bool waitForEnd = true)
        {
            _pipeline.SetState(Gst.State.Playing);

            if (waitForEnd)
            {
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

            }
        }
        public abstract void Interact();
        private void HandleUnhandled(UnhandledExceptionArgs args)
        {
            var e = ((Exception)args.ExceptionObject);
            Log($"Exception occured: {e.Message}{e.StackTrace}");
        }

        public void Dispose()
        {
            // Free resources
            _pipeline.Bus.Unref();
            _pipeline.SetState(Gst.State.Null);
            _pipeline.Unref();
        }
    }
}
