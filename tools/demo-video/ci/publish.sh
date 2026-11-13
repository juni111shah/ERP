#!/usr/bin/env bash
# CI only: force-pushes this run's results (explore screenshots, scene marks, preview frames, and the video when under 95 MB)
# to the branch demo-video-out-<web|mobile>, so they can be fetched with plain git.
set -uo pipefail
KIND=$1
cd "$(dirname "$0")/../../.."
ROOT=$PWD
T=tools/demo-video
PUB=$(mktemp -d)
cp -r "$T/work/explore-$KIND" "$PUB/" 2>/dev/null || true
cp "$T"/work/rec/*.marks.json "$PUB/" 2>/dev/null || true
cp "$T"/work/build/*.ass "$PUB/" 2>/dev/null || true
# The exact voice clips and a high-quality copy of the raw recording, so the final mix (with music) can be built elsewhere.
if [ -d "$T/work/voice" ]; then mkdir -p "$PUB/voice" && cp "$T"/work/voice/*.mp3 "$T"/work/voice/durations.json "$PUB/voice/" 2>/dev/null || true; fi
FFX="$T/node_modules/ffmpeg-static/ffmpeg"
if [ -f "$T/work/rec/$KIND.mp4" ]; then
  "$FFX" -hide_banner -loglevel error -y -i "$T/work/rec/$KIND.mp4" -c:v libx264 -preset medium -crf 17 -maxrate 4M -bufsize 8M -pix_fmt yuv420p -an "$PUB/raw-$KIND.mp4" || true
  [ -f "$PUB/raw-$KIND.mp4" ] && [ "$(stat -c %s "$PUB/raw-$KIND.mp4")" -ge 95000000 ] && rm -f "$PUB/raw-$KIND.mp4"
fi
# The phone's own full-resolution screen recordings (used by the social ads), made smaller.
if [ "$KIND" = mobile ]; then mkdir -p "$PUB/rec"; for f in "$T"/work/rec/phone-*.mp4; do [ -f "$f" ] || continue; n=$(basename "$f" .mp4); "$FFX" -hide_banner -loglevel error -y -i "$f" -vf scale=720:-2 -c:v libx264 -preset medium -crf 20 -an "$PUB/rec/${n#phone-}.mp4" || true; done; fi
tail -400 api.log > "$PUB/api-tail.log" 2>/dev/null || true
FF="$T/node_modules/ffmpeg-static/ffmpeg"
for f in "$T"/out/*.mp4; do
  [ -f "$f" ] || continue
  mkdir -p "$PUB/previews"
  "$FF" -hide_banner -loglevel error -i "$f" -vf "fps=1/6,scale=640:-1" -q:v 6 "$PUB/previews/$(basename "$f" .mp4)-%03d.jpg" || true
  if [ "$(stat -c %s "$f")" -lt 95000000 ]; then cp "$f" "$PUB/"; fi
done
cd "$PUB"
git init -q && git checkout -q -b "demo-video-out-$KIND" && git add -A
git -c user.name="demo-video" -c user.email="demo-video@users.noreply.github.com" commit -qm "Demo video output: $KIND (run $GITHUB_RUN_ID)" || exit 0
git push -qf "https://x-access-token:${GITHUB_TOKEN}@github.com/${GITHUB_REPOSITORY}.git" "demo-video-out-$KIND"
echo "Published to demo-video-out-$KIND"
