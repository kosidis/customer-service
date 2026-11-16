import { render, screen } from "@testing-library/react";
import { redirect } from "next/navigation";
import { loginAction } from "@/app/login/actions";
import LoginPage from "@/app/login/page";
import { LoginForm } from "@/components/LoginForm";
import { getCurrentUser, login } from "@/lib/api/auth";
import { ApiError } from "@/lib/api/http";
import { getSessionToken, setSessionCookie } from "@/lib/auth/session";
import { initialLoginFormState } from "@/lib/form-state";

jest.mock("next/navigation", () => ({
  redirect: jest.fn((path: string) => {
    throw new Error(`NEXT_REDIRECT:${path}`);
  }),
}));
jest.mock("next/headers", () => ({ cookies: jest.fn() }));
jest.mock("@/lib/api/auth", () => ({ login: jest.fn(), getCurrentUser: jest.fn() }));
jest.mock("@/lib/auth/session", () => ({
  setSessionCookie: jest.fn(),
  getSessionToken: jest.fn(),
}));

const loginMock = login as jest.Mock;
const meMock = getCurrentUser as jest.Mock;
const tokenMock = getSessionToken as jest.Mock;
const GENERIC = "Something went wrong signing in. Try again.";

function form(fields: Record<string, string>): FormData {
  const data = new FormData();
  for (const [k, v] of Object.entries(fields)) data.set(k, v);
  return data;
}
const creds = { username: "alice", password: "s3cret-pw" };

beforeEach(() => jest.clearAllMocks());

describe("loginAction", () => {
  it("loginAction_Api401_ReturnsGenericError", async () => {
    loginMock.mockRejectedValue(new ApiError(401, "Unauthorized"));
    const state = await loginAction(initialLoginFormState, form(creds));
    expect(state.formError).toBe("Invalid username or password.");
    expect(state.fieldErrors).toEqual({});
    expect(setSessionCookie).not.toHaveBeenCalled();
  });

  it("loginAction_Api401WithDetail_SameMessage", async () => {
    loginMock.mockRejectedValue(new ApiError(401, "Unauthorized", "User is locked out"));
    const state = await loginAction(initialLoginFormState, form(creds));
    expect(state.formError).toBe("Invalid username or password.");
    expect(JSON.stringify(state)).not.toContain("locked");
  });

  it("loginAction_Api400_MapsFieldErrors", async () => {
    loginMock.mockRejectedValue(
      new ApiError(400, "Validation", undefined, { username: ["Required"], weird: ["Odd"] }),
    );
    const state = await loginAction(initialLoginFormState, form(creds));
    expect(state.fieldErrors).toEqual({ username: ["Required"] });
    expect(state.formError).toBe("Odd");
  });

  it("loginAction_Api400OnlyKnown_NoFormError", async () => {
    loginMock.mockRejectedValue(new ApiError(400, "Validation", undefined, { password: ["Required"] }));
    const state = await loginAction(initialLoginFormState, form(creds));
    expect(state.fieldErrors).toEqual({ password: ["Required"] });
    expect(state.formError).toBeNull();
  });

  it("loginAction_Api400NoErrors_UsesGenericInvalid", async () => {
    loginMock.mockRejectedValue(new ApiError(400, "Bad"));
    const state = await loginAction(initialLoginFormState, form(creds));
    expect(state.formError).toBe("Enter your username and password.");
  });

  it("loginAction_Api500_GenericNoDetail", async () => {
    loginMock.mockRejectedValue(new ApiError(500, "Boom", "stack trace here"));
    const state = await loginAction(initialLoginFormState, form(creds));
    expect(state.formError).toBe(GENERIC);
    expect(JSON.stringify(state)).not.toContain("stack trace");
  });

  it("loginAction_NetworkError_GenericNoDetail", async () => {
    loginMock.mockRejectedValue(new TypeError("fetch failed ECONNREFUSED"));
    const state = await loginAction(initialLoginFormState, form(creds));
    expect(state.formError).toBe(GENERIC);
    expect(JSON.stringify(state)).not.toContain("ECONNREFUSED");
  });

  it("loginAction_Failure_EchoesUsernameNotPassword", async () => {
    loginMock.mockRejectedValue(new ApiError(401, "Unauthorized"));
    const state = await loginAction(initialLoginFormState, form(creds));
    expect(state.values).toEqual({ username: "alice" });
    expect(JSON.stringify(state)).not.toContain("s3cret-pw");
  });

  it("loginAction_Success_SetsCookieAndRedirectsToCustomers", async () => {
    loginMock.mockResolvedValue({ token: "tok", username: "alice", role: "Admin", mustChangePassword: false });
    await expect(loginAction(initialLoginFormState, form(creds))).rejects.toThrow("NEXT_REDIRECT:/customers");
    expect(loginMock).toHaveBeenCalledWith(creds);
    expect(setSessionCookie).toHaveBeenCalledWith("tok");
    expect(redirect).toHaveBeenCalledWith("/customers");
  });

  it("loginAction_MustChangePassword_RedirectsToAccountPassword", async () => {
    loginMock.mockResolvedValue({ token: "tok", username: "alice", role: "Admin", mustChangePassword: true });
    await expect(loginAction(initialLoginFormState, form(creds))).rejects.toThrow(
      "NEXT_REDIRECT:/account/password",
    );
    expect(setSessionCookie).toHaveBeenCalledWith("tok");
  });
});

describe("LoginForm", () => {
  it("LoginForm_ShowsFormError", () => {
    render(
      <LoginForm
        action={jest.fn()}
        initialState={{ fieldErrors: {}, formError: "Invalid username or password.", values: { username: "alice" } }}
      />,
    );
    expect(screen.getByRole("alert")).toHaveTextContent("Invalid username or password.");
    expect(screen.getByLabelText(/username/i)).toHaveValue("alice");
  });

  it("LoginForm_ShowsFieldErrors", () => {
    render(
      <LoginForm
        action={jest.fn()}
        initialState={{ fieldErrors: { username: ["Required"] }, formError: null, values: { username: "" } }}
      />,
    );
    expect(screen.getByText("Required")).toBeInTheDocument();
    expect(screen.getByLabelText(/username/i)).toHaveAttribute("aria-invalid", "true");
  });

  it("LoginForm_DoesNotPrefillPassword", () => {
    render(<LoginForm action={jest.fn()} initialState={initialLoginFormState} />);
    const password = screen.getByLabelText(/password/i);
    expect(password).toHaveAttribute("type", "password");
    expect(password).toHaveValue("");
  });
});

describe("LoginPage", () => {
  it("LoginPage_SignedIn_RedirectsToCustomers", async () => {
    tokenMock.mockResolvedValue("tok");
    meMock.mockResolvedValue({ username: "alice", role: "Admin", mustChangePassword: false });
    await expect(LoginPage()).rejects.toThrow("NEXT_REDIRECT:/customers");
  });

  it("LoginPage_NoCookie_RendersFormWithoutApiCall", async () => {
    tokenMock.mockResolvedValue(undefined);
    render(await LoginPage());
    expect(meMock).not.toHaveBeenCalled();
    expect(screen.getByLabelText(/username/i)).toBeInTheDocument();
  });

  it("LoginPage_Api401_RendersForm", async () => {
    tokenMock.mockResolvedValue("stale");
    meMock.mockRejectedValue(new ApiError(401, "Unauthorized"));
    render(await LoginPage());
    expect(screen.getByLabelText(/username/i)).toBeInTheDocument();
    expect(redirect).not.toHaveBeenCalled();
  });

  it("LoginPage_OtherError_Rethrows", async () => {
    tokenMock.mockResolvedValue("tok");
    const err = new ApiError(500, "Boom");
    meMock.mockRejectedValue(err);
    await expect(LoginPage()).rejects.toBe(err);
  });
});
