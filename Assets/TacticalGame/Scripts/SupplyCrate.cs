using UnityEngine;

namespace TacticalGame
{
    public sealed class SupplyCrate : WorldInteractable
    {
        public bool medical;
        public int amount = 48;
        public bool Collected { get; private set; }
        public override string Prompt => "[E] " + (medical ? "Medical supplies / +50 health" : "Ammunition / +48 reserve");

        public override void Interact(PlayerController player)
        {
            if (Collected || !GameSession.Instance.CanControl) return;
            if (medical && player.health.Current >= player.health.maximum)
            {
                GameSession.Instance.Notify("Health is already full. Save this medical kit for later.");
                return;
            }
            Collected = true;
            if (medical) player.health.Heal(amount);
            else player.rifle.AddAmmo(amount);
            GameSession.Instance.Notify(medical ? "Medical supplies collected." : "+48 rounds added to your reserve. Press R to reload.");
            GameAudio.Instance.PlayCue(740f, .12f);
            gameObject.SetActive(false);
        }
    }
}
