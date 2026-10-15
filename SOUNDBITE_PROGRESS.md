# SoundBite — Development Progress

> **Purpose:** This is the living record of SoundBite's actual implementation status. Read it alongside `SOUNDBITE_CONTEXT.md` before planning or implementing work. Update it after every meaningful coding, testing, debugging, or planning session.
>
> **Accuracy rule:** Distinguish verified implementation from assumptions and planned work. Do not claim that code works, tests pass, or changes have been pushed unless that has been verified.

## 1. Current Project Snapshot

- **Project:** SoundBite
- **Application:** Blazor Web App
- **Interactivity:** Interactive Server
- **Language:** C#
- **Target framework:** .NET 10, subject to verification
- **Database:** SQLite
- **ORM:** Entity Framework Core
- **Authentication:** ASP.NET Core Identity
- **Testing framework:** NUnit
- **Version control:** Git and GitHub
- **Main context file:** `SOUNDBITE_CONTEXT.md`

Update these details if the verified project configuration changes.

## 2. Current Status

- **Overall status:** In progress
- **Current development phase:** Verify against the repository before updating
- **Current focus:** Inspect the latest repository state and identify the highest-priority incomplete task
- **Last verified:** Not yet recorded in this initial template
- **Current blocker:** Not yet verified

Do not leave these placeholder values unchanged once the repository has been inspected.

## 3. Feature Implementation Tracker

Use the following statuses consistently:

- **Not started:** No meaningful implementation exists.
- **Planned:** Requirements or an implementation plan exist, but coding has not begun.
- **In progress:** Implementation has begun but is incomplete.
- **Implemented — unverified:** Code exists, but the required verification has not been completed.
- **Blocked:** Progress depends on resolving a documented issue.
- **Complete — verified:** The agreed acceptance criteria have been met and the relevant verification has succeeded.

Initial status is unknown until the repository is inspected. Update each status only when supported by available evidence.

| Feature | Status | Evidence / notes |
|---|---|---|
| User registration and authentication | Unknown — inspect | Verify Identity configuration, endpoints, and actual registration/login behaviour |
| User profiles | Unknown — inspect | Verify profile model, pages, and editing functionality |
| Music catalogue | Unknown — inspect | Verify track, album, and artist models and catalogue UI |
| Spotify integration | Unknown — inspect | Verify API client, configuration, and actual integration |
| Track ratings and reviews | Unknown — inspect | Verify required 1–5 rating, optional text, uniqueness, and editing |
| Album-track many-to-many relationship | Unknown — inspect | Verify EF Core model, configuration, and migration |
| Playlist management | Unknown — inspect | Verify create/edit and track management |
| Playlist duplicate prevention | Unknown — inspect | Verify service validation and database enforcement |
| Following and followers | Unknown — inspect | Verify relationship model and functionality |
| Likes and comments | Unknown — inspect | Verify supported interaction types and authorization |
| Activity feed | Unknown — inspect | Verify activity creation and feed display |
| Rankings and trends | Unknown — inspect | Verify ranking metrics and implementation |
| Administrative user management | Unknown — inspect | Verify authorization and administrative functionality |
| Automated tests | Unknown — inspect | Verify test project, test coverage, and latest test results |
| Git and repository hygiene | Unknown — inspect | Verify working tree, recent commits, and whether changes are pushed |

Do not mark a feature complete merely because a corresponding class or UI component exists. Assess the agreed behaviour and the level of verification available.

## 4. Latest Completed Work

Record the latest meaningful work in reverse chronological order.

For each entry, include:

- Date, if known.
- Task completed.
- Files or components changed.
- Important implementation decisions.
- Verification performed and its result.
- Remaining limitations.

No implementation session has been recorded in this initial template.

## 5. Current Implementation Details

Maintain a concise record of important implementation facts that are not already obvious from the main context file.

Examples include:

- Existing services and their responsibilities.
- Implemented pages and components.
- Database entity relationships.
- Authentication and authorization patterns.
- API endpoint routes and request formats.
- Current migration state.
- Configuration requirements, excluding secrets.
- Existing test commands and known build constraints.

Verify these details against the repository. Remove or correct outdated information rather than allowing this section to become misleading.

## 6. Important Decisions and Assumptions

Record decisions made during implementation that future work must respect.

For each decision, include:

- **Decision:** What was chosen.
- **Reason:** Why it was chosen.
- **Impact:** What future implementation depends on it.
- **Status:** Confirmed, provisional, or awaiting user input.

Do not duplicate stable decisions already documented in `SOUNDBITE_CONTEXT.md`. Use this section primarily for recent implementation decisions or link back to the main context file.

## 7. Known Issues and Blockers

For each unresolved issue, record:

- **Issue:** Clear description.
- **Impact:** What it prevents or risks.
- **Evidence:** Error message, failing test, or relevant file, where available.
- **Next action:** The most useful step to resolve it.
- **Status:** Open, investigating, blocked, or resolved.

Remove resolved issues from the active list once their resolution has been verified. Retain a short historical note in the completed-work section if it is useful for future debugging.

## 8. Verification Record

Record the most recent relevant verification results.

| Check | Result | Notes |
|---|---|---|
| Application build | Not yet verified | Record command and outcome |
| Automated tests | Not yet verified | Record command, number of tests where available, and failures |
| Application startup | Not yet verified | Record whether the application started successfully |
| Relevant feature behaviour | Not yet verified | Record the specific scenario checked |
| Database migrations | Not yet verified | Record whether migrations were reviewed and applied successfully |

Only record a passing result when the check actually ran and succeeded. If a check was not run, keep it marked as not verified.

## 9. Git Status

Update this section using the actual Git state when accessible.

- **Working-tree status:** Not yet verified
- **Uncommitted changes:** Not yet verified
- **Latest relevant commit:** Not yet verified
- **Changes committed:** Not yet verified
- **Changes pushed to remote:** Not yet verified

Record the suggested commit title and description for the latest meaningful implementation when applicable.

Never include secrets or sensitive configuration in this file.

## 10. Next Recommended Tasks

Maintain a prioritised list of actionable tasks based on the actual repository state.

For each task, include:

1. **Task:** What should be done.
2. **Reason:** Why it matters now.
3. **Dependencies:** What must be in place first.
4. **Acceptance criteria:** How completion will be assessed.

Do not invent a definitive task order before inspecting the repository. Revise the list whenever new work changes the priorities.

## 11. Handover for the Next Chat Session

At the end of each meaningful implementation session, write a short handover containing:

- What was accomplished.
- What remains incomplete.
- Any known errors or blockers.
- Which files are most relevant to the next task.
- The next recommended action.
- Any verification that remains outstanding.

This section should allow the next session to continue without requiring the user to repeat the entire conversation.

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

If the repository cannot be edited directly, provide the exact updated sections for the user to save manually.
