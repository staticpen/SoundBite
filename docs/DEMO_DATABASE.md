# Reproducible demonstration database

This opt-in console tool creates synthetic coursework data in a **new directory outside the repository**. It never uses the application's default connection string, edits migrations, or touches `SoundBite/soundbite.db`. The web application does not reference or invoke the tool. It refuses an explicitly configured `Production` environment.

## Requirements and creation

Run from the repository root with the pinned **.NET SDK 9.0.319** installed (`global.json` disables roll-forward). NuGet access is required for the first restore. The tool references the application's pinned **EF Core 9.0.21** and calls `Database.MigrateAsync()` against the explicit external database, applying all **13 existing migrations**, including `AddNotificationOptOuts` and `AddDemoReviews`. No separate globally installed EF tool is needed.

Choose a new physical directory under your private user profile, outside any repository or shared/cloud-synchronised directory. Symbolic links and Windows junctions are rejected. The final directory must not already exist; its parent can exist. A completed demo can be passed again safely.

macOS (bash/zsh; replace the checkout path with your own):

```sh
cd "/path/to/SoundBiteFinal"
export DOTNET_ENVIRONMENT=Development
export ASPNETCORE_ENVIRONMENT=Development
DEMO_DIR="$HOME/.local/share/SoundBiteDemo/rehearsal-01"
dotnet run --project tools/SoundBite.Demo -c Release -- --create-demo --directory "$DEMO_DIR"
export ConnectionStrings__DefaultConnection="Data Source=$DEMO_DIR/soundbite-demo.db"
dotnet run --project SoundBite --launch-profile https
```

Windows PowerShell:

```powershell
Set-Location 'C:\path\to\SoundBiteFinal'
$env:DOTNET_ENVIRONMENT = 'Development'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$demoDir = Join-Path $env:LOCALAPPDATA 'SoundBiteDemo\rehearsal-01'
dotnet run --project tools/SoundBite.Demo -c Release -- --create-demo --directory $demoDir
$env:ConnectionStrings__DefaultConnection = "Data Source=$demoDir\soundbite-demo.db"
dotnet run --project SoundBite --launch-profile https
```

Use an ordinary path without semicolons (connection-string separators). Trust the local HTTPS development certificate if prompted/needed (`dotnet dev-certs https --trust`). The HTTPS launch profile serves `https://localhost:7020`.

For **Visual Studio 2022 17.14**, install the ASP.NET/web workload and the exact SDK. Open `SoundBite.sln` and select SoundBite as the startup project. Start Visual Studio from the configured PowerShell session so it inherits `ConnectionStrings__DefaultConnection`, or set that environment variable through your private local debug configuration. Restart any already-running Visual Studio instance after changing its environment. Do not commit the local connection override. The console tool need not be added to the solution; its project is runnable directly.

## Contents and accounts

- 10 fictional artists, 15 fictional albums, 40 fictional tracks; Spotify IDs intentionally remain null.
- Three ordinary (non-admin) users: `demo.alex`, `demo.sam`, `demo.jamie`; emails use `example.invalid`.
- 90 reviews with ratings from 2 to 5, six public playlists with eight tracks each, six follows.
- 36 comments including one-level replies, 60 likes across reviews/tracks/comments/playlists, 84 notifications including read and unread examples.
- Feeds are derived by the existing application service from reviews, comments, likes and follows; there is no separate activity table to seed.
- The newer `DemoReviews` table is created but intentionally empty: offline users have ordinary user-linked reviews so social interactions continue to work. Main's rankings combine both tables and remain populated without Spotify sample reviews.
- Every track has at least two reviews; recent timestamps populate top-rated, most-reviewed and seven-day trending lists.

Names and content are synthetic. Only passwords and Identity security values are random; timestamps are relative to creation time. No Spotify credentials or live API access are needed.

**Passwords are generated independently for each run and saved only in `credentials.json` beside the external database.** The tool prints paths, never passwords. Open that file privately to obtain each account's password; do not paste it into logs, screenshots, source control or submission files. The database contains Identity password hashes, not plaintext passwords. On Unix the directory is owner-only (0700) and credentials file is owner read/write (0600). On Windows the directory inherits its parent's ACL: use your private `%LOCALAPPDATA%` and verify access is limited to your account and system administrators. Keep both files private. Do not reuse these credentials elsewhere.

## Rerun and safe reset

Repeat the same setup command against a completed demo: it reports that the existing database and passwords were preserved after checking migration history through a **read-only SQLite connection**. It does not apply migrations, reseed or change passwords on a rerun. A completion marker (`demo-manifest.txt`) and credentials/database files identify a completed demo. Missing files, incomplete runs, or unrelated existing directories are rejected rather than repaired or overwritten.

An old 11-migration demo is rejected with the missing migration names and instructions to create a **new external directory**. A database with unknown migrations is also rejected. The old database and credentials remain unchanged. Creation always uses the migrations in the current checkout; update the checkout before creating a new demo. Stop SoundBite before checking or resetting a demo. The check uses immutable read-only access to avoid creating SQLite WAL/SHM files. Nonempty transaction journals are rejected: shut the application down cleanly, or create a new demo directory; do not delete journal files manually.

To reset safely:

1. Stop SoundBite (and its debugger).
2. Choose a **new** directory, e.g. `rehearsal-02`, and run the same creation command with that path.
3. Point `ConnectionStrings__DefaultConnection` to the new `soundbite-demo.db`; restart the application/Visual Studio.
4. Use the new private credentials file. Keep the old directory as a backup until the new demo is checked. Delete only that old external directory manually when no longer needed.

This reset never deletes an existing database, and also refreshes timestamps. After seven days, trending results naturally age out: reset into a new directory before presenting. If setup fails, preserve the failed directory for diagnosis and choose another directory for the retry. Do not remove its marker/lock to force adoption.

Do not copy a live SQLite database without its transaction state. Stop the application before backing up the complete external directory. Do not point a production deployment at demo data.

## Verification and presentation

Focused automated checks:

```sh
dotnet test SoundBite.Tests/SoundBite.Tests.csproj -c Release --filter "FullyQualifiedName~DemoDatabaseTests|FullyQualifiedName~CataloguePersistenceTests|FullyQualifiedName~RankingAndTrendServiceTests"
```

The demo tests create and remove their own external temporary directories. They check all 13 migrations, rejection of an unchanged 11-migration demo, counts, relationships, SQLite integrity/foreign keys, unchanged reruns, non-destructive resets, path/production guards, generated logins and server-rendered pages for all three users. They do not use the tracked application database.

Suggested short demo:

1. Sign in as any demo user using the private credentials file.
2. At `/catalogue/search`, search `Harbour`; inspect the artist, album and track details.
3. Open `/rankings` for all three populated ranking sections.
4. Open `/playlists` and `Evening Harbour Walk` to show its eight tracks.
5. Open `/feed`, `/notifications` and `/users/demo.alex` for social activity, unread notifications and follows.

The team's `--seed-catalogue` command is a separate live Spotify workflow; do not run it for this offline dataset. This tool does not load `SOUNDBITE_CONFIG_FILE` or production secrets. When starting the web application, clear that variable if it points at a deployment configuration and always set the explicit demo connection override.

Live Spotify validation and Windows-specific F5/reconnection checks remain separate tasks. Admin provisioning is not enabled by this tool; clear stale local `Admin:UserIds` configuration when using a fresh demo, since nonexistent administrator IDs cause application startup to fail.
