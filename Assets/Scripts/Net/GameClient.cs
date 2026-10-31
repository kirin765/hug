using System;
using UnityEngine;

namespace Hug
{
    /// <summary>
    /// Owns the socket, speaks the HUG.IO protocol and exposes it as events.
    /// Nothing in here touches gameplay objects — see GameBootstrap for that.
    /// </summary>
    public class GameClient : MonoBehaviour
    {
        public HugSocket Socket { get; private set; }
        public bool Joined { get; private set; }
        public int MyId { get; private set; }
        public string MyName { get; private set; } = "You";
        public int MyColor { get; private set; }
        public float ArenaRadius { get; private set; } = Proto.ArenaRadius;
        public int LatencyMs { get; private set; } = -1;

        public event Action Welcome;
        public event Action<SnapMsg> Snapshot;
        public event Action<HugMsg> Hug;
        public event Action<int, string> PlayerJoined;
        public event Action<int, string> PlayerLeft;
        public event Action<string> Rejected;
        public event Action<string> StatusChanged;
        public event Action Disconnected;

        string _url;
        string _pendingName;
        bool _hasSpawn;
        Vector2 _spawn;
        bool _wantConnected;
        bool _shouldReconnect;
        float _reconnectAt;
        float _pingTimer;
        float _poseTimer;
        float _lastX, _lastZ, _lastYaw;
        bool _poseDirty;

        public void SetSpawn(Vector2 spawn)
        {
            _hasSpawn = true;
            _spawn = spawn;
        }

        public void Connect(string url, string playerName)
        {
            _url = string.IsNullOrEmpty(url) ? Proto.FallbackUrl : url;
            _pendingName = playerName;
            _wantConnected = true;

            if (Socket == null)
            {
                Socket = gameObject.AddComponent<HugSocket>();
                Socket.Opened += OnSocketOpened;
                Socket.Received += OnSocketMessage;
                Socket.Failed += OnSocketFailed;
                Socket.Closed += OnSocketClosed;
            }

            StatusChanged?.Invoke("Connecting…");
            Socket.Connect(_url);
        }

        public void SetPose(float x, float z, float yaw)
        {
            _lastX = x;
            _lastZ = z;
            _lastYaw = yaw;
            _poseDirty = true;
        }

        public void RequestHug(int targetId)
        {
            if (!Joined || Socket == null) return;
            Socket.Send(JsonUtility.ToJson(new HugOut { to = targetId }));
        }

        public void Leave()
        {
            _wantConnected = false;
            _shouldReconnect = false;
            if (Socket != null) Socket.Close();
            Joined = false;
        }

        void Update()
        {
            if (Socket == null) return;

            if (_shouldReconnect && !Joined &&
                Socket.Current != HugSocket.State.Open &&
                Socket.Current != HugSocket.State.Connecting &&
                Time.unscaledTime > _reconnectAt)
            {
                _shouldReconnect = false;
                StatusChanged?.Invoke("Reconnecting…");
                Socket.Connect(_url);
            }

            if (!Joined) return;

            _poseTimer -= Time.deltaTime;
            if (_poseTimer <= 0f && _poseDirty)
            {
                _poseTimer = 1f / Proto.TickHz;
                _poseDirty = false;
                Socket.Send(JsonUtility.ToJson(new PosOut { x = _lastX, z = _lastZ, r = _lastYaw }));
            }

            _pingTimer -= Time.deltaTime;
            if (_pingTimer <= 0f)
            {
                _pingTimer = 2f;
                Socket.Send(JsonUtility.ToJson(new PingOut { ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }));
            }
        }

        void OnSocketOpened()
        {
            Socket.Send(JsonUtility.ToJson(new JoinOut
            {
                n = _pendingName,
                s = _hasSpawn,
                x = _spawn.x,
                z = _spawn.y,
            }));
        }

        void OnSocketFailed(string error)
        {
            StatusChanged?.Invoke(error);
            Disconnected?.Invoke();
            if (_wantConnected) ScheduleReconnect();
        }

        void OnSocketClosed(int code)
        {
            Joined = false;
            Disconnected?.Invoke();
            if (_wantConnected) ScheduleReconnect();
        }

        void ScheduleReconnect()
        {
            _shouldReconnect = true;
            _reconnectAt = Time.unscaledTime + 2f;
        }

        void OnSocketMessage(string json)
        {
            if (string.IsNullOrEmpty(json)) return;

            switch (ReadType(json))
            {
                case "welcome":
                {
                    var msg = JsonUtility.FromJson<WelcomeMsg>(json);
                    if (msg == null) return;
                    MyId = msg.i;
                    MyName = msg.n;
                    MyColor = msg.c;
                    ArenaRadius = msg.a > 1f ? msg.a : Proto.ArenaRadius;
                    Joined = true;
                    StatusChanged?.Invoke("Connected");
                    Welcome?.Invoke();
                    break;
                }
                case "snap":
                {
                    var msg = JsonUtility.FromJson<SnapMsg>(json);
                    if (msg?.p != null) Snapshot?.Invoke(msg);
                    break;
                }
                case "hug":
                {
                    var msg = JsonUtility.FromJson<HugMsg>(json);
                    if (msg != null) Hug?.Invoke(msg);
                    break;
                }
                case "joined":
                {
                    var msg = JsonUtility.FromJson<JoinedMsg>(json);
                    if (msg != null && msg.i != MyId) PlayerJoined?.Invoke(msg.i, msg.n);
                    break;
                }
                case "left":
                {
                    var msg = JsonUtility.FromJson<LeftMsg>(json);
                    if (msg != null) PlayerLeft?.Invoke(msg.i, msg.n);
                    break;
                }
                case "nope":
                {
                    var msg = JsonUtility.FromJson<NopeMsg>(json);
                    if (msg != null) Rejected?.Invoke(msg.m);
                    break;
                }
                case "pong":
                {
                    var msg = JsonUtility.FromJson<PongMsg>(json);
                    if (msg == null) return;
                    long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    LatencyMs = Mathf.Clamp((int)(now - msg.ts), 0, 9999);
                    break;
                }
            }
        }

        /// <summary>Reads the "t" field without paying for a full parse.</summary>
        static string ReadType(string json)
        {
            int i = json.IndexOf("\"t\":", StringComparison.Ordinal);
            if (i < 0) return null;
            int open = json.IndexOf('"', i + 4);
            if (open < 0) return null;
            int close = json.IndexOf('"', open + 1);
            if (close < 0) return null;
            return json.Substring(open + 1, close - open - 1);
        }
    }
}
