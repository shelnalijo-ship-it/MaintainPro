import { NextRequest } from "next/server";
import { beforeEach, describe, expect, it, vi } from "vitest";

const cookieValues = new Map<string, string>();
vi.mock("next/headers", () => ({ cookies: vi.fn(async () => ({ get: (name: string) => cookieValues.has(name) ? { value: cookieValues.get(name) } : undefined })) }));

describe("backend session proxy", () => {
  beforeEach(() => { cookieValues.clear(); vi.restoreAllMocks(); });
  it("refreshes once after an expired access token and retries the request", async () => {
    cookieValues.set("maintainpro_access", "expired-access"); cookieValues.set("maintainpro_refresh", "valid-refresh");
    const fetchMock = vi.spyOn(globalThis, "fetch");
    fetchMock.mockResolvedValueOnce(new Response(JSON.stringify({ status: 401 }), { status: 401, headers: { "content-type": "application/problem+json" } }));
    fetchMock.mockResolvedValueOnce(new Response(JSON.stringify({ accessToken: "new-access", refreshToken: "new-refresh", accessTokenExpiresAt: new Date(Date.now() + 600_000).toISOString(), user: { id: "u1" } }), { status: 200, headers: { "content-type": "application/json" } }));
    fetchMock.mockResolvedValueOnce(new Response(JSON.stringify({ id: "u1" }), { status: 200, headers: { "content-type": "application/json" } }));
    const { GET } = await import("./route");
    const response = await GET(new NextRequest("http://localhost/api/backend/v1/auth/me"), { params: Promise.resolve({ path: ["v1", "auth", "me"] }) });
    expect(response.status).toBe(200);
    expect(fetchMock).toHaveBeenCalledTimes(3);
    expect((fetchMock.mock.calls[2][1]?.headers as Headers).get("authorization")).toBe("Bearer new-access");
    expect(response.headers.get("set-cookie")).toContain("maintainpro_access");
  });
  it("forwards logout and clears both session cookies", async () => {
    cookieValues.set("maintainpro_access", "access"); cookieValues.set("maintainpro_refresh", "refresh");
    vi.spyOn(globalThis, "fetch").mockResolvedValue(new Response(null, { status: 204 }));
    const { POST } = await import("./route");
    const response = await POST(new NextRequest("http://localhost/api/backend/v1/auth/logout", { method: "POST" }), { params: Promise.resolve({ path: ["v1", "auth", "logout"] }) });
    expect(response.status).toBe(204);
    expect(response.headers.get("set-cookie")).toContain("maintainpro_access=");
    expect(response.headers.get("set-cookie")).toContain("maintainpro_refresh=");
  });
});
