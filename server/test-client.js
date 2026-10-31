/**
 * End-to-end protocol test. Starts the server on a throwaway port, connects
 * three fake clients and checks movement + hug scoring rules.
 *
 *   node test-client.js
 */

import assert from "node:assert/strict";
import { hugScores } from "./protocol.js";
import { spawn } from "node:child_process";
import { WebSocket } from "ws";

const PORT = 8123;
const WS_URL = `ws://127.0.0.1:${PORT}/ws`;

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

function connect(name) {
  return new Promise((resolve, reject) => {
    const ws = new WebSocket(WS_URL);
    const inbox = [];
    let cursor = 0;
    const waiters = [];
    ws.on("message", (raw) => {
      const msg = JSON.parse(raw.toString());
      inbox.push(msg);
      const idx = waiters.findIndex((w) => w.pred(msg));
      if (idx !== -1) {
        const [w] = waiters.splice(idx, 1);
        clearTimeout(w.timer);
        w.resolve(msg);
      }
    });
    ws.on("error", reject);
    ws.on("open", () => {
      ws.send(JSON.stringify({ t: "join", n: name }));
      resolve({
        ws,
        name,
        inbox,
        send: (m) => ws.send(JSON.stringify(m)),
        waitFor(pred, ms = 3000) {
          for (let i = cursor; i < inbox.length; i++) {
            if (pred(inbox[i])) {
              cursor = i + 1;
              return Promise.resolve(inbox[i]);
            }
          }
          return new Promise((res, rej) => {
            const timer = setTimeout(() => rej(new Error(`timeout waiting for ${pred}`)), ms);
            waiters.push({
              pred,
              timer,
              resolve: (m) => {
                cursor = Math.max(cursor, inbox.indexOf(m) + 1);
                res(m);
              },
            });
          });
        },
        close: () => ws.close(),
      });
    });
  });
}

/** Walk a client toward a point in small steps the server will accept. */
async function walkTo(client, targetX, targetZ, steps = 30) {
  const snap = await client.waitFor((m) => m.t === "snap");
  const me = snap.p.find((p) => p.n === client.name);
  let { x, z } = me;
  for (let i = 1; i <= steps; i++) {
    const k = i / steps;
    client.send({ t: "pos", x: x + (targetX - x) * k, z: z + (targetZ - z) * k, r: 0 });
    await sleep(40);
  }
}

// --- pure scoring rule -------------------------------------------------------
const scoring = [
  ["both at zero start the board", [0, 0], [1, 1]],
  ["close scores get a point each", [5, 3], [6, 4]],
  ["exactly 100 apart is still close", [100, 0], [101, 1]],
  ["101 apart swaps 10%", [101, 0], [91, 10]],
  ["big gap swaps 10% both ways", [1000, 500], [950, 550]],
  ["the gap keeps shrinking", [900, 100], [820, 180]],
];

const results = [];
function check(name, fn) {
  try {
    fn();
    results.push(["PASS", name]);
  } catch (e) {
    results.push(["FAIL", name, e.message]);
  }
}

const server = spawn(process.execPath, ["server.js", "--port", String(PORT), "--bots", "0"], {
  cwd: new URL(".", import.meta.url).pathname,
  stdio: ["ignore", "pipe", "pipe"],
});
server.stderr.on("data", (d) => process.stderr.write(`[server] ${d}`));

for (const [name, input, expected] of scoring) {
  check(name, () => {
    assert.deepEqual(hugScores(input[0], input[1]), expected);
  });
}

try {
  // Wait for the port to open.
  for (let i = 0; i < 50; i++) {
    try {
      await fetch(`http://127.0.0.1:${PORT}/health`);
      break;
    } catch {
      await sleep(100);
    }
  }

  const a = await connect("Alice");
  const b = await connect("Bob");
  const c = await connect("Carol");

  const wa = await a.waitFor((m) => m.t === "welcome");
  const wb = await b.waitFor((m) => m.t === "welcome");
  const wc = await c.waitFor((m) => m.t === "welcome");

  check("welcome gives unique ids", () => {
    assert.equal(new Set([wa.i, wb.i, wc.i]).size, 3);
    assert.ok(wa.i > 0);
    assert.equal(wa.a, 30);
    assert.equal(wa.v, 1);
  });

  check("welcome sanitises name", () => {
    assert.equal(wa.n, "Alice");
  });

  // Everyone should see three players in the snapshot.
  await sleep(150);
  const snap = await a.waitFor((m) => m.t === "snap" && m.p.length >= 3);
  check("snapshot lists all players", () => {
    assert.equal(snap.p.length, 3);
    assert.ok(snap.p.every((p) => typeof p.n === "string" && typeof p.s === "number"));
  });

  // Move Alice and Bob to the same spot, Carol far away.
  await walkTo(a, 0, 0);
  await walkTo(b, 1.5, 0);
  await walkTo(c, 20, 20);

  const farErr = await (async () => {
    a.send({ t: "hug", to: wc.i });
    return a.waitFor((m) => m.t === "nope", 1500);
  })();
  check("hug out of range is rejected", () => {
    assert.equal(farErr.m, "Too far away");
  });

  await sleep(800); // clear the cooldown from the rejected attempt

  a.send({ t: "hug", to: wb.i });
  const hugEvent = await a.waitFor((m) => m.t === "hug", 2000);
  check("hug broadcasts an event", () => {
    assert.equal(hugEvent.f, wa.i);
    assert.equal(hugEvent.o, wb.i);
    assert.equal(hugEvent.fs, 1);
    assert.equal(hugEvent.os, 1);
  });

  const hugSeenByC = await c.waitFor((m) => m.t === "hug" && m.f === wa.i, 2000);
  check("hug event reaches every client", () => {
    assert.equal(hugSeenByC.o, wb.i);
  });

  await sleep(150);
  const snap2 = await b.waitFor(
    (m) => m.t === "snap" && m.p.find((p) => p.i === wa.i)?.s === 1,
  );
  check("both players gain a hug point", () => {
    assert.equal(snap2.p.find((p) => p.i === wa.i).s, 1);
    assert.equal(snap2.p.find((p) => p.i === wb.i).s, 1);
    assert.equal(snap2.p.find((p) => p.i === wc.i).s, 0);
  });

  a.send({ t: "hug", to: wb.i });
  const cd = await a.waitFor((m) => m.t === "nope", 1500);
  check("hug cooldown is enforced", () => {
    assert.equal(cd.m, "Easy there!");
  });

  check("snapshot marks the hug pose", () => {
    const p = snap2.p.find((x) => x.i === wa.i);
    assert.equal(p.h, 1);
  });

  // --- close scores: Alice(1) hugs Carol(0) => +1 each
  await walkTo(c, 0.6, 1.4);
  await sleep(800);
  a.send({ t: "hug", to: wc.i });
  const h2 = await a.waitFor((m) => m.t === "hug" && m.o === wc.i, 2000);
  check("close scores get a point each", () => {
    assert.equal(h2.fs, 2);
    assert.equal(h2.os, 1);
    assert.equal(h2.fd, 1);
    assert.equal(h2.od, 1);
  });

  await sleep(150);
  const snap3 = await b.waitFor((m) => m.t === "snap" && m.p.find((p) => p.i === wa.i)?.s === 2);
  check("everyone sees the new scores", () => {
    assert.equal(snap3.p.find((p) => p.i === wa.i).s, 2);
    assert.equal(snap3.p.find((p) => p.i === wc.i).s, 1);
    assert.equal(snap3.p.find((p) => p.i === wb.i).s, 1);
  });

  a.close();
  const left = await b.waitFor((m) => m.t === "left" && m.i === wa.i, 3000);
  check("disconnect broadcasts leave", () => {
    assert.equal(left.n, "Alice");
  });

  b.close();
  c.close();
} catch (e) {
  results.push(["FAIL", "harness", e.stack || e.message]);
} finally {
  await sleep(100);
  server.kill("SIGTERM");
}

for (const [status, name, msg] of results) {
  console.log(`${status === "PASS" ? "  ok " : "FAIL "} ${name}${msg ? `\n       ${msg}` : ""}`);
}
const failed = results.filter((r) => r[0] === "FAIL").length;
console.log(`\n${results.length - failed}/${results.length} passed`);
process.exit(failed ? 1 : 0);
