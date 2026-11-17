import type { Metadata } from "next";
import Link from "next/link";
import type { CurrentUser } from "@/lib/api/types";
import { fetchCurrentUser } from "@/lib/auth/session";
import { signOutAction } from "./account/signout/actions";
import "./globals.css";

export const metadata: Metadata = {
  title: "Customers",
  description: "Manage customers: list, search, add, edit and delete.",
};

// The layout also renders on /login, so it must never redirect or throw: an API outage
// must not make /login and Sign out unreachable. Any failure just hides the user chip.
// fetchCurrentUser is request-cached, so protected pages don't call the API twice.
async function currentUserOrNull(): Promise<CurrentUser | null> {
  try {
    return await fetchCurrentUser();
  } catch {
    return null;
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
                <Link href="/account/password" className="text-slate-600 underline hover:text-slate-900">
                  Change password
                </Link>
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
