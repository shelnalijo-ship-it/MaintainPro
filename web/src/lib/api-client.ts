import type { PagedResult, ProblemDetails } from "@/types/api";

const API_ROOT = "/api/backend/v1";

export class ApiError extends Error {
  constructor(public readonly status: number, public readonly problem: ProblemDetails) {
    super(problem.detail || problem.title || `Request failed with status ${status}`);
    this.name = "ApiError";
  }
}

export type QueryValue = string | number | boolean | null | undefined;

export function queryString(values: Record<string, QueryValue>) {
  const params = new URLSearchParams();
  Object.entries(values).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== "") params.set(key, String(value));
  });
  const query = params.toString();
  return query ? `?${query}` : "";
}

async function parseProblem(response: Response): Promise<ProblemDetails> {
  try {
    const body = (await response.json()) as ProblemDetails;
    return { ...body, status: body.status ?? response.status };
  } catch {
    return { status: response.status, title: response.statusText || "Request failed" };
  }
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  if (init.body && !(init.body instanceof FormData) && !headers.has("content-type")) headers.set("content-type", "application/json");
  headers.set("accept", "application/json");
  const response = await fetch(`${API_ROOT}${path}`, { ...init, headers, credentials: "same-origin" });
  if (!response.ok) throw new ApiError(response.status, await parseProblem(response));
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export async function download(path: string, fallbackName: string, signal?: AbortSignal) {
  const response = await fetch(`${API_ROOT}${path}`, { signal, credentials: "same-origin" });
  if (!response.ok) throw new ApiError(response.status, await parseProblem(response));
  const blob = await response.blob();
  const disposition = response.headers.get("content-disposition") ?? "";
  const match = /filename\*?=(?:UTF-8''|\")?([^\";]+)/i.exec(disposition);
  const filename = match ? decodeURIComponent(match[1].replace(/\"$/, "")) : fallbackName;
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = filename;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(url);
}

export const api = {
  get: <T>(path: string, signal?: AbortSignal) => request<T>(path, { signal }),
  post: <T>(path: string, body?: unknown, signal?: AbortSignal) => request<T>(path, { method: "POST", body: body instanceof FormData ? body : body === undefined ? undefined : JSON.stringify(body), signal }),
  put: <T>(path: string, body: unknown, signal?: AbortSignal) => request<T>(path, { method: "PUT", body: body instanceof FormData ? body : JSON.stringify(body), signal }),
  patch: <T>(path: string, body: unknown, signal?: AbortSignal) => request<T>(path, { method: "PATCH", body: JSON.stringify(body), signal }),
  delete: <T = void>(path: string, signal?: AbortSignal) => request<T>(path, { method: "DELETE", signal }),
  page: <T>(path: string, signal?: AbortSignal) => request<PagedResult<T>>(path, { signal }),
  download,
};

export function errorMessage(error: unknown) {
  if (error instanceof ApiError) {
    const validation = error.problem.errors ? Object.values(error.problem.errors).flat()[0] : undefined;
    return validation ?? error.problem.detail ?? error.problem.title ?? error.message;
  }
  if (error instanceof TypeError) return "The server could not be reached. Check the API address and try again.";
  return error instanceof Error ? error.message : "Something went wrong. Please try again.";
}
