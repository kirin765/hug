/**
 * HugWebSocket.jslib
 *
 * Browser WebSocket bridge for Unity WebGL. Unity's managed
 * System.Net.WebSockets does not exist in WebGL, so the socket lives in JS and
 * talks to C# through SendMessage on the GameObject running HugSocket.
 */

mergeInto(LibraryManager.library, {
  $HugWSState: {
    socket: null,
    gameObject: null,
  },

  HugWS_Connect__deps: ['$HugWSState'],
  HugWS_Connect: function (urlPtr, gameObjectPtr) {
    var url = UTF8ToString(urlPtr);
    var go = UTF8ToString(gameObjectPtr);

    try {
      if (HugWSState.socket) {
        HugWSState.socket.onopen = HugWSState.socket.onmessage = null;
        HugWSState.socket.onerror = HugWSState.socket.onclose = null;
        HugWSState.socket.close();
      }
    } catch (e) {}

    var post = function (method, arg) {
      try {
        SendMessage(go, method, arg === undefined ? '' : String(arg));
      } catch (e) {
        console.warn('[HugWS] SendMessage failed: ' + method, e);
      }
    };

    var socket;
    try {
      socket = new WebSocket(url);
    } catch (e) {
      post('OnWsError', 'Bad server address');
      return;
    }

    HugWSState.socket = socket;
    HugWSState.gameObject = go;

    socket.onopen = function () {
      post('OnWsOpen');
    };

    socket.onmessage = function (event) {
      if (typeof event.data === 'string') {
        post('OnWsMessage', event.data);
      } else if (event.data instanceof ArrayBuffer) {
        post('OnWsMessage', UTF8ArrayToString(new Uint8Array(event.data), 0));
      }
    };

    socket.onerror = function () {
      post('OnWsError', 'Cannot reach the server');
    };

    socket.onclose = function (event) {
      if (HugWSState.socket === socket) HugWSState.socket = null;
      post('OnWsClose', event.code);
    };
  },

  HugWS_Send__deps: ['$HugWSState'],
  HugWS_Send: function (msgPtr) {
    var socket = HugWSState.socket;
    if (socket && socket.readyState === 1) {
      socket.send(UTF8ToString(msgPtr));
    }
  },

  HugWS_Close__deps: ['$HugWSState'],
  HugWS_Close: function () {
    var socket = HugWSState.socket;
    HugWSState.socket = null;
    if (socket) {
      try {
        socket.onopen = socket.onmessage = socket.onerror = socket.onclose = null;
        socket.close(1000);
      } catch (e) {}
    }
  },

  HugWS_IsOpen__deps: ['$HugWSState'],
  HugWS_IsOpen: function () {
    var socket = HugWSState.socket;
    return socket && socket.readyState === 1 ? 1 : 0;
  },

  // --- pointer bridge -------------------------------------------------------
  // Unity's Input System mouse device is unreliable in some browsers, so the UI
  // reads the browser's pointer events directly. Values are normalised to the
  // canvas so the C# side can scale them to Screen.width/height.
  $HugPointer: {
    nx: 0.5,
    ny: 0.5,
    dx: 0,
    dy: 0,
    down: 0,
    pressed: 0,
    released: 0,
    ready: 0,
  },

  HugPointer_Init__deps: ['$HugPointer'],
  HugPointer_Init: function () {
    var P = HugPointer;
    if (P.ready) return;
    P.ready = 1;

    function update(e) {
      var canvas = document.getElementById('unity-canvas');
      if (!canvas) return;
      var r = canvas.getBoundingClientRect();
      if (r.width <= 0 || r.height <= 0) return;
      var nx = (e.clientX - r.left) / r.width;
      var ny = (e.clientY - r.top) / r.height;
      P.dx += nx - P.nx;
      P.dy += ny - P.ny;
      P.nx = nx;
      P.ny = ny;
    }

    window.addEventListener('pointermove', update, { passive: true });
    window.addEventListener('pointerdown', function (e) {
      update(e);
      P.down = 1;
      P.pressed = 1;
    }, { passive: true });
    window.addEventListener('pointerup', function (e) {
      update(e);
      P.down = 0;
      P.released = 1;
    }, { passive: true });
    window.addEventListener('pointercancel', function () {
      P.down = 0;
      P.released = 1;
    }, { passive: true });
    window.addEventListener('blur', function () {
      P.down = 0;
    });
  },

  HugPointer_Nx__deps: ['$HugPointer'],
  HugPointer_Nx: function () { return HugPointer.nx; },
  HugPointer_Ny__deps: ['$HugPointer'],
  HugPointer_Ny: function () { return HugPointer.ny; },
  HugPointer_Dx__deps: ['$HugPointer'],
  HugPointer_Dx: function () { return HugPointer.dx; },
  HugPointer_Dy__deps: ['$HugPointer'],
  HugPointer_Dy: function () { return HugPointer.dy; },
  HugPointer_Down__deps: ['$HugPointer'],
  HugPointer_Down: function () { return HugPointer.down; },
  HugPointer_Pressed__deps: ['$HugPointer'],
  HugPointer_Pressed: function () { return HugPointer.pressed; },
  HugPointer_Released__deps: ['$HugPointer'],
  HugPointer_Released: function () { return HugPointer.released; },
  HugPointer_ClearEdges__deps: ['$HugPointer'],
  HugPointer_ClearEdges: function () {
    HugPointer.pressed = 0;
    HugPointer.released = 0;
    HugPointer.dx = 0;
    HugPointer.dy = 0;
  },

  HugIsCoarsePointer: function () {
    try {
      return window.matchMedia && window.matchMedia('(pointer: coarse)').matches ? 1 : 0;
    } catch (e) {
      return 0;
    }
  },
});
