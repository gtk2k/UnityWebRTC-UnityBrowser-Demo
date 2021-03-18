
const myId = (new MediaStream()).id.replace(/[{}-]/g, '');
console.log(`myId:${myId}`);
const ws = new WebSocket(`ws://localhost:8998?id=${myId}`);
ws.onopen = evt => {
    console.log('ws open');
};
ws.onmessage = async evt => {
    const msg = JSON.parse(evt.data);
    console.log(`Recieve ${msg.type}`);
    if (!pc) createPC();
    if (msg.sdp)
        await setDesc('Remote', msg);
    if (msg.candidate) {
        delete msg.type;
        delete msg.sdp;
        console.log(JSON.stringify(msg));
        await pc.addIceCandidate(msg);
    }
};

function send(msg) {
    console.log(`Send ${msg.type}`);
    ws.send(JSON.stringify(msg));
}


let pc = null;
startButton.onclick = async evt => {
    await createPC(true);
};

async function setDesc(side, msg) {
    console.log(`Set ${side} ${msg.type}`);
    await pc[`set${side}Description`](msg);
    if (msg.type === 'offer')
        await createDesc('Answer');
}

async function createDesc(type) {
    console.log(`Create ${type}`);
    const desc = await pc[`create${type}`]();
    await pc.setLocalDescription(desc);
    send(desc);
}

async function createPC(isCaller) {
    pc = new RTCPeerConnection({ iceServers: [{ urls: 'stun:stun.l.google.com:19302' }] });
    pc.onicecandidate = evt => {
        if (evt.candidate)
            send(evt.candidate);
    };
    pc.ontrack = evt => {
        console.log('onTrack');
        var vid = document.createElement('video');
        vid.muted = true;
        vid.srcObject = evt.streams[0] || new MediaStream();
        vidContainer.append(vid);
        vid.play();
    };
    pc.onconnectionstatechange = evt => {
        console.log(pc.connectionState);
    }
    if (isCaller) {
        const stream = await navigator.mediaDevices.getUserMedia({ video: true });
        stream.getTracks().forEach(track => pc.addTrack(track, stream));
        await createDesc('Offer');
    }
}