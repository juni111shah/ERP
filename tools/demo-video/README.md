# Client demo videos

Records the two HigherSquare 360 demo videos from the running app: a neural voice-over, background music, animated title cards, a visible cursor with click ripples, section labels and burned-in captions.

| Output | What it shows |
|---|---|
| `HigherSquare360-Web-Demo.mp4` (1920x1080) | Pricing page, registration of a new workspace, first sign-in, then every module of the seeded workspace (Al Rayyan Trading) |
| `HigherSquare360-Mobile-Demo.mp4` (1920x1080, phone in a branded frame) | The Flutter app on an Android phone: intro tour, sign-in, overview, documents, approvals, My HR, modules |

[STORYBOARD.md](STORYBOARD.md) lists the scenes. The spoken words are in [script.json](script.json), split into beats: each beat starts when its action starts on screen and never before the previous line has finished, so voice and picture stay together. Edit a line, record again, rebuild.

## Re-record on GitHub Actions (what was used)

The dev PC's Windows Smart App Control blocks a freshly built `ERP.Api.dll`, so the recording runs on GitHub's Linux runners ([.github/workflows/demo-video.yml](../../.github/workflows/demo-video.yml)): SQL Server, the API with the demo seed and `ng serve` for the web job; the same API plus an Android emulator with the debug APK for the phone job.

1. Set the mode of each job in [ci-mode.txt](ci-mode.txt): `record` for the videos, `explore` for screenshots and a control list of every screen (useful after UI changes).
2. Push to `claude/demo-video`. Put `[web-only]` or `[mobile-only]` in the commit message to run one job.
3. Each job force-pushes its results to a branch: `demo-video-out-web` / `demo-video-out-mobile` (raw recording, voice clips, scene marks, preview frames). Fetch them:

```bash
git fetch origin demo-video-out-web && mkdir -p tools/demo-video/work/ci-web && git archive FETCH_HEAD | tar -x -C tools/demo-video/work/ci-web
```

4. Mix in the music and captions here (Pixabay does not serve the track to GitHub's runners):

```bash
cd tools/demo-video && npm install && SRC=work/ci-web node build.mjs web
```

The phone video is the same with `mobile` in place of `web`.

## Re-record on this PC

Needs Node 20+, the .NET 8 SDK, SQL Server Express (`.\SQLEXPRESS`), Google Chrome, and for the phone the Flutter SDK and the Android emulator `lifejet_pixel`. Smart App Control must be off (Windows Security > App & browser control).

Start the API on a throwaway database and the web app (two terminals, repository root):

```bash
cd backend/src/ERP.Api && ASPNETCORE_ENVIRONMENT=Development Database__AutoMigrate=true Seed__Enabled=true Seed__AdminPassword='DevOnly#2026' Seed__AdminEmail=admin@demo.local Seed__MultiWorkspaceEmail=owner@demo.local ASPNETCORE_URLS=http://0.0.0.0:5080 RateLimiting__LoginPerMinute=100000 ConnectionStrings__Default='Server=.\SQLEXPRESS;Database=ERP_DemoVideo;Integrated Security=true;TrustServerCertificate=true' dotnet run --no-launch-profile
```

```bash
cd frontend && npx ng serve --proxy-config proxy.conf.json --port 4200
```

Fill the demo data and open sign-up (the founder must otherwise approve each registration):

```bash
node tools/seed-demo.mjs http://localhost:5080/api/v1 && node docs/demo-data/1-payroll-leave-approvals-settings.mjs && node docs/demo-data/2-credit-notes-attachments-idempotency.mjs && node tools/demo-video/ci/open-signup.mjs
```

Then remove the word "demo" from what the camera sees (workspace names, sign-in keys, e-mails, people, memos):

```bash
sqlcmd -S .SQLEXPRESS -E -d ERP_DemoVideo -i tools/demo-video/ci/rebrand.sql
```

After this, sign in as `admin@alrayyan.qa` (workspace `alrayyan`) or `owner@alrayyan.qa` (`lusail`).

Then in `tools/demo-video`:

```bash
npm install && node voice.mjs && node record-web.mjs && node build.mjs web
```

For the phone: start the emulator, install the app (`cd mobile && flutter build apk --debug && adb install -r build/app/outputs/flutter-apk/app-debug.apk`), then `node record-mobile.mjs && node build.mjs mobile`.

To start clean, drop the database: `sqlcmd -S .\SQLEXPRESS -E -Q "DROP DATABASE ERP_DemoVideo"`.

## Music and voice

- Music: "Upbeat Corporate" by AtlasAudio from Pixabay (https://pixabay.com/music/corporate-upbeat-corporate-617614/), free for commercial use under the Pixabay Content License. It goes in `assets/music.mp3` and is not committed, because the licence does not allow redistributing the track on its own. `MUSIC=<path>` uses another track.
- Voice: Microsoft neural voice `en-US-AndrewMultilingualNeural` through the free Edge speech service (`msedge-tts`). Only the script text is sent. Change `voice` in `script.json` for another voice (for example `en-US-AvaMultilingualNeural` or `en-GB-RyanNeural`).

## How it works

- `lib/recorder.mjs` drives headless Chrome with Playwright and captures it over the DevTools screencast (JPEG frames, quality 92), encoding live to H.264 at a steady 30 fps. It injects the cursor, click ripples, section labels, a spotlight and the title cards into the page, and can freeze the picture while it signs in behind a title card.
- `record-web.mjs` plays the web story; `record-mobile.mjs` records the emulator with `adb screenrecord` (one clip per scene), taps through the app by accessibility labels (`lib/adb.mjs`), and lays the clips into a phone bezel on a branded stage (`lib/phone-stage.mjs`) with animated side titles.
- `build.mjs` places each voice clip at its mark, loops the music under the voice and ducks it while the voice speaks, burns in the captions and fades in and out.
- `explore-web.mjs` / `explore-mobile.mjs` save a screenshot and control list of every screen, to write or fix the recording scripts.

Everything generated lives in `work/` and `out/`, which are not committed.
