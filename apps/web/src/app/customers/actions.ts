"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import {
  createCustomer,
  deleteCustomer,
  updateCustomer,
} from "@/lib/api/customers";
import { ApiError, isKnownField } from "@/lib/api/http";
import type { CreateCustomerRequest } from "@/lib/api/types";
import type { CustomerFormState, CustomerFormValues } from "@/lib/form-state";

function text(formData: FormData, name: string): string {
  const value = formData.get(name);
  return typeof value === "string" ? value : "";
}

function readValues(formData: FormData): CustomerFormValues {
  return {
    firstName: text(formData, "firstName"),
    lastName: text(formData, "lastName"),
    email: text(formData, "email"),
    phoneNumber: text(formData, "phoneNumber"),
    domain: text(formData, "domain"),
  };
}

function toRequest(values: CustomerFormValues): CreateCustomerRequest {
  const phone = values.phoneNumber.trim();
  const domain = values.domain.trim();
  return {
    firstName: values.firstName,
    lastName: values.lastName,
    email: values.email,
    phoneNumber: phone === "" ? null : phone,
    domain: domain === "" ? null : domain,
  };
}

const GENERIC_SAVE_ERROR = "Something went wrong saving the customer. Try again.";

function toFormState(
  error: unknown,
  values: CustomerFormValues,
  { notFoundMessage }: { notFoundMessage?: string } = {},
): CustomerFormState {
  if (error instanceof ApiError) {
    if (error.status === 400) {
      const fieldErrors: Record<string, string[]> = {};
      const other: string[] = [];
      for (const [key, messages] of Object.entries(error.fieldErrors)) {
        if (isKnownField(key)) {
          fieldErrors[key] = messages;
        } else {
          other.push(...messages);
        }
      }
      const formError =
        other.length > 0
          ? other.join(" ")
          : Object.keys(fieldErrors).length > 0
            ? null
            : (error.detail ?? error.title ?? "The customer details are not valid.");
      return { fieldErrors, formError, values };
    }
    if (error.status === 409) {
      return {
        fieldErrors: { email: ["A customer with this email already exists."] },
        formError: null,
        values,
      };
    }
    if (error.status === 404 && notFoundMessage) {
      return { fieldErrors: {}, formError: notFoundMessage, values };
    }
    return { fieldErrors: {}, formError: GENERIC_SAVE_ERROR, values };
  }
  return {
    fieldErrors: {},
    formError: GENERIC_SAVE_ERROR,
    values,
  };
}

export async function createCustomerAction(
  _prev: CustomerFormState,
  formData: FormData,
): Promise<CustomerFormState> {
  const values = readValues(formData);
  try {
    await createCustomer(toRequest(values));
  } catch (error) {
    return toFormState(error, values);
  }
  revalidatePath("/customers");
  redirect("/customers");
}

export async function updateCustomerAction(
  id: string,
  _prev: CustomerFormState,
  formData: FormData,
): Promise<CustomerFormState> {
  const values = readValues(formData);
  try {
    await updateCustomer(id, toRequest(values));
  } catch (error) {
    return toFormState(error, values, {
      notFoundMessage: "This customer no longer exists.",
    });
  }
  revalidatePath("/customers");
  redirect("/customers");
}

const RETURN_BASE = "http://app.invalid";

// Only paths under /customers in this app are allowed, so the return path can't be an
// open redirect. Parsing resolves "..", "%2e%2e" and "//host" before the check; a raw
// prefix test would let "/customers/../x" through.
function safeReturnPath(returnTo: string | undefined): string {
  if (!returnTo?.startsWith("/") || returnTo.includes("\\")) {
    return "/customers";
  }
  let url: URL;
  try {
    url = new URL(returnTo, RETURN_BASE);
  } catch {
    return "/customers";
  }
  const underCustomers = url.pathname === "/customers" || url.pathname.startsWith("/customers/");
  return url.origin === RETURN_BASE && underCustomers ? url.pathname + url.search : "/customers";
}

export async function deleteCustomerAction(
  id: string,
  returnTo?: string,
): Promise<{ error: string } | undefined> {
  try {
    await deleteCustomer(id);
  } catch (error) {
    // Already gone: treat as success so the list refreshes.
    if (!(error instanceof ApiError && error.status === 404)) {
      return { error: "Could not delete the customer. Please try again." };
    }
  }
  revalidatePath("/customers");
  redirect(safeReturnPath(returnTo));
}
