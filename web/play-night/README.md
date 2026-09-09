# Play-night web kit (STORY-8.3)

The playtest debrief form and its admin portal, deployed on Vercel so testers open a
plain URL with no account. The protocol it implements is F3 of the product plan
(`docs/product-plan.md` on `feat/M8.1-feedback-loop` — a companion branch/PR to this
one; that file won't be on `main` until that PR merges, so the path here is a forward
reference, not a claim it's already committed).

| File | What it is |
|---|---|
| `index.html` | The tester debrief — six questions + rating, posts to `/api/debrief` |
| `admin.html` | Host-only log: per-night rollups, every response, CSV export |
| `api/debrief.js` | POST — validates and writes one JSON blob per response (Vercel Blob) |
| `api/debriefs.js` | GET — public tonight-count for the form's chip; full log behind `ADMIN_KEY` |
| `api/_lib.js` | Shared session-date logic (leading `_` keeps it out of Vercel's routing) |

## Deploying (once)

All commands run from this folder (`web/play-night/`) in a terminal:

1. `npx vercel login` — interactive; needs the Vercel account in the browser.
2. `npx vercel link` — create/link the Vercel project (accept the defaults; project
   name `foundry-play-night`).
3. In the browser: Vercel dashboard ▸ the project ▸ **Storage** ▸ Create ▸ **Blob** ▸
   connect it to the project. This injects `BLOB_READ_WRITE_TOKEN` automatically.
4. `npx vercel env add ADMIN_KEY production` — paste a long random string when
   prompted (e.g. from `openssl rand -hex 24`). This is the admin portal's key.
5. `npx vercel deploy --prod` — prints the live URL. The form is `/`, the portal
   is `/admin.html`.

Verification: open the URL on a phone, submit a test debrief, then open
`/admin.html`, enter the key, and confirm the response shows. Delete test rows from
the Vercel dashboard ▸ Storage ▸ Blob if you care.

## Redeploying after edits

`npx vercel deploy --prod` from this folder. Nothing else.

## Notes

- Responses are one JSON blob each under `debriefs/<YYYY-MM-DD>/`. The date is
  computed server-side from the submitter's timezone offset, never from a client-
  sent date — see `api/_lib.js`. Blob URLs carry an unguessable random suffix and
  are never disclosed to a non-admin caller; `ADMIN_KEY` gates the one code path
  (`api/debriefs.js`'s full-log branch) that ever lists or reads them back.
- The submit endpoint is public by design (testers have no accounts). Defenses,
  in order: a honeypot field, hard field caps, and a 500-response-per-night cap
  that bounds worst-case storage and the admin portal's read cost. None of this
  is real rate limiting — proportionate for a friend-group link, not for a
  publicly shared one.
- The admin key lives in `sessionStorage`, not `localStorage`: it doesn't survive
  closing the tab, so a shared or borrowed browser doesn't leave the log open.
- The CSV export prefixes any cell starting with `=`, `+`, `-` or `@` so a
  tester-supplied name or note can't execute as a formula when opened in a
  spreadsheet app.
- This form answers the *qualitative* half of the design doc's §11 open
  questions (fun peak, confusion, priority fairness, Sparks). Match duration and
  player-count come from the telemetry funnel instead (`round_completed`,
  `match_completed` — see F1 in the product plan), which is the more reliable
  source for numbers a human is bad at estimating after the fact.
- The page uses the game's own "chunky arcade" palette from
  `Assets/_Project/Code/SceneTools/ThemeGenerator.cs` — if the theme changes,
  change the CSS tokens at the top of both HTML files to match.
