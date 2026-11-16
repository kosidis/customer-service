import type { Metadata } from "next";
import Link from "next/link";
import { getCurrentUser } from "@/lib/api/auth";
import { ApiError } from "@/lib/api/http";
import type { CurrentUser } from "@/lib/api/types";
import { getSessionToken } from "@/lib/auth/session";
import { signOutAction } from "./account/signout/actions";
import "./globals.css";

export const metadata: Metadata = {
  title: "Customers",
  description: "Manage customers: list, search, add, edit and delete.",
};

// The layout also renders on /login, so it must not redirect: read the cookie, ask the
// API only when one exists, and treat a 401 as "signed out".
async function currentUserOrNull(): Promise<CurrentUser | null> {
  if (!(await getSessionToken())) return null;
  try {
    return await getCurrentUser();
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) return null;
    throw error;
  }
}

export default async function RootLayout({ children }: { children: React.ReactNode }) {
  const user = await currentUserOrNull();
  return (
    <html lang="en" className="h-full antialiased">
      <body className="min-h-full bg-slate-50 text-slate-900">
        <header className="border-b border-slate-200 bg-white">
          <div className="mx-auto flex max-w-5xl items-center px-4 py-3">
            <Link href="/customers" className="text-lg font-semibold">
              Customer Service
            </Link>
            {user && (
              <div className="ml-auto flex items-center gap-3 text-sm">
                <span>{user.username}</span>
                <form action={signOutAction}>
                  <button type="submit" className="rounded-md border border-slate-300 px-3 py-1 hover:bg-slate-100">
                    Sign out
                  </button>
                </form>
              </div>
            )}
          </div>
        </header>
        <main className="mx-auto max-w-5xl px-4 py-8">{children}</main>
      </body>
    </html>
  );
}
