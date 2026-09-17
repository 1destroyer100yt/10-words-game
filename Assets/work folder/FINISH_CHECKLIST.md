# What remains before calling the game done

Assessment: 17 September 2026. Based on the current work log and art/story handoff; this assessment did not run new tests or change the game.

The intended small game's full loop is implemented: explanation, title/settings, opening story, clue hunt, jewel, key, escape, ending, death and replay. The editor story test passes 45 assertions. That establishes story wiring, not release readiness or gameplay quality.

## Required finishing work

- [ ] Play the complete current game normally, without teleporting. Finish on each demon setting; check reachable objectives, hiding, distractions, deaths, retained clues, restarts and repeated wins. The story test teleports and cannot prove a fair, enjoyable route.
- [ ] Have someone unfamiliar with it play without coaching. Verify that they understand the scratched clues, map crosses, remaining search wing, eye key, both sockets and the opening/ending pictures. Tune pacing, visibility, clue timing and difficulty based on observed confusion or frustration.
- [ ] Finish the outstanding code review and resolve verified release-blocking findings. Recheck the logged scene-builder behavior that reports a wall-count mismatch but continues; decide intentionally whether noise should pass through walls. Isolate automated story-test scores from real editor player records. These are known follow-ups, not a claim that every item breaks the shipped game.
- [ ] Produce a fresh release candidate for the intended delivery platform. The current web build is stale: it predates the darkness, heartbeat, settings and story additions. The earlier request to consult the user before rebuilding remains in effect.
- [ ] Test that exact exported build from a clean launch through an ending and replay. Check supported browsers/devices, loading, performance, audio, fullscreen, resizing, keyboard/mouse and any advertised controller support, saved settings, pause/resume, Start, Quit and restart. Editor success does not substitute for build testing.
- [ ] Package the release: settle the public title, add the description/controls and credits outside the ten-word gameplay presentation, choose a cover/screenshot, preserve a versioned backup and the tested build, and deliver or publish to the chosen destination when authorized.

## Optional, not needed for this scope

More levels, additional enemy types, more story chapters, achievements, extra visual effects and new music are expansion ideas. They are not prerequisites for finishing this small game.

## Definition of done

A new player can launch the delivered build, understand how to play, complete a fair run, see the ending and replay or quit, without coaching, blocking errors or broken controls. The tested release is saved and ready to hand to another person.

Next useful session: a normal complete playthrough and a fresh-player clarity check, followed by targeted fixes and the release build.
