#!/usr/bin/env bash
# CI only, inside the emulator step: installs the app and explores or records it.
set -euo pipefail
cd "$(dirname "$0")/.."
adb wait-for-device
adb install -r ../../mobile/build/app/outputs/flutter-apk/app-debug.apk
adb shell settings put system show_touches 1
adb shell settings put system screen_off_timeout 1800000
adb shell svc power stayon true
adb reverse tcp:5080 tcp:5080 || true
if [ "$(tr -d '[:space:]' < ci-mode.txt)" = "record" ]; then
  node record-mobile.mjs
else
  node explore-mobile.mjs
fi
