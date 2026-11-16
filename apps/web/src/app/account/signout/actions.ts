"use server";

import { redirect } from "next/navigation";
import { logout } from "@/lib/api/auth";
import { clearSessionCookie } from "@/lib/auth/session";

export async function signOutAction(): Promise<void> {
  try {
    await logout();
  } catch {
    // The local cookie is cleared regardless; an expired or unreachable session is fine.
  }
  await clearSessionCookie();
  // redirect() throws, so it runs outside the try/catch.
  redirect("/login");
}
