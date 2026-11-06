import type { Customer } from "@/lib/api/types";

export function makeCustomer(overrides: Partial<Customer> = {}): Customer {
  return {
    id: "11111111-1111-1111-1111-111111111111",
    firstName: "Ada",
    lastName: "Lovelace",
    email: "ada@example.com",
    phoneNumber: "+44 20 7946 0000",
    createdAtUtc: "2026-01-02T03:04:05Z",
    updatedAtUtc: "2026-02-03T04:05:06Z",
    ...overrides,
  };
}
