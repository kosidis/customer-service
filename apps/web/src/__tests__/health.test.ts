/** @jest-environment node */
import { GET } from "@/app/api/health/route";

describe("health route", () => {
  it("returns Healthy without touching the backend", async () => {
    const fetchSpy = jest.fn();
    global.fetch = fetchSpy as unknown as typeof fetch;
    const res = GET();
    expect(res.status).toBe(200);
    expect(res.headers.get("content-type")).toContain("text/plain");
    expect(await res.text()).toBe("Healthy");
    expect(fetchSpy).not.toHaveBeenCalled();
  });
});
