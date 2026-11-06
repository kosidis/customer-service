"use client";

export default function CustomersError({ reset }: { error: Error; reset: () => void }) {
  return (
    <div role="alert" className="rounded-md border border-red-300 bg-red-50 p-6">
      <h1 className="text-lg font-semibold text-red-900">Couldn&apos;t reach the customer service</h1>
      <p className="mt-1 text-sm text-red-800">Something went wrong while loading customers. Please try again.</p>
      <button type="button" onClick={() => reset()} className="mt-4 rounded-md bg-red-700 px-4 py-2 text-sm font-medium text-white hover:bg-red-800">
        Try again
      </button>
    </div>
  );
}
