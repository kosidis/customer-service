"use client";

import { useActionState } from "react";
import type { ChangePasswordFormState } from "@/lib/form-state";

type Action = (
  state: ChangePasswordFormState,
  formData: FormData,
) => Promise<ChangePasswordFormState>;

interface Props {
  action: Action;
  initialState: ChangePasswordFormState;
}

const inputClass =
  "mt-1 block w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm shadow-sm focus:border-blue-600 focus:outline-none focus:ring-1 focus:ring-blue-600 aria-[invalid=true]:border-red-600";

function Field({
  name,
  label,
  errors,
  autoComplete,
}: {
  name: string;
  label: string;
  errors?: string[];
  autoComplete: string;
}) {
  const errorId = `${name}-error`;
  const hasError = !!errors?.length;
  return (
    <div>
      <label htmlFor={name} className="block text-sm font-medium">
        {label}
      </label>
      <input
        id={name}
        name={name}
        type="password"
        required
        maxLength={256}
        autoComplete={autoComplete}
        aria-invalid={hasError}
        aria-describedby={hasError ? errorId : undefined}
        className={inputClass}
      />
      {hasError ? (
        <p id={errorId} className="mt-1 text-sm text-red-700">
          {errors.join(" ")}
        </p>
      ) : null}
    </div>
  );
}

export function ChangePasswordForm({ action, initialState }: Props) {
  const [state, formAction, pending] = useActionState(action, initialState);
  const { fieldErrors } = state;

  // Re-key on each result so the password fields are reset, never refilled.
  const formKey = JSON.stringify([fieldErrors, state.formError]);

  return (
    <form key={formKey} action={formAction} className="max-w-sm space-y-4">
      {state.formError ? (
        <div role="alert" className="rounded-md border border-red-300 bg-red-50 px-3 py-2 text-sm text-red-800">
          {state.formError}
        </div>
      ) : null}
      <Field name="currentPassword" label="Current password" errors={fieldErrors.currentPassword} autoComplete="current-password" />
      <Field name="newPassword" label="New password" errors={fieldErrors.newPassword} autoComplete="new-password" />
      <button
        type="submit"
        disabled={pending}
        className="rounded-md bg-blue-700 px-4 py-2 text-sm font-medium text-white hover:bg-blue-800 disabled:cursor-not-allowed disabled:opacity-60"
      >
        {pending ? "Changing..." : "Change password"}
      </button>
    </form>
  );
}
