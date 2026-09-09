// GET /api/debriefs
//   ?count=1&tzOffsetMinutes=N  — public: how many debriefs tonight (the social-norm chip).
//   (no query, Authorization: Bearer <ADMIN_KEY>) — every response, for the admin portal.
//
// ADMIN_KEY is a Vercel environment variable; without it set, the admin path is closed.
import { list } from '@vercel/blob';
import { sessionDateFor } from './_lib.js';

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
    // Same rule as the write path: the caller's date string is never trusted, only its
    // timezone offset, so this can't be used to probe an arbitrary night's count either.
    const session = sessionDateFor(req.query.tzOffsetMinutes);
    const blobs = await listAll(`debriefs/${session}/`);
    return res.status(200).json({ session, count: blobs.length });
  }

  const auth = req.headers.authorization || '';
  const key = auth.startsWith('Bearer ') ? auth.slice(7) : '';
  if (!process.env.ADMIN_KEY || key !== process.env.ADMIN_KEY) {
    return res.status(401).json({ error: 'Wrong or missing admin key.' });
  }

  // Security model, spelled out because "public" sounds looser than it is: each blob's URL
  // carries a crypto.randomUUID() suffix that is never returned by /api/debrief and never
  // rendered anywhere a non-admin can see it, so reaching a response's content means guessing
  // that UUID — not realistic. ADMIN_KEY is still the real boundary for the one thing that
  // actually enumerates them: this handler, which is the only code that calls list() (it needs
  // the server-side BLOB_READ_WRITE_TOKEN a browser never has) and only runs past the key check
  // above.
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
