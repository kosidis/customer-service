export function SearchBox({ defaultValue }: { defaultValue?: string }) {
  return (
    <form action="/customers" method="get" role="search" className="flex gap-2">
      <label htmlFor="search" className="sr-only">
        Search customers
      </label>
      <input
        id="search"
        name="search"
        type="search"
        defaultValue={defaultValue}
        placeholder="Search by name, email or phone"
        className="w-72 rounded-md border border-slate-300 bg-white px-3 py-2 text-sm"
      />
      <button type="submit" className="rounded-md bg-slate-800 px-3 py-2 text-sm font-medium text-white hover:bg-slate-900">
        Search
      </button>
    </form>
  );
}
