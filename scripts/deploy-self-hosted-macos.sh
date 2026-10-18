#!/bin/bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
APP_SUPPORT="$HOME/Library/Application Support/SoundBite"
APP_DIR="$APP_SUPPORT/app"
USER_AGENT="$HOME/Library/LaunchAgents/com.staticpen.soundbite.plist"
SYSTEM_AGENT="/Library/LaunchDaemons/com.staticpen.soundbite.plist"
LABEL="com.staticpen.soundbite"
USER_DOMAIN="gui/$(id -u)"
TIMESTAMP="$(date '+%Y%m%d-%H%M%S')-$$"
BACKUP_DIR="$APP_SUPPORT/app-previous-$TIMESTAMP"
STAGING_DIR=""
SERVICE_KIND=""

service_bootout() {
    if [[ "$SERVICE_KIND" == "daemon" ]]; then
        sudo launchctl bootout system "$SYSTEM_AGENT"
    else
        launchctl bootout "$USER_DOMAIN" "$USER_AGENT"
    fi
}

service_bootstrap() {
    if [[ "$SERVICE_KIND" == "daemon" ]]; then
        sudo launchctl bootstrap system "$SYSTEM_AGENT"
    else
        launchctl bootstrap "$USER_DOMAIN" "$USER_AGENT"
    fi
}

fail() {
    printf 'Deployment failed: %s\n' "$1" >&2
    exit 1
}

cleanup() {
    if [[ -n "$STAGING_DIR" && -d "$STAGING_DIR" ]]; then
        rm -rf -- "$STAGING_DIR"
    fi
}
trap cleanup EXIT

cd "$REPO_ROOT"

if [[ "$(git branch --show-current)" != "main" ]]; then
    fail "check out main before deploying"
fi

if [[ -n "$(git status --porcelain)" ]]; then
    fail "commit all changes before deploying"
fi

command -v dotnet >/dev/null || fail "dotnet was not found in PATH"
[[ -d "$APP_DIR" ]] || fail "published app directory is missing: $APP_DIR"
if [[ -f "$SYSTEM_AGENT" ]]; then
    SERVICE_KIND="daemon"
    sudo -v
    sudo launchctl print "system/$LABEL" >/dev/null 2>&1 ||
        fail "system LaunchDaemon is not loaded; verify it before deploying"
elif [[ -f "$USER_AGENT" ]]; then
    SERVICE_KIND="agent"
    launchctl print "$USER_DOMAIN/$LABEL" >/dev/null 2>&1 ||
        fail "user LaunchAgent is not loaded; verify it before deploying"
else
    fail "SoundBite LaunchAgent/LaunchDaemon plist was not found"
fi

printf 'Running Release test suite...\n'
dotnet test SoundBite.sln --configuration Release

STAGING_DIR="$(mktemp -d "$APP_SUPPORT/.app-staging.XXXXXX")"
printf 'Publishing Release app...\n'
dotnet publish SoundBite/SoundBite.csproj \
    --configuration Release \
    --no-restore \
    --output "$STAGING_DIR"
[[ -f "$STAGING_DIR/SoundBite.dll" ]] || fail "publish output is missing SoundBite.dll"

printf 'Stopping SoundBite and retaining the current app at %s...\n' "$BACKUP_DIR"
service_bootout ||
    fail "could not stop the SoundBite service; no files were replaced"

if ! mv "$APP_DIR" "$BACKUP_DIR"; then
    if ! service_bootstrap; then
        fail "could not move the current app and the SoundBite service did not restart"
    fi
    fail "could not move the current app; existing app was restarted"
fi

if ! mv "$STAGING_DIR" "$APP_DIR"; then
    mv "$BACKUP_DIR" "$APP_DIR" ||
        fail "could not restore the previous app from $BACKUP_DIR"
    service_bootstrap ||
        fail "previous app was restored but the SoundBite service did not restart"
    fail "could not install the new app; previous app was restored"
fi
STAGING_DIR=""

if ! service_bootstrap; then
    mv "$APP_DIR" "$APP_SUPPORT/app-failed-$TIMESTAMP" ||
        fail "new app failed to start and could not be moved aside"
    mv "$BACKUP_DIR" "$APP_DIR" ||
        fail "new app failed to start and previous app could not be restored"
    service_bootstrap ||
        fail "previous app was restored but the SoundBite service did not restart"
    fail "new app failed to start; previous app was restored"
fi

printf 'Waiting for the local app health check...\n'
for attempt in {1..60}; do
    if curl --silent --fail --output /dev/null http://127.0.0.1:5081/; then
        printf 'Deployment succeeded. Previous app retained at %s\n' "$BACKUP_DIR"
        exit 0
    fi
    sleep 1
done

printf 'Health check failed; restoring the previous app...\n' >&2
service_bootout ||
    fail "health check failed and the new app could not be stopped; inspect LaunchAgent logs"
mv "$APP_DIR" "$APP_SUPPORT/app-failed-$TIMESTAMP" ||
    fail "health check failed and the new app could not be moved aside"
mv "$BACKUP_DIR" "$APP_DIR" ||
    fail "health check failed and the previous app could not be restored"
service_bootstrap ||
    fail "previous app was restored but the SoundBite service did not restart"
fail "health check failed; previous app was restored"
