---
name: jest-rtl
description: WHEN writing, running or debugging Jest and React Testing Library tests in apps/web/src/__tests__, or changing the Jest config or coverage threshold.
---

# Jest + React Testing Library (`apps/web`)

## Setup

- `jest.config.ts` uses `next/jest` (`nextJest({ dir: "./" })`), so SWC compiles TypeScript and JSX. The test environment is `jsdom`.
- `testMatch` is `**/*.test.{ts,tsx}`. Tests live in `src/__tests__/`; `fixtures.ts` there has `makeCustomer(overrides)`, a complete `Customer`.
- `jest.setup.ts` imports `@testing-library/jest-dom`, which gives matchers like `toBeInTheDocument`, `toHaveValue`, `toBeRequired`.
- `moduleNameMapper`:
  - `^server-only$` goes to `test/empty-module.ts` (an empty module), because the real package throws outside a server bundle.
  - `^@/(.*)$` goes to `src/$1`, matching the `@/*` path in `tsconfig.json`.
- Coverage (`npm run test:coverage`) collects `src/**/*.{ts,tsx}` and excludes `src/app/layout.tsx`, `src/app/customers/loading.tsx`, `src/__tests__/**` and `*.d.ts`. The global threshold is 80% for lines, branches, functions and statements. Only exclude static boilerplate with no logic, never to dodge a gap.

## Commands

Run from `apps/web`.

```bash
npm test                                   # all tests
npm test -- src/__tests__/api.test.ts      # one file
npm test -- -t "redirects"                 # tests whose name matches
npm run test:coverage                      # with the 80% gate; report in coverage/
```

## Test files

| File | Covers | What it mocks |
|---|---|---|
| `api.test.ts` | `apiFetch`, `ApiError`, `customers.ts` | `global.fetch` |
| `actions.test.ts` | the server actions | `next/cache`, `next/navigation`, `@/lib/api/customers` |
| `pages.test.tsx` | server-component pages (called as functions, then rendered) | `next/navigation`, `@/lib/api/customers`, `@/app/customers/actions` |
| `components.test.tsx` | components and `error.tsx` | `@/app/customers/actions` |
| `health.test.ts` | `/api/health` (`/** @jest-environment node */`, because it uses `Response`) | `fetch`, to assert it's never called |

## Patterns

- **Mock the layer below, not the network.** Pages and actions mock `@/lib/api/customers` with `jest.mock("@/lib/api/customers", () => ({ listCustomers: jest.fn(), ... }))` and cast to `jest.Mock` to set return values. Only `api.test.ts` mocks `fetch`.
- **`next/navigation`:** `redirect` and `notFound` throw in Next. Mock them to throw too (`jest.fn(() => { throw new Error("NEXT_REDIRECT"); })`) and assert `await expect(action(...)).rejects.toThrow("NEXT_REDIRECT")`, so the code after a redirect doesn't run. For pages that only call `redirect()` (like `Home`), a plain `jest.fn()` is enough.
- **`next/cache`:** `jest.mock("next/cache", () => ({ revalidatePath: jest.fn() }))`, then assert `toHaveBeenCalledWith("/customers")`.
- **Async server components:** `render(await CustomersPage({ searchParams: Promise.resolve({ page: "2" }) }))`. `params` and `searchParams` are Promises.
- **Server actions:** build a `FormData` with `data.set(name, value)` and call the action directly with `initialFormState`. Assert both the returned `CustomerFormState` and the arguments of the API call.
- **Components:** use `userEvent` (`await userEvent.click(...)`, `await userEvent.type(...)`), not `fireEvent`. Query by role and accessible name (`getByRole("button", { name: "Delete Ada" })`, `getByLabelText(/email/i)`) so tests also check accessibility. For a client component that calls a server action, mock `@/app/customers/actions` and use `findBy...` or `waitFor` for the state update.
- **`window.confirm`:** `jest.spyOn(window, "confirm").mockReturnValue(true | false)`, and `jest.restoreAllMocks()` in `afterEach`.
- **Reset state:** `beforeEach(() => jest.clearAllMocks())` in files with module mocks; `fetchMock.mockReset()` in `api.test.ts`.
- **Names:** plain sentences in `it("...")`, one behaviour each; group by unit with `describe`.

## Gotchas

- `jest.mock` factories are hoisted above imports; reference only `jest.fn()` and values defined inside the factory.
- A component that imports a server action needs that action module mocked, or the test pulls in `next/cache` and the API layer.
- The `server-only` guard is disabled in Jest on purpose. `next build` is what enforces it.
- `CustomerTable` formats dates with `timeZone: "UTC"`, so assert the UTC string.
- Don't lower the threshold in `jest.config.ts` to get a PR through. Add tests.

Related skills: `web-api-layer`, `ui-page`, `nextjs`, `test` (all suites), `ci`.
