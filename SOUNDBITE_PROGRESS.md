# SoundBite — Development Progress

> **Purpose:** This is the living record of SoundBite's actual implementation status. Read it alongside `SOUNDBITE_CONTEXT.md` before planning or implementing work. Update it after every meaningful coding, testing, debugging, or planning session.
>
> **Accuracy rule:** Distinguish verified implementation from assumptions and planned work. Do not claim that code works, tests pass, or changes have been pushed unless that has been verified.

## 1. Current Project Snapshot

- **Project:** SoundBite
- **Application:** Blazor Web App
- **Interactivity:** Interactive Server
- **Language:** C#
- **Target framework:** .NET 9, SDK 9.0.319, Microsoft/EF packages 9.0.21
- **Database:** SQLite
- **ORM:** Entity Framework Core
- **Authentication:** ASP.NET Core Identity
- **Testing framework:** NUnit
- **Version control:** Git and GitHub
- **Main context file:** `SOUNDBITE_CONTEXT.md`

## 2. Current Status

- **Overall status:** In progress
- **Current development phase:** Building working catalogue and social product features
- **Current focus:** .NET 9 compatibility implemented on `Net9Compatibility`; Windows Visual Studio 2022 verification outstanding
- **Last verified:** 2026-10-16
- **Current blocker:** Likes, comments, activity feed and external catalogue integration remain incomplete

## 3. Feature Implementation Tracker

| Feature | Status | Evidence / notes |
|---|---|---|
| Authentication | Implemented — verified | Website registration, username/email login, logout, profile editing, cookie persistence and authorization checks pass using isolated migrated databases; see latest work. |
| User profiles | Implemented — verified | `ApplicationUser` stores a display name and bio; the account page can edit them, and `/users/{username}` displays the public profile without exposing account email. The migration and persistence round-trip are tested. |
| Music catalogue | In progress | `Artist`, `Album`, and `Track` models exist with validation and EF relationship wiring in `ApplicationDbContext.cs`. Local `CatalogueService`, catalogue search UI and track details are implemented and regression verified; external metadata integration remains outstanding. |
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
| Automated tests | Passed — verified | 177 NUnit tests passed on .NET 9, 0 failed/skipped (2026-10-16), including all 175 original cases and two direct-route 404 regressions. |
| Git and repository hygiene | Verified | `Net9Compatibility` starts at fetched `origin/main` (`ac92cbe`); compatibility changes reviewed for the authorized commit and PR. Existing database unchanged. |

## 4. Latest Completed Work

### 2026-10-16 — .NET 9 compatibility

- Created `Net9Compatibility` from freshly fetched `origin/main` (`ac92cbe056983dba7ba8226455d9b2b71c964018`). Initial tracked/untracked checkout was clean; ignored local files preserved. No commits, pushes or merges.
- **.NET 10 baseline:** SDK 10.0.401; `dotnet build SoundBite/SoundBite.csproj -c Release` succeeded, 0 warnings/errors. `dotnet test SoundBite.Tests/SoundBite.Tests.csproj -c Release` passed 175/175, 0 failed/skipped. Logs and TRX are in `/tmp/soundbite-net9-verification`.
- Installed official .NET SDK 9.0.319 alongside 10.0.401 without removing .NET 10. `global.json` pins 9.0.319 exactly (`rollForward: disable`, previews off). Both projects target `net9.0`; removed test `LangVersion=latest` to use C# 13. Four Microsoft package references are pinned to 9.0.21; existing NUnit/test SDK/coverage versions retained. Added `SoundBite.sln` and `.config/dotnet-tools.json` (EF CLI 9.0.21).
- Blazor adaptations: replaced .NET 10 `ResourcePreloader`, framework script asset lookup, `Router.NotFoundPage`, status-code scope overload and SSR redirect opt-out property with .NET 9 equivalents. Retained `MapStaticAssets`, `ImportMap`, app asset fingerprinting and static SSR account forms. Reconnect UI observes .NET 9 CSS class changes and supports retry/reload; unsupported .NET 10 pause/resume hooks were replaced by reload for lost circuits. Existing domain/UI features, models, services and migration history were preserved.
- Compatibility issue found and fixed: short `<Pages.NotFound />` markup was not resolved as a Razor component; fully qualified the component and reran validation. Two pre-existing test methods emit CS1998 under a full .NET 9 rebuild (`PlaylistServiceTests:87`, `ReviewServiceTests:88`); left unrelated tests unchanged.
- **Final automated validation:** `dotnet build SoundBite.sln -c Release --no-restore` passed (incremental: 0 warnings/errors). Explicit `-t:Rebuild` passed with the two existing test warnings and 0 errors. `dotnet test SoundBite.sln -c Release --no-build` passed **177/177**, 0 failed/skipped. All 175 existing tests retained; two added HTTP cases assert unknown routes return 404 and the application Not Found content. `dotnet publish SoundBite/SoundBite.csproj -c Release --no-restore -o /tmp/soundbite-net9-verification/publish` passed.
- **EF Core 9 verification:** full suite exercises fresh SQLite migration, upgrade from `AddPlaylists` with existing users/tracks/playlists, rollback/re-upgrade, constraints, CRUD and model/snapshot consistency. EF CLI `migrations has-pending-model-changes` reports no changes; `database update --connection 'Data Source=/tmp/soundbite-net9-verification/cli.db'` applies all historical migrations successfully. Browser seed harness also migrates a fresh disposable database and reports no pending model changes. No historical migrations or snapshot were rewritten.
- **Published-app browser validation:** Production environment on localhost, using only `/tmp/soundbite-net9-verification/browser.db`. Passed registration/auto-login, cookie-backed profile save and refresh, email login/logout, interactive catalogue search, track details/review submission, playlist creation/add-track persistence, owner versus anonymous/other-user controls, follow/unfollow persistence and follower lists. Pagination verified with 12 disposable followers over 10/2-item pages, disabled boundaries and Previous navigation. Missing URL renders Not Found and returns HTTP 404. All six referenced local CSS/JS/image assets return HTTP 200. Reconnection after server restart displays the modal and reloads a lost circuit; counter interaction works afterward. Suspending/resuming the test server also verified transient-disconnect recovery with counter state preserved (3, then incremented to 4). Intentional outages produce expected network console errors. HTTP-only local launch logs the expected missing HTTPS-port warning; HTTPS cookie/antiforgery behavior is covered by integration tests.
- **Protected state:** `SoundBite/soundbite.db` SHA-256 remains `ead1d21ea950e99cd6548f23d0b83159ac69c24b2bd4cbbf2dcac8376159eeaf`. Only disposable SQLite databases were migrated or written during verification. No data/model/service/migration source changes.
- **Changed files:** `SoundBite/SoundBite.csproj`, `SoundBite.Tests/SoundBite.Tests.csproj`, `SoundBite.Tests/AuthenticationUiTests.cs`, `SoundBite/Components/App.razor`, `SoundBite/Components/Routes.razor`, all three `ReconnectModal.razor`/`.css`/`.js` files, `SoundBite/Program.cs`, `SoundBite.sln`, `global.json`, `.config/dotnet-tools.json`, `SOUNDBITE_CONTEXT.md`, this progress file.
- **Outstanding:** Ready for Windows Visual Studio 2022 17.14 testing, not yet Windows-verified. Install exact SDK 9.0.319 and ASP.NET workload; open solution, restore/build, run 177 tests in Test Explorer, F5 and publish against a disposable database. .NET 9 support ends 10 November 2026. Existing application DB remains unmigrated. Exhausted-retry/manual Retry/Reload browser checks remain outstanding. Commit, branch push and PR creation authorized on 2026-10-17; do not merge.

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
| Application build | Passed | Release solution rebuild passed on SDK 9.0.319 with 0 errors and 2 existing test warnings; Release publish passed. |
| Automated tests | Passed — verified | 177 NUnit tests passed on .NET 9, 0 failed/skipped (2026-10-16), including all 175 original cases and two direct-route 404 regressions. |
| Application startup | Passed | Published .NET 9 output launched in Production on localhost:5199/5200 with an explicit disposable SQLite connection. |
| Relevant feature behaviour | Passed | Website authentication, profile editing, refresh/navigation and authenticated/anonymous review/playlist access verified; see latest work. |
| Database migrations | Passed | EF 9 fresh migration, data-preserving upgrade, rollback/re-upgrade, constraints and model/snapshot checks passed on disposable SQLite databases; historical files untouched. |

## 9. Git Status

- **Active branch:** `Net9Compatibility`, created from freshly fetched `origin/main` at `ac92cbe056983dba7ba8226455d9b2b71c964018`.
- **Initial checkout:** Clean on `FollowUnfollow`; no unrelated changes, stashing or cleanup required. Ignored files were preserved.
- **Current work:** Compatibility changes reviewed for the authorized commit, branch push and PR on 2026-10-17. Commit identity and publication status are recorded in Git/GitHub. Merge is not authorized.
- **Authorized commit title:** `Add .NET 9 and Visual Studio 2022 compatibility`.
- **Suggested description:** Retarget app/tests and pin SDK/EF tooling, adapt Blazor routing/assets/reconnection, preserve migrations and features, add 404 regressions and record validation.

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

- Commit, branch push and PR creation are authorized for `Net9Compatibility`; do not merge. Windows VS2022 and exhausted-retry/manual Retry/Reload checks remain outstanding.
- Open `SoundBite.sln` on Windows in Visual Studio 2022 17.14 with SDK 9.0.319 and the ASP.NET workload. Restore/build, run all 177 tests in Test Explorer, launch with F5 and verify publish. These Windows checks have not run on this macOS host.
- Use a disposable SQLite connection override for Windows testing. The existing application database remains unchanged and unmigrated; database deployment is a separately authorized step.
- .NET 9 support ends 10 November 2026, after the submission deadline of 19 October 2026 at 11:59 pm Sydney time.
- Likes/comments/feed/Spotify integration and profile activity remain independent future work.

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
