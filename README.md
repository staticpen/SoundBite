# SoundBite

SoundBite is a social music discovery web app, built for UTS 31927 Application Development with .NET (Assignment 2).

- **Markers:** you only need the submitted ZIP. Start at [Getting Started](#getting-started).
- **Hosted version:** see [Tailscale Access](#tailscale-access).
- **Team only:** server hosting is covered in [Deployment](#deployment-team-only).

## Contents

- [Project Overview](#project-overview)
- [Main Features](#main-features)
- [Technology Stack](#technology-stack)
- [Getting Started](#getting-started)
- [Database Setup](#database-setup)
- [Tailscale Access](#tailscale-access)
- [Additional Configuration](#additional-configuration)
- [Testing](#testing)
- [Deployment (team only)](#deployment-team-only)
- [Troubleshooting](#troubleshooting)

## Project Overview

SoundBite lets listeners discover artists, albums and tracks, rate and review music, save favourites, and follow other listeners. It is a .NET 9 Blazor web app that stores its data in a local SQLite database.

Spotify is optional. SoundBite uses it only to import music that isn't already in the local catalogue.

## Main Features

- **Accounts:** register, sign in with a username or email, edit your profile, and sign out.
- **Search:** search the local catalogue at `/search`. If nothing matches, signed-in users can find the artist on Spotify and click **Add artist** to import their albums and tracks.
- **Ratings and reviews:** rate tracks from 1 to 5 stars, with an optional written review. Each user has one review per track and can edit it.
- **Favourites:** save tracks to your Favourites (`/favourites`). They also appear on your public profile.
- **Social:** like tracks, reviews and comments; comment on reviews and reply to comments. Signed-in users see an activity feed on the Home page.
- **Listeners:** find other listeners (`/listeners`), view their profiles (`/users/{username}`) and follow them.
- **Notifications:** in-app notifications (`/notifications`), with settings to choose which kinds you receive.
- **Rankings:** top-rated, most-reviewed and trending tracks (`/rankings`), based on SoundBite reviews.
- **Administration:** administrators can manage users and roles at `/admin/users`.

## Technology Stack

| Area | Technology |
| --- | --- |
| Language and framework | C# 13, .NET 9, ASP.NET Core Blazor Web App (Interactive Server) |
| Data | SQLite with Entity Framework Core 9.0.21 |
| Accounts | ASP.NET Core Identity 9.0.21 |
| UI | Razor components, Bootstrap, custom CSS |
| Music data | Spotify Web API (optional) |
| Testing | NUnit 4.3.2 |

## Getting Started

These steps are for running SoundBite on your own computer from the submitted ZIP. You don't need Git, GitHub or Tailscale. Spotify credentials aren't required to build, launch or run the tests.

SoundBite has been tested on Windows with Visual Studio 2022.

### Prerequisites

- **Windows** with **Visual Studio 2022 version 17.14** and the **ASP.NET and web development** workload.
- **.NET SDK 9.0.319** exactly ([.NET 9 downloads](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)). The project's `global.json` requires this version. It can be installed alongside other SDKs.
- An internet connection the first time you build, to download NuGet packages.

### Step 1: Extract and open

1. Extract the whole ZIP to a folder, for example `C:\path\to\SoundBiteFinal`. Use **Extract All** rather than running from inside the ZIP.
2. Check that the folder contains `SoundBite.sln`, `global.json` and the hidden `.config` folder.
3. In Visual Studio 2022, choose **File → Open → Project/Solution** and open `SoundBite.sln`.
4. Set **SoundBite** as the startup project.

### Step 2: Restore and build

Open PowerShell in the extracted folder and run:

```powershell
Set-Location 'C:\path\to\SoundBiteFinal'
dotnet --list-sdks
dotnet --version
dotnet restore SoundBite.sln
dotnet tool restore
dotnet build SoundBite.sln --no-restore --nologo
```

`dotnet --version` should print `9.0.319`. You can also build from Visual Studio with **Build → Build Solution**.

### Step 3: Create the database

In the **same PowerShell window**, run:

```powershell
$env:DOTNET_ENVIRONMENT = 'Development'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
Remove-Item Env:SOUNDBITE_CONFIG_FILE -ErrorAction SilentlyContinue
$dataDir = Join-Path $env:LOCALAPPDATA 'SoundBiteLocal\assessment-01'
New-Item -ItemType Directory -Path $dataDir -Force | Out-Null
$env:ConnectionStrings__DefaultConnection = "Data Source=$dataDir\soundbite-local.db"
dotnet ef database update --project SoundBite/SoundBite.csproj --startup-project SoundBite/SoundBite.csproj --connection "$env:ConnectionStrings__DefaultConnection"
```

This creates an empty database outside the project folder. See [Database Setup](#database-setup) for details.

### Step 4: Run the app

**From PowerShell**, in the same window:

```powershell
dotnet dev-certs https --trust
dotnet run --project SoundBite --launch-profile https
```

Accept the certificate prompt if it appears. Then open <https://localhost:7020>. The app also listens on `http://localhost:5147`. Press **Ctrl+C** to stop it.

**From Visual Studio (F5):** Visual Studio needs to know where the database is. Run this once, in the same PowerShell window as Step 3:

```powershell
dotnet user-secrets set 'ConnectionStrings:DefaultConnection' "$env:ConnectionStrings__DefaultConnection" --project SoundBite/SoundBite.csproj
```

Then select the **https** launch profile and press **F5**, or **Ctrl+F5** to run without debugging.

### Step 5: First use

1. Register an account at `/register`. Passwords need at least 8 characters, including an uppercase letter, a lowercase letter, a number and a symbol.
2. A new database has no music. Before using catalogue-dependent features such as ratings, reviews, favourites and rankings, set up [Spotify](#spotify-optional) and import an artist, unless you already have access to an authorised populated catalogue.

### macOS / Linux

You need .NET SDK 9.0.319. From the extracted folder, run:

```sh
mkdir -p "$HOME/.local/share/SoundBite"
export ConnectionStrings__DefaultConnection="Data Source=$HOME/.local/share/SoundBite/soundbite.db"
dotnet tool restore
dotnet tool run dotnet-ef database update --project SoundBite/SoundBite.csproj
dotnet run --project SoundBite/SoundBite.csproj --launch-profile https
```

If the browser doesn't trust the certificate, run `dotnet dev-certs https --trust`.

## Database Setup

SoundBite uses SQLite through Entity Framework Core. You don't need a separate database server.

- **Where the database lives:** `ConnectionStrings__DefaultConnection` sets the location, through an environment variable or User Secrets. Always use a full path outside the project folder, as in Step 3. Without it, the app falls back to `Data Source=soundbite.db` and may use the `SoundBite/soundbite.db` file that ships with the project. Don't use or share that file.
- **Migrations aren't applied automatically.** Run the `dotnet ef database update` command from Step 3 (Windows) or the macOS/Linux commands above. It builds the full schema from the project's 15 migrations. The database starts empty, with no users, music or reviews.
- **After code changes** that add a migration, run the same update command again.
- **Don't create new migrations** to install the app.
- **Upgrading an old database:** back it up first. One migration replaces the old Playlists feature with Favourites, and playlist names and ordering are lost.

## Tailscale Access

Our team hosts a running copy of SoundBite on a Mac called `mair`. It is private: only devices on our Tailscale network (tailnet) can reach it.

**Link:** <https://mair.tailae22d2.ts.net/>

### How to connect

1. Ask the SoundBite tailnet administrator to invite you to the tailnet, or to share the `mair` machine with your Tailscale account. The link alone doesn't give you access.
2. Install Tailscale from [tailscale.com/download](https://tailscale.com/download) and sign in with the account the administrator approved. If the machine was shared with you, accept the invitation while signed in to that account.
3. Check that Tailscale says **Connected** and that `mair` appears in your device list.
4. Open the link in Safari, Chrome, Edge or Firefox. To rate and review, sign in with a SoundBite account; this is separate from your Tailscale account.

### If the link doesn't open

1. Make sure Tailscale says **Connected** and lists `mair`. If `mair` is missing, check that you accepted the invitation with the right account.
2. In Windows PowerShell, run:

   ```powershell
   tailscale status
   tailscale ping mair.tailae22d2.ts.net
   Resolve-DnsName mair.tailae22d2.ts.net
   Test-NetConnection mair.tailae22d2.ts.net -Port 443
   ```

   `tailscale status` should list `mair`, and the last command should show `TcpTestSucceeded : True`.
3. Use the exact HTTPS link. Don't switch to an IP address or `http://`, and don't click past a browser security warning.
4. If it still fails, send the administrator the output of `tailscale status` and `Test-NetConnection`. The host Mac must be on and running SoundBite.

No router port forwarding is needed. Never share SoundBite or Tailscale passwords in messages.

## Additional Configuration

### Spotify (optional)

Spotify is optional for building, launching and testing SoundBite. Credentials are normally needed to add music to a fresh database using **Add artist** or the commands below. Without credentials, a search with no local results may show "Spotify artist search failed. Please try again later."

1. Use a Spotify Premium account to create an app in the [Spotify developer dashboard](https://developer.spotify.com/dashboard). Select **Web API**, then copy the client ID and client secret.
2. Save them using one of these options:

   **Option A: PowerShell or a terminal**, from the extracted folder (required on the team's deployment host):

   ```sh
   dotnet user-secrets set "Spotify:ClientId" "<your-client-id>" --project SoundBite/SoundBite.csproj
   dotnet user-secrets set "Spotify:ClientSecret" "<your-client-secret>" --project SoundBite/SoundBite.csproj
   ```

   **Option B: Visual Studio.** Right-click the **SoundBite** project, choose **Manage User Secrets**, and add the following, keeping any existing settings:

   ```json
   {
     "Spotify": {
       "ClientId": "YOUR_CLIENT_ID",
       "ClientSecret": "YOUR_CLIENT_SECRET"
     }
   }
   ```

   **Option C: environment variables.** Set `Spotify__ClientId` and `Spotify__ClientSecret`.

3. Restart the app, sign in, search for an artist that isn't in the catalogue, and click **Add artist**.

Never put credentials in `appsettings.json`, launch settings or screenshots.

**Bulk import (optional).** In the PowerShell window from Step 3, run:

```powershell
dotnet run --project SoundBite --launch-profile https -- --seed-catalogue --seed-query 'Miles Davis'
```

This imports the music and then exits. Leave out `--seed-query` to use the built-in artist list, or repeat it to add more searches.

To fill in missing album artwork, run the same command with `--refresh-album-covers` in place of the seed options. You can't combine the two. Both commands require the full database path from Step 3.

### Administrator access (optional)

New accounts aren't administrators, and there is no default admin password. To make an existing account an administrator:

1. Find its user ID in your database.
2. Set it as `Admin:UserIds`, for example as the environment variable `Admin__UserIds__0`.
3. Restart the app and sign in again.

If the ID doesn't match a user in the database, the app won't start. Remove old admin settings when you switch databases.

### Other settings

- **User Secrets and environment variables:** User Secrets are stored outside the project folder and load when the app runs in Development, which the `https` launch profile sets. Environment variables take priority over User Secrets. If you change one in a terminal, restart Visual Studio for it to take effect.
- **`SOUNDBITE_CONFIG_FILE`:** leave it unset when running locally. Only the team's hosted deployment uses it.

## Testing

**Visual Studio:** build the solution, then open **Test → Test Explorer** and click **Run All Tests**.

**PowerShell or a terminal**, from the extracted folder:

```powershell
dotnet test SoundBite.Tests/SoundBite.Tests.csproj --no-build --nologo
```

Leave out `--no-build` if you've changed code since the last build. The NUnit tests use temporary databases and simulated Spotify responses, so they don't need credentials or internet access.

## Deployment (team only)

> **Markers can skip this section.** It describes how our team runs the hosted version on a Mac. The scripts check the Git branch, so they work only in the team's Git working copy, not from the submitted ZIP.

### How hosting works

| Item | Value |
| --- | --- |
| Host | Mac `mair`, always on |
| App files | `~/Library/Application Support/SoundBite/app` |
| Database | `~/Library/Application Support/SoundBite/data/soundbite.db` |
| Backups and logs | `…/SoundBite/backups` and `…/SoundBite/logs` (`stdout.log`, `stderr.log`) |
| Spotify config | `…/SoundBite/config/spotify-production.json` (readable by the owner only) |
| Background service | `/Library/LaunchDaemons/com.staticpen.soundbite.plist`. The scripts also support the older per-user `~/Library/LaunchAgents/com.staticpen.soundbite.plist`. |
| Service settings | `ASPNETCORE_ENVIRONMENT=Production`, `ASPNETCORE_URLS=http://127.0.0.1:5081`, `ConnectionStrings__DefaultConnection`, `Admin__UserIds__0`, `SOUNDBITE_CONFIG_FILE` |
| Tailscale Serve | `https://mair.tailae22d2.ts.net/` → `http://127.0.0.1:5081`. Check with `tailscale serve status`. |

The app only accepts connections from the Mac itself; Tailscale Serve passes outside requests to it over HTTPS.

### Deploy a new version

From an up-to-date `main` branch with nothing uncommitted, run:

```sh
./scripts/deploy-self-hosted-macos.sh
```

Use an interactive terminal, because the script asks for the Mac's password. It:

1. Runs the Release tests (`dotnet test SoundBite.sln --configuration Release`).
2. Publishes the app.
3. Swaps in the new version and keeps the previous one as a backup.
4. Checks that the site responds. If not, it restores the previous version automatically.

The script **does not update the database**. If a release adds migrations:

1. Stop the running SoundBite service.
2. Back up the database to `backups/`.
3. Check the backup with `PRAGMA integrity_check`.
4. Run the `dotnet ef database update` command with the connection string set to the database path in the table above.
5. Restart the service and check that the application responds.

### Existing host setup (already done)

These scripts support the team's existing host and are not a complete fresh-server installation procedure. They rely on the published app and legacy LaunchAgent prerequisites listed below.

```sh
python3 scripts/import-production-spotify-secrets.py
./scripts/install-self-hosted-macos-daemon.sh
```

- The **Python script** copies the Spotify credentials from User Secrets into the protected config file. It needs them saved with the `dotnet user-secrets set` commands (Option A), not the Visual Studio method.
- The **install script** sets SoundBite up as a background service that starts before anyone logs in. It runs the Python script itself. Run it as your normal user, not with `sudo`. It needs:
  - the app already published to `app/`
  - the older LaunchAgent loaded, with `Admin__UserIds__0` set
  - no existing LaunchDaemon

**Rules:**

- Never deploy the `SoundBite/soundbite.db` file that ships with the project.
- Keep the database, backups, Spotify config and admin ID out of Git.
- Keep router port forwarding turned off.

## Troubleshooting

| Problem | Solution |
| --- | --- |
| "A compatible .NET SDK was not found" | Install SDK **9.0.319**, reopen Visual Studio or PowerShell, and check with `dotnet --version`. |
| Visual Studio can't open or build the project | Make sure you have Visual Studio 2022 **17.14** with the ASP.NET and web development workload. |
| `dotnet ef` not found | Make sure the hidden `.config` folder was extracted, then run `dotnet tool restore`. |
| "no such table" or pages fail to load | The database hasn't been set up. Run the Step 3 command, and check that the app uses the same connection string. |
| "unable to open database file" | Use a full path to a folder that exists and that you can write to. |
| Database locked | Close any other running copies of SoundBite and retry. |
| Pages have no styling | Run with the `https` launch profile, or set `ASPNETCORE_ENVIRONMENT=Development`. |
| Certificate warning on localhost | Run `dotnet dev-certs https --trust`, then restart the browser. |
| Port 7020 or 5147 already in use | Stop the other running copy of SoundBite. |
| Spotify search or import fails | Check both credentials, restart the app, and make sure you're signed in. |
| App won't start after configuring an admin | Remove or fix the `Admin:UserIds` setting. Leave `SOUNDBITE_CONFIG_FILE` unset. |
| No tests in Test Explorer | Rebuild the solution and clear any Test Explorer filters. |
| Buttons stop responding | Keep the app running. Restart it if needed, then reload the page. |
| Hosted link won't open | See [If the link doesn't open](#if-the-link-doesnt-open). |
| Deploy script refuses to run (team only) | Run it from the team's Git copy on `main`, with all changes committed. |
| Deployment fails its health check (team only) | It rolls back automatically. Check `logs/stderr.log`. |
