// POST /api/debrief — one tester's debrief, written as one JSON blob under
// debriefs/<session-date>/. Public endpoint by design (testers have no accounts);
// the honeypot field, field caps, and friend-group scale are the whole defense.
import { put } from '@vercel/blob';

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

  const session = /^\d{4}-\d{2}-\d{2}$/.test(b.session)
    ? b.session
    : new Date().toISOString().slice(0, 10);

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
    playAgain: pick(b.playAgain, ['Yes', 'Maybe', 'No']),
    invite: line(b.invite, 120),
    rating: [1, 2, 3, 4, 5].includes(b.rating) ? b.rating : null,
  };

  if (!doc.playAgain) {
    return res.status(400).json({ error: 'Question 4 needs an answer.' });
  }

  // addRandomSuffix keeps blob URLs unguessable; the admin API is the read path.
  await put(`debriefs/${session}/${crypto.randomUUID()}.json`, JSON.stringify(doc), {
    access: 'public',
    contentType: 'application/json',
    addRandomSuffix: true,
  });

  return res.status(200).json({ ok: true });
}
