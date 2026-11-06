export default function Loading() {
  return (
    <div className="space-y-4 animate-pulse" aria-busy="true" aria-label="Loading customers">
      <div className="h-8 w-48 rounded bg-slate-200" />
      <div className="h-10 w-72 rounded bg-slate-200" />
      {Array.from({ length: 6 }, (_, i) => (
        <div key={i} className="h-10 rounded bg-slate-200" />
      ))}
    </div>
  );
}
