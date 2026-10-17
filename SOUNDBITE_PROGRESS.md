# SoundBite — Development Progress

> **Purpose:** This is the living record of SoundBite's actual implementation status. Read it alongside `SOUNDBITE_CONTEXT.md` before planning or implementing work. Update it after every meaningful coding, testing, debugging, or planning session.
>
> **Accuracy rule:** Distinguish verified implementation from assumptions and planned work. Do not claim that code works, tests pass, or changes have been pushed unless that has been verified.

## 1. Current Project Snapshot

- **Project:** SoundBite
- **Application:** Blazor Web App
- **Interactivity:** Interactive Server
- **Language:** C#
- **Target framework:** .NET 10 (verified in `SoundBite/SoundBite.csproj`)
- **Database:** SQLite
- **ORM:** Entity Framework Core
- **Authentication:** ASP.NET Core Identity
- **Testing framework:** NUnit
- **Version control:** Git and GitHub
- **Main context file:** `SOUNDBITE_CONTEXT.md`

## 2. Current Status

- **Overall status:** In progress
- **Current development phase:** Building working catalogue and social product features
- **Current focus:** Completing the social experience with live notification updates
- **Last verified:** 2026-10-17
- **Current blocker:** Live Spotify API access is unverified until valid application credentials are configured. The threaded-comment migration has not been applied to the configured application database.

## 3. Feature Implementation Tracker

| Feature | Status | Evidence / notes |
|---|---|---|
| Authentication | Implemented — verified | Website registration, username/email login, logout, profile editing, cookie persistence and authorization checks pass using isolated migrated databases; see latest work. |
| User profiles | Implemented — verified | `ApplicationUser` stores a display name and bio; the account page can edit them, and `/users/{username}` displays the public profile without exposing account email. The migration and persistence round-trip are tested. |
| Music catalogue | In progress — discovery and import workflow verified | Local search links to track, artist, and album details. The catalogue search page also offers Spotify track search and imports selected tracks with their album and artists. |
| Spotify integration | Partially implemented — service and persistence verified | Client-credentials authentication, token caching, external track search, and idempotent local import are implemented. Live Spotify access requires `Spotify__ClientId` and `Spotify__ClientSecret` environment variables and has not been verified. |
| Track ratings and reviews | Implemented — verified | Reviews persist through EF Core; the service supports create/read/update, the database enforces one review per user per track and valid 1–5 ratings, and track details support public review reading plus authenticated submission/editing. |
| Album-track many-to-many relationship | Implemented — unverified | `ApplicationDbContext` maps `ArtistAlbums` and `ArtistTracks` and `Track.AlbumId` with a nullable optional foreign key. This matches the intended design, but the schema has not been validated via a migration review beyond the code. |
| Playlist management | Implemented — verified | Users can create, edit, and delete playlists; add/remove tracks; and browse public playlist listings/details. Owner checks are enforced in the service. |
| Playlist duplicate prevention | Implemented — verified | Service validation and a unique database index on playlist and track prevent duplicates. Playlist item positions preserve order. |
| Following and followers | Implemented — verified | Authenticated idempotent follow/unfollow, public counts and expandable 10-user pages; database constraints, concurrent requests, account deletion and browser flows verified. Merged into `main` at `ac92cbe`. |
| Likes and comments | Implemented — verified | Signed-in users can idempotently like reviews, tracks, public playlists, and comments; private playlist likes are owner-only. Users can add/edit/delete their own review comments and reply one level deep to top-level comments. Replies are grouped under parents, cannot be nested further, and are preserved as top-level comments if their parent is deleted. |
| Activity feed | Implemented — verified | `/feed` globally orders and paginates reviews, comments/replies, and likes by followed users and the signed-in user. Reply entries identify the parent author. Like activity covers reviews, comments, tracks, and public playlists; private-playlist likes are excluded. |
| Notifications | Implemented — verified | Persistent recipient-scoped notifications are created for follows, review comments, replies, and likes on reviews, comments, and playlists. Self-notifications and duplicate follow/like events are suppressed. `/notifications` lists the latest 30, supports individual and bulk read marking, and the signed-in navigation displays an unread count. A single-instance event dispatcher updates active recipient circuits; navigation and circuit reconnection reload persisted state. Track likes have no content owner to notify. |
| Rankings and trends | Implemented — verified | `/rankings` shows top-rated tracks (minimum two reviews), most-reviewed tracks overall, and tracks with the most reviews in the last seven days. Metrics use SoundBite review data only, with stable tie-break ordering. |
| Administrative user management | Implemented — verified | `/admin/users` supports user search/paging, account lock/unlock, and admin-role grants/revocation. Every service operation checks live role membership and lockout state server-side. Initial roles are bootstrapped from configured user IDs. |
| Automated tests | Passed — verified | Full NUnit suite passed: 223/223, including event creation/deduplication, recipient-only live delivery, reconnect refresh, recipient privacy, read-state updates, and authenticated notifications UI. |
| Git and repository hygiene | In progress | Live notification updates are uncommitted on `main` at `e3e1cad`. |

## 4. Latest Completed Work

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

## 5. Current Implementation Details

- The application entry point in `SoundBite/Program.cs` configures a Blazor Web App with `AddRazorComponents().AddInteractiveServerComponents()`, SQLite via `UseSqlite(...)`, ASP.NET Core Identity, and the ranking/trend services and strategies.
- `SoundBite/Data/ApplicationDbContext.cs` defines the main EF entities: `Artists`, `Albums`, and `Tracks`, and uses many-to-many joins to model `ArtistAlbums` and `ArtistTracks`.
- `Services/CatalogueService.cs` implements local database search/reads; `/catalogue/search` offers local and Spotify track search. Signed-in users can import Spotify matches; `/tracks/{id}`, `/artists/{id}`, and `/albums/{id}` show local catalogue details. Catalogue editing beyond Spotify track import is not implemented.
- `Track` has an optional `AlbumId`/`Album` relationship and uses validation attributes for required `Title` and optional non-whitespace `SpotifyId`.
- `Album`, `Artist`, and `Track` models initialize empty navigation collections and enforce basic validation requirements.
- `Endpoints/AccountEndpoints.cs` exposes registration, login, logout, and current-user endpoints.
- `Data/ApplicationUser.cs` includes `DisplayName` and optional `Bio`. The account page edits these fields; `/users/{username}` displays a public profile without exposing email.
- Remaining model and service placeholders include `UserService`. Rankings and trends use SoundBite review data; Spotify access is implemented but requires locally configured credentials. Likes support reviews, tracks, playlists, and comments; comments/feed entries remain review-scoped.
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
| Remaining social interaction scope | Notifications are not backed by a distributed event bus; private-playlist likes are excluded from the activity feed | Live delivery uses an in-process dispatcher by design for the current single-instance app | Add a distributed backplane if deploying multiple instances; otherwise prioritize user notification preferences and paging | Open |
| Spotify access has not been exercised against a live account | External search/import cannot be confirmed without valid API credentials | Spotify client credentials are read from environment variables; automated tests use a mock HTTP handler | Configure approved credentials locally and smoke-test search/import while respecting Spotify's policies | Open |
| Catalogue data and discovery remain limited | Spotify track search/import, local catalogue details, and review-based track rankings/trends are implemented | No artwork import or general catalogue editing; rankings are track-only because reviews apply to tracks | Consider general catalogue management after protected administration is implemented | Open |
| Initial administrator needs deployment configuration | The first admin must exist before the app can assign its admin role | Startup reads indexed `Admin:UserIds` settings and fails explicitly if an ID is unknown | Register the first user, configure the ID, apply migrations, then restart; keep settings in trusted environment configuration | Open |

## 8. Verification Record

| Check | Result | Notes |
|---|---|---|
| Application build | Passed | `dotnet test SoundBite.Tests/SoundBite.Tests.csproj --nologo -v minimal` built the app and test project successfully. |
| Automated tests | Passed — verified | 223 NUnit tests passed, 0 failed/skipped (2026-10-17). |
| Application startup | Passed | Command: `dotnet run --project SoundBite --urls http://localhost:5080` → app reported `Now listening on: http://localhost:5080` and `Application started.` |
| Relevant feature behaviour | Passed | Authentication/profile/review/playlist/follow flows were previously verified; rendered tests cover review interactions, feed, and track likes. |
| Database migrations | Passed | Existing migrations, `20261017012957_ThreadedReviewComments`, and `20261017013935_AddNotifications` are exercised by isolated SQLite tests; EF reports no pending model changes. The configured app database is not migrated. |

## 9. Git Status

- **Active branch:** `main` at `e3e1cad` (`Add persistent in-app notifications`), synchronized with `origin/main` before current changes.
- **Working tree:** Live notification dispatch/reconnection handling, tests, and this progress update are uncommitted.
- **Latest verification:** Focused `NotificationTests` passed 5/5; full NUnit suite passed 223/223. Application build passed; EF reports no pending model changes; CRLF-aware whitespace check passed.

## 10. Next Recommended Tasks

1. **Task:** Add notification paging and user preferences.
   - **Reason:** The notification page currently returns only the latest 30 and sends every supported notification type without user controls.
   - **Dependencies:** Preserve recipient scoping, unread-count correctness, and existing read-state behavior when filtering or loading more notifications.
   - **Acceptance criteria:** Users can load older notifications and independently disable selected notification categories; persistence, authorization, unread counts, and UI are covered by tests.

## 11. Handover for the Next Chat Session

- Follow/unfollow, catalogue discovery, Spotify-backed track search/import, review-based track rankings/trends, protected administrator user management, threaded comments, activity-feed expansion, and persistent notifications are committed on `main`. Live notification updates are the current uncommitted change.
- Active recipient circuits receive updates through a per-user in-process dispatcher. Reconnection/navigation refreshes persisted state. The full suite passed 223/223 on 2026-10-17.
- Initial admin provisioning uses trusted `Admin__UserIds__0` configuration after registration and migration. Live Spotify connectivity remains unverified. The application database still needs `20261017012957_ThreadedReviewComments` and `20261017013935_AddNotifications` applied. Next: add notification paging and user preferences.

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
