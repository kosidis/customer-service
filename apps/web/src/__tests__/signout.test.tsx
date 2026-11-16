import { renderToStaticMarkup } from "react-dom/server";
import { redirect } from "next/navigation";
import { signOutAction } from "@/app/account/signout/actions";
import RootLayout from "@/app/layout";
import { getCurrentUser, logout } from "@/lib/api/auth";
import { ApiError } from "@/lib/api/http";
import { clearSessionCookie, getSessionToken } from "@/lib/auth/session";

jest.mock("next/navigation", () => ({
  redirect: jest.fn((path: string) => {
    throw new Error(`NEXT_REDIRECT:${path}`);
  }),
}));
jest.mock("@/lib/api/auth", () => ({ logout: jest.fn(), getCurrentUser: jest.fn() }));
jest.mock("@/lib/auth/session", () => ({
  clearSessionCookie: jest.fn(),
  getSessionToken: jest.fn(),
}));

beforeEach(() => jest.clearAllMocks());

describe("signOutAction", () => {
  it("signOutAction_ClearsCookieAndRedirects", async () => {
    (logout as jest.Mock).mockResolvedValue(undefined);
    await expect(signOutAction()).rejects.toThrow("NEXT_REDIRECT:/login");
    expect(logout).toHaveBeenCalled();
    expect(clearSessionCookie).toHaveBeenCalled();
    expect(redirect).toHaveBeenCalledWith("/login");
  });

  it("signOutAction_ApiFails_StillClearsCookie", async () => {
    (logout as jest.Mock).mockRejectedValue(new ApiError(500, "Boom"));
    await expect(signOutAction()).rejects.toThrow("NEXT_REDIRECT:/login");
    expect(clearSessionCookie).toHaveBeenCalled();
  });
});

describe("RootLayout", () => {
  async function html(): Promise<string> {
    return renderToStaticMarkup(await RootLayout({ children: <p>content</p> }));
  }

  it("Layout_SignedIn_ShowsUsernameAndSignOut", async () => {
    (getSessionToken as jest.Mock).mockResolvedValue("tok");
    (getCurrentUser as jest.Mock).mockResolvedValue({ username: "ada", role: "Admin", mustChangePassword: false });
    const out = await html();
    expect(out).toContain("ada");
    expect(out).toContain("Sign out");
  });

  it("Layout_NoCookie_ShowsNoUserAndSkipsApi", async () => {
    (getSessionToken as jest.Mock).mockResolvedValue(undefined);
    const out = await html();
    expect(out).not.toContain("Sign out");
    expect(out).toContain("content");
    expect(getCurrentUser).not.toHaveBeenCalled();
  });

  it("Layout_Expired401_SwallowedAndShowsNoUser", async () => {
    (getSessionToken as jest.Mock).mockResolvedValue("stale");
    (getCurrentUser as jest.Mock).mockRejectedValue(new ApiError(401, "Unauthorized"));
    expect(await html()).not.toContain("Sign out");
  });

  it("Layout_ServerError_Propagates", async () => {
    (getSessionToken as jest.Mock).mockResolvedValue("tok");
    (getCurrentUser as jest.Mock).mockRejectedValue(new ApiError(500, "Boom"));
    await expect(html()).rejects.toThrow();
  });
});
