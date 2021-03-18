using System;
using System.Threading;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.Events;
using WebSocketSharp;
using WebSocketSharp.Server;

public class SignalingServer
{
    public UnityEvent<string> OnOpen = new UnityEvent<string>();
    public UnityEvent<string, string> OnMessage = new UnityEvent<string, string>();
    public UnityEvent<string, ushort, string> OnClose = new UnityEvent<string, ushort, string>();
    public UnityEvent<string, string> OnError = new UnityEvent<string, string>();
    private SynchronizationContext context;
    private WebSocketServer server;
    private class signalingBehaviour : WebSocketBehavior
    {
        public delegate void dlgOnOpen(string id);
        public delegate void dlgOnTextMessage(string id, string msg);
        public delegate void dlgOnBinaryMessage(string id, byte[] msg);
        public delegate void dlgOnClose(string id, ushort code, string reason);
        public delegate void dlgOnError(string id, string errorMessage);

        public event dlgOnOpen _OnOpen;
        public event dlgOnTextMessage _OnTextMessage;
        public event dlgOnBinaryMessage _OnBinaryMessage;
        public event dlgOnClose _OnClose;
        public event dlgOnError _OnError;

        protected override void OnOpen()
        {
            _OnOpen.Invoke(ID);
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            if (e.IsText)
                _OnTextMessage.Invoke(ID, e.Data);
            else
                _OnBinaryMessage.Invoke(ID, e.RawData);
        }

        protected override void OnClose(CloseEventArgs e)
        {
            _OnClose.Invoke(ID, e.Code, e.Reason);
        }

        protected override void OnError(ErrorEventArgs e)
        {
            _OnError.Invoke(ID, e.Message);
        }
    }
    public SignalingServer(int port = 8998)
    {
        context = SynchronizationContext.Current;
        server = new WebSocketServer(port);
#pragma warning disable CS0618
        server.AddWebSocketService("/", () =>
        {
            try
            {
                var behaviour = new signalingBehaviour();
                behaviour._OnOpen += (id) =>
                {
                    context.Post(_ =>
                    {
                        OnOpen.Invoke(id);
                    }, null);
                };
                behaviour._OnTextMessage += (id, msg) =>
                {
                    context.Post(_ =>
                    {
                        OnMessage.Invoke(id, msg);
                    }, null);
                };
                behaviour._OnClose += (id, code, reason) =>
                {
                    context.Post(_ =>
                    {
                        OnClose.Invoke(id, code, reason);
                    }, null);
                };
                behaviour._OnError += (id, errorMessage) =>
                {
                    context.Post(_ =>
                    {
                        OnError.Invoke(id, errorMessage);
                    }, null);
                };
                return behaviour;
            }
            catch (Exception ex)
            {
                Debug.LogError(ex.Message);
                return null;
            }
        });
#pragma warning restore CS0618 // 型またはメンバーが旧型式です
        server.Start();
    }

    public void Send(string id, ref RTCSessionDescription desc)
    {
        Debug.Log($"Send {desc.type} to {id}");
        var json = JsonUtility.ToJson(SignalingMessage.fromDesc(ref desc));
        server.WebSocketServices["/"].Sessions[id].Context.WebSocket.Send(json);
    }

    public void Send(string id, RTCIceCandidate cand)
    {
        Debug.Log($"Send Candidate to {id}");
        var json = JsonUtility.ToJson(SignalingMessage.fromCand(cand));
        server.WebSocketServices["/"].Sessions[id].Context.WebSocket.Send(json);
    }
}

public class SignalingMessage
{
    public string type;
    public string sdp;
    public string candidate;
    public string sdpMid;
    public int sdpMLineIndex;

    public SignalingMessage(string type, string sdp)
    {
        this.type = type;
        this.sdp = sdp;
    }

    public SignalingMessage(string candidate, string sdpMid, int sdpMLineIndex)
    {
        this.type = "candidate";
        this.candidate = candidate;
        this.sdpMid = sdpMid;
        this.sdpMLineIndex = sdpMLineIndex;
    }

    public static SignalingMessage fromDesc(ref RTCSessionDescription desc)
    {
        return new SignalingMessage(desc.type.ToString().ToLower(), desc.sdp);
    }

    public static SignalingMessage fromCand(RTCIceCandidate cand)
    {
        return new SignalingMessage(cand.Candidate, cand.SdpMid, cand.SdpMLineIndex.Value);
    }

    public RTCSessionDescription toDesc()
    {
        return new RTCSessionDescription
        {
            type = type == "offer" ? RTCSdpType.Offer :
                    type == "answer" ? RTCSdpType.Answer :
                    type == "pranswer" ? RTCSdpType.Pranswer :
                    RTCSdpType.Rollback,
            sdp = sdp
        };
    }

    public RTCIceCandidate toCand()
    {
        var candidateInfo = new RTCIceCandidateInit
        {
            candidate = candidate,
            sdpMid = sdpMid,
            sdpMLineIndex = sdpMLineIndex
        };
        var cand = new RTCIceCandidate(candidateInfo);
        return cand;
    }

    public string toJson()
    {
        if (!string.IsNullOrEmpty(sdp))
            return $"{{\"type\":\"{type.ToString().ToLower()}\", \"sdp\":\"{sdp}\"}}";
        else
            return $"\"candidate\":\"{candidate}\", \"sdpMid\":\"{sdpMid}\", \"sdpMLineIndex\":{sdpMLineIndex}}}";
    }
}
