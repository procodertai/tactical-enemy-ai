using UnityEngine;

namespace TacticalGame
{
    public abstract class WorldInteractable : MonoBehaviour
    {
        public string displayName;
        public abstract string Prompt { get; }
        public abstract void Interact(PlayerController player);
    }
}
