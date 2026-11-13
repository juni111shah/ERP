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
