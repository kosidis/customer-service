"use client";

import Link from "next/link";
import { useActionState } from "react";
import type { CustomerFormState } from "@/lib/form-state";

type Action = (
  state: CustomerFormState,
  formData: FormData,
) => Promise<CustomerFormState>;

interface Props {
  action: Action;
  initialState: CustomerFormState;
  submitLabel: string;
}

const inputClass =
  "mt-1 block w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm shadow-sm focus:border-blue-600 focus:outline-none focus:ring-1 focus:ring-blue-600 aria-[invalid=true]:border-red-600";

function Field({
  name,
  label,
  type = "text",
  required,
  maxLength,
  value,
  errors,
  autoComplete,
}: {
  name: string;
  label: string;
  type?: string;
  required?: boolean;
  maxLength: number;
  value: string;
  errors?: string[];
  autoComplete?: string;
}) {
  const errorId = `${name}-error`;
  const hasError = !!errors?.length;
  return (
    <div>
      <label htmlFor={name} className="block text-sm font-medium">
        {label}
        {required ? <span aria-hidden="true"> *</span> : null}
      </label>
      <input
        id={name}
        name={name}
        type={type}
        required={required}
        maxLength={maxLength}
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

export function CustomerForm({ action, initialState, submitLabel }: Props) {
  const [state, formAction, pending] = useActionState(action, initialState);
  const { values, fieldErrors } = state;

  // Re-key on submission result so defaultValue reflects the values the server echoed back.
  const formKey = JSON.stringify([values, fieldErrors, state.formError]);

  return (
    <form key={formKey} action={formAction} className="max-w-xl space-y-4">
      {state.formError ? (
        <div role="alert" className="rounded-md border border-red-300 bg-red-50 px-3 py-2 text-sm text-red-800">
          {state.formError}
        </div>
      ) : null}
      <Field name="firstName" label="First name" required maxLength={100} value={values.firstName} errors={fieldErrors.firstName} autoComplete="given-name" />
      <Field name="lastName" label="Last name" required maxLength={100} value={values.lastName} errors={fieldErrors.lastName} autoComplete="family-name" />
      <Field name="email" label="Email" type="email" required maxLength={256} value={values.email} errors={fieldErrors.email} autoComplete="email" />
      <Field name="phoneNumber" label="Phone" type="tel" maxLength={32} value={values.phoneNumber} errors={fieldErrors.phoneNumber} autoComplete="tel" />
      <Field name="domain" label="Domain" maxLength={253} value={values.domain} errors={fieldErrors.domain} />
      <div className="flex items-center gap-3 pt-2">
        <button
          type="submit"
          disabled={pending}
          className="rounded-md bg-blue-700 px-4 py-2 text-sm font-medium text-white hover:bg-blue-800 disabled:cursor-not-allowed disabled:opacity-60"
        >
          {pending ? "Saving..." : submitLabel}
        </button>
        <Link href="/customers" className="text-sm text-slate-700 underline">
          Cancel
        </Link>
      </div>
    </form>
  );
}
