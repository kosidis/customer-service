import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import {
  createCustomerAction,
  deleteCustomerAction,
  updateCustomerAction,
} from "@/app/customers/actions";
import { createCustomer, deleteCustomer, updateCustomer } from "@/lib/api/customers";
import { ApiError } from "@/lib/api/http";
import { initialFormState } from "@/lib/form-state";
import { makeCustomer } from "./fixtures";

jest.mock("next/cache", () => ({ revalidatePath: jest.fn() }));
jest.mock("next/navigation", () => ({
  redirect: jest.fn(() => {
    throw new Error("NEXT_REDIRECT");
  }),
}));
jest.mock("@/lib/api/customers", () => ({
  createCustomer: jest.fn(),
  updateCustomer: jest.fn(),
  deleteCustomer: jest.fn(),
}));

function form(fields: Record<string, string>): FormData {
  const data = new FormData();
  for (const [k, v] of Object.entries(fields)) data.set(k, v);
  return data;
}

const filled = { firstName: "Ada", lastName: "Lovelace", email: "ADA@x.io", phoneNumber: "  " };

beforeEach(() => jest.clearAllMocks());

describe("createCustomerAction", () => {
  it("creates, revalidates and redirects; blank phone becomes null", async () => {
    (createCustomer as jest.Mock).mockResolvedValue(makeCustomer());
    await expect(createCustomerAction(initialFormState, form(filled))).rejects.toThrow("NEXT_REDIRECT");
    expect(createCustomer).toHaveBeenCalledWith({
      firstName: "Ada",
      lastName: "Lovelace",
      email: "ADA@x.io",
      phoneNumber: null,
    });
    expect(revalidatePath).toHaveBeenCalledWith("/customers");
    expect(redirect).toHaveBeenCalledWith("/customers");
  });

  it("trims a provided phone", async () => {
    (createCustomer as jest.Mock).mockResolvedValue(makeCustomer());
    await expect(
      createCustomerAction(initialFormState, form({ ...filled, phoneNumber: " 123 " })),
    ).rejects.toThrow();
    expect((createCustomer as jest.Mock).mock.calls[0][0].phoneNumber).toBe("123");
  });

  it("returns API field errors and keeps values on 400", async () => {
    (createCustomer as jest.Mock).mockRejectedValue(
      new ApiError(400, "Validation", undefined, { email: ["Invalid email"] }),
    );
    const state = await createCustomerAction(initialFormState, form(filled));
    expect(state.fieldErrors).toEqual({ email: ["Invalid email"] });
    expect(state.formError).toBeNull();
    expect(state.values).toEqual(filled);
    expect(redirect).not.toHaveBeenCalled();
    expect(revalidatePath).not.toHaveBeenCalled();
  });

  it("maps 409 to an email error", async () => {
    (createCustomer as jest.Mock).mockRejectedValue(new ApiError(409, "Conflict", "dup"));
    const state = await createCustomerAction(initialFormState, form(filled));
    expect(state.fieldErrors).toEqual({ email: ["A customer with this email already exists."] });
    expect(state.values.email).toBe("ADA@x.io");
  });

  it("shows a generic message for 5xx and hides the API detail", async () => {
    (createCustomer as jest.Mock).mockRejectedValue(new ApiError(500, "Server error", "Boom"));
    const state = await createCustomerAction(initialFormState, form(filled));
    expect(state.formError).toBe("Something went wrong saving the customer. Try again.");
    expect(state.fieldErrors).toEqual({});
  });

  it("surfaces 400 keys that match no known field as the form error", async () => {
    (createCustomer as jest.Mock).mockRejectedValue(
      new ApiError(400, "Validation", undefined, {
        email: ["Invalid email"],
        request: ["Body is required."],
        "": ["Bad model."],
      }),
    );
    const state = await createCustomerAction(initialFormState, form(filled));
    expect(state.fieldErrors).toEqual({ email: ["Invalid email"] });
    expect(state.formError).toBe("Body is required. Bad model.");
  });

  it("uses title for a 400 without field errors and no detail", async () => {
    (createCustomer as jest.Mock).mockRejectedValue(new ApiError(400, "Bad request"));
    const state = await createCustomerAction(initialFormState, form(filled));
    expect(state.formError).toBe("Bad request");
  });

  it("returns a friendly error for network failures", async () => {
    (createCustomer as jest.Mock).mockRejectedValue(new TypeError("fetch failed"));
    const state = await createCustomerAction(initialFormState, form(filled));
    expect(state.formError).toBe("Something went wrong saving the customer. Try again.");
    expect(state.values).toEqual(filled);
  });

  it("treats missing form fields as empty", async () => {
    (createCustomer as jest.Mock).mockRejectedValue(new TypeError("x"));
    const state = await createCustomerAction(initialFormState, new FormData());
    expect(state.values).toEqual({ firstName: "", lastName: "", email: "", phoneNumber: "" });
  });
});

describe("updateCustomerAction", () => {
  it("updates by bound id, revalidates and redirects", async () => {
    (updateCustomer as jest.Mock).mockResolvedValue(makeCustomer());
    await expect(updateCustomerAction("abc", initialFormState, form(filled))).rejects.toThrow("NEXT_REDIRECT");
    expect((updateCustomer as jest.Mock).mock.calls[0][0]).toBe("abc");
    expect(revalidatePath).toHaveBeenCalledWith("/customers");
    expect(redirect).toHaveBeenCalledWith("/customers");
  });

  it("returns errors on failure", async () => {
    (updateCustomer as jest.Mock).mockRejectedValue(new ApiError(409, "Conflict"));
    const state = await updateCustomerAction("abc", initialFormState, form(filled));
    expect(state.fieldErrors.email).toBeDefined();
  });

  it("says the customer no longer exists on 404", async () => {
    (updateCustomer as jest.Mock).mockRejectedValue(new ApiError(404, "Not Found", "internal detail"));
    const state = await updateCustomerAction("abc", initialFormState, form(filled));
    expect(state.formError).toBe("This customer no longer exists.");
  });
});

describe("deleteCustomerAction", () => {
  it("deletes, revalidates and redirects", async () => {
    (deleteCustomer as jest.Mock).mockResolvedValue(undefined);
    await expect(deleteCustomerAction("abc")).rejects.toThrow("NEXT_REDIRECT");
    expect(deleteCustomer).toHaveBeenCalledWith("abc");
    expect(revalidatePath).toHaveBeenCalledWith("/customers");
  });

  it("redirects back to the given customers list view", async () => {
    (deleteCustomer as jest.Mock).mockResolvedValue(undefined);
    await expect(deleteCustomerAction("abc", "/customers?search=ada&page=2")).rejects.toThrow("NEXT_REDIRECT");
    expect(redirect).toHaveBeenCalledWith("/customers?search=ada&page=2");
  });

  it.each([
    "https://evil.example",
    "//evil.example",
    "/other",
    "/customersx",
    "javascript:alert(1)",
    "/customers/../../other",
    "/customers/%2e%2e/other",
    "/customers/..",
    "/\\evil.example",
    "/customers\\..\\other",
  ])(
    "ignores unsafe return path %s",
    async (returnTo) => {
      (deleteCustomer as jest.Mock).mockResolvedValue(undefined);
      await expect(deleteCustomerAction("abc", returnTo)).rejects.toThrow("NEXT_REDIRECT");
      expect(redirect).toHaveBeenCalledWith("/customers");
    },
  );

  it("treats 404 as success", async () => {
    (deleteCustomer as jest.Mock).mockRejectedValue(new ApiError(404, "Not Found"));
    await expect(deleteCustomerAction("abc")).rejects.toThrow("NEXT_REDIRECT");
  });

  it("returns an error for other failures", async () => {
    (deleteCustomer as jest.Mock).mockRejectedValue(new ApiError(500, "Boom"));
    await expect(deleteCustomerAction("abc")).resolves.toEqual({
      error: expect.stringMatching(/Could not delete/),
    });
    expect(redirect).not.toHaveBeenCalled();
  });
});
