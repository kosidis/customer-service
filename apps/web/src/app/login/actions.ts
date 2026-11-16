"use server";

import { redirect } from "next/navigation";
import { login } from "@/lib/api/auth";
import { ApiError, isKnownField } from "@/lib/api/http";
import { setSessionCookie } from "@/lib/auth/session";
import type { LoginFormState } from "@/lib/form-state";

const GENERIC_LOGIN_ERROR = "Something went wrong signing in. Try again.";
const INVALID_CREDENTIALS = "Invalid username or password.";

function text(formData: FormData, name: string): string {
  const value = formData.get(name);
  return typeof value === "string" ? value : "";
}

function toFormState(error: unknown, values: { username: string }): LoginFormState {
  if (error instanceof ApiError) {
    // One message for every 401, whatever the detail, so it can't reveal whether the
    // username exists or the account is locked.
    if (error.status === 401) {
      return { fieldErrors: {}, formError: INVALID_CREDENTIALS, values };
    }
    if (error.status === 400) {
      const fieldErrors: Record<string, string[]> = {};
      const other: string[] = [];
      for (const [key, messages] of Object.entries(error.fieldErrors)) {
        if (isKnownField(key)) {
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
            : "Enter your username and password.";
      return { fieldErrors, formError, values };
    }
  }
  return { fieldErrors: {}, formError: GENERIC_LOGIN_ERROR, values };
}

export async function loginAction(
  _prev: LoginFormState,
  formData: FormData,
): Promise<LoginFormState> {
  const username = text(formData, "username");
  const password = text(formData, "password");
  const values = { username };
  let mustChangePassword: boolean;
  try {
    const response = await login({ username, password });
    await setSessionCookie(response.token);
    mustChangePassword = response.mustChangePassword;
  } catch (error) {
    return toFormState(error, values);
  }
  redirect(mustChangePassword ? "/account/password" : "/customers");
}
