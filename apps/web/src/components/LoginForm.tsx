"use client";

import { useActionState } from "react";
import type { LoginFormState } from "@/lib/form-state";

type Action = (state: LoginFormState, formData: FormData) => Promise<LoginFormState>;

interface Props {
  action: Action;
  initialState: LoginFormState;
}

const inputClass =
  "mt-1 block w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm shadow-sm focus:border-blue-600 focus:outline-none focus:ring-1 focus:ring-blue-600 aria-[invalid=true]:border-red-600";

function Field({
  name,
  label,
  type = "text",
  value,
  errors,
  autoComplete,
}: {
  name: string;
  label: string;
  type?: string;
  value?: string;
  errors?: string[];
  autoComplete?: string;
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
        type={type}
        required
        maxLength={256}
        defaultValue={value}
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

export function LoginForm({ action, initialState }: Props) {
  const [state, formAction, pending] = useActionState(action, initialState);
  const { values, fieldErrors } = state;

  // Re-key on each result so the username is restored and the password field is reset.
  const formKey = JSON.stringify([values, fieldErrors, state.formError]);

  return (
    <form key={formKey} action={formAction} className="max-w-sm space-y-4">
      {state.formError ? (
        <div role="alert" className="rounded-md border border-red-300 bg-red-50 px-3 py-2 text-sm text-red-800">
          {state.formError}
        </div>
      ) : null}
      <Field name="username" label="Username" value={values.username} errors={fieldErrors.username} autoComplete="username" />
      <Field name="password" label="Password" type="password" errors={fieldErrors.password} autoComplete="current-password" />
      <button
        type="submit"
        disabled={pending}
        className="rounded-md bg-blue-700 px-4 py-2 text-sm font-medium text-white hover:bg-blue-800 disabled:cursor-not-allowed disabled:opacity-60"
      >
        {pending ? "Signing in..." : "Sign in"}
      </button>
    </form>
  );
}
