# Ball Clash — Ricochet Duel

Ball Clash is a Unity 2D ricochet-duel game. Choose a ball fighter, aim once, release, and use its skill at the decisive moment while both balls bounce autonomously through the arena.

## Features

- Eight playable ball archetypes across Fighter, Magic, and Marksman roles.
- Easy, Medium, and Hard bot behaviour.
- Procedural arena rendering, effects, trails, projectiles, status effects, and HUD—no external sprite dependency.
- Eight distinct skills: Meteor Crash, Freeze, Lightning Dash, Web Wall, Void Step, Tornado, Prism Shield, and Gravity Well.
- Mouse drag aiming and an energy-gated skill button.

## Run

1. Open this directory using Unity Hub (Unity 6 / HDRP project).
2. Open `Assets/OutdoorsScene.unity`, then enter Play mode.
3. The `BallClashBootstrap` component creates the game automatically, so no scene wiring is required.

## Project structure

```
Assets/BallClash/Scripts/
  BallDefinition.cs       # fighter data model
  BallClashBootstrap.cs   # scene-independent startup
  BallClashGame.cs        # UI, rendering, physics, combat and bot AI
```

## Controls

- Drag from the player ball to aim and set power.
- Release to begin the duel.
- Use the skill button once the energy meter reaches 100%.

## Source-port note

This project is a C#/Unity port of the supplied HTML canvas game. It retains its gameplay rules, labels, colour language, icons, and procedural visual style. Browser emoji glyphs are rendered by the installed Unity platform font, so their exact pixel shape can vary by operating system.
