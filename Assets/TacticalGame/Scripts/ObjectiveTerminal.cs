using UnityEngine;

namespace TacticalGame
{
    public sealed class ObjectiveTerminal : WorldInteractable
    {
        public string letter = "A";
        public Renderer screen;
        public Light statusLight;
        public TextMesh statusText;
        public Material onlineMaterial;
        public bool Completed { get; private set; }
        public override string Prompt => Completed ? displayName + " / ONLINE" : "[E] Restore " + displayName;

        public override void Interact(PlayerController player)
        {
            if (Completed || !GameSession.Instance.CanControl) return;
            Completed = true;
            screen.sharedMaterial = onlineMaterial;
            statusLight.color = new Color(.24f, 1f, .55f);
            statusText.text = letter + " / ONLINE";
            statusText.color = statusLight.color;
            GameSession.Instance.CompleteObjective(this);
        }
    }
}
