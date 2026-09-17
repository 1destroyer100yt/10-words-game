# Art and story handoff — 17 September 2026

## Launch sequence

`Intro` explains play through five animated, wordless lessons, then opens the illustrated Start/Quit menu in `main`. Starting the game plays the four opening story pictures before handing over control. The menu's replay control returns to the explanation.

The lessons cover movement, hiding, throwing a distraction, solving five scratched clues to narrow six building wings to one, and collecting the jewel and eye key to light both portal sockets.

## Story pictures

| Picture | What the player sees |
| --- | --- |
| World going out | A fractured grey planet and a staff with an empty socket. |
| Way down | A crowned traveller approaching a red portal. |
| Theft | A jewel lifted beneath the horned watcher; the opening words occupy the black middle band. |
| School | Rooms disappear into darkness; scratches and the lost jewel blink. |
| Escape | The traveller carries the jewel toward a burning portal while demons reach after them. |
| Return | The staff is filled, red veins reconnect the planet, and a watching eye opens. |

Each picture is 64×40 pixels, rendered with point filtering and two animation variants. The final eye opens after 1.6 seconds. The only opaque colours are black, grey `(70,70,70)`, and red `(237,28,36)`. White sprite masks take their colour from the game renderer.

The ten existing words remain: **YOU BORROWED THE LIGHT / HE WANTS IT BACK / PAID / LONGER**. No new story words were added.

## Playable story

1. Stand near a blinking scratched clue until it resolves. Its wing is crossed off the map. Solving a clue makes noise.
2. After five clues, the final wing is boxed in red. Search it; the jewel appears only when close.
3. Taking the jewel makes the demons more dangerous, strengthens the heartbeat and darkness, and reveals the eye key.
4. Collect the key to fill the portal's second socket, then return through the portal.
5. The ending plays and a fresh run begins. Death preserves solved clues; a win clears them.

## Where the art lives

- `Assets/scripts/PixelFrames.cs`: six story drawings and animation variants.
- `Assets/scripts/Editor/FloorplanSetup.Story.cs`: scratched clue and eye-key designs, scene wiring, targeted artwork refresh.
- `Assets/Floorplan/Icons/ClueMark.png` and `ActivatorMark.png`: installed world/minimap art.
- `Assets/Resources/Menu/menu-art.png`: illustrated title background from the previous art pass.
- `Assets/scripts/IntroSequence.cs`: five animated explanation lessons.

## Review tools

- **Tools → Floorplan → Dump Story Frames** exports twelve pictures to `Logs/StoryFrames`.
- **Tools → Floorplan → Refresh Story Artwork** applies clue/key redraws without regenerating the level. Save the main scene afterward.
- **Tools → Floorplan → Configure Startup Flow** wires the intro icons and restores `Intro → main` launch order.
- **Tools → Floorplan → Story Test** runs the playable story in the editor. This inherited test teleports through objectives; it is not a walking/navigation test.

Screenshots, backups, and verification reports for this pass are in `Logs/Codex/art-story-20260917`. See `CODEX_WORK_LOG.md` for actual test results and remaining limitations. The web build is still the earlier build; this pass does not rebuild or publish it.
