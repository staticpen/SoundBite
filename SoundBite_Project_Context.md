# SoundBite — Project Context, Planning, and Implementation Guide

> **Purpose:** This document is the handoff context for an AI assistant helping plan and build SoundBite across future conversations. Treat it as the project's working source of truth. Preserve the user's work, distinguish confirmed decisions from proposals, and update this document when decisions change.

## 1. Project overview

**SoundBite** is a social music platform inspired by Letterboxd. Users should do more than browse music: they should review and rate tracks, follow other users, interact with content, curate playlists, and see activity from people they follow.

**Project purpose (confirmed):** SoundBite serves both as a university coursework project and as a software-development portfolio project.

**Scope (confirmed):** All planned features are in scope. This includes accounts and profiles, music catalogue integration, reviews and ratings, playlists, likes, comments and replies, follows, an activity feed, rankings, trends, and admin/user management. Features should be implemented in manageable stages according to dependencies and coursework deadlines, without silently dropping planned functionality.

The user is learning software design and implementation and wants to understand how the system works, not merely receive finished code.

### Collaboration and coding rules for the assistant

- **The assistant may generate code.** It may write complete classes, services, components, tests, configuration, database mappings, and other implementation artefacts when appropriate to the task.
- **Before generating substantial new code, ask 3–5 relevant questions.** These should clarify requirements, expected behaviour, constraints, existing architecture, and important edge cases. Ask questions that materially affect the implementation rather than generic questions.
- If essential details are already established by this document, the current conversation, or inspected project files, do not ask the user to repeat them. Ask only the remaining questions needed to make sound implementation decisions.
- For small, unambiguous changes, do not force unnecessary questions. The 3–5 question requirement primarily applies before creating substantial code or implementing a feature.
- Ask questions before generating the code, not after. Where practical, wait for the user's answers before proceeding with a substantial implementation.
- If a task has several independent implementation decisions, present 3–5 concise questions together when that is more efficient than asking them individually. If the user prefers a guided, one-question-at-a-time discussion, follow that preference.
- Explain design decisions and trade-offs in beginner-friendly language. The goal is to help the user understand and maintain the code, not just copy it.
- **After generating code, briefly explain what it does.** Summarise the main responsibilities, important design choices, how the pieces interact, and any assumptions or limitations. Avoid lengthy explanations unless the user requests them.
- Clearly distinguish code that has been generated from code that has actually been integrated, built, or tested. Never claim that an implementation works unless there is evidence from execution or testing.
- Preserve existing code, data, Git history, and uncommitted changes. Never recreate the repository, delete history, force-push, or overwrite files without explicit permission.
- Inspect relevant existing files and project conventions before changing established code. Prefer targeted changes over unnecessary rewrites.
- Work in manageable increments. Avoid implementing unrelated features in the same change unless requested.
- Clearly label what is **confirmed**, **recommended**, **proposed**, **implemented**, and **still undecided**.
- Avoid unnecessary web searches for project-specific planning unless current external documentation or facts are needed.
- Do not address the user by name unless explicitly requested.

### Git workflow and delivery expectations

After completing a meaningful implementation or change:

1. Summarise the files and functionality changed.
2. Explain the implementation succinctly, including important assumptions and any known limitations.
3. Report the build and test results if those checks were actually performed. If they were not performed, say so and recommend the appropriate checks.
4. Remind the user to review the changes, inspect Git status and the diff, and push the work to Git when ready.
5. Provide a suggested **Git commit title** and **commit description** that accurately reflect the work completed.
6. Do not claim that changes have been committed or pushed unless this has actually been verified. Do not automatically commit or push without the user's explicit authorisation.

Suggested final response structure after an implementation:

- **What changed:** Short summary of the implementation.
- **How it works:** Concise explanation of the main design and behaviour.
- **Verification:** Actual build/test results or the checks still required.
- **Git reminder:** Ask the user to review and push the changes when ready.
- **Suggested commit title:** A short, imperative or conventional commit-style title.
- **Suggested commit description:** A concise summary of the important changes.

For planning-only, explanation-only, or debugging conversations where no meaningful code change has been made, adapt this workflow rather than adding irrelevant Git instructions.

## 2. Technology stack and development environment

### Confirmed setup

- **Application:** Blazor Web App
- **Framework:** .NET 10 (`net10.0`)
- **Language:** C#
- **Interactivity:** Blazor Interactive Server
- **Database:** SQLite
- **ORM:** Entity Framework Core
- **Authentication:** ASP.NET Core Identity
- **Testing:** NUnit
- **IDE/editor:** VS Code on macOS
- **Source control:** Git; repository already exists and has an origin remote
- **Music catalogue:** Spotify API is the planned source of music metadata, including titles, cover art, artist information, and release information

### Repository locations

Repository root:

`/Users/aaronurayan/Documents/GitHub/SoundBite`

Application project:

`/Users/aaronurayan/Documents/GitHub/SoundBite/SoundBite`

Test project:

`/Users/aaronurayan/Documents/GitHub/SoundBite/SoundBite.Tests/SoundBite.Tests.csproj`

From the repository root:

```bash
dotnet run --project SoundBite/SoundBite.csproj
dotnet test SoundBite.Tests/SoundBite.Tests.csproj
```

### Project configuration known from previous work

- `SoundBite/SoundBite.csproj` targets `net10.0`, with nullable reference types and implicit usings enabled.
- EF Core SQLite, EF Core Design, and ASP.NET Core Identity EF packages were present at version `10.0.12` at the time of the last check.
- SQLite connection string in `SoundBite/appsettings.json` was `Data Source=soundbite.db`.
- The database file `SoundBite/soundbite.db` exists and was tracked by Git at the last check.
- An initial EF Core migration named `InitialCreate` exists, and `dotnet ef database update` previously succeeded.
- `Program.cs` configures the database, Identity, authentication/authorization, Razor components, and account endpoints. A duplicate SQLite `AddDbContext` registration was noticed in an earlier snapshot; verify current code before changing it.
- A warning about the HTTPS redirect port was seen during local startup. Do not assume this is still present without checking.

## 3. Current implementation status (last known)

This status is based on prior conversation, not a fresh inspection of the repository. Confirm current files and Git status before making changes.

### Infrastructure already created

- `SoundBite/Data/ApplicationUser.cs` inherits `IdentityUser`.
- `SoundBite/Data/ApplicationDbContext.cs` inherits `IdentityDbContext<ApplicationUser>`.
- `SoundBite/Endpoints/AccountEndpoints.cs` maps `POST /api/account/register`.
- `Program.cs` maps account endpoints with `app.MapAccountEndpoints()`.
- `SoundBite/Components/Pages/Register.razor` and its `.razor.css` stylesheet exist.
- Registration endpoint was tested using `curl`: creating an account and duplicate-account validation worked at that time. The complete browser form submission was not yet confirmed.
- `SoundBite.Tests` is an NUnit test project referencing the application. At the last check, one default/generated test passed; meaningful application tests still need to be written.
- A SQLite database and initial migration exist.

### Backend scaffolding

The following folders and C# stubs were created. The stubs were empty shells, not completed domain implementations.

**`SoundBite/Models/`**
- `Track.cs`
- `Album.cs`
- `Artist.cs`
- `Review.cs`
- `Playlist.cs`
- `PlaylistItem.cs`
- `Follow.cs`
- `Like.cs`
- `Comment.cs`
- `ActivityItem.cs`

**`SoundBite/Interfaces/`**
- `IMusicCatalog.cs`
- `IRankingStrategy.cs`

**`SoundBite/Services/`**
- `UserService.cs`
- `ReviewService.cs`
- `PlaylistService.cs`
- `FollowService.cs`
- `SocialInteractionService.cs`
- `FeedService.cs`
- `RankingService.cs`
- `TrendService.cs`
- `AdminService.cs`
- `SpotifyCatalogService.cs`

**`SoundBite/Strategies/`**
- `RatingRankingStrategy.cs`
- `PopularityRankingStrategy.cs`

**`SoundBite/Configuration/`**
- `SpotifyOptions.cs`

All of the above stubs existed and the project built successfully at the last check. They may since have changed; inspect before editing.

### Git hygiene cautions

- `.gitignore` exists at the repository root.
- `SoundBite/soundbite.db` was already tracked by Git, so adding it to `.gitignore` alone will not stop tracking it.
- `.DS_Store` was modified and `.gitignore` was modified at the last reported status. It was unclear whether `.DS_Store` ignore rules were added.
- `SoundBite/cookies.txt` was not tracked at the last check and was intended to be ignored as a local test artifact.
- The last reported `git status --short` included changes to `.DS_Store`, `.gitignore`, `SoundBite/Program.cs`, `SoundBite/soundbite.db`, new `SoundBite.Tests/`, new registration UI files, and new endpoint files. This is historical status, not a statement of current status.
- Before committing, inspect `git status`, `git diff --stat`, and staged changes. Avoid blindly staging unrelated changes.
- Do not delete or untrack the database without checking whether it contains needed data and getting the user's agreement.
- Never include secrets, credentials, local test artefacts, or unintended database changes in a commit.
- A suggested Git title and description must match the actual changes, not planned work that has not been implemented.

## 4. Product features and requirements

### Users and profiles

Users should be able to:

- Register, log in, and manage an account.
- View and edit their profile. Exact profile fields are still to be decided.
- Review and rate tracks.
- Follow other users.
- View other users' activity.
- Like and comment on relevant content.

An existing `ApplicationUser` based on ASP.NET Identity should be extended as needed; do not create a second unrelated `User` model unless there is a clear reason.

### Music catalogue

- Music metadata should come from Spotify, including track and album/release titles and cover art.
- Spotify imports should avoid duplicate catalogue entries by using the external Spotify catalogue ID, not title matching.
- The domain model uses the general concept **`Album`** for a release. Do **not** add a release-type property for album/single/EP/compilation unless the user changes this decision.
- Exact Spotify API integration, credentials, token flow, caching, and API limits still need planning.

### Reviews and ratings

- A user can have **only one review per track**.
- Editing a review updates the existing review rather than creating another.
- Review records should be associated with one user and one track.
- **Rating scale (confirmed): 1–5 stars.**
- **A rating is required; written review text is optional (confirmed).** A rating-only review is valid.
- Ratings outside the range of 1–5 must be rejected.
- Created/updated timestamp semantics should be defined consistently.

### Playlists

Users should be able to create, edit, discover, save, and share playlists.

Confirmed decisions:

- A playlist has one owner.
- **Duplicate tracks are not allowed within a playlist.**
- Playlist ordering is needed; a playlist item can store a position.
- Public/private/unlisted visibility and default visibility remain undecided.

### Likes and comments

Confirmed decisions:

- Likes can target **reviews, comments, tracks, and playlists**.
- Comments are attached to reviews.
- Comments support replies.

Proposed implementation design (not yet implemented or finally confirmed):

- A `Comment` has an author and a review. A top-level comment has no parent; a reply references its parent comment.
- Initially limit replies to one level unless the user chooses fully nested threads.
- Validate that a reply's parent belongs to the same review.
- To keep relational integrity, consider a `Like` entity with a user foreign key and nullable foreign keys to each allowed target (`Review`, `Comment`, `Track`, `Playlist`), plus a check constraint that exactly one target foreign key is populated. Configure a unique user/target pair for each target type. This is a recommendation, not an implemented design.
- Deleting a target should not leave orphaned likes or comments.

### Follows

Proposed design:

- Follow is directional: A following B does not imply B follows A.
- `FollowerId` and `FollowedId` both reference `ApplicationUser`.
- Prevent duplicate follow pairs and self-following.
- Exact user-blocking, private accounts, and follow-request behaviour are not in scope yet.

### Activity feed

The app should show recent activity from other users, such as reviews and playlist activity. A proposed `ActivityItem` records an actor, event type, target reference, and timestamp.

This design still needs a decision about how to reference targets and what happens when a target is deleted. A generic `TargetType` plus `TargetId` is flexible but does not provide ordinary relational foreign-key enforcement. Initially, the feed could include only selected events (for example, review created, playlist created, and follow) rather than every edit and like.

### Ranking and trends

Planned features:

- Rank tracks, albums, and artists by a metric.
- Display popular and increasingly popular music.
- `IRankingStrategy`, `RatingRankingStrategy`, `PopularityRankingStrategy`, `RankingService`, and `TrendService` scaffolds exist from earlier work.
- Ranking metrics, time windows, popularity definitions, and cold-start behaviour are not yet specified.
- Do not treat Spotify popularity as the same thing as SoundBite user ratings unless the product design explicitly chooses that.

### Admin

- Admin user management is planned.
- Permissions, moderation tools, report flows, and admin role setup still need definition.

## 5. Domain model planning

This section captures the current proposed entity relationships. Treat it as a design guide, not code that has already been written.

### Catalogue entities

**`Artist`**

- Represents a music artist.
- Can be associated with multiple albums and multiple tracks.
- Artist ↔ Album and Artist ↔ Track are proposed many-to-many relationships to support collaborations and featured artists.

**`Album`**

- Represents a general music release, regardless of whether Spotify labels it a single, EP, compilation, or full-length album.
- Contains one or more tracks.
- **Album ↔ Track relationship (confirmed): many-to-many.** A track may appear on multiple releases, and a release can contain multiple tracks.
- Use an explicit join entity only if relationship-specific data is needed; otherwise, a standard EF Core many-to-many mapping may be sufficient.
- Spotify external IDs should be unique per provider. With Spotify as the only provider, a unique Spotify ID may be sufficient; a `(Provider, ExternalId)` uniqueness rule is more extensible if other catalogues are added.

**`Track`**

- Represents the track/recording that users review and add to playlists.
- Has Spotify metadata and a unique external Spotify ID.
- Can be associated with one or more artists and one or more albums/releases.
- Decide which metadata to persist versus retrieve on demand.

### Social and user-generated entities

**`Review`**

- Belongs to one `ApplicationUser` and one `Track`.
- Unique constraint/index on `(UserId, TrackId)`.
- Rating is required and must be within **1–5 stars**.
- Written review text is optional; a rating-only review is valid.
- Created/updated timestamps should be handled consistently.

**`Playlist`**

- Has one owner.
- Has many `PlaylistItem` records.
- Store visibility and timestamps once those product decisions are made.

**`PlaylistItem`**

- Joins a playlist to a track and stores ordering/position.
- Unique constraint on `(PlaylistId, TrackId)` to prevent duplicate tracks.
- A unique `(PlaylistId, Position)` may be useful to prevent duplicate positions. Reordering under such a constraint needs careful update logic to avoid temporary collisions.
- Decide whether positions start at zero or one; one-based positions are often easier to explain to users.

**`Comment`**

- Belongs to an author and a review.
- May reference a parent comment to support replies.
- If nested replies are allowed, define the maximum depth or allow arbitrary nesting.
- Validate that parent and child comments refer to the same review.

**`Like`**

- Belongs to a user and targets exactly one of Review, Comment, Track, or Playlist.
- Enforce one like per user per target and define how unliking works.
- A strongly relational schema is recommended over a generic target ID if preserving foreign-key integrity is a priority.

**`Follow`**

- Connects a follower to a followed user.
- Unique follower/followed pair and no self-follow.

**`ActivityItem`**

- Records a user activity event for the feed.
- Proposed fields include actor, event type, target reference, and created timestamp.
- Decide whether activity is stored permanently, generated from other records, or uses a hybrid approach.

## 6. Proposed implementation order

Implement domain entities and relationships before service logic. Do not rush to migrations until the first set of model properties and relationships is sufficiently settled.

1. **Catalogue:** `Artist`, `Album`, `Track`
2. **Reviews:** `Review`
3. **Playlists:** `Playlist`, `PlaylistItem`
4. **Comments:** `Comment`
5. **Social interactions:** `Like`, `Follow`
6. **Activity feed:** `ActivityItem`
7. **EF Core mapping/configuration:** relationships, unique indexes, check constraints, delete behaviours, and any join entities.
8. **Migrations/database update:** generate and review migrations once model design is ready.
9. **Services:** implement one use case at a time, with validation and authorization.
10. **Tests:** add meaningful unit/integration tests for constraints and workflows.
11. **API/UI integration:** connect forms and components to services and verify full user flows.
12. **Spotify integration, rankings, trends, and admin:** implement after the core data and social workflows are stable, unless coursework priorities suggest otherwise.

This order is a recommendation; adjust it to coursework deadlines and dependencies. All planned features remain in scope, so use staged delivery rather than silently dropping features.

## 7. Persistence rules and deletion policy

The following are proposed rules that need confirmation and EF Core configuration:

- User deletion and whether their reviews/playlists should be deleted, anonymised, or retained are undecided.
- Deleting a review should remove or otherwise handle its comments and likes.
- Deleting a comment should handle its replies and likes.
- Deleting a playlist should remove its playlist items and likes on the playlist.
- Removing a track from the catalogue should not accidentally destroy user-created content. Prefer explicit archival/cleanup or restrictive delete behaviour where appropriate.
- Deleting a track should handle playlist items, reviews, and likes deliberately.
- User-owned likes and follows should be removed or handled when the user is deleted.
- Be explicit about cascade, restrict, and set-null behaviour; don't rely on accidental defaults.
- Consider storing timestamps in UTC and keeping them consistent across the app.

## 8. Validation, constraints, and security principles

At minimum, plan to enforce:

- One review per user per track.
- Required rating within the **1–5 star** range; written review text is optional.
- No duplicate tracks within a playlist.
- No duplicate playlist item positions if positions are uniquely constrained.
- No duplicate likes for the same user/target.
- No duplicate follow pair and no self-follow.
- Exactly one target for each like.
- Non-empty/trimmed names and text where text is required, with reasonable maximum lengths.
- Valid same-review comment replies.
- Unique external catalogue IDs.
- Authorization: users should only edit/delete their own reviews, playlists, comments, and other owned content unless an admin action is authorized.
- Server-side validation even if the UI also validates.
- Secrets such as Spotify client credentials must not be committed to Git.

These rules may be enforced at more than one layer. Database constraints protect data integrity; services enforce application workflows and permissions; UI validation improves feedback but is not sufficient by itself.

## 9. Testing expectations

The existing NUnit project passed one test at the last check, but it was believed to be the generated placeholder test. Treat the meaningful test suite as **not yet implemented** until verified.

Suggested early tests:

- A user can create a review with a valid rating and no written text.
- Ratings below 1 or above 5 are rejected.
- A second review by the same user for the same track is rejected or updates the existing review according to the chosen service contract.
- Different users can review the same track.
- Duplicate playlist tracks are rejected.
- Playlist positions and reordering behave correctly.
- Users cannot like the same target twice.
- Users cannot follow themselves or create duplicate follows.
- A reply cannot reference a comment on a different review.
- Users cannot modify another user's content.
- Spotify imports with an already-existing external ID do not create duplicates.
- A track can be associated with multiple albums/releases, and an album can contain multiple tracks.

Choose whether a test should use a real temporary SQLite database or mocks based on what is being tested. Database uniqueness and EF mapping behaviour are best verified with database-backed tests.

## 10. Decisions confirmed so far

- The product is a social music platform inspired by Letterboxd.
- **Purpose:** Both university coursework and portfolio development.
- **Scope:** All planned features are in scope, implemented in stages as appropriate.
- Stack: Blazor Web App, Interactive Server, C#, .NET 10, SQLite, EF Core, ASP.NET Core Identity, NUnit.
- Spotify is the planned source of catalogue metadata.
- Existing `ApplicationUser`/Identity should be used rather than creating a duplicate user entity.
- One review per user per track; edits update the existing review.
- **Rating scale is 1–5 stars; a rating is required and written review text is optional.**
- Duplicate tracks are not allowed in a playlist.
- Likes apply to reviews, comments, tracks, and playlists.
- Comments are on reviews and support replies.
- Duplicate Spotify imports should be prevented using external catalogue IDs.
- **Album ↔ Track is many-to-many.**
- Use a generic `Album` concept for all releases; do not distinguish singles/albums with an `AlbumType` property for now.
- **Coding assistance should be mixed depending on the task.** The assistant may generate code after clarifying relevant requirements and should explain the implementation afterward.

## 11. Important open questions

Ask these gradually, when they become relevant. Do not block all implementation on answering every question.

1. **Playlist visibility:** Should playlists be public/private/unlisted? What is the default?
2. **Comment replies:** One level of replies, or fully nested threads?
3. **User deletion:** Should user content be deleted, anonymised, or retained if an account is deleted?
4. **Activity feed:** Which actions create feed items? Should edits/likes appear, or only major events?
5. **Spotify import:** Which metadata should be stored locally, and how should refreshes or missing catalogue items be handled?
6. **Ranking/trends:** What counts as rating, popularity, and gaining popularity? What time windows should be used?
7. **Playlist saves/shares:** Does “save playlist” mean bookmarking another user's playlist, copying it, or both?
8. **Coursework priorities:** Which features are mandatory for each assessment milestone, and what order best fits the deadlines? All planned features remain in the overall product scope.
9. **Profile fields:** Which user-editable profile details should be supported?

The rating scale, review text requirement, and Album/Track relationship are confirmed and should no longer be treated as open questions.

## 12. How a future assistant should continue

At the start of a new session:

1. Read this document.
2. Ask the user what they are working on now only if their request doesn't identify the next task.
3. If discussing implementation, verify current files and Git status rather than assuming the historical state above is still current.
4. Before substantial code generation, ask 3–5 relevant questions about requirements, expected behaviour, constraints, existing architecture, and edge cases. Use known project context to avoid repeating questions that have already been answered.
5. Once the necessary decisions are clear, generate the requested code or implementation artefacts. Inspect existing files first where possible, and make targeted changes that preserve unrelated work.
6. After generating code, explain succinctly what it does, how the main pieces interact, and any important assumptions or limitations.
7. Report build and test results accurately. If checks have not been run, identify which checks the user should perform.
8. After a meaningful implementation, remind the user to review the diff, check Git status, and push the changes when ready. Provide a suggested Git commit title and description that reflect the actual work. Do not claim to have committed or pushed anything unless it has actually happened.
9. For debugging, inspect the exact current code and error where possible, then work through reproduction, hypotheses, tests, and root cause.
10. Update this context document when important decisions or implementation status change, and clearly mark any stale details.
