using UnityEngine;

namespace TacticalGame
{
    [RequireComponent(typeof(CharacterController), typeof(Health))]
    public sealed class PlayerController : MonoBehaviour
    {
        [Header("Move and look")]
        public float walkSpeed = 5f;
        public float sprintSpeed = 8f;
        public float jumpHeight = 1.1f;
        public float gravity = -24f;
        [Range(.3f, 5f)] public float sensitivity = 2f;
        public Camera viewCamera;
        public Health health;
        public Rifle rifle;
        [Header("Interaction")]
        public float interactionDistance = 3.1f;
        public WorldInteractable Focus { get; private set; }
        public bool IsMoving { get; private set; }

        CharacterController controller;
        float pitch;
        float verticalSpeed;
        float stepClock;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            sensitivity = PlayerPrefs.GetFloat("LookSensitivity", sensitivity);
        }

        void Update()
        {
            var game = GameSession.Instance;
            Focus = null;
            IsMoving = false;
            if (game == null || !game.CanControl) return;

            float mouseX = Input.GetAxisRaw("Mouse X") * sensitivity;
            float mouseY = Input.GetAxisRaw("Mouse Y") * sensitivity;
            transform.Rotate(0f, mouseX, 0f);
            pitch = Mathf.Clamp(pitch - mouseY, -82f, 82f);
            viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            float x = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)
                    - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
            float z = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f)
                    - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
            var movement = Vector3.ClampMagnitude(transform.right * x + transform.forward * z, 1f);
            bool sprint = Input.GetKey(KeyCode.LeftShift) && !rifle.IsAiming;
            float speed = sprint ? sprintSpeed : walkSpeed;
            if (rifle.IsAiming) speed *= .65f;
            if (controller.isGrounded)
            {
                verticalSpeed = -2f;
                if (Input.GetKeyDown(KeyCode.Space)) verticalSpeed = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
            verticalSpeed += gravity * Time.deltaTime;
            controller.Move((movement * speed + Vector3.up * verticalSpeed) * Time.deltaTime);
            IsMoving = movement.sqrMagnitude > .01f && controller.isGrounded;
            if (IsMoving)
            {
                stepClock -= Time.deltaTime;
                if (stepClock <= 0f) { GameAudio.Instance.Footstep(); stepClock = sprint ? .28f : .43f; }
            }

            if (Physics.Raycast(viewCamera.transform.position, viewCamera.transform.forward, out var hit,
                interactionDistance, ~(1 << 2), QueryTriggerInteraction.Ignore))
                Focus = hit.collider.GetComponentInParent<WorldInteractable>();
            if (Focus != null && Input.GetKeyDown(KeyCode.E)) Focus.Interact(this);
            if (transform.position.y < -8f) health.TakeDamage(health.maximum);
        }

        public void SetSensitivity(float value)
        {
            sensitivity = value;
            PlayerPrefs.SetFloat("LookSensitivity", value);
        }
    }
}
