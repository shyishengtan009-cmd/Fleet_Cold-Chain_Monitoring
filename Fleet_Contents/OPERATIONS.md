# Operations Guide

How to run this locally, redeploy it, and recover access if you're setting up on a new
machine. This file is committed to the public repo — it never contains real secrets,
only where to find/regenerate them.

## Live deployment

- Frontend: https://fleet-frontend-3b1t.onrender.com/monitoring/fleet/dashboard
- Backend health check: https://fleet-cold-chain-monitoring.onrender.com/api/fleet/health
- Hosting: Render free tier (backend = Web Service "Fleet_Cold-Chain_Monitoring", frontend =
  Static Site "fleet-frontend"), both under the same Render account/workspace.
- Database: Neon Postgres, free tier, project name "Fleet Cold Chain".

## Setting up on a new machine

1. Clone the repo, `cd` into it.
2. Install prerequisites: .NET 8 SDK, Node.js (18+), and optionally `psql` if you ever need
   to inspect the database directly.
3. Get the Neon connection string: log into [neon.tech](https://neon.tech) → "Fleet Cold
   Chain" project → Connection Details → copy the pooler connection string (the one with
   `-pooler` in the hostname, not the direct host — `FleetSimService` opens bursts of
   short-lived connections every 30s, which the pooler is built for).
4. Convert it to the format Npgsql needs. Neon gives you
   `postgresql://user:pass@host/db?sslmode=require`; Npgsql needs:
   ```
   Host=...;Database=...;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true;
   ```
5. Create `Fleet_Contents/Backend/ColdChainFleet/appsettings.Development.json` (gitignored,
   never committed) with that connection string:
   ```json
   { "Database": { "ConnectionString": "Host=...;Database=...;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true;" } }
   ```
6. Run the backend: `cd Fleet_Contents/Backend/ColdChainFleet && dotnet run`. First run applies
   all schema migrations automatically and starts `FleetSimService` generating data. Listens
   on `http://localhost:5276`.
7. Run the frontend: `cd Fleet_Contents/Frontend && npm install && npm run dev`. Open the
   printed local URL + `/monitoring/fleet/dashboard`.

## Redeploying after a code change

Both Render services auto-deploy on every push to `main`. There's no separate "deploy" step —
just commit and push.

**Before pushing anything risky**: verify both builds succeed locally first —
`dotnet build` in `Fleet_Contents/Backend/ColdChainFleet` and `npm run build` in
`Fleet_Contents/Frontend`. If a build fails on Render, the previous working deploy stays live
(safe). If it builds but has a runtime bug, the live site breaks immediately on deploy
(not safe) — so test locally first, and for larger changes, work on a separate branch and
only merge to `main` once confirmed.

## Recovering Render access on a new machine

1. Log into [render.com](https://render.com) with the account this was set up under.
2. You'll see two services: `Fleet_Cold-Chain_Monitoring` (backend, Docker Web Service) and
   `fleet-frontend` (Static Site).
3. Backend env vars (Environment tab) you should already see set, no need to re-enter unless
   reconfiguring from scratch:
   - `Database__ConnectionString` — the Neon pooler string (see above)
   - `Jwt__Key` — a random signing key (regenerate via Render's "Generate" button if lost;
     it's just a demo JWT secret, no migration needed)
   - `Cors__AllowedOrigins` — must equal the frontend's exact Render URL
4. Frontend build settings (Settings tab): build command writes `public/appsettings.json`
   from the `BACKEND_URL` env var before `vite build` — see that env var is set to the
   backend's Render URL.
5. Frontend Redirects/Rewrites tab must have: Source `/*`, Destination `/index.html`,
   Action `Rewrite`. Without this, direct links/refreshes to any page other than the root
   404 (Render doesn't auto-fallback to `index.html` for client-side routes by default).

## Known gotchas (don't rediscover these)

- **Npgsql rejects Neon's default `postgresql://` URI connection string** — it only accepts
  the classic `Key=Value;` ADO.NET format. Convert before pasting anywhere.
- **CORS** is dynamic (`Program.cs`'s `SetIsOriginAllowed`) — allows any `localhost`/
  `127.0.0.1` port for local dev, plus whatever's in `Cors:AllowedOrigins` config for
  production. If you add another frontend deployment, add its URL there too.
- **Free-tier cold start**: the backend sleeps after ~15 min idle; first request after that
  waits ~30-60s. `FleetSimService` (the background data generator) also stops while the
  backend is asleep, so data can look briefly stale right after a cold start — this is
  expected, not a bug.
- **A stale `dotnet run` process can squat on port 5276** if you restart without fully
  killing the previous one — check `netstat -ano` for the actual PID if you get unexplained
  404s on routes you can see exist in the controller source.
- **The Render Static Site rewrite rule** (see above) is the fix for both "direct link
  404s" and a confusing CSS `MIME type ('text/plain')` console error — they're the same
  root cause, not two separate bugs.

## Things deliberately not migrated/automated

- No CI pipeline yet — builds are only verified manually before pushing.
- No automated tests yet.
- Demo data freshness depends entirely on `FleetSimService` having been running; there's a
  one-time historical backfill (migration `0017_backfill_demo_history.sql`) but it only ran
  once — it won't replay on a fresh database without manually adding a similar migration.
