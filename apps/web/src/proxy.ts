import { NextResponse, type NextRequest } from "next/server";

// Same value as SESSION_COOKIE in lib/auth/session.ts; not imported because that
// module is server-only and pulls in next/headers, which the proxy must not load.
const SESSION_COOKIE = "session";

// Optimistic check only: the presence of the cookie, never its validity.
// The API decides whether the session is real.
export default function proxy(request: NextRequest) {
  if (!request.cookies.has(SESSION_COOKIE)) {
    return NextResponse.redirect(new URL("/login", request.url));
  }
  return NextResponse.next();
}

export const config = {
  matcher: ["/((?!login|api/health|_next/static|_next/image|favicon.ico).*)"],
};
