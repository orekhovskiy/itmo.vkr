import { Component, OnInit } from '@angular/core';
import adapter from 'webrtc-adapter';
import { SignalerService } from '../signaler.service';

class Message {
  public constructor(
    public MessageType: number,
    public Data: string,
    public IceDataSeparator: string
  ) { }
}

@Component({
  selector: 'app-main',
  templateUrl: './main.component.html',
  styleUrls: ['./main.component.css']
})
export class MainComponent implements OnInit {

  private startButton;
  private callButton;
  private hangupButton;
  private startTime;
  private localVideo;
  private remoteVideo;
  private localStream;
  private pc1: RTCPeerConnection;
  // private pc2;
  readonly offerOptions = <RTCOfferOptions><any> {
    offerToReceiveAudio: 1,
    offerToReceiveVideo: 1
  };

  private _ws: WebSocket;

  constructor(private signaler: SignalerService) {
  }
  
  private setupWebSocket() {
    this._ws = new WebSocket('ws://localhost/mysvc/rtcsignaller');
    this._ws.onmessage = async (evt: MessageEvent) => {
      console.warn('received: ' + evt.data);
      const data: Message = JSON.parse(evt.data);
      
      switch (data.MessageType) {
        case 1:
          {
            let desc: RTCSessionDescriptionInit = {
              type: 'offer',
              sdp: data.Data 
            };
            try {
              await this.pc1.setRemoteDescription(desc);
              this.onSetRemoteSuccess(this.pc1);
            } catch (e) {
              this.onSetSessionDescriptionError(e);
            }
          } break;
        case 2:
          {
            let desc: RTCSessionDescriptionInit = {
              type: 'answer',
              sdp: data.Data 
            };
            try {
              await this.pc1.setRemoteDescription(desc);
              this.onSetRemoteSuccess(this.pc1);
            } catch (e) {
              this.onSetSessionDescriptionError(e);
            }
          } break;
        case 3:
          {
            let parts = data.Data.split(data.IceDataSeparator);
            let candidate = <RTCIceCandidateInit><any> {
              candidate: parts[0],
              sdpMid: parts[2],
              sdpMLineIndex: parts[1]
            };
            try {
              await this.pc1.addIceCandidate(candidate);
              this.onAddIceCandidateSuccess(this.pc1);
            } catch (e) {
              this.onAddIceCandidateError(this.pc1, e);
            }
            console.log(` ICE candidate:\n${candidate ? candidate.candidate : '(null)'}`);
          } break;
      }
    };
  }

  ngOnInit(): void {
    this.startButton = <HTMLInputElement>document.getElementById('startButton');
    this.callButton = <HTMLInputElement>document.getElementById('callButton');
    this.hangupButton = <HTMLInputElement>document.getElementById('hangupButton');
    this.localVideo = <HTMLVideoElement>document.getElementById('localVideo');
    this.remoteVideo = <HTMLVideoElement>document.getElementById('remoteVideo');
  }

  logVideoEvent(type) {
    console.log(type);
    
    let element;
    switch(type) {
      case 'Local':
        element = document.getElementById('localVideo');
        break;
      case 'Remote':
        element = document.getElementById('remoteVideo');
        break;
      default:
        element = null;
        break;
    }
    console.log(`${type} video videoWidth: ${element.videoWidth}px,  videoHeight: ${element.videoHeight}px`);
  }

  logResize() {
    console.log(`Remote video size changed to ${this.remoteVideo.videoWidth}x${this.remoteVideo.videoHeight}`);
    if (this.startTime) {
      const elapsedTime = window.performance.now() - this.startTime;
      console.log('Setup time: ' + elapsedTime.toFixed(3) + 'ms');
      this.startTime = null;
    }
  }

  // getName(pc) {
  //   return (pc === this.pc1) ? 'pc1' : 'pc2';
  // }
  
  // getOtherPc(pc) {
  //   return (pc === this.pc1) ? this.pc2 : this.pc1;
  // }
  
  async start() {
    this.setupWebSocket();
    console.log('Requesting local stream');
    this.startButton.disabled = true;
    try {
      const stream = await navigator.mediaDevices.getUserMedia({audio: true, video: true});
      console.log('Received local stream');
      this.localVideo.srcObject = stream;
      this.localStream = stream;
      this.callButton.disabled = false;
    } catch (e) {
      console.log(e);      
      alert(`getUserMedia() error: ${e.name}`);
    }
  }

  async call() {
    this.callButton.disabled = true;
    this.hangupButton.disabled = false;
    console.log('Starting call');
    this.startTime = window.performance.now();
    const videoTracks = this.localStream.getVideoTracks();
    const audioTracks = this.localStream.getAudioTracks();
    if (videoTracks.length > 0) {
      console.log(`Using video device: ${videoTracks[0].label}`);
    }
    if (audioTracks.length > 0) {
      console.log(`Using audio device: ${audioTracks[0].label}`);
    }
    const configuration: RTCConfiguration = {
      iceServers: [
        {
          urls: "stun:stun.l.google.com:19302"
        }
      ]
    };
    console.log('RTCPeerConnection configuration:', configuration);
    this.pc1 = new RTCPeerConnection(configuration);
    console.log('Created local peer connection object pc1');
    this.pc1.addEventListener('icecandidate', e => this.onIceCandidate(this.pc1, e));
    // this.pc2 = new RTCPeerConnection(configuration);
    console.log('Created remote peer connection object pc2');
    //this.pc2.addEventListener('icecandidate', e => this.onIceCandidate(this.pc2, e));
    this.pc1.addEventListener('iceconnectionstatechange', e => this.onIceStateChange(this.pc1, e));
    // this.pc2.addEventListener('iceconnectionstatechange', e => this.onIceStateChange(this.pc2, e));
    // this.pc2.addEventListener('track', e => this.gotRemoteStream(this, e));
  
    this.localStream.getTracks().forEach(track => this.pc1.addTrack(track, this.localStream));
    console.log('Added local stream to pc1');
  
    try {
      console.log('pc1 createOffer start');
      const offer = await this.pc1.createOffer(this.offerOptions);
      await this.onCreateOfferSuccess(offer);
    } catch (e) {
      this.onCreateSessionDescriptionError(e);
    }
  }

  onCreateSessionDescriptionError(error) {
    console.log(`Failed to create session description: ${error.toString()}`);
  }

  async onCreateOfferSuccess(desc: RTCSessionDescriptionInit) {
    console.log(`Offer from pc1\n${desc.sdp}`);
    console.log('pc1 setLocalDescription start');
    try {
      await this.pc1.setLocalDescription(desc);
      this.onSetLocalSuccess(this.pc1);
    } catch (e) {
      this.onSetSessionDescriptionError();
    }
  

    console.log('pc2 setRemoteDescription start');
    try {
      //await this.pc2.setRemoteDescription(desc);
      
      let cc = new Message(1, desc.sdp, '|');      
      this._ws.send(JSON.stringify(cc));
      //await (this.getOtherPc(pc).addIceCandidate(event.candidate))

      // this.onSetRemoteSuccess(this.pc2);
    } catch (e) {
      this.onSetSessionDescriptionError(e);
    }
  
    // // console.log('pc2 createAnswer start');
    // // Since the 'remote' side has no media stream we need
    // // to pass in the right constraints in order for it to
    // // accept the incoming offer of audio and video.
    // try {
    //   const answer = await this.pc2.createAnswer();
    //   await this.onCreateAnswerSuccess(answer);
    // } catch (e) {
    //   this.onCreateSessionDescriptionError(e);
    // }
  }

  onSetLocalSuccess(pc) {
    console.log(` setLocalDescription complete`);
  }
  
  onSetRemoteSuccess(pc) {
    console.log(` setRemoteDescription complete`);
  }
  
  onSetSessionDescriptionError(error?) {
    console.log(`Failed to set session description: ${error.toString()}`);
  }
  
  gotRemoteStream(ctx, e) {
    if (ctx.remoteVideo.srcObject !== e.streams[0]) {
      this.remoteVideo.srcObject = e.streams[0];
      console.log('pc2 received remote stream');
    }
  }
  
  async onCreateAnswerSuccess(desc) {
    console.log(`Answer from pc2:\n${desc.sdp}`);
    console.log('pc2 setLocalDescription start');
    try {
      //await this.pc2.setLocalDescription(desc);
      // this.onSetLocalSuccess(this.pc2);
    } catch (e) {
      this.onSetSessionDescriptionError(e);
    }
    console.log('pc1 setRemoteDescription start');
    try {
      await this.pc1.setRemoteDescription(desc);
      this.onSetRemoteSuccess(this.pc1);
    } catch (e) {
      this.onSetSessionDescriptionError(e);
    }
  }
  
  async onIceCandidate(pc, event: RTCPeerConnectionIceEvent) {
    try {
      let candidateInfo = event.candidate.toJSON();
      let SdpMlineIndex = candidateInfo.sdpMLineIndex;
      let SdpMid = candidateInfo.sdpMid;
      let Content = candidateInfo.candidate;
      let cc = new Message(3, `${Content}|${SdpMlineIndex}|${SdpMid}`, '|');      
      this._ws.send(JSON.stringify(cc));
      //await (this.getOtherPc(pc).addIceCandidate(event.candidate));
      this.onAddIceCandidateSuccess(pc);
    } catch (e) {
      this.onAddIceCandidateError(pc, e);
    }
    console.log(` ICE candidate:\n${event.candidate ? event.candidate.candidate : '(null)'}`);
    // console.log(`${this.getName(pc)} ICE candidate:\n${event.candidate ? event.candidate.candidate : '(null)'}`);
  }
  
  onAddIceCandidateSuccess(pc) {
    // console.log(`${this.getName(pc)} addIceCandidate success`);
    console.log(`addIceCandidate success`);
  }
  
  onAddIceCandidateError(pc, error) {
    // console.log(`${this.getName(pc)} failed to add ICE Candidate: ${error.toString()}`);
    console.log(`failed to add ICE Candidate: ${error.toString()}`);
  }
  
  onIceStateChange(pc, event) {
    if (pc) {
      // console.log(`${this.getName(pc)} ICE state: ${pc.iceConnectionState}`);
      console.log('ICE state change event: ', event);
    }
  }
  
  hangup() {
    console.log('Ending call');
    this.pc1.close();
    // this.pc2.close();
    this.pc1 = null;
    //this.pc2 = null;
    this.hangupButton.disabled = true;
    this.callButton.disabled = false;
  }
}
