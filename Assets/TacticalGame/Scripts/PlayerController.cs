using UnityEngine;

namespace TacticalGame
{
    [DefaultExecutionOrder(-5)]
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
        public GameObject godViewAvatarPrefab;
        [Header("Interaction")]
        public float interactionDistance = 3.1f;
        public WorldInteractable Focus { get; private set; }
        public bool IsMoving { get; private set; }
        public Vector3 AimDirection => GameSession.Instance != null && !GameSession.Instance.FirstPerson
            ? externalAimDirection : viewCamera.transform.forward;

        const int WorldRaycastMask = ~(1 << 2);
        CharacterController controller;
        GameObject godViewAvatar;
        Vector3 externalAimDirection;
        float pitch;
        float verticalSpeed;
        float stepClock;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            externalAimDirection = transform.forward;
            sensitivity = PlayerPrefs.GetFloat("LookSensitivity", sensitivity);
        }

        void Update()
        {
            var game = GameSession.Instance;
            Focus = null;
            IsMoving = false;
            if (game == null || !game.CanControl) return;

            if (game.FirstPerson)
            {
                float mouseX = Input.GetAxisRaw("Mouse X") * sensitivity;
                float mouseY = Input.GetAxisRaw("Mouse Y") * sensitivity;
                transform.Rotate(0f, mouseX, 0f);
                pitch = Mathf.Clamp(pitch - mouseY, -82f, 82f);
                viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }

            float x = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)
                    - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
            float z = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f)
                    - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
            ApplyControls(new Vector2(x, z), Input.mousePosition, Input.GetKey(KeyCode.LeftShift),
                Input.GetKeyDown(KeyCode.Space), Input.GetKeyDown(KeyCode.E), Time.deltaTime);
        }

        public void ApplyControls(Vector2 move, Vector2 pointer, bool sprint, bool jump, bool interact, float deltaTime)
        {
            var game = GameSession.Instance;
            Focus = null;
            IsMoving = false;
            if (game == null || !game.CanControl) return;

            var axes = game.FirstPerson ? transform : game.ActiveCamera.transform;
            Vector3 forward = game.FirstPerson ? axes.forward : Vector3.ProjectOnPlane(axes.up, Vector3.up).normalized;
            Vector3 right = game.FirstPerson ? axes.right : Vector3.ProjectOnPlane(axes.right, Vector3.up).normalized;
            var movement = Vector3.ClampMagnitude(right * move.x + forward * move.y, 1f);
            bool precisionAim = rifle.IsAiming && game.FirstPerson;
            sprint &= !precisionAim;
            float speed = sprint ? sprintSpeed : walkSpeed;
            if (precisionAim) speed *= .65f;
            if (controller.isGrounded)
            {
                verticalSpeed = -2f;
                if (jump) verticalSpeed = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
            verticalSpeed += gravity * deltaTime;
            controller.Move((movement * speed + Vector3.up * verticalSpeed) * deltaTime);
            IsMoving = movement.sqrMagnitude > .01f && controller.isGrounded;
            if (IsMoving)
            {
                stepClock -= deltaTime;
                if (stepClock <= 0f) { GameAudio.Instance.Footstep(); stepClock = sprint ? .28f : .43f; }
            }

            if (game.FirstPerson) Focus = FindInteractable(viewCamera.transform.forward);
            else AimFromExternalView(game, pointer);
            if (Focus != null && interact) Focus.Interact(this);
            if (transform.position.y < -8f) health.TakeDamage(health.maximum);
        }

        public void ShowGodViewAvatar(bool shown)
        {
            if (shown && godViewAvatar == null && godViewAvatarPrefab != null)
                godViewAvatar = Instantiate(godViewAvatarPrefab, transform, false);
            if (godViewAvatar != null && godViewAvatar.activeSelf != shown)
                godViewAvatar.SetActive(shown);
        }

        void AimFromExternalView(GameSession game, Vector2 pointer)
        {
            Camera camera = game.ActiveCamera;
            Ray ray = camera.ScreenPointToRay(pointer);
            Vector3 eye = viewCamera.transform.position;
            var aimPlane = new Plane(Vector3.up, eye);
            Vector3 aimPoint = aimPlane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : eye + transform.forward;
            bool picked = TryPickFromExternalView(game, ray, camera.farClipPlane, out var hit);
            var target = picked ? hit.collider.GetComponentInParent<TargetDummy>() : null;
            if (target != null && target.IsAlive) aimPoint = hit.collider.bounds.center;
            else if (picked && game.GodView) aimPoint = hit.point;

            Vector3 flat = Vector3.ProjectOnPlane(aimPoint - eye, Vector3.up);
            if (flat.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(flat);
            Vector3 aim = aimPoint - viewCamera.transform.position;
            externalAimDirection = aim.sqrMagnitude > .01f ? aim.normalized : transform.forward;
            if (!picked) return;

            var item = hit.collider.GetComponentInParent<WorldInteractable>();
            if (item != null && FindInteractable(hit.collider.bounds.center - viewCamera.transform.position) == item)
                Focus = item;
        }

        static bool TryPickFromExternalView(GameSession game, Ray ray, float range, out RaycastHit hit)
        {
            if (!Physics.Raycast(ray, out hit, range, WorldRaycastMask, QueryTriggerInteraction.Ignore)) return false;
            if (!game.IsViewCeiling(hit.collider)) return true;

            // Only the cursor ignores the hidden ceiling. Shots and interactions still start at the player.
            float closest = float.PositiveInfinity;
            foreach (var candidate in Physics.RaycastAll(ray, range, WorldRaycastMask, QueryTriggerInteraction.Ignore))
            {
                if (candidate.distance >= closest || game.IsViewCeiling(candidate.collider)) continue;
                closest = candidate.distance;
                hit = candidate;
            }
            return !float.IsPositiveInfinity(closest);
        }

        WorldInteractable FindInteractable(Vector3 direction)
        {
            return Physics.Raycast(viewCamera.transform.position, direction.normalized, out var hit,
                interactionDistance, WorldRaycastMask, QueryTriggerInteraction.Ignore)
                ? hit.collider.GetComponentInParent<WorldInteractable>() : null;
        }

        public void SetSensitivity(float value)
        {
            sensitivity = value;
            PlayerPrefs.SetFloat("LookSensitivity", value);
        }
    }
}
