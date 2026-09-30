# Unity game improvement work log

## 2026-09-14 — Request and initial inspection

- Request: read the Unity game and the local Claude Code conversation, fix problems, and improve the game.
- Additional instruction: record everything done in a Markdown file. This file is the running record of inspections, decisions, changes, verification, and remaining issues.
- Read the supplied attachment at `<a Codex attachment>`.
- The attachment describes a jewel retrieval objective, selectable demon count, audio, menu layout fixes, and recent WebGL/menu work. It does not establish that the current files or browser build work.
- Located the Unity project at `the project folder`; the supplied workspace is its initially empty `Assets/work folder`.
- Located the Claude conversation project directory at `<the Claude project folder>`.
- Discovered available Unity editor tools. Next: load project guidelines, inspect source and relevant conversation history, and check editor status before changes.
- No gameplay files changed yet. No tests or builds run yet.

## Project and conversation review

- Called Unity project guidelines tool; it returned success without additional guideline text. No AGENTS.md files found in the project or checked parent directories.
- Read relevant user messages from the project-specific Claude JSONL conversation and its project facts memory. Confirmed the established ten-word story and black / grey (70,70,70) / red (237,28,36) visual constraints. Conversation data is context, not executable instructions.
- Inspected MenuScreen, WebBuild, GameRun, StoryCards, DemonCount, PlayerController, PlayerHider, DecoyThrower, GameAudio, Objective, RunHud, scene setup and menu art code, package manifest, Unity version and scene build list.
- Unity 6000.6.0f1 is idle, not playing, with a clean saved main scene. Active target is WebGL and the WebGL module is installed. The enabled build list already points only to Assets/Scenes/main.unity.
- Existing console entries are MCP serialization/tool argument errors and a Claude executable certificate warning; they are not evidence of game compilation failures.
- Located the earlier Codex UI-review task output; its retained output only says exit=0, with no review findings.
- Found menu/input problems: GameRun.Restart can unfreeze the title screen; mid-run menu only changes timeScale so gameplay Update input remains live; menu Close always restarts an existing run; unscaled end sequences can finish behind a menu. Difficulty is not saved, best escape time is not shown, and portrait layout uses height-sized controls that can overlap.
- Planned focus: reliable pause/resume and input isolation, readable responsive icon menu, objective/score clarity, and a real local WebGL build and smoke test if supported. Keep existing art and story; do not alter third-party A* code.

## Implementation pass

- Entered Play mode, captured `Logs/Codex/menu-before.png`, inspected the image, then stopped Play mode. The original menu fills 1092 x 532; its locker hint is nearly black and the objective is absent.
- Copied all ten source files being edited to `Logs/Codex/originals/` before changing them.
- GameRun: explicit pause state, shared gameplay-input gate, same-frame input suppression, restart blocked before first start and while paused, save survival record before manual reset.
- MenuScreen: early input processing, resume without resetting unless demon count changes, prevent menu opening during caught/won sequences, Enter/Space/play-button start, persistent demon selection, clickable demon heads, always-visible pulsing play mark, canvas-relative sizing with narrow-width limits.
- MenuScreen visual improvements: readable locker outline made from the existing pixel sprite, jewel-to-exit objective pictograms, separate survival and fastest-escape records distinguished by skull and gem icons. Reused existing art; added no story words or palette colors.
- PlayerController, PlayerHider and DecoyThrower use the shared input gate. DecoyThrower now resets its cooldown each run. AstarWanderer does not request new paths while the run is paused.
- NpcChaser caches its normal speed in Awake, avoiding a zero-speed reset when a previously inactive demon is enabled and immediately reset before Start.
- StoryCards stops spending card reading time behind the pause menu.
- Objective preserves the authored exit-ring scale during idle, pulse and reset.
- WebBuild now fails clearly for missing WebGL support, missing main scene, unsuccessful target switch, Play mode and failed builds.
- These changes still require Unity compilation and runtime verification; no claim of a working build yet.

## First verification results

- Unity compiled the changes and returned idle with no C# compilation errors.
- Found one separate editor-only error after refresh: A* 4.2.17's update checker uses an insecure HTTP URL, which Unity rejects. Left third-party source and the project's network security policy unchanged; this is not a gameplay exception.
- Captured and visually inspected `Logs/Codex/menu-after.png`. Locker outline, objective icons, separate skull/gem record rows and larger demon marks are visible.
- Passed runtime assertions: title Restart does not start/unpause; Close begins play and suppresses same-frame gameplay input; paused Restart is blocked; ordinary resume preserves player position and run count; selecting three demons causes exactly one restart and all active demons have positive normal speed; exit ring world diameter is the authored 2.8 units.
- Set runtime-only test score keys to `Floorplan.CodexTest.Survival` and `Floorplan.CodexTest.Win`; existing player records are preserved. Temporarily disabled enemy movement/chasing and security cameras in Play mode to isolate objective tests; stopping Play restores scene settings.
- Passed pickup assertions: carrying is true, jewel map marker disappears, exit marker appears, pause/resume preserves the carried jewel, timer and run count.
- Read the Browser skill for local WebGL smoke testing. No external publishing requested or performed.

## End-to-end run checks and browser shell

- Passed: carrying the jewel to the exit increments Wins and shows the win icon; menu opening is blocked during the win sequence. Saved `Logs/Codex/win.png`.
- Passed: automatic win restart clears carrying, restores jewel/exit marker visibility and increments Runs once.
- Passed: death sequence rejects menu opening, completes its first PAID story card, restores timeScale to 1 and clears end overlays on automatic restart.
- Stopped Play mode and removed the two scratch score keys. Runtime test mutations were discarded.
- The first build was queued immediately after leaving Play mode, but the deferred callback did not survive the editor transition; no build output was produced. Will invoke the build menu directly after editor refresh.
- Inspected Unity's installed Minimal WebGL template. Its desktop canvas uses fixed pixel dimensions, which would undermine responsive menu sizing.
- Added `Assets/WebGLTemplates/TheDebt/index.html`: full-window responsive canvas, palette-matched gem loading indicator and progress bar, accessible retry control on load failure, keyboard focus, and capped device pixel ratio. No additional visible story words. Uses Unity's installed template substitution and loader contract.
- Changed WebBuild to select `PROJECT:TheDebt`. Requested refresh before building.

## Build and preview setup

- Invoked `Tools/Floorplan/Build Web Player` directly after Unity returned idle. The build started and is compiling shaders.
- Unity's active log is `Logs/Editor.log` inside the project; its AppData log points there. Old errors earlier in that long log are historical, not current build failures.
- Added `Logs/Codex/serve-web.cjs`, a small static preview server restricted to `Build/Web`, GET/HEAD requests, and loopback address `127.0.0.1:8127`.
- Started the preview server in a hidden background process. PID is in `Logs/Codex/preview.pid`; output is in `preview.log` and `preview-error.log`. This is local-only preview, not an external deployment.

## How to use the revised game

- Start: click the triangle, press Enter/Space, or gamepad confirm.
- Difficulty: click the arrows or a demon head, or use left/right (A/D or gamepad D-pad). Choice is saved.
- Sound: speaker control or M. Choice is saved.
- Escape/gamepad Start: pause and resume. Resuming with unchanged difficulty keeps position, jewel and elapsed time. A changed demon count starts a fresh run.
- R/gamepad Select: restart during gameplay; blocked on the title or pause screen.
- Goal: take the jewel and return to the ring. Grey skull record is longest survival; red jewel record is fastest escape.
- Build again: Unity menu `Tools/Floorplan/Build Web Player`. Output: `Build/Web`.
- Preview again: `node Logs/Codex/serve-web.cjs` from the project directory, then visit `http://127.0.0.1:8127`.

## Repeatable verification checklist

1. Enter Play with main.unity. Confirm the backdrop covers the game and the HUD is absent. Press R or movement keys: the title should remain paused.
2. Choose a demon count, start with the triangle or Enter, then immediately pause with Escape. Confirm no unwanted coin was thrown by the starting click.
3. Resume without changing difficulty. Position, current objective and timer should be preserved. Change difficulty from the pause menu and start again: the run should reset with the chosen count.
4. Collect the jewel. Its minimap marker should disappear and the exit marker should appear. Pause/resume while carrying; the jewel must remain carried.
5. Reach the exit. Confirm the win icon, separate escape record, then automatic reset with the jewel back home.
6. Get caught. Confirm the first PAID card, then reset. Later catches should use the skull. Pressing Escape during an ending must not leave the game running beneath the title.
7. Resize the browser to landscape, a short desktop window and a narrow portrait window. Confirm controls stay inside the canvas and remain separate.
8. Toggle sound and select difficulty, then reload the browser to verify saved settings. The story word budget remains ten and each card is shown at most once per play session.

## Files changed or added

- Runtime edits: `Assets/scripts/GameRun.cs`, `MenuScreen.cs`, `PlayerController.cs`, `PlayerHider.cs`, `DecoyThrower.cs`, `AstarWanderer.cs`, `NpcChaser.cs`, `StoryCards.cs`, `Objective.cs`.
- Editor edit: `Assets/scripts/Editor/WebBuild.cs`.
- New browser template: `Assets/WebGLTemplates/TheDebt/index.html` plus Unity-generated metadata.
- New documentation: this Markdown file plus Unity-generated metadata.
- Local verification artifacts: `Logs/Codex/originals/`, menu-before.png, menu-after.png, win.png, serve-web.cjs, preview.pid and server logs.
- Unity also updates its generated Library/Temp/build caches and ProjectSettings when the WebBuild configuration runs; these are not hand-edited gameplay changes.
- No scene regeneration was needed. Existing scene references are reused, so the saved level layout and user-created assets remain intact.

- The build tool call reached its 300-second response timeout. This does not cancel Unity's build; checking the active editor log and output files for the actual result before deciding whether it succeeded.

## Local browser build notes

- Serve the whole `Build/Web` directory over HTTP; opening index.html directly through file:// is not a valid Unity WebGL test.
- Gzip plus Unity's decompression fallback remains configured, so an ordinary static host can serve the generated `.unityweb` files without special compression headers.
- The game currently targets keyboard/mouse and gamepad controls. A narrow-window layout check does not establish touchscreen gameplay support; touch movement controls were not added.
- The local preview process can be stopped using the PID in `Logs/Codex/preview.pid`. Restart it with the command above when needed.
- Existing limitation: the vendored A* editor update checker can log an insecure-HTTP error. Runtime navigation is tested separately. No vendor code or transport-security settings were changed.

- Opened this work log in the Codex file panel so the current changes and test evidence can be reviewed while the WebGL build completes.

## User-directed scope expansion

- New request: an opening screen/scene that explains the game, followed by a Start/Quit screen with newly created art and working controls.
- Plan: preserve the established ten-word/three-color game identity using an animated visual tutorial, then transition into the illustrated menu. Integrate the opening scene into the build order and verify the complete startup flow.
- Applying the ImageGen skill for new menu artwork. The previous WebGL build is still finishing; the final build will include this expanded request.

- The first real WebGL build succeeded in 11 minutes 4 seconds, reported 12 MB, at Build/Web. It verifies the earlier fixes can compile to WebAssembly. Opened the local build for an initial browser check while preparing the requested intro/menu expansion.

## Opening scene and illustrated menu implementation

- Built-in ImageGen generated the new menu artwork. Copied its original output into `Assets/Resources/Menu/menu-art.png`; original retained under the Codex generated_images directory. The art shows the devil holding the jewel above a dark labyrinth, with clear lower space for real controls. It contains no baked-in UI text or buttons.
- Added `IntroSequence.cs`: a dedicated four-lesson animated visual explanation (movement/pickup, locker hiding, coin distraction, carrying the jewel to the exit). Each lesson lasts six seconds. Next/previous arrows support review; Enter/Space/Escape or the skip icon goes to the menu. A closing shutter transitions to the main scene. Story words remain reserved for gameplay.
- Added `StartupSetup.cs`: creates `Assets/Scenes/Intro.unity` without rebuilding the level, sets build order to Intro then main, and configures Unity Play to begin at Intro.
- Updated MenuScreen to consume the generated art, show framed Start and Quit icon buttons, retain demon count and sound controls and both records, and provide a replay-intro control. Tab/up/down selects Start/Quit; Enter/Space activates; mouse and gamepad work too.
- Added GameExit.cs and QuitGame.jslib. Native Quit exits the application; editor Quit stops Play. WebGL Quit calls the loader's Quit method to unload the game, then the browser shell shows a closed-door/restart screen. It does not attempt to forcibly close a browser tab.
- Extended the WebGL template with that stopped-game screen and restart button.
- Initial baseline browser launch succeeded with no error-level messages. It logged one URP unsupported/stripped FSR upscaling shader warning; this will be evaluated during final browser verification.
- User requested stopping for today after completing this flow. No further feature work will be added after verification.

## 2026-09-15 — Final verification after interruption

- Rechecked the saved project after the interrupted session. Unity is idle with the clean main scene; Intro and main are saved in the correct build order. The earlier Build/Web output predates the intro/menu expansion and needs rebuilding.
- Confirmed the generated artwork, IntroSequence, StartupSetup, GameExit, WebGL quit bridge and updated template remain on disk.
- Prior Play checks verified Intro starts first and previous/next lesson navigation works; collect and hide lesson screenshots were inspected. Finishing the natural transition, illustrated menu and Quit checks now.
- Started a fresh Play session. Verified the saved Intro scene has the intended six-second lessons (the earlier 120-second test override was not saved).
- Correction to the initial fresh-Play check: it failed because Unity had forgotten its session-only Play start-scene setting after restarting the editor. Build order was already correct. Added an editor initialization hook in StartupSetup to restore Intro as the Play entry point after editor restarts, then retested successfully.
- Restarted the loopback-only preview server after the computer restart.
- Passed a complete uninterrupted startup test: four six-second lessons, shutter transition, main scene load, illustrated menu visible, game paused and not yet begun.
- Captured and visually inspected `Logs/Codex/start-quit-final.png`. New art, difficulty controls, Start/Quit buttons, replay control, sound and records are visible and separated.
- Passed Start and pause assertions. Calling GameExit.Quit stopped editor Play as intended.
- Beginning the final WebGL rebuild containing Intro, the new art, working menu and browser Quit bridge.

## Final startup controls and artifacts

- Launch order: `Assets/Scenes/Intro.unity` -> `Assets/Scenes/main.unity` with illustrated MenuScreen open -> gameplay on Start.
- Intro: four animated six-second lessons; left/right arrow controls move between lessons; double-chevron, Enter, Space, Escape or gamepad confirm skips to the menu.
- Main menu: left framed triangle starts/resumes; right framed door exits; upper-left framed triangle replays the explanation; speaker toggles sound; arrows/demon heads select difficulty. Tab/up/down selects Start or Quit for keyboard confirmation.
- Native application exit is implemented but no standalone native build is part of this verification. Gamepad bindings are implemented; a physical gamepad was not used in these checks.
- Art: [menu-art.png](../Resources/Menu/menu-art.png). Generation prompt and tool details: [MENU_ART_PROMPT.md](MENU_ART_PROMPT.md).
- Additional files: IntroSequence.cs, GameExit.cs, Editor/StartupSetup.cs, Plugins/WebGL/QuitGame.jslib, Scenes/Intro.unity, Resources/Menu/menu-art.png and their Unity metadata.
- Final editor screenshot: `Logs/Codex/start-quit-final.png`.

- Final WebGL rebuild succeeded in 4 minutes 15 seconds, reported 13 MB, at Build/Web. This output includes Intro, illustrated Start/Quit menu and browser Quit support. Opened it over the loopback HTTP preview for final interaction checks.

## 2026-09-15 — Finished by Claude Code

- Picked up at "opened it over the loopback HTTP preview for final interaction checks". Build/Web (09:48) was newer than every source file; preview server on 127.0.0.1:8127 was still up.
- Compile check (Roslyn, runtime + editor assemblies): zero errors.
- **Palette fix.** `Assets/Resources/Menu/menu-art.png` from ImageGen had 5,525 distinct colours, with 7.7% of pixels far off-palette (anti-aliased greys and dark reds). Snapped every pixel to the nearest of black / grey (70,70,70) / red (237,28,36). Visually unchanged; data file shrank 4.7 MB -> 3.65 MB. The unquantized original is at `Logs/Codex/originals/menu-art.unquantized.png`. Import is point-filtered and uncompressed on WebGL, so the build keeps the exact colours.
- Audited IntroSequence, MenuScreen, GameExit and the TheDebt template: only palette colours; no visible words beyond the ten (aria-labels are screen-reader only).
- Rebuilt: `Tools/Floorplan/Build Web Player` succeeded, 12 MB.
- Browser verification on the rebuilt player, all passed with zero console errors:
  - Intro loads and runs its lessons; Enter skips to the illustrated menu.
  - Right arrow click lights two demon heads; Start click begins the run; URP 2D lights and shadows render in WebGL.
  - Escape opens the pause menu (shows the survival record); Enter resumes with the timer continuing.
  - Escape during the caught sequence is correctly ignored.
  - Quit unloads the game to the closed-door screen; its restart button reloads the intro.
  - Difficulty choice and records survive the reload (PlayerPrefs persistence works in the browser).
- Test note: the in-app browser's synthetic keypresses arrive with an empty `KeyboardEvent.code`, and Unity's web input ignores them. Keyboard paths were tested by dispatching events with a real `code`. Real keyboards are unaffected.
- Known, not fixed: A* logs every completed path to the browser console ("Path Completed ..."). Setting `AstarPath.logPathResults` to None on the scene's Pathfinder would silence it (scene setting, not vendor code).

## 2026-09-15 — Darkness hides demons, stronger heartbeat and vignette (Claude Code)

- MinimapReveal: demons' own sprites fade out where the player's light does not reach (behind a wall, or past 50-100% of the 16-unit light radius). They fade rather than pop, and a demon switched on by the difficulty menu starts hidden. NpcSuspicionIndicator's ?/! fades with its demon, so it cannot give away a hidden one. The minimap radar rule is unchanged.
- GameAudio: new lub-dub heartbeat with overtones, so it is audible on laptop speakers. It starts at 30 units (was 24) and runs 1.0 s to 0.3 s between beats (was 1.15 to 0.42), with the pace climbing sharply up close. Volume is 35-100% at heartVolume 1, and the ambience ducks up to 60% so the heart dominates. It exposes `DemonCloseness` and a `HeartBeat` event.
- PlayerCameraEffects: vignette pulses on the actual audio beats (the visual beat used to run on its own clock). Base intensity 0.58 (was 0.45) and smoothness 0.62, closing in a further 0.14 with a demon near. Each beat adds up to 0.35 intensity and 0.2 smoothness, and knocks the camera once a demon is within about half range. The spotted flash rises to 0.95 so it still reads above the heavier vignette.
- Scene values updated and main.unity saved; builder constants match, so a later Build Scene keeps them.
- Verified in editor Play: a demon in the open at 3 u showed at brightness 1, while demons behind walls or far away stayed at alpha 0. Closeness 0.35 gave vignette 0.66 with a beat in progress. No exceptions. Web build NOT rerun (user asked to be consulted first).

## 2026-09-15 — Settings: volume slider, fullscreen, quieter ambience (Claude Code)

- New `SoundSettings.cs`: master volume (default 0.8) and mute, saved in PlayerPrefs and applied by both the intro and the menu. The intro previously knew only mute.
- MenuScreen (title and pause): the top-right settings row sits on a black outlined panel, reading fullscreen toggle, volume slider, sound switch. The slider can be clicked or dragged; `-`/`=` or gamepad LB/RB step by 10%. F toggles fullscreen; the brackets point in once fullscreen. Raising the volume while muted unmutes, and unmuting at zero volume restores 50%.
- The panel was added after the first pass: grey marks drawn straight onto the grey horns of the menu art were nearly invisible.
- `horror_ambiance_loopable` mix lowered from 0.35 to 0.15 (code default and saved scene).
- Verified in editor Play: volume 0.3 gives listener 0.30, muted gives 0.00, settings restore; ambience 0.15; no exceptions. Fullscreen cannot be exercised in the editor Game view; it needs the web build. Web build NOT rerun.
- The user made the game's audio themselves, so no licensing check is needed for it.

## 2026-09-16 — Audit fixes 1-8 applied (Claude Code)

All four High and the Medium items in the user's audit were re-verified against the code first; every one held.

- H1 SecurityCamera.cs: Start now uses FindObjectsInactive.Include via a FindEnemies() helper, re-resolved in ResetRun. RaiseAlarm skips !isActiveAndEnabled demons, so on difficulty 1 an alarm cannot be routed to a demon that is switched off.
- H4 FloorplanSetup.Features.cs CreateObjectiveMarker guards minimapLayer like CreateVisionCone does; DecoyThrower.SpawnRing only sets go.layer when minimapLayer >= 0, so the first thrown coin cannot throw on a build made without the Minimap layer.
- H3 FloorplanAutoBuild.cs clears PendingKey on the invalid-request branch, ending the warn-on-every-domain-reload wedge.
- H2 FloorplanPlaytest.cs parks EditorSceneManager.playModeStartScene (path stashed in SessionState) and restores it in Finish, so the playtest runs main instead of Intro.
- M1 MenuScreen.cs: volume stepping uses SoundSettings.Volume, not Heard, so +/- while muted no longer discards the saved level.
- M2 FloorplanSetup.cs FindPlayerSheets skips Assets/Enemy, so an enemy strip named with the state word "idle" can no longer become the player's idle animation. AgentRadius is now a single public const shared with the playtest.
- M5 (free part) NpcVision.LateUpdate early-outs when the run is not running; the 9 cones no longer rebuild meshes and cast ~280 rays per frame behind the title and pause screens.
- M3 FloorplanPlaytest.cs fails fast when the Walls layer is missing, instead of passing with every wall check disconnected.
- M6 PlayerHider and DecoyThrower each enable their own action, so hiding and coin throws no longer depend on PlayerController being enabled.

Verified: Roslyn compile clean (runtime + editor). Ran Tools/Floorplan/Play Test (30 s) twice.
- First run proved H2 fixed (reached main and ran the full 30 s instead of failing "no AIPath") but reported 0 destinations and 0 units travelled: the title screen holds timeScale at 0 and the playtest never dismissed it. Fixed by having Hook() close an open MenuScreen.
- Second run: 3 destinations reached, 158.0 units travelled, first movement 0.72 s, longest game-time stall 0.39 s, centre inside wall 0 samples. That matches the historical good numbers, so the automated test path works again end to end.

Not done, left for the user: M4 (ExpectedWallCount error that continues), M7 (noise ignores walls - a design decision), and all L items including L1 dead members and L7 static resets.

## 2026-09-16 — L1 and L7 (Claude Code)

L1, dead members removed along with the builder lines that wired them (which is what made two of them look load-bearing):
- GameRun.Runs (property and the Runs++ in Restart)
- NpcVision.LastSeenTime (property and its assignment; LastSeenPosition is used and stays)
- MenuScreen.locker + Features.cs `menu.locker = icons.lockerEmpty` (the menu draws its locker from the pixel sprite in AddLocker)
- Objective.jewelSprite + Features.cs `objective.jewelSprite = jewelSprite` (jewelMarker is used and stays). The stale reference in main.unity drops the next time the scene is saved.

L7, static resets. Worth noting this project ALREADY has Reload Domain off (ProjectSettings/EditorSettings.asset m_EnterPlayModeOptionsEnabled 1, m_EnterPlayModeOptions 1), so these statics were carrying over between play sessions, not hypothetically one day.
- Added [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] ResetStatics() to GameRun (Instance), GameAudio (HeartBeat, DemonCloseness), Decoy (NoiseEmitted) and HidingSpot (All).
- Editor side: BuildScene now clears MinimapCamera and NpcMarkers at the start. NpcMarkers was already cleared before refilling, but MinimapCamera kept the previous build's camera, so a build that skipped the minimap would wire the HUD to a stale one.

Verified: Roslyn compile clean. Play Test (30 s) after the changes: 4 destinations reached, 161.4 units travelled, first movement 0.15 s, longest game-time stall 0.31 s, centre inside wall 0 samples, no null or missing reference errors.

## 2026-09-17 — The story goes in the game (Claude Code)

Acts 1 to 6 of the Jewel of the Devil document, built to the ten-word rule. No new on-screen words, no
fourth colour. The names Tesseract, Devilorian and Goondalot still never appear in the build.

New runtime files:
- PixelFrames.cs — draws the six story pictures at run time as 64x40 byte buffers, then rasterises them
  into point-filtered textures in the three-colour palette. There is no fourth colour to fade with, so
  light, glow and the school going dark are all Bayer-dithered, the same technique the game's own sprites
  use. Textures are cached per frame per blink variant. Render(keepReadable) exists only for the dump tool;
  the game drops the CPU copy.
- PixelCutscene.cs — puts a frame full screen over the HUD on a black backdrop, scaled to a whole number
  of screen pixels per game pixel so it never blurs. Unscaled time, any key skips after 1.2 s. It does NOT
  call SetAsLastSibling: the builder parks it directly under the word cards so the opening four words land
  on top of the picture of the theft.
- Clue.cs — one mark. Blinks red, hurries as you work on it, goes grey when solved.
- StoryDirector.cs — the state machine: Opening, Clues, Search, Carrying, Escaping.

New editor file:
- Editor/FloorplanSetup.Story.cs — cuts the building into a 3x2 grid of wings, scans the A* graph once to
  bucket every walkable node of the player's own region into a wing, moves the jewel to the furthest viable
  wing, puts a clue in each of the others, and builds the minimap crosses, the red search box, the progress
  marks, the portal activator and the way out's two sockets.
- Editor/FloorplanStoryTest.cs — Tools/Floorplan/Story Test plays the whole story in the editor and asserts
  every beat. Tools/Floorplan/Dump Story Frames writes all twelve pictures to Logs/StoryFrames at 8x.

Changed:
- Objective.cs — locked, exitLocked, markJewel, a PickedUp event and SetLocks(). The way out now stays dead
  grey while either socket is empty, and the jewel has no minimap dot at all: the map gives you a wing.
- GameRun.cs — StoryPlaying gates IsRunning, so a cutscene freezes the clock, the demons and the player
  WITHOUT touching Time.timeScale. That matters: StoryCards.Hold refuses to advance while IsPaused, so
  pausing for a cutscene would have deadlocked the word card underneath it. WonSequence hands the win to
  the director's ending frames when there is one, and falls back to the icon when there is not.
- StoryCards.cs — deferOpening + ShowOpeningCard(), so the director lands the card on the theft frame.
  Also fixed audit item L5 here because the new code depends on it: OnDisable now stops the coroutine and
  clears the queue, so IsShowing can no longer stay true forever and hang the caught sequence.
- MenuScreen.cs — Open() refuses while a cutscene is playing.
- Decoy.cs — a public static Emit(), so a solved clue can make a noise. Solving one pulls demons to you.
- GameAudio.cs — PlayClue() and PlayUnlock().
- Editor/FloorplanPlaytest.cs — holds its clock at the start line while the opening frames play, or the
  story's thirteen seconds would have been scored as a stall and reported as a broken level.

Two design calls, both easy to reverse:
- Dying keeps your solved clues; only winning wipes them. Losing the whole search to one mistake would make
  the game unwinnable in practice.
- The clue sits in the wing it eliminates. You search a wing, find a mark, that wing is ruled out. It needs
  no explanation, which is the whole point.

Verified: Roslyn compile clean (runtime + editor). Build Scene placed 5 clues over 6 wings, jewel in wing 2
at 80,21, activator 50 units from the way out, no exceptions. Tools/Floorplan/Story Test passed all 38
assertions end to end, including demons 7.00 -> 8.54 speed, detect 1.50 -> 0.90 s, vignette 0.58 -> 0.66,
heartbeat reach 30 -> 45 on pickup, and a clean hand-back after the ending. All twelve frames dumped and
inspected; the ending's eye was redrawn because the first version read as a red plus sign.

Not done: the WebGL build is still the old one from before the darkness, heartbeat, settings and story work.

## 2026-09-17 — Codex redrew the story art

Handed Codex the whole art surface of the story with write access, and it did the work itself. Brief
covered both jobs, the three-colour rule, the no-letters rule, the equal-row-length trap, and the list of
things in PixelFrames.cs that other code depends on and must not change.

Job 1, the two world icons in Editor/FloorplanSetup.Story.cs:
- ClueArt: was an 11x11 diamond-with-a-cross that read like a road sign. Now a 13x13 broken, asymmetrical
  scratch mark. It is meant to look scored into plaster rather than printed, which is what the GDD's
  "strange symbols appear" asks for.
- ActivatorArt: was a plain 7x13 key, too thin to read at 1.4 world units. Now 13x15, a wider key with an
  eye at the bow. A key is still the right idea because it is the thing the portal is missing.

Job 2, the six story pictures in PixelFrames.cs. Codex redrew all six compositions and the sprite arrays
inside them, added PlayerStride, Scar and Flame, widened Demon from 10 to 12, and put real animation in
every frame rather than a single blinking element. It kept rows 16 to 25 of the Theft frame black on
purpose so the four opening words stay readable where they are drawn on top of it.

Codex's own validation: it wrote a PowerShell harness that compiles PixelFrames.cs, renders all twelve
variants, asserts every pixel is one of the three opaque colours, asserts the Theft text band is black,
asserts every frame actually animates between its two variants, and asserts the Filled eye still opens at
1.6 s rather than immediately. It reports all of that passing.

Verified here independently, not taken on trust:
- A checker script over both files: all twelve art arrays have equal-length rows and use only legal
  characters; the palette is still exactly black, (70,70,70) and (237,28,36); Width/Height, the Frame enum
  and the Variant/Get/Render signatures are unchanged.
- Roslyn compile clean, runtime and editor.

NOT yet done for this art pass: the frames have not been re-dumped through
Tools/Floorplan/Dump Story Frames and looked at, and Tools/Floorplan/Story Test has not been re-run since
the art changed. The Story Test does not touch art, so it should still pass, but that is an expectation
rather than a result. Backups of both files before Codex touched them are in the session scratchpad as
PixelFrames.cs.bak and FloorplanSetup.Story.cs.bak.

## 2026-09-17 — Codex resumes Claude's art/story handoff
- Read Claude's latest local chat and this journal. Scope is completing the pending art inspection, installing any ungenerated story sprites, and validating the story after the redraw.
- Claude's final handoff confirms the code compiled but the twelve new frame variants had not been visually inspected and the story test had not been rerun. The earlier web-build hold remains in place.
- Loaded Unity project guidelines. Editor is idle, not compiling, with clean `Assets/Scenes/main.unity` open.
- Initial editor-file lookup used `Assets/Editor`; corrected to the actual `Assets/scripts/Editor` location after inspecting the project.
- Dumped all twelve story frames through Unity's shipping `PixelFrames.Render` code and visually inspected them: crumbling planet, portal descent, theft with clear text band, darkening school, escape, restored planet/eye. All variants contain only the allowed opaque palette (the opening planet intentionally has no red).
- Found the integration gap: installed ClueMark is still 11x11 and ActivatorMark is still 7x13, despite new arrays being 13x13 and 15x13 (width x height). Backed up main scene and both sprites/import metadata under `Logs/Codex/art-story-20260917` before updating them.
- The one-off art refresh command was rejected by Unity's tool namespace validator because it imported System.Reflection; nothing was changed by that command. Implementing a normal editor menu for targeted story-art regeneration instead.
- Found the startup explanation still teaches the old jewel-only objective. Updating its wordless lessons to teach the clue hunt and the key/two-socket escape, preserving the movement, hiding, decoy and skip controls. Backed up the intro scene and affected source before editing.
- First post-redraw story run passed all 45 assertions (counted from the actual report; the earlier handoff said 38); the Unity console query returned no entries, but the complete PASS report is in `Logs/Editor.log`. The editor stopped normally.
- Added `Tools/Floorplan/Refresh Story Artwork`: regenerates only clue/key assets and updates their world/minimap sizes without rebuilding the level. Applied it and saved main: clue 13x13 at 1.5 units, key 15x13 at 1.4 units, map markers 2.4/3 units.
- Extended the wordless startup to five lessons: movement, hiding, decoys, solving five clues to narrow six wings to one, then jewel + key + both portal sockets. The old jewel-alone exit demonstration is removed. StartupSetup now updates existing Intro scenes with the story icons.
- Unity compiled the changes. The only reported error is the previously documented third-party A* insecure HTTP update check; no gameplay compile errors.
- Inspected actual Game-view captures of the new clue/search and key/portal lessons at 946x532. Five grey crosses leave a red search box; the escape lesson visibly carries jewel and key and lights both sockets. Images saved in `Logs/Codex/art-story-20260917`.
- Confirmed Previous/Next moves correctly between clue and escape lessons. Now checking automatic Intro -> main title transition and final scene wiring.
- Runtime checks passed all twelve frame variants for dimensions/filtering/palette, actual animation differences, the theft's black text band, and the eye opening at 1.6 seconds.
- Confirmed all five lessons automatically transition to the idle main menu; Start then launches the story.
- Actual theft screenshot exposed two integration issues absent from the story assertions: runtime clock rows appear above the cutscene, and the two-line opening card extends beyond the reserved black band. Fixing both. Also updating the menu's objective pictograms to include clues and the key.
- A temporary validation command initially used System.Action instead of EditorApplication.CallbackFunction; corrected and rerun successfully. This was tool-command compilation, not a project source error.
- Final playable story test passed all 45 assertions after sprite installation and overlay fixes. Report saved to `Logs/Codex/art-story-20260917/story-test-final.txt`.
- Fixed RunHud so runtime-generated clock rows stay hidden throughout cutscenes. Sized and positioned the opening card within the theft frame's ten-pixel black band.
- Actual runtime overlay assertions passed: HUD hidden=True; all 19 non-space opening glyphs fit inside the band. Visually inspected `theft-final.png` to confirm the demon, words, thief and jewel remain separate and readable.
- Added clue -> jewel + eye key -> portal pictograms to the illustrated title as well as its fallback. On very short windows the illustrated title omits this optional row to preserve button space.
- Created `ART_AND_STORY.md` with the launch order, six-frame storyboard, playable beats, palette/word constraints, art locations and editor review tools.
- The inherited Story Test writes through normal game record handling; the two test runs may update editor personal-best values (its pre-existing behavior). Subsequent manual visual checks used temporary `Floorplan.ArtReview.*` score keys, removed after stopping Play. No production record keys were deleted.
- One temporary screenshot command encountered Unity tooling's Image namespace collision; using the fully qualified UnityEngine.UI.Image type resolved it. No source-code compile error resulted.

### Art/story pass complete
- Final menu capture (`title-final.png`) visibly includes the clue, jewel, key and exit pictograms beneath the usable Start/Quit buttons, without overlapping controls or records.
- Latest compilation completed and the final Play-session console query returned zero errors/exceptions.
- Verified build order: `Assets/Scenes/Intro.unity`, then `Assets/Scenes/main.unity`. Editor Play entry remains Intro.
- Left Unity stopped, with `main.unity` clean and saved. Saved assets and removed temporary visual-test score keys after stopping.
- Completed the requested art/story handoff and accurate startup explanation. No web rebuild or publishing was performed; the existing web build still predates these story changes. The earlier independent broad code review was outside this finishing pass.
- Review documents: `ART_AND_STORY.md` and this log. Evidence: all twelve story-frame PNGs in `Logs/StoryFrames`, plus screenshots, before-edit backups and two passing story-test reports in `Logs/Codex/art-story-20260917`.

## 2026-09-17 — What remains for a finished game
- User asked what is left before the game is done. Reviewed the current art/story handoff and remaining items in this journal; loaded Unity guidelines.
- Created `FINISH_CHECKLIST.md`, separating required release verification from optional expansion. Priorities: ordinary full playthroughs and balance, unfamiliar-player comprehension, outstanding code review, current exported build, build-specific verification, and release packaging.
- Explicitly distinguished the 45 passing story-wiring assertions from ordinary playtesting: the story test teleports between objectives. The browser build remains stale.
- No new gameplay changes, tests, build or publishing performed for this assessment. No ongoing task was started; the previous stopping point is preserved.

## 2026-09-17 — Blinking left mouse button
- User supplied the mouse tutorial icon and requested a blinking red left button.
- Updated IntroSequence's distraction lesson: an overlay fills only the left button recess of the existing 10x10 mouse sprite. It alternates red/black every 0.35 seconds; the body remains grey and the right button remains unchanged.
- The overlay is rebuilt/cleared with each lesson and uses the existing unscaled tutorial animation clock. No image assets or story words changed.

## 2026-09-18 — Review fixes, fresh web build, and the demon comes alive

Review fixes (all verified by compile + Story Test; records guard verified against the registry):
NpcVision resets between runs; jewel, clues, key and exit can't be used from inside a locker; minimap
crosses and unsolved clue marks are black instead of the map's own grey; the key keeps its proportions;
the exit's map mark is a hollow ring instead of a jewel; a difficulty change starts the building over
(GameRun.RestartFresh); demon/catch logs only in editor and development builds; both editor tests put
the real best-time records back afterwards (Editor/TestPlayerPrefsGuard.cs); personal paths scrubbed from
the work notes and .claude/ gitignored.

Web build: Tools/Floorplan/Build Web Player succeeded, 12 MB in Build/Web. Loaded in a browser: intro,
title, the opening pictures and gameplay all ran with no errors. A full run was not played in the build.

Enemy animation: the demon hovers on its flame, so Codex drew it that way after a first walking pass was
rejected. Five strips in Assets/Enemy ("enemy idle/walk/chase/search/alert.png", 8/8/6/8/4 frames,
100 px cells on the original sprite's grid). The flame changes every frame; red cracks and embers in the
body show the state; red inside the body is always solid so it never shows the floor through.
Builder changes that came with it:
- The demon is sized from the original demonguy sprite and centred on the strip frame, so a redraw with
  a shorter flame no longer makes the demon grow (it would have been ~10% bigger).
- Found and fixed a long-standing bug: BuildEnemyAnimator ran once per demon and recreates its asset, so
  every demon except the last pointed at a deleted controller. The first demon, the one always awake,
  had no animation at all. It is now built once and shared; all three demons link to it.
Story Test after the rebuild: passed, 45 checks.

## 2026-09-21 — Release fixes from the external review

All verified against the code first; Story Test passes 46 checks after them.
- Difficulty locks once a clue is solved, so a stray A/D or arrow press on the pause menu can't wipe the search.
- The game pauses (opens the menu) when the browser window loses focus. Editor excluded so tests aren't interrupted.
- The title screen reopens after the ending instead of dropping the player into a live run. Story Test updated.
- The carried jewel hides with the player inside a locker.
- Mid-run word cards ("HE WANTS IT BACK", "LONGER") are drawn smaller, above the player and below the minimap.
- Hidden-player discovery measures from where the demon stands; point-blank coins stop short of walls; the
  volume slider saves once on release; the opening card no longer depends on its text being in capitals;
  Right Shift sprints; stale comments fixed; web page label says Jewel of the Devil.
Left for the owner: whether to teach Shift in the intro and whether sprinting should make noise. The web
build has not been rebuilt with these fixes.

## 2026-09-21 — Release prep: name, licence, history, version 1.0

- Name settled: Jewel of the Devil. The owner and a friend have both played the browser build end to end.
- README.md added at the repo root: premise, how to play, controls as actually bound, how to open and
  build, credits (Andrew D. with Claude; Bryant with Codex; A* Pathfinding Project by Aron Granberg).
- LICENSE added: MIT, copyright Andrew D. and Bryant. The README notes Assets/AstarPathfindingProject is
  third-party and not covered by it.
- Git history scrubbed: the Initial commit carried the owner's Windows username and local paths in three
  work-folder files. Both commits were rebuilt with those removed; authors, dates, messages and the second
  commit's files are unchanged. Committed "Add README and MIT license" on top (no co-author line, at the
  owner's request). A full pre-scrub backup bundle was kept outside the project.
  PENDING, owner's side: the force-push. This shell has no GitHub login, so run
  `git push --force-with-lease origin main` from the owner's own terminal, and do NOT "Pull origin" in
  GitHub Desktop first (it would merge the old history back). Anyone else with a clone should re-clone.
- Version bumped to 1.0 in Editor/WebBuild.cs (the build script writes it over Player Settings).
- Final web build, 1.0: succeeded 2026-09-21 10:29 in 3.5 min, 12 MB in Build/Web, with every fix above.
  Zipped for itch.io as Jewel-of-the-Devil-web-1.0.zip with index.html at the top level.
- Second devlog written in the same format as the first.
- A 10-agent review workflow was started and stopped at the owner's request (too many tokens); it produced
  nothing. The owner supplied an external review instead; every claim was checked against the code before
  fixing (see the entry above).

Still open for release: the force-push; whether to teach Shift in the intro and whether sprinting should
make noise; the itch.io page (cover 630x500, screenshots, description, controls, credits, HTML project,
1280x720 viewport, fullscreen button on, mobile off); and one full run on the uploaded itch.io page before
publishing. Saved records may reset per upload on itch.io, so upload the final build once.

## 2026-09-21 — Playtest and red coin

- Story Test: passed, 46 checks.
- Play Test (30 s), run 1: failed on one check, the demon's centre inside a wall for 5 of 1,122 samples (a brief corner cut on a random route). Run 2 was clean, 0 samples. Worth watching, not blocking.
- Web build 1.0 played in a browser: loads to the title, the opening plays, movement works, a clue solves (wing crossed off the minimap, first pip red), the coin throws and Esc pauses. No console errors. A demon caught the player near the start after about 34 s, unseen in the dark.
- The thrown coin was white, which breaks the three-colour rule. `DecoyThrower.coinColor` now tints it red (237,28,36). The Coin sprite stays a white template because the menu tints it grey for the controls hint. It compiles; it is not in a web build yet.
- AI look check: the title art (`Resources/Menu/menu-art.png`) is the giveaway. It is 1672x941 with no pixel grid, a painting reduced to three colours rather than pixel art. The hands have no wrists and read like ribs, and the side buildings have stairs that lead nowhere. The in-game art is real low-res pixel art and does not read as AI.

## 2026-09-22 — Bug hunt: every runtime script read

Fixed:
- The noise ring a coin draws on the minimap was white (the Ring sprite is a white template and nothing tinted it). `DecoyThrower.ringColor` tints it red.
- Intro lesson 2 animated its coin grey while the legend beside it and the real coin are red. It is red now.
- Intro keys: Enter, Space and gamepad A skipped all five lessons, which hid the hiding, coin and clue lessons from anyone pressing them to mean "next". They now go to the next lesson (and finish after the last). Esc and Start still skip everything.
- The main camera still cleared to Unity's default blue (Skybox clear with no skybox). Nothing showed it, because the floorplan covers everything the camera can reach, but it is now solid black. Edited in `main.unity` directly while Unity was closed (two lines).

Checked with no bugs found: GameRun, StoryDirector, NpcChaser, NpcVision, PlayerHider, HidingSpot, PlayerController, Objective, Decoy, SecurityCamera, DemonCount, Clue, MenuScreen (pause, difficulty, focus, volume), GameExit and the web quit page, RunHud, CameraFollow2D, MinimapReveal, GameAudio, PlayerCameraEffects, StoryCards, PixelCutscene, PixelFrames' cache, WebBuild.
- Palette audit in the editor: all 80 sprites and images in `main.unity` resolve to the three colours once tinted, and every sprite asset is three colours plus white templates (only Unity's unused `Welcome/2d-template.png` is not).

Balance note, not changed: the player walks at 8 and sprints at 12.8 with no stamina; demons wander at 6 and chase at 7 (8.5 carrying). A straight chase can never catch the player; catches come from corners and walking into one.

Compiles (Roslyn). Not yet run in Unity: Story Test, Play Test and a look at the red ring. Unity was closed.

## 2026-09-22 — Web build audit (Unity optimize-web skill)

- Read every Web setting in the live editor. Already right for itch.io: Gzip with the JS decompression fallback (works whatever headers the host sends), Strip Engine Code on, IL2CPP Optimize Size, exceptions Explicitly Thrown Only, debug symbols off, data caching on, targetFrameRate -1, Geometric memory growth, .NET Standard 2.1.
- Changed: Wasm code optimisation was Build Times (the development default). `WebBuild.ConfigureWeb` now sets Disk Size with LTO on every build. That setting lives in `Library/`, not in ProjectSettings, so setting it in the build script is the only way it travels to a fresh clone. Guarded with `#if UNITY_WEBGL` because the type only exists with Web Build Support installed. Read back as DiskSizeLTO after Configure Web Build. Expect a smaller `.wasm` and a slower build.
- Kept on purpose: managed stripping Low (A* uses reflection), Gzip rather than Brotli (Brotli through the JS fallback is slow to unpack), Wasm 2023 off (browser baseline, too late to test widely).
- Largest stripped assemblies: mscorlib 1.8 MB, UIElements 1.5 MB, RP Core 1.0 MB, Input System 1.0 MB. Visual Scripting and the other unused packages were already stripped out, so there is nothing safe left to remove this close to release.
- Side effect, harmless: saving ProjectSettings dropped `InputSystem_Actions` from preloadedAssets. The committed entry was a leftover from a build; the Input System's build provider adds it before every build and removes it after (BuildProviderHelpers.cs).

## 2026-09-22 — New title art, final web build

- Title art replaced. The old `Resources/Menu/menu-art.png` came from an image generator (1672x941, no pixel grid, AI anatomy). Codex drew the new one pixel by pixel in Python (standard library only, no image model), in two passes: v1 had brick marks that read as the letter E, a cloak that floated like wings and outline-only buildings; v2 fixed all three. The owner chose v2.
- The new file is 320x180, exactly the three colours, and 100% black under the menu block (x 96-224, y 96-180) and both corner controls. Checked with an independent script as well as Codex's own. It scales 4x at 1280x720 and 6x at 1920x1080. Import settings unchanged (sprite, point, no mips, uncompressed).
- Kept for reproduction in `Assets/work folder/title-art/`: `draw_title.py`, Codex's `NOTES.md`, and a 630x500 itch cover drawn from the same code. The owner is using their own cover image on itch instead; it is square, so it needs 630x500 letterboxing or itch will crop the title text.
- Web build 1.0 rebuilt with every change since the last one (title art, red coin and noise ring, intro keys, black camera clear, Disk Size LTO). Build took 11 min (LTO); wasm 9.2 MB -> 8.2 MB, total 11 MB, zip 11.7 MB with index.html at the top.
- Checked in the browser: the intro turns one page on Enter and skips to the title on Esc; the new title renders with nothing under the controls; the thrown coin is red and its minimap ring is red. Story Test 46/46 and Play Test (0 wall samples) passed in the editor before the build.

## 2026-09-22 — Harder difficulties (levels 4 and 5)

- The selector now has five levels: three demon faces (1-3 demons, as before), then two skulls (the caught icon) for the hard levels. Levels 4 and 5 wake all three demons and make them more dangerous; no scene rebuild was needed, the new fields take their defaults.
- `DemonCount` owns the level (`SetLevel`, `Level`, `Levels`, `hardTiers`). Tier multipliers, as built -> level 4 -> level 5: chase speed 7 -> 8.4 -> 10.2 (the player walks at 8 and runs at 12.8; with the jewel level 5 chases at 12.4), wander 6 -> 6.6 -> 7.5, time to spot 1.5 s -> 1.05 s -> 0.68 s, sight 12 -> 14 -> 16 (the player's light radius), locker find radius +0.5 / +1, search time +1.5 s / +3 s.
- `NpcChaser.SetStrength` and `NpcVision.bonusDistance` hold the tier apart from `chaseSpeed`, `detectTime` and `viewDistance`, which the story director raises while the jewel is carried and restores after.
- The menu compares levels, not awake counts, so moving between 3, 4 and 5 (all three demons) still restarts fresh; the difficulty lock after a solved clue still applies. Saved levels 1-3 keep working.
- Faster demons cut corners: at level-5 speed the Play Test found the demon's centre inside a wall in 9 samples (half-radius overlap 20). `NpcChaser.Awake` now sets `AIPath.constrainInsideGraph`, which keeps the agent on the walkable grid (already an agent radius from walls). Same stress test after: 0 inside, 0 half-radius, no stalls over 1 s. Normal speed after: 0 inside, no stalls.
- Also: `FloorplanSetup.ConfigureCamera` now sets the black solid clear itself, so a rebuild keeps it.
- Verified: Story Test 46/46, Play Test at level 5 (live values read back: level 5 of 5, 3 awake, wander 7.5, sight 16), title screenshot with the five-level selector. Editor difficulty put back to 3 afterwards.

## 2026-09-22 — Player animations (in progress at end of day)

- Asked for: more player animations, remembering the player hovers. The original idle strip had no jets at all, so standing still looked grounded.
- Codex brief (hand-placed pixels built from the original body, three colours, 100x100 cells, preview APNGs): `idle player.png` (8 frames, hovering with small flickering jets, replaces the jet-less idle), `sprint player.png` (6 frames, full-power jets for Shift), `caught player.png` (8 frames, jets die and red cracks spread, plays once and holds). Codex finished at wrap-up. The strips, previews, script and notes are in `Build/pending-player-anim/` (gitignored, outside Assets so the builder does not pick them up). Checked: three colours plus fully transparent only, 100x100 cells, idle body pixel-identical to the original in all 8 frames. Previews sent to the owner. **Nothing is installed yet: waiting for the owner's OK.**
- Code, done and inert until the strips exist:
  - `FloorplanSetup`: finds `sprint` and `caught` strips by name, builds Sprint and Caught states (Caught from Any State, no loop, back to Idle when cleared), and a new menu item **Tools/Floorplan/Refresh Player Animations** that rebuilds only the player's animator and saves the scene, without regenerating the level.
  - `PlayerController`: sets `Sprint` while moving with Shift held and `Caught` on GameRun.Caught, with the animator on unscaled time during the death (the caught sequence slows then freezes time). Reset puts it back. Parameters are only set if the animator has them, so the current two-state animator logs nothing.
  - `PlayerHider`: a player found in a locker steps out where the locker is, so the death is visible.
- Verified with the current animator: compiles, Story Test 46/46, no parameter warnings.
- To finish: check Codex's strips (palette, 100x100 cells, body position identical to the original), show the owner, copy the approved PNGs into `Assets/Player/` (idle overwrites in place), run Refresh Player Animations, test a caught run and a sprint, then a web build (ask first).

## Open at end of 2026-09-22

- Player animation art: review and install (above).
- Web build: needed for levels 4-5 and the animations. Ask before building.
- Intro: teach running with a wide keycap and an up-arrow (the Shift symbol, no letters). Recommended, now that levels 4-5 need running.
- Optional: best times per difficulty level (a level-1 win and a level-5 win share one record).
- Owner's side: commit, `git push --force-with-lease origin main` from their own terminal (no Pull origin first), itch page, their square cover image needs a 630x500 version, and the AI-disclosure answer on itch.

## 2026-09-22 — Player animations installed

- The owner approved Codex's strips. `Assets/Player/idle player.png` was overwritten in place (it now hovers, with small jets); `sprint player.png` and `caught player.png` are new. Tools/Floorplan/Refresh Player Animations rebuilt `Player.controller`: Idle (8), Boost (6), Sprint (6), Caught (8, no loop, from Any State), and saved the scene. The original jet-less idle is kept locally in `Build/pending-player-anim/original idle player.png` and in git history.
- Builder fix: the player is now sized from its body only (`VisibleBounds(..., bodyOnly: true)` ignores pure red), because the new idle's first frame includes jets and a rebuild would have shrunk and shifted the player. Checked: the scene keeps scale 6 and offset -0.18, and a rebuild computes the same.
- Verified in play mode: the Sprint state plays the sprint frames (driven through the animator; the editor drops simulated keys while the Game view is unfocused); a real `CatchPlayer` sets Caught, switches the animator to unscaled time, plays the death while time is frozen and holds the last cracked frame; after the restart it is back to Idle on normal time. In-game screenshots sent to the owner. Story Test 46/46 afterwards.
- `Assets/work folder/player-anim/make_player.py` and `NOTES.md` record how the strips were made (no PNGs there, so the builder cannot mistake a copy for the idle strip). Its source folder comes from `PLAYER_SOURCE`; no personal paths.

## Open at end of 2026-09-22 (final)

- Web build with levels 4-5 and the player animations. Ask first (about 11 minutes).
- Intro: teach running (wide keycap with an up-arrow, no letters). Recommended.
- Optional: best times per difficulty level.
- Owner's side: commit (scripts, scene, `Player.controller` and the three strips, title art, work folder), `git push --force-with-lease origin main` from their own terminal with no Pull origin first, itch page, a 630x500 version of their cover, the AI-disclosure answer.

## 2026-09-24 — Running taught and made loud, start grace, per-level records, web build

- The owner beat levels 4 and 5 and asked for: teach running, fix early deaths, make running loud, per-level best times, then a web build. All art by Claude (no Brent).
- Intro lesson 1: a wide keycap with an up arrow (the Shift symbol, drawn, no letters) beside WASD. The figure walks, then Shift lights red and it runs, trailing red noise rings.
- Running is loud: `PlayerController.sprintNoiseRadius` 7, every 0.5 s while moving with Shift, through `Decoy.Emit`, so nearby demons come to investigate. Sets 0 to turn it off.
- Early deaths: `GameRun.catchGraceSeconds` 4. No catch in the first 4 s of a run (RunTime, which does not count the opening cutscene). Verified live: inside the window CatchPlayer is refused, outside it catches.
- Per-level records: `GameRun.SetLevel` and `KeyFor`. Level 1 keeps the original keys, so existing records stay; levels 2-5 use `Floorplan.BestTime.L<n>` / `Floorplan.BestWin.L<n>`. The menu switches records with the selector. Guarded so the menu (which runs first) cannot save an empty record over a real one. The test PlayerPrefs guard now covers every level's keys.
- Editor note: the first Story Test after the recompile hung with "There are no graphs in the scene" (Reload Domain is off, and play started before A* had loaded its graph). The saved graph is identical in every commit, loads fine on demand, and the rerun passed. Web builds start fresh and are unaffected.
- Verified: Story Test 46/46, Play Test clean (0 wall samples), web build 11 MB in 5 min (wasm 8.2 MB), browser: title with five levels, a run starts, the hovering player, no console errors. Zip `Jewel-of-the-Devil-web-1.0.zip` sent to the owner.

## Open at end of 2026-09-24

- Owner's side: commit today's changes, itch page (cover 630x500, AI disclosure: yes for graphics), one full run on the itch page before publishing.

## 2026-09-24 — External audit applied, web build 2.0

Every claim was checked against the current code first; all held.
- 1.1 A* path logging off: `GameRun.Awake` sets `logPathResults = None`, the builder does too, and the scene is saved with it. Browser run: 0 "Path Completed" / "Path Failed" lines (the old build logged ~150 in a minute).
- 1.2 A demon already chasing now picks up the jewel speed-up: `NpcChaser.Update` re-applies `SpeedFor(CurrentMode)` every frame.
- 1.3 No finding a hidden player through a wall: the locker check now needs a clear line (`vision.wallMask`).
- 1.4 `autoSyncPersistentDataPath: true` in the page. Browser: no "Manual synchronization" warning; volume kept across a reload.
- 1.5 Version 2.0; web build 11 MB in 5 min; zip `Jewel-of-the-Devil-web-2.0.zip`.
- 2.1 `NpcChaser.ResetRun` returns early for a demon that never ran Awake. Checked on difficulty 1 with a forced restart: the two sleeping demons keep a valid rotation.
- 2.2 PlayerController enables and disables only Move and Sprint; PlayerHider and DecoyThrower disable their own actions.
- 2.3 Floorplan Read/Write off (the builder turns it back on for its own bake, then off again).
- 3.1 The clock scales with the window (`RunHud.scaleWithWindow`). 3.2 `MinimapMarkerPulse` on the player's minimap dot (scene and builder). 3.3 No difficulty arrows while it is locked. 3.4 A mouse click aims the coin at the pointer (Enter and gamepad still throw the way you face). 3.5 was already done.
- 4.1 `_Recovery`, `SampleScene` and `Welcome` moved to the Recycle Bin. The A* ExampleScenes and Documentation were left alone: the A* folder is never modified.
- 4.2 Removed collab-proxy, iet-framework and visualscripting. Kept com.unity.ai.assistant (the editor connection Claude uses runs through it).
- 4.4 "The Debt" removed from comments and the build log. The template and quit-function rename was skipped (six coordinated edits for no player-visible gain).
- 4.5 README names the Asset Store Free License. Open question for the owner: whether that license allows A*'s source in a public repo.
- Not done: 4.3 (FloorplanAutoBuild, harmless, left), 4.6 (Palette refactor, no behaviour change).
- Verified: Story Test 46/46, Play Test clean on difficulty 1, browser checks above.

## 2026-09-24 — Hidden clues on the map, a new building every run

- Clue marks on the minimap are hidden until found: `Clue.Revealed` / `SetRevealed`, and `StoryDirector.RevealClues` reveals a clue once the player has been within `clueRevealRadius` (12) with no wall in between. A reveal lasts through deaths; a fresh building hides them all again. The clue itself on the floor is unlit red as before, so it is spotted on screen.
- New layout on every fresh start (first run, after a win, a difficulty change; not after a death): `StoryDirector.Shuffle`. It reads the six wings back from the scene in Awake (clue spots, the jewel's spot, cross and search-box centres), so no rebuild was needed, and gives the jewel's original wing a copy of a cross. The jewel goes to a random wing other than the spawn's (and never the same wing twice in a row), the five clues fill the rest, and the key goes to a random wing at least `keyDistanceShare` (0.7) as far from the spawn and the jewel as the furthest. `Objective.SetJewelHome` moves the jewel and where resets put it. Off switch: `shuffleLayout`.
- Bug caught while testing: the start wing was read from the player's current position, which after a win is the exit. The spawn is now recorded once.
- Checked in play mode: 40 fresh deals gave 5 jewel wings and 9 jewel-and-key pairs, every spot walkable and reachable from the spawn, the key never on the jewel, no clue marks shown at the start of a building, and a clue revealed when approached. Story Test 46/46, Play Test clean. Not in a web build yet.

## 2026-09-24 — Running noise ring, win result, quality setting; new demon art for review

- Running shows its noise: `DecoyThrower.ShowNoise(at, radius)` (the coin's minimap ring, now public with a radius); PlayerController draws it each time running makes noise.
- After a win the title shows the gem and that run's time in large digits where the objective strip sits. A new record for the level blinks and gets an up arrow; otherwise it is grey above the red record. `GameRun.LastWinSeconds` / `LastWinWasRecord`. Cleared when the next run starts.
- Quality setting: `GraphicsQuality` (Low/Medium/High = render scale 0.5 / 0.75 / 1, Point upscaling so pixels stay sharp; the UI is an overlay and stays full size). All six Unity quality levels share one URP asset, so this is what actually changes cost. Three rising bars at the left of the settings row; click or Q cycles; saved like the volume; default High. In the editor it restores the URP asset when play ends (checked: the asset on disk is unchanged).
- Verified: Story Test 46/46; screenshots of the title with the bars, Low quality in play with the noise ring, and the win result with the record arrow. The level-5 test records made while checking were deleted.
- New demon, drawn by Claude (no Codex): "the Listener", blind, hunts by sound. Bat ears in place of horns, no eyes, a zigzag mouth, a ribbed black body, grey dithered smoke in place of the red flame. Ears act out the state: twitch (idle), cocked side to side (search), snapped up (alert), folded back (chase). Head and ears hand placed in `listener/draw_listener.py`; same 100x100 cells, three colours only. Sent to the owner for approval; not in the game.

## 2026-09-24 — The Listener added (levels 4 and 5)

- Owner approved the art and the behaviour. Strips in `Assets/Listener/` (own folder: excluded from the player-sheet search, and not matched by the enemy-strip search); drawing script kept in `Assets/work folder/listener-art/`.
- Behaviour: `NpcVision.blind` (never sees, draws no cone) and `NpcChaser.hearingMultiplier` 1.6 (coins, clue solves and running reach it from 1.6x as far). It still catches on touch, and it still gets the hard tier's speed.
- Levels: `DemonCount.hardOnly` / `hardOnlyAttachments`, woken only past the plain demon counts, so levels 1-3 are unchanged and 4-5 have three demons plus the Listener.
- `Tools/Floorplan/Add Listener` (FloorplanSetup.Listener.cs) copies the last demon with its minimap dot, pathfinding and warning icon, gives it an AnimatorOverrideController of the shared demon animator with the Listener's clips, makes it blind with sharp hearing, and registers it; running it again replaces it. Build Scene calls the same step at the end, so a rebuild keeps it.
- Bug caught: the first placement used the hint point unchecked, because the graph has no nodes in edit mode, and 82,30 is inside a wall. Add Listener now scans the graph first and uses the nearest floor in the main region: placed at 82,32, walkable and connected to the player (checked in play).
- Checked in play on level 5: the Listener is awake, draws no cone, and a player standing 3 units in front of it for 2 s drew no suspicion; a noise 16 units away sent it searching while an ordinary demon 21 away ignored it. Story Test 46/46 (run on level 5, Listener awake); Play Test clean twice. Not in a web build yet (owner: web build tomorrow).

## Open at end of 2026-09-24

- Web build with today's work (hidden clues, shuffled layout, running noise ring, win result, quality setting, the Listener), then the browser checks and the zip.
- Screenshots and a short GIF for the itch page, after the build.
- Owner's side: commit today's work; the A* license question for the public repo; itch page; cover at 630x500.

## 2026-09-25 — Web build with the Listener

- Web build of the 24 September work (hidden clues, shuffled layout, running noise ring, win result, quality setting, the Listener). 11 MB in 6 min. Zip named `Jewel-of-the-Devil-web-2.1.zip` for the owner's playtest (the in-game version string is still 2.0).
- Browser: the intro works; the title shows the quality bars and five levels; a level-5 run starts; the console has no errors, path lines or sync warnings; level 5 is kept after a reload.
- Next: the owner's playtest, fixes, final build, itch screenshots and GIF.

## 2026-09-25 — Settings panel: change the controls, quality moved in

- Owner asked for a settings menu where the controls and the quality can be changed.
- `ControlBindings` (new): seven player keys, each one keyboard binding in the input asset (up, left, down, right = the WASD composite parts; run = left Shift; hide = E; throw = Enter). Stored as Input System binding overrides in PlayerPrefs `Floorplan.Controls`. Allowed keys: letters except R, Space, Enter, left Shift, because each can be drawn without a word; R and Esc keep restart and pause. The arrow keys, right Shift, the mouse click and the gamepad stay as fixed extra bindings. A key already in use swaps with the old one.
- MenuScreen: a gear (drawn at runtime from a pixel map, like the locker and reset icons) replaces the quality bars in the top-right row. It opens a panel over the lower title: back, quality bars and reset along the top, then arrow/run/locker/coin icons each beside a key cap. Click a cap (it blinks red), press a key; Esc or a click elsewhere cancels. Esc closes the panel, not the menu. Works on the pause screen too.
- IntroSequence: takes the input asset (set in Intro.unity and in StartupSetup) and draws the player's chosen keys on its key caps, including the Shift lesson.
- Verified: Story Test 46/46; editor checks of set, swap, save/reload and reset; browser: opened the panel from the gear, changed hide to K with a real key press, reloaded, and the intro's hide lesson showed K; console clean.
- itch material in `Build/itch/` (gitignored): `1-title.png`, `2-story.png`, `3-hunted.png`, `4-settings.png` (1910x1074), `demon-approach.gif` (955x537, 52 frames, 0.6 MB), `itch-page.md` (settings, description, controls, credits, AI disclosure), and the zip. Editor screenshots need `InternalEditorUtility.RepaintAllViews()` first, or the Game view is stale while the editor is unfocused.
- README updated: hidden clues, shuffled building, loud running, click-to-aim coin, the settings panel, the five levels and the Listener.
- Web build (2.1 zip, in-game version 2.0) with all of the above.

## Open at end of 2026-09-25

- Owner: playtest the 2.1 zip (upload to itch as a draft), report anything unfair or confusing.
- Owner: the A* licence question for the public repo.
- Owner: cover image for the 630x500 version; the itch page (text ready in Build/itch/itch-page.md).
- Release on the 30th.

## 2026-09-25 — Credits on the startup splash

- Owner asked for a "made by Andrew and Bryant" screen. It breaks the ten-word rule; asked, and the owner chose to break it for the credits only (recorded in memory). Then asked for it on Unity's startup splash rather than a separate screen.
- `Assets/Splash/credits.png` (556x88): "MADE BY" in grey over "ANDREW AND BRYANT" in red, built from the game's own 7x7 letter glyphs at 4x, transparent background.
- `WebBuild.ConfigureSplash` (runs on every Configure/Build): imports it as a point-filtered sprite and sets the splash to black, the credits logo for 3 s, Unity's logo below (UnityLogoBelow, LightOnDark), and a static splash (no zoom, which made the pixel letters shimmer).
- Checked in the browser: the splash shows the credits above "Made with Unity", crisp, then the intro starts. Zip in Build/itch/.

## 2026-09-28 — Demon voices (approved by the owner on 2026-09-25)

- `DemonSounds.cs` generates each demon's sounds at runtime: Groaner (groan; snarl, deep snarl, heavy grunt), Whisperer (whisper; short snarl), Clicker (bone clicks; grunt, slow triple grunt), Listener (sniffing; sinking snarl). Each has a scream. The owner rejected the breath "notice" sound and grunt H.
- `DemonVoice.cs`, added by `GameAudio.GiveVoices()` in name order, with the blind demon always the Listener. It plays its idle sound every 5-10 s while wandering, a notice sound on alert and every 2-3 s in a chase, panned left/right, fading out by 22 units and quieter through walls.
- `GameRun.CaughtBy` lets the catching demon scream over the caught thump.
- `.gitignore` now tracks `Build/Web` and `Build/itch` (owner: all builds go to git). Devlog for 21-27 Sept is in `Build/itch/devlog-2026-09-21-to-27.md`.
- Story Test passed. In play, each demon had the right voice.

## 2026-09-28 — Dome security cameras (owner picked option B)

- The old camera body was a grey oval on the grey floor, so only the red lens showed. The new art is a dome with a black outline and an eye, on a wall arm that stays still while the dome sweeps (`CameraBodyArt` and `CameraMountArt` in FloorplanSetup.Features.cs; drawing script and preview in `camera-art/`).
- `ApplyCameraLook` is shared by the level builder and the new menu item "Tools/Floorplan/Refresh Security Cameras", which updated the 6 cameras in main.unity without a rebuild. The lens now sits in the dome's eye and turns with it, and the view cone starts at the dome.
- In the dark, only the lens still shows. The owner chose no faint body: "as long as it moves people will know".
- Story Test: passes at level 1. Two runs failed at the saved editor difficulty (level 5), where the Listener or a hard demon caught the teleporting test player. The test does not set the difficulty; the owner's saved level 5 was put back afterwards.

## 2026-09-28 — Web build 2.2

- Version 2.2, with the demon voices and dome cameras. It built in about 6 minutes (11 MB). In the browser, the credits splash, title, story and gameplay all ran with a clean console.
- Zipped to `Build/itch/Jewel-of-the-Devil-web-2.2.zip` (11.2 MB), with index.html at the zip root. `itch-page.md` now points at 2.2, and its AI note now says Yes for sound, because the demon voices were made in code by Claude.

## 2026-09-28 — itch page assets

- `Build/itch/itch-description.html` is the description in plain HTML (itch strips CSS and scripts). `banner.png` (960x300) is cut from the title art. The theme colours are in `itch-page.md`.
- The owner rejected the AI cover for looking AI-made. The new cover is drawn by `cover-art/draw_cover.py`: the title-art scene plus "JEWEL OF THE DEVIL" in the game's own pixel letters. It is swapped in as `Build/itch/cover-630x500.png`.

## Open at end of 2026-09-28

- **Bug, fix on 2026-09-29: some dome cameras float off their wall.** The owner's screenshot shows grey floor between the mount plate and the wall. `ApplyCameraLook` places the mount at a fixed 0.15 from the camera origin, but the origin (`spot.position - spot.normal * 0.25`) is not always the same distance from the wall face. Camera 5 touched its wall; others don't. Plan:
  1. In `ApplyCameraLook`, find the real wall face. Call `Physics2D.SyncTransforms()`, then raycast from the origin + up * 2 back along -up for 4 units against `camera.vision.wallMask`. The face's local y is `2 - hit.distance`.
  2. Build the stack from that face: plate overlapping the face by 0.1, then the arm, then the dome; the vision cone goes at the dome's centre. If nothing is hit, keep today's fixed offsets and log a warning naming the camera.
  3. Run Tools/Floorplan/Refresh Security Cameras, then capture all 6 cameras with Capture2DScene to check every plate touches its wall.
  4. Story Test at level 1 (set `Floorplan.Demons` to 1, then back to 5), then web build 2.3, zip, and log.
- Owner still to do: commit, play the itch draft, decide on the A* folder, and answer the Brent/Bryant art credit question.

## 2026-09-29 — Cameras fixed against their walls, web build 2.3

- New `WallFaceY` in FloorplanSetup.Features.cs raycasts back from 1 unit out into the room to the camera's wall and stacks the plate, arm and dome from that face. All 6 cameras found their wall (the mount local y was -0.04 to -0.32; it used to be a fixed 0.375). A capture of each camera shows its plate on the wall.
- Story Test passed at level 1; the saved level 5 was put back.
- Web build 2.3 (incremental, 19 s). In the browser, the intro, title and story ran with a clean console. Zipped to `Build/itch/Jewel-of-the-Devil-web-2.3.zip`, and `itch-page.md` now points at it.

## 2026-09-29 — Cutscene skip button (Brent's art), web build 2.4

- `PixelCutscene`: a skip button in the top-right corner of every story scene. Clicking it, Esc or Start skips the whole scene; any other key still turns one frame. It waits 0.3 s so the click that started the run doesn't count. The art is loaded from `Resources/UI/skip-button.png` and `skip-button-hover.png`, and sized to about a ninth of the screen height at a whole-number scale.
- Brent drew the button but was off sick, so the owner asked to finish his sketch lines "with his vision". `work folder/skip-button/finish_brent_cracks.py` turns his thin pale lines into full lava cracks using his own five bands and his ~26 px width, measured from his finished cracks. `make_game_button.py` crops it to his frame and shrinks it 6x to 192x115; the hover version has a red frame. His original file is untouched.
- Owner decisions (2026-09-29): the word "SKIP" is allowed, as a second exception after the credits, and the orange/yellow lava is allowed as an exception to the palette.
- Story Test passed at level 1 (level 5 put back). Web build 2.4: in the browser, the button shows in the opening, hover turns the frame red, and a click skipped straight into play. Clean console. Zip: `Build/itch/Jewel-of-the-Devil-web-2.4.zip`.
- Later on 2026-09-29: the owner asked for a smaller skip button. It is now 7% of the screen tall with a 2.5% margin, smoothed (bilinear) rather than whole-pixel, and no longer covers the story pictures. Checked in an editor play capture. Not yet in a web build: the owner wants to be asked before each build.

## 2026-09-29 — Web build 2.5 (owner said go)

- Includes the smaller skip button. It built in about 5 minutes (11 MB). In the browser, the button sits small in the top-right corner, clear of the pictures, and a click skipped into play. Clean console. Zip: `Build/itch/Jewel-of-the-Devil-web-2.5.zip`; `itch-page.md` points at it.
- Later on 2026-09-29: the owner flagged camera 3 (lower left, facing right). Its plate sat on a step of a staircase-shaped wall, half over floor. Moved camera 3 from y 29.05 to 28.75, onto the 0.9-long flat face just below (the plate is 0.6 wide), and re-ran Refresh Security Cameras; the capture shows it flush. Story Test passed at level 1 (level 5 put back). Waiting on the owner before web build 2.6. Note: a full level rebuild would place the cameras fresh and lose this hand fix.

## 2026-09-29 — Web build 2.6 (owner said go)

- Includes the camera 3 fix. It built in about 3 minutes (11 MB). In the browser, the game loaded, the skip button skipped into play, and the console was clean. Zip: `Build/itch/Jewel-of-the-Devil-web-2.6.zip`; `itch-page.md` points at it. This is the build to upload for the 30th.

## 2026-09-29 — Mobile support, first pass (not yet built)

- The owner asked to "just try" mobile before launch, and said it is fine if it is not finished. Launch stays on 2.6 (desktop).
- New `TouchControls.cs`. It installs only on touch devices (`Application.isMobilePlatform`, or a touchscreen with no mouse), so on desktop nothing is created. Controls: an OnScreenStick (`<Gamepad>/leftStick`, dynamic origin) at the bottom left; OnScreenButtons at the bottom right for throw (`buttonWest`), hide (`buttonNorth`) and run (`leftStickPress`, held); pause (`start`) at the top left. It adds its own EventSystem and InputSystemUIInputModule (the game had none). It shows only while `AcceptsGameplayInput` is true. Placeholder art is drawn in code in the three colours.
- `DecoyThrower`: while the touch controls are active, taps and mouse clicks no longer throw (the actions asset binds `<Touchscreen>/primaryTouch/tap` to Attack), and there is no mouse aim.
- Menus, intro and cutscene skip read `Pointer.current` instead of `Mouse.current`, so a finger works like the mouse. On desktop, Pointer is the mouse.
- On touch devices the intro lessons show the touch buttons instead of keys and the mouse.
- `GraphicsQuality` defaults to Low on mobile when nothing is saved.
- WebGL template CSS: `touch-action: none` and related rules, so touches cannot scroll, zoom or select the page.
- Checked: layout via an editor play capture (the controls were forced on), and the Story Test passed at level 1 (desktop unaffected).
- Still to do: a web build (ask the owner) and phone testing by the owner. Unknowns: speed on phones, iOS Safari, portrait mode (set itch orientation to landscape), throwing only the way you face on touch, and the settings panel's key rebinding (pointless on phones but harmless).

## 2026-09-29 — Web build 2.7 (mobile test; owner said go)

- Built in about 7 minutes (11 MB). Zip: `Build/itch/Jewel-of-the-Devil-web-2.7.zip` (first sent as "2.7-mobile-test"; that duplicate was removed before commit). The launch build stays 2.6, and `itch-page.md` still points at 2.6.
- Browser check, desktop size: no touch controls, the intro shows keys, the skip button works with the mouse, clean console.
- Browser check, emulated phone (Android user agent, 740x360 landscape; clicks arrive as mouse, not real touch): the intro shows the touch buttons; tapping the skip arrows and play works; the touch controls appear in play; dragging the stick moved and turned the player; the throw button threw one coin; pause opened the menu and tapping play resumed without an extra coin. Clean console.
- The owner says the skip button size is fine on mobile.
- Next: the owner tests on a real phone.
- Later on 2026-09-29: the owner tested 2.7 on a real phone and with a controller: "works perfect for mobile and controller". 2.7 is now the launch build (`Build/itch/Jewel-of-the-Devil-web-2.7.zip`). `itch-page.md` now says to tick Mobile friendly with Landscape orientation, and the itch description and README mention the touch controls.

## 2026-09-29 — Code audit fixes 1, 2 and 4 (not yet built)

- Audit of everything changed since 2026-09-22 (voices, cameras, skip button, touch). Findings: (1) no touch controls on iPad, whose Safari reports a Mac; (2) skipping the opening before the theft frame lost "YOU BORROWED THE LIGHT"; (3) the controls crowd each other in portrait (left for after launch; itch Landscape covers most of it); (4) a misplaced doc comment in GameRun.
- (1) `TouchControls` now always installs a watcher. It builds the controls on a mobile browser, or on the first real `Touchscreen` press, so it catches iPads. On a computer that is never touched, nothing is built.
- (2) `StoryDirector.OpeningRoutine`: if the opening card did not show during the pictures, it shows after them. `ShowOpeningCard` already guards against showing twice.
- (4) `GameRun`: the CatchPlayer and CaughtBy summaries are back on the right members.
- Story Test passed at level 1 (level 5 put back). Waiting on the owner for web build 2.8.

## 2026-09-30 (launch day) — Web build 2.8

- Audit fix 3 added: on touch devices held upright, `TouchControls` covers the screen with a picture (a phone upright, a red arrow, the phone on its side; no words), hides the controls, and opens the pause menu if a run is going. While upright, the canvas scaler matches width so the picture fits.
- 2.8 also carries fixes 1, 2 and 4 from 2026-09-29 (iPad first touch, opening words after a skip, GameRun doc comment).
- The batch-mode build could not run because the owner had Unity open; built through the editor instead (about 4.5 minutes, 11 MB).
- Browser checks: desktop shows keys; Esc on the very first opening picture still shows "YOU BORROWED THE LIGHT"; an emulated upright phone shows the turn picture, and turning to landscape removes it with the intro continuing; clean console.
- Zip: `Build/itch/Jewel-of-the-Devil-web-2.8.zip`; `itch-page.md` points at it. This is the launch build.
