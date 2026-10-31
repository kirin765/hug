/**
 * HUG.IO server
 * -------------
 * One process does two jobs:
 *   1. Serves the Unity WebGL build (static files) on http://localhost:8080
 *   2. Runs the authoritative game loop over WebSocket at /ws
 *
 * Run:  npm install && npm start
 * Open: http://localhost:8080
 *
 * Flags / env:
 *   --port 8080        PORT
 *   --bots 3           BOTS      (friendly wandering bots, 0 = off)
 *   --web ../web       WEB_DIR   (static build folder)
 */

import http from "node:http";
import { createReadStream, statSync } from "node:fs";
import { extname, join, normalize, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { WebSocketServer } from "ws";
import {
  ARENA_RADIUS,
  COLORS,
  HUG_ANIM_MS,
  HUG_COOLDOWN_MS,
  HUG_LOCK_MS,
  HUG_RANGE,
  MAX_PLAYERS,
  PLAYER_RADIUS,
  PLAYER_SPEED,
  PROTOCOL_VERSION,
  TICK_HZ,
  clampToArena,
  cleanName,
  hugScores,
} from "./protocol.js";

const __dirname = fileURLToPath(new URL(".", import.meta.url));

// ---------------------------------------------------------------- config ---

function arg(name, fallback) {
  const i = process.argv.indexOf(`--${name}`);
  if (i !== -1 && process.argv[i + 1]) return process.argv[i + 1];
  return fallback;
}

const PORT = Number(arg("port", process.env.PORT || 8080));
const BOT_COUNT = Number(arg("bots", process.env.BOTS ?? 2));
const WEB_DIR = resolve(__dirname, arg("web", process.env.WEB_DIR || "../web"));

// ----------------------------------------------------------------- state ---

/** @type {Map<number, any>} */
const players = new Map();
let nextId = 1;
let nextColor = 0;

const now = () => Date.now();

function makePlayer(name, ws, isBot = false) {
  const id = nextId++;
  const angle = Math.random() * Math.PI * 2;
  const dist = 4 + Math.random() * (ARENA_RADIUS - 9);
  const p = {
    id,
    name: cleanName(name),
    color: nextColor++ % COLORS.length,
    x: Math.cos(angle) * dist,
    z: Math.sin(angle) * dist,
    yaw: Math.random() * 360,
    score: 0,
    ws,
    isBot,
    joinedAt: now(),
    lastPosAt: now(),
    lastHugAt: 0,
    hugUntil: 0,
    hugWith: 0,
    // bot brain
    botWanderAt: 0,
    botDir: Math.random() * Math.PI * 2,
    botHuggedAt: new Map(),
  };
  players.set(id, p);
  return p;
}

function send(ws, msg) {
  if (ws && ws.readyState === 1) ws.send(JSON.stringify(msg));
}

function broadcast(msg) {
  const data = JSON.stringify(msg);
  for (const p of players.values()) {
    if (p.ws && p.ws.readyState === 1) p.ws.send(data);
  }
}

function snapshot() {
  const t = now();
  return {
    t: "snap",
    p: [...players.values()].map((p) => ({
      i: p.id,
      n: p.name,
      c: p.color,
      x: round(p.x, 2),
      z: round(p.z, 2),
      r: round(p.yaw, 1),
      s: p.score,
      h: t < p.hugUntil ? 1 : 0,
    })),
  };
}

const round = (v, d) => Math.round(v * 10 ** d) / 10 ** d;

// ------------------------------------------------------------------ hugs ---

function tryHug(from, toId) {
  const target = players.get(toId);
  if (!target || target === from) return "Nobody there";

  const t = now();
  if (t - from.lastHugAt < HUG_COOLDOWN_MS) return "Easy there!";

  const dist = Math.hypot(from.x - target.x, from.z - target.z);
  if (dist > HUG_RANGE) return "Too far away";

  from.lastHugAt = t;
  from.hugUntil = t + HUG_ANIM_MS;
  from.hugWith = target.id;
  target.hugUntil = t + HUG_ANIM_MS;
  target.hugWith = from.id;
  target.lastHugAt = Math.max(target.lastHugAt, t - HUG_COOLDOWN_MS + HUG_LOCK_MS);

  // Close scores: +1 each. Far apart: swap 10% of each other's score.
  const [fromScore, targetScore] = hugScores(from.score, target.score);
  const fromDelta = fromScore - from.score;
  const targetDelta = targetScore - target.score;
  from.score = fromScore;
  target.score = targetScore;

  broadcast({
    t: "hug",
    f: from.id,
    o: target.id,
    x: round((from.x + target.x) / 2, 2),
    z: round((from.z + target.z) / 2, 2),
    fs: from.score,
    os: target.score,
    fd: fromDelta,
    od: targetDelta,
  });
  return null;
}

// ------------------------------------------------------------------ bots ---

const BOT_NAMES = ["Mochi", "Biscuit", "Noodle", "Peach", "Waffles", "Bean", "Pudding", "Tofu"];

function makeBots(n) {
  for (let i = 0; i < n; i++) {
    const name = BOT_NAMES[i % BOT_NAMES.length];
    makePlayer(name, null, true);
    log(`bot ${name} joined`);
  }
}

function updateBots(dt) {
  const t = now();
  for (const bot of players.values()) {
    if (!bot.isBot) continue;

    // Look for someone nearby to hug.
    let nearest = null;
    let nearestD = Infinity;
    for (const other of players.values()) {
      if (other === bot) continue;
      const d = Math.hypot(other.x - bot.x, other.z - bot.z);
      if (d < nearestD) {
        nearestD = d;
        nearest = other;
      }
    }

    // Bots are generous but not farm bots: same target at most every 15s.
    const huggedRecently = nearest && t - (bot.botHuggedAt.get(nearest.id) || 0) < 15000;

    if (nearest && nearestD < HUG_RANGE * 0.8 && t - bot.lastHugAt > 2600 && !huggedRecently) {
      bot.botHuggedAt.set(nearest.id, t);
      tryHug(bot, nearest.id);
      // wander off after a hug
      bot.botDir = Math.atan2(bot.z - nearest.z, bot.x - nearest.x) + (Math.random() - 0.5);
      bot.botWanderAt = t + 1200;
    } else if (nearest && nearestD < 22) {
      // walk toward them
      bot.botDir = Math.atan2(nearest.z - bot.z, nearest.x - bot.x);
    } else if (t > bot.botWanderAt) {
      bot.botDir += (Math.random() - 0.5) * 2.4;
      bot.botWanderAt = t + 1500 + Math.random() * 2500;
    }

    const speed = PLAYER_SPEED * 0.55;
    let nx = bot.x + Math.cos(bot.botDir) * speed * dt;
    let nz = bot.z + Math.sin(bot.botDir) * speed * dt;
    [nx, nz] = clampToArena(nx, nz);
    bot.x = nx;
    bot.z = nz;
    bot.yaw = (bot.botDir * 180) / Math.PI;
  }
}

// ------------------------------------------------------------ http / ws ---

const MIME = {
  ".html": "text/html; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".mjs": "text/javascript; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".wasm": "application/wasm",
  ".data": "application/octet-stream",
  ".unityweb": "application/octet-stream",
  ".png": "image/png",
  ".jpg": "image/jpeg",
  ".svg": "image/svg+xml",
  ".ico": "image/x-icon",
  ".txt": "text/plain; charset=utf-8",
  ".br": "application/octet-stream",
  ".gz": "application/octet-stream",
};

const server = http.createServer((req, res) => {
  const url = new URL(req.url, "http://localhost");

  if (url.pathname === "/health") {
    res.writeHead(200, { "content-type": "application/json" });
    res.end(JSON.stringify({ ok: true, players: players.size, uptime: process.uptime() }));
    return;
  }

  let pathname = decodeURIComponent(url.pathname);
  if (pathname === "/") pathname = "/index.html";

  // Prevent path traversal.
  const filePath = normalize(join(WEB_DIR, pathname));
  if (!filePath.startsWith(WEB_DIR)) {
    res.writeHead(403).end("Forbidden");
    return;
  }

  let stat;
  try {
    stat = statSync(filePath);
  } catch {
    res.writeHead(404, { "content-type": "text/plain" }).end("Not found");
    return;
  }
  if (!stat.isFile()) {
    res.writeHead(404, { "content-type": "text/plain" }).end("Not found");
    return;
  }

  const ext = extname(filePath).toLowerCase();
  const headers = {
    "content-type": MIME[ext] || "application/octet-stream",
    "content-length": stat.size,
    // Builds are rebuilt often; never let a browser hold on to a stale .data/.wasm.
    "cache-control": "no-cache, no-store, must-revalidate",
    "etag": `"${stat.size.toString(16)}-${Math.floor(stat.mtimeMs).toString(16)}"`,
  };
  // Unity needs these for SharedArrayBuffer / threads (harmless otherwise).
  headers["cross-origin-opener-policy"] = "same-origin";
  headers["cross-origin-embedder-policy"] = "require-corp";
  headers["cross-origin-resource-policy"] = "cross-origin";

  res.writeHead(200, headers);
  createReadStream(filePath).pipe(res);
});

const wss = new WebSocketServer({ server, path: "/ws", maxPayload: 4096 });

wss.on("connection", (ws) => {
  /** @type {any} */
  let me = null;
  ws.isAlive = true;
  ws.on("pong", () => (ws.isAlive = true));

  ws.on("message", (raw) => {
    let msg;
    try {
      msg = JSON.parse(raw.toString());
    } catch {
      return;
    }
    if (!msg || typeof msg !== "object") return;

    if (msg.t === "join") {
      if (me) return;
      if (players.size >= MAX_PLAYERS) {
        send(ws, { t: "nope", m: "Server is full" });
        return;
      }
      me = makePlayer(msg.n, ws);
      if (msg.s) {
        const sx = Number(msg.x);
        const sz = Number(msg.z);
        if (Number.isFinite(sx) && Number.isFinite(sz)) {
          const [cx, cz] = clampToArena(sx, sz);
          me.x = cx;
          me.z = cz;
        }
      }
      send(ws, {
        t: "welcome",
        v: PROTOCOL_VERSION,
        i: me.id,
        n: me.name,
        c: me.color,
        a: ARENA_RADIUS,
        range: HUG_RANGE,
        speed: PLAYER_SPEED,
        rate: TICK_HZ,
      });
      broadcast({ t: "joined", i: me.id, n: me.name, c: me.color });
      send(ws, snapshot());
      log(`${me.name} (#${me.id}) joined — ${players.size} online`);
      return;
    }

    if (!me) return;
    const t = now();

    if (msg.t === "pos") {
      const x = Number(msg.x);
      const z = Number(msg.z);
      const r = Number(msg.r);
      if (!Number.isFinite(x) || !Number.isFinite(z)) return;

      // Light anti-cheat: clamp to arena and to a sane max speed.
      const dt = Math.max(0.001, (t - me.lastPosAt) / 1000);
      const maxStep = PLAYER_SPEED * dt * 1.8 + 1.2;
      let nx = x;
      let nz = z;
      const step = Math.hypot(x - me.x, z - me.z);
      if (step > maxStep) {
        const k = maxStep / step;
        nx = me.x + (x - me.x) * k;
        nz = me.z + (z - me.z) * k;
      }
      [nx, nz] = clampToArena(nx, nz);
      me.x = nx;
      me.z = nz;
      if (Number.isFinite(r)) me.yaw = ((r % 360) + 360) % 360;
      me.lastPosAt = t;
      return;
    }

    if (msg.t === "hug") {
      const err = tryHug(me, Number(msg.to));
      if (err) send(ws, { t: "nope", m: err });
      return;
    }

    if (msg.t === "ping") {
      send(ws, { t: "pong", ts: msg.ts });
    }
  });

  ws.on("close", () => {
    if (!me) return;
    players.delete(me.id);
    broadcast({ t: "left", i: me.id, n: me.name });
    log(`${me.name} (#${me.id}) left — ${players.size} online`);
  });
});

// Drop half-open sockets.
setInterval(() => {
  for (const ws of wss.clients) {
    if (ws.isAlive === false) {
      ws.terminate();
      continue;
    }
    ws.isAlive = false;
    ws.ping();
  }
}, 30000);

// ------------------------------------------------------------- game loop ---

const TICK_MS = 1000 / TICK_HZ;
let lastTick = performance.now();

setInterval(() => {
  const t = performance.now();
  const dt = Math.min(0.2, (t - lastTick) / 1000);
  lastTick = t;

  updateBots(dt);
  broadcast(snapshot());

  // Let hug poses expire so the flag flips back to 0.
  const n = now();
  for (const p of players.values()) {
    if (p.hugUntil && n > p.hugUntil) {
      p.hugUntil = 0;
      p.hugWith = 0;
    }
  }
}, TICK_MS);

function log(...args) {
  console.log(new Date().toISOString().slice(11, 19), ...args);
}

// ----------------------------------------------------------------- start ---

makeBots(BOT_COUNT);

server.listen(PORT, () => {
  log(`HUG.IO server on http://localhost:${PORT}  (ws://localhost:${PORT}/ws)`);
  log(`serving build from ${WEB_DIR}`);
  log(`${players.size} bots wandering`);
});
