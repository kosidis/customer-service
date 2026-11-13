import Link from "next/link";
import type { Customer } from "@/lib/api/types";
import { DeleteCustomerButton } from "./DeleteCustomerButton";

const dateFormat = new Intl.DateTimeFormat("en-GB", {
  dateStyle: "medium",
  timeStyle: "short",
  timeZone: "UTC",
});

export function formatUpdated(iso: string): string {
  return `${dateFormat.format(new Date(iso))} UTC`;
}

export function CustomerTable({
  customers,
  returnTo,
}: {
  customers: Customer[];
  returnTo?: string;
}) {
  if (customers.length === 0) {
    return (
      <p className="rounded-md border border-dashed border-slate-300 bg-white p-8 text-center text-slate-600">
        No customers found.
      </p>
    );
  }
  return (
    <div className="overflow-x-auto rounded-md border border-slate-200 bg-white">
      <table className="min-w-full divide-y divide-slate-200 text-sm">
        <caption className="sr-only">Customers</caption>
        <thead className="bg-slate-100 text-left">
          <tr>
            <th scope="col" className="px-4 py-2 font-medium">Name</th>
            <th scope="col" className="px-4 py-2 font-medium">Email</th>
            <th scope="col" className="px-4 py-2 font-medium">Phone</th>
            <th scope="col" className="px-4 py-2 font-medium">Domain</th>
            <th scope="col" className="px-4 py-2 font-medium">Updated</th>
            <th scope="col" className="sticky right-0 border-l border-slate-200 bg-slate-100 px-4 py-2 font-medium"><span className="sr-only">Actions</span></th>
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100">
          {customers.map((c) => (
            <tr key={c.id}>
              <td className="px-4 py-2">{c.firstName} {c.lastName}</td>
              <td className="px-4 py-2">
                <div className="line-clamp-3 max-w-[14rem] break-words" title={c.email}>{c.email}</div>
              </td>
              <td className="px-4 py-2">{c.phoneNumber ?? "-"}</td>
              <td className="px-4 py-2">
                <div className="line-clamp-3 max-w-[14rem] break-words" title={c.domain ?? undefined}>{c.domain ?? "-"}</div>
              </td>
              <td className="px-4 py-2 whitespace-nowrap">{formatUpdated(c.updatedAtUtc)}</td>
              <td className="sticky right-0 whitespace-nowrap border-l border-slate-100 bg-white px-4 py-2">
                <div className="flex justify-end gap-3">
                  <Link
                    href={`/customers/${encodeURIComponent(c.id)}/edit`}
                    className="text-blue-700 underline"
                    aria-label={`Edit ${c.firstName} ${c.lastName}`}
                  >
                    Edit
                  </Link>
                  <DeleteCustomerButton
                    id={c.id}
                    name={`${c.firstName} ${c.lastName}`}
                    returnTo={returnTo}
                  />
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
