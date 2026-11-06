---
name: ui-page
description: WHEN adding or changing a page, form or list in the web app in apps/web, end to end from the API call through the server action and component to the tests.
---

# Add or edit a page or form (`apps/web`)

Order matters: data in, then action, then UI, then tests, then checks. Write each test before its code (superpowers `test-driven-development`). This skill links to the others rather than repeating them: `web-api-layer`, `nextjs`, `jest-rtl`.

## Add a page or form

1. **API call.** If the page needs data or a write the app doesn't call yet, add it in `src/lib/api` (see `web-api-layer`). If the API lacks the endpoint, change the API first (`endpoint` skill).
2. **Form state (forms only).** Reuse `CustomerFormState` from `src/lib/form-state.ts` (`fieldErrors`, `formError`, `values`). For a new form shape, add a sibling type and an initial constant there, with every field a string so `defaultValue` works.
3. **Server action** in `src/app/customers/actions.ts` (or an `actions.ts` in the new route folder):
   - Start the file with `"use server"`. The signature for `useActionState` is `(prev, formData) => Promise<State>`.
   - Read `FormData` into values, call the API layer inside `try`, and return `toFormState(error, values)` from `catch` so the user's input comes back.
   - After the `try/catch`, call `revalidatePath(...)` then `redirect(...)`. Never put them inside the `try`.
   - Don't write validation rules; show the API's `fieldErrors`. A 400 whose keys match no field, a 5xx and a network error become the form-level message.
4. **Component** in `src/components/`:
   - A server component by default. Add `"use client"` only for state, effects, `useActionState` or `useTransition`.
   - A form uses `useActionState(action, initialState)`, a `<label htmlFor>` per input, `aria-invalid` and `aria-describedby` for errors, `role="alert"` for the form error, and a disabled submit while pending.
   - Style with Tailwind classes, matching the existing slate and blue palette.
5. **Page** at `src/app/<route>/page.tsx`:
   - An async server component. `await params` and `await searchParams`.
   - `export const dynamic = "force-dynamic"` if it reads the API.
   - Missing data: `notFound()`. For an edit action bound to an id: `action.bind(null, id)`.
   - Link to it from the list or the layout.
6. **Tests** in `src/__tests__` (see `jest-rtl`): the API function in `api.test.ts`; the action in `actions.test.ts` (success redirects and revalidates, 400, 409, other failures); the component in `components.test.tsx`; the page in `pages.test.tsx`.
7. **Verify** from `apps/web`:
   ```bash
   npm run lint && npm run typecheck && npm run test:coverage && npm run build
   ```
   Then try it for real: start the API and `API_BASE_URL=http://localhost:8080 npm run dev` (see the `run` skill), and exercise success, a validation error, a duplicate email (409) and the API being down. Finish with `sh .claude/pre-pr-checks`.

## Edit an existing page or form

1. Find the layers it touches: the page in `src/app/`, the component in `src/components/`, the action in `src/app/customers/actions.ts`, the API call in `src/lib/api/customers.ts`.
2. Start with the test that pins the new behaviour; run it and watch it fail.
3. Change bottom-up (API layer, action, component, page). `npm run typecheck` lists callers after a type change.
4. Check what moves with it: `Pagination` and `SearchBox` build `/customers?search=...&page=...` URLs, and delete returns to the list keeping `search` and `page` (the redirect target must start with `/customers`). A change to list parameters touches all three, plus `customers/page.tsx`.
5. Update fixtures (`makeCustomer`) and text assertions, then run the verify step above.

## Gotchas

- The form's `required` and `maxLength` attributes are browser hints only. The API decides what's valid.
- Check what the API's `search` really matches (see the README) before you change the `SearchBox` placeholder.
- Don't call `fetch` or read `API_BASE_URL` in pages or components.
- Dates come from the API in UTC; `CustomerTable` formats them with `timeZone: "UTC"` and labels them `UTC`.
