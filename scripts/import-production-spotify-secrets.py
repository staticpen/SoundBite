#!/usr/bin/env python3

import json
import os
import stat
import sys
import tempfile
import xml.etree.ElementTree as ET
from pathlib import Path


def fail(message: str) -> None:
    print(f"Spotify setup failed: {message}", file=sys.stderr)
    raise SystemExit(1)


repo_root = Path(__file__).resolve().parent.parent
project_file = repo_root / "SoundBite" / "SoundBite.csproj"
try:
    project = ET.parse(project_file)
except (OSError, ET.ParseError) as error:
    fail(f"could not read project settings: {error}")

user_secrets_id = project.findtext(".//UserSecretsId")
if not user_secrets_id:
    fail("the application project has no UserSecretsId")

secrets_file = (
    Path.home()
    / ".microsoft"
    / "usersecrets"
    / user_secrets_id
    / "secrets.json"
)
try:
    secrets_stat = secrets_file.stat()
except OSError:
    fail("local User Secrets file is missing")

if secrets_stat.st_uid != os.getuid() or stat.S_IMODE(secrets_stat.st_mode) & 0o077:
    fail("User Secrets must be owned by this account and have no group/other permissions")

try:
    with secrets_file.open(encoding="utf-8-sig") as source:
        secrets = json.load(source)
except (OSError, json.JSONDecodeError):
    fail("local User Secrets file is not valid JSON")

client_id = secrets.get("Spotify:ClientId")
client_secret = secrets.get("Spotify:ClientSecret")
if not isinstance(client_id, str) or not client_id.strip():
    fail("Spotify:ClientId is not configured in User Secrets")
if not isinstance(client_secret, str) or not client_secret.strip():
    fail("Spotify:ClientSecret is not configured in User Secrets")

support_dir = Path.home() / "Library" / "Application Support" / "SoundBite"
config_dir = support_dir / "config"
for directory in (support_dir, config_dir):
    try:
        directory.mkdir(mode=0o700, parents=True, exist_ok=True)
        os.chmod(directory, 0o700)
        directory_stat = directory.stat()
    except OSError as error:
        fail(f"could not secure the configuration directory: {error}")
    if directory_stat.st_uid != os.getuid():
        fail("configuration directory is not owned by this account")

target = config_dir / "spotify-production.json"
try:
    file_descriptor, temporary_name = tempfile.mkstemp(
        prefix=".spotify-production-",
        dir=config_dir,
    )
    os.fchmod(file_descriptor, 0o600)
    with os.fdopen(file_descriptor, "w", encoding="utf-8") as output:
        json.dump(
            {"Spotify": {"ClientId": client_id, "ClientSecret": client_secret}},
            output,
        )
        output.write("\n")
        output.flush()
        os.fsync(output.fileno())
    os.replace(temporary_name, target)
    os.chmod(target, 0o600)
except OSError as error:
    fail(f"could not write the protected Production configuration: {error}")

print(f"Production Spotify configuration saved with mode 600: {target}")
