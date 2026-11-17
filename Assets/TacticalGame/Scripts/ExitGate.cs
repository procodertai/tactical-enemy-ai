using UnityEngine;

namespace TacticalGame
{
    public sealed class ExitGate : WorldInteractable
    {
        public Renderer indicator;
        public Material unlockedMaterial;
        public TextMesh sign;
        bool unlocked;
        public override string Prompt => GameSession.Instance.ExitUnlocked ? "[E] Extract / complete mission" : "EXIT LOCKED / Restore terminals A and B";

        void Update()
        {
            if (!unlocked && GameSession.Instance != null && GameSession.Instance.ExitUnlocked)
            {
                unlocked = true;
                indicator.sharedMaterial = unlockedMaterial;
                sign.text = "EXIT / READY";
                sign.color = new Color(.3f, 1f, .6f);
            }
        }

        public override void Interact(PlayerController player)
        {
            if (GameSession.Instance.ExitUnlocked) GameSession.Instance.Escape();
            else GameSession.Instance.Notify("The exit needs power. Restore both cyan terminals first.");
        }
    }
}
