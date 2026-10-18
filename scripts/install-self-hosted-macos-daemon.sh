#!/bin/bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
APP_SUPPORT="$HOME/Library/Application Support/SoundBite"
APP_DIR="$APP_SUPPORT/app"
CONFIG_FILE="$APP_SUPPORT/config/spotify-production.json"
USER_AGENT="$HOME/Library/LaunchAgents/com.staticpen.soundbite.plist"
SYSTEM_AGENT="/Library/LaunchDaemons/com.staticpen.soundbite.plist"
LABEL="com.staticpen.soundbite"
USER_DOMAIN="gui/$(id -u)"
TIMESTAMP="$(date '+%Y%m%d-%H%M%S')-$$"
AGENT_BACKUP="$APP_SUPPORT/backups/com.staticpen.soundbite.launchagent-$TIMESTAMP.plist"
TEMP_PLIST=""

fail() {
    printf 'LaunchDaemon setup failed: %s\n' "$1" >&2
    exit 1
}

cleanup() {
    if [[ -n "$TEMP_PLIST" && -f "$TEMP_PLIST" ]]; then
        rm -f -- "$TEMP_PLIST"
    fi
}
trap cleanup EXIT

cd "$REPO_ROOT"

if [[ "$(git branch --show-current)" != "main" ]]; then
    fail "check out main before installing"
fi

if [[ -n "$(git status --porcelain)" ]]; then
    fail "commit all changes before installing"
fi

[[ "$(id -u)" -ne 0 ]] || fail "run this script as your normal account, not through sudo"
[[ -d "$APP_DIR" && -f "$APP_DIR/SoundBite.dll" ]] ||
    fail "publish the updated app before installing the LaunchDaemon"
[[ -f "$USER_AGENT" ]] || fail "the existing LaunchAgent plist is missing"
[[ ! -e "$SYSTEM_AGENT" ]] || fail "a system LaunchDaemon plist already exists"
launchctl print "$USER_DOMAIN/$LABEL" >/dev/null 2>&1 ||
    fail "the current LaunchAgent is not loaded"
command -v dotnet >/dev/null || fail "dotnet was not found in PATH"
DOTNET_BIN="$(command -v dotnet)"
[[ -x "$DOTNET_BIN" ]] || fail "dotnet executable is not available: $DOTNET_BIN"
ADMIN_USER_ID="$(/usr/libexec/PlistBuddy -c 'Print :EnvironmentVariables:Admin__UserIds__0' "$USER_AGENT")" ||
    fail "could not read the configured administrator ID"
[[ -n "$ADMIN_USER_ID" ]] || fail "the configured administrator ID is empty"

python3 "$SCRIPT_DIR/import-production-spotify-secrets.py"
mkdir -p "$APP_SUPPORT/backups" "$APP_SUPPORT/logs"
chmod 700 "$APP_SUPPORT/backups" "$APP_SUPPORT/logs"

TEMP_PLIST="$(mktemp "${TMPDIR:-/tmp}/com.staticpen.soundbite.XXXXXX")"
export APP_DIR APP_SUPPORT CONFIG_FILE DOTNET_BIN ADMIN_USER_ID
python3 - "$TEMP_PLIST" <<'PY'
import os
import plistlib
import pwd
import sys

username = pwd.getpwuid(os.getuid()).pw_name
app_support = os.environ["APP_SUPPORT"]
plist = {
    "Label": "com.staticpen.soundbite",
    "ProgramArguments": [
        os.environ["DOTNET_BIN"],
        os.path.join(os.environ["APP_DIR"], "SoundBite.dll"),
    ],
    "WorkingDirectory": os.environ["APP_DIR"],
    "UserName": username,
    "RunAtLoad": True,
    "KeepAlive": True,
    "EnvironmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Production",
        "ASPNETCORE_URLS": "http://127.0.0.1:5081",
        "ConnectionStrings__DefaultConnection": (
            f"Data Source={app_support}/data/soundbite.db"
        ),
        "Admin__UserIds__0": os.environ["ADMIN_USER_ID"],
        "SOUNDBITE_CONFIG_FILE": os.environ["CONFIG_FILE"],
    },
    "StandardOutPath": os.path.join(app_support, "logs", "stdout.log"),
    "StandardErrorPath": os.path.join(app_support, "logs", "stderr.log"),
}
with open(sys.argv[1], "wb") as output:
    plistlib.dump(plist, output)
PY
plutil -lint "$TEMP_PLIST" >/dev/null || fail "generated LaunchDaemon plist is invalid"

printf 'Requesting administrator authorization to install the LaunchDaemon...\n'
sudo -v
sudo install -o root -g wheel -m 644 "$TEMP_PLIST" "$SYSTEM_AGENT" ||
    fail "could not install the system LaunchDaemon plist"

printf 'Stopping the user-session LaunchAgent...\n'
if ! launchctl bootout "$USER_DOMAIN" "$USER_AGENT"; then
    sudo rm -f -- "$SYSTEM_AGENT" ||
        fail "LaunchAgent did not stop and the unused system plist could not be removed"
    fail "could not stop the current LaunchAgent; it remains in place"
fi

if ! mv "$USER_AGENT" "$AGENT_BACKUP"; then
    launchctl bootstrap "$USER_DOMAIN" "$USER_AGENT" ||
        fail "could not preserve the LaunchAgent plist and it did not restart"
    sudo rm -f -- "$SYSTEM_AGENT" ||
        fail "LaunchAgent was restarted but the unused system plist could not be removed"
    fail "could not preserve the previous LaunchAgent plist"
fi

if ! sudo launchctl bootstrap system "$SYSTEM_AGENT"; then
    mv "$AGENT_BACKUP" "$USER_AGENT" ||
        fail "system LaunchDaemon did not load and the previous plist could not be restored"
    launchctl bootstrap "$USER_DOMAIN" "$USER_AGENT" ||
        fail "previous LaunchAgent plist was restored but it did not start"
    sudo rm -f -- "$SYSTEM_AGENT" ||
        fail "previous LaunchAgent was restored but the unused system plist could not be removed"
    fail "system LaunchDaemon did not load; previous LaunchAgent was restored"
fi

printf 'Waiting for the local app health check...\n'
for attempt in {1..60}; do
    if launchctl print "system/$LABEL" >/dev/null 2>&1 &&
        curl --silent --fail --output /dev/null http://127.0.0.1:5081/; then
        printf 'LaunchDaemon is running. Previous LaunchAgent plist retained at %s\n' "$AGENT_BACKUP"
        printf 'Spotify configuration is stored outside the app with mode 600.\n'
        exit 0
    fi
    sleep 1
done

printf 'Health check failed; restoring the LaunchAgent...\n' >&2
if ! sudo launchctl bootout system "$SYSTEM_AGENT"; then
    fail "health check failed and the system LaunchDaemon could not be stopped"
fi
mv "$AGENT_BACKUP" "$USER_AGENT" ||
    fail "health check failed and the previous LaunchAgent plist could not be restored"
launchctl bootstrap "$USER_DOMAIN" "$USER_AGENT" ||
    fail "previous LaunchAgent plist was restored but it did not start"
sudo rm -f -- "$SYSTEM_AGENT" ||
    fail "previous LaunchAgent was restored but the unused system plist could not be removed"
fail "health check failed; previous LaunchAgent was restored"
