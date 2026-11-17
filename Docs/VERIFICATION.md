# Verification record

Verified on 17 November 2026.

## Unity 6000.6.0f1

All game and editor C# source files compile against the installed **6000.6.0f1** Unity assemblies, using the version-specific API branches. Final compilation completed without C# warnings or errors. The reproducible source check is `Tools/Check-UnityCompilation.ps1` and requires an installed .NET SDK.

Normal Unity import, Play mode, and a Windows build in **6000.6.0f1** are **not verified**. The installed editor is incomplete. Normal startup fails because `UnityPackageManager.exe` is missing. Disabling Package Manager gets further but encounters missing compiler/build tools. Its Windows playback support is also absent. A separate attempt using precompiled scripts still encountered the missing compiler tools. No files in the editor installation were repaired, removed or replaced.

Repair that editor in Unity Hub, open the main project, and rerun **Tactical Game → Validate both maps**, **Run gameplay smoke checks**, and **Build Windows game**. The main project version remains **6000.6.0f1**.

## Separate Unity 2021.3.45f2 verification copy

The complete installed **2021.3.45f2** editor was used on a separate project under the ignored `Artifacts/CompatibilityUnity2021` folder. The game uses APIs available in both editor generations, with conditional branches for newer object-search and built-in-font APIs. The generated scene assets and their `.meta` files are included in the main project for forward import into Unity 6. The newer font is selected explicitly in Unity 6.

The following passed for **both Substation and Rail Depot**:

- Scene validation: two wired objectives, one wired exit, three wired guards, player/cameras/rifle references, no missing scripts, and no missing materials on active renderers.
- Collision clearance along two sampled, continuous walking routes around the central obstacle.
- Initial briefing, 100 player health, 24-round magazine and 120 reserve rounds.
- The exit remains locked before objectives are restored.
- The real rifle raycast damages a target and consumes a round.
- Reload transfers exactly the missing ammunition and consumes reserve correctly.
- Placing a solid wall on the shot ray prevents the same target receiving damage.
- Target defeat disables its colliders.
- Medical and ammunition interactions apply their benefits.
- Pause freezes the mission clock.
- The overhead camera switches correctly and disables player control.
- Terminal B can be completed before A, repeated interaction counts once, and both unlock extraction.
- Extraction produces the win screen.
- Restart resets objectives, health and ammunition.
- The visible electrical hazard damages the player.
- Depleting player health produces defeat.
- An actually expiring mission clock produces timeout.
- Scene changes and fresh rounds work across both maps.
- The template's rigged skinned character is attached to the enemy.
- The guard's initial warning prevents immediate damage.
- An unobstructed guard actually deals 8 damage to the player.
- Inserting a solid wall on that same shot ray prevents further guard fire and damage.
- Defeating the guard stops its attacks.

The Windows standalone build completed successfully. Its final convenience copy is `Builds/Compatibility2021/LastLight.exe`. This executable is a **Unity 2021 build**, not a Unity 6000.6 build.

Manual UI inspection confirmed the template character/rifle models, textured environment, normal first-person view without floating markers, both map views, pause/menu flow, and overhead markers. Standing exposed also produced enemy damage and defeat in the standalone game. The missing template light material caught in the first visual pass was replaced with a compatible emissive material. Decorative world text and floating enemy names are hidden; console status text is mounted on the console itself. The updated executable was left open at Substation's briefing.

The smoke checks call gameplay components directly for deterministic verification; they do not replace a human playthrough of mouse aiming, movement feel, jumping, interaction reach, audio quality, or difficulty. Only the small temporary fixed-attack guard is claimed and tested. The tactical course algorithms remain unimplemented.

Raw build/test logs and compiled outputs stay in the ignored `Logs`, `Artifacts`, and `Builds` directories. The code and editable source assets are the deliverable tracked by the repository.
