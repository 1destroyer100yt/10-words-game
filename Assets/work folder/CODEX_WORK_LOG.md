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
