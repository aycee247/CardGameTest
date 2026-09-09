# Play-night web kit (STORY-8.3)

The playtest debrief form and its admin portal, deployed on Vercel so testers open a
plain URL with no account. The protocol it implements is `docs/product-plan.md` §F3.

| File | What it is |
|---|---|
| `index.html` | The tester debrief — five questions + rating, posts to `/api/debrief` |
| `admin.html` | Host-only log: per-night rollups, every response, CSV export |
| `api/debrief.js` | POST — validates and writes one JSON blob per response (Vercel Blob) |
| `api/debriefs.js` | GET — public tonight-count for the form's chip; full log behind `ADMIN_KEY` |

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

- Responses are one JSON blob each under `debriefs/<YYYY-MM-DD>/` with unguessable
  URLs; the admin API (`ADMIN_KEY`) is the read path. Fine for friend-group data;
  not a place for anything sensitive.
- The submit endpoint is public by design (testers have no accounts); a honeypot
  field and hard field caps are the spam defense, which is proportionate at this
  scale.
- The page uses the game's own "chunky arcade" palette from
  `Assets/_Project/Code/SceneTools/ThemeGenerator.cs` — if the theme changes,
  change the CSS tokens at the top of both HTML files to match.
