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
# The launcher is not needed (the app is started directly) and on a slow CI emulator it raises "not responding" dialogs.
adb shell pm disable-user --user 0 com.google.android.apps.nexuslauncher || true
adb shell pm disable-user --user 0 com.android.launcher3 || true
adb shell settings put global window_animation_scale 1 || true
sleep 45   # let the freshly booted emulator settle
if grep -q '^mobile=record' ci-mode.txt; then
  node record-mobile.mjs
else
  node explore-mobile.mjs
fi
