# SoundBite — Project Context, Planning and Implementation Guide

## 1. Project Overview

SoundBite is a web-based social music platform inspired by Letterboxd. Users can discover music, rate and review tracks, create playlists, follow other users, and interact with music-related activity.

The project serves two purposes:

1. **University coursework:** demonstrate software engineering principles, requirements analysis, system design, implementation, and testing.
2. **Professional portfolio:** demonstrate practical software development skills, sound architecture, maintainable code, database design, and a working social application.

The application should be developed incrementally, with an emphasis on maintainability, reliability, usability, and clear separation of responsibilities.

## 2. How to Use This Context File

This file is the primary source of truth for SoundBite's stable requirements, technical decisions, architecture, and development rules.

The separate `SOUNDBITE_PROGRESS.md` file is the source of truth for the project's current implementation status.

**Both files must be used together.**

- Read this file to understand what SoundBite is intended to become and how it should be built.
- Read `SOUNDBITE_PROGRESS.md` to understand what has already been implemented, what has been tested, what remains incomplete, and what should happen next.
- Do not assume a feature is implemented merely because it is described in this file.
- Do not assume progress-file information is still accurate without checking the current code when the task depends on it.
- Do not ask the user to paste these files into the conversation when they are already available in the repository or through an accessible file connection.
- If the files cannot be accessed, explain the limitation and request only the relevant missing information.

### File responsibilities

| File | Responsibility | Update frequency |
|---|---|---|
| `SOUNDBITE_CONTEXT.md` | Requirements, architecture, technical decisions, coding standards, and workflow instructions | Only when stable project information or guidance changes |
| `SOUNDBITE_PROGRESS.md` | Completed work, implementation status, tests, issues, recent changes, Git status, and next steps | After every meaningful implementation or project-work session |

Avoid duplicating the full contents of one file in the other. The progress file should refer to requirements and design decisions in this file instead of reproducing them.

## 3. Mandatory Progress-File Workflow

The assistant must maintain `SOUNDBITE_PROGRESS.md` as a living development record.

### Before planning or implementing work

1. Read `SOUNDBITE_CONTEXT.md`.
2. Read `SOUNDBITE_PROGRESS.md` before proposing implementation work.
3. Inspect the relevant source files, project configuration, tests, and Git state when accessible.
4. Compare the progress file against the actual repository where accuracy matters.
5. Identify the current implementation state, dependencies, existing patterns, known problems, and relevant next steps.
6. Avoid reimplementing existing functionality or contradicting established decisions.
7. If the progress file is missing, create it using the structure defined in the companion progress-file template or a suitable equivalent.
8. If the progress file is stale, correct the relevant entries based on verified repository evidence.

### After planning, coding, or testing

Update `SOUNDBITE_PROGRESS.md` whenever meaningful work has occurred, including when:

- New code has been created or existing code has been changed.
- A feature has been completed or partially implemented.
- Database entities, relationships, migrations, or schema have changed.
- Tests have been added, modified, or executed.
- A build, test, integration, or runtime issue has been discovered or resolved.
- Architecture or implementation decisions have been made.
- Work has been blocked or deferred.
- Git commits or pushes have been verified.
- The next recommended task has changed.

The progress file should record what actually happened, not what was merely planned.

### Progress-file accuracy rules

- Never claim code was built, tested, or executed unless that action actually occurred.
- Distinguish between implemented, partially implemented, planned, blocked, and verified functionality.
- Record failed builds and tests where they are relevant to future work.
- Include the names of important files and components changed.
- Record the reasons for important decisions and any assumptions that remain unresolved.
- Do not mark a task complete solely because code was generated.
- If implementation cannot be verified, explicitly state that verification is outstanding.
- Preserve useful historical context without allowing outdated details to override the current codebase.
- Keep the file concise, structured, and actionable.
- Do not record credentials, API secrets, access tokens, or other sensitive information.
- Do not rewrite the entire context file after every coding task. Update only the progress file unless a stable requirement, architecture decision, or instruction has changed.

### If the progress file cannot be updated

If repository editing is unavailable, provide the exact updated Markdown content or a focused replacement section that the user can copy into `SOUNDBITE_PROGRESS.md`.

Do not silently skip the update. Explain briefly what needs to be saved manually.

## 4. Technology Stack

The current intended stack is:

- **Framework:** Blazor Web App
- **Rendering and interactivity:** Interactive Server
- **Language:** C#
- **Target framework:** .NET 10
- **Database:** SQLite
- **Data access:** Entity Framework Core
- **Authentication:** ASP.NET Core Identity
- **Testing:** NUnit
- **UI:** Razor components, Bootstrap, and custom CSS
- **Music metadata:** Spotify API integration, subject to API access and applicable restrictions
- **Development environment:** VS Code
- **Version control:** Git and GitHub

Treat this as the intended baseline, not proof that every component is correctly configured. Inspect the current project files before changing framework versions, packages, registrations, or configuration.

Do not migrate to another framework or replace the database without a clear reason and the user's agreement.

## 5. Project Location and Repository

Previously established repository location:

`/Users/aaronurayan/Documents/GitHub/SoundBite`

Previously established application project directory:

`/Users/aaronurayan/Documents/GitHub/SoundBite/SoundBite`

The repository is expected to contain the Blazor application, database-related code, and possibly a separate test project.

The exact structure may have changed. Verify the current repository before relying on these paths.

Important project elements previously identified include:

- `SoundBite.csproj`
- `Program.cs`
- `Components/`
- `Data/`
- `Endpoints/`
- `Migrations/`
- `Properties/`
- `wwwroot/`
- `soundbite.db`

The test project has previously been associated with a `SoundBite.Tests` directory. Verify its current location and project references before running tests.

## 6. Product Goals and Scope

All previously planned core features are in scope. Implement them incrementally rather than attempting to build the entire application at once.

### 6.1 User accounts and profiles

Users should be able to:

- Register and log in.
- Log out securely.
- View and manage their profiles.
- Discover and follow other users.
- View other users' profiles and public activity.
- View their own activity and interactions.
- Manage appropriate account settings.

Authentication and authorization must be enforced on the server, not only through UI visibility.

### 6.2 Music catalogue

The application should support music metadata, including:

- Tracks
- Albums
- Artists, where needed for catalogue and ranking functionality
- Cover artwork and relevant metadata from Spotify or another approved source

Spotify integration should be introduced through a suitable service abstraction so that external API calls do not become tightly coupled to UI components.

Respect Spotify's API requirements, access restrictions, and applicable branding and content policies. Do not assume that metadata or artwork may be permanently stored or redistributed without restriction.

### 6.3 Ratings and reviews

Users should be able to rate and review tracks.

Requirements:

- Ratings use a **1–5 star scale**.
- A rating is required when a user submits a track review.
- Written review text is optional.
- A rating-only submission is valid.
- A user can have at most one review/rating record per track.
- Editing an existing review updates that record rather than creating a duplicate.
- Validation must be applied in the UI and enforced appropriately in the application and database layers.

### 6.4 Playlists

Users should be able to:

- Create playlists.
- Edit playlist details.
- Add and remove tracks.
- Discover playlists.
- Save or follow playlists, if supported by the chosen design.
- Share playlists.
- View playlist details.

A track must not appear more than once in the same playlist.

Use appropriate database constraints and service-level validation to enforce this rule.

### 6.5 Social activity and interactions

The social layer should support:

- A feed of relevant user activity.
- Recent reviews, ratings, and other supported music-related actions.
- Likes on reviews, comments, tracks, and playlists.
- Comments on reviews.
- Replies to comments, where supported by the final data model.
- Viewing other users' activity.
- Following and unfollowing users.

Prevent duplicate likes and invalid relationships. Apply suitable authorization checks before users modify or delete content.

### 6.6 Rankings and trends

The application should support rankings and discovery experiences for relevant music entities, including tracks, albums, and artists where applicable.

Possible metrics include:

- Average user rating.
- Number of ratings or reviews.
- Popularity based on application activity.
- Recently gaining popularity.
- Recent activity and engagement.

Keep the metric definitions clear. Do not present a ranking as objective or externally sourced if it is actually calculated from SoundBite user activity.

### 6.7 Administration

Administrative functionality should include appropriate user management and administrative access controls.

Restrict administrative actions through server-side authorization. Ordinary users must not gain administrative access by changing UI state or submitting crafted requests.

## 7. Data Model and Business Rules

The data model should evolve with the requirements, while preserving clear relationships and database integrity.

### Core entities

Expected entities include:

- `ApplicationUser`
- `Track`
- `Album`
- `Artist`, if required by the catalogue design
- `Review` or an equivalent track-rating entity
- `Playlist`
- A playlist-track join entity, if needed
- Follow relationships
- Likes
- Comments and comment replies
- Activity records, if required by the feed design

These are conceptual expectations, not instructions to create every class immediately. Inspect the existing model and implement entities as they become necessary.

### Track and album relationship

A track and an album have a **many-to-many relationship**.

- An album can contain multiple tracks.
- A track can be associated with multiple albums, such as when it appears on an original album and a compilation or reissue.

Represent this relationship appropriately in Entity Framework Core, using an explicit join entity if additional relationship metadata or ordering is required.

Do not assume that a track belongs to exactly one album.

### Review and rating rules

- A review belongs to a user and a track.
- A review contains a required integer rating from 1 to 5.
- Written text may be empty or absent.
- Enforce uniqueness for the user-track combination.
- Validate input on the server and use database constraints where appropriate.

### Playlist rules

- A playlist has an owner.
- A playlist can contain multiple tracks.
- A track cannot be duplicated within the same playlist.
- Track ordering should be supported if the product design requires it.

### Social relationship rules

- Follow relationships must reference valid users.
- A user must not follow the same user more than once.
- Define and enforce appropriate uniqueness rules for likes and other interactions.
- Use foreign keys and suitable deletion behavior to preserve data integrity.
- Avoid cascading deletes that could unexpectedly remove important user content.

Before implementing a new entity or changing a relationship, inspect the existing entity classes, EF Core configurations, migrations, and database state.

## 8. Architecture and Coding Principles

The project should favour clear separation of concerns and maintainable code.

### General principles

- Keep UI components focused on presentation and user interaction.
- Put business rules in appropriate service or domain layers.
- Keep database access and persistence responsibilities clear.
- Use dependency injection for application services.
- Prefer strongly typed models and clear method signatures.
- Use asynchronous operations for database and network calls where appropriate.
- Handle exceptions and validation failures deliberately.
- Avoid unnecessary abstractions, duplication, and premature optimisation.
- Follow the patterns already established in the codebase unless there is a good reason to improve them.
- Avoid large unrelated refactors when implementing a small feature.

### Services

Introduce services where they provide a useful separation of responsibilities. Potential services include:

- Account or profile services.
- Music catalogue services.
- Review and rating services.
- Playlist services.
- Social interaction services.
- Feed services.
- Ranking and trend services.
- Spotify API integration services.

These are potential responsibilities, not a requirement to create all service classes immediately.

### Database changes

When changing the data model:

1. Inspect the current entity definitions and EF Core configuration.
2. Determine whether a schema change is needed.
3. Update relationships and validation.
4. Create or update migrations when appropriate.
5. Consider the current local database and existing data.
6. Verify that the migration and application configuration are consistent.
7. Avoid destructive schema changes without understanding their impact.

Never delete or recreate the database as a routine troubleshooting step without first assessing the risk of data loss and obtaining the user's agreement when data may be lost.

## 9. Coding Collaboration Rules

The assistant is allowed and encouraged to create actual code. Do not impose a general restriction against code generation.

The assistant should support the user as a learning developer while still helping implement real features.

### Before creating substantial code

Ask **3–5 relevant clarifying questions** before substantial new implementation.

Questions should establish details that materially affect the solution, such as:

- Expected behaviour and acceptance criteria.
- Existing code or components that should be reused.
- Database and relationship implications.
- Validation, authorization, and error-handling expectations.
- Testing expectations or UI behaviour.

Use the current context file, progress file, repository, and conversation to avoid asking questions that have already been answered.

Do not ask questions merely to satisfy a quota. For a genuinely small, unambiguous change, proceed directly when the requirements are already clear. For substantial work, ask 3–5 useful questions and wait for the answers before proceeding when practical.

If a question can be answered by inspecting accessible project files, inspect the files rather than making the user do unnecessary work.

### When implementing code

The assistant may create or modify:

- C# classes and services.
- Razor components and layouts.
- Database entities and EF Core configurations.
- Migrations.
- API endpoints.
- Authentication and authorization logic.
- Validation and error handling.
- Tests and test fixtures.
- CSS and other UI assets.
- Project configuration when necessary.

Make changes that fit the existing architecture. Explain any meaningful trade-offs or changes in direction.

### After generating or modifying code

Provide a concise explanation covering:

1. What was changed.
2. What the main classes, methods, or components do.
3. How the changes fit into the existing application.
4. Important assumptions or limitations.
5. What was actually built or tested and the result.

Keep the explanation concise and appropriate to the task. Explain unfamiliar concepts when useful for the user's learning.

Clearly distinguish code that was generated from code that was actually integrated, built, tested, or run. Never claim successful execution without evidence.

### After implementation

1. Update `SOUNDBITE_PROGRESS.md` with the work completed.
2. Record files changed and important design decisions.
3. Record build and test results accurately.
4. Record outstanding issues and the next recommended task.
5. Remind the user to review the diff and push the changes to Git when ready.
6. Suggest a Git commit title and description that accurately reflect the actual changes.

Do not claim to have committed or pushed anything unless the operation was actually performed and its success verified. Do not perform a commit or push without the user's explicit authorization.

## 10. Testing and Verification

Testing should be introduced alongside implementation, rather than left until the end.

Use NUnit for automated tests where appropriate.

Test relevant behaviour such as:

- Valid and invalid ratings.
- Required ratings and optional review text.
- Duplicate reviews for the same user and track.
- Playlist duplicate prevention.
- Valid and invalid follow relationships.
- Duplicate likes.
- Authorization and access control.
- Service behaviour and database interactions.
- API failures and external service edge cases.

When possible, run the appropriate build and tests after a meaningful change.

Record the exact commands and meaningful results in `SOUNDBITE_PROGRESS.md`. If tests cannot be run, state why and identify what still needs verification.

## 11. Git and Repository Hygiene

Use Git to track meaningful changes.

Before suggesting a commit:

- Inspect the working tree and relevant diff when possible.
- Avoid including unrelated changes.
- Never include credentials, API keys, connection secrets, or other sensitive configuration.
- Do not overwrite the user's uncommitted work.
- Do not assume that a successful local change has already been pushed to GitHub.

After meaningful implementation, provide:

**Suggested commit title:** A concise imperative summary.

**Suggested commit description:** A short explanation of the implementation, key behaviour, and relevant tests.

Remind the user to review and push the changes. The progress file must distinguish between modified files, committed work, and pushed work based on verified Git state.

## 12. Planning and Implementation Workflow

For each new feature or bug fix, follow this process:

1. **Load context:** Read this file and `SOUNDBITE_PROGRESS.md`.
2. **Verify current state:** Inspect relevant code, project configuration, tests, and Git state.
3. **Define the task:** Identify expected behaviour, constraints, dependencies, and acceptance criteria.
4. **Clarify:** Ask 3–5 relevant questions before substantial new code, unless the requirements are already sufficiently clear.
5. **Plan:** Propose a focused implementation plan and identify the files likely to change.
6. **Implement:** Create or modify code following the existing architecture.
7. **Verify:** Run relevant builds, tests, or manual checks where available.
8. **Explain:** Briefly describe the implementation and any outstanding limitations.
9. **Update progress:** Update `SOUNDBITE_PROGRESS.md` with verified outcomes, changed files, decisions, issues, and next steps.
10. **Git reminder:** Ask the user to review the changes and push when ready, providing a suggested commit title and description.
11. **Maintain context:** Update this file only if a stable requirement, architectural decision, technology choice, or collaboration rule has changed.

If the work is blocked, record the blocker and the next action required. Do not mark incomplete work as finished merely to advance the plan.

## 13. Definition of Done

A feature should be marked complete only when its agreed acceptance criteria have been met and its status is supported by evidence.

Depending on the task, completion may require:

- Implementation integrated into the application.
- Appropriate validation and authorization.
- Database changes and migrations.
- Relevant automated tests.
- A successful build.
- Appropriate manual verification.
- Accurate progress-file documentation.

Not every task requires every verification step, but any outstanding checks must be identified explicitly.

## 14. Final Instruction to the Assistant

Treat SoundBite as an ongoing software engineering project, not a sequence of disconnected coding questions.

Always use the main context file and progress file together. Check existing work before implementing, ask relevant questions before substantial code, generate and explain code when appropriate, verify changes honestly, and update `SOUNDBITE_PROGRESS.md` after meaningful work.

The purpose of the two-file system is to preserve continuity without repeatedly copying the entire project context into the conversation. Keep stable guidance in this file and current implementation knowledge in the progress file.
