import { cookies } from "next/headers";
import { NextRequest, NextResponse } from "next/server";

const ACCESS_COOKIE = "maintainpro_access";
const REFRESH_COOKIE = "maintainpro_refresh";
const API_BASE_URL = (process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5043").replace(/\/$/, "");

interface BackendAuthResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  user: unknown;
}

function cookieOptions(maxAge?: number) {
  return {
    httpOnly: true,
    secure: process.env.NODE_ENV === "production",
    sameSite: "strict" as const,
    path: "/",
    ...(maxAge === undefined ? {} : { maxAge }),
  };
}

function copyHeaders(request: NextRequest, accessToken?: string) {
  const headers = new Headers();
  const contentType = request.headers.get("content-type");
  const accept = request.headers.get("accept");
  if (contentType) headers.set("content-type", contentType);
  if (accept) headers.set("accept", accept);
  if (accessToken) headers.set("authorization", `Bearer ${accessToken}`);
  headers.set("x-forwarded-host", request.headers.get("host") ?? "maintainpro-web");
  return headers;
}

async function callBackend(request: NextRequest, path: string[], body: ArrayBuffer | undefined, accessToken?: string) {
  return fetch(`${API_BASE_URL}/api/${path.join("/")}${request.nextUrl.search}`, {
    method: request.method,
    headers: copyHeaders(request, accessToken),
    body,
    cache: "no-store",
    signal: request.signal,
  });
}

async function rotateSession(refreshToken: string) {
  const response = await fetch(`${API_BASE_URL}/api/v1/auth/refresh`, {
    method: "POST",
    headers: { "content-type": "application/json", accept: "application/json" },
    body: JSON.stringify({ refreshToken }),
    cache: "no-store",
  });
  if (!response.ok) return null;
  return (await response.json()) as BackendAuthResponse;
}

function persistSession(response: NextResponse, auth: BackendAuthResponse) {
  const expires = new Date(auth.accessTokenExpiresAt);
  response.cookies.set(ACCESS_COOKIE, auth.accessToken, cookieOptions(Math.max(1, Math.floor((expires.getTime() - Date.now()) / 1000))));
  response.cookies.set(REFRESH_COOKIE, auth.refreshToken, cookieOptions(7 * 24 * 60 * 60));
}

function clearSession(response: NextResponse) {
  response.cookies.set(ACCESS_COOKIE, "", cookieOptions(0));
  response.cookies.set(REFRESH_COOKIE, "", cookieOptions(0));
}

async function toNextResponse(backend: Response) {
  const headers = new Headers();
  for (const name of ["content-type", "content-disposition", "cache-control", "x-content-type-options"]) {
    const value = backend.headers.get(name);
    if (value) headers.set(name, value);
  }
  return new NextResponse(backend.body, { status: backend.status, headers });
}

async function proxy(request: NextRequest, context: { params: Promise<{ path: string[] }> }) {
  const { path } = await context.params;
  const route = path.join("/");
  const cookieStore = await cookies();
  const body = request.method === "GET" || request.method === "HEAD" ? undefined : await request.arrayBuffer();

  if (route === "v1/auth/login") {
    const backend = await callBackend(request, path, body);
    if (!backend.ok) return toNextResponse(backend);
    const auth = (await backend.json()) as BackendAuthResponse;
    const response = NextResponse.json({ user: auth.user, accessTokenExpiresAt: auth.accessTokenExpiresAt });
    persistSession(response, auth);
    return response;
  }

  if (route === "v1/auth/logout") {
    const refreshToken = cookieStore.get(REFRESH_COOKIE)?.value;
    const accessToken = cookieStore.get(ACCESS_COOKIE)?.value;
    let backend: Response | null = null;
    if (refreshToken) {
      backend = await fetch(`${API_BASE_URL}/api/v1/auth/logout`, {
        method: "POST",
        headers: { "content-type": "application/json", authorization: `Bearer ${accessToken ?? ""}` },
        body: JSON.stringify({ refreshToken }),
        cache: "no-store",
      });
    }
    const response = backend ? await toNextResponse(backend) : new NextResponse(null, { status: 204 });
    clearSession(response);
    return response;
  }

  let accessToken = cookieStore.get(ACCESS_COOKIE)?.value;
  const refreshToken = cookieStore.get(REFRESH_COOKIE)?.value;

  if (route === "v1/auth/refresh") {
    if (!refreshToken) return NextResponse.json({ title: "Authentication required", status: 401, detail: "Your session has expired." }, { status: 401 });
    const auth = await rotateSession(refreshToken);
    if (!auth) {
      const response = NextResponse.json({ title: "Authentication required", status: 401, detail: "Your session has expired." }, { status: 401 });
      clearSession(response);
      return response;
    }
    const response = NextResponse.json({ user: auth.user, accessTokenExpiresAt: auth.accessTokenExpiresAt });
    persistSession(response, auth);
    return response;
  }

  let backend = await callBackend(request, path, body, accessToken);
  let rotated: BackendAuthResponse | null = null;
  if (backend.status === 401 && refreshToken) {
    rotated = await rotateSession(refreshToken);
    if (rotated) {
      accessToken = rotated.accessToken;
      backend = await callBackend(request, path, body, accessToken);
    }
  }

  const response = await toNextResponse(backend);
  if (rotated) persistSession(response, rotated);
  if (backend.status === 401) clearSession(response);
  return response;
}

export const GET = proxy;
export const POST = proxy;
export const PUT = proxy;
export const PATCH = proxy;
export const DELETE = proxy;
