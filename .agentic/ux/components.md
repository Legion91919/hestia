# Components (Blazor, `src/Hestia.Api/Components/`)

| Component | Props / inputs | Loading | Empty | Error | Success |
|---|---|---|---|---|---|
| `MainLayout` | `Body`; reads auth state | n/a (server-rendered) | n/a | n/a | brand bar, tenant name, Updates link, sign-out |
| `Pages/Login` (`/login`) | form `Email`, `Password`, `ReturnUrl` | button disabled on submit | n/a | "Email or password is wrong" / "access revoked" banner (`role=alert`) | redirect to `/` |
| `Pages/Documents` (`/documents`) | `DocumentsPageModel` (hero + earlier rows) | streaming placeholder (skeleton) while the list loads | "No documents yet" card | error card with correlation reference and "Try again" | hero card for newest statement + earlier list |
| `DocumentRow` | `DocumentRowModel { Title, Fund, Period, PublishedLabel, SizeLabel, IsNew, DownloadUrl }` | n/a | n/a | n/a | title, metadata line, "New"/"opened" in words, Open link |
| `UpdatesBell` (interactive server) | user id from `AuthenticationState` | "Updates" with no count | "Updates" (no badge) | silently shows no badge, retries on next tick | badge with unread count, refreshed every 15 s |
| `Pages/Updates` (`/updates`) | inbox page for the user | skeleton | "Nothing new" | error card | list of notifications, newest first |
| `Pages/IrConsole` (`/ir`) | `IrConsoleModel { Investors, Users, RecentAccess }`; SSR forms `revoke`, `restore`, `publish` | n/a (SSR) | "No downloads yet" in access log | form error banner | three panels: publish statement, investor users (revoke/restore), access log |
