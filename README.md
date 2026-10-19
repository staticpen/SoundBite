# SoundBite — Assignment 2

This README documents submission revision `cef1f7ae957e04148116ec91a19ceaa7602c9744` of `staticpen/SoundBite`. Use application files from that revision; these instructions do not describe older branches.

## Project Overview

SoundBite is a social music discovery application built for university coursework. Users can explore artists, albums and tracks, rate and review music, save favourite tracks, and interact with other listeners.

The implemented features include:

- Local catalogue search and artist, album and track detail pages; optional Spotify artist discovery and catalogue import.
- Track ratings from 1–5 stars, optional written reviews, and editing an existing review.
- A dedicated **Favourites** page for adding/removing saved tracks. Favourites also appear on public profiles.
- Independent **Like/Unlike** interactions on tracks, reviews and comments. Liking a track does not add it to Favourites.
- Account registration, login, editable profiles, listener discovery, following/unfollowing, review comments and replies, and an activity feed. Public profiles show favourite tracks and reviews.
- In-app notifications with read status and category preferences.
- Top-rated, most-reviewed and weekly trending tracks, calculated from SoundBite data.
- Role-restricted user administration.

Local assessment does **not** require Tailscale, Aaron's hosted website, or access to a teammate's computer. The app runs without Spotify credentials, but a fresh database starts without music. Importing music through the current UI requires Spotify access; an already populated local catalogue remains usable without it.

## Technology Stack

Versions below are taken from `global.json`, the project files and `.config/dotnet-tools.json`.

| Component | Version / implementation |
| --- | --- |
| Language | C# 13, the default for the `net9.0` target; no explicit language override |
| Application | .NET 9 Blazor Web App, Interactive Server, ASP.NET Core |
| Required SDK | **9.0.319**, pinned with roll-forward disabled and preview SDKs excluded |
| Entity Framework Core | SQLite and Design packages **9.0.21** |
| Authentication | ASP.NET Core Identity EntityFrameworkCore **9.0.21** |
| Database | SQLite through the EF Core provider; no separately installed database server required |
| EF command-line tool | Local `dotnet-ef` **9.0.21** |
| Music API | Spotify Web API **v1**, called using `HttpClient` and client credentials; no Spotify SDK package |
| Tests | NUnit **4.3.2**, NUnit3TestAdapter **5.0.0**, NUnit.Analyzers **4.7.0** |
| Test infrastructure | Microsoft.NET.Test.Sdk **17.14.0**, Microsoft.AspNetCore.Mvc.Testing **9.0.21**, coverlet.collector **6.0.4** |

`SoundBite.sln` contains the web and NUnit test projects. Keep the full source tree, migrations and `.config/dotnet-tools.json` in the submission. See [RUNNING_LOCALLY.md](RUNNING_LOCALLY.md) for the repository's companion local-run guide. Historical progress and hosted-access notes are not local prerequisites.

## Prerequisites

- Windows with **Visual Studio 2022 17.14** and the **ASP.NET and web development** workload, installed through Visual Studio Installer. Microsoft's [SDK/Visual Studio compatibility table](https://learn.microsoft.com/en-us/dotnet/core/porting/versioning-sdk-msbuild-vs) associates SDK 9.0.3xx with Visual Studio 17.14.
- **.NET SDK 9.0.319** for your Windows architecture, installed alongside any other SDKs. A runtime alone or a different .NET 9 SDK does not satisfy this repository's exact pin. Use the official [.NET 9 downloads](https://dotnet.microsoft.com/en-us/download/dotnet/9.0).
- PowerShell and a current browser with JavaScript enabled.
- Internet access for initial NuGet/tool restoration. Live Spotify functionality additionally needs Spotify API access and your own developer credentials.
- Git only if cloning; an extracted copy of the complete submission also works. Node.js, npm, SQL Server and a standalone SQLite installation are not needed for the documented workflow.

**Verification boundary:** the commands and automated tests were checked on macOS with the pinned SDK. Windows installation, Visual Studio project loading, Test Explorer and F5 still require validation on a Windows machine. The PowerShell examples below have been checked against the repository's interfaces but were not executed on Windows.

## Installation and Setup

### 1. Open and build the solution

Extract or clone the complete repository to a writable directory. In Visual Studio, choose **File → Open → Project/Solution** and select `SoundBite.sln`. Set **SoundBite** as the startup project and select **Debug / Any CPU**.

Open PowerShell at the repository root (the directory containing `SoundBite.sln`) and run:

```powershell
Set-Location 'C:\path\to\SoundBiteFinal'
dotnet --list-sdks
dotnet --version
dotnet restore SoundBite.sln
dotnet tool restore
dotnet build SoundBite.sln --no-restore --nologo
```

Replace the example checkout path. `dotnet --version` must report `9.0.319` from this directory. In Visual Studio, **Restore NuGet Packages** and **Build → Build Solution** provide the corresponding restore/build actions. Configure a database before starting the web application.

### 2. Create a local SQLite database

The committed default is `ConnectionStrings:DefaultConnection = Data Source=soundbite.db`. It is a relative path; using it can select the repository database. For assessment, use an explicit absolute path outside the repository instead. Do not rely on or distribute existing private database contents.

Create a new external database using the existing migrations. This creates the schema, not sample users, music or reviews. Register accounts in the app and configure Spotify to import music if the catalogue is empty. Do not create a new migration for installation.

```powershell
$env:DOTNET_ENVIRONMENT = 'Development'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
Remove-Item Env:SOUNDBITE_CONFIG_FILE -ErrorAction SilentlyContinue
$dataDir = Join-Path $env:LOCALAPPDATA 'SoundBiteLocal\assessment-01'
New-Item -ItemType Directory -Path $dataDir -Force | Out-Null
$env:ConnectionStrings__DefaultConnection = "Data Source=$dataDir\soundbite-local.db"
dotnet ef database update --project SoundBite/SoundBite.csproj --startup-project SoundBite/SoundBite.csproj --connection "$env:ConnectionStrings__DefaultConnection"
```

Choose an unused path for a fresh installation. Normal web startup **does not automatically apply migrations**. This revision contains **15 migrations**, ending with `20261018124608_AddAlbumCoverUrl`. Apply the entire migration chain; the historical filenames and schema changes are intentional.

For an existing personal database, stop the app and back it up before applying updates. The migration chain includes a data conversion that replaces older collection tables with `FavouriteTracks` and removes obsolete related interactions. It is not a lossless upgrade of all historical data. Fresh-database migration was verified separately; no existing submission database was migrated during README verification.

### 3. Start locally

From the same configured PowerShell session:

```powershell
dotnet dev-certs https --trust
dotnet run --project SoundBite --launch-profile https
```

Approve the local development certificate prompt if shown. Open [https://localhost:7020](https://localhost:7020); the profile also binds HTTP port 5147. Stop the process with **Ctrl+C**.

For **Visual Studio F5**, the app must receive the same database connection string. A convenient persistent local setting is:

```powershell
dotnet user-secrets set 'ConnectionStrings:DefaultConnection' "$env:ConnectionStrings__DefaultConnection" --project SoundBite/SoundBite.csproj
```

Run this only after the database setup above has set the intended connection string. The project already has a `UserSecretsId`; no `user-secrets init` is needed. This stores the setting outside the repository and loads it in Development. Select the **https** project launch profile in Visual Studio, then press **F5** (or **Ctrl+F5** without debugging).

Environment variables override user secrets. If Visual Studio inherited an older `ConnectionStrings__DefaultConnection`, close it and relaunch with that override cleared or corrected. A variable set in a terminal does not change an already-running Visual Studio process. Leave `SOUNDBITE_CONFIG_FILE` unset for this workflow: it is an optional external deployment configuration source, not a local prerequisite.

## Spotify API Configuration

Spotify is **optional for running SoundBite with an already populated local catalogue**. Registration, local search, reviews, ratings, likes, favourites, following, comments, notifications and local rankings do not need API credentials.

Credentials are required for Spotify artist search when local search finds no matches, importing an artist's albums/tracks, the Favourites search fallback, and the optional `--seed-catalogue` and `--refresh-album-covers` commands. Without credentials, a local search miss can show **“Spotify artist search failed. Please try again later.”** Existing local records remain usable. SoundBite does not stream music or sign users in through Spotify; external Spotify links are separate from API access.

1. Create your own application through the [Spotify developer dashboard](https://developer.spotify.com/dashboard), following Spotify's [app setup documentation](https://developer.spotify.com/documentation/web-api/concepts/apps). Obtain its client ID and client secret privately. API availability depends on Spotify's current developer access requirements.
2. In Visual Studio, right-click the **SoundBite** project and select **Manage User Secrets**. Merge these keys into the local JSON, preserving the database setting if present:

   ```json
   {
     "Spotify": {
       "ClientId": "YOUR_CLIENT_ID",
       "ClientSecret": "YOUR_CLIENT_SECRET"
     }
   }
   ```

   Replace the placeholders only in the private local secrets file. Alternatively, supply environment variables named `Spotify__ClientId` and `Spotify__ClientSecret` to the app process. Do not place credentials in tracked `appsettings` or launch settings, screenshots or the README.
3. Restart the application. Search for an artist absent from the local catalogue, sign in, and use **Add artist** on a Spotify result.

The server implements Spotify's [Client Credentials flow](https://developer.spotify.com/documentation/web-api/tutorials/client-credentials-flow). SoundBite has no Spotify user-login callback or redirect-URI configuration setting.

For an optional batch import into your chosen external database, after migrations and credentials are configured:

```powershell
dotnet run --project SoundBite --launch-profile https -- --seed-catalogue --seed-query 'Miles Davis'
```

Run this in the session with the absolute `ConnectionStrings__DefaultConnection` override. This imports catalogue records and then exits; it does not create reviews, start the website or apply migrations. Omit `--seed-query` to use the predefined queries. Live Spotify import was not tested during README verification. An optional `--refresh-album-covers` run fills missing artwork for existing Spotify albums; it also requires an external absolute database path and cannot be combined with seeding.

## How to Use SoundBite

1. **Register and log in:** use `/register` with a username, unique email and confirmed password. Identity requires at least eight characters and its default uppercase, lowercase, digit and non-alphanumeric requirements. Use `/login` with username or email. Edit display name/bio and sign out through `/account`.
2. **Discover music:** open `/search` (also `/catalogue/search`), search the local artists, albums and tracks, then open a result. Spotify fallback is used only when local search returns no results.
3. **Rate and review:** open a track and sign in. Select a rating from 1–5; written text is optional. Saving again edits your one review for that track.
4. **Save favourites:** use **Add to Favourites** on a search result or track detail page. Open `/favourites` to view/remove saved tracks or use **Add Tracks** to search. Each user can save a track once; saving and removing favourites requires sign-in. These saved tracks are also visible on your public profile.
5. **Like tracks and interact:** **Like/Unlike** on a track is independent of saving it as a favourite. Like reviews and comments, add comments or one-level replies to reviews, and edit/delete your own comments. Visit `/feed` for social activity.
6. **Find listeners and follow:** open `/listeners` to search usernames (at least two characters) or discover people who reviewed music in the last 30 days. Open `/users/{username}` to see a listener's display name, bio, favourites, reviews and follower/following lists, and follow/unfollow them when signed in. Account emails are not shown on public profiles.
7. **Notifications:** open `/notifications` to view notifications and manage read status. At `/notifications/settings`, choose which categories should generate future notifications; existing notifications are retained.
8. **Rankings:** visit `/rankings` for top-rated tracks (at least two reviews), most-reviewed tracks and seven-day trends. Rankings use **community reviews only**, not Spotify charts, track likes, favourites or sample reviews. Fresh databases need community reviews before these lists populate.

**Administration:** `/admin/users` lets administrators search users, lock/unlock accounts and manage administrator roles. Ordinary registration does not grant administrator access. The initial administrator must be an existing registered user whose Identity ID is supplied through trusted local `Admin:UserIds` configuration (environment form: `Admin__UserIds__0`). Obtain the ID from your own local database, configure it privately, then restart and sign in again. This is optional for ordinary assessment; no default administrator password is supplied. An unknown configured ID causes startup to fail, so clear stale administrator settings when changing databases.

## Running Tests

In Visual Studio, build the solution, open **Test → Test Explorer**, and select **Run All Tests**. The NUnit adapter is already referenced by the test project; no separate NUnit extension is required.

From the repository root, after the restore/build steps above:

```powershell
dotnet test SoundBite.Tests/SoundBite.Tests.csproj --no-build --nologo
```

Omit `--no-build` if source has changed since the last build. Tests cover authentication, persistence/migrations, catalogue behaviour, reviews, favourites, following, likes/comments, feeds, notifications, rankings, listener discovery, public profiles and administration. Database tests use isolated temporary or in-memory SQLite databases; Spotify tests use simulated responses rather than requiring live credentials.

### Verification record

Verified on **19 October 2026** in a separate temporary checkout of **`cef1f7ae957e04148116ec91a19ceaa7602c9744`**, using SDK 9.0.319 on macOS:

- Solution restore, local tool restore and Debug build: passed with **0 errors and 3 existing CS1998 warnings** in test code.
- Full NUnit suite for this exact revision: **313 passed, 0 failed, 0 skipped**.
- All **15 migrations** applied successfully to a new external temporary SQLite database. SQLite integrity check returned `ok`, with no foreign-key violations. EF reported no pending model changes.
- The HTTPS launch profile started successfully against that external database. `/`, `/search`, `/favourites`, `/listeners`, `/rankings`, `/register` and `/login` returned HTTP 200. These are initial-response smoke checks, not interactive browser acceptance tests; the HTTP client bypassed certificate validation, so this does not verify certificate trust.
- No checks were run against the existing working tree's database. Previous test totals from older branches do not validate this revision.
- Still to verify on Windows: SDK installation, solution load, PowerShell setup, certificate trust, Test Explorer discovery, F5 and browser interactions. Live Spotify credentials/import also require separate verification.

## Troubleshooting

| Symptom | Action |
| --- | --- |
| “A compatible .NET SDK was not found” | Install SDK **9.0.319** and reopen the terminal/IDE. Run `dotnet --list-sdks` and `dotnet --version` at the repository root. `global.json` prevents substitution by other installed SDK versions. |
| Visual Studio cannot load/build `net9.0` | Verify Visual Studio 2022 **17.14**, the ASP.NET/web workload, and the exact SDK. CLI success alone does not establish IDE compatibility. |
| NuGet restore or tool restore fails | Check network/proxy access to NuGet and retry `dotnet restore SoundBite.sln` and `dotnet tool restore` from the root. Preserve the pinned package versions. |
| `dotnet ef` is not found | Run `dotnet tool restore` where `.config/dotnet-tools.json` is in scope. The documented commands use the local tool. |
| “no such table”, missing columns or catalogue load failures | Confirm the actual absolute connection string. Apply all migrations using the external connection string above. Normal web startup does not migrate the database. Do not delete the supplied database or historical migrations. |
| “unable to open database file” | Ensure the parent directory exists and is writable, and use an absolute path without semicolons. Check that Visual Studio receives the same setting as the terminal. |
| Database locked | Stop duplicate app instances/debug sessions and close tools holding the database, then retry. Keep transaction journal files intact. |
| HTTPS certificate warning | Run `dotnet dev-certs https --trust`, accept the prompt and restart the browser. Certificate trust still needs Windows verification. |
| Port 7020 or 5147 is already in use | Stop the previous SoundBite process/debug session and retry the documented profile. |
| Spotify search/import fails | Check both private credential keys, restart, and check network/API access and provider restrictions. A signed-in account is required to import. Existing local records remain usable while live access is unavailable; a fresh database needs music imported first. Never paste tokens or secrets into error reports. |
| App fails on an administrator ID or external config file | Remove stale local `Admin:UserIds` settings or correct them for the selected database. Leave `SOUNDBITE_CONFIG_FILE` unset unless deliberately using your own valid external configuration. |
| No tests in Test Explorer | Restore and build the full solution, check Test Explorer filters and confirm the test SDK/NUnit adapter restored. Windows test discovery remains to be verified. |
| Interactive controls stop responding | Keep the local server running; Blazor Interactive Server requires its connection. Retry/reload after restarting the server and use the same localhost URL. |
