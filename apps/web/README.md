# Customer web app

Next.js 16 (App Router, React 19, Tailwind CSS) UI for the customer API in `apps/api`: list, search, add, edit and delete customers. No login. All backend calls happen on the server (`src/lib/api`, server actions); the browser never talks to the API directly.

## Run

```bash
cd apps/web
npm ci
API_BASE_URL=http://localhost:8080 npm run dev     # http://localhost:3000
```

Start the API first, for example `docker compose up -d sqlserver api` (or `podman compose ...`) from the repo root.

## Environment variables

| Variable | Required | Description |
|---|---|---|
| `API_BASE_URL` | yes | Base URL of the customer API, e.g. `http://localhost:8080`. Read at request time, so one build works in every environment. |

`GET /api/health` returns `200 Healthy` without calling the backend (use it for Kubernetes probes).

## Scripts

| Script | Purpose |
|---|---|
| `npm run dev` / `build` / `start` | Next.js dev server, production build (`output: "standalone"`), production server |
| `npm run lint` | ESLint |
| `npm run typecheck` | `tsc --noEmit` |
| `npm test` | Jest + React Testing Library |
| `npm run test:coverage` | Jest with the 80% global coverage gate |

Tests live in `src/__tests__`. Coverage excludes only `src/app/layout.tsx` and `src/app/customers/loading.tsx` (static boilerplate).
