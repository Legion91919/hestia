# User flows

## 1. Investor opens this quarter's statement (slice 1)
1. `/login` (`Pages/Login`): email + password. Server checks the password (Auth block), refuses revoked users, sets an HttpOnly cookie holding the bearer token.
2. `/` redirects investors to `/documents` (`Pages/Documents`, screen `screens/statements.html`). Data: `GET /api/documents` (same query, tenant from the `tenant_id` claim).
3. "Open statement" calls `GET /api/documents/{id}/download`. The server writes `document.downloaded` (who, what, when) to the Audit block, then streams the PDF.
4. `UpdatesBell` shows unread notifications; `/updates` lists them (Notifications block inbox).

## 2. IR publishes a statement (slice 1, supports flow 1)
1. `/ir` (`Pages/IrConsole`), "Publish statement" form (investor, fund, period, closing balance) or `PUT /api/ir/investors/{tenantId}/documents/{documentId}` with a file.
2. The PDF is stored under `<root>/<tenantId>/<documentId>.pdf`; every active user of that investor gets an in-app "New statement" notification.

## 3. IR revokes a user (slice 1)
1. `/ir`, investor users table, "Revoke access" (or `POST /api/ir/users/{userId}/revoke`).
2. The user's security stamp rotates and the user is marked revoked: their next request (page, API or download) is refused, and sign-in is refused.

## 4. IR answers "who opened what?" (slice 1)
1. `/ir`, access log panel (newest downloads), or `GET /api/audit/entities/document/{id}` (Audit block, role `ir`).
