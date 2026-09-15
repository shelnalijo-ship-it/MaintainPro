# MaintainPro Manager Web Portal

The manager portal is a responsive Next.js App Router application for MaintainPro. It covers the manager, administrator, and permitted supervisor workflows exposed by the existing ASP.NET Core API.

## Local setup

1. Copy `.env.example` to `.env.local`.
2. Set `NEXT_PUBLIC_API_BASE_URL` to the MaintainPro API origin. The checked-in example uses `http://localhost:5043`.
3. Start the API.
4. Install packages and start the portal:

```powershell
npm install
npm run dev
```

Open `http://localhost:3000`. Do not place database credentials, signing keys, access tokens, or refresh tokens in frontend environment files.

## Architecture

- `src/app` contains App Router pages, portal layouts, loading/error boundaries, and the same-origin backend proxy.
- `src/features` contains business screens grouped by MaintainPro module.
- `src/components` contains the portal shell and shared UI system.
- `src/lib/api-client.ts` is the browser API boundary for JSON, multipart forms, Problem Details errors, cancellation, paging, and downloads.
- `src/types/api.ts` contains DTOs aligned with the backend contracts.

The browser calls `/api/backend/*`. The proxy forwards requests to the configured API and keeps access and refresh tokens in secure HTTP-only cookies. It retries a failed authorized request once after a successful token refresh and clears the session on logout or failed refresh.

TanStack Query manages server state and cache invalidation. React Hook Form and Zod provide form state and client validation. Recharts renders dashboard analytics, while native CSS handles the shared responsive design system.

## Verification

```powershell
npm run lint
npm run typecheck
npm run test
npm run build
```

Tests cover login and validation, protected navigation and logout, token refresh, KPI/empty/error dashboard states, machine workflows and authorization, maintenance plan/checklist behavior, and report filtering/export.

## Current API limitations

- The backend has no audit-log query endpoint, so the audit page explains that audit records cannot yet be displayed.
- Machine documents are exposed per machine, so the document library requires a machine selection before loading records.

Neither limitation is hidden or replaced with sample production data.
