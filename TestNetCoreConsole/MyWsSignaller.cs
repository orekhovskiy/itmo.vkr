using Microsoft.MixedReality.WebRTC;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TestNetCoreConsole
{
    class MyWsSignaller : NotifierBase
    {
        [Serializable]
        public class Message
        {
            public enum WireMessageType
            {
                Unknown = 0,
                Offer = 1,
                Answer = 2,
                Ice = 3
            }

            /// <summary>
            /// Convert a message type from <see xref="string"/> to <see cref="WireMessageType"/>.
            /// </summary>
            /// <param name="stringType">The message type as <see xref="string"/>.</param>
            /// <returns>The message type as a <see cref="WireMessageType"/> object.</returns>
            public static WireMessageType WireMessageTypeFromString(string stringType)
            {
                if (string.Equals(stringType, "offer", StringComparison.OrdinalIgnoreCase))
                {
                    return WireMessageType.Offer;
                }
                else if (string.Equals(stringType, "answer", StringComparison.OrdinalIgnoreCase))
                {
                    return WireMessageType.Answer;
                }
                throw new ArgumentException($"Unkown signaler message type '{stringType}'");
            }

            /// <summary>
            /// Convert an SDP message type to a serialized node-dss message type.
            /// </summary>
            /// <param name="type">The SDP message type to convert.</param>
            /// <returns>The equivalent node-dss serialized message type.</returns>
            public static WireMessageType TypeFromSdpMessageType(SdpMessageType type)
            {
                switch (type)
                {
                    case SdpMessageType.Offer: return WireMessageType.Offer;
                    case SdpMessageType.Answer: return WireMessageType.Answer;
                }
                throw new ArgumentException($"Invalid SDP message type '{type}'.");
            }

            /// <summary>
            /// Create a node-dss message from an existing SDP offer or answer message.
            /// </summary>
            /// <param name="message">The SDP message to serialize.</param>
            /// <returns>The newly create node-dss message containing the serialized SDP message.
            public static Message FromSdpMessage(SdpMessage message)
            {
                return new Message
                {
                    MessageType = TypeFromSdpMessageType(message.Type),
                    Data = message.Content,
                    IceDataSeparator = "|"
                };
            }

            /// <summary>
            /// Create a node-dss message from an existing ICE candidate.
            /// </summary>
            /// <param name="candidate">The ICE candidate to serialize.</param>
            /// <returns>The newly create node-dss message containing the serialized ICE candidate.</returns>
            public static Message FromIceCandidate(IceCandidate candidate)
            {
                return new Message
                {
                    MessageType = WireMessageType.Ice,
                    Data = $"{candidate.Content}|{candidate.SdpMlineIndex}|{candidate.SdpMid}",
                    IceDataSeparator = "|"
                };
            }

            /// <summary>
            /// Convert the current SDP message back to an <see cref="SdpMessage"/> object.
            /// </summary>
            /// <returns>The newly created <see cref="SdpMessage"/> object corresponding to the current message.</returns>
            public SdpMessage ToSdpMessage()
            {
                if ((MessageType != WireMessageType.Offer) && (MessageType != WireMessageType.Answer))
                {
                    throw new InvalidOperationException("The node-dss message it not an SDP message.");
                }
                return new SdpMessage
                {
                    Type = (MessageType == WireMessageType.Offer ? SdpMessageType.Offer : SdpMessageType.Answer),
                    Content = Data
                };
            }

            /// <summary>
            /// Convert the current ICE message back to an <see cref="IceCandidate"/> object.
            /// </summary>
            /// <returns>The newly created <see cref="IceCandidate"/> object corresponding to the current message.</returns>
            public IceCandidate ToIceCandidate()
            {
                if (MessageType != WireMessageType.Ice)
                {
                    throw new InvalidOperationException("The node-dss message it not an ICE candidate message.");
                }
                var parts = Data.Split(new string[] { IceDataSeparator }, StringSplitOptions.None); //, StringSplitOptions.RemoveEmptyEntries);
                // Note the arguments order; candidate content is first in the node-dss protocol.
                // The order of the arguments matches the order in which they are serialized in FromIceCandidate().
                return new IceCandidate
                {
                    SdpMid = parts[2],
                    SdpMlineIndex = int.Parse(parts[1]),
                    Content = parts[0]
                };
            }

            /// <summary>
            /// Message type.
            /// </summary>
            public WireMessageType MessageType;

            /// <summary>
            /// Primary message content, which depends on the type of message.
            /// </summary>
            public string Data;

            /// <summary>
            /// Data separator for ICE serialization.
            /// </summary>
            public string IceDataSeparator;
        }

        public int PollTimeMs = 500;

        public bool IsPolling { get { lock (_pollingLock) { return _isPolling; } } }

        public event Action OnPollingDone;
        public event Action OnConnect;
        public event Action OnDisconnect;
        public event Action<Message> OnMessage;
        public event Action<Exception> OnFailure;

        private int _connectedEventFired = 0;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isPolling = false;
        private readonly object _pollingLock = new object();

        readonly WebSocketContext _wsCtx;
        readonly WebSocket _ws;

        public MyWsSignaller(WebSocketContext wsCtx)
        {
            _wsCtx = wsCtx;
            _ws = wsCtx.WebSocket;
        }

        private async Task SendMessageImpl(string jsonMsg)
        {
            //var msgContent = Encoding.UTF8.GetBytes(jsonMsg);
            //await _ws.SendAsync(BitConverter.GetBytes(msgContent.Length), WebSocketMessageType.Binary, false, _cancellationTokenSource.Token);
            //await _ws.SendAsync(msgContent, WebSocketMessageType.Binary, true, _cancellationTokenSource.Token);
            await _ws.SendAsync(Encoding.UTF8.GetBytes(jsonMsg), WebSocketMessageType.Text, true, _cancellationTokenSource.Token);
        }

        public Task SendMessageAsync(Message message)
        {
            var jsonMsg = JsonConvert.SerializeObject(message);

            var task = this.SendMessageImpl(jsonMsg).ContinueWith((postTask) =>
            {
                if (postTask.Exception != null)
                {
                    OnFailure?.Invoke(postTask.Exception);
                    OnDisconnect?.Invoke();
                }
            });

            // Atomic read
            if (Interlocked.CompareExchange(ref _connectedEventFired, 1, 1) == 1)
            {
                return task;
            }

            // On first successful message, fire the OnConnect event
            return task.ContinueWith((prevTask) =>
            {
                if (prevTask.Exception != null)
                {
                    OnFailure?.Invoke(prevTask.Exception);
                }

                if (prevTask.IsCompletedSuccessfully)
                {
                    // Only invoke if this task was the first one to change the value, because
                    // another task may have completed faster in the meantime and already invoked.
                    if (0 == Interlocked.Exchange(ref _connectedEventFired, 1))
                    {
                        OnConnect?.Invoke();
                    }
                }
            });
        }

        /// <summary>
        /// Start polling the node-dss server with a GET request, and continue to do so
        /// until <see cref="StopPollingAsync"/> is called.
        /// </summary>
        /// <returns>Returns <c>true</c> if polling effectively started with this call.</returns>
        /// <remarks>
        /// The <see cref="LocalPeerId"/> field must be set before calling this method.
        /// This method can safely be called multiple times, and will do nothing if
        /// polling is already underway or waiting to be stopped.
        /// </remarks>
        public bool StartPollingAsync(CancellationToken cancellationToken = default)
        {
            lock (_pollingLock)
            {
                if (_isPolling)
                {
                    return false;
                }
                _isPolling = true;
                _cancellationTokenSource = new CancellationTokenSource();
            }
            RaisePropertyChanged("IsPolling");

            // Build the GET polling request
            // string requestUri = $"{_httpServerAddress}data/{LocalPeerId}";

            long lastPollTimeTicks = DateTime.UtcNow.Ticks;
            long pollTimeTicks = TimeSpan.FromMilliseconds(PollTimeMs).Ticks;

            var masterTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token, cancellationToken);
            var masterToken = masterTokenSource.Token;
            masterToken.Register(() =>
            {
                lock (_pollingLock)
                {
                    _isPolling = false;
                    _cancellationTokenSource.Dispose();
                    _cancellationTokenSource = null;
                }
                RaisePropertyChanged("IsPolling");
                Interlocked.Exchange(ref _connectedEventFired, 0);
                OnDisconnect?.Invoke();
                OnPollingDone?.Invoke();
                masterTokenSource.Dispose();
            });

            // Prepare the repeating poll task.
            // In order to poll at the specified frequency but also avoid overlapping requests,
            // use a repeating task which re-schedule itself on completion, either immediately
            // if the polling delay is elapsed, or at a later time otherwise.
            async void PollServer()
            {
                try
                {
                    // Polling loop
                    while (true)
                    {
                        masterToken.ThrowIfCancellationRequested();

                        // Send GET request to DSS server.
                        lastPollTimeTicks = DateTime.UtcNow.Ticks;
                        //HttpResponseMessage response = await _httpClient.GetAsync(requestUri,
                        //    HttpCompletionOption.ResponseHeadersRead, masterToken);

                        //var lenBuff = new byte[4];
                        //var wsr1 = await _ws.ReceiveAsync(lenBuff, cancellationToken);
                        //var dataBuff = new byte[BitConverter.ToInt32(lenBuff)];
                        //var wsr2 = await _ws.ReceiveAsync(dataBuff, cancellationToken);
                        //var jsonMsg = Encoding.UTF8.GetString(dataBuff);
                        WebSocketReceiveResult wsr;
                        var result = new LinkedList<(byte[] buff, int len)>();
                        do
                        {
                            var buff = new byte[4096];
                            wsr = await _ws.ReceiveAsync(buff, cancellationToken);
                            result.AddLast((buff, wsr.Count));
                        } while (!wsr.EndOfMessage);

                        var data = new byte[result.Sum(r => r.len)];
                        var pos = 0;
                        foreach (var part in result)
                        {
                            Array.Copy(part.buff, 0, data, pos, part.len);
                            pos += part.len;
                        }

                        var jsonMsg = Encoding.UTF8.GetString(data);

                        System.Diagnostics.Debug.Print(jsonMsg);
                        // On first successful HTTP request, raise the connected event
                        if (0 == Interlocked.Exchange(ref _connectedEventFired, 1))
                        {
                            OnConnect?.Invoke();
                        }

                        masterToken.ThrowIfCancellationRequested();

                        // In order to avoid exceptions in GetStreamAsync() when the server returns a non-success status code (e.g. 404),
                        // first get the HTTP headers, check the status code, then if successful wait for content.
                        // if (response.IsSuccessStatusCode)
                        {
                            // string jsonMsg = await response.Content.ReadAsStringAsync();

                            masterToken.ThrowIfCancellationRequested();

                            var jsonSettings = new JsonSerializerSettings
                            {
                                Error = (object s, ErrorEventArgs e) => throw new Exception("JSON error: " + e.ErrorContext.Error.Message)
                            };
                            Message msg = JsonConvert.DeserializeObject<Message>(jsonMsg, jsonSettings);
                            if (msg != null)
                            {
                                OnMessage?.Invoke(msg);
                            }
                            else
                            {
                                throw new Exception("Failed to deserialize signaler message from JSON.");
                            }
                        }

                        masterToken.ThrowIfCancellationRequested();

                        // Delay next loop iteration if current polling was faster than target poll duration
                        long curTime = DateTime.UtcNow.Ticks;
                        long deltaTicks = curTime - lastPollTimeTicks;
                        long remainTicks = pollTimeTicks - deltaTicks;
                        if (remainTicks > 0)
                        {
                            int waitTimeMs = new TimeSpan(remainTicks).Milliseconds;
                            await Task.Delay(waitTimeMs);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Manual cancellation via UI, do not report error
                }
                catch (Exception ex)
                {
                    OnFailure?.Invoke(ex);
                }
            }

            // Start the poll task immediately
            Task.Run(PollServer, masterToken);
            return true;
        }

        /// <summary>
        /// Asynchronously cancel the polling process. Once polling is actually stopped
        /// and can be restarted, the <see cref="OnPollingDone"/> event is fired.
        /// </summary>
        /// <returns>
        /// Returns <c>true</c> if polling cancellation was effectively initiated
        /// by this call.
        /// </returns>
        /// <remarks>
        /// This method can safely be called multiple times, and does nothing if not
        /// already polling or already waiting for polling to end.
        /// </remarks>
        public bool StopPollingAsync()
        {
            lock (_pollingLock)
            {
                if (_isPolling)
                {
                    // Note: cannot dispose right away, need to wait for end of all tasks.
                    _cancellationTokenSource?.Cancel();
                    return true;
                }
            }
            return false;
        }

    }

    public class NotifierBase : Component, INotifyPropertyChanged
    {
        /// <inheritdoc/>
        public event PropertyChangedEventHandler PropertyChanged;

        // private readonly CoreDispatcher _dispatcher;

        protected NotifierBase()
        {
           //  _dispatcher = Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher;
        }

        /// <summary>
        /// Try to set a property to a new value, and raise the <see cref="PropertyChanged"/> event if
        /// the value actually changed, or does nothing if not. Values are compared with the built-in
        /// <see cref="object.Equals(object, object)"/> method.
        /// </summary>
        /// <typeparam name="T">The property type.</typeparam>
        /// <param name="storage">Storage field for the property, whose value is overwritten with the new value.</param>
        /// <param name="value">New property value to set, which is possibly the same value it currently has.</param>
        /// <param name="propertyName">
        /// Property name. This is automatically inferred by the compiler if the method is called from
        /// within a property setter block <c>set { }</c>.
        /// </param>
        /// <returns>Return <c>true</c> if the property value actually changed, or <c>false</c> otherwise.</returns>
        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value))
            {
                return false;
            }
            storage = value;
            RaisePropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// Raise the <see cref="PropertyChanged"/> event for the given property name, taking care of dispatching
        /// the call to the appropriate thread.
        /// </summary>
        /// <param name="propertyName">Name of the property which changed.</param>
        protected void RaisePropertyChanged(string propertyName)
        {
            // The event must be raised on the UI thread
            //if (_dispatcher.HasThreadAccess)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
            //else
            //{
            //    _ = _dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            //        () => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
            //}
        }
    }
}
