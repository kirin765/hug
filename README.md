# HUG.IO

A tiny 3D multiplayer web game. There is no combat and no timer — the only score
is **hugs**.

Walk up to someone and press **E**. If you are within 100 hugs of each other
you both get a point. If one of you is further ahead, the hug instead swaps 10%
of each score — so hugging the leader pulls you up and pulls them down. Dash
with **Space** to catch someone (or to run away from a hug you don't want).

![island](docs/screenshot.png)

## Play

```bash
cd server
npm install
npm start
```

Then open <http://localhost:8080> — open a second tab to play with yourself, or
send the link to a friend.

| Input | Action |
| --- | --- |
| `WASD` / arrows | move |
| `E` | hug the nearest player |
| `Space` / `Shift` | dash |
| drag | orbit camera |
| scroll | zoom |
| touch | left stick, ♥ hug, » dash |

## The rule

When A hugs B, `server/protocol.js` decides the new scores:

```js
if (Math.abs(A - B) <= 100) {
  A += 1; B += 1;                    // close: a point each
} else {
  const giveA = round(A * 0.10);     // far apart: swap 10% of each score
  const giveB = round(B * 0.10);
  A = A - giveA + giveB;
  B = B - giveB + giveA;
}
```

Each player hands over 10% of their own score and receives 10% of the other's,
so the total is conserved while a big lead melts away. The thresholds are
constants at the top of `server/protocol.js`:

| Constant | Default | Meaning |
| --- | --- | --- |
| `HUG_EVEN_POINTS` | `1` | points each for a "close" hug |
| `HUG_FAIR_GAP` | `100` | max score difference that counts as close |
| `HUG_EXCHANGE_RATE` | `0.1` | share of each score swapped when far apart |

## How it works

```
Unity WebGL client  ──ws://host/ws──▶  Node server (authoritative)
       ▲                                     │
       └──────────  20 Hz snapshots  ◀───────┘
```

- **Server** (`server/server.js`) — one process that serves the WebGL build and
  runs the game loop. It validates hug range/cooldown, applies the averaging
  rule, clamps positions to the island and broadcasts 20 Hz snapshots. Two
  friendly bots wander around and hug people by default (`--bots N`, `--bots 0`
  for an empty world).
- **Client** (`Assets/Scripts`) — Unity 6 / URP. Movement is client-predicted,
  remote players are interpolated between snapshots. In a WebGL build the socket
  is a browser `WebSocket` (see `Assets/Plugins/WebGL/HugWebSocket.jslib`); in
  the editor it uses `System.Net.WebSockets`, so you can test with Play Mode.
- **No art assets** — the island, characters, hearts, UI sprites and sound
  effects are all generated at runtime (`Art.cs`, `Effects.cs`, `Sound.cs`).
  The only imported asset is the Varela Round font (SIL OFL).

### Project layout

```
Assets/
  Editor/            ProjectSetup (materials + scene + player settings), BuildWebGL
  Plugins/WebGL/     WebSocket bridge (.jslib)
  Resources/         Fonts + the three shared materials
  Scenes/SampleScene.unity
  Scripts/
    Net/             Protocol, HugSocket, GameClient
    Game/            GameBootstrap, World, LocalPlayer, RemotePlayer, PlayerVisual,
                     CameraRig, Effects, Art, PlatformInfo
    UI/              Hud, UiKit, UiButton, TouchControls, InputController, Sound
  WebGLTemplates/Hug/  full-bleed page + loading screen
server/
  server.js          static host + WebSocket game loop
  protocol.js        tuning shared with the client
  test-client.js     protocol tests (node test-client.js)
```

## Build the WebGL player

In Unity: **HUG → Build WebGL**, or from a terminal (editor closed):

```bash
unity build --target WebGL -o web --execute-method Hug.EditorTools.BuildWebGL.Build .
```

The build lands in `web/`, which the server serves. After building, restart the
server (it reads the folder on startup).

First-time setup of the project is **HUG → Setup Project** (creates the shared
materials, rebuilds the scene, applies WebGL player settings). It is safe to run
again.

## Deploying

The server is a plain Node process with one dependency (`ws`), so any box that
can run Node works. Point `WEB_DIR` at the build and put it behind a TLS
terminator that forwards WebSockets:

```bash
PORT=8080 WEB_DIR=./web BOTS=2 node server.js
```

The client connects to `ws(s)://<page-host>/ws` automatically, so as long as the
page is served by this server, nothing needs configuring. `?server=wss://.../ws`
overrides it.

Handy URL parameters:

| Param | Meaning |
| --- | --- |
| `?name=Sam` | skip the name card |
| `?x=0&z=0` | spawn at a specific spot (used by the tests) |
| `?server=ws://host:port/ws` | connect to a different server |

## Tests

```bash
cd server
npm test
```

Starts a throwaway server and checks joining, snapshots, hug range, cooldown,
the scoring rule (both branches) and disconnect broadcasts.

## License

Code is MIT. Varela Round is licensed under the SIL Open Font License 1.1
(`Assets/Resources/Fonts/VarelaRound-OFL.txt`).
