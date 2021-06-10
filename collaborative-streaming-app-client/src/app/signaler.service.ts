import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class SignalerService {
  readonly signalerOptions = {
    serverAddress: "http://127.0.0.1:3000/",
    localPeerId: "id1",
    remotePeerId: "id2",
  }
  
  constructor(private http: HttpClient) { }

  public sendMessage(msg) {

  }

  public poll(callback) {

  }
}
