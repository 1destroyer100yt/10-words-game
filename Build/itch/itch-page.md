# itch.io page for Jewel of the Devil

Everything below is ready to paste. Build/ is not in git, so this file stays on your machine.

## Project settings (Edit game)

- **Title:** Jewel of the Devil
- **Short description / tagline:** A stealth horror game told in ten words.
- **Classification:** Games
- **Kind of project:** HTML
- **Upload:** `Jewel-of-the-Devil-web-2.8.zip` (desktop, controller and phone, plus the audit fixes). Tick **This file will be played in the browser**.
- **Viewport dimensions:** 1280 x 720
- **Frame options:** tick **Fullscreen button**. Tick **Mobile friendly** and set **Orientation** to **Landscape** (phones get on-screen controls). Leave **SharedArrayBuffer support** off.
- **Genre:** Action (or Survival)
- **Tags:** horror, stealth, pixel-art, top-down, atmospheric, short, minimalist, singleplayer
- **AI generation disclosure:** Yes, for graphics. (Part of the art was drawn by Codex and Claude through code. The demon voices were made by Claude in code, so answer Yes for sound too. Answer No for text.)
- **Cover image:** `cover-630x500.png` (the pixel cover drawn by `Assets/work folder/cover-art/draw_cover.py` from the title-screen art).
- **Screenshots, in this order:** `3-hunted.png`, `2-story.png`, `1-title.png`, `4-settings.png`. Add `demon-approach.gif` as the first screenshot if you want the page to move.
- **Visibility:** Draft (Restricted) until you have played one full run on the page, then Public on the 30th.

## Theme (Edit theme, top of the game page)

itch removes any CSS or scripts from the description, so the look comes from these settings.

- **Banner:** upload `banner.png` (960 x 300, the demon from the title screen).
- **Background:** `#000000`. **Second background** (the panel behind the text): `#0d0d0d`, or `#000000` for one flat black.
- **Text:** `#bdbdbd`. The game's own grey (`#464646`) is too dark to read on black.
- **Links and buttons:** `#ed1c24`, the game's red.
- **Headers font:** pick a pixel font from the list if there is one (Silkscreen or VT323 suit the game); otherwise keep the default. **Body font:** default.
- **Layout:** screenshots in the right column; the game embed above the text.

## Description

Paste `itch-description.html` with the editor's `<>` (HTML) button. The version below is the same text in plain words.

> Goondalot is dying. King Tesseract steals the Jewel of the Devil from Devilorian, ruler of the
> Underworld, to save it, and drops it while fleeing through the School of Goondalot. Devilorian
> follows him in and fills the building with devils.
>
> Find the Jewel. Find the key to the portal. Get out alive.
>
> The whole game shows only ten words. Everything else is told through pictures, light and three
> colours: black, grey and red.

**How to play**

- You can only see as far as your own light. The devils hide in the dark; your heartbeat speeds up as they get closer.
- Find the five marks in the walls. Each one you solve crosses a wing off your map, and is loud.
- When one wing is left, search it for the Jewel. Taking it makes every devil faster.
- Bring the portal key back to the way out, and escape.
- The building changes every time you start fresh.

**Controls**

| | Keyboard and mouse | Gamepad |
|---|---|---|
| Move | WASD or arrow keys | Left stick |
| Run (loud) | Shift | Left stick press |
| Hide in a closet | E | North button |
| Throw a coin | Click (lands where you click) or Enter | West button |
| Pause | Esc | Start |
| Restart | R | Select |

Keys can be changed with the gear on the title screen, which also sets the graphics quality.

**Difficulty:** five levels. Levels 4 and 5 add the Listener, a blind devil that hunts by sound.

**Credits**

- Andrew D. — design, story, programming, audio
- Bryant — art, animation, map and minimap
- A* Pathfinding Project (free version) by Aron Granberg

## Before you publish

1. Upload the zip, open the page, and play one full run to the ending.
2. Try the fullscreen button and the quality bars.
3. Check the page on a second browser if you can.
