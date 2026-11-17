using UnityEngine;

namespace TacticalGame
{
    // Default TextMesh fonts draw through walls. Use a depth-tested font material instead.
    [ExecuteAlways, RequireComponent(typeof(TextMesh))]
    public sealed class WorldLabel : MonoBehaviour
    {
        TextMesh label;

        void OnEnable()
        {
            label = GetComponent<TextMesh>();
            #if UNITY_2022_2_OR_NEWER
            // Also migrates labels authored by an earlier editor's built-in Arial font.
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            #endif
            Font.textureRebuilt += UpdateAtlas;
            Refresh();
        }

        void OnDisable() => Font.textureRebuilt -= UpdateAtlas;

        void UpdateAtlas(Font font)
        {
            if (label != null && label.font == font) Refresh();
        }

        void Refresh()
        {
            if (label == null || label.font == null) return;
            var material = GetComponent<Renderer>().sharedMaterial;
            if (material != null && material.shader.name == "TacticalGame/WorldText")
                material.mainTexture = label.font.material.mainTexture;
        }
    }
}
