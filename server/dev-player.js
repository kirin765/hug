/**
 * A dumb scripted player, handy for testing by hand:
 *
 *   node dev-player.js Bob 1.6 0
 *
 * Joins the local server, stands still and logs every hug it sees. Press E in
 * the browser next to it and watch the scores change.
 */

import { WebSocket } from "ws";

const [name = "Bob", x = "0", z = "0"] = process.argv.slice(2);
const url = process.env.HUG_URL || "ws://127.0.0.1:8080/ws";

const ws = new WebSocket(url);

ws.on("open", () => {
  ws.send(JSON.stringify({ t: "join", n: name, s: true, x: Number(x), z: Number(z) }));
  console.log(`[${name}] joining at (${x}, ${z})`);
});

ws.on("message", (raw) => {
  const m = JSON.parse(raw.toString());
  switch (m.t) {
    case "welcome":
      console.log(`[${name}] joined as #${m.i}`);
      break;
    case "hug":
      console.log(
        `[${name}] HUG #${m.f} -> #${m.o} | scores ${m.fs} / ${m.os} | delta ${fmt(m.fd)} / ${fmt(m.od)}`,
      );
      break;
    case "nope":
      console.log(`[${name}] rejected: ${m.m}`);
      break;
    case "snap":
      if (process.env.VERBOSE) {
        console.log(
          `[${name}] ` + m.p.map((p) => `${p.n}=${p.s}`).join("  "),
        );
      }
      break;
  }
});

ws.on("close", () => console.log(`[${name}] disconnected`));
ws.on("error", (e) => console.log(`[${name}] error ${e.message}`));

function fmt(n) {
  return n > 0 ? `+${n}` : String(n);
}

setInterval(() => {
  if (ws.readyState === 1) ws.send(JSON.stringify({ t: "ping", ts: Date.now() }));
}, 3000);
