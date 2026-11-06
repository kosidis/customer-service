import "server-only";
import { ApiError, apiFetch } from "./http";
import type {
  CreateCustomerRequest,
  Customer,
  PagedResponse,
  UpdateCustomerRequest,
} from "./types";

export interface ListCustomersParams {
  page?: number;
  pageSize?: number;
  search?: string;
}

export function listCustomers({
  page = 1,
  pageSize = 20,
  search,
}: ListCustomersParams = {}): Promise<PagedResponse<Customer>> {
  const query = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
  });
  if (search) {
    query.set("search", search);
  }
  return apiFetch<PagedResponse<Customer>>(`/api/customers?${query.toString()}`);
}

export async function getCustomer(id: string): Promise<Customer | null> {
  try {
    return await apiFetch<Customer>(`/api/customers/${encodeURIComponent(id)}`);
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) {
      return null;
    }
    throw error;
  }
}

export function createCustomer(request: CreateCustomerRequest): Promise<Customer> {
  return apiFetch<Customer>("/api/customers", { method: "POST", body: request });
}

export function updateCustomer(
  id: string,
  request: UpdateCustomerRequest,
): Promise<Customer> {
  return apiFetch<Customer>(`/api/customers/${encodeURIComponent(id)}`, {
    method: "PUT",
    body: request,
  });
}

export async function deleteCustomer(id: string): Promise<void> {
  await apiFetch<void>(`/api/customers/${encodeURIComponent(id)}`, {
    method: "DELETE",
  });
}
