import Link from "next/link";
import { redirect } from "next/navigation";
import { CustomerTable } from "@/components/CustomerTable";
import { Pagination, pageHref } from "@/components/Pagination";
import { SearchBox } from "@/components/SearchBox";
import { listCustomers } from "@/lib/api/customers";

export const dynamic = "force-dynamic";

type SearchParams = Promise<{ [key: string]: string | string[] | undefined }>;

function first(value: string | string[] | undefined): string | undefined {
  return Array.isArray(value) ? value[0] : value;
}

export default async function CustomersPage({ searchParams }: { searchParams: SearchParams }) {
  const params = await searchParams;
  const search = first(params.search)?.trim() || undefined;
  const parsed = Number.parseInt(first(params.page) ?? "1", 10);
  const page = Number.isFinite(parsed) && parsed >= 1 ? parsed : 1;

  const result = await listCustomers({ page, search });

  // e.g. the last row of the last page was just deleted.
  if (result.totalPages > 0 && page > result.totalPages) {
    redirect(pageHref(result.totalPages, search));
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <h1 className="text-2xl font-semibold">Customers</h1>
        <Link href="/customers/new" className="rounded-md bg-blue-700 px-4 py-2 text-sm font-medium text-white hover:bg-blue-800">
          Add customer
        </Link>
      </div>
      <SearchBox defaultValue={search} />
      <CustomerTable customers={result.items} returnTo={pageHref(result.page, search)} />
      <Pagination page={result.page} totalPages={result.totalPages} search={search} />
    </div>
  );
}
