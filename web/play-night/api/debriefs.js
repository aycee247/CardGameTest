// GET /api/debriefs
//   ?count=1&session=YYYY-MM-DD  — public: how many debriefs tonight (the social-norm chip).
//   (no query, Authorization: Bearer <ADMIN_KEY>) — every response, for the admin portal.
//
// ADMIN_KEY is a Vercel environment variable; without it set, the admin path is closed.
import { list } from '@vercel/blob';

async function listAll(prefix) {
  const blobs = [];
  let cursor;
  do {
    const page = await list({ prefix, cursor, limit: 1000 });
    blobs.push(...page.blobs);
    cursor = page.cursor;
  } while (cursor);
  return blobs;
}

export default async function handler(req, res) {
  if (req.method !== 'GET') {
    res.setHeader('Allow', 'GET');
    return res.status(405).json({ error: 'GET only' });
  }

  if (req.query.count) {
    const session = /^\d{4}-\d{2}-\d{2}$/.test(req.query.session)
      ? req.query.session
      : new Date().toISOString().slice(0, 10);
    const blobs = await listAll(`debriefs/${session}/`);
    return res.status(200).json({ session, count: blobs.length });
  }

  const auth = req.headers.authorization || '';
  const key = auth.startsWith('Bearer ') ? auth.slice(7) : '';
  if (!process.env.ADMIN_KEY || key !== process.env.ADMIN_KEY) {
    return res.status(401).json({ error: 'Wrong or missing admin key.' });
  }

  const blobs = await listAll('debriefs/');
  const responses = await Promise.all(blobs.map(async (blob) => {
    try {
      const r = await fetch(blob.url);
      return await r.json();
    } catch {
      return null; // a torn or non-JSON blob shouldn't take the whole portal down
    }
  }));

  responses.sort((a, b) => {
    const at = a && a.submittedAt ? a.submittedAt : '';
    const bt = b && b.submittedAt ? b.submittedAt : '';
    return at < bt ? 1 : -1;
  });

  return res.status(200).json({ responses: responses.filter(Boolean) });
}
