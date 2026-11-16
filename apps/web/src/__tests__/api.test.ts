import { ApiError, apiFetch } from "@/lib/api/http";
import {
  createCustomer,
  deleteCustomer,
  getCustomer,
  listCustomers,
  updateCustomer,
} from "@/lib/api/customers";
import { login, logout, getCurrentUser, changePassword } from "@/lib/api/auth";
import { getSessionToken } from "@/lib/auth/session";
import { makeCustomer } from "./fixtures";

jest.mock("@/lib/auth/session", () => ({ getSessionToken: jest.fn() }));
const getSessionTokenMock = getSessionToken as jest.Mock;

const fetchMock = jest.fn();

function respond(status: number, body?: unknown, statusText = "") {
  fetchMock.mockResolvedValueOnce({
    ok: status >= 200 && status < 300,
    status,
    statusText,
    json: async () => {
      if (body === undefined) throw new SyntaxError("no body");
      return body;
    },
  });
}

async function caught(promise: Promise<unknown>): Promise<ApiError> {
  try {
    await promise;
  } catch (e) {
    return e as ApiError;
  }
  throw new Error("expected rejection");
}

beforeEach(() => {
  fetchMock.mockReset();
  getSessionTokenMock.mockReset();
  getSessionTokenMock.mockResolvedValue(undefined);
  global.fetch = fetchMock as unknown as typeof fetch;
  process.env.API_BASE_URL = "http://api.test:8080/";
});

describe("apiFetch", () => {
  it("throws a clear error when API_BASE_URL is unset", async () => {
    delete process.env.API_BASE_URL;
    await expect(apiFetch("/x")).rejects.toThrow(/API_BASE_URL is not set/);
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("sends no-store GET without body headers", async () => {
    respond(200, { ok: true });
    await expect(apiFetch("/api/x")).resolves.toEqual({ ok: true });
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe("http://api.test:8080/api/x");
    expect(init.cache).toBe("no-store");
    expect(init.method).toBe("GET");
    expect(init.body).toBeUndefined();
    expect(init.headers["Content-Type"]).toBeUndefined();
  });

  it("returns undefined on 204", async () => {
    respond(204);
    await expect(apiFetch("/x", { method: "DELETE" })).resolves.toBeUndefined();
  });

  it("maps 400 to ApiError with lower-camel field errors", async () => {
    respond(400, {
      title: "One or more validation errors occurred.",
      status: 400,
      errors: { FirstName: ["required"], Email: ["bad", "worse"], Junk: "nope" },
    });
    const error = await caught(apiFetch("/x"));
    expect(error).toBeInstanceOf(ApiError);
    expect(error.status).toBe(400);
    expect(error.title).toBe("One or more validation errors occurred.");
    expect(error.fieldErrors).toEqual({ firstName: ["required"], email: ["bad", "worse"] });
  });

  it("maps field-error keys case-insensitively and from JSON paths", async () => {
    respond(400, {
      errors: {
        "$.phoneNumber": ["too long"],
        "$.Domain": ["bad domain"],
        PHONENUMBER: ["bad"],
        "$.customer.EMAIL": ["bad email"],
        lastname: ["req"],
        "$.Unknown": ["x"],
      },
    });
    const error = await caught(apiFetch("/x"));
    expect(error.fieldErrors).toEqual({
      phoneNumber: ["too long", "bad"],
      domain: ["bad domain"],
      email: ["bad email"],
      lastName: ["req"],
      "$.Unknown": ["x"],
    });
  });

  it.each([
    [404, "Not Found", "Customer missing"],
    [409, "Conflict", "A customer with email 'x' already exists."],
  ])("maps %i to ApiError with detail", async (status, title, detail) => {
    respond(status, { title, status, detail });
    const error = await caught(apiFetch("/x"));
    expect(error).toMatchObject({ status, title, detail, fieldErrors: {} });
    expect(error.message).toBe(detail);
  });

  it("maps a 500 with a non-JSON body using status text", async () => {
    respond(500, undefined, "Internal Server Error");
    const error = await caught(apiFetch("/x"));
    expect(error).toMatchObject({ status: 500, title: "Internal Server Error" });
  });

  it("falls back to a generic title with no status text", async () => {
    respond(502, undefined);
    const error = await caught(apiFetch("/x"));
    expect(error.title).toBe("Request failed");
  });

  it("serialises a JSON body", async () => {
    respond(201, { id: "1" });
    await apiFetch("/x", { method: "POST", body: { a: 1 } });
    const init = fetchMock.mock.calls[0][1];
    expect(init.body).toBe('{"a":1}');
    expect(init.headers["Content-Type"]).toBe("application/json");
  });
});

describe("customers api", () => {
  it("lists with defaults", async () => {
    respond(200, { items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 });
    await listCustomers();
    expect(fetchMock.mock.calls[0][0]).toBe(
      "http://api.test:8080/api/customers?page=1&pageSize=20",
    );
  });

  it("lists with paging and encoded search", async () => {
    respond(200, { items: [], page: 2, pageSize: 5, totalCount: 0, totalPages: 0 });
    await listCustomers({ page: 2, pageSize: 5, search: "a&b c/ü" });
    expect(fetchMock.mock.calls[0][0]).toBe(
      "http://api.test:8080/api/customers?page=2&pageSize=5&search=a%26b+c%2F%C3%BC",
    );
  });

  it("gets a customer with an encoded id", async () => {
    const customer = makeCustomer();
    respond(200, customer);
    await expect(getCustomer("a/b")).resolves.toEqual(customer);
    expect(fetchMock.mock.calls[0][0]).toBe("http://api.test:8080/api/customers/a%2Fb");
  });

  it("returns null when the customer is not found", async () => {
    respond(404, { title: "Not Found", status: 404 });
    await expect(getCustomer("x")).resolves.toBeNull();
  });

  it("rethrows other errors from getCustomer", async () => {
    respond(500, { title: "boom", status: 500 });
    await expect(getCustomer("x")).rejects.toMatchObject({ status: 500 });
  });

  it("creates with POST", async () => {
    respond(201, makeCustomer());
    await createCustomer({ firstName: "A", lastName: "B", email: "a@b.co", phoneNumber: null, domain: "example.com" });
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe("http://api.test:8080/api/customers");
    expect(init.method).toBe("POST");
    expect(JSON.parse(init.body)).toEqual({ firstName: "A", lastName: "B", email: "a@b.co", phoneNumber: null, domain: "example.com" });
  });

  it("updates with PUT", async () => {
    respond(200, makeCustomer());
    await updateCustomer("id 1", { firstName: "A", lastName: "B", email: "a@b.co" });
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe("http://api.test:8080/api/customers/id%201");
    expect(init.method).toBe("PUT");
  });

  it("deletes with DELETE and handles 204", async () => {
    respond(204);
    await expect(deleteCustomer("id")).resolves.toBeUndefined();
    expect(fetchMock.mock.calls[0][1].method).toBe("DELETE");
  });
});

describe("auth", () => {
  it("apiFetch_SessionCookiePresent_SendsBearerHeader", async () => {
    getSessionTokenMock.mockResolvedValue("tok123");
    respond(200, {});
    await apiFetch("/api/x");
    expect(fetchMock.mock.calls[0][1].headers.Authorization).toBe("Bearer tok123");
  });

  it("apiFetch_NoCookie_SendsNoAuthorizationHeader", async () => {
    respond(200, {});
    await apiFetch("/api/x");
    expect(fetchMock.mock.calls[0][1].headers.Authorization).toBeUndefined();
  });

  it("login_PostsCredentials", async () => {
    getSessionTokenMock.mockResolvedValue("stale");
    const body = { token: "t", username: "bob", role: "Admin", mustChangePassword: false };
    respond(200, body);
    await expect(login({ username: "bob", password: "pw" })).resolves.toEqual(body);
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe("http://api.test:8080/api/auth/login");
    expect(init.method).toBe("POST");
    expect(JSON.parse(init.body)).toEqual({ username: "bob", password: "pw" });
    expect(init.headers.Authorization).toBeUndefined();
    expect(getSessionTokenMock).not.toHaveBeenCalled();
  });

  it("logout_PostsLogout", async () => {
    respond(204);
    await expect(logout()).resolves.toBeUndefined();
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe("http://api.test:8080/api/auth/logout");
    expect(init.method).toBe("POST");
  });

  it("getCurrentUser_Gets_Me", async () => {
    const me = { username: "bob", role: "Viewer", mustChangePassword: true };
    respond(200, me);
    await expect(getCurrentUser()).resolves.toEqual(me);
    expect(fetchMock.mock.calls[0][0]).toBe("http://api.test:8080/api/auth/me");
  });

  it("changePassword_PostsBody", async () => {
    respond(204);
    await changePassword({ currentPassword: "a", newPassword: "b" });
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe("http://api.test:8080/api/auth/change-password");
    expect(init.method).toBe("POST");
    expect(JSON.parse(init.body)).toEqual({ currentPassword: "a", newPassword: "b" });
  });

  it("normaliseKey_CurrentPassword_MapsToKnownField", async () => {
    respond(400, {
      title: "Bad",
      errors: { CurrentPassword: ["x"], newpassword: ["y"], Username: ["u"], "$.password": ["p"] },
    });
    const err = await caught(apiFetch("/x"));
    expect(err.fieldErrors).toEqual({
      currentPassword: ["x"],
      newPassword: ["y"],
      username: ["u"],
      password: ["p"],
    });
  });
});
