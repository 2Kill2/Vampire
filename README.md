# Bloodbound Unity Project

This Unity 6 project contains the initial playable prototype for Bloodbound, a fixed-camera gothic action RPG starring Valadrin.

## Open the project

Open this folder in Unity Hub with Unity 6000.4.7f1. The first launch may take a few minutes while Unity imports assets.

## Prototype scene

Open `Assets/Scenes/BloodboundPrototype.unity` to edit the blockout room in the Scene view. Environment pieces, Valadrin, the vampire thrall, camera, and lights are saved scene objects; they are not created when Play starts. When the legacy bootstrap-only scene is opened, an editor migration converts it into this saved scene once. The scene has a fixed cinematic camera, stamina-based attacks and dodges, target lock, a simple vampire enemy, health feedback, and an on-screen control guide.

To create or rebuild the prototype scene from the editor, use `Bloodbound > Build Prototype Scene`. Rebuilding replaces the saved prototype scene after an editor confirmation. The builder also creates the materials in `Assets/Materials`.

## Project organization

- `Assets/Scenes`: authored Unity scenes
- `Assets/Scripts/Player`: Valadrin's controller
- `Assets/Scripts/Enemies`: enemy behavior
- `Assets/Input`: editable Input System action assets
- `Assets/Editor/SceneSetup`: editor-only scene builder tools
- `Assets/Materials`: prototype materials

Valadrin's controller uses the generated `ValadrinControls` C# wrapper for its `Gameplay` map, following the input pattern used in Trailhead. The wrapper is generated from `Assets/Input/ValadrinControls.inputactions`; edit the bindings in Unity's Input Actions editor and let Unity regenerate the wrapper.

## Controls

- WASD, arrow keys, or left stick: move
- J, left mouse button, or gamepad X: attack
- Space, left Shift, or gamepad A: dodge
- Tab or right stick click: lock on to the nearest enemy
- R: restart the encounter after defeat or victory

The project uses Unity's Input System package. Controls and systems are scaffolding for iteration, not final game design.
