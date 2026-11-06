"use client";

import { useState, useTransition } from "react";
import { deleteCustomerAction } from "@/app/customers/actions";

export function DeleteCustomerButton({
  id,
  name,
  returnTo,
}: {
  id: string;
  name: string;
  returnTo?: string;
}) {
  const [pending, startTransition] = useTransition();
  const [error, setError] = useState<string | null>(null);

  function onClick() {
    if (!window.confirm(`Delete ${name}? This cannot be undone.`)) {
      return;
    }
    setError(null);
    startTransition(async () => {
      const result = await deleteCustomerAction(id, returnTo);
      if (result?.error) {
        setError(result.error);
      }
    });
  }

  return (
    <span className="inline-flex flex-col items-end">
      <button
        type="button"
        onClick={onClick}
        disabled={pending}
        aria-label={`Delete ${name}`}
        className="text-red-700 underline disabled:opacity-60"
      >
        {pending ? "Deleting..." : "Delete"}
      </button>
      {error ? (
        <span role="alert" className="text-xs text-red-700">
          {error}
        </span>
      ) : null}
    </span>
  );
}
