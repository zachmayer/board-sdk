# Bloom: build plan

*Companion to [DESIGN.md](DESIGN.md). Draft 2, 2026-09-27. How to build the design on Board with the same headless, agent-driven workflow as Pool Panic and Flying Hamsters. Every number here is a starting point for the hardware probes to confirm.*

## Contents

1. [The piece-set model](#1-the-piece-set-model)
2. [Board facts that bind the design](#2-board-facts-that-bind-the-design)
3. [Hardware probes (milestone 0)](#3-hardware-probes-milestone-0)
4. [Input](#4-input)
5. [Simulation](#5-simulation)
6. [Rendering](#6-rendering)
7. [Audio](#7-audio)
8. [Saves](#8-saves)
9. [Board integration](#9-board-integration)
10. [Repo, tests and tooling](#10-repo-tests-and-tooling)
11. [Milestones](#11-milestones)
12. [Sources](#12-sources)

## 1. The piece-set model

The Mushka model is public; the SDK's editor downloader reads the same manifest.

```bash
curl -sS https://dev.board.fun/downloads/models/manifest.json   # every piece set, with URLs
curl -sSo Assets/StreamingAssets/mushka_v1.3.1.tflite \
  https://dev.board.fun/downloads/models/mushka_v1.3.1.tflite
# then in Assets/Board/Settings/BoardInputSettings.asset:
#   m_GlyphModelFilename: mushka_v1.3.1.tflite
```

- **Glyph IDs** (SDK 3.2.1 simulator palette, `Editor/Assets/Simulation/Palettes/Mushka.asset`): 0 Watering Can, 1 Goodie Bag, 2 Nozzle, 3 Scrubby Brush, 4 Wand.
- **One model at a time.** Pieces from other sets arrive as `BoardContactType.Blob`, with no id or rotation. Swapping models takes 0.5–1 s and cancels every contact, so pick the model at launch.
- **Chop Chop + Mushka** (`chop_chop_mushka_v1.0.0.tflite`) has 10 piece classes by its output tensor, but the ID-to-piece map isn't published. If it's ever wanted, log each piece on hardware and map pieces by name, never by standalone ID (both standalone sets use 0–4).

## 2. Board facts that bind the design

- **Hardware:** 24", 1920×1080, 3.6 px per mm; MediaTek Genio 700 with a Mali-G57 MC3 GPU (OpenGL ES 3.2). Passively cooled, so it throttles in long sessions.
- **Contacts** (`BoardContact`, SDK 3.2.1): `contactId`, `type`, `screenPosition`, `orientation` (radians), `phase`, `glyphId`, `isTouched`, `bounds`.
  - **Rotation sign is unresolved:** the 3.2.1 source says clockwise from vertical, the 3.3.0 docs say counter-clockwise. Measure it per piece (probe 3).
  - **`bounds` is unusable:** 3.2.1 builds it as `new Bounds(center, extents)`, but Unity's constructor takes a size, so it's half size, and it's axis-aligned. Game code never reads it; see the piece-geometry table.
  - **`isTouched`** needs a conductive area on the piece and has never been checked on Mushka pieces. Fingers always report touched.
  - **`persistence`** (tracker frames, default 4, range 4–40) is how long a lost glyph survives before its contact ends and comes back with a new `contactId`.
  - **Smoothing** (translation and rotation, default 0.5) is one global low-pass filter. It shrinks fast back-and-forth strokes, and changing it at runtime cancels every contact.
- **Board's piece guide, in numbers:** about 200 ms to confirm a placement; holds of 200 ms to 1.5 s (longer "feels like a stuck button"); quick taps are unreliable; tracking drops when a piece moves fast, lifts at an angle, or is covered by a hand; tool pieces should be stateless; no extra smoothing on top of the SDK's; pausing cancels every contact.

## 3. Hardware probes (milestone 0)

Before any game code. About an agent day, plus two hours at the Board with Zach and a kid.

1. **`isTouched` per piece.** With `BoardInput.enableDebugView` on, touch each piece's body, spout, handle and ring; slide it; rest a hand *near* it to look for false positives. Decides whether the can and hose pour on a hand, or on parking over a thirsty plant.
2. **Dropouts while pouring.** A hand pours with the can for 30 s at persistence 4, 8 and 12. Count and time the dropouts, then pick one global value.
3. **Pose calibration.** Log each piece sitting still for 10 s (jitter) and rotating slowly through 360° (sign, and where zero points: spout or handle). Fit a circle to the wand's hole to find its centre offset (probably about 138 px along the handle) and its size (about 135–146 px). Film the wand sliding under a crosshair at 240 fps to measure lag.
4. **Scrub traces.** Record each kid scrubbing naturally for 5 s at smoothing 0.5 and 0.3. Sets the sponge's pixels per layer and its per-second cap.
5. **Blobs.** Do forearms, cups and other sets' pieces register as blobs? (It decides whether blobs can ever act as fingers.)
6. **Saves.** Create a save under profile A, switch to profile B and list saves; load one and log `BoardSession.players`; record `GetMaxPayloadSize()`.
7. **Stress soak.** A stress scene (see Rendering) for 30 minutes with 5 pieces and 10 fingers on the glass, under GLES3 and under Vulkan. Crash-free first, then lower touch-to-screen latency, decides the graphics API.
8. **Headless GPU capture.** Can `make shots` render a URP frame on the Mac without the GUI? If not, the Mac standalone build takes a `--shots` argument, writes PNGs and quits.

### Piece geometry (filled in by probe 3)

| glyphId | Piece | Rotation sign | Zero points to | Effect point | Footprint (SDK palette, px) |
|---|---|---|---|---|---|
| 0 | Watering Can | ? | ? | spout splash, about 160 px out | 202×116 |
| 1 | Goodie Bag | ? | ? | bag mouth | 124×176 |
| 2 | Nozzle | ? | ? | nozzle tip | 248×98 |
| 3 | Scrubby Brush | ? | ? | brush face | 158×114 |
| 4 | Wand | ? | ? | ring centre, about 138 px out; hole about 140 px | 182×456 |

Each footprint becomes an oriented polygon (the wand's with its hole cut out).

## 4. Input

### InputRouter

A pure C# layer turns `BoardContact` (and `DesktopInput` on the Mac) into two outputs. `GardenSim` consumes only these.

- **`ToolPose`:** `{glyphId, effectPoint, angle, velocity, parked, touched, present, sinceSeen}`.
- **`FingerIntent`:** `Hold`, `HoldDrag`, `Rub`, `Trowel`, `HoldSun`.

### Rules it enforces

- **Tools survive tracking blinks.** Poses are keyed by `glyphId`, not `contactId`. A lost glyph coasts at its last pose for 300 ms, then rebinds to the next contact with that `glyphId`. If two contacts report the same `glyphId`, the one nearest the last pose wins.
- **`touched` has hysteresis:** on after 100 ms touched, off after 250 ms untouched.
- **Parked** means under 40 px/s for 150 ms.
- **Pour fallback, per glyph:** if `isTouched` stays false through 5 s of cumulative sliding (a sliding piece must be in a hand), that glyph switches to "pour when parked over a thirsty plant, stop when it's full".
- **Fingers near pieces:** ignore finger contacts inside a piece's oriented footprint plus 30 px (the hand holding it, and piece bases that register as touches), except inside the magnifier's ring hole, where poking a bug is the natural move.
- **Hold the sun:** one finger contact moving less than 15 px, no other contact within 60 px, no piece within 150 px. The ring empties the moment any of that breaks.
- **Sponge distance:** accumulate path length while the sponge's footprint overlaps a cluster; sample every 50 ms; drop steps under 3 px (jitter); cap credit per second rather than per stroke, so fast scrubbing never feels slower; reset on lift or cancel. Probe 4 sets pixels per layer at the shipped smoothing.
- **Magnifier focus:** under 40 px/s for 250 ms.
- **Sponge near friends:** under 150 px/s counts as slow (a landed friend lifts off and hovers); probe 4's scrub traces confirm the threshold.
- **Critter hits:** 50 px radius, nearest wins.

### DesktopInput and traces

- **DesktopInput** (gated at runtime, never `#if UNITY_EDITOR`): keys 1–5 put that tool under the mouse and the scroll wheel rotates it; Shift sets `touched`; Space pins the tool in place, so the mouse can drive a second tool or a finger (left-drag). Bloom's core moment is two tools and a finger at once, and the siblings' single mouse point can't do that.
- **ContactRecorder:** a development-build toggle logs every contact frame as JSON lines, read through `board-connect logs`. The probe traces go in `Tests/Traces` and replay through `InputRouter` in edit-mode tests: pour start and stop, layers scrubbed, coasting through blinks.

## 5. Simulation

- **`GardenSim`:** pure C#, fixed 60 Hz, seeded randomness, no Unity dependencies in the rules. Plants, critters, the weather director, the economy and unlocks all live here.
- **`ShadowModel`:** a pure C# function, used by both the light rule and the renderer. Each plant (and the tree) is a disc of radius r at height h (height step plus mound). At sun direction s, its shadow is the disc moved h·k(s) along −s. At dawn it samples twelve sun positions and checks each plant's top, counting only taller casters: at least 70% lit is Full, 35–70% Part, less Shade.
- **Presentation state** (springs, unfurl timers, particles) lives in the renderer's `PlantRig`. It's stepped at the sim's tick for determinism, never read by the rules and never saved.
- **Determinism** is checked on one platform at a time (Mono on the Mac and IL2CPP on ARM64 won't match bit for bit); cross-platform checks use invariants.
- **Bots:** `GardenBot` plays whole years at a configurable attention (tasks per minute). Balance sweeps use a **mixed family** (for example 10, 8, 5 and 3 tasks a minute), not four identical bots.
  - **What they report:** tasks per player per minute, backlog, Q per crop, income per year, and the worst stretch of each summer.
  - **Acceptance:** tasks per player per minute stays inside DESIGN.md's band (about 3 in year 1, about 6 at the peak), and no summer has a stretch where the garden is mostly wilting.
  - **Prices:** the roster and catalog prices in DESIGN.md are regenerated from sweeps at C = 0.8.

## 6. Rendering

**Unity 6 URP, Forward, on the graphics API probe 7 picks** (GLES3 unless Vulkan proves stable; Vulkan crashed the Mali driver in Pool Panic).

### Constraints (the Mali-G57 is a tile-based GPU)

- **One directional light, no extra lights.** Fireflies are unlit glowing quads.
- **No post-processing stack, Opaque and Depth Textures off, no camera stacking.** Any of these forces the GPU to write tiles out mid-frame and ends near-free MSAA. Night, rain, heat and the season grades are global shader uniforms.
- **No `discard`, no alpha-to-coverage.** Both force a late depth test on Mali. Leaf outlines (lance, heart, lobed, serrated, ruffled, fern) are baked at startup into small opaque meshes of 16–32 vertices; 4× MSAA smooths the edges.
- **Planar shadows, not shadow maps.** Every part is drawn a second time, flattened by `ShadowModel`'s offset, into a half-res R8 target, blurred once and multiplied over the ground.
- **The magnifier** is a stencil disc just inside the ring hole. Inside it, critters are drawn a second time, instanced, at 2× around their own centres with their detail parts on. The rim, glint and fringe are art on the rim. No second camera, no copy of the scene.
- **Shaders** are warmed at load from a `ShaderVariantCollection`, with strict variant stripping, so nothing compiles mid-summer.
- **60 fps, VSync every V-blank.**

### Parts and budget

- A pure C# `PlantRig` turns each plant's read-only state and its `SpeciesLook` (about 20 parameters) into about 60 part instances. Each part kind is one instanced draw, reading per-instance data from a float texture by instance ID. Wind and bounce run in the vertex shader.
- **Budget:** 58 crops, meadow grass and wildflower spots as cheap billboards, and 100 critters, with 5 pieces and 10 fingers on the glass. After a 30-minute soak on the Board, the 95th-percentile frame stays at or under 14 ms. The game logs frame times, read through `board-connect logs`.
- **Governor:** if the 95th percentile tops 15 ms for 10 s, render scale drops to 0.8. Presentation only; the sim never sees it.
- **The year in bloom** replays from stored per-dusk plant states through the same rig. The low orbiting camera needs the parts to hold up off-axis; if they don't, the replay stays top-down.

### Shots and video

- **Tests assert on data,** under `-nographics`: `PlantRig`'s instance buffer for a seeded scenario hashes the same on every run.
- **`make shots` / `make video`** drop `-nographics`, turn off asynchronous shader compilation, and render to a RenderTexture on the Mac's GPU. Those images are for eyes and are compared with a tolerance, never byte for byte (Metal on the Mac isn't GLES on the Board).
- **`make device-shots`** runs a development build that plays a baked-in scenario on the Board and captures it with `board-connect screenshot`. That's the truth about how it looks.

## 7. Audio

Synthesized like the siblings: float arrays built at startup in a pure `*Sounds` class, played live, and mixed at event timestamps into `make video`.

- **Voices:** kalimba, marimba, bells, pads and noise beds. Birdsong and rain are the risk; if they sound cheap, CC0 recordings go through the same path.
- **The garden plays itself:** a radial sweep once per bar (pentatonic, about 76 BPM); each healthy plant it passes plays a note, capped at 24 notes a bar. Species picks instrument and scale degree, stage the octave, health the volume.
- **Mix:** tool sounds play immediately; reward chimes quantize to the next 16th (at most 200 ms) and snap to the scale; voice caps per category; no stereo cues.

## 8. Saves

- **One save per garden.** Board ties a save to the session's players when it's created, and loading a save replaces the session's players with the save's. The garden gate lists saves and draws its own picker.
- **API rules:**
  - Create a garden's save once, keep its id, and only ever `UpdateSaveGame` it.
  - **One save operation at a time.** The SDK keeps a single static `TaskCompletionSource` per operation, so an overlapping call leaves the first `await` hanging. Everything goes through a queue.
  - **Never call `RemovePlayersFromSaveGame` or `RemoveActiveProfileFromSaveGame`.** A save with no players left is deleted silently and for good.
  - **The Application ID** in `BoardGeneralSettings` is generated once and committed; regenerating it orphans every save.
  - Storage is capped at 64 MB per app.
- **`SaveStore`** has two implementations picked at runtime: Board's API on the device, and a file in `persistentDataPath` on the Mac (off the device, Board's API returns an empty list and a four-byte mock payload).
- **Autosave** at every season start and every summer dawn covers every exit, so the pause menu doesn't offer "Exit & Save".

### What's in a save

- **Format:** a version number and the RNG state.
- **Calendar:** year, season, day, and this summer's deck and draws.
- **Yard:** lots owned, each spot's soil (hollow, flat, mound), pot positions, stakes and trellises.
- **Plants:** species, spot, growth, water, daily care scores, dead leaves, fruit states, pinched vine tips.
- **Live critters:** species, host plant, cluster size, age, tag.
- **Economy:** coins, wishes on the jar lid, crops unlocked, gear owned, decorations and where they stand.
- **Unlock counters:** sun-sulks, tomato flops, beans with nothing to climb, aphids washed; cards offered and seen; first sightings shown.
- **Keepsakes:** the collection book, and each year's per-dusk plant states for the album replay (a few KB a year; re-rendered on demand, no images stored).

A hand-rolled, versioned binary. Every version bump ships a migration and a round-trip test.

## 9. Board integration

In the first build deployed (milestone 0):

- `SetPauseScreenContext` with `showSaveOptionUponExit: false`; custom buttons for **Show me how**, **Missing piece** and **Gentle pace** (`BoardPauseCustomButton`); audio sliders for music, garden and effects.
- Pause is detected through `OnApplicationPause`, `OnApplicationFocus` and `pauseScreenActionReceived`. Pausing cancels every contact, so on resume `ToolPose` rebinds by `glyphId` and a still-parked can starts pouring again after the touch hysteresis.
- `HideProfileSwitcher()` during play and `ShowProfileSwitcher()` on the garden gate, since opening a garden changes the session's players.
- Exit calls `BoardApplication.Exit()`.

## 10. Repo, tests and tooling

- **Its own repo**, like the siblings: `zachmayer/bloom`, Unity 6000.3.8f1, the Board SDK tgz vendored at the root, the Mushka model in `Assets/StreamingAssets`.
- **Make targets:** `test`, `playtest`, `shots`, `video`, `device-shots`, `build-android`, `deploy`, `logs`.
- **Tests:**
  - Every plant reaches harvest, and nothing dies with zero attention.
  - Soil is conserved.
  - The weather deck holds its constraints: the first year's script, the six calm cards in years 2–3, six of eight from year 4, day 1 Sunny, no spikes back to back, the last day never a Heat Wave.
  - The first year's script: no aphids before day 2, no bees before day 3, no dead leaves before day 4.
  - `ShadowModel` passes golden cases, such as "lettuce on the shadow side of a sunflower is Part", "a mounded tomato clears it", and "a radish under the pumpkin canopy is smothered; a staked tomato isn't".
  - The Q formula, the pollination floor, and the golden-fruit rule.
  - Seeds and the hose arrive at their earliest year, other gear the winter after its problem shows, and no winter brings more than three new things as DESIGN's pillar 3 counts them.
  - A save round-trips to the same hash, and every migration runs.
  - `InputRouter` passes its trace replays.
  - Bot sweeps stay inside the task band.

## 11. Milestones

Ordered so the cheapest ways the project could fail are tested first: feel, then fun, then performance, then looks.

| # | Milestone | Kills | Done when | Rough effort |
|---|---|---|---|---|
| 0 | **Probes and skeleton** | unknowns in input, GPU and saves | the repo builds an APK with pause integration and a committed Application ID; the contact recorder works; the eight probes are logged; the stress scene passes its soak; the graphics API is chosen; headless shots work or the fallback is chosen | 1 agent day, plus 2 h at the Board |
| 1 | **Grey-box year 1 on the Board** | fun, feel | year 1 of `GardenSim` runs (radish and sunflower; water, aphids, bees, dead leaves; day and night; the director; harvest) with `InputRouter` and trace tests, DesktopInput, bots and `make playtest`, a debug-draw renderer (discs, arcs, shadow polygons) and placeholder sounds. The family plays one summer, and pour, scrub and the lens feel right | 3–5 agent days, plus a playtest |
| 2 | **One beautiful plant** | the look | the sunflower rig with `ShadowModel` shadows in URP, rig-hash tests, `make shots` and `make video`. She watches the time-lapse and asks to see it again; kids name it from all four sides; the soak still passes with real shaders | 1–2 weeks |
| 3 | **Year 1, finished** | tone | critter art, tool effects, the garden playing itself, the ghost hand, setting the table, the garden gate, saves with round-trip tests, the year in bloom. The kids play it on hardware | about 1 week |
| 4 | **Winter and years 2–3** | progression | the shop (land, wishes on the jar lid, decorations), cards by year and problem, clay pots, the trowel (mounds, hollows, transplanting), lettuce, tomato and stakes, beans, ladybugs, hoverflies and wasps, the magnifier's tags, first sightings, the collection book, a save migration test | 1–2 weeks |
| 5 | **The v1 arc** | balance | years 4–5 (pumpkin, rough days, trellis, the hose, corn, the ladybug jar), the director and prices tuned by bot sweeps | 1–2 weeks |

About 6–10 weeks on the calendar. Every milestone ends with a family playtest; the calendar is set by playtests and Zach's review time, not agent hours. Later content (DESIGN.md section 15) follows v1, a few things at a time.

## 12. Sources

- **SDK 3.2.1 source** (the vendored tgz): `Runtime/Input/BoardContact.cs`, `Runtime/Input/BoardInput.cs`, `Runtime/Save/BoardSaveGameManager.cs`, `Runtime/Core/BoardApplication.cs`, `Runtime/Core/BoardPauseScreenContext.cs`, `Editor/Core/BoardModelDownloader.cs`, `Editor/Input/BoardInputSettingsEditor.cs`, `CHANGELOG.md`
- **Board docs:** https://docs.dev.board.fun/guides/piece-interaction-design · https://docs.dev.board.fun/guides/touch-input · https://docs.dev.board.fun/unity/api/BoardContact · https://docs.dev.board.fun/learn/pieces · https://docs.dev.board.fun/guides/save-games · https://docs.dev.board.fun/unity/performance · https://docs.dev.board.fun/unity/changelog
- **Piece-set models:** https://dev.board.fun/downloads/models/manifest.json
- **Arm GPU Best Practices** (depth testing, discard, alpha-to-coverage): https://developer.arm.com/documentation/102540/latest/Depth--Z--and-stencil--S--testing
- **Genio 700:** https://genio.mediatek.com/genio-700
- **Sibling repos:** `zachmayer/pool-panic` and `zachmayer/flying-hamsters` (make targets, `Snapshot.cs`, `InputRouter.cs`, pause handling, the OpenGL ES 3 build setting)
