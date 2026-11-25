var PhotonWebGLKeepAlivePlugin = {
    PhotonWebGL_StartKeepAliveWorker: function() {
        if (typeof window === 'undefined') return;
        if (window.__photonKeepAliveWorkerStarted) return;
        window.__photonKeepAliveWorkerStarted = true;

        try {
            var workerBlob = new Blob([
                "setInterval(function() { postMessage('heartbeat'); }, 200);"
            ], { type: 'application/javascript' });
            var worker = new Worker(URL.createObjectURL(workerBlob));
            worker.onmessage = function() {
                if (typeof unityInstance !== 'undefined' && unityInstance != null) {
                    try {
                        unityInstance.SendMessage('[WebGLBackgroundKeepAlive]', 'OnKeepAliveTick');
                    } catch(e) {}
                }
            };
            console.log('[PhotonWebGLKeepAlive] Background Web Worker active.');
        } catch(e) {
            console.warn('[PhotonWebGLKeepAlive] Could not start keepalive worker:', e);
        }
    }
};

mergeInto(LibraryManager.library, PhotonWebGLKeepAlivePlugin);

