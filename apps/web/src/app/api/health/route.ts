export const dynamic = "force-dynamic";

export function GET() {
  return new Response("Healthy", {
    status: 200,
    headers: { "Content-Type": "text/plain; charset=utf-8" },
  });
}
