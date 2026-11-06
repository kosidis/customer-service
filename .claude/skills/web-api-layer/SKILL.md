---
name: web-api-layer
description: WHEN adding or changing a call from the web app to the customer API in apps/web/src/lib/api, including types, error mapping and the tests that mock fetch.
---

# Web API layer (`apps/web/src/lib/api`)

This is the only code in the web app that calls the API. It runs on the server only: `http.ts` and `customers.ts` start with `import "server-only"`. `types.ts` holds plain types.

| File | Contents |
|---|---|
| `types.ts` | `Customer`, `CreateCustomerRequest`, `UpdateCustomerRequest`, `PagedResponse<T>`, `ProblemDetails`, `ValidationProblemDetails`. They mirror the API's JSON (camelCase) |
| `http.ts` | `apiFetch<T>(path, { method, body })` and `ApiError` |
| `customers.ts` | One function per endpoint: `listCustomers`, `getCustomer`, `createCustomer`, `updateCustomer`, `deleteCustomer` |

## How `apiFetch` behaves

- Reads `API_BASE_URL` on every call, strips trailing slashes, and throws `API_BASE_URL is not set...` if it's empty.
- Sends `Accept: application/json, application/problem+json`, `Content-Type: application/json` only when there's a body, and `cache: "no-store"`.
- On a non-2xx response it parses the problem-details body (a body that isn't JSON is tolerated) and throws `ApiError(status, title, detail, fieldErrors)`. `title` falls back to `statusText`, then `"Request failed"`.
- `fieldErrors` comes from the problem's `errors` object. `normaliseKey` takes the last segment of JSON-path keys (`$.phoneNumber`, `$.customer.EMAIL`) and matches it case-insensitively against `KNOWN_FIELDS` (`firstName`, `lastName`, `email`, `phoneNumber`); keys it can't match are only lower-camel-cased, and the actions turn those into the form-level error. **When you add a form field, add it to `KNOWN_FIELDS`**, or the API's error for it lands in the form-level message instead of under the input. Entries that aren't arrays are dropped.
- A `204` returns `undefined`.
- A network failure is a plain `fetch` rejection, not an `ApiError`. Callers tell the two apart with `instanceof ApiError`.

`getCustomer` turns a 404 into `null`. The other functions let `ApiError` propagate. The server actions in `src/app/customers/actions.ts` map it to form state: a 400 with field keys becomes field errors, a 409 becomes an email error, and anything else becomes a form-level message (generic for 5xx and network errors). Keep that mapping in the action, not here.

## Add an API call

1. Check the API's contract first (`Contracts/`, the controller, the README table). If the API doesn't have the endpoint, use the `endpoint` skill first.
2. Add or extend the request and response types in `types.ts`. Match the API's JSON names exactly; nullable columns are `string | null`.
3. Add one function to `customers.ts`. Build paths with `encodeURIComponent` for ids and `URLSearchParams` for queries (skip empty values, as `listCustomers` does for `search`). Return `apiFetch<T>(...)` with a concrete `T`.
4. Decide what a failure means for the caller. If a status is an expected outcome (like a 404 meaning "absent"), catch `ApiError` for that status only and rethrow everything else.
5. Add tests (below), then use the call from an action or page; see the `ui-page` skill.

## Change an API call

1. Change `types.ts` first and let `npm run typecheck` list every caller, including `src/__tests__/fixtures.ts` (`makeCustomer`).
2. Update the function, then the tests that pin the old URL, method or body.
3. If the API's error shape changed, change `toFieldErrors` or `ApiError` in `http.ts` and add a case to `describe("apiFetch")`.

## Tests (`src/__tests__/api.test.ts`)

- `global.fetch` is replaced by a `jest.fn()` in `beforeEach`, and `process.env.API_BASE_URL` is set (with a trailing slash, to cover the strip). The `respond(status, body)` helper queues one response; omit the body to make `json()` throw.
- Assert the full URL, method, headers, `cache: "no-store"` and the JSON body from `fetchMock.mock.calls[0]`.
- Cover the success path and each error branch you add: 400 with `errors`, 404, 409, 204, a non-JSON error body, a network rejection.
- `server-only` needs no handling: Jest maps it to an empty module (see the `jest-rtl` skill).
- Other test files mock `@/lib/api/customers` instead of `fetch`. Only `api.test.ts` mocks `fetch`.

## Gotchas

- Never call `fetch` or read `API_BASE_URL` outside `http.ts`.
- Never import this layer into a client component.
- Never re-implement the API's validation rules here or in the UI. The API's 400 `errors` are the source of truth.
- Don't add caching (`next: { revalidate }`, `force-cache`): customer data must be live.
