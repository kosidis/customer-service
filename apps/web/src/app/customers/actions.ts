"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import {
  createCustomer,
  deleteCustomer,
  updateCustomer,
} from "@/lib/api/customers";
import { ApiError } from "@/lib/api/http";
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
  };
}

function toRequest(values: CustomerFormValues): CreateCustomerRequest {
  const phone = values.phoneNumber.trim();
  return {
    firstName: values.firstName,
    lastName: values.lastName,
    email: values.email,
    phoneNumber: phone === "" ? null : phone,
  };
}

function toFormState(error: unknown, values: CustomerFormValues): CustomerFormState {
  if (error instanceof ApiError) {
    if (error.status === 400 && Object.keys(error.fieldErrors).length > 0) {
      return { fieldErrors: error.fieldErrors, formError: null, values };
    }
    if (error.status === 409) {
      return {
        fieldErrors: { email: ["A customer with this email already exists."] },
        formError: null,
        values,
      };
    }
    return {
      fieldErrors: {},
      formError: error.detail ?? error.title ?? "Something went wrong.",
      values,
    };
  }
  return {
    fieldErrors: {},
    formError: "Could not reach the customer service. Please try again.",
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
    return toFormState(error, values);
  }
  revalidatePath("/customers");
  redirect("/customers");
}

export async function deleteCustomerAction(
  id: string,
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
  redirect("/customers");
}
