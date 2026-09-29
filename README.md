# Jewel of the Devil

A top-down stealth horror game told in ten words.

Goondalot is dying. King Tesseract steals the Jewel of the Devil from Devilorian, ruler of the
Underworld, to save it. He drops it while fleeing through the School of Goondalot. Devilorian follows
him in, corrupts the building, and fills it with devils. Find the Jewel, find the key to the portal,
and get out alive.

The whole game shows only ten words. Everything else is told through pictures, light and three
colours: black, grey and red.

## How to play

- Find the five marks scratched into the walls. They only appear on your map once you have seen
  them. Stand on one to solve it. Each one crosses a wing of the building off your map. Solving one
  is loud, so the devils come to look.
- The building changes every time you start fresh: the Jewel, the marks and the key move to other
  wings. Dying keeps what you have found.
- When one wing is left, it is boxed in red on the map. Search it for the Jewel.
- Taking the Jewel makes every devil faster and angrier, and reveals the portal key.
- Bring the key back to the portal to fill its second socket, then escape.
- You can only see as far as your own light. The devils hide in the dark, and your heartbeat speeds
  up as they get closer.

## Controls

| Action | Keyboard and mouse | Gamepad |
|---|---|---|
| Move | WASD or arrow keys | Left stick |
| Run (loud: devils nearby hear it) | Left Shift | Left stick press |
| Hide in a closet | E | North button |
| Throw a coin to make a noise | Left click (lands where you click) or Enter (the way you face) | West button |
| Pause | Esc | Start |
| Restart the run | R | Select |
| Mute, fullscreen, quality (title and pause screen) | M, F, Q | |
| Volume down, up (title and pause screen) | - and = | LB, RB |

On phones and tablets (held sideways) the game shows its own controls: a stick on the left, and run, hide and
throw buttons on the right, with pause in the top left.

The keys for moving, running, hiding and throwing can be changed: click the gear on the title or
pause screen, click a key, then press the new one. The arrow keys, right Shift and the mouse click
always work as well. The same panel sets the graphics quality (three bars: lower is smoother on weak
machines).

## Difficulty

Five levels on the title screen. Levels 1 to 3 wake one, two or three demons. Levels 4 and 5 wake all
three at greater strength, plus the Listener: a blind demon that cannot see you but hears coins,
solved clues and running from much further away. Each level keeps its own best time and best win.

## Running it

- **Unity version:** 6000.6.0f1, with the Web Build Support module for the browser version.
- Open the project in Unity Hub. Play starts on `Assets/Scenes/Intro.unity`, which leads into
  `Assets/Scenes/main.unity`.
- `Tools/Floorplan/Build Web Player` builds the browser version into `Build/Web`. The build has to be
  served from a web host such as itch.io. Most browsers will not run it from a file on disk.
- `Tools/Floorplan/Build Scene` regenerates the level from the floorplan image.
- `Tools/Floorplan/Story Test` plays the whole story automatically and checks every step.

## Credits

- **Andrew D.** - design, story, programming, audio. Built with Claude.
- **Bryant** - art, animation, map and minimap. Made with Codex.
- **A\* Pathfinding Project** (Free edition) by Aron Granberg, used for the devils' pathfinding.

## License

The game's own code, art and audio are released under the [MIT License](LICENSE).

`Assets/AstarPathfindingProject` is third-party and is not covered by that license. It is the free
version of the A* Pathfinding Project by Aron Granberg, used under the Unity Asset Store Free License
named in its `Readme.txt`.
