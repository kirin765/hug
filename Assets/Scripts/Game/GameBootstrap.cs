using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hug
{
    /// <summary>
    /// Entry point. Builds the island and the HUD, connects the socket and glues
    /// snapshots to avatars. Drop this on a single empty GameObject in the scene.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        GameClient _client;
        Hud _hud;
        InputController _input;
        Effects _fx;
        CameraRig _rig;
        LocalPlayer _local;
        Transform _playersRoot;

        readonly Dictionary<int, RemotePlayer> _remotes = new Dictionary<int, RemotePlayer>();
        readonly Dictionary<int, PlayerState> _latest = new Dictionary<int, PlayerState>();

        string _pendingName = "You";
        bool _everJoined;
        float _boardTimer;
        float _pingTimer;
        int _nearbyId = -1;

        void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            _rig = World.Build(transform, Proto.ArenaRadius);

            _playersRoot = new GameObject("Players").transform;
            _playersRoot.SetParent(transform, false);

            var fxGo = new GameObject("Effects");
            fxGo.transform.SetParent(transform, false);
            _fx = fxGo.AddComponent<Effects>();
            _fx.Setup();

            _hud = gameObject.AddComponent<Hud>();
            _hud.Build();
            _hud.PlayRequested += StartGame;

            _input = gameObject.AddComponent<InputController>();
            _input.Touch = _hud.Touch;

            _client = gameObject.AddComponent<GameClient>();
            _client.Welcome += OnWelcome;
            _client.Snapshot += OnSnapshot;
            _client.Hug += OnHug;
            _client.PlayerLeft += OnPlayerLeft;
            _client.Rejected += OnRejected;
            _client.StatusChanged += OnStatus;
            _client.Disconnected += OnDisconnected;

            string name = QueryParam("name");
            if (!string.IsNullOrEmpty(name))
            {
                StartGame(name);
            }
            else
            {
                _hud.ShowEntry(PlayerPrefs.GetString("hug.name", ""), "");
            }
        }

        // ------------------------------------------------------------- session

        void StartGame(string playerName)
        {
            _pendingName = string.IsNullOrEmpty(playerName) ? "You" : playerName;
            PlayerPrefs.SetString("hug.name", _pendingName);
            PlayerPrefs.Save();

            _hud.HideEntry();
            _hud.SetHint("Connecting…");

            string sx = QueryParam("x");
            string sz = QueryParam("z");
            if (!string.IsNullOrEmpty(sx) && !string.IsNullOrEmpty(sz) &&
                float.TryParse(sx, out float px) && float.TryParse(sz, out float pz))
            {
                _client.SetSpawn(new Vector2(px, pz));
            }

            _client.Connect(ResolveUrl(), _pendingName);
        }

        void OnWelcome()
        {
            _everJoined = true;
            _hud.HideEntry();
            _hud.SetScore(0);
            _hud.SetHint(IsTouchUi()
                ? "left stick to move  ·  tap ♥ to hug  ·  » to dash"
                : "WASD move  ·  E hug  ·  SPACE dash  ·  drag to look");
            Sound.Play(Sound.Kind.Pop);
        }

        void OnStatus(string status)
        {
            if (_client.Joined) return;
            _hud.SetHint(status);
            if (status == "Connecting…" || status == "Reconnecting…")
            {
                _hud.SetEntryBusy(true);
            }
        }

        void OnDisconnected()
        {
            if (_everJoined)
            {
                _hud.Toast("Connection lost — reconnecting…", UiKit.Muted);
                return;
            }

            _hud.ShowEntry(_pendingName, "Could not reach the hug server. Is it running?");
        }

        // ------------------------------------------------------------ snapshot

        void OnSnapshot(SnapMsg snap)
        {
            var seen = new HashSet<int>();

            for (int i = 0; i < snap.p.Length; i++)
            {
                var state = snap.p[i];
                if (state == null) continue;

                seen.Add(state.i);
                _latest[state.i] = state;

                if (state.i == _client.MyId)
                {
                    if (_local == null) SpawnLocal(state);
                    _local.Score = state.s;
                    _hud.SetScore(state.s);
                    continue;
                }

                if (!_remotes.TryGetValue(state.i, out var remote))
                {
                    remote = SpawnRemote(state);
                }

                remote.Apply(state);
            }

            // drop players the server no longer knows about
            List<int> gone = null;
            foreach (var pair in _remotes)
            {
                if (!seen.Contains(pair.Key))
                {
                    gone ??= new List<int>();
                    gone.Add(pair.Key);
                }
            }

            if (gone != null)
            {
                for (int i = 0; i < gone.Count; i++)
                {
                    if (_remotes.TryGetValue(gone[i], out var r))
                    {
                        _remotes.Remove(gone[i]);
                        _latest.Remove(gone[i]);
                        if (r != null) Destroy(r.gameObject);
                    }
                }
            }
        }

        void SpawnLocal(PlayerState state)
        {
            var go = new GameObject("LocalPlayer");
            go.transform.SetParent(_playersRoot, false);
            _local = go.AddComponent<LocalPlayer>();
            _local.Init(state.i, state.n, Palette.Get(state.c), new Vector3(state.x, 0f, state.z));

            _rig.Target = go.transform;
            _rig.Snap();
            _rig.Yaw = 0f;
        }

        RemotePlayer SpawnRemote(PlayerState state)
        {
            var go = new GameObject("RemotePlayer");
            go.transform.SetParent(_playersRoot, false);
            var remote = go.AddComponent<RemotePlayer>();
            remote.Init(state.i, state.n, Palette.Get(state.c), new Vector3(state.x, 0f, state.z));
            remote.Apply(state);
            _remotes[state.i] = remote;
            return remote;
        }

        void OnPlayerLeft(int id, string playerName)
        {
            if (_remotes.TryGetValue(id, out var remote))
            {
                _remotes.Remove(id);
                _latest.Remove(id);
                if (remote != null) Destroy(remote.gameObject);
            }
        }

        // ----------------------------------------------------------------- hug

        void OnHug(HugMsg msg)
        {
            Vector3 at = new Vector3(msg.x, 0f, msg.z);
            Color color = Palette.Get(GetColor(msg.f));
            _fx.HugBurst(at, color);
            _fx.Pulse(at, color);
            Sound.Play(Sound.Kind.Hug);

            if (_local != null)
            {
                if (msg.f == _client.MyId) _local.PlayHug(at);
                if (msg.o == _client.MyId) _local.PlayHug(at);
            }

            if (_remotes.TryGetValue(msg.f, out var from))
            {
                from.PlayHug();
                from.LookAt(at);
            }

            if (_remotes.TryGetValue(msg.o, out var to))
            {
                to.PlayHug();
                to.LookAt(at);
            }

            if (msg.o == _client.MyId)
            {
                _fx.PopText(_local != null ? _local.transform.position : at, DeltaText(msg.od), DeltaColor(msg.od));
                _hud.Toast($"{NameOf(msg.f)} hugged you   {DeltaText(msg.od)}", DeltaColor(msg.od));
            }
            else if (msg.f == _client.MyId)
            {
                _fx.PopText(at, DeltaText(msg.fd), DeltaColor(msg.fd));
                _hud.Toast($"You hugged {NameOf(msg.o)}   {DeltaText(msg.fd)}", DeltaColor(msg.fd));
            }
        }

        static string DeltaText(int delta)
        {
            return delta > 0 ? "+" + delta : delta.ToString();
        }

        static Color DeltaColor(int delta)
        {
            if (delta > 0) return UiKit.PinkSoft;
            if (delta < 0) return new Color(0.62f, 0.82f, 1f);
            return UiKit.Muted;
        }

        void OnRejected(string reason)
        {
            _hud.Toast(reason, UiKit.Muted);
            Sound.Play(Sound.Kind.Error);
        }

        int GetColor(int id)
        {
            return _latest.TryGetValue(id, out var state) ? state.c : 0;
        }

        string NameOf(int id)
        {
            return _latest.TryGetValue(id, out var state) ? state.n : "someone";
        }

        // -------------------------------------------------------------- update

        void Update()
        {
            float dt = Time.deltaTime;
            var input = _input.Sample();
            _input.AllowLook = _client.Joined && !_hud.EntryOpen;

            if (_client.Joined)
            {
                _rig.Orbit(input.Look.x);
                _rig.Zoom(input.Zoom);
            }

            if (_local != null)
            {
                _local.Tick(input.Move, _rig.Forward, _rig.Right, dt);
                _client.SetPose(_local.transform.position.x, _local.transform.position.z, _local.Yaw);

                UpdateNearby();

                if (input.Hug && _nearbyId >= 0 && _local.CanHug)
                {
                    _client.RequestHug(_nearbyId);
                    if (_remotes.TryGetValue(_nearbyId, out var target))
                    {
                        _local.PlayHug(target.transform.position);
                    }
                }

                if (input.Dash && _local.TryDash(DashDirection(input.Move)))
                {
                    _fx.DashPuff(_local.LastDashFrom, _local.transform.forward);
                    _rig.Punch(9f);
                    Sound.Play(Sound.Kind.Dash);
                }

                if (_hud.Touch != null) _hud.Touch.SetDashCharge(_local.DashCharge);
            }

            _pingTimer -= dt;
            if (_pingTimer <= 0f)
            {
                _pingTimer = 1f;
                _hud.SetPing(_client.LatencyMs);
            }

            _boardTimer -= dt;
            if (_boardTimer <= 0f)
            {
                _boardTimer = 0.25f;
                RefreshBoard();
            }
        }

        Vector3 DashDirection(Vector2 move)
        {
            Vector3 wish = _rig.Forward * move.y + _rig.Right * move.x;
            if (wish.sqrMagnitude < 0.01f) return _local.transform.forward;
            return wish;
        }

        void UpdateNearby()
        {
            Vector3 myPos = _local.transform.position;
            float best = float.MaxValue;
            RemotePlayer bestPlayer = null;

            foreach (var remote in _remotes.Values)
            {
                if (remote == null) continue;
                Vector3 d = remote.transform.position - myPos;
                d.y = 0f;
                float dist = d.magnitude;
                if (dist < best)
                {
                    best = dist;
                    bestPlayer = remote;
                }
            }

            if (bestPlayer != null && best <= Proto.HugRange + 0.4f)
            {
                _nearbyId = bestPlayer.Id;
                _hud.SetPrompt(IsTouchUi()
                    ? $"tap ♥ to hug {bestPlayer.PlayerName}"
                    : $"press <b>E</b> to hug {bestPlayer.PlayerName}");
            }
            else
            {
                _nearbyId = -1;
                _hud.SetPrompt(null);
            }
        }

        void RefreshBoard()
        {
            var entries = new List<Hud.Entry>(_latest.Count);
            foreach (var state in _latest.Values)
            {
                entries.Add(new Hud.Entry
                {
                    Name = state.n,
                    Score = state.s,
                    Color = Palette.Get(state.c),
                    IsMe = state.i == _client.MyId,
                });
            }

            entries.Sort((a, b) => b.Score.CompareTo(a.Score));
            if (entries.Count > 6) entries.RemoveRange(6, entries.Count - 6);
            _hud.SetLeaderboard(entries);
        }

        bool IsTouchUi()
        {
            return _hud != null && _hud.Touch != null && _hud.Touch.Visible;
        }

        // ------------------------------------------------------------ plumbing

        /// <summary>ws(s)://host/ws when served by the game server, or ?server= override.</summary>
        static string ResolveUrl()
        {
            string overrideUrl = QueryParam("server");
            if (!string.IsNullOrEmpty(overrideUrl)) return overrideUrl;

#if UNITY_WEBGL && !UNITY_EDITOR
            string absolute = Application.absoluteURL;
            if (!string.IsNullOrEmpty(absolute) && Uri.TryCreate(absolute, UriKind.Absolute, out var uri))
            {
                string scheme = uri.Scheme == "https" ? "wss" : "ws";
                bool defaultPort = (uri.Scheme == "https" && uri.Port == 443) || (uri.Scheme == "http" && uri.Port == 80);
                return $"{scheme}://{uri.Host}{(defaultPort ? string.Empty : ":" + uri.Port)}/ws";
            }
#endif
            return Proto.FallbackUrl;
        }

        static string QueryParam(string key)
        {
            string url = Application.absoluteURL;
            if (string.IsNullOrEmpty(url)) return null;

            int q = url.IndexOf('?');
            if (q < 0) return null;

            string query = url.Substring(q + 1);
            int hash = query.IndexOf('#');
            if (hash >= 0) query = query.Substring(0, hash);

            string[] parts = query.Split('&');
            for (int i = 0; i < parts.Length; i++)
            {
                int eq = parts[i].IndexOf('=');
                if (eq <= 0) continue;
                if (parts[i].Substring(0, eq) != key) continue;
                return Uri.UnescapeDataString(parts[i].Substring(eq + 1).Replace("+", " "));
            }

            return null;
        }
    }
}
