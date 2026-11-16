import "server-only";
import { cookies } from "next/headers";

export const SESSION_COOKIE = "session";

export async function getSessionToken(): Promise<string | undefined> {
  const store = await cookies();
  return store.get(SESSION_COOKIE)?.value;
}
