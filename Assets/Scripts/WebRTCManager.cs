using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.UI;
using WebSocketSharp;
using WebSocketSharp.Server;

public class WebRTCManager : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private Camera cam;
    [SerializeField] private GameObject receiveImagePrefab;

    private SignalingServer signalingServer;
    private Dictionary<string, RTCPeerConnection> pcs = new Dictionary<string, RTCPeerConnection>();
    private RTCConfiguration conf = new RTCConfiguration { 
        iceServers = new[] { new RTCIceServer { urls = new[] { "stun:stun.l.google.com:19302" } } } 
    };
    private RTCOfferOptions offerOption = new RTCOfferOptions
    {
        iceRestart = false,
        offerToReceiveAudio = false,
        offerToReceiveVideo = true
    };
    private RTCAnswerOptions answerOptions = new RTCAnswerOptions
    {
        iceRestart = false
    };

    private Dictionary<string, WebSocket> clients = new Dictionary<string, WebSocket>();

    public enum Side
    {
        Local,
        Remote
    }

    private void Awake()
    {
        WebRTC.Initialize(EncoderType.Software);
    }

    // Start is called before the first frame update
    void Start()
    {
        signalingServer = new SignalingServer(8998);
        signalingServer.OnOpen.AddListener(onSignalingOpen);
        signalingServer.OnMessage.AddListener(onSignalingMessage);
        signalingServer.OnClose.AddListener(onSignalingClose);
        signalingServer.OnError.AddListener(onSignalingError);
        StartCoroutine(WebRTC.Update());
    }

    private int xPos = 0;
    private void createPC(string id, bool isCaller = false)
    {
        Debug.Log($"[Create PC] id:{id}");
        var pc = new RTCPeerConnection(ref conf);
        pcs.Add(id, pc);
        pc.OnIceCandidate = cand =>
        {
            //Debug.Log($"onIceCandidate: id:{id}");
            signalingServer.Send(id, cand);
        };
        pc.OnTrack = evt =>
        {
            Debug.Log($"onTrack: id:{id}");
            var vTrack = evt.Track as VideoStreamTrack;
            var ri = Instantiate(receiveImagePrefab);
            ri.transform.position = new Vector3(xPos++, 0, 0);
            ri.GetComponent<Renderer>().material.mainTexture = vTrack.InitializeReceiver(1920, 1080);
        };
        var sendTrack = cam.CaptureStreamTrack(1920, 1080, 2000000);
        pc.AddTrack(sendTrack);
        if (isCaller)
            createDesc(id, RTCSdpType.Offer);
        
    }

    private void onSignalingOpen(string id)
    {
        Debug.Log($"[Signaling Open] id:{id}");
    }

    private void onSignalingMessage(string id, string json)
    {
        var msg = JsonUtility.FromJson<SignalingMessage>(json);

        //Debug.Log($"Receive {msg.type} form {id}");
        if (!pcs.ContainsKey(id))
            createPC(id);
        if (!string.IsNullOrEmpty(msg.sdp))
            StartCoroutine(setDesc(id, Side.Remote, msg.toDesc()));
        if (!string.IsNullOrEmpty(msg.candidate))
        {
            //Debug.Log($"[AddIceCandidate] id:{id}");
            pcs[id].AddIceCandidate(msg.toCand());
        }
    }

    private void onSignalingClose(string id, ushort code, string reason)
    {
        Debug.Log($"[Signaling Close] id:{id}, code:{code}, reqson:{reason}");
    }

    private void onSignalingError(string id, string errorMessage)
    {
        Debug.LogError($"[Signaling Error] id: {id}, errorMessage:{errorMessage}");
    }

    IEnumerator createDesc(string id, RTCSdpType type)
    {
        Debug.Log($"[Create {type}] id:{id}");
        var pc = pcs[id];
        var op = type == RTCSdpType.Offer ? pc.CreateOffer(ref offerOption) : pc.CreateAnswer(ref answerOptions);
        yield return op;
        if (op.IsError)
            Debug.LogError(op.Error.message);
        else
        {
            yield return setDesc(id, Side.Local, op.Desc);
        }
    }

    IEnumerator setDesc(string id, Side side, RTCSessionDescription desc)
    {
        var pc = pcs[id];
        Debug.Log($"[Set {side} {desc.type}] id:{id}");
        var op = side == Side.Local ? pc.SetLocalDescription(ref desc) : pc.SetRemoteDescription(ref desc);
        yield return op;
        if (op.IsError)
            Debug.LogError(op.Error.message);
        else if (desc.type == RTCSdpType.Offer)
            StartCoroutine(createDesc(id, RTCSdpType.Answer));
        else if (side == Side.Local && desc.type == RTCSdpType.Answer)
            signalingServer.Send(id, ref desc);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
