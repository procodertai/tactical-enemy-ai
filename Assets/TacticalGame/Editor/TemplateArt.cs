using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace TacticalGame.Editor
{
    // Visual-only extraction of Unity's Multiplayer FPS template. No template networking scripts.
    public static class TemplateArt
    {
        public const string Root = "Assets/ThirdParty/UnityMultiplayerFPS/";

        public static void PrepareMaterials()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { Root.TrimEnd('/') }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                Texture albedo = SavedTexture(material, "_MainTex") ?? SavedTexture(material, "_BaseMap") ?? SavedTexture(material, "_BaseColor");
                Texture normal = SavedTexture(material, "_BumpMap") ?? SavedTexture(material, "_Normal");
                material.shader = Shader.Find("Standard");
                material.mainTexture = albedo;
                material.SetTexture("_BumpMap", normal);
                if (normal != null) material.EnableKeyword("_NORMALMAP");
                material.SetFloat("_Glossiness", .45f);
                material.SetFloat("_Metallic", .3f);
                EditorUtility.SetDirty(material);
            }
            AssetDatabase.SaveAssets();
        }

        static Texture SavedTexture(Material material, string key)
        {
            var properties = new SerializedObject(material).FindProperty("m_SavedProperties.m_TexEnvs");
            for (int i = 0; i < properties.arraySize; i++)
            {
                var entry = properties.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("first").stringValue == key)
                    return entry.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;
            }
            return null;
        }

        public static GameObject Character(Transform parent)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Characters/Armature/Model/Armature_arms.fbx");
            var model = Object.Instantiate(asset, parent);
            model.name = "Template Armature / full body";
            model.transform.localPosition = Vector3.zero;
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (renderer.name.Contains("ArmsOnly")) { renderer.gameObject.SetActive(false); continue; }
                renderer.sharedMaterials = new[] { Material("Materials/M_Armature_Body_orange.mat"), Material("Materials/M_Armature_Arms_orange.mat"), Material("Materials/M_Armature_Legs_orange.mat") };
            }
            string controllerPath = "Assets/TacticalGame/Materials/GuardIdle.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                var clip = AssetDatabase.LoadAllAssetsAtPath(Root + "Characters/Armature/Animations/Armature_Shoot_AssaultRifle_F.anim.fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
                controller.AddMotion(clip);
            }
            var animator = model.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            return model;
        }

        public static Material Material(string path) => AssetDatabase.LoadAssetAtPath<Material>(Root + path);

        public static void Inspect()
        {
            var report = new StringBuilder();
            foreach (string relative in new[] { "Characters/Armature/Model/Armature_arms.fbx", "Guns/AssaultRifle/Geo_assaultRifle.fbx", "Environment/BlockKit_NoStamp/Pfb_Block_1x1x1_NoStamp.prefab", "Environment/WallFloorKit/Pfb_wall_4x2_StripeBlue.prefab" })
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Root + relative);
                if (asset == null) throw new System.Exception("Template model missing: " + relative);
                var instance = Object.Instantiate(asset);
                report.AppendLine(relative);
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                    report.AppendLine("  " + renderer.name + " bounds=" + renderer.bounds + " mats=" + string.Join(",", renderer.sharedMaterials.Select(m => m == null ? "NULL" : m.name)));
                foreach (var animator in instance.GetComponentsInChildren<Animator>())
                    report.AppendLine("  Animator " + animator.name + " avatar=" + animator.avatar);
                Object.DestroyImmediate(instance);
            }
            Directory.CreateDirectory("Artifacts");
            File.WriteAllText("Artifacts/template-inspection.txt", report.ToString());
            Debug.Log(report.ToString());
        }

        public static GameObject Fit(string relative, Transform parent, Vector3 centre, Vector3 size, float yaw = 0)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Root + relative);
            if (asset == null) throw new System.Exception("Required template art is missing: " + relative);
            var go = Object.Instantiate(asset, parent);
            go.name = "Template / " + asset.name;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            foreach (var collider in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
            foreach (var renderer in go.GetComponentsInChildren<Renderer>())
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    if (materials[i] == null || relative.Contains("Pfb_light"))
                        materials[i] = AssetDatabase.LoadAssetAtPath<Material>("Assets/TacticalGame/Materials/" + (relative.Contains("Pfb_light") ? "Terminal Cyan.mat" : "Blue Steel.mat"));
                renderer.sharedMaterials = materials;
            }
            var bounds = LocalBounds(go, parent);
            go.transform.localScale = new Vector3(size.x / bounds.size.x, size.y / bounds.size.y, size.z / bounds.size.z);
            bounds = LocalBounds(go, parent);
            go.transform.localPosition += centre - bounds.center;
            if (yaw != 0) go.transform.RotateAround(parent.TransformPoint(centre), Vector3.up, yaw);
            return go;
        }

        static Bounds LocalBounds(GameObject go, Transform parent)
        {
            bool first = true;
            var result = new Bounds();
            foreach (var renderer in go.GetComponentsInChildren<Renderer>().Where(r => r.enabled))
            {
                Bounds bounds = renderer.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 point = parent.InverseTransformPoint(bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    if (first) { result = new Bounds(point, Vector3.zero); first = false; }
                    else result.Encapsulate(point);
                }
            }
            return result;
        }

        public static Bounds BoundsOf(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
            if (renderers.Length == 0) throw new System.Exception("Template model has no renderers: " + go.name);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
    }
}
