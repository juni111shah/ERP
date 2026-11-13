# Client demo videos

Records the two HigherSquare 360 demo videos from the running app, with a neural voice-over, background music, animated title cards, a visible cursor and burned-in captions.

| Output | Made by |
|---|---|
| `out/HigherSquare360-Web-Demo.mp4` (1920x1080) | `record-web.mjs` drives Chrome through pricing, registration and every module |
| `out/HigherSquare360-Mobile-Demo.mp4` (1920x1080, phone in a branded frame) | `record-mobile.mjs` drives the Flutter app on the Android emulator |

The storyboard is in [STORYBOARD.md](STORYBOARD.md). The spoken words are in [script.json](script.json): edit a line there, run `voice` again and rebuild. The recording waits for each line to finish, so the picture and voice stay in step.

## What you need

- Node 20 or later, .NET 8 SDK, SQL Server Express (`.\SQLEXPRESS`), Google Chrome.
- For mobile: the Flutter SDK and the Android emulator `lifejet_pixel` (any phone AVD works; set `AVD`).
- Windows **Smart App Control must be off**: it blocks the freshly built `ERP.Api.dll`.
- Internet once for `npm install` and for the voice (only the script text goes to Microsoft's speech service).
- `assets/music.mp3`: "Upbeat Corporate" by AtlasAudio, from Pixabay (https://pixabay.com/music/corporate-upbeat-corporate-617614/), free for commercial use under the Pixabay Content License. It is not committed, because the licence does not allow redistributing the track on its own. Download it again from that page, or set `MUSIC=<path>` to use another track.

## Re-record

From the repository root, start the API on a throwaway database and the web app (two terminals):

```bash
cd backend/src/ERP.Api && ASPNETCORE_ENVIRONMENT=Development Database__AutoMigrate=true Seed__Enabled=true Seed__AdminPassword='DevOnly#2026' Seed__AdminEmail=admin@demo.local Seed__MultiWorkspaceEmail=owner@demo.local ASPNETCORE_URLS=http://0.0.0.0:5080 RateLimiting__LoginPerMinute=100000 ConnectionStrings__Default='Server=.\SQLEXPRESS;Database=ERP_DemoVideo;Integrated Security=true;TrustServerCertificate=true' dotnet run --no-launch-profile
```

```bash
cd frontend && npx ng serve --proxy-config proxy.conf.json --port 4200
```

Fill the demo workspace once the API is up (see `docs/demo-data/README.md`):

```bash
node tools/seed-demo.mjs http://localhost:5080/api/v1
```

Then, in `tools/demo-video`:

```bash
npm install
```

```bash
node voice.mjs
```

```bash
node record-web.mjs
```

```bash
node build.mjs web
```

For the phone video, start the emulator, install the app (`cd mobile && flutter build apk --debug && adb install -r build/app/outputs/flutter-apk/app-debug.apk`), then:

```bash
node record-mobile.mjs
```

```bash
node build.mjs mobile
```

Registration creates a new workspace each run. `record-web.mjs` uses a unique workspace code per run, so a re-run never clashes. To start completely clean, drop the database: `sqlcmd -S .\SQLEXPRESS -E -Q "DROP DATABASE ERP_DemoVideo"`.

## How it works

- `lib/recorder.mjs` drives headless Chrome with Playwright and captures it over the DevTools screencast (JPEG frames, quality 92), encoding live to H.264 at a steady 30 fps. It injects the cursor, click ripples, lower-thirds and title cards into the page, and writes the time each scene starts to `work/rec/<name>.marks.json`.
- `record-mobile.mjs` records the emulator with `adb screenrecord`, one clip per scene, and taps through the app with `adb shell input`, finding buttons by their accessibility labels (`uiautomator dump`).
- `build.mjs` places each voice clip at its scene mark, loops the music under the voice and ducks it when the voice speaks, burns in the captions (`work/build/*.ass`) and fades in and out.

Everything generated lives in `work/` and `out/`, which are not committed.
