/**
 * HUG.IO shared protocol constants.
 *
 * The Unity client mirrors these values in Assets/Scripts/Net/Protocol.cs.
 * Keep the two files in sync.
 */

export const PROTOCOL_VERSION = 1;

export const ARENA_RADIUS = 30; // metres, playable disc
export const PLAYER_SPEED = 7.0; // metres / second (client movement speed)
export const PLAYER_RADIUS = 0.45;

export const HUG_RANGE = 3.4; // max distance between centres to hug
export const HUG_COOLDOWN_MS = 650; // per-hugger cooldown
export const HUG_LOCK_MS = 650; // per-target lock (prevents instant re-hug)
export const HUG_ANIM_MS = 900; // how long the hug pose is held

// --- hug scoring -----------------------------------------------------------
// Close scores: a hug is simply worth a point each.
export const HUG_EVEN_POINTS = 1;
// "Close" means the two scores are at most this far apart.
export const HUG_FAIR_GAP = 100;
// Far apart: the two players exchange 10% of their own score with each other.
export const HUG_EXCHANGE_RATE = 0.1;

/**
 * Scores after a hug. Each player hands over 10% of their own score and
 * receives 10% of the other player's — a catch-up mechanic. When the two are
 * within HUG_FAIR_GAP of each other the hug is just worth a point each.
 *
 * @returns {[number, number]} new [a, b]
 */
export function hugScores(a, b) {
  if (Math.abs(a - b) <= HUG_FAIR_GAP) {
    return [a + HUG_EVEN_POINTS, b + HUG_EVEN_POINTS];
  }
  const giveA = Math.round(a * HUG_EXCHANGE_RATE);
  const giveB = Math.round(b * HUG_EXCHANGE_RATE);
  return [a - giveA + giveB, b - giveB + giveA];
}

export const TICK_HZ = 20; // snapshot broadcast rate
export const MAX_NAME_LEN = 14;
export const MAX_PLAYERS = 64;

export const COLORS = [
  "#ff6b9d", // pink
  "#ffd166", // yellow
  "#6ee7ff", // cyan
  "#a78bfa", // purple
  "#7cf29c", // mint
  "#ff9f6e", // orange
  "#8ab4ff", // blue
  "#ff8fab", // rose
  "#c4f24d", // lime
  "#f9a8d4", // blush
];

/** Strip control chars / tags and clamp a display name. */
export function cleanName(raw) {
  let name = String(raw ?? "")
    .replace(/[\u0000-\u001f\u007f-\u009f]/g, "")
    .replace(/[<>]/g, "")
    .trim();
  if (!name) name = "Anon";
  if (name.length > MAX_NAME_LEN) name = name.slice(0, MAX_NAME_LEN);
  return name;
}

export function clampToArena(x, z) {
  const d = Math.hypot(x, z);
  const max = ARENA_RADIUS - PLAYER_RADIUS;
  if (d <= max) return [x, z];
  const k = max / (d || 1);
  return [x * k, z * k];
}
