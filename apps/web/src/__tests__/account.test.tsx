import { render, screen } from "@testing-library/react";
import { redirect } from "next/navigation";
import { changePasswordAction } from "@/app/account/actions";
import ChangePasswordPage from "@/app/account/password/page";
import { ChangePasswordForm } from "@/components/ChangePasswordForm";
import { changePassword } from "@/lib/api/auth";
import { ApiError } from "@/lib/api/http";
import { requireUser } from "@/lib/auth/session";
import { initialChangePasswordFormState } from "@/lib/form-state";

jest.mock("next/navigation", () => ({
  redirect: jest.fn((path: string) => {
    throw new Error(`NEXT_REDIRECT:${path}`);
  }),
}));
jest.mock("next/headers", () => ({ cookies: jest.fn() }));
jest.mock("@/lib/api/auth", () => ({ changePassword: jest.fn() }));
jest.mock("@/lib/auth/session", () => ({
  requireUser: jest.fn(),
  clearSessionCookie: jest.fn(),
}));

const changeMock = changePassword as jest.Mock;
const requireUserMock = requireUser as jest.Mock;
const GENERIC = "Something went wrong changing the password. Try again.";

function form(fields: Record<string, string>): FormData {
  const data = new FormData();
  for (const [k, v] of Object.entries(fields)) data.set(k, v);
  return data;
}
const input = { currentPassword: "old-s3cret", newPassword: "new-s3cret" };

beforeEach(() => jest.clearAllMocks());

describe("changePasswordAction", () => {
  it("changePasswordAction_400WithFieldErrors_ReturnsThem", async () => {
    changeMock.mockRejectedValue(
      new ApiError(400, "Validation", undefined, { newPassword: ["Too short"], currentPassword: ["Wrong"] }),
    );
    const state = await changePasswordAction(initialChangePasswordFormState, form(input));
    expect(state.fieldErrors).toEqual({ newPassword: ["Too short"], currentPassword: ["Wrong"] });
    expect(state.formError).toBeNull();
    expect(changeMock).toHaveBeenCalledWith(input);
  });

  it("changePasswordAction_400UnknownKey_GoesToFormError", async () => {
    changeMock.mockRejectedValue(new ApiError(400, "Validation", undefined, { weird: ["Odd"] }));
    const state = await changePasswordAction(initialChangePasswordFormState, form(input));
    expect(state.fieldErrors).toEqual({});
    expect(state.formError).toBe("Odd");
  });

  it("changePasswordAction_400CustomerFieldKey_GoesToFormError", async () => {
    changeMock.mockRejectedValue(new ApiError(400, "Validation", undefined, { email: ["Bad"] }));
    const state = await changePasswordAction(initialChangePasswordFormState, form(input));
    expect(state.fieldErrors).toEqual({});
    expect(state.formError).toBe("Bad");
  });

  it("changePasswordAction_401_RedirectsToLogin", async () => {
    changeMock.mockRejectedValue(new ApiError(401, "Unauthorized"));
    await expect(changePasswordAction(initialChangePasswordFormState, form(input))).rejects.toThrow(
      "NEXT_REDIRECT:/login",
    );
    expect(redirect).toHaveBeenCalledWith("/login");
  });

  it("changePasswordAction_5xxOrNetwork_ReturnsGenericMessage", async () => {
    changeMock.mockRejectedValue(new ApiError(500, "Boom", "stack trace here"));
    let state = await changePasswordAction(initialChangePasswordFormState, form(input));
    expect(state.formError).toBe(GENERIC);
    expect(JSON.stringify(state)).not.toContain("stack trace");
    changeMock.mockRejectedValue(new TypeError("fetch failed ECONNREFUSED"));
    state = await changePasswordAction(initialChangePasswordFormState, form(input));
    expect(state.formError).toBe(GENERIC);
    expect(JSON.stringify(state)).not.toContain("ECONNREFUSED");
  });

  it("changePasswordAction_Success_RedirectsToCustomers", async () => {
    changeMock.mockResolvedValue(undefined);
    await expect(changePasswordAction(initialChangePasswordFormState, form(input))).rejects.toThrow(
      "NEXT_REDIRECT:/customers",
    );
  });

  it("changePasswordAction_Failure_NeverReturnsPasswords", async () => {
    for (const err of [
      new ApiError(400, "Validation", undefined, { newPassword: ["Too short"] }),
      new ApiError(500, "Boom"),
    ]) {
      changeMock.mockRejectedValue(err);
      const state = await changePasswordAction(initialChangePasswordFormState, form(input));
      const json = JSON.stringify(state);
      expect(json).not.toContain("old-s3cret");
      expect(json).not.toContain("new-s3cret");
    }
  });
});

describe("ChangePasswordForm", () => {
  it("ChangePasswordForm_ShowsFieldErrors", () => {
    render(
      <ChangePasswordForm
        action={jest.fn()}
        initialState={{ fieldErrors: { newPassword: ["Too short"] }, formError: "Failed" }}
      />,
    );
    expect(screen.getByText("Too short")).toBeInTheDocument();
    expect(screen.getByLabelText(/new password/i)).toHaveAttribute("aria-invalid", "true");
    expect(screen.getByLabelText(/current password/i)).toHaveAttribute("aria-invalid", "false");
    expect(screen.getByRole("alert")).toHaveTextContent("Failed");
  });

  it("ChangePasswordForm_DoesNotPrefillPasswords", () => {
    render(<ChangePasswordForm action={jest.fn()} initialState={initialChangePasswordFormState} />);
    for (const label of [/current password/i, /new password/i]) {
      const field = screen.getByLabelText(label);
      expect(field).toHaveAttribute("type", "password");
      expect(field).toHaveValue("");
    }
  });
});

describe("ChangePasswordPage", () => {
  it("ChangePasswordPage_Render_CallsRequireUserAllowingPendingChange", async () => {
    requireUserMock.mockResolvedValue({ username: "alice", role: "Admin", mustChangePassword: true });
    render(await ChangePasswordPage());
    expect(requireUserMock).toHaveBeenCalledWith({ allowPendingChange: true });
    expect(screen.getByLabelText(/current password/i)).toBeInTheDocument();
    expect(screen.getByText(/must set a new password/i)).toBeInTheDocument();
  });

  it("ChangePasswordPage_NoPendingChange_HidesNotice", async () => {
    requireUserMock.mockResolvedValue({ username: "alice", role: "Admin", mustChangePassword: false });
    render(await ChangePasswordPage());
    expect(screen.queryByText(/must set a new password/i)).not.toBeInTheDocument();
  });
});
