# Task Manager

A small to-do app. .NET 10 / EF Core / SQLite on the back, React + TypeScript (Vite) on the front.

## Prerequisites

- .NET SDK 10.0+
- Node 20+ with npm

## Run it

Backend on port 5154. The database file is created on first run.

```bash
cd backend
dotnet run --project src/TaskManager.Api --launch-profile http
```

Frontend on port 5173, in a second terminal:

```bash
cd frontend
npm install
npm run dev
```

Tests:

```bash
cd backend
dotnet test
```

The frontend reads the API URL from `frontend/.env.development`.

---

## What's built

**Auth** ([backend/src/TaskManager.Api/Auth](backend/src/TaskManager.Api/Auth))

- Register and login endpoints return a signed token. The frontend sends it on every request.
- Tokens last 7 days.
- Passwords are hashed with Argon2id (see Engineering decisions).
- Every task is tied to a user. The database itself is taught to only return rows the caller owns, so a forgotten check in a controller can't leak someone else's data.
- A failed login returns the same error whether the email exists or not, so the endpoint can't be used to figure out which emails are registered.

**Tasks API** ([backend/src/TaskManager.Api](backend/src/TaskManager.Api))

- `GET /api/tasks?status=` lists newest first, optional status filter.
- `GET /api/tasks/{id}` returns 404 if the task is missing or owned by someone else.
- `POST /api/tasks` returns 201 with the new task's URL in the `Location` header.
- `PUT /api/tasks/{id}` updates a task, with a check to detect simultaneous edits (see below).
- `DELETE /api/tasks/{id}` is a soft delete and returns 204.
- Validation lives next to the request types. Bad input gets a 400 with a structured error body.
- Migration ships in source and runs on startup. CORS allows the dev frontend.

**Frontend** ([frontend/src](frontend/src))

- Sign-in / register screen. Token lives in `localStorage`. Sign-out button in the header.
- Full CRUD with immediate list updates, status filter, overdue badge.
- A 409 from a save pops a conflict resolution UI: the server's current version next to your pending edit, with **Discard mine** or **Overwrite with mine**.
- Form validation uses the same schema the API client parses responses with, so the rules live in one place.
- Optimistic delete that puts the row back if the request fails. Loading, empty, and error states throughout.
- Due dates are entered and shown in the user's local time, but sent over the wire as UTC.
- Light and dark mode follow the OS setting.

**Tests** ([backend/tests/TaskManager.Api.Tests](backend/tests/TaskManager.Api.Tests))

20 integration tests against the real API and a real SQLite database (in memory for speed). The riskiest areas get the most coverage:

- **Cross-user authorization** (4 tests). User A cannot GET, PUT, or DELETE User B's task. List only returns the caller's tasks.
- **Validation** (3 tests). Empty title rejected. Oversized title rejected. Short password rejected on register.

Also covered: happy paths and 404s, the concurrent-edit case, the soft delete policy (verified by reading the row back from the database directly), an unauthenticated request gets a 401, and login can't be used to enumerate accounts.

In-memory SQLite rather than the EF in-memory provider, because the EF provider silently ignores concurrent-edit checks. The concurrency test would pass without actually proving anything.

---

## Engineering decisions

**Argon2id for passwords.** Password hashes need to be slow on purpose. Slow enough that if the database ever leaks, an attacker can't try millions of guesses per second on a GPU. Argon2id is the current best choice for that. It's deliberately memory-hungry, which makes parallel GPU attacks expensive. The settings live in [PasswordHasher.cs](backend/src/TaskManager.Api/Auth/PasswordHasher.cs) and aim for about half a second per check, which is invisible at the login screen but punishing in bulk. Each stored hash carries the settings it was generated with, so the cost can be turned up later without forcing a password reset.

**Ownership enforced in the database layer, not the controller.** The "this user can only see their own tasks" rule lives in the database setup, not inside any one endpoint. Every read inherits it automatically. If a new endpoint is added tomorrow and forgets to filter by owner, the rule still applies. Defense in depth.

**Detect concurrent edits.** Each task carries a version number that goes up by one on every save. When you save, the request includes the version you started from. If someone else saved in between, the server sees a mismatch and returns a 409 with the current state of the task. The UI uses that to show you both versions and ask what you want to do. Same user, two tabs is a real scenario. Without this, the second save silently wipes out the first.

**Soft delete instead of hard delete.** Deleting a task sets a `DeletedAt` timestamp instead of removing the row. The database is told to hide those rows from every read. The data is still there for accidental-delete recovery and audit. A trash/restore UI is the natural next step but isn't in scope.

**Shared validation between frontend and backend.** The contract is written on both sides: C# request types on the server, Zod schemas in [schemas.ts](frontend/src/schemas.ts) on the client. Nothing forces those two to stay in sync at compile time. To close that gap on the frontend: the Zod schemas are the source of truth for the TypeScript types *and* the form validation rules, so adding a field to the schema means the form has to handle it. Every API response is also parsed through a schema at the boundary, so if the server's wire format ever drifts, you get a clear error at the network call instead of `undefined` deep in a component. Generating the types directly from the backend's API spec would close the gap completely but adds a build step. Zod at the boundary felt like the right stopping point.

---

## Deliberately left out

- **Refresh tokens.** Access tokens last 7 days. A real production setup would issue short-lived access tokens (around 15 minutes) plus a refresh token, and rotate them. Out of scope.
- **Password reset and email verification.** Both need an email provider and a token-link flow.
- **Rate limiting on auth.** Each password check takes ~500ms which is its own backpressure, but production should add a real limiter on the auth endpoints to deter brute-force.
- **Caching.** SQLite reads on a few hundred tasks are already sub-millisecond. There's nothing to speed up. The first move when caching does matter is conditional GETs (the browser asks "do you have a newer version?"), not an in-process or distributed cache.
- **Pagination.** Fine for hundreds of tasks. Would add a `?skip=&take=` or a cursor for thousands.
- **Trash / restore UI.** Soft-deleted rows are in the database but unreachable from the API.
- **Activity history**, drag-and-drop reordering, recurring tasks, subtasks, tags, attachments.

## With another day

1. Refresh tokens with rotation, plus a sign-out endpoint that revokes them.
2. End-to-end tests in a browser for the full create / edit / delete flows, and the conflict UI with two windows.
3. Rate limiting on the auth endpoints, and password reset.
4. Accessibility pass. Keyboard navigation, focus management on form open/close, screen-reader announcements on the banners.
5. Structured logging with request correlation IDs.

## Trade-offs

- **Token in `localStorage`.** Simpler than HTTP-only cookies. The downside is that if the app ever ships a script-injection bug, the token is reachable. Cookies would need cross-site-request protection and stricter content security headers, which is more wiring for an exercise this size. Production should move to cookies.
- **No repository layer.** `DbContext` is already an abstraction over the database. A repository with one implementation would just be a second name for the same thing.
- **`DataAnnotations` over FluentValidation.** Validation rules are simple and live next to the request types.
- **No frontend state library.** `useState` plus one `useTasks` hook is enough.
- **SQLite over EF in-memory.** EF's in-memory provider loses data on restart and skips concurrency checks.
- **Migration runs on startup.** Fine for one process. With multiple replicas it should move to a separate init step.
- **No HTTPS in dev.** In production, TLS would terminate at the load balancer.

## Project layout

```
backend/
  src/TaskManager.Api/
    Auth/         # Password hasher, token issuer, current-user accessor
    Contracts/    # Request and response types + validation
    Controllers/  # AuthController, TasksController
    Data/         # DbContext + migration
    Domain/       # User, TaskItem
    Program.cs
  tests/TaskManager.Api.Tests/
    ApiFactory.cs     # Test host over in-memory SQLite
    AuthApiTests.cs   # 6 auth tests
    TasksApiTests.cs  # 14 task + cross-user tests
frontend/src/
  api/         # Fetch wrapper (Zod-parsed), tasks + auth endpoints
  auth/        # AuthContext, token storage
  components/  # AuthScreen, TaskForm, TaskRow, ConflictBanner
  hooks/       # useTasks
  schemas.ts   # Zod schemas, source of truth for FE types and validation
  App.tsx, App.css, main.tsx
```
