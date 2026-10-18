# Running SoundBite locally

## Prerequisites

- Git
- The .NET SDK **9.0.319**. The repository pins this version in `global.json`.
- A browser

The application uses SQLite, so no separate database server is required.

## Get the code

```sh
git clone https://github.com/staticpen/SoundBite.git
cd SoundBite
```

If you already have the repository, run the remaining commands from its root
directory.

## Configure a local database

Keep your development database outside the repository. The checked-in
`SoundBite/soundbite.db` is not intended for local development.

### macOS or Linux

Run these commands in the terminal you will use to start the application:

```sh
mkdir -p "$HOME/.local/share/SoundBite"
export ConnectionStrings__DefaultConnection="Data Source=$HOME/.local/share/SoundBite/soundbite.db"
```

### Windows PowerShell

Run these commands in the PowerShell window you will use to start the
application:

```powershell
New-Item -ItemType Directory -Force "$env:LOCALAPPDATA\SoundBite" | Out-Null
$env:ConnectionStrings__DefaultConnection = "Data Source=$env:LOCALAPPDATA\SoundBite\soundbite.db"
```

The environment variable overrides the default database connection for the
current terminal session.

## Apply database migrations

The app does not apply EF Core migrations automatically. Restore the
repository's local .NET tool and update the database explicitly:

```sh
dotnet tool restore
dotnet tool run dotnet-ef database update --project SoundBite/SoundBite.csproj
```

Run these commands from the repository root, in the same terminal where you set
`ConnectionStrings__DefaultConnection`.

## Start the app

The HTTPS launch profile serves the app at `https://localhost:7020`:

```sh
dotnet run --project SoundBite/SoundBite.csproj --launch-profile https
```

If your browser does not trust the local development certificate, create and
trust it with:

```sh
dotnet dev-certs https --trust
```

Stop the app with **Ctrl+C**. You can create an account through the app's
registration page.

## Optional: enable Spotify features

Spotify-backed search and import features require a Spotify API client ID and
client secret. The app itself can run without these credentials, but features
that call Spotify will not be available.

Store credentials in .NET User Secrets rather than in a committed settings
file:

```sh
dotnet user-secrets set "Spotify:ClientId" "<your-client-id>" --project SoundBite/SoundBite.csproj
dotnet user-secrets set "Spotify:ClientSecret" "<your-client-secret>" --project SoundBite/SoundBite.csproj
```

User Secrets are loaded when the app runs with the `Development` environment,
which is set by the HTTPS launch profile.

## Run the tests

From the repository root:

```sh
dotnet test SoundBite.Tests/SoundBite.Tests.csproj
```

## Updating the database after code changes

When a change adds or updates an EF Core migration, keep the local database
schema up to date by running the migration command again:

```sh
dotnet tool run dotnet-ef database update --project SoundBite/SoundBite.csproj
```

Keep the database connection environment variable set in the same terminal.
