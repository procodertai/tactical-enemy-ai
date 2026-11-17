# Multiplayer FPS template assets

This game now uses real assets from **Unity Multiplayer FPS 1.0.8**, copyright © 2026 Unity Technologies ApS. Source: the [official Unity package](https://download.packages.unity.com/com.unity.template.multiplayer-fps/-/com.unity.template.multiplayer-fps-1.0.8.tgz), described in [Unity's template documentation](https://docs.unity3d.com/Packages/com.unity.template.multiplayer-fps@1.0/manual/index.html). The package declares Unity 6000.3 as its minimum editor generation.

Archive SHA-1, verified against Unity's package registry: `d2c8d37dd0c2f9da61eaec978e70d8977f403569`.

The included art is under the **Unity Companion License**, not this repository's MIT license. The original notice is retained in `Assets/ThirdParty/UnityMultiplayerFPS/LICENSE.md` and links to the full license terms. Preserve that notice and attribution when sharing this Unity project.

## What is used

- `Characters/Armature/Model/Armature_arms.fbx`: the template's full rigged body, with its arms-only alternative disabled for enemies.
- `Characters/Armature/Animations/Armature_Shoot_AssaultRifle_F.anim.fbx`: the rifle holding pose, used by an ordinary Animator controller.
- Orange Armature body, arms and legs materials with their original albedo, normal and metallic textures.
- `Guns/AssaultRifle`: the detailed rifle mesh, materials and textures, used for the player's weapon and enemy weapons.
- `Environment/WallFloorKit`: modular panel meshes used on perimeter walls and low cover.
- `Environment/BlockKit_NoStamp` and its BlockKit mesh dependencies: beveled equipment and cargo housings.
- `Environment/Tileables`: floor and wall albedo/normal maps.
- `Environment/WindowKit`, `RoundPlatformsColumns` and `ConnectorLightRailingKit`: windows, depot columns and light fixtures.
- `Audio/Robot_gunfire_01.wav` and `Audio/Robot_footstep_01.wav`: rifle and footstep effects.

Supporting assets from the same kit remain available in the ThirdParty folder for later scene editing. The original archive and source project stay in the ignored `Artifacts/TemplateSource` folder for reference. The playable project does not need that archive.

## Adaptations

The template's full project uses URP, DOTS, GhostBridge and Netcode for Entities. The existing course foundation uses ordinary GameObjects and the Built-in Render Pipeline. The visual import trial confirmed that the meshes, rig, pose and textures work independently of the template's networked game systems.

`TemplateArt.cs` adapts the visual materials to Unity Standard: albedo maps and normal maps are transferred, compatible metallic maps are retained, and model instances are fitted to the existing collision layout. Custom shader-graph tint/decal effects are not reproduced exactly. The light fixture mesh uses the game's cyan emissive material. No claim is made that the original URP lighting or entire template scene was imported unchanged.

The template character retains its detailed skinned mesh. Enemy damage, guard rotation and firing are our small gameplay components. The template network player prefab and its ECS scripts are not attached. The player keeps the existing movement and weapon rules, now with a template rifle model and audio.

The main Unity project still targets **6000.6.0f1**. Its installed editor remains incomplete, so the local executable is verified using a separate **2021.3.45f2** project. Code is also compiled against the 6000.6 assemblies; full 6000.6 editor/player verification still requires the installation repair described in `RUNNING.md`.
