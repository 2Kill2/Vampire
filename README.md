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

## Blood Claim prototype milestone

Defeat the Vampire Thrall, approach within 2.2 units, and hold position in range while Valadrin completes a short claim. Taking damage or leaving range interrupts the claim. A successful claim grants Blood once. Use Q or gamepad right shoulder (RB/R1) to spend Blood on a heavy area strike.

Inspector tuning is available on `ValadrinController` (maximum Blood, claim reward/range/duration, power cost/damage/radius) and `EnemyController` (vulnerable window and claim flash duration).

Manual checks: defeat a thrall and claim once; try claiming again; walk out of range during the claim; take damage during the claim; use the power with insufficient Blood and then with enough Blood; verify attack, dodge, lock-on, movement, restart, defeat, and victory still work. Unity runtime verification is required; these checks have not been run here.

## Blood conversion and target reticle

Blood drains over time to restore health. Tune `Blood Drain Per Second` and `Health Per Blood Unit` on ValadrinController; conversion pauses when health is full and never spends more Blood than needed to fill the missing health. For target lock feedback, assign a Sprite to `Target Reticle` on ValadrinController. Reticle size and world-space target offset are configurable beside it. If the sprite is unassigned, no reticle is drawn.

## Weapon prototypes

Valadrin and Sword Carrier enemies use visible rectangular sword meshes mounted on hilt pivots. The sword turns red only during its configurable active damage frames. EnemyController exposes a dropdown for Sword Carrier, Spear Carrier, and Bow Carrier; only Sword Carrier is implemented so far. Enemy sword timing and dimensions are configurable on the EnemyController component.
