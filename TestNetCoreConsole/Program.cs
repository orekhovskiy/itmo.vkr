using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using Microsoft.MixedReality.WebRTC;
using TestNetCoreConsole.GstInteractors;

namespace TestNetCoreConsole
{
    class Program
    {
        static async Task Main(string[] args)
        {
            bool working = false;
            using (var interactor = new AppSrcToAutoVideoSinkGstInteractor())
            using (var svc = new MyWebSvc("http://127.0.0.1/mysvc/"))
            {
                svc.OnWebSocketConnection += ws =>
                {
                    var signaller = new MyWsSignaller(ws);

                    var receiver = new MyWebRtcStreamReceiver(signaller);
                    receiver.OnFrameReceived += frame =>
                      {
                          interactor.HandleFrame(frame);
                          Console.WriteLine("Frame received");
                          if (!working)
                          {
                              working = true;
                              interactor.Interact($"video/x-raw, width={frame.width}, height={frame.height}, format=I420, framerate=30/1", false);
                          }
                      };

                    receiver.Start();
                };
                while (Console.ReadKey().Key != ConsoleKey.Q) ;
            }
            return;

            bool needVideo = Array.Exists(args, arg => (arg == "-v") || (arg == "--video"));
            bool needAudio = Array.Exists(args, arg => (arg == "-a") || (arg == "--audio"));

            AudioTrackSource microphoneSource = null;
            VideoTrackSource webcamSource = null;
            Transceiver audioTransceiver = null;
            Transceiver videoTransceiver = null;
            LocalAudioTrack localAudioTrack = null;
            LocalVideoTrack localVideoTrack = null;
            try
            {
                // Asynchronously retrieve a list of available video capture devices (webcams).
                var deviceList = await DeviceVideoTrackSource.GetCaptureDevicesAsync();

                // For example, print them to the standard output
                foreach (var device in deviceList)
                {
                    Console.WriteLine($"Found webcam {device.name} (id: {device.id})");
                }
                using var pc = new PeerConnection();
                var config = new PeerConnectionConfiguration
                {
                    IceServers = new List<IceServer> {
                    new IceServer{ Urls = { "stun:stun.l.google.com:19302" } }
                }
                };
                await pc.InitializeAsync(config);
                Console.WriteLine("Peer connection initialized.");
                webcamSource = await DeviceVideoTrackSource.CreateAsync();
                var videoTrackConfig = new LocalVideoTrackInitConfig
                {
                    trackName = "webcam_track"
                };
                localVideoTrack = LocalVideoTrack.CreateFromSource(webcamSource, videoTrackConfig);
                microphoneSource = await DeviceAudioTrackSource.CreateAsync();
                var audioTrackConfig = new LocalAudioTrackInitConfig
                {
                    trackName = "microphone_track"
                };

                // Record video from local webcam, and send to remote peer
                if (needVideo)
                {
                    Console.WriteLine("Opening local webcam...");
                    localVideoTrack = LocalVideoTrack.CreateFromSource(webcamSource, videoTrackConfig);
                    videoTransceiver = pc.AddTransceiver(MediaKind.Video);
                    videoTransceiver.DesiredDirection = Transceiver.Direction.SendReceive;
                    videoTransceiver.LocalVideoTrack = localVideoTrack;
                }

                // Record audio from local microphone, and send to remote peer
                if (needAudio)
                {
                    Console.WriteLine("Opening local microphone...");
                    LocalAudioTrack.CreateFromSource(microphoneSource, audioTrackConfig);
                    audioTransceiver = pc.AddTransceiver(MediaKind.Audio);
                    audioTransceiver.DesiredDirection = Transceiver.Direction.SendReceive;
                    audioTransceiver.LocalAudioTrack = localAudioTrack;
                }

                var signaler = new NamedPipeSignaler(pc, "testpipe");
                signaler.SdpMessageReceived += async (SdpMessage message) =>
                {
                    // Note: we use 'await' to ensure the remote description is applied
                    // before calling CreateAnswer(). Failing to do so will prevent the
                    // answer from being generated, and the connection from establishing.
                    await pc.SetRemoteDescriptionAsync(message);
                    if (message.Type == SdpMessageType.Offer)
                    {
                        pc.CreateAnswer();
                    }
                };

                signaler.IceCandidateReceived += (IceCandidate candidate) =>
                {
                    pc.AddIceCandidate(candidate);
                };

                await signaler.StartAsync();

                pc.Connected += () =>
                {
                    Console.WriteLine("PeerConnection: connected.");
                };

                pc.IceStateChanged += (IceConnectionState newState) =>
                {
                    Console.WriteLine($"ICE state: {newState}");
                };

                pc.AudioTrackAdded += (RemoteAudioTrack track) =>
                {
                    track.AudioFrameReady += RemoteAudioFrameReady;
                };

                pc.VideoTrackAdded += (RemoteVideoTrack track) =>
                {
                    Console.WriteLine("Track added");
                    track.I420AVideoFrameReady += RemoteVideoFrameReady;
                };

                if (needVideo)
                {
                    Console.WriteLine("NEED VIDEO: Connecting to remote peer...");
                    pc.CreateOffer();
                }
                else
                {
                    Console.WriteLine("Waiting for offer from remote peer...");
                }

                Console.WriteLine("Press a key to terminate the application...");
                Console.ReadKey(true);
                signaler.Stop();
                Console.WriteLine("Program termined.");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            localAudioTrack?.Dispose();
            localVideoTrack?.Dispose();
            microphoneSource?.Dispose();
            webcamSource?.Dispose();
        }

        private static void Svc_OnWebSocketConnection(System.Net.WebSockets.WebSocketContext obj)
        {
            throw new NotImplementedException();
        }

        private static void RemoteAudioFrameReady(AudioFrame frame)
        {

        }

        public static void RemoteVideoFrameReady(I420AVideoFrame frame)
        {

        }
    }
}
