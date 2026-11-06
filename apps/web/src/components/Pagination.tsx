import Link from "next/link";

interface Props {
  page: number;
  totalPages: number;
  search?: string;
}

export function pageHref(page: number, search?: string): string {
  const query = new URLSearchParams();
  if (search) {
    query.set("search", search);
  }
  query.set("page", String(page));
  return `/customers?${query.toString()}`;
}

export function Pagination({ page, totalPages, search }: Props) {
  if (totalPages <= 1) {
    return null;
  }
  const linkClass = "rounded-md border border-slate-300 bg-white px-3 py-1.5 text-sm hover:bg-slate-100";
  const disabledClass = "rounded-md border border-slate-200 px-3 py-1.5 text-sm text-slate-400";
  return (
    <nav aria-label="Pagination" className="mt-4 flex items-center justify-between">
      {page > 1 ? (
        <Link href={pageHref(page - 1, search)} rel="prev" className={linkClass}>
          Previous
        </Link>
      ) : (
        <span aria-disabled="true" className={disabledClass}>Previous</span>
      )}
      <span className="text-sm text-slate-600" aria-current="page">
        Page {page} of {totalPages}
      </span>
      {page < totalPages ? (
        <Link href={pageHref(page + 1, search)} rel="next" className={linkClass}>
          Next
        </Link>
      ) : (
        <span aria-disabled="true" className={disabledClass}>Next</span>
      )}
    </nav>
  );
}
