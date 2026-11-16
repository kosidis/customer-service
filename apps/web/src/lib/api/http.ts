import "server-only";
import type { ValidationProblemDetails } from "./types";

export class ApiError extends Error {
  readonly status: number;
  readonly title: string;
  readonly detail?: string;
  readonly fieldErrors: Record<string, string[]>;

  constructor(
    status: number,
    title: string,
    detail?: string,
    fieldErrors: Record<string, string[]> = {},
  ) {
    super(detail ?? title);
    this.name = "ApiError";
    this.status = status;
    this.title = title;
    this.detail = detail;
    this.fieldErrors = fieldErrors;
  }
}

const KNOWN_FIELDS = ["firstName", "lastName", "email", "phoneNumber", "domain"] as const;

function lowerCamel(key: string): string {
  return key.charAt(0).toLowerCase() + key.slice(1);
}

// Maps "FirstName", "firstname" and JSON-path style keys such as "$.phoneNumber"
// or "$.customer.email" onto the known form fields. Unknown keys are kept
// (lower-camel) so callers can surface them as a form-level error.
function normaliseKey(key: string): string {
  const segments = key.split(/[.[\]]/).filter((s) => s !== "" && s !== "$");
  const last = segments[segments.length - 1] ?? key;
  const known = KNOWN_FIELDS.find((f) => f.toLowerCase() === last.toLowerCase());
  return known ?? lowerCamel(key);
}

export function isKnownField(key: string): boolean {
  return (KNOWN_FIELDS as readonly string[]).includes(key);
}

function toFieldErrors(errors: unknown): Record<string, string[]> {
  const result: Record<string, string[]> = {};
  if (errors && typeof errors === "object") {
    for (const [key, value] of Object.entries(errors)) {
      if (Array.isArray(value)) {
        const name = normaliseKey(key);
        result[name] = [...(result[name] ?? []), ...value.map(String)];
      }
    }
  }
  return result;
}

function baseUrl(): string {
  const url = process.env.API_BASE_URL;
  if (!url) {
    throw new Error("API_BASE_URL is not set. Point it at the customer API, e.g. http://localhost:8080.");
  }
  return url.replace(/\/+$/, "");
}

export async function apiFetch<T>(
  path: string,
  init: { method?: string; body?: unknown } = {},
): Promise<T> {
  const url = `${baseUrl()}${path}`;
  const hasBody = init.body !== undefined;
  const response = await fetch(url, {
    method: init.method ?? "GET",
    headers: {
      Accept: "application/json, application/problem+json",
      ...(hasBody ? { "Content-Type": "application/json" } : {}),
    },
    body: hasBody ? JSON.stringify(init.body) : undefined,
    cache: "no-store",
  });

  if (!response.ok) {
    let problem: ValidationProblemDetails = {};
    try {
      problem = (await response.json()) as ValidationProblemDetails;
    } catch {
      // Non-JSON error body; fall back to status text below.
    }
    throw new ApiError(
      response.status,
      problem.title ?? (response.statusText || "Request failed"),
      problem.detail,
      toFieldErrors(problem.errors),
    );
  }

  if (response.status === 204) {
    return undefined as T;
  }
  return (await response.json()) as T;
}
