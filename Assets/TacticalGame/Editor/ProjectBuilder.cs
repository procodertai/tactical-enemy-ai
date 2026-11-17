using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TacticalGame.Editor
{
    // This authors ordinary, editable GameObjects into .unity scenes and .prefab files.
    // It is an editor tool, not a runtime map generator or an AI algorithm.
    public static class ProjectBuilder
    {
        const string Root = "Assets/TacticalGame";
        public const string SubstationPath = Root + "/Scenes/Substation.unity";
        public const string DepotPath = Root + "/Scenes/RailDepot.unity";
        static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        static Transform environment;
        static Material concrete, steel, dark, white, yellow, cyan, green, orange, red, glass, ground, worldText;

        [MenuItem("Tactical Game/Rebuild starter scenes (overwrites generated scenes)")]
        public static void RebuildFromMenu()
        {
            if (!EditorUtility.DisplayDialog("Rebuild starter scenes?", "This replaces both starter scenes, shared materials and prefabs. Save any custom scenes under different names first.", "Rebuild", "Cancel")) return;
            BuildAll();
        }

        public static void BuildAll()
        {
            Directory.CreateDirectory(Root + "/Scenes");
            Directory.CreateDirectory(Root + "/Materials");
            Directory.CreateDirectory(Root + "/Prefabs");
            AssetDatabase.Refresh();
            TemplateArt.PrepareMaterials();
            MakeMaterials();
            BuildScene(false);
            BuildScene(true);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(SubstationPath, true), new EditorBuildSettingsScene(DepotPath, true) };
            PlayerSettings.companyName = "Course Project";
            PlayerSettings.productName = "Last Light - Tactical Training";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = true;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            QualitySettings.vSyncCount = 1;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowDistance = 65f;
            QualitySettings.antiAliasing = 4;
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(SubstationPath);
            Debug.Log("LAST LIGHT: Both scenes, reusable prefabs and build scene list are ready. Open Substation and press Play.");
        }

        static void MakeMaterials()
        {
            Materials.Clear();
            ground = Mat("Asphalt", new Color(.12f, .17f, .19f), .15f);
            concrete = Mat("Concrete", new Color(.36f, .43f, .44f), .05f);
            steel = Mat("Blue Steel", new Color(.15f, .27f, .32f), .45f);
            dark = Mat("Graphite", new Color(.055f, .085f, .10f), .35f);
            white = Mat("Ivory", new Color(.77f, .80f, .74f), .1f);
            yellow = Mat("Safety Yellow", new Color(.96f, .64f, .16f), .2f);
            cyan = Mat("Terminal Cyan", new Color(.13f, .75f, .83f), .4f, 1.6f);
            green = Mat("Online Green", new Color(.19f, .88f, .48f), .3f, 1.5f);
            orange = Mat("Target Orange", new Color(.89f, .32f, .13f), .2f);
            red = Mat("Warning Red", new Color(.94f, .12f, .055f), .1f, 1f);
            glass = Mat("Window Blue", new Color(.15f, .43f, .51f), .7f, .15f);
            // Reuse template PBR tile sets on the original circulation/collision layout.
            ground.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TemplateArt.Root + "Environment/Tileables/Tex_FloorTileable1x1_BaseColor.png");
            ground.mainTextureScale = new Vector2(22, 20);
            ground.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TemplateArt.Root + "Environment/Tileables/Tex_FloorTileable1x1_Normal.png"));
            ground.EnableKeyword("_NORMALMAP");
            ground.color = new Color(.55f, .61f, .65f);
            steel.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TemplateArt.Root + "Environment/Tileables/Tex_WallTileable1x2_BaseColor.png");
            steel.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TemplateArt.Root + "Environment/Tileables/Tex_WallTileable1x2_Normal.png"));
            steel.EnableKeyword("_NORMALMAP");
            steel.color = new Color(.62f, .72f, .79f);
            const string textPath = Root + "/Materials/World Text.mat";
            worldText = AssetDatabase.LoadAssetAtPath<Material>(textPath);
            if (worldText == null)
            {
                worldText = new Material(Shader.Find("TacticalGame/WorldText"));
                AssetDatabase.CreateAsset(worldText, textPath);
            }
            const string skyPath = Root + "/Materials/Evening Sky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null) { sky = new Material(Shader.Find("Skybox/Procedural")); AssetDatabase.CreateAsset(sky, skyPath); }
            sky.SetColor("_SkyTint", new Color(.40f, .53f, .62f));
            sky.SetColor("_GroundColor", new Color(.16f, .20f, .22f));
            sky.SetFloat("_Exposure", 1.1f);
            sky.SetFloat("_AtmosphereThickness", .85f);
            Materials["Sky"] = sky;
        }

        static Material Mat(string name, Color color, float metallic = 0f, float emission = 0f)
        {
            string path = Root + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(material, path); }
            material.color = color;
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Glossiness", .28f);
            if (emission > 0)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * emission);
            }
            EditorUtility.SetDirty(material);
            Materials[name] = material;
            return material;
        }

        static void BuildScene(bool depot)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sessionObject = new GameObject("MISSION - Rules, HUD and Audio");
            var game = sessionObject.AddComponent<GameSession>();
            sessionObject.AddComponent<GameHud>();
            var audio = sessionObject.AddComponent<GameAudio>();
            audio.templateRifle = AssetDatabase.LoadAssetAtPath<AudioClip>(TemplateArt.Root + "Audio/Robot_gunfire_01.wav");
            audio.templateFootstep = AssetDatabase.LoadAssetAtPath<AudioClip>(TemplateArt.Root + "Audio/Robot_footstep_01.wav");
            game.missionName = depot ? "RAIL DEPOT" : "SUBSTATION";
            game.nextScene = depot ? "Substation" : "RailDepot";
            game.briefing = depot
                ? "The evacuation train is waiting. Restore the rail signal and platform control, then reach the boarding gate."
                : "The district has gone dark. Restore the breaker and power relay, then leave through the service gate.";
            environment = new GameObject("ENVIRONMENT - Editable modular geometry").transform;
            game.environmentReflections = Lighting(depot);
            Shell(depot);
            if (depot) Depot(); else Substation();
            DressWithTemplate(depot);

            var gameplay = new GameObject("GAMEPLAY - Objectives, targets and supplies").transform;
            var a = Terminal(depot ? "Rail signal" : "Main breaker", "A", new Vector3(-14, 0, 11), gameplay);
            var b = Terminal(depot ? "Platform control" : "Power relay", "B", new Vector3(14, 0, 10), gameplay);
            game.objectives = new[] { a, b };
            game.exitGate = Gate(new Vector3(0, 0, 18.1f), gameplay);
            game.targets = new[]
            {
                Target("01", new Vector3(-9, 0, 0), gameplay),
                Target("02", new Vector3(9, 0, 4), gameplay),
                Target("03", new Vector3(2.7f, 0, 13), gameplay)
            };
            Supply(false, new Vector3(-11, .48f, -11), gameplay);
            Supply(false, new Vector3(12, .48f, 12.8f), gameplay);
            Supply(true, new Vector3(13, .48f, -8), gameplay);
            Hazard(new Vector3(15, 0, -13), gameplay);
            game.player = Player(new Vector3(0, .12f, -16), gameplay);
            game.overviewCamera = Overview();
            Markers();
            PrefabUtility.SaveAsPrefabAsset(game.player.gameObject, Root + "/Prefabs/Player.prefab");
            PrefabUtility.SaveAsPrefabAsset(game.targets[0].gameObject, Root + "/Prefabs/StationaryTarget.prefab");
            PrefabUtility.SaveAsPrefabAsset(game.targets[0].gameObject, Root + "/Prefabs/BasicGuard.prefab");
            PrefabUtility.SaveAsPrefabAsset(a.gameObject, Root + "/Prefabs/ObjectiveTerminal.prefab");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, depot ? DepotPath : SubstationPath);
        }

        static ReflectionProbe Lighting(bool depot)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.45f, .58f, .64f);
            RenderSettings.ambientEquatorColor = new Color(.28f, .36f, .4f);
            RenderSettings.ambientGroundColor = new Color(.19f, .22f, .23f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(.19f, .29f, .34f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = .008f;
            RenderSettings.skybox = Materials["Sky"];
            var light = new GameObject("Evening sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, .83f, .65f);
            light.intensity = 1.25f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            RenderSettings.sun = light;
            var fill = new GameObject("Cool ambient fill").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(.58f, .76f, 1f);
            fill.intensity = .35f;
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(35, 145, 0);
            var probe = new GameObject("Yard reflections").AddComponent<ReflectionProbe>();
            probe.transform.position = new Vector3(0, 3, 0);
            probe.size = new Vector3(43, 12, 41);
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.resolution = 128;
            return probe;
        }

        static void Shell(bool depot)
        {
            Box("Foundation", new Vector3(0, -.35f, 0), new Vector3(42, .7f, 40), ground);
            Box("West perimeter", new Vector3(-20.5f, 2.5f, 0), new Vector3(1, 5, 40), concrete);
            Box("East perimeter", new Vector3(20.5f, 2.5f, 0), new Vector3(1, 5, 40), concrete);
            Box("South perimeter", new Vector3(0, 2.5f, -19.5f), new Vector3(42, 5, 1), steel);
            Box("North perimeter", new Vector3(0, 2.5f, 19.5f), new Vector3(42, 5, 1), steel);
            for (int x = -18; x <= 18; x += 6)
            {
                Box("Wall rib N", new Vector3(x, 2.6f, 18.93f), new Vector3(.35f, 5.2f, .35f), dark);
                Box("Wall rib S", new Vector3(x, 2.6f, -18.93f), new Vector3(.35f, 5.2f, .35f), dark);
                for (int z = -19; z <= 19; z += 2)
                    Box("Ground expansion joint", new Vector3(x, .007f, z), new Vector3(.035f, .012f, 1.9f), dark, false);
            }
            for (int z = -16; z <= 16; z += 8)
            {
                Box("West wall rib", new Vector3(-19.93f, 2.7f, z), new Vector3(.35f, 5.4f, .35f), dark);
                Box("East wall rib", new Vector3(19.93f, 2.7f, z), new Vector3(.35f, 5.4f, .35f), dark);
            }
            for (int z = -16; z <= 15; z += 3)
            {
                Box("West route stripe", new Vector3(-10.5f, .015f, z), new Vector3(.12f, .02f, 1.5f), yellow, false);
                Box("East route stripe", new Vector3(10.5f, .015f, z), new Vector3(.12f, .02f, 1.5f), yellow, false);
            }
            Text("South briefing sign", depot ? "VEYRA / RAIL DEPOT" : "VEYRA / SUBSTATION 04", new Vector3(-8f, 3.2f, -18.93f), .28f, white.color, 180);
            Text("North service sign", "EVACUATION ROUTE", new Vector3(0, 4.3f, 18.9f), .27f, white.color);
            Text("West zone sign", "A / CONTROL", new Vector3(-19.9f, 3.1f, 7f), .22f, cyan.color, -90);
            Text("East zone sign", "B / SERVICE", new Vector3(19.9f, 3.1f, 7f), .22f, cyan.color, 90);
            for (int x = -17; x <= 17; x += 34)
                for (int z = -14; z <= 14; z += 14) Lamp(new Vector3(x, 0, z));
            // Distant silhouettes sit outside the collision boundary.
            for (int i = 0; i < 10; i++)
            {
                float h = 8 + i % 4 * 3;
                Box("Distant industrial building", new Vector3(-38 + i * 8, h / 2 - 1, 34 + i % 3 * 5), new Vector3(6, h, 7), dark);
                Box("Distant warm window", new Vector3(-38 + i * 8, h - 3, 30.4f + i % 3 * 5), new Vector3(4.5f, .5f, .04f), yellow, false);
            }
        }

        static void Substation()
        {
            // A central transformer island leaves broad routes on both sides.
            Box("Transformer plinth", new Vector3(0, .2f, 1), new Vector3(10, .4f, 12), concrete);
            for (int z = -2; z <= 4; z += 6)
            {
                Box("Transformer housing", new Vector3(0, 1.8f, z), new Vector3(6, 3.2f, 3.8f), steel);
                Box("Transformer yellow fascia", new Vector3(0, 2.7f, z - 1.94f), new Vector3(5.9f, .28f, .08f), yellow, false);
                for (int x = -2; x <= 2; x++)
                {
                    Box("Cooling fin", new Vector3(x, 1.5f, z - 2.05f), new Vector3(.13f, 2.1f, .3f), dark);
                    Cylinder("Ceramic bushing", new Vector3(x * .9f, 3.9f, z), new Vector3(.3f, .5f, .3f), white);
                }
                Text("Transformer number", z < 0 ? "TX / 041" : "TX / 042", new Vector3(0, 2.1f, z - 2.22f), .16f, white.color);
            }
            // Control room with two open entrances; the terminals remain on the ground plane.
            Box("Control room west wall", new Vector3(-18, 1.7f, 10), new Vector3(.4f, 3.4f, 11), steel);
            Box("Control room north wall", new Vector3(-14, 1.7f, 15.3f), new Vector3(8, 3.4f, .4f), steel);
            Box("Control room east stub", new Vector3(-10, 1.7f, 12.8f), new Vector3(.4f, 3.4f, 5), steel);
            Box("Control room south stub", new Vector3(-16.5f, 1.7f, 4.7f), new Vector3(3, 3.4f, .4f), steel);
            Box("Control room lintel", new Vector3(-12.5f, 3.1f, 4.7f), new Vector3(5, .6f, .4f), yellow);
            Box("Control room roof edge", new Vector3(-14, 3.6f, 10), new Vector3(8.5f, .25f, 11.5f), dark);
            Text("Control room entry sign", "CONTROL / A", new Vector3(-12.5f, 3.1f, 4.45f), .16f, dark.color);
            Box("Control desk", new Vector3(-16, .7f, 13), new Vector3(2.8f, 1.4f, 1), dark);
            Box("Control desk screen", new Vector3(-16, 1.65f, 13.05f), new Vector3(1.4f, .6f, .15f), cyan);
            // Right maintenance route, with both north and south access.
            Box("Relay shelter rear", new Vector3(15.1f, 1.4f, 14.2f), new Vector3(6.8f, 2.8f, .4f), steel);
            Box("Relay shelter side", new Vector3(18.3f, 1.4f, 10.8f), new Vector3(.4f, 2.8f, 7), steel);
            for (int z = -2; z <= 4; z += 3) Crate(new Vector3(15.5f, 0, z), new Vector3(3, 1.5f, 1.6f));
            Barricade(new Vector3(-9, 0, -6), 90);
            Barricade(new Vector3(8.5f, 0, -4), 0);
            Barricade(new Vector3(7, 0, 10), 90);
            for (int x = -4; x <= 4; x += 2) Box("Cable tray", new Vector3(x, .07f, -9), new Vector3(.15f, .12f, 5), dark);
            Text("Yard ground stencil", "04 / TRANSFORMER YARD", new Vector3(0, .035f, -9.5f), .19f, white.color, 0, true);
            Crate(new Vector3(-14, 0, -6), new Vector3(3, 2, 3));
            Crate(new Vector3(-14, 2, -6), new Vector3(2.7f, 1.5f, 2.7f));
        }

        static void Depot()
        {
            // A stationary carriage divides west/east lanes; both ends connect.
            Box("Train undercarriage", new Vector3(0, .65f, .5f), new Vector3(5.2f, .7f, 19), dark);
            Box("Train carriage", new Vector3(0, 2.45f, .5f), new Vector3(5.4f, 3.2f, 18), steel);
            Box("Train roof", new Vector3(0, 4.15f, .5f), new Vector3(5.6f, .3f, 18.4f), white);
            for (int side = -1; side <= 1; side += 2)
            {
                Box("Carriage cream stripe", new Vector3(side * 2.73f, 2.1f, .5f), new Vector3(.06f, .45f, 17.8f), white, false);
                for (int z = -6; z <= 6; z += 3)
                {
                    Box("Train window", new Vector3(side * 2.76f, 3f, z), new Vector3(.06f, .9f, 1.8f), glass, false);
                    Cylinder("Train wheel", new Vector3(side * 2.4f, .55f, z), new Vector3(.8f, .18f, .8f), dark, 90);
                }
                Box("Rail", new Vector3(side * 1.9f, .055f, .5f), new Vector3(.12f, .11f, 27), steel);
                Box("Platform edge marking", new Vector3(side * 4.2f, .02f, .5f), new Vector3(.28f, .03f, 23), yellow, false);
            }
            for (int z = -12; z <= 13; z++) Box("Track sleeper", new Vector3(0, .03f, z), new Vector3(4.5f, .05f, .2f), dark, false);
            Text("Train carriage number", "V / 07", new Vector3(0, 2.9f, -8.54f), .26f, white.color);
            Box("Train end door", new Vector3(0, 2f, -8.55f), new Vector3(1.3f, 2f, .08f), dark);
            for (int z = -7; z <= 9; z += 8)
            {
                Box("West canopy column", new Vector3(-8, 2.1f, z), new Vector3(.45f, 4.2f, .45f), concrete);
                Box("East canopy column", new Vector3(8, 2.1f, z), new Vector3(.45f, 4.2f, .45f), concrete);
            }
            Box("West canopy", new Vector3(-8.5f, 4.5f, 1), new Vector3(8.5f, .25f, 24), dark);
            Box("East canopy", new Vector3(8.5f, 4.5f, 1), new Vector3(8.5f, .25f, 24), dark);
            // Ticket hall cover; open south/east entries.
            Box("Ticket hall back", new Vector3(-15, 1.6f, 15.3f), new Vector3(7, 3.2f, .4f), steel);
            Box("Ticket hall side", new Vector3(-18.3f, 1.6f, 10.5f), new Vector3(.4f, 3.2f, 9.5f), steel);
            Box("Ticket desk", new Vector3(-15.5f, .65f, 7), new Vector3(3, 1.3f, 1.1f), concrete);
            Text("Ticket hall sign", "TICKETS / CONTROL A", new Vector3(-14.5f, 2.5f, 15.05f), .16f, cyan.color);
            for (int z = -5; z <= 5; z += 10)
            {
                Box("Bench seat", new Vector3(15.8f, .55f, z), new Vector3(2.8f, .2f, 1f), white);
                Box("Bench back", new Vector3(15.8f, .95f, z + .45f), new Vector3(2.8f, .8f, .12f), steel);
                Box("Bench leg", new Vector3(14.8f, .25f, z), new Vector3(.15f, .5f, .8f), dark);
                Box("Bench leg", new Vector3(16.8f, .25f, z), new Vector3(.15f, .5f, .8f), dark);
                Crate(new Vector3(14, 0, z + 1.6f), new Vector3(.9f, .8f, .6f));
            }
            Crate(new Vector3(-12, 0, -5), new Vector3(2.5f, 1.5f, 2));
            Barricade(new Vector3(11.8f, 0, 7), 90);
            Text("Platform ground stencil", "PLATFORM 02 / EVACUATION", new Vector3(8, .035f, -12.5f), .18f, white.color, 0, true);
        }

        static void DressWithTemplate(bool depot)
        {
            const string wall = "Environment/WallFloorKit/Pfb_wall_4x2_StripeBlue.prefab";
            for (int i = -16; i <= 16; i += 4)
            {
                TemplateArt.Fit(wall, environment, new Vector3(i, 2.4f, -18.91f), new Vector3(3.85f, 4.7f, .18f));
                TemplateArt.Fit(wall, environment, new Vector3(i, 2.4f, 18.91f), new Vector3(3.85f, 4.7f, .18f));
                TemplateArt.Fit(wall, environment, new Vector3(-19.9f, 2.4f, i), new Vector3(3.85f, 4.7f, .18f), 90);
                TemplateArt.Fit(wall, environment, new Vector3(19.9f, 2.4f, i), new Vector3(3.85f, 4.7f, .18f), 90);
            }
            var originals = new List<Transform>();
            foreach (Transform child in environment) originals.Add(child);
            foreach (var item in originals)
            {
                if (item.name == "Transformer housing")
                {
                    item.GetComponent<Renderer>().enabled = false;
                    TemplateArt.Fit("Environment/BlockKit_NoStamp/Pfb_Block_1x2x1_NoStamp.prefab", environment, item.position, item.localScale);
                }
                if (item.name.Contains("canopy column"))
                {
                    item.GetComponent<Renderer>().enabled = false;
                    TemplateArt.Fit("Environment/RoundPlatformsColumns/Pfb_roundColumn_1x2m.prefab", environment, item.position, new Vector3(.55f, 4.2f, .55f));
                }
                if (item.name == "Light fixture")
                {
                    item.GetComponent<Renderer>().enabled = false;
                    TemplateArt.Fit("Environment/ConnectorLightRailingKit/Pfb_light.prefab", environment, item.position, new Vector3(1.2f, .2f, .8f));
                }
            }
            if (!depot)
            {
                TemplateArt.Fit("Environment/WindowKit/Pfb_window_1.6x3.2.prefab", environment, new Vector3(-17.73f, 2.1f, 9), new Vector3(2.5f, 1.25f, .08f), 90);
                TemplateArt.Fit("Environment/WindowKit/Pfb_window_1.6x3.2.prefab", environment, new Vector3(-14, 2.1f, 15.02f), new Vector3(2.5f, 1.25f, .08f));
            }
        }

        static PlayerController Player(Vector3 position, Transform parent)
        {
            var go = new GameObject("PLAYER - Select to tune movement and rifle");
            go.transform.SetParent(parent);
            go.transform.position = position;
            go.layer = 2; // Ignore Raycast: the gun cannot hit its own player or model.
            var controller = go.AddComponent<CharacterController>();
            controller.height = 1.85f;
            controller.center = new Vector3(0, .94f, 0);
            controller.radius = .34f;
            controller.stepOffset = .3f;
            controller.slopeLimit = 45f;
            var hp = go.AddComponent<Health>();
            var player = go.AddComponent<PlayerController>();
            var rifle = go.AddComponent<Rifle>();
            player.health = hp;
            player.rifle = rifle;
            var cameraObject = new GameObject("First Person Camera");
            cameraObject.transform.SetParent(go.transform, false);
            cameraObject.transform.localPosition = new Vector3(0, 1.65f, 0);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 73f;
            camera.nearClipPlane = .04f;
            camera.farClipPlane = 150f;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = new Color(.19f, .29f, .34f);
            cameraObject.AddComponent<AudioListener>();
            player.viewCamera = camera;
            rifle.viewCamera = camera;
            var weapon = new GameObject("R-24 view model").transform;
            weapon.SetParent(cameraObject.transform, false);
            weapon.localPosition = new Vector3(.23f, -.21f, .46f);
            rifle.weaponModel = weapon;
            TemplateArt.Fit("Guns/AssaultRifle/Pfb_assaultRifle.prefab", weapon, new Vector3(0, 0, .10f), new Vector3(.72f, .24f, .08f), -90);
            LocalBox("Glove right", weapon, new Vector3(.018f, -.14f, -.13f), new Vector3(.11f, .10f, .15f), dark);
            LocalBox("Glove left", weapon, new Vector3(-.07f, -.075f, .17f), new Vector3(.105f, .08f, .17f), dark);
            var flashObject = new GameObject("Muzzle flash");
            flashObject.transform.SetParent(weapon, false);
            flashObject.transform.localPosition = new Vector3(0, .035f, .45f);
            var flash = flashObject.AddComponent<Light>();
            flash.type = LightType.Point;
            flash.color = new Color(1f, .65f, .18f);
            flash.intensity = 3;
            flash.range = 4;
            flash.enabled = false;
            rifle.muzzleFlash = flash;
            var tracer = flashObject.AddComponent<LineRenderer>();
            tracer.sharedMaterial = yellow;
            tracer.positionCount = 2;
            tracer.startWidth = .012f;
            tracer.endWidth = .006f;
            tracer.shadowCastingMode = ShadowCastingMode.Off;
            tracer.enabled = false;
            rifle.tracer = tracer;
            foreach (var item in go.GetComponentsInChildren<Transform>()) item.gameObject.layer = 2;
            return player;
        }

        static ObjectiveTerminal Terminal(string name, string letter, Vector3 position, Transform parent)
        {
            var root = new GameObject("TERMINAL " + letter + " - " + name);
            root.transform.SetParent(parent);
            root.transform.position = position;
            var terminal = root.AddComponent<ObjectiveTerminal>();
            terminal.displayName = name;
            terminal.letter = letter;
            LocalBox("Pedestal", root.transform, new Vector3(0, .55f, 0), new Vector3(.9f, 1.1f, .7f), dark);
            LocalBox("Console body", root.transform, new Vector3(0, 1.4f, 0), new Vector3(1.25f, .85f, .7f), steel);
            var screen = LocalBox("Interactive screen", root.transform, new Vector3(0, 1.46f, -.365f), new Vector3(.95f, .52f, .035f), cyan);
            terminal.screen = screen.GetComponent<Renderer>();
            LocalBox("Keyboard", root.transform, new Vector3(0, 1.0f, -.5f), new Vector3(1, .08f, .5f), dark);
            LocalBox("Yellow console stripe", root.transform, new Vector3(0, 1.91f, 0), new Vector3(1.25f, .1f, .72f), yellow);
            var text = Text("Status", letter + " / OFFLINE", position + new Vector3(0, 1.47f, -.395f), .045f, dark.color);
            text.transform.SetParent(root.transform, true);
            terminal.statusText = text;
            terminal.onlineMaterial = green;
            var light = new GameObject("Status light").AddComponent<Light>();
            light.transform.SetParent(root.transform, false);
            light.transform.localPosition = new Vector3(0, 1.8f, -1f);
            light.type = LightType.Point;
            light.color = cyan.color;
            light.range = 4;
            light.intensity = 1.5f;
            terminal.statusLight = light;
            return terminal;
        }

        static ExitGate Gate(Vector3 position, Transform parent)
        {
            var root = new GameObject("EXIT - Restore both terminals to unlock");
            root.transform.SetParent(parent);
            root.transform.position = position;
            var gate = root.AddComponent<ExitGate>();
            gate.displayName = "Service exit";
            LocalBox("Gate door", root.transform, new Vector3(0, 1.5f, 0), new Vector3(3.8f, 3f, .4f), dark);
            LocalBox("Frame left", root.transform, new Vector3(-2.1f, 1.7f, 0), new Vector3(.35f, 3.4f, .7f), concrete);
            LocalBox("Frame right", root.transform, new Vector3(2.1f, 1.7f, 0), new Vector3(.35f, 3.4f, .7f), concrete);
            gate.indicator = LocalBox("Lock indicator", root.transform, new Vector3(0, 3.2f, -.1f), new Vector3(4.4f, .3f, .6f), red).GetComponent<Renderer>();
            for (int x = -1; x <= 1; x++) LocalBox("Door seam", root.transform, new Vector3(x, 1.5f, -.22f), new Vector3(.04f, 2.9f, .03f), steel);
            var text = Text("Exit sign", "EXIT / LOCKED", position + new Vector3(0, 2.4f, -.25f), .10f, orange.color);
            text.transform.SetParent(root.transform, true);
            gate.sign = text;
            gate.unlockedMaterial = green;
            Box("Extraction threshold", position + new Vector3(0, .02f, -1.4f), new Vector3(4.5f, .03f, 1.4f), yellow, false);
            return gate;
        }

        static TargetDummy Target(string id, Vector3 position, Transform parent)
        {
            var root = new GameObject("GUARD " + id + " - Basic fixed attack");
            root.transform.SetParent(parent);
            root.transform.position = position;
            var health = root.AddComponent<Health>();
            var target = root.AddComponent<TargetDummy>();
            target.targetId = id;
            target.health = health;
            target.hitMaterial = yellow;
            var body = new GameObject("Target body").transform;
            body.SetParent(root.transform, false);
            target.body = body;
            var model = TemplateArt.Character(body);
            target.chest = model.GetComponentInChildren<SkinnedMeshRenderer>();
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.height = 1.8f; capsule.radius = .32f; capsule.center = Vector3.up * .91f;
            TemplateArt.Fit("Guns/AssaultRifle/Pfb_assaultRifle.prefab", body, new Vector3(.22f, 1.23f, .35f), new Vector3(.78f, .24f, .08f), -90);
            var label = Text("Target ID", id + " / STATIONARY", position + new Vector3(0, 2.65f, 0), .1f, orange.color);
            label.transform.SetParent(root.transform, true);
            target.label = label;
            label.GetComponent<Renderer>().enabled = false;
            var guard = root.AddComponent<BasicGuard>();
            var lamp = new GameObject("Weapon warning").AddComponent<Light>();
            lamp.transform.SetParent(body, false);
            lamp.transform.localPosition = new Vector3(.22f, 1.3f, .75f);
            lamp.type = LightType.Point; lamp.color = new Color(1f, .27f, .08f); lamp.range = 2.5f; lamp.enabled = false;
            guard.warningLight = lamp;
            var tracer = root.AddComponent<LineRenderer>();
            tracer.sharedMaterial = red; tracer.positionCount = 2; tracer.startWidth = .018f; tracer.endWidth = .008f;
            tracer.enabled = false; guard.tracer = tracer;
            return target;
        }

        static void Supply(bool medical, Vector3 position, Transform parent)
        {
            var root = new GameObject(medical ? "SUPPLY - Medical kit" : "SUPPLY - Ammunition");
            root.transform.SetParent(parent);
            root.transform.position = position;
            var supply = root.AddComponent<SupplyCrate>();
            supply.medical = medical;
            supply.amount = medical ? 50 : 48;
            supply.displayName = medical ? "Medical kit" : "Ammunition";
            LocalBox("Crate", root.transform, Vector3.zero, new Vector3(1.2f, .8f, .8f), medical ? white : yellow);
            LocalBox("Lid", root.transform, new Vector3(0, .43f, 0), new Vector3(1.25f, .12f, .85f), dark);
            LocalBox("Stripe horizontal", root.transform, new Vector3(0, .03f, -.41f), new Vector3(.48f, .12f, .02f), medical ? red : dark);
            if (medical) LocalBox("Stripe vertical", root.transform, new Vector3(0, .03f, -.43f), new Vector3(.12f, .45f, .02f), red);
            var label = Text("Supply sign", medical ? "+ MEDICAL" : "AMMO / 48", position + new Vector3(0, .95f, 0), .1f, medical ? green.color : yellow.color);
            label.transform.SetParent(root.transform, true);
            PrefabUtility.SaveAsPrefabAsset(root, Root + "/Prefabs/" + (medical ? "MedicalKit" : "AmmoCrate") + ".prefab");
        }

        static void Hazard(Vector3 position, Transform parent)
        {
            var root = new GameObject("HAZARD - Optional health test, red floor zone");
            root.transform.SetParent(parent);
            root.transform.position = position;
            var zone = root.AddComponent<DamageZone>();
            zone.size = new Vector3(3, 3, 3);
            var plate = LocalBox("Live test plate", root.transform, new Vector3(0, .025f, 0), new Vector3(3, .04f, 3), red);
            for (int i = -1; i <= 1; i++) LocalBox("Warning stripe", root.transform, new Vector3(i * .8f, .055f, 0), new Vector3(.22f, .02f, 2.9f), yellow);
            var label = Text("Hazard label", "DANGER / LIVE TEST", position + new Vector3(0, .07f, 1.9f), .14f, yellow.color, 0, true);
            label.transform.SetParent(root.transform, true);
        }

        static Camera Overview()
        {
            var camera = new GameObject("Tactical Overview Camera - Tab in game").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 45, -2);
            camera.transform.rotation = Quaternion.Euler(90, 0, 0);
            camera.orthographic = true;
            camera.orthographicSize = 23;
            camera.farClipPlane = 100;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.05f, .08f, .1f);
            camera.enabled = false;
            camera.cullingMask = ~(1 << 2);
            return camera;
        }

        static void Markers()
        {
            var root = new GameObject("AI LESSON MARKERS - Data only, no algorithms").transform;
            var positions = new[] { new Vector3(-7, 0, -5), new Vector3(7, 0, -5), new Vector3(-8, 0, 7), new Vector3(9, 0, 9), new Vector3(-16, 0, 1), new Vector3(16, 0, 6), new Vector3(0, 0, -12) };
            for (int i = 0; i < positions.Length; i++)
            {
                MarkerKind kind = i < 4 ? MarkerKind.Cover : i < 6 ? MarkerKind.Flank : MarkerKind.Fallback;
                var go = new GameObject(kind + " marker " + (i + 1));
                go.transform.SetParent(root);
                go.transform.position = positions[i];
                var marker = go.AddComponent<TacticalMarker>();
                marker.kind = kind;
                marker.id = i + 1;
            }
        }

        static void Crate(Vector3 position, Vector3 size)
        {
            var collider = Box("Cargo collision", position + Vector3.up * size.y / 2, size, steel);
            collider.GetComponent<Renderer>().enabled = false;
            TemplateArt.Fit("Environment/BlockKit_NoStamp/Pfb_Block_1x1x1_NoStamp.prefab", environment, position + Vector3.up * size.y / 2, size);
        }

        static void Barricade(Vector3 position, float yaw)
        {
            var root = new GameObject("Concrete cover barrier").transform;
            root.SetParent(environment);
            root.position = position;
            root.rotation = Quaternion.Euler(0, yaw, 0);
            var collider = LocalBox("Concrete barrier", root, new Vector3(0, .7f, 0), new Vector3(3.6f, 1.4f, .7f), concrete);
            collider.GetComponent<Renderer>().enabled = false;
            TemplateArt.Fit("Environment/WallFloorKit/Pfb_wall_4x2_StripeBlue.prefab", root, new Vector3(0, .7f, 0), new Vector3(3.6f, 1.4f, .7f));
        }

        static void Lamp(Vector3 position)
        {
            Box("Light pole", position + Vector3.up * 2.5f, new Vector3(.16f, 5, .16f), dark);
            Box("Light fixture", position + new Vector3(0, 5, -.4f), new Vector3(1.2f, .15f, .8f), yellow);
            var light = new GameObject("Warm yard light").AddComponent<Light>();
            light.transform.SetParent(environment);
            light.transform.position = position + new Vector3(0, 4.7f, -.4f);
            light.type = LightType.Point;
            light.color = new Color(1, .74f, .37f);
            light.range = 10;
            light.intensity = 1.1f;
        }

        static GameObject Box(string name, Vector3 position, Vector3 size, Material material, bool collision = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(environment, false);
            go.transform.position = position;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.isStatic = true;
            return go;
        }

        static GameObject LocalBox(string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            var go = Box(name, Vector3.zero, size, material);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.isStatic = false;
            return go;
        }

        static void Cylinder(string name, Vector3 position, Vector3 scale, Material material, float angle = 0)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(environment);
            go.transform.position = position;
            go.transform.localScale = scale;
            go.transform.rotation = Quaternion.Euler(0, 0, angle);
            go.GetComponent<Renderer>().sharedMaterial = material;
            go.isStatic = true;
        }

        static TextMesh Text(string name, string content, Vector3 position, float characterSize, Color color, float yaw = 0, bool floor = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(environment);
            go.transform.position = position;
            go.transform.rotation = floor ? Quaternion.Euler(90, 0, 0) : Quaternion.Euler(0, yaw, 0);
            var text = go.AddComponent<TextMesh>();
            #if UNITY_2022_2_OR_NEWER
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            #else
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            #endif
            worldText.mainTexture = text.font.material.mainTexture;
            go.GetComponent<Renderer>().sharedMaterial = worldText;
            text.text = content;
            text.fontSize = 64;
            text.characterSize = characterSize * .55f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = color;
            if (name != "Status" && name != "Exit sign") go.GetComponent<Renderer>().enabled = false;
            go.AddComponent<WorldLabel>();
            return text;
        }
    }
}
