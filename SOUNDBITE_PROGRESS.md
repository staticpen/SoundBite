# SoundBite — Development Progress

> **Purpose:** This is the living record of SoundBite's actual implementation status. Read it alongside `SOUNDBITE_CONTEXT.md` before planning or implementing work. Update it after every meaningful coding, testing, debugging, or planning session.
>
> **Accuracy rule:** Distinguish verified implementation from assumptions and planned work. Do not claim that code works, tests pass, or changes have been pushed unless that has been verified.

## 1. Current Project Snapshot

- **Project:** SoundBite
- **Application:** Blazor Web App
- **Interactivity:** Interactive Server
- **Language:** C#
- **Target framework:** .NET 9 for Visual Studio 2022 compatibility (verified in `SoundBite/SoundBite.csproj`)
- **Database:** SQLite
- **ORM:** Entity Framework Core
- **Authentication:** ASP.NET Core Identity
- **Testing framework:** NUnit
- **Version control:** Git and GitHub
- **Main context file:** `SOUNDBITE_CONTEXT.md`

## 2. Current Status

- **Overall status:** In progress
- **Current development phase:** Building working catalogue and social product features
- **Current focus:** Follow/unfollow implemented, reviewed and verified for the authorized commit
- **Last verified:** 2026-10-16
- **Current blocker:** Likes, comments, activity feed and external catalogue integration remain incomplete

## 3. Feature Implementation Tracker

| Feature | Status | Evidence / notes |
|---|---|---|
| Authentication | Implemented — verified | Website registration, username/email login, logout, profile editing, cookie persistence and authorization checks pass using isolated migrated databases; see latest work. |
| User profiles | Implemented — verified | `ApplicationUser` stores a display name and bio; the account page can edit them, and `/users/{username}` displays the public profile without exposing account email. The migration and persistence round-trip are tested. |
| Music catalogue | In progress | `Artist`, `Album`, and `Track` models exist with validation and EF relationship wiring in `ApplicationDbContext.cs`. There is no catalog UI or metadata service implementation beyond the model layer. |
| Spotify integration | Not started | `Services/SpotifyCatalogService.cs` is empty and there is no client configuration or API integration yet. |
| Track ratings and reviews | Implemented — verified | Reviews persist through EF Core; the service supports create/read/update, the database enforces one review per user per track and valid 1–5 ratings, and track details support public review reading plus authenticated submission/editing. |
| Album-track many-to-many relationship | Implemented — unverified | `ApplicationDbContext` maps `ArtistAlbums` and `ArtistTracks` and `Track.AlbumId` with a nullable optional foreign key. This matches the intended design, but the schema has not been validated via a migration review beyond the code. |
| Playlist management | Implemented — verified | Users can create, edit, and delete playlists; add/remove tracks; and browse public playlist listings/details. Owner checks are enforced in the service. |
| Playlist duplicate prevention | Implemented — verified | Service validation and a unique database index on playlist and track prevent duplicates. Playlist item positions preserve order. |
| Following and followers | Implemented — verified | Authenticated idempotent follow/unfollow, public counts and expandable 10-user pages; database constraints, concurrent requests, account deletion and browser flows verified. |
| Likes and comments | Not started | `Models/Like.cs` and `Models/Comment.cs` are empty. No authorization or interaction logic is implemented. |
| Activity feed | Not started | `Models/ActivityItem.cs`, `Services/FeedService.cs`, and social service classes are still empty or incomplete. |
| Rankings and trends | Planned | `Services/RankingService.cs`, `Services/TrendService.cs`, `Strategies/PopularityRankingStrategy.cs`, and `Strategies/RatingRankingStrategy.cs` exist, but no implemented ranking logic is visible in the current code. |
| Administrative user management | Not started | `Services/AdminService.cs` is empty and there is no admin UI or authorization pattern beyond Identity setup. |
| Automated tests | Passed — verified | 175 NUnit tests passed, 0 failed/skipped (2026-10-16), including 21 follow service/database cases and a rendered-profile integration regression. |
| Git and repository hygiene | Verified | `FollowUnfollow` starts at fetched `origin/main` (`e507912`); follow changes reviewed for the authorized commit. Existing database unchanged. |

## 4. Latest Completed Work

### 2026-10-17 — Step 2: Visual Studio 2022 compatibility (retarget to net9.0)

- **Finding:** Microsoft's SDK/MSBuild/VS version table states net10.0 can only be targeted from Visual Studio 18.0+; VS 2022 (17.x) targets up to net9.0. The code also used .NET 10-only APIs: `<ResourcePreloader />` (`App.razor`), `Router NotFoundPage=` (`Routes.razor`), `createScopeForStatusCodePages` (`Program.cs`) and the `BlazorDisableThrowNavigationException` property. Decision: net10.0 is **NO-GO** for VS 2022, so the projects were retargeted to net9.0 (**GO**, see limitations below).
- **Baseline before the retarget (net10.0, SDK 10.0.302):** app and test builds 0 warnings/0 errors; `dotnet test` **175 passed, 0 failed, 0 skipped**; smoke test `/` 200, `/not-found` 200, unknown URL 404, `/login` 200.
- **Changes (minimal, group-approved):** both projects `net10.0`→`net9.0`; `Identity.EntityFrameworkCore`, `EntityFrameworkCore.Design`, `EntityFrameworkCore.Sqlite`, `Mvc.Testing` `10.0.*`→`9.0.*`; removed `BlazorDisableThrowNavigationException`; removed `<ResourcePreloader />`; removed the custom `<ReconnectModal />` and its unreferenced `Components/Layout/ReconnectModal.razor`, `.razor.css` and `.razor.js` (the template relies on .NET 10-only `components-reconnect-state-changed` and `Blazor.resumeCircuit()`), so Blazor's built-in .NET 9 reconnection UI is used; `Routes.razor` now uses the .NET 9 `<NotFound><LayoutView>` form; `Program.cs` calls `UseStatusCodePagesWithReExecute("/not-found")`; added `SoundBite.sln` (classic `.sln`, both projects). Migrations and the model snapshot are untouched. Added two regression tests in `AuthenticationUiTests.cs` (app shell serves every stylesheet/script URL it references with 200, has no `Link` preload header and no custom reconnect element; unknown route returns 404 with the Not Found page).
- **Results on net9.0:** SDK 9.0.319 (user-local `~/.dotnet9`): solution build 0 errors, `dotnet test SoundBite.sln` **177 passed, 0 failed, 0 skipped** (175 existing + 2 new; re-run after the reconnect removal with the same totals). SDK 10.0.302: solution build 0 errors, also with `-p:LangVersion=13` (VS 2022 C# level); net9.0 tests run only via `DOTNET_ROLL_FORWARD=Major` (no .NET 9 runtime in the system install): 177 passed. Smoke test on net9: all routes as in the baseline, static assets 200, 404 shows the Not Found content, no `Link` preload header. `soundbite.db` SHA-256 unchanged (`ead1d21e…eeaf`).
- **Not verified / limitations:** Visual Studio 2022 itself is unavailable (macOS); CLI success is not proof. A Windows team member must open `SoundBite.sln` in VS 2022 17.12+, Build, run Test Explorer (expect 177) and launch the `https` profile. In Development, net9 does not fingerprint `blazor.web.js`/`ReconnectModal.razor.js` (cosmetic). Reconnection after removing `ReconnectModal`: in headless Chrome against `/playlists` (an `InteractiveServer` page), stopping the server made Blazor's built-in handler create its own `#components-reconnect-modal`, and restarting the server reloaded the page onto a new working circuit. It could not be confirmed that the built-in dialog is **visibly displayed** (the host element stayed `display: none` in the headless probe), so a person should check this once in a normal browser: open `/playlists`, stop the app, and confirm a reconnect dialog appears. The default `soundbite.db` is unmigrated (`no such table: Playlists`), so `/playlists` shows its error banner when run against it; this is the existing, documented state, not a regression. .NET 9 support ends 2026-11-10.

### 2026-10-16 — Follow/unfollow pre-commit review

- Reviewed the full tracked diff and all four new files: only follow implementation, migration/designer/snapshot, tests, minimal profile integration and this progress record are included. No blocking findings or implementation edits were needed. Aaron's existing profile markup/loading logic and account editing remain intact; `UserProfile.razor` adds only the keyed follow panel.
- Confirmed server-side authenticated actor resolution on every mutation, actor/target existence checks, actor-scoped deletion, parameterized insertion, and database-enforced unique pairs, no self-follows and valid user references. Migration designer matches the snapshot; only follow schema and EF-generated version metadata changed.
- Reran `dotnet build SoundBite/SoundBite.csproj --nologo`: 0 warnings/errors. Reran `dotnet test SoundBite.Tests/SoundBite.Tests.csproj --nologo`: 175 passed, 0 failed/skipped, including existing profile/authentication regressions. CRLF-aware whitespace check passed; `Program.cs` retains its existing CRLF format.
- Application database SHA-256 remains `ead1d21ea950e99cd6548f23d0b83159ac69c24b2bd4cbbf2dcac8376159eeaf`, identical to HEAD and before this review. WAL and SHM hashes also remained unchanged. Tests use isolated databases; no application migration was applied.
- Prior implementation-session browser verification is retained below; browser checks were not repeated during this review. User authorized the commit title `Implement user follow and unfollow functionality`; no push or merge is authorized.

### 2026-10-16 — Follow/unfollow implemented and verified

- Rechecked `origin/main` before implementation: branch `FollowUnfollow` and remote both at `e507912e62c4da48e70e74a32eb749ef239c9fd6`. Initial working tree was clean. Read context and progress before editing. No commit, push, merge, reset or stash performed.
- Added `Models/Follow.cs`, `Services/FollowService.cs`, `Components/Pages/ProfileFollowPanel.razor`, EF mapping and service registration. Aaron's `UserProfile.razor` has exactly one added integration line; its existing markup and profile-loading code remain intact.
- Composite primary key enforces one relationship per pair. A reverse composite index supports incoming follows. A database check rejects self-following; both user foreign keys cascade only to follow rows, so deleting an account removes incoming/outgoing relationships while preserving other accounts and their relationships.
- Mutations accept only the target user ID. The service resolves the actor from the server's `AuthenticationStateProvider` on every operation and checks the actor still exists. Anonymous, missing-claim, deleted-actor, self and missing-target requests are rejected. No new HTTP mutation endpoint or client-supplied actor ID was added.
- Follow uses parameterized SQLite `INSERT ... ON CONFLICT (FollowerId, FollowingId) DO NOTHING`; unfollow deletes only the authenticated actor's matching row. Both are idempotent. Independent-connection concurrency tests verify repeated and mixed requests without duplicate relationships. Other database constraint failures are not swallowed.
- Public counts and expandable follower/following lists use a fixed page size of 10, stable username/ID ordering, bounded offsets, and a public DTO exposing only username/display name. UI disables actions while busy, handles failures, hides self-follow controls, offers anonymous login, refreshes counts after mutations, and resets list state when navigating profiles.
- Added migration `20261015102645_AddFollows` (plus designer and snapshot). Temporary SQLite tests cover fresh migrations, upgrade from `AddPlaylists` with existing users/tracks/playlists, rollback and re-upgrade, model/snapshot consistency, uniqueness, foreign keys, self-follow prevention and account deletion.
- Verification: `dotnet build SoundBite/SoundBite.csproj --nologo` passed with **0 warnings, 0 errors**. `dotnet test SoundBite.Tests/SoundBite.Tests.csproj --nologo` passed **175/175**, 0 failed/skipped. Includes 21 new `FollowServiceTests` cases and one new `AuthenticationUiTests` rendered-profile integration case. Existing authentication, profile, review and playlist tests pass. `git -c core.whitespace=cr-at-eol diff --check` passed.
- Browser verification on `http://localhost:5197` with two separately registered disposable accounts: Bob followed Alice; Alice's followers became 1, Bob's following became 1, and lists linked to the correct profiles. Signed out, verified anonymous expanded-list access and no mutation button; signed in as Alice, followed Bob, refreshed to verify persistence, then unfollowed Bob. Bob's follower count returned to 0 while his outgoing relationship remained. Own profiles showed no Follow button. Added 12 temporary fixture users to verify 13 followers across 10/3-item pages, Previous/Next boundaries and collapse. A Razor string-binding error discovered during browser verification was corrected and covered by the new HTTP regression test.
- Isolation: browser database `/tmp/soundbite-follow-browser/test.db`; every automated follow test uses a separate temporary SQLite file. Existing `SoundBite/soundbite.db` SHA-256 before/after: `ead1d21ea950e99cd6548f23d0b83159ac69c24b2bd4cbbf2dcac8376159eeaf`, identical to HEAD. No migration was applied to the application database. EF CLI installed under `/tmp/soundbite-follow-tools`, without project/package changes; it reports an older tool patch than runtime, but generation and migration execution succeeded.
- Outstanding: application database must be migrated in a separately authorized deployment step before using the feature there. Counts refresh on page load, list actions and local mutations; there is no live cross-session count broadcast. Existing authentication circuit revalidation behavior was preserved. No implementation blocker remains.
- Changed files: `Models/Follow.cs`, `Services/FollowService.cs`, `Data/ApplicationDbContext.cs`, `Program.cs`, `Components/Pages/ProfileFollowPanel.razor`, `Components/Pages/UserProfile.razor`, migration/designer/snapshot, `SoundBite.Tests/FollowServiceTests.cs`, `SoundBite.Tests/AuthenticationUiTests.cs`, and this progress file.

### 2026-10-15 — Authentication UI repaired and verified

- Fetched `origin/main` and confirmed PR #13's routing fix: `168d967faf1de9efc3619e34d2e0bddb041be903`. The requested `FixAuthenticationUI` branch already existed at exactly that commit with a clean working tree, so it was reused without resetting it.
- Root causes: Register used a relative API URL on an HttpClient without a base address. Account called cookie-writing Identity methods inside an Interactive Server circuit after HTTP response headers had started. A server-side HTTP client would also not deliver its received cookie to the browser.
- Solution: `/register`, `/login`, and `/account` use static SSR with named HTTP POST forms, form binding, server validation and antiforgery protection. App selects render mode through `AcceptsInteractiveRouting()`. Registration and login issue Identity cookies during the HTTP request and redirect to `/account`; logout is an antiforgery-protected POST. Other pages retain interactivity. Account links to the dedicated registration page rather than maintaining a second registration implementation. Profile loading preserves posted values; updates resolve the current authenticated user on the server. Website login enables Identity lockout counting.
- Changed files: `SoundBite/Components/App.razor`, `SoundBite/Components/Pages/Account.razor`, `SoundBite/Components/Pages/Register.razor`, `SoundBite/Program.cs` (public entry point for integration tests), `SoundBite.Tests/SoundBite.Tests.csproj` (MVC testing dependency), new `SoundBite.Tests/AuthenticationUiTests.cs`, and this progress file. No models, migrations, review/playlist services, or existing JSON API endpoints changed.
- `dotnet build SoundBite/SoundBite.csproj --nologo`: passed, 0 warnings/errors. `dotnet test SoundBite.Tests/SoundBite.Tests.csproj --nologo`: **153 passed, 0 failed, 0 skipped**. Nine new HTTP cases cover registration cookie issuance, username/email login, logout, profile persistence/public privacy, invalid credentials/duplicate registration/password mismatch, invalid profile rejection, anonymous profile forgery, and missing antiforgery tokens on all four forms. Authenticated/anonymous review and playlist rendering is also asserted; existing ownership and service authorization tests pass.
- Browser verified using `http://localhost:5193`: registration auto-login; profile editing and persisted values; email login; logout; authenticated refresh/navigation; authenticated playlist creation with owner management controls; authenticated review submission; anonymous public review/playlist reading without mutation controls. Browser input automation required actual keyboard change events for interactive forms. No application defect remained from that tooling behavior.
- Isolation: fresh browser database `/tmp/soundbite-auth-browser/test.db` migrated using an external console harness, with one test track. Server launched with explicit `ConnectionStrings__DefaultConnection` override. HTTP tests each migrate their own unique temporary SQLite database and remove it after disposal. Existing `SoundBite/soundbite.db` SHA-256 before/after remained `ead1d21ea950e99cd6548f23d0b83159ac69c24b2bd4cbbf2dcac8376159eeaf`.
- Diff hygiene: `git -c core.whitespace=cr-at-eol diff --check` passed; existing CRLF line endings were preserved in App and Program.
- Commit verification (2026-10-15): reviewed the full tracked diff and new test file; no unrelated changes found. Re-ran the full application build (0 warnings/errors) and NUnit suite (153 passed, 0 failed/skipped). Route uniqueness/ownership and live HTTP authentication tests passed. Antiforgery tokens are generated by SSR EditForms and explicitly on logout; missing tokens are rejected on all four forms.
- Security review: Identity cookies received over HTTPS have Secure, HttpOnly and SameSite=Lax flags (regression asserted); the existing framework SecurePolicy follows the request, with HTTPS redirection and production HSTS in place. Sign-ins use session cookies. All UI redirects target the fixed `/account` path; an external query/form ReturnUrl is ignored in a new regression test. Failed login/registration submissions now clear bound passwords before rendering, including invalid-form submissions. Regression assertions cover failed credentials, mismatched and duplicate registration password disclosure, and public-profile email privacy. No credential logging or new sensitive API responses were introduced; pre-existing JSON APIs are unchanged.
- Database verification: `SoundBite/soundbite.db` is byte-identical to HEAD and retains SHA-256 `ead1d21ea950e99cd6548f23d0b83159ac69c24b2bd4cbbf2dcac8376159eeaf` after the full test run. Only temporary/in-memory databases were used by tests.
- Outstanding: no blocker remains for the requested authentication UI flows. Existing JSON APIs remain separate and unchanged; broader authentication features such as confirmation/recovery and cross-tab circuit revalidation were not added. The application database was not migrated. Pre-commit review completed; this work is included in `Fix authentication UI and cookie handling`. No push or merge authorized or performed.


### 2026-10-15 — Duplicate registration route fixed and verified

- Removed only `@page "/register"` from `SoundBite/Components/Pages/Account.razor`; the dedicated `Register.razor` owns `/register`, while Account retains `/login` and `/account`. No authentication logic changed.
- Added `SoundBite.Tests/PageRouteTests.cs`: scans compiled component route attributes for duplicate templates (case-insensitive, ignoring trailing slashes) and checks the owners of all three authentication routes. This detects duplicate declarations, not every possible overlap between parameterized route patterns.
- Regression proof: before the fix, 2 of the 4 new cases failed and explicitly identified Account and Register sharing `/register`; after the fix the full suite passed.
- `dotnet build SoundBite/SoundBite.csproj --nologo`: passed, 0 warnings/errors. `dotnet test SoundBite.Tests/SoundBite.Tests.csproj --nologo`: 144 passed, 0 failed/skipped. `git diff --check`: passed.
- Browser: `/register`, `/login`, `/account`, `/catalogue/search` all loaded without routing errors. Followed Register's Log in link successfully. Existing navigation targets Account, and review/playlist login links and account redirects remain valid; no link changes required.
- Used a fresh SQLite database at `/tmp/soundbite-route-check.jPlMyn/test.db`, migrated through a temporary external console harness with `Database.MigrateAsync()` because `dotnet ef` was unavailable. Launched with an explicit `ConnectionStrings__DefaultConnection` override on localhost:5187. No application database migration was run.
- API checks against temporary data: registration 200, authenticated `/api/account/me` 200, logout 200, unauthenticated `/me` 401, username and email login 200. An invalid-password request was rejected (observed final HTTP 400 with automatic redirects enabled; not asserted as the endpoint's original status).
- Existing UI issues reproduced: Register submission displays “Unable to reach the server” (relative request URL with no HTTP client base address configured); valid Account login throws `InvalidOperationException: Headers are read-only, response has already started` when setting the cookie from the interactive circuit. These are separate from route resolution and were left unchanged under this task's authentication scope boundary.
- Existing `SoundBite/soundbite.db` SHA-256 before/after: `ead1d21ea950e99cd6548f23d0b83159ac69c24b2bd4cbbf2dcac8376159eeaf`. Checkout was initially clean; only the three requested implementation/documentation files changed. No commit, push or merge performed.

### 2026-10-15 — Playlist persistence and management implemented

- **Task completed:** Added playlists owned by Identity users, ordered playlist tracks, public listing/details, and owner-only playlist and track management.
- **Files or components changed:** Playlist models, EF mappings and `PlaylistService`; `/playlists` and `/playlists/{id}` pages; navigation; `AddPlaylists` migration and model snapshot; `PlaylistServiceTests.cs`.
- **Important implementation decisions:** Playlists are public by default. Only owners can create, edit, delete, or change tracks. A unique database index prevents duplicate tracks in a playlist; deleting a playlist cascades to its items, not to tracks.
- **Verification performed and its result:** Full NUnit suite passed: 128/128. Focused playlist tests passed: 9/9, covering persistence, visibility, ownership, CRUD, ordering, and duplicate prevention.
- **Remaining limitations:** The configured application database was not migrated during this session. Browser-based playlist interaction has not been manually tested.

### 2026-10-15 — Public and editable user profiles implemented

- **Task completed:** Added profile data, profile editing for the signed-in user, and a public profile route.
- **Files or components changed:** `SoundBite/Data/ApplicationUser.cs`, `SoundBite/Components/Pages/Account.razor`, `SoundBite/Components/Pages/UserProfile.razor`, the EF migration and model snapshot, and `SoundBite.Tests/CataloguePersistenceTests.cs`.
- **Important implementation decisions:** Kept profile fields on the Identity user for the smallest coherent implementation. Public profile pages show display name, username, and optional bio, not email. The migration supplies an empty display-name default so existing accounts remain compatible.
- **Verification performed and its result:** `dotnet test SoundBite.Tests/SoundBite.Tests.csproj -v minimal` passed: 116/116. The focused migration/persistence tests passed: 20/20.
- **Remaining limitations:** Apply the new migration to the app's configured database before using profile editing (`dotnet ef database update --project SoundBite/SoundBite.csproj --startup-project SoundBite/SoundBite.csproj`). No profile image, profile activity, or social follow functionality is included. The page was compile-validated but not manually exercised in a browser.

### 2026-10-15 — Account authentication flow implemented

- **Task completed:** Added a working interactive account page and endpoint support for registration, sign-in, sign-out, and current-user lookup.
- **Files or components changed:** `SoundBite/Endpoints/AccountEndpoints.cs`, `SoundBite/Components/Layout/NavMenu.razor`, and `SoundBite/Components/Pages/Account.razor`.
- **Important implementation decisions:** The fastest path to a working product is to make the account workflow usable in-browser immediately instead of only exposing a bare API endpoint. The account screen now supports registration, login, and logout and shows the signed-in user details.
- **Verification performed and its result:** Ran `dotnet test SoundBite.Tests/SoundBite.Tests.csproj -v minimal`; result: `115/115` tests passed successfully.
- **Remaining limitations:** At that point, there was no editable profile or public profile page; both have since been added.

### 2026-10-15 — Earlier review branch discrepancy resolved

- **Task status:** An earlier checkpoint found that review changes were missing from the `Authentication` branch at that time. The review feature has since been implemented and is present in the current branch.
- **Current evidence:** Reviews are EF-backed, have a persistence migration and tests, and are available from the track detail workflow.
- **Action:** The current feature tracker reflects the implementation; the earlier checkpoint is retained only as historical context.

### 2026-10-15 — Repository baseline verification and progress update

- **Task completed:** Verified the current configuration, test state, startup state, and project structure against the repository.
- **Files or components changed:** `SOUNDBITE_PROGRESS.md` was updated to reflect the repository state; no application code was changed during this verification session.
- **Important implementation decisions:** The project is confirmed to be a starter scaffold rather than a fully implemented social music application. Identity + SQLite + EF Core are established, but most domain features remain stubs.
- **Verification performed and its result:** Ran `dotnet test SoundBite.Tests/SoundBite.Tests.csproj -v minimal`; result: 70/70 tests passed. Ran `dotnet run --project SoundBite --urls http://localhost:5080`; result: application started successfully and reported `Now listening on: http://localhost:5080` and `Application started.`
- **Remaining limitations:** No end-to-end authentication flow, catalogue UI, playlist logic, or social interaction workflow was validated beyond compile/startup and test execution.

## 5. Current Implementation Details

- The application entry point in `SoundBite/Program.cs` configures a Blazor Web App with `AddRazorComponents().AddInteractiveServerComponents()`, SQLite via `UseSqlite(...)`, and ASP.NET Core Identity via `AddIdentityCore<ApplicationUser>()` and `AddEntityFrameworkStores<ApplicationDbContext>()`.
- `SoundBite/Data/ApplicationDbContext.cs` defines the main EF entities: `Artists`, `Albums`, and `Tracks`, and uses many-to-many joins to model `ArtistAlbums` and `ArtistTracks`.
- `Track` has an optional `AlbumId`/`Album` relationship and uses validation attributes for required `Title` and optional non-whitespace `SpotifyId`.
- `Album`, `Artist`, and `Track` models initialize empty navigation collections and enforce basic validation requirements.
- `Endpoints/AccountEndpoints.cs` exposes registration, login, logout, and current-user endpoints.
- `Data/ApplicationUser.cs` includes `DisplayName` and optional `Bio`. The account page edits these fields; `/users/{username}` displays a public profile without exposing email.
- Remaining model and service placeholders include `Like`, `Comment`, `ActivityItem`, `SpotifyCatalogService`, `AdminService`, `FeedService`, `SocialInteractionService`, `UserService`, and the ranking/trend services. Follow persistence and `FollowService` are implemented.
- The NUnit test project is active and passes, and the app builds and starts successfully.
- `20261015074342_AddUserProfiles` migrates the display-name and bio fields; the playlist migration adds playlists and ordered track items.

## 6. Important Decisions and Assumptions

- **Decision:** Keep the app in a scaffolded early stage while establishing core app infrastructure.
  - **Reason:** This is consistent with the repository evidence: Identity, EF Core, and core models are present, but the social product features remain unfinished.
  - **Impact:** Future work must build on the existing EF/Identity foundation rather than reworking the app structure.
  - **Status:** Confirmed

- **Decision:** Model the catalogue as `Artist`/`Album`/`Track` with EF many-to-many relationships and optional album assignment.
  - **Reason:** The relationship structure is explicit in `ApplicationDbContext` and aligns with the intended track/album requirements.
  - **Impact:** Future catalogue features can extend this model without changing the core schema pattern.
  - **Status:** Confirmed

- **Decision:** Keep the review/rating feature as a small, EF-backed domain model rather than an in-memory placeholder.
  - **Reason:** The review workflow is now represented in the data model and service layer, with database uniqueness and validation enforced through code and schema constraints.
  - **Impact:** Product requirements for user reviews can now be satisfied with a stable domain and test coverage, and future work can build on the same pattern for other social features.
  - **Status:** Confirmed

## 7. Known Issues and Blockers

| Issue | Impact | Evidence | Next action | Status |
|---|---|---|---|---|
| Registration form cannot reach endpoint | Resolved | Replaced server-side loopback HTTP call with static SSR form processing | Verified in browser and HTTP tests | Closed |
| Interactive login cannot set cookie | Resolved | Cookie operations now execute during HTTP form requests before rendering | Verified registration, login and logout | Closed |
| Remaining social features | Likes, comments and activity feed are incomplete | Their services/models remain placeholders; follows are implemented | Implement a basic activity feed and remaining interactions | Open |
| Remaining social persistence | Likes, comments and feed persistence are missing | Empty `Like.cs`, `Comment.cs`, `ActivityItem.cs` and related services | Design remaining social entities | Open |
| No Spotify client is configured | Music catalogue enrichment is not connected to external metadata | `SpotifyCatalogService.cs` is empty and no API client configuration exists | Add a service abstraction and integrate Spotify metadata access | Open |

## 8. Verification Record

| Check | Result | Notes |
|---|---|---|
| Application build | Passed | `dotnet test SoundBite.Tests/SoundBite.Tests.csproj -v minimal` built the app and test project successfully. |
| Automated tests | Passed — verified | 175 NUnit tests passed, 0 failed/skipped (2026-10-16), including 21 follow service/database cases and a rendered-profile integration regression. |
| Application startup | Passed | Command: `dotnet run --project SoundBite --urls http://localhost:5080` → app reported `Now listening on: http://localhost:5080` and `Application started.` |
| Relevant feature behaviour | Passed | Website authentication, profile editing, refresh/navigation and authenticated/anonymous review/playlist access verified; see latest work. |
| Database migrations | Passed | Profile, review, and playlist migrations are exercised by SQLite persistence tests, including playlist creation and CRUD against the migrated schema. |

## 9. Git Status

- **Active branch:** `FollowUnfollow`, based on fetched `origin/main` at `e507912e62c4da48e70e74a32eb749ef239c9fd6`.
- **Initial checkout:** Clean. No unrelated changes or stashing required.
- **Current work:** Follow implementation, migration, regression tests and progress update reviewed and verified for the authorized commit. No push or merge performed.
- **Authorized commit title:** `Implement user follow and unfollow functionality` (commit identity is recorded in Git history).
- **Suggested description:** Add idempotent follow/unfollow, public paginated profile lists, SQLite constraints and cascading relationship cleanup, with migration, concurrency, authorization and UI regression coverage.

## 10. Next Recommended Tasks

1. **Task:** Implement a basic activity feed and remaining social interactions.
   - **Reason:** The application’s core differentiation is social discovery, and the current models/services are still empty.
   - **Dependencies:** Build on the verified follow model; finalize like, comment and activity schemas.
   - **Acceptance criteria:** Users can follow others, like content, comment, and see feed activity without duplicate or invalid entries.

2. **Task:** Build the Spotify metadata abstraction and catalogue service.
   - **Reason:** The app requires external catalogue data and currently has no integration point.
   - **Dependencies:** Decide on a client configuration and service contract before wiring metadata retrieval.
   - **Acceptance criteria:** Metadata can be fetched and mapped into `Artist`, `Album`, and `Track` models without coupling the UI directly to Spotify APIs.

## 11. Handover for the Next Chat Session

- Follow/unfollow implementation is verified on `FollowUnfollow` with 175 passing tests and two-account browser checks. Full diff reviewed and build/tests rerun for the authorized commit. Do not push or merge without authorization.
- Existing database remains unchanged and unmigrated. Browser fixtures are isolated at `/tmp/soundbite-follow-browser/test.db`.
- Apply the migration only as a separately authorized database/deployment step. Likes, comments, feed, Spotify integration and profile activity remain independent future work.

## 12. Instructions for Updating This File

Whenever meaningful work is performed:

1. Re-read `SOUNDBITE_CONTEXT.md` and this progress file if they are not already available in the current working context.
2. Compare the previous progress notes with the changes actually made.
3. Update the feature tracker where relevant.
4. Record the files changed and the purpose of those changes.
5. Record actual build, test, startup, or manual verification results.
6. Update decisions, blockers, Git status, and next steps as necessary.
7. Update the handover summary.
8. Remove stale information and keep the document concise.
9. Preserve unresolved work instead of implying it is finished.
10. Save the updated file in the repository.

**Do not replace this document with a full copy of `SOUNDBITE_CONTEXT.md`.** The two files have different responsibilities. This file records project progress; the main context file records stable requirements, design decisions, and development rules.
