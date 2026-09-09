// POST /api/debrief — one tester's debrief, written as one JSON blob under
// debriefs/<session-date>/. Public endpoint by design (testers have no accounts);
// the honeypot field, field caps, the per-night cap below, and friend-group scale are the
// whole defense — there is no rate limiting, so a determined script could still burst well
// past normal use before the cap catches it. Acceptable at this scale; revisit before ever
// sharing the link somewhere public.
import { list, put } from '@vercel/blob';
import { sessionDateFor } from './_lib.js';

// Bounds worst-case storage and the admin portal's unbounded fetch-everything read (#103
// review) — not a substitute for real rate limiting, just a backstop against a single night
// running away.
const MAX_PER_SESSION = 500;

const PHASES = ['Roll', 'Shape', 'Commit', 'Reveal', 'Re-pick', 'Upkeep',
  'Final scores', 'Scoring', 'Nowhere'];

const line = (v, max) => {
  if (v == null) return null;
  const s = String(v).trim().slice(0, max);
  return s.length ? s : null;
};
const pick = (v, allowed) => (allowed.includes(v) ? v : null);

export default async function handler(req, res) {
  if (req.method !== 'POST') {
    res.setHeader('Allow', 'POST');
    return res.status(405).json({ error: 'POST only' });
  }

  const b = req.body || {};

  // Honeypot: a visually hidden field no human fills. Pretend success so bots move on.
  if (b.website) return res.status(200).json({ ok: true });

  const name = line(b.name, 24);
  if (!name) return res.status(400).json({ error: 'Add your name first.' });

  const session = sessionDateFor(b.tzOffsetMinutes);
  const prefix = `debriefs/${session}/`;

  const doc = {
    session,
    submittedAt: new Date().toISOString(),
    name,
    funPeak: pick(b.funPeak, PHASES),
    funPeakNote: line(b.funPeakNote, 200),
    confusion: pick(b.confusion, PHASES),
    confusionNote: line(b.confusionNote, 200),
    priorityFair: pick(b.priorityFair, ['Fair', "Didn't notice", 'Unfair']),
    priorityNote: line(b.priorityNote, 200),
    sparksWorthTracking: pick(b.sparksWorthTracking, ['Yes', 'Meh', 'No']),
    playAgain: pick(b.playAgain, ['Yes', 'Maybe', 'No']),
    invite: line(b.invite, 120),
    rating: [1, 2, 3, 4, 5].includes(b.rating) ? b.rating : null,
  };

  if (!doc.playAgain) {
    return res.status(400).json({ error: 'Question 4 needs an answer.' });
  }

  const { blobs } = await list({ prefix, limit: MAX_PER_SESSION });
  if (blobs.length >= MAX_PER_SESSION) {
    return res.status(429).json({ error: "Tonight's log is full — tell Aaron directly." });
  }

  // addRandomSuffix keeps blob URLs unguessable; the admin API is the read path — see the
  // README for what that does and doesn't guarantee.
  await put(`${prefix}${crypto.randomUUID()}.json`, JSON.stringify(doc), {
    access: 'public',
    contentType: 'application/json',
    addRandomSuffix: true,
  });

  return res.status(200).json({ ok: true });
}
