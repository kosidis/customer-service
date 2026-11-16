import { renderToStaticMarkup } from "react-dom/server";
import { redirect } from "next/navigation";
import { signOutAction } from "@/app/account/signout/actions";
import RootLayout from "@/app/layout";
import { logout } from "@/lib/api/auth";
import { ApiError } from "@/lib/api/http";
import { clearSessionCookie, fetchCurrentUser } from "@/lib/auth/session";

jest.mock("next/navigation", () => ({
  redirect: jest.fn((path: string) => {
    throw new Error(`NEXT_REDIRECT:${path}`);
  }),
}));
jest.mock("@/lib/api/auth", () => ({ logout: jest.fn() }));
jest.mock("@/lib/auth/session", () => ({
  clearSessionCookie: jest.fn(),
  fetchCurrentUser: jest.fn(),
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
    (fetchCurrentUser as jest.Mock).mockResolvedValue({ username: "ada", role: "Admin", mustChangePassword: false });
    const out = await html();
    expect(out).toContain("ada");
    expect(out).toContain("Sign out");
  });

  it("Layout_Anonymous_ShowsNoUser", async () => {
    (fetchCurrentUser as jest.Mock).mockResolvedValue(null);
    const out = await html();
    expect(out).not.toContain("Sign out");
    expect(out).toContain("content");
  });

  it("Layout_ServerError_RendersWithoutUserAndDoesNotThrow", async () => {
    (fetchCurrentUser as jest.Mock).mockRejectedValue(new ApiError(500, "Boom"));
    const out = await html();
    expect(out).toContain("content");
    expect(out).not.toContain("Sign out");
  });
});
