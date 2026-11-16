"use server";

import { redirect } from "next/navigation";
import { changePassword } from "@/lib/api/auth";
import { ApiError } from "@/lib/api/http";
import type { ChangePasswordFormState } from "@/lib/form-state";

const PASSWORD_FIELDS = ["currentPassword", "newPassword"];

const GENERIC_ERROR = "Something went wrong changing the password. Try again.";

function text(formData: FormData, name: string): string {
  const value = formData.get(name);
  return typeof value === "string" ? value : "";
}

function badRequestState(error: ApiError): ChangePasswordFormState {
  const fieldErrors: Record<string, string[]> = {};
  const other: string[] = [];
  for (const [key, messages] of Object.entries(error.fieldErrors)) {
    if (PASSWORD_FIELDS.includes(key)) {
      fieldErrors[key] = messages;
    } else {
      other.push(...messages);
    }
  }
  const formError =
    other.length > 0
      ? other.join(" ")
      : Object.keys(fieldErrors).length > 0
        ? null
        : "Enter your current and new password.";
  return { fieldErrors, formError };
}

export async function changePasswordAction(
  _prev: ChangePasswordFormState,
  formData: FormData,
): Promise<ChangePasswordFormState> {
  const currentPassword = text(formData, "currentPassword");
  const newPassword = text(formData, "newPassword");
  let failure: ChangePasswordFormState | null = null;
  let unauthorized = false;
  try {
    await changePassword({ currentPassword, newPassword });
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) {
      unauthorized = true;
    } else if (error instanceof ApiError && error.status === 400) {
      failure = badRequestState(error);
    } else {
      failure = { fieldErrors: {}, formError: GENERIC_ERROR };
    }
  }
  // redirect() throws, so it runs after the try/catch.
  if (unauthorized) redirect("/login");
  if (failure) return failure;
  redirect("/customers");
}
