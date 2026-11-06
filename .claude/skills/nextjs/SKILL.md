---
name: nextjs
description: WHEN adding or changing routes, server components, server actions, config, styling or the build in the Next.js 16 web app in apps/web.
---

# Next.js app (`apps/web`)

Next.js 16.4 App Router, React 19, TypeScript, Tailwind CSS 4. `apps/web/AGENTS.md` warns that this Next.js differs from what you remember: read the matching guide in `apps/web/node_modules/next/dist/docs/` (after `npm ci`) before using an API you're unsure of, and heed deprecation notices.

## Commands

Run from `apps/web`.

```bash
npm ci                                              # install from the lockfile
API_BASE_URL=http://localhost:8080 npm run dev      # http://localhost:3000
npm run lint                                        # eslint, next core-web-vitals + typescript
npm run typecheck                                   # tsc --noEmit
npm run build                                       # production build, standalone output
```

`next dev` rewrites `AGENTS.md` between its `BEGIN:nextjs-agent-rules` markers. Leave that block as it is.

## How the app is built

- **Server components by default.** Pages in `src/app/**/page.tsx` are async server components that call `src/lib/api` directly. Only files with `"use client"` run in the browser: `CustomerForm`, `DeleteCustomerButton` and `app/customers/error.tsx`.
- **`params` and `searchParams` are Promises** in this version. `await` them (`const { id } = await params`), as `customers/page.tsx` and `customers/[id]/edit/page.tsx` do.
- **Pages that read the API export `const dynamic = "force-dynamic"`** (`/customers`, `/customers/[id]/edit`; `/api/health` too). Never prerender customer data at build time: the build has no `API_BASE_URL` and no API.
- **`/` redirects to `/customers`** with `redirect()` from `next/navigation`, and `/customers?page=N` past the last page redirects to the last page (keeping `search`). An unknown customer id calls `notFound()`: the not-found page renders, but because `customers/loading.tsx` has already started streaming the response, the HTTP status is 200 with a `noindex` tag, not 404. That's Next.js behaviour, not a bug.
- **Server actions** live in `src/app/customers/actions.ts` (`"use server"`). Forms bind them with `useActionState`; delete calls one inside `useTransition`. `revalidatePath("/customers")` then `redirect(...)` go after the `try/catch`, because `redirect` works by throwing.
- **The edit page binds the id:** `updateCustomerAction.bind(null, id)` gives `CustomerForm` an action with the `(state, formData)` signature.
- **`error.tsx` and `loading.tsx`** under `src/app/customers/` are the error boundary (a client component; `reset()` retries) and the Suspense skeleton for that segment. An API failure while rendering a page lands in `error.tsx`.
- **`GET /api/health`** (`src/app/api/health/route.ts`) returns `200 Healthy` and never calls the API. Kubernetes probes use it, so a backend outage doesn't restart pods.
- **No `next/font`.** `globals.css` sets a system font stack, so the build needs no network access to Google Fonts.
- **Config (`next.config.ts`):**
  - `output: "standalone"` makes `.next/standalone/server.js` with traced `node_modules`. It doesn't include `.next/static` or `public/`; the Dockerfile copies them beside it and the container runs `node server.js`.
  - Tailwind 4 goes through a Turbopack rule: `turbopack.rules["*.css"]` with the `@tailwindcss/turbopack` loader. `globals.css` is just `@import "tailwindcss";`. There's no `tailwind.config` file. Don't add a PostCSS plugin on top.

## Environment

`API_BASE_URL` is read from `process.env` on each request in `src/lib/api/http.ts`. Never use a `NEXT_PUBLIC_` variable for it, and never read it at module load or in a client component: that would bake it into the build or expose it to the browser.

## Gotchas

- Importing `src/lib/api/*` from a client component fails the build (`server-only`). Pass data down as props, or call a server action.
- `redirect()` and `notFound()` throw. Don't put them in a `try` block with a catch-all.
- Server actions are public POST endpoints. The API validates; never trust an `id` or redirect target without checking it.
- `CustomerForm` re-keys its `<form>` on its state on purpose: it makes `defaultValue` show the values the server echoed back.
- `npm run build` fails on type and lint errors that `next dev` tolerates. Run the full check list before a PR.

Related skills: `ui-page` (end-to-end recipe), `web-api-layer`, `jest-rtl`, `docker` (image), `helm` (chart).
