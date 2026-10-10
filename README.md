# Bloodbound Unity Project

This Unity 6 project contains the initial playable prototype for Bloodbound, a fixed-camera gothic action RPG starring Valadrin.

## Open the project

Open this folder in Unity Hub with Unity 6000.4.7f1. The first launch may take a few minutes while Unity imports assets.

## Prototype scene

Open `Assets/Scenes/BloodboundPrototype.unity` and press Play. The scene is a blockout combat room with a fixed cinematic camera, keyboard/gamepad movement, stamina-based attacks and dodges, target lock, a simple vampire enemy, health feedback, and an on-screen control guide.

## Controls

- WASD or left stick: move
- J or left mouse button: attack
- Space or left Shift: dodge
- Tab: lock on to the nearest enemy
- R: restart the encounter after defeat or victory

The prototype currently uses Unity's legacy input axes for the smallest dependency footprint. Controls and systems are scaffolding for iteration, not final game design.
