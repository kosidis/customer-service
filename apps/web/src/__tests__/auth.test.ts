/** @jest-environment node */
import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { NextRequest } from "next/server";
import proxy, { config } from "@/proxy";
import { getCurrentUser } from "@/lib/api/auth";
import { ApiError } from "@/lib/api/http";
import {
  SESSION_COOKIE,
  clearSessionCookie,
  requireUser,
  setSessionCookie,
} from "@/lib/auth/session";

jest.mock("next/headers", () => ({ cookies: jest.fn() }));
jest.mock("next/navigation", () => ({
  redirect: jest.fn((path: string) => {
    throw new Error(`NEXT_REDIRECT:${path}`);
  }),
}));
jest.mock("@/lib/api/auth", () => ({ getCurrentUser: jest.fn() }));

const cookiesMock = cookies as jest.Mock;
const getCurrentUserMock = getCurrentUser as jest.Mock;
const redirectMock = redirect as unknown as jest.Mock;

const store = { get: jest.fn(), set: jest.fn(), delete: jest.fn() };

beforeEach(() => {
  jest.clearAllMocks();
  delete process.env.SESSION_COOKIE_SECURE;
  cookiesMock.mockResolvedValue(store);
  store.get.mockReturnValue({ name: SESSION_COOKIE, value: "tok" });
});

const user = { username: "alice", role: "Admin", mustChangePassword: false };

describe("requireUser", () => {
  it("requireUser_NoCookie_RedirectsToLogin", async () => {
    store.get.mockReturnValue(undefined);
    await expect(requireUser()).rejects.toThrow("NEXT_REDIRECT:/login");
    expect(getCurrentUserMock).not.toHaveBeenCalled();
  });

  it("requireUser_Api401_RedirectsToLogin", async () => {
    getCurrentUserMock.mockRejectedValue(new ApiError(401, "Unauthorized"));
    await expect(requireUser()).rejects.toThrow("NEXT_REDIRECT:/login");
    expect(redirectMock).toHaveBeenCalledWith("/login");
  });

  it("requireUser_OtherError_Rethrows", async () => {
    const err = new ApiError(500, "Boom");
    getCurrentUserMock.mockRejectedValue(err);
    await expect(requireUser()).rejects.toBe(err);
    expect(redirectMock).not.toHaveBeenCalled();
  });

  it("requireUser_PendingChange_RedirectsToAccountPassword", async () => {
    getCurrentUserMock.mockResolvedValue({ ...user, mustChangePassword: true });
    await expect(requireUser()).rejects.toThrow("NEXT_REDIRECT:/account/password");
  });

  it("requireUser_PendingChangeAllowed_ReturnsUser", async () => {
    const pending = { ...user, mustChangePassword: true };
    getCurrentUserMock.mockResolvedValue(pending);
    await expect(requireUser({ allowPendingChange: true })).resolves.toEqual(pending);
  });

  it("requireUser_ValidUser_ReturnsUser", async () => {
    getCurrentUserMock.mockResolvedValue(user);
    await expect(requireUser()).resolves.toEqual(user);
  });

  it("requireUser_ViewerNeedsAdmin_RedirectsToCustomers", async () => {
    getCurrentUserMock.mockResolvedValue({ ...user, role: "Viewer" });
    await expect(requireUser({ role: "Admin" })).rejects.toThrow("NEXT_REDIRECT:/customers");
  });

  it("requireUser_AdminNeedsAdmin_ReturnsUser", async () => {
    getCurrentUserMock.mockResolvedValue(user);
    await expect(requireUser({ role: "Admin" })).resolves.toEqual(user);
  });

  it("requireUser_NoRoleOption_AllowsViewer", async () => {
    const viewer = { ...user, role: "Viewer" };
    getCurrentUserMock.mockResolvedValue(viewer);
    await expect(requireUser()).resolves.toEqual(viewer);
  });
});

describe("session cookie", () => {
  it("setSessionCookie_SecureEnvTrue_SetsSecure", async () => {
    process.env.SESSION_COOKIE_SECURE = "true";
    await setSessionCookie("abc");
    expect(store.set).toHaveBeenCalledWith(SESSION_COOKIE, "abc", {
      httpOnly: true,
      sameSite: "lax",
      secure: true,
      maxAge: 31536000,
      path: "/",
    });
  });

  it("setSessionCookie_SecureEnvUnset_NotSecure", async () => {
    await setSessionCookie("abc");
    expect(store.set).toHaveBeenCalledWith(
      SESSION_COOKIE,
      "abc",
      expect.objectContaining({ secure: false, httpOnly: true, sameSite: "lax" }),
    );
  });

  it("clearSessionCookie_Called_DeletesCookie", async () => {
    await clearSessionCookie();
    expect(store.delete).toHaveBeenCalledWith(SESSION_COOKIE);
  });
});

describe("proxy", () => {
  it("proxy_NoCookie_RedirectsToLogin", () => {
    const res = proxy(new NextRequest("http://localhost:3000/customers"));
    expect(res.status).toBe(307);
    expect(res.headers.get("location")).toBe("http://localhost:3000/login");
  });

  it("proxy_CookiePresent_Continues", () => {
    const res = proxy(
      new NextRequest("http://localhost:3000/customers", {
        headers: { cookie: `${SESSION_COOKIE}=tok` },
      }),
    );
    expect(res.headers.get("location")).toBeNull();
    expect(res.headers.get("x-middleware-next")).toBe("1");
  });

  it("proxy_Matcher_ExcludesPublicPaths", () => {
    const pattern = new RegExp(`^${config.matcher[0]}$`);
    for (const p of ["/login", "/api/health", "/_next/static/a.js", "/_next/image", "/favicon.ico"]) {
      expect(pattern.test(p)).toBe(false);
    }
    expect(pattern.test("/customers")).toBe(true);
  });
});
