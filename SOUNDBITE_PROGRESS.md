# SoundBite — Development Progress

> **Purpose:** This is the living record of SoundBite's actual implementation status. Read it alongside `SOUNDBITE_CONTEXT.md` before planning or implementing work. Update it after every meaningful coding, testing, debugging, or planning session.
>
> **Accuracy rule:** Distinguish verified implementation from assumptions and planned work. Do not claim that code works, tests pass, or changes have been pushed unless that has been verified.

## 1. Current Project Snapshot

- **Project:** SoundBite
- **Application:** Blazor Web App
- **Interactivity:** Interactive Server
- **Language:** C#
- **Target framework:** .NET 9; SDK 9.0.319, Microsoft/EF packages 9.0.21
- **Database:** SQLite
- **ORM:** Entity Framework Core
- **Authentication:** ASP.NET Core Identity
- **Testing framework:** NUnit
- **Version control:** Git and GitHub
- **Main context file:** `SOUNDBITE_CONTEXT.md`

## 2. Current Status

- **Overall status:** In progress
- **Current development phase:** Presentation and deployment readiness
- **Current focus:** Complete deployment of the committed Spotify artist-album pagination fix, then retry artist import
- **Last verified:** 2026-10-18
- **Current blocker:** Production artist imports still fail on the currently deployed code because Spotify rejects `limit=50` on the artist-albums endpoint. The correction is committed as `55f9a20`, but deployment stopped before tests/publish because this non-interactive terminal cannot provide the LaunchDaemon's sudo password. Run the deployment script in a local interactive terminal, then retry. Failed attempts did not change catalogue counts.

## 3. Feature Implementation Tracker

| Feature | Status | Evidence / notes |
|---|---|---|
| Authentication | Implemented — verified | Website registration, username/email login, logout, profile editing, cookie persistence and authorization checks pass using isolated migrated databases; see latest work. |
| User profiles | Implemented — verified | `ApplicationUser` stores a display name and bio; the account page can edit them, and `/users/{username}` displays the public profile without exposing account email. The migration and persistence round-trip are tested. |
| Music catalogue | Seeded in production; artist import correction committed, deployment pending | `/search` searches local artists, albums, and tracks, then searches Spotify artists on a local miss. The deployed code fails at Spotify's artist-albums request; committed fix uses live-verified page size 10. Failed attempts left 17 artists, 38 albums, and 50 tracks unchanged. |
| Spotify integration | Production seed verified; interactive import fix awaits deployment | The deployed credentials completed five default searches, refreshing 50 unique tracks and creating 26 sample reviews across 12 tracks. A live API probe showed artist-album requests accept limit 10 and reject 20 and 50. |
| Track ratings and reviews | Implemented — verified | Reviews persist through EF Core; the service supports create/read/update, the database enforces one review per user per track and valid 1–5 ratings, and track details support public review reading plus authenticated submission/editing. |
| Album-track many-to-many relationship | Implemented — unverified | `ApplicationDbContext` maps `ArtistAlbums` and `ArtistTracks` and `Track.AlbumId` with a nullable optional foreign key. This matches the intended design, but the schema has not been validated via a migration review beyond the code. |
| Playlist management | Implemented — verified | Users can create, edit, and delete playlists; add/remove tracks; and browse public playlist listings/details. Owner checks are enforced in the service. |
| Playlist duplicate prevention | Implemented — verified | Service validation and a unique database index on playlist and track prevent duplicates. Playlist item positions preserve order. |
| Following and followers | Implemented — verified | Authenticated idempotent follow/unfollow, public counts and expandable 10-user pages; database constraints, concurrent requests, account deletion and browser flows verified. Merged into `main` at `ac92cbe`. |
| Likes and comments | Implemented — verified | Signed-in users can idempotently like reviews, tracks, public playlists, and comments; private playlist likes are owner-only. Users can add/edit/delete their own review comments and reply one level deep to top-level comments. Replies are grouped under parents, cannot be nested further, and are preserved as top-level comments if their parent is deleted. |
| Activity feed | Implemented — verified | `/feed` globally orders and paginates reviews, comments/replies, and likes by followed users and the signed-in user. Reply entries identify the parent author. Like activity covers reviews, comments, tracks, and public playlists; private-playlist likes are excluded. |
| Notifications | Implemented — verified | In addition to recipient-scoped notifications, `/notifications` uses keyset paging to load older items without duplicates; unread counts and read-state behavior remain independent of the displayed page. Users can persist per-category opt-outs from `/notifications/settings`, affecting future notifications only. Paging, authorization, read state, preference persistence/isolation, and UI behavior are covered by tests. The single-instance event dispatcher updates active recipient circuits; navigation and reconnection reload persisted state. |
| Rankings and trends | Implemented — verified in isolated SQLite tests | `/rankings` shows top-rated tracks (minimum two reviews), most-reviewed tracks overall, and tracks with the most reviews in the last seven days. Separately stored, labelled sample reviews can populate these lists without masquerading as community reviews. |
| Navigation and home | Implemented — tested | Navigation order is Home, Search, Activity Feed, Ratings and Trends, Playlists, Account. Account opens a dropdown with Edit Profile, Notifications, and role-gated Admin. Home shows a personalized welcome to signed-in users. |
| Administrative user management | Implemented — verified | `/admin/users` supports user search/paging, account lock/unlock, and admin-role grants/revocation. Every service operation checks live role membership and lockout state server-side. The front-end label is “Admin.” |
| Automated tests | Passed — verified | Full NUnit suite passed **268/268** on 2026-10-18; the focused catalogue, Spotify, rankings, track-detail, and route suite passed **46/46**. EF reports no pending model changes. |
| Git and repository hygiene | In progress | Fix commit `55f9a20` is synchronized with `origin/main`; this deployment-status update is the only uncommitted change. The new code commit is not yet deployed. |

## 4. Latest Completed Work

### 2026-10-17 — B: local release-readiness checks (partial; environment-dependent checks outstanding)

- **Build and tests:** With SDK 9.0.319, `dotnet build SoundBite.sln --configuration Release --no-restore` passed with 0 warnings/errors. `dotnet test SoundBite.sln --configuration Release --no-build` passed **263/263**, 0 failed/skipped.
- **Publish:** `dotnet publish SoundBite/SoundBite.csproj --configuration Release --no-build --output /tmp/soundbite-b-readiness-publish` succeeded.
- **Startup smoke test:** Started the app with `ConnectionStrings__DefaultConnection` pointing to `/tmp/soundbite-b-readiness.db`, a temporary copy of the tracked database. The home page returned HTTP 200 and the catalogue search page rendered; a local search interaction returned its no-results status without errors. The application database was not used as the runtime target.
- **Local stylesheet/User Secrets setup:** The app was initially started with `--no-launch-profile` but without `ASPNETCORE_ENVIRONMENT=Development`, causing styles not to load and preventing the expected local development configuration. Restarting with `ASPNETCORE_ENVIRONMENT=Development` fixed the styles, as confirmed by the user. Continue to point the connection string at the disposable database copy.
- **Reconnect check:** In the automated browser check, stopping the app showed the .NET 9 rejoining state; after restart against the same temporary copy, the dialog closed and the page remained interactive. In the user's manual check, restarting at the same URL allowed the page to continue without the reconnect dialog appearing, consistent with recovery before the UI reaches the failed-reconnect state. The explicit Retry/Reload buttons were not reached, so their behavior remains unverified.
- **Spotify:** Confirmed both `Spotify:ClientId` and `Spotify:ClientSecret` exist in local User Secrets; values were redacted during verification. User ran the live client-credentials token request and track search successfully (both returned HTTP 200), imported a selected track, and found it through local catalogue search. This was tested with the temporary database copy and Development environment. Credentials remain local and were not printed or committed.
- **Spotify options:** `SpotifyOptions` uses empty-string defaults and `Program.cs` binds the `Spotify` section; credentials are supplied through User Secrets locally or `Spotify__ClientId`/`Spotify__ClientSecret` environment variables in deployment. A local hard-coded Client ID and malformed property edit were removed; the file now matches the committed version. Focused `SpotifyCatalogServiceTests` passed 2/2.
- **Migration safety:** Restored the repository-local EF tool 9.0.21 and ran `dotnet ef migrations list` against the temporary database copy. `20261017064453_AddNotificationOptOuts` is pending there; `dotnet ef migrations has-pending-model-changes` reports no model changes. No migration was applied to the copy or application database. Do not migrate any deployment target without its owner's explicit authorization and a verified backup.
- **Deployment direction:** User chose an always-on macOS host with Windows viewers, using `~/Library/Application Support/SoundBite/data/soundbite.db`. Separate `app`, `data`, `backups`, and `logs` directories exist under Application Support; the Production publish is in `app`. Keep the backend loopback-only and router port forwarding disabled.
- **Deployment database and backups:** Created the fresh production DB and applied all 12 migrations. `PRAGMA integrity_check` returned `ok`. User registered the intended first administrator; configured `Admin__UserIds__0` with the account ID and verified its `Administrator` membership in SQLite. The empty pre-account baseline and post-admin/pre-tailnet backup are independently integrity-checked. DB/backup files use mode 600, private directories mode 700, all outside Git.
- **Tailscale Serve:** Mac `mair` (`100.97.51.90`) serves `https://mair.tailae22d2.ts.net/` through HTTPS port 443 to `http://127.0.0.1:5081`. `tailscale serve status`, curl with TLS verification (HTTP 200), and integrated-browser rendering verified the tailnet endpoint. Windows viewers need Tailscale installed and authorized in the same tailnet. No router ports are exposed.
- **Reverse proxy/configuration support:** `Program.cs` trusts `X-Forwarded-Proto` only from IPv4 loopback, before HTTPS redirection. It also loads the external JSON file named by `SOUNDBITE_CONFIG_FILE`, with environment and command-line settings retaining higher precedence. Release build passed and the full suite passed **263/263**. Production publish was smoke-tested on a disposable DB; live Tailscale HTTPS returned 200.
- **Current LaunchAgent:** Per-user `~/Library/LaunchAgents/com.staticpen.soundbite.plist` (mode 600) is active and local/tailnet requests return 200. It starts at account login but stops on logout, so it does not meet the user's newly confirmed always-on requirement.
- **Production Spotify configuration:** Existing local User Secrets were copied without displaying values to `~/Library/Application Support/SoundBite/config/spotify-production.json` (file mode 600; config directory mode 700). The app does not load this file until the updated `Program.cs` is deployed and the LaunchDaemon supplies its path. No credentials are in Git or the plist.
- **Prepared system service:** `scripts/install-self-hosted-macos-daemon.sh` creates a LaunchDaemon running as the current account before login, migrates the user LaunchAgent plist to a rollback copy, and requests local administrator authorization to install under `/Library/LaunchDaemons`. It has not been run; current worktree changes must first be committed to `main`, deployed, then the installer can be run. Manual reboot verification remains afterward.
- **Not verified here:** LaunchDaemon installation/startup, Spotify API through the Production configuration, launch after reboot, Windows viewer access; Windows Visual Studio and explicit Retry/Reload are waived/optional.
- **Git:** `Program.cs` and this progress record are modified; three deployment/helper scripts are added. The DB, backups, and Spotify configuration remain outside the repository. The local non-secret `UserSecretsId` project setting remains unchanged.

### 2026-10-18 — Self-hosted deployment script

- **What:** Added `scripts/deploy-self-hosted-macos.sh` for one-command release deployment. It requires a clean committed `main` worktree, runs Release tests, publishes to a temporary folder, retains the previous app, restarts the active service, health-checks localhost, and attempts rollback on failure. It never migrates or replaces the database. It supports the existing LaunchAgent and the future system LaunchDaemon (the latter requires sudo authorization).
- **Spotify:** Added `scripts/import-production-spotify-secrets.py` to copy the existing local User Secrets into a protected external JSON file without printing values. Ran it successfully; file mode is 600 and directory mode is 700. `Program.cs` now loads that file when `SOUNDBITE_CONFIG_FILE` is set and reapplies environment/command-line overrides.
- **Always-on service:** Added `scripts/install-self-hosted-macos-daemon.sh`. It produces a LaunchDaemon for the current account and requests administrator authorization locally; it retains the current user-agent plist for rollback. It must be run after the committed Program/config changes are deployed.
- **How to use:** Commit/merge changes to `main`; run `./scripts/deploy-self-hosted-macos.sh`; then run `./scripts/install-self-hosted-macos-daemon.sh` from the repository and approve the local administrator prompt. Future app releases need only the deploy script; it detects the LaunchDaemon and requests administrator authorization to restart it.
- **Validation:** Both shell scripts pass `bash -n`; Python helper passes AST parsing and ran successfully. Full test suite passed **263/263**. `git diff --check` passed after normalizing the `Program.cs` line endings that had been flagged. The deployment script's dirty-worktree guard was exercised; no publish/service swap has been run because code is still uncommitted. LaunchDaemon plist/service and reboot remain unverified until the installer is run.

### 2026-10-18 — User-reported post-restart Safari check

- **What:** The user restarted the Mac, opened the app in Safari, and reported that its functionality worked.
- **Verification scope:** This is user-reported manual verification of the app after restart/login. It does not establish that the LaunchDaemon was installed, that the app starts before login, that the deployed instance uses the latest `main` commit, that Production Spotify credentials are active, or that a Windows viewer can connect.
- **Git:** At review time, `main` and `origin/main` both point to `938f573` (`Added self-hosted deployment`); the worktree is clean.

### 2026-10-18 — Search redesign and Spotify catalogue seeding

- **What:** Renamed the catalogue-search UI to **Search**, made `/search` the primary route while retaining `/catalogue/search` as a compatibility alias, and removed the “Find tracks on Spotify” panel. Search now remains local-database-only.
- **Seeding:** Added an explicit `--seed-catalogue` command that queries Spotify and upserts tracks plus their album/artist IDs, names, and relationships. Its repeatable default is five artist-name searches (The Beatles, Taylor Swift, Miles Davis, Daft Punk, and Beyonce), capped by the existing 10-result Spotify query limit (up to 50 unique tracks). Repeat `--seed-query "term"` to use custom searches. The command requires an absolute SQLite path and does not run migrations, preventing accidental changes to the repository's relative `soundbite.db`.
- **Implementation:** Extracted database upsert behavior into `SpotifyTrackImporter`; the Spotify client now only handles API search. Updated navigation, home and detail links, and catalogue/Spotify tests.
- **Verification:** Focused tests passed 9/9; full `dotnet test SoundBite.sln --configuration Release --nologo` passed **263/263**. The seed command was invoked without a database override and correctly refused the relative repository DB path before any import. No live Spotify calls or database writes were made.
- **Files:** `SoundBite/Program.cs`, `SoundBite/Interfaces/ISpotifyCatalog.cs`, `SoundBite/Services/SpotifyCatalogService.cs`, new `SpotifyTrackImporter.cs` and `SpotifyCatalogueSeeder.cs`, Search/navigation/home/detail Razor components, `CatalogueIntegrationTests.cs`, `SpotifyCatalogServiceTests.cs`, and this progress file.

### 2026-10-17 — A3: notification settings page (implemented, verified, merged in PR #19)

- **What:** New `/notifications/settings` page (`NotificationSettings.razor`) with one native Bootstrap switch per notification category (all six `NotificationType` values), reached from a "Notification settings" link on `/notifications`. Each change saves immediately through the existing A2 `NotificationService.SetTypeEnabledAsync`; settings load through `GetDisabledTypesAsync`. No backend, schema, migration, `Program.cs` or NavMenu changes, no JavaScript or new CSS.
- **Behaviour:** Switches are disabled while a save runs; success shows "Preferences saved."; a failed save shows an error and the switch stays at the last saved value; a failed load offers Try again; signed-out visitors are asked to log in. The user is always the signed-in user from server-side auth state (no user ID parameter). Settings live in `NotificationOptOuts`, so they survive refresh and sign-out/sign-in. They apply to future notifications only.
- **Render mode:** `App.razor` makes routed pages interactive unless they carry `ExcludeFromInteractiveRouting` (only `Account` and `Register` do). The new page does not, and a test asserts that.
- **Files:** `Components/Pages/NotificationSettings.razor` (new), `Components/Pages/Notifications.razor` (one link), `SoundBite.Tests/NotificationSettingsPageTests.cs` (new, 6 tests), `SoundBite.Tests/PageRouteTests.cs` (one route case).
- **Review fixes (Codex A3 review):** (1) Failed-save rollback: the switch's local state is updated and rendered before the save is awaited; if saving fails the previous state is restored and rendered again, so the switch always matches the last saved setting. (2) Retry race: `LoadAsync` sets `loading = true` (and renders) on every load including retries and clears it in `finally`; the switches are not rendered while loading and `SaveAsync` ignores calls while loading or saving. (3) Event-level tests: see below.
- **Tests (10 in `NotificationSettingsPageTests`):** The page tests render the real page against an isolated, migrated in-memory SQLite database and raise the page's real `onchange` / `onclick` events through the renderer (`Renderer.DispatchEventAsync` using handler IDs read from the rendered frames), so the markup's `@onchange` wiring is exercised. They cover: interactive routing, default all-on, only the signed-in user's saved settings shown, switching off/on with reload persistence and another user's opt-outs unchanged, the new state shown while saving with the switches locked, failed-save rollback (including an already-disabled category), retry hiding the switches while loading, a failed retry not stuck loading, and the signed-out visitor. Per-user isolation and creation-point filtering are covered by the A2 service tests. Scratch-copy mutations each made the matching tests fail: no rollback (2), no `loading = true` on retry (1), `@onchange` removed (4), no optimistic update (2). A scoped `#pragma warning disable BL0006` covers the test helper that reads render-tree frames.
- **Verification (SDK 9.0.319 from `~/.dotnet9`):** Release build 0 errors, 3 existing CS1998 warnings. Focused notification and route tests 53/53. Full suite **263/263**. `git diff --check` clean. `SoundBite/soundbite.db` SHA-256 unchanged (`33a52d2d...e53c`). **Limitation:** there is no browser test; the event tests do not run Blazor's JavaScript or a real DOM, so the browser-side checkbox behaviour (the DOM checkbox toggling and being patched back after a failed save) still needs the manual check below.
- **Manual browser check (not yet performed):** run the app against a copy of the database with all migrations applied (never `SoundBite/soundbite.db`). Log in, open `/notifications/settings`, turn "Someone follows you" off and confirm "Preferences saved."; refresh and confirm it is still off. To see a rollback, leave the page open, delete that user's row from the database copy (the service then rejects the save as the account is gone) and toggle a switch: it should flip, then return to its previous position with "Your preference couldn't be saved." Reload failures can be checked the same way: reload the page with the row missing, see "Try again", restore the row, press Try again, and confirm the switches are hidden under "Loading notification settings..." until they appear.
- **Git:** merged to `main` in PR #19 (`4553f1b`).

### 2026-10-17 — A2: notification category preferences (implemented, verified, merged in PR #18)

- **Design:** New `NotificationOptOut` (`UserId`, `Type`; composite key; cascade FK to the user). A row means that category is off; no rows means everything is on, so default delivery is unchanged and no backfill is needed. All six existing categories are optional social notifications (there is no security/system type), so none is marked required.
- **Service:** `NotificationService` gains `GetDisabledTypesAsync`, `SetTypeEnabledAsync` (idempotent, rejects undefined enum values) and internal `IsEnabledAsync`. The check runs at the three creation points (`FollowService`, and the comment and like paths in `SocialInteractionService`) using the recipient's setting; when disabled, the follow/like/comment is still saved but no notification row or live event is created. No UI was added.
- **History:** Future notifications only. Existing notifications, read states and unread counts are untouched; re-enabling does not backfill.
- **Migration:** `20261017064453_AddNotificationOptOuts` (additive; `Down` drops only `NotificationOptOuts`). Generated with dotnet-ef 9.0.21 on SDK 9.0.319 using a disposable connection override. After review, `ProductVersion` is `9.0.21` in the new designer and in the model snapshot (the version of the EF tooling that generated them). Historical migrations are unchanged.
- **Files changed:** `Models/NotificationOptOut.cs` (new), `Data/ApplicationDbContext.cs`, `Services/NotificationService.cs`, `Services/FollowService.cs`, `Services/SocialInteractionService.cs`, migration + designer (new), `Migrations/ApplicationDbContextModelSnapshot.cs`, `SoundBite.Tests/NotificationTests.cs` (10 new tests, including a disabled-comment test and migration rollback/upgrade).
- **Verification (SDK 9.0.319 from `~/.dotnet9`):** focused `NotificationTests` + `NotificationsPageTests` 32/32 passed; full NUnit suite 252/252 passed (baseline 242). `migrations has-pending-model-changes`: no changes. Migration script is the single `CREATE TABLE`. `database update` on a disposable SQLite file applied all 12 migrations; down to `AddNotifications` (11, table dropped) and up again (12, table back) all gave `integrity_check` ok and no `foreign_key_check` rows.
- **Protected state:** `SoundBite/soundbite.db` SHA-256 unchanged (`33a52d2d...e53c`). The migration has **not** been applied to it. Applying it (and the still-pending `ThreadedReviewComments` and `AddNotifications`) needs the DB owner's approval, a backup first, and a trial on a copy.
- **Git:** merged to `main` in PR #18.

### 2026-10-17 — A1: notification paging (implemented, verified, merged in PR #17)

- **Task completed:** Users can load notifications older than the newest 30 via a "Load more" button on `/notifications`. Category preferences and settings UI were completed in A2/A3.
- **Files changed:** `Models/Notification.cs` (`NotificationPage` gains `HasMore`, default false), `Services/NotificationService.cs`, `Components/Pages/Notifications.razor`, `SoundBite.Tests/NotificationTests.cs`, new `SoundBite.Tests/NotificationsPageTests.cs`, and this progress file. No schema, migration, `Program.cs` or application-database changes.
- **Decisions:** Keyset paging (`CreatedAt`, then `Id`) rather than Skip, so a notification arriving between loads cannot duplicate or skip items. `GetPageAsync(CancellationToken)` keeps its signature; older pages use the overload `GetPageAsync(beforeCreatedAt, beforeId, cancellationToken)`, which rejects `beforeId <= 0`. The recipient filter is applied before the cursor and paging. Unread count and mark-read stay independent of the displayed page.
- **Page refresh (after review):** Any refresh (live event, reconnect, mark-read) replaces the list with the newest page and resets paging, so read states and deleted notifications always match the database; users press Load more again. A `loadGeneration` counter is bumped by every refresh, and a Load more request that began earlier discards its result if a refresh has started since. Load more is ignored while a refresh is running, and a successful refresh clears any Load more error.
- **Refresh-recovery fix (after Codex review of da572bf):** a failed live refresh that overlapped an in-flight Load more used to leave `loadingMore` stuck (the abandoned Load more skipped its cleanup, and only a successful refresh cleared it) and hid the list behind the error. `LoadAsync` now clears the Load more state when a refresh starts and re-renders so Load more shows as disabled during a refresh. If a refresh fails after a successful load, the previous list stays visible with a "couldn't be refreshed" banner and a Try again button; a failed first load also shows Try again. Stale Load more/refresh results are still ignored by `loadGeneration`.
- **Tests:** `NotificationTests` has 9 paging/authorization tests (the cross-user cursor test now asserts exact expected IDs). `NotificationsPageTests` renders the real page using the same `StaticHtmlRenderer` + reflection approach as `CatalogueIntegrationTests` (no new framework) and covers: Load more without duplicates, live refresh reset with current read states and deleted/new items, Load more failure then retry, refresh clearing the error, and a stale Load more result being ignored. Added for the fix: a failed refresh overlapping Load more keeps the list and recovers on retry, initial-load failure offers retry, and Load more is disabled during a refresh; the live-refresh test also checks older-page read states after Load more. The three new tests fail against da572bf's page. Removing the generation check or the reset in a scratch copy also made the matching tests fail.
- **Verification:** On this machine only SDK 10.0.302 (no .NET 9 runtime) is installed, so the repo's `global.json` pin blocked builds here; verification used a scratch copy of the working tree with `global.json` removed, built by SDK 10.0.302 for `net9.0` and run with `DOTNET_ROLL_FORWARD=Major`. Release build: 0 warnings, 0 errors. Notification tests: 22/22. Full suite: **242/242 passed**, 0 failed/skipped (225 previous + 17 new). The developer separately ran the pinned .NET 9 build and the earlier 234-test suite successfully; the final 242 total has not been re-run under the pinned SDK. No browser test of the Load more button.
- **Git:** merged to `main` in PR #17; the refresh-recovery fix is included.

### 2026-10-17 — Live-notification main update integrated and verified for PR #16

- Started clean on `Net9Compatibility` at `ad96f2c`; fetched and merged `origin/main` at `07f9aabad86b72f453c61d4adb8324e568f83e29` using a normal merge, without rebase or force-push. Only this progress document conflicted; retained latest feature records and compatibility history. Commit/push authorized after verification; PR merge is not authorized.
- Preserved main's live per-user notification dispatcher, scoped circuit reconnect handler, navigation/page refresh subscriptions and disposal, and follow/social notification publishing. All feature source matches main; `Program.cs` retains every new registration with the .NET 9 status-code overload. SDK/package pins and Blazor fixes remain unchanged. No compatibility failure required source or test changes.
- `dotnet build SoundBite.sln -c Release`: passed, 0 errors, 3 existing CS1998 test warnings. `dotnet test SoundBite.sln -c Release --no-build`: **225/225 passed**, 0 failed/skipped (223 main cases plus 2 compatibility cases). `dotnet publish SoundBite/SoundBite.csproj -c Release --no-build -o /tmp/soundbite-net9-final-integration/publish`: passed.
- EF 9 CLI model consistency check reports no pending model changes. All **11 migrations** applied to `/tmp/soundbite-net9-final-integration/check.db`; SQLite integrity is `ok`, no foreign-key violations. Logs/TRX are in `/tmp/soundbite-net9-final-integration`. Automated persistence tests use disposable databases. No historical migrations were rewritten.
- Database cleanup from earlier work is retained: `SoundBite/soundbite.db` matches latest main exactly (Git blob `8e37c754e5bcaa5fd7a9ab47436389420dbacc7c`) and is absent from the PR diff. Before/after SHA-256 matches; no writes or migrations against the application database in this session.
- No browser tests or live Spotify calls repeated. Outstanding: Windows VS2022 17.14 with SDK 9.0.319, manual exhausted-retry/Retry/Reload validation, and live Spotify validation. Live notification dispatch supports one application process; multi-instance delivery requires shared infrastructure.

### 2026-10-17 — Latest main integrated into PR #16; merge not committed

- Started clean on `Net9Compatibility` at `505af740ca4f80ae4dcb85d456a697b948b85322`; fetched `origin/main` at `e3e1cad33b2c86036738e9a27d6e71e6dc1402f1`. Prepared a normal merge with `--no-commit --no-ff`, without rebasing or force-pushing. HEAD remains unchanged; MERGE_HEAD records the incoming main commit.
- Resolved the sole textual conflict in this progress document by retaining main's feature tracker and all feature records, retaining the compatibility implementation record, and updating current status and verification. `Program.cs` and authentication tests merged automatically.
- Protected database handling: incoming main changes the tracked `SoundBite/soundbite.db`. Ran merge checkout operations in a temporary working directory, restored only that database's index entry from HEAD, and copied merged source files into the real working tree. The actual application database was never overwritten, restored, or migrated; its bytes and staged entry match pre-merge HEAD. This intentionally excludes main's binary database update while preserving all model/migration code.
- Preserved social likes/comments/replies/feed, notifications, rankings/trends, administrator management, artist/album discovery, and Spotify integration. All new service/model/migration source matches main exactly. `Program.cs` retains every main registration, Identity roles and administrator bootstrap; its only difference from main is the .NET 9 status-code overload. Authentication tests retain main's new cases plus the two 404 cases. SDK 9.0.319, Microsoft/EF 9.0.21, solution, tool manifest and Blazor compatibility fixes remain intact.
- Reviewed notification recipient filtering, current-user resolution, read-state ExecuteUpdateAsync, event subscription disposal, and transactional event creation against EF 9. Admin bootstrap remains opt-in via configured IDs. Existing HTTP authentication/authorization and notification tests pass; no tests were removed or weakened and no additional compatibility code workaround was needed.
- `dotnet build SoundBite.sln -c Release`: passed, 0 errors; 3 existing CS1998 warnings in AdminServiceTests:136, PlaylistServiceTests:87 and ReviewServiceTests:88.
- `dotnet test SoundBite.sln -c Release --no-build`: **223 passed, 0 failed, 0 skipped** (main's 221 plus 2 compatibility cases). TRX/logs: `/tmp/soundbite-net9-integration/`.
- `dotnet publish SoundBite/SoundBite.csproj -c Release --no-build -o /tmp/soundbite-net9-integration/publish`: passed.
- EF CLI 9.0.21 `migrations has-pending-model-changes` with disposable connection override: no pending changes. `database update --connection 'Data Source=/tmp/soundbite-net9-integration/ef-check.db'` applied all **11** migrations through `20261017013935_AddNotifications`. SQLite integrity check returned `ok`; foreign-key check returned no violations. The NUnit suite also exercises isolated migration/upgrade/rollback and data-persistence behavior. Historical migration files and the model snapshot are identical to main.
- Protected application database SHA-256 remains `ead1d21ea950e99cd6548f23d0b83159ac69c24b2bd4cbbf2dcac8376159eeaf`. No browser testing or live Spotify calls performed in this integration session.
- Ready for a merge commit after approval. Stop before committing/pushing; PR #16 remains unmerged. Windows VS2022, manual exhausted-retry/Retry/Reload, live Spotify and real-time notification delivery limitations remain as documented.

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

### 2026-10-17 — Live notification updates

- **Task completed:** Added user-targeted live notification change dispatch to active Blazor circuits and refresh recovery on circuit reconnection/navigation.
- **Files or components changed:** `Services/NotificationEventDispatcher.cs`, `Services/NotificationCircuitHandler.cs`, `Services/FollowService.cs`, `Services/SocialInteractionService.cs`, `Services/NotificationService.cs`, `Components/Layout/NavMenu.razor`, `Components/Pages/Notifications.razor`, `Program.cs`, `NotificationTests.cs`, `FollowServiceTests.cs`, `SocialInteractionTests.cs`, `TrackDetailsTests.cs`, `CatalogueIntegrationTests.cs`, and this progress file.
- **Important implementation decisions:** A singleton in-process dispatcher publishes only to subscribers registered for the recipient's authenticated user ID. Components marshal updates to their Blazor dispatcher and dispose subscriptions with their lifetime. A scoped circuit handler re-reads persisted notification state after reconnection; navigation also refreshes the unread count. The existing database remains the source of truth.
- **Verification performed and its result:** Application build passed with 0 warnings/errors. Focused notification/follow/social/authentication tests passed 50/50. Full NUnit suite passed 223/223. EF reports no pending model changes; CRLF-aware `git diff --check` passed.
- **Remaining limitations:** In-process event delivery assumes one application instance; multiple instances would need a distributed backplane. No two-browser manual smoke test was performed. Database migrations `20261017012957_ThreadedReviewComments` and `20261017013935_AddNotifications` have not been applied to the configured application database.

### 2026-10-17 — Persistent in-app notifications

- **Task completed:** Added durable notifications for follows, review comments, comment replies, and likes on reviews, comments, and playlists. Added a private notifications page, unread navigation count, and individual/bulk read actions.
- **Files or components changed:** `Models/Notification.cs`, `Data/ApplicationDbContext.cs`, `Services/NotificationService.cs`, `Services/FollowService.cs`, `Services/SocialInteractionService.cs`, `Components/Pages/Notifications.razor`, `Components/Layout/NavMenu.razor`, `Program.cs`, migration `20261017013935_AddNotifications` (designer and snapshot), `NotificationTests.cs`, `AuthenticationUiTests.cs`, `PageRouteTests.cs`, and this progress file.
- **Important implementation decisions:** Follow and like writes plus their notifications are in one database transaction; only a newly inserted follow/like creates a notification. Comment notifications are saved with their comment. Self-notifications are suppressed. Track likes do not notify because tracks have no user owner; private playlist likes are only possible for the owner and therefore do not generate self-notifications. Mark-read queries are scoped to the authenticated recipient.
- **Verification performed and its result:** Focused notification, authentication UI, and route tests passed 25/25. Full NUnit suite passed 221/221. EF reports no pending model changes after migration generation; CRLF-aware `git diff --check` passed.
- **Remaining limitations:** The migration has been exercised by isolated test databases but not applied to the configured application database. Live delivery is in-process and requires a single app instance.

### 2026-10-17 — Activity feed replies and likes

- **Task completed:** Added reply context and like activities to the paginated feed.
- **Files or components changed:** `Models/ActivityItem.cs`, `Services/FeedService.cs`, `Components/Pages/ActivityFeed.razor`, `FeedServiceTests.cs`, `ActivityFeedUiTests.cs`, and this progress file.
- **Important implementation decisions:** Reviews, comments, and likes are combined before ordering and paging, so likes cannot bypass the feed's page limit. The feed includes review, comment, track, and public-playlist likes. Likes on private playlists are omitted to avoid exposing private activity. Comment replies identify the parent author; liked replies include that context too.
- **Verification performed and its result:** Focused feed tests passed 6/6. Full NUnit suite passed 216/216. Mixed-page tests confirm no duplicate/omitted IDs across pages; private-playlist like visibility is covered.
- **Remaining limitations:** Activity is fetched as a current snapshot. Likes on private playlists intentionally do not appear in the feed.

### 2026-10-17 — Threaded review comments

- **Task completed:** Added one-level replies to review comments, nested display under parent comments on track details, and a database migration.
- **Files or components changed:** `Models/Comment.cs`, `Data/ApplicationDbContext.cs`, `Services/SocialInteractionService.cs`, `Components/Pages/ReviewSocialPanel.razor`, migration `20261017012957_ThreadedReviewComments` (including designer and model snapshot), `SocialInteractionTests.cs`, `TrackDetailsTests.cs`, and this progress file.
- **Important implementation decisions:** Replies can target only top-level comments, keeping threads one level deep. Parent comments can be deleted without losing replies: direct replies are promoted to top-level comments, and the self-reference uses `SET NULL`. Actor identity remains resolved server-side; comments can only be edited/deleted by their owners.
- **Verification performed and its result:** Focused social interaction and track detail tests passed 29/29. Full NUnit suite passed 215/215. `dotnet ef migrations has-pending-model-changes` reported no unscaffolded model changes; CRLF-aware `git diff --check` passed.
- **Remaining limitations:** The migration has only been exercised in isolated test databases; it has not been applied to the configured application database.

### 2026-10-17 — Protected administrator user management

- **Task completed:** Added an admin user directory with search and paging, account lock/unlock, and administrator role management. Added configured administrator bootstrap and hid the admin navigation link from non-admin users.
- **Files or components changed:** `Services/AdminService.cs`, `Services/AdminRoleBootstrapper.cs`, `Program.cs`, `Components/Pages/AdminUsers.razor`, `Components/Layout/NavMenu.razor`, `AdminServiceTests.cs`, `AuthenticationUiTests.cs`, and `PageRouteTests.cs`.
- **Important implementation decisions:** Initial admins are configured using `Admin__UserIds__0` (and additional indexed IDs) after the account exists and database migrations are applied. Every admin service operation verifies live role membership and lockout status from the database rather than relying only on potentially stale role claims. Admins cannot lock or demote themselves, and the last admin cannot be demoted. No user deletion was added.
- **Verification performed and its result:** Focused admin provisioning, service authorization, anonymous-page, and route tests passed 15/15. Full NUnit suite passed 212/212. CRLF-aware `git diff --check` passed.
- **Remaining limitations:** Bootstrap behavior is tested through the bootstrapper but has not been exercised by starting the deployed app with production configuration. No schema change was needed because Identity role tables already exist.

### 2026-10-17 — Review-based rankings and trends

- **Task completed:** Implemented track rankings for highest average rating and all-time review count, plus a seven-day trending list. Added a discoverable `/rankings` page linked from navigation and the home page.
- **Files or components changed:** `Interfaces/IRankingStrategy.cs`, `Strategies/RatingRankingStrategy.cs`, `Strategies/PopularityRankingStrategy.cs`, `Services/RankingService.cs`, `Services/TrendService.cs`, `Models/TrackRankingEntry.cs`, `Models/TrendingTrackEntry.cs`, `Components/Pages/Rankings.razor`, `Program.cs`, `Components/Layout/NavMenu.razor`, `Components/Pages/Home.razor`, `RankingAndTrendServiceTests.cs`, `PageRouteTests.cs`, and `CatalogueIntegrationTests.cs`.
- **Important implementation decisions:** Rankings use local SoundBite reviews only, not Spotify metrics. Top-rated requires at least two reviews to reduce single-review skew; popularity counts reviews; trending counts reviews in the last seven days. Sorting is deterministic and limits/windows are validated. No database changes were needed.
- **Verification performed and its result:** Focused ranking/trend, route, and catalogue tests passed 20/20. Full NUnit suite passed 205/205. CRLF-aware `git diff --check` passed.
- **Remaining limitations:** Rankings cover tracks only because ratings/reviews are track-scoped. No authenticated browser test was performed. Changes are not committed.

### 2026-10-17 — Spotify track search and catalogue import

- **Task completed:** Added a Spotify-backed track search and an import action that adds the selected track plus its album and artists to the local catalogue.
- **Files or components changed:** `Configuration/SpotifyOptions.cs`, `Interfaces/ISpotifyCatalog.cs`, `Models/SpotifyTrackMatch.cs`, `Services/SpotifyAccessTokenProvider.cs`, `Services/SpotifyCatalogService.cs`, `Program.cs`, `Components/Pages/CatalogueSearch.razor`, `SpotifyCatalogServiceTests.cs`, and `CatalogueIntegrationTests.cs`.
- **Important implementation decisions:** Uses Spotify's client-credentials flow with cached access tokens; credentials come from `Spotify__ClientId` and `Spotify__ClientSecret` environment variables, never checked-in configuration. Import is restricted to signed-in users and matches imported records by Spotify ID to avoid duplicates. Spotify results are attributed and link back to Spotify; artwork is not fetched or stored.
- **Verification performed and its result:** Focused Spotify/catalogue/route tests passed 16/16, and the full NUnit suite passed 199/199 after the final changes. Mock HTTP tests verify token use/caching, search mapping, idempotent import, missing-credential errors, and rejection of anonymous imports. CRLF-aware `git diff --check` passed.
- **Remaining limitations:** Live API credentials and current Spotify developer access have not been tested; no artwork is imported, and API terms, branding, and metadata use requirements must continue to be observed. Changes are not committed.

### 2026-10-17 — Artist/album catalogue details and discovery landing page

- **Task completed:** Made artist and album search results navigable, added artist and album detail pages, and replaced the scaffold home page with entry points into catalogue search, playlists, and the activity feed.
- **Files or components changed:** `Components/Pages/CatalogueSearch.razor`, `Components/Pages/ArtistDetails.razor`, `Components/Pages/AlbumDetails.razor`, `Components/Pages/Home.razor`, `CatalogueIntegrationTests.cs`, and `PageRouteTests.cs`.
- **Important implementation decisions:** Reused the existing `IMusicCatalog` and EF-backed artist/album queries; no schema or migration changes were needed. Detail pages handle invalid IDs, missing records, and load failures, and link available related entities and tracks.
- **Verification performed and its result:** Focused catalogue and route tests passed 13/13. Full NUnit suite passed 196/196. `git diff --check` passed after normalizing the Home page line endings.
- **Remaining limitations:** Catalogue content is local only; external music metadata integration, catalogue editing, and broader discovery such as rankings remain future work. Changes are not committed.

### 2026-10-17 — Like support extended to all planned targets

- **Task completed:** Extended likes from reviews to tracks, playlists, and comments, and added reusable like controls to track details, playlist details, and each review comment.
- **Files or components changed:** `Models/Like.cs`, `Data/ApplicationDbContext.cs`, `Services/SocialInteractionService.cs`, `Components/Pages/LikeButton.razor`, `ReviewSocialPanel.razor`, `TrackDetails.razor`, `PlaylistDetails.razor`, migration `20261017005729_ExpandLikeTargets`, model snapshot, and `SocialInteractionTests.cs`.
- **Important implementation decisions:** Likes use nullable foreign keys for each supported target and a database check requiring exactly one target, with filtered unique indexes per target/user. The migration preserves existing review likes. Non-owner actors cannot like private playlists; private playlist counts are also hidden from non-owners. Like actions resolve the actor from server authentication and remain idempotent.
- **Verification performed and its result:** Full suite passed: 192/192. Focused like-target, migration-preservation, and track integration tests passed: 29/29; the 11 focused social persistence tests also passed. EF reports no pending model changes, and the CRLF-aware diff whitespace check passed.
- **Remaining limitations:** Comments and feed entries still attach to reviews; likes themselves are not shown as feed events. Threaded replies remain absent. Configured app database migration and authenticated browser testing were not performed.

### 2026-10-17 — Review interactions and activity feed implemented

- **Task completed:** Added review likes, review comments with owner-only edit/delete, a paginated follow-based feed, track-detail interaction controls, and a feed page.
- **Files or components changed:** `Models/Like.cs`, `Models/Comment.cs`, `Models/ActivityItem.cs`, `Data/ApplicationDbContext.cs`, `Services/SocialInteractionService.cs`, `Services/FeedService.cs`, `Components/Pages/ReviewSocialPanel.razor`, `Components/Pages/ActivityFeed.razor`, `TrackDetails.razor`, `NavMenu.razor`, `Program.cs`, migration `20261017002358_AddReviewSocialInteractions`, and social/feed/UI tests.
- **Important implementation decisions:** Like/comment support is initially attached to reviews; like mutations resolve the actor from server authentication and are idempotent under concurrency. Comments are flat (no replies yet), limited to 2,000 non-blank characters, and only their author can edit/delete them. The feed queries the signed-in user and followed users' reviews/comments directly rather than duplicating them in a separate event table.
- **Verification performed and its result:** Full NUnit suite passed: 187/187. Focused social/feed, rendered UI, track detail, and route tests passed: 31/31. `dotnet ef migrations has-pending-model-changes` reported no model changes after migration generation.
- **Remaining limitations:** Broader like targets/replies are not implemented. The migration was exercised by tests against isolated SQLite databases; it was not applied to the configured application database. Browser smoke test confirmed the anonymous `/feed` prompt/navigation; authenticated like/comment interaction was not manually exercised.

### 2026-10-17 — Main branch status check

- Confirmed `main` is clean and synchronized with `origin/main` at `ac92cbe`, after merge of follow/unfollow.
- Re-ran the complete NUnit suite: 175 passed, 0 failed, 0 skipped.
- Compared tracker with source: basic persisted catalogue search and track details exist; artist/album detail navigation and Spotify integration do not. Corrected the stale catalogue status below.

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

### 2026-10-18 — Artist catalogue import, demonstration rankings, and presentation UX

- **Task completed:** Expanded `/search` so a local miss searches Spotify artists and offers full artist-discography import to signed-in users. Added a local Artists list, separately stored sample reviews for the ranking lists, the requested Account dropdown/navigation order, a personalized home welcome, and the “Admin” label.
- **Files or components changed:** `CatalogueSearch.razor`, `SpotifyCatalogService.cs`, `SpotifyTrackImporter.cs`, `SpotifyArtistCatalogService.cs`, `ISpotifyCatalog.cs`, `SpotifyTrackMatch.cs`, `DemoReview.cs`, `DemoReviewService.cs`, `DemoReviewSeeder.cs`, `ApplicationDbContext.cs`, migration `20261018035010_AddDemoReviews` and its designer/model snapshot, `RankingService.cs`, `TrendService.cs`, `TrackRankingEntry.cs`, `TrendingTrackEntry.cs`, `Rankings.razor`, `TrackDetails.razor`, `NavMenu.razor` and its CSS, `Home.razor`, `AdminUsers.razor`, `Program.cs`, relevant catalogue/Spotify/ranking/track tests, and this file.
- **Important implementation decisions:** Artist imports are server-authorized for signed-in users, transactional, and idempotent; they include album releases and associated tracks, not only a selected track. Spotify pagination URLs are restricted to HTTPS `api.spotify.com`. Demonstration reviews remain separate from community reviews and are visibly labelled on rankings and track details. `--seed-catalogue` seeds catalogue records and the sample rankings but does not apply migrations.
- **Verification performed and its result:** Focused catalogue, Spotify, rankings, track-detail, and route tests passed **46/46**; the full NUnit suite passed **268/268**. `dotnet ef migrations has-pending-model-changes` reports no pending model changes. Tests exercise the new migration in isolated SQLite databases, cover anonymous import rejection, repeatable demo-review seeding, and personalized home greeting, and do not migrate the target database.
- **Remaining limitations at implementation checkpoint:** Live Spotify, production migration, and seeding were still outstanding; those steps were subsequently completed as recorded below after the user authorized production database changes.

### 2026-10-18 — Production migration and presentation seed

- **What:** After the user confirmed deployment and explicitly authorized database changes, verified commit `41f90cabf56e90cccd1e6122875305b0a87bd199` was clean and synchronized with `origin/main`. Confirmed the production app was live, the SQLite database passed `PRAGMA quick_check`, and only `20261018035010_AddDemoReviews` was pending.
- **Backup and migration:** Created `/Users/aaronurayan/Library/Application Support/SoundBite/backups/soundbite-pre-demo-reviews-20261018-150136.db` using SQLite's online backup API. Backup integrity passed and mode is `0600`. Applied `AddDemoReviews` to the production database; no earlier migrations were pending.
- **Seed and verification:** The deployed app used the protected LaunchDaemon Spotify configuration. Five Spotify queries refreshed/imported 50 unique tracks; demo seeding created 26 sample reviews on 12 tracks. Post-seed SQLite integrity passed, catalogue counts are 17 artists / 38 albums / 50 tracks, and all 26 demo seed keys are unique. Local `/`, `/search`, and `/rankings` and the deployed HTTPS tailnet versions of those pages returned HTTP 200; the pages contained the expected Artists and ranking sections.
- **Still to verify:** Manually test a signed-in Spotify artist add, anonymous account prompt, Account dropdown/Admin role display, and access from a Windows tailnet viewer. No credentials were displayed or changed.

### 2026-10-18 — Diagnosed Spotify artist-import failure

- **Reported symptom:** User was signed in as administrator but saw “The artist catalogue could not be added. Please try again.”
- **Diagnosis:** Production logs show artist search returned HTTP 200, then `GET /v1/artists/{id}/albums?include_groups=album&limit=50` returned HTTP 400. A read-only API probe using the protected production credentials returned `Invalid limit` for limits 50 and 20, and HTTP 200 for limit 10. Authentication and local persistence were not reached; failed attempts left catalogue counts unchanged at 17 artists / 38 albums / 50 tracks.
- **Fix prepared:** `SpotifyCatalogService` now requests 10 albums per page. The mock test follows Spotify's `offset=10` continuation and asserts each artist-album page uses `limit=10`.
- **Verification:** A read-only live API request fetched all 11 album releases for the failing artist across two pages (10 + 1). Focused Spotify tests passed 4/4 and the full suite passed 268/268. The fix was committed as `55f9a20`; deployment was attempted but the script could not obtain interactive sudo authorization in the agent terminal. It must be run in a local interactive terminal before the user retries. No production DB migration or data changes were required.

## 5. Current Implementation Details

- The application entry point in `SoundBite/Program.cs` configures a Blazor Web App with `AddRazorComponents().AddInteractiveServerComponents()`, SQLite via `UseSqlite(...)`, ASP.NET Core Identity, and the ranking/trend services and strategies.
- `SoundBite/Data/ApplicationDbContext.cs` defines the main EF entities: `Artists`, `Albums`, and `Tracks`, and uses many-to-many joins to model `ArtistAlbums` and `ArtistTracks`.
- `Services/CatalogueService.cs` implements local database search/reads; `/search` is the primary search route and `/catalogue/search` remains an alias. When local search has no results, Spotify artist matches can be imported with their album releases and tracks by any signed-in user. `/tracks/{id}`, `/artists/{id}`, and `/albums/{id}` show local catalogue details.
- `Track` has an optional `AlbumId`/`Album` relationship and uses validation attributes for required `Title` and optional non-whitespace `SpotifyId`.
- `Album`, `Artist`, and `Track` models initialize empty navigation collections and enforce basic validation requirements.
- `Endpoints/AccountEndpoints.cs` exposes registration, login, logout, and current-user endpoints.
- `Data/ApplicationUser.cs` includes `DisplayName` and optional `Bio`. The account page edits these fields; `/users/{username}` displays a public profile without exposing email.
- Remaining model and service placeholders include `UserService`. Rankings combine community reviews with clearly labelled `DemoReviews`; Spotify access requires configured credentials. Likes support reviews, tracks, playlists, and comments; comments/feed entries remain review-scoped.
- The NUnit test project is active; the full suite passed 268/268 for the current worktree.
- `20261018035010_AddDemoReviews` adds the separate sample-review table and has been applied to the configured deployment database after an integrity-checked backup.

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
| Multi-instance notification delivery | Notifications are not backed by a distributed event bus | Live delivery uses an in-process dispatcher by design for the current single-instance app | Add a distributed backplane only if deploying multiple instances | Open |
| Production Spotify artist import currently fails | Artist albums API rejects `limit=50` with HTTP 400 `Invalid limit` | Production log stack traces and live API probe; limit 10 succeeds. Fix commit `55f9a20` passes all 268 tests; deployment script requires interactive sudo | Run `./scripts/deploy-self-hosted-macos.sh` in the VS Code terminal, enter local macOS password, then retry artist import | In progress |
| Windows viewer access not recently confirmed | Tailnet pages work from the host but not yet verified on the viewer | Prior deployment records require Windows Tailscale authorization | Have the Windows viewer join the tailnet and smoke-test the deployed link | Open |
| Initial administrator needs deployment configuration | The first admin must exist before the app can assign its admin role | Startup reads indexed `Admin:UserIds` settings and fails explicitly if an ID is unknown | Register the first user, configure the ID, apply migrations, then restart; keep settings in trusted environment configuration | Open |

## 8. Verification Record

| Check | Result | Notes |
|---|---|---|
| Application build | Passed | Release build completed as part of the 2026-10-18 full test run; three pre-existing `CS1998` warnings remain in test files. |
| Automated tests | Passed — verified | Full .NET 9 Release suite: 268 passed, 0 failed/skipped. Focused catalogue, Spotify, rankings, track detail, and route suite passed 46/46. |
| Application publish | Passed | `dotnet publish SoundBite/SoundBite.csproj --configuration Release --no-build --output /tmp/soundbite-b-readiness-publish` succeeded. |
| Application startup and local smoke test | Passed | App ran against a disposable copy at `/tmp/soundbite-b-readiness.db`; `/` returned HTTP 200, catalogue search rendered and accepted a local-search interaction. No target database was used. |
| Circuit reconnect | Partially verified | A running page survived a local server stop/restart: the rejoining dialog appeared and dismissed, and page interaction worked afterward. Manual Retry and Reload buttons were not reached. |
| Relevant feature behaviour | Passed | Authentication/profile/review/playlist/follow flows were previously verified; rendered tests cover review interactions, feed, and track likes. |
| Database migrations | Applied and integrity-checked | `AddDemoReviews` is recorded in the production migration history; `PRAGMA quick_check` returned `ok`. A protected pre-migration backup is retained under Application Support. |

## 9. Git Status

- **Branch:** `main` at `55f9a20` (`Fix Spotify artist album pagination limit`), synchronized with `origin/main`. Spotify correction/test are committed; this deployment-status progress update is local.
- **Database:** `AddDemoReviews` is applied to the deployment database after a mode-600 integrity-checked backup at `~/Library/Application Support/SoundBite/backups/soundbite-pre-demo-reviews-20261018-150136.db`.
- **PRs #17–#19:** Notification paging, category preferences, and settings UI are merged into `main`.

## 10. Next Recommended Tasks

1. **Task B:** Verify and prepare a deployable app.
   - **Done locally:** Release build, full NUnit suite, publish to `~/Library/Application Support/SoundBite/app`, production-mode smoke test, and automatic reconnect recovery. Created the selected production DB, applied all 12 migrations, checked SQLite integrity, and made a verified pre-account baseline backup.
   - **Done:** Release build and 268 tests, Production publish/deployment at `41f90ca`, production migration and seed, verified integrity and backups, first account/Admin role, Tailscale HTTPS Serve, active LaunchDaemon, and protected external Production Spotify config file.
   - **Still required:** Commit and deploy the artist-album `limit=10` correction, then re-test the signed-in artist import. Verify from a Windows viewer on Tailscale. Windows VS2022 and explicit Retry/Reload checks are waived/optional per user.
   - **Safety:** Never use the tracked `SoundBite/soundbite.db` or `/tmp/soundbite-spotify-test.db` for deployment. Keep Spotify credentials and DB/backup files outside Git. Keep router forwarding disabled.
2. **Complete presentation acceptance checks:** Test the deployed `/search` Spotify fallback and signed-in artist import, verify its albums/tracks are added, check the anonymous account prompt, and confirm the Account menu/role-specific Admin link. Inspect `/rankings` to ensure sample review labels are clear.
3. **Task C (as needed):** Consider album/artist discovery, broader catalogue management, or shared notification delivery only if deploying multiple app instances; these are not blockers for the current single-instance application.

## 11. Handover for the Next Chat Session

- A is complete: paging, category opt-outs, and the settings page are implemented and merged in PRs #17–#19. Focused notification and route tests passed 53/53 on 2026-10-17; the A3 record reports the full suite passed 263/263.
- User confirmed code commit `41f90ca` was deployed. Following explicit authorization, backed up the production DB, applied `AddDemoReviews`, and seeded 50 tracks plus 26 labelled demo reviews. Artist import then exposed Spotify's 10-album page-size cap; signed-in admin auth was not the cause. The correction is commit `55f9a20` and all 268 tests pass. Deployment was attempted but failed before stopping/replacing the app because the non-interactive terminal could not prompt for sudo. Run the deploy script interactively and retry only after its health check succeeds. Windows Tailscale viewer access remains outstanding.
- Notification event delivery is single-instance; shared infrastructure is only needed if deploying multiple instances.

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
