using UnityEngine;

namespace TacticalGame
{
    public enum ScoutState { Patrol, Chase, Search }

    // Supplies patrol and search to the squad controller, and can drive a standalone guard.
    [RequireComponent(typeof(PathMover))]
    public sealed class GuardScout : MonoBehaviour
    {
        [Header("Vision")]
        public float viewRange = 16f;
        [Range(20f, 180f)] public float viewAngle = 110f;   // full cone angle
        public float spotSeconds = 0.35f;                    // must see the player this long to react
        public float loseSeconds = 0.6f;                     // out of sight this long = lost

        [Header("Speed")]
        public float patrolSpeed = 2.2f;
        public float chaseSpeed = 4.2f;

        [Header("Chase")]
        public float attackDistance = 12f;
        public float minDistance = 3f;
        public float repathSeconds = 0.5f;

        [Header("Patrol and search")]
        public float patrolMin = 10f;
        public float patrolMax = 24f;
        public float waitMin = 1f;
        public float waitMax = 3f;
        public float scanSpeed = 70f;       // degrees per second while looking around
        public float searchSeconds = 8f;
        public float searchRadius = 6f;

        public ScoutState state = ScoutState.Patrol;   // visible in the Inspector while playing

        PathMover mover;
        Health health;
        BasicGuard basic;
        Vector3 lastSeen, lastDir, prevPlayer;
        bool hasPrev, waiting;
        float aware, lostTimer, waitUntil, nextRepath, searchEnds;
        bool utilityControlled, utilityScouting, hasSearchCenter, searchedCenter;
        Vector3 searchCenter, lastSearchCenter;
        float utilityMovementSpeed;

        public bool IsUtilityScouting => utilityScouting;
        void Awake()
        {
            mover = GetComponent<PathMover>();
            health = GetComponent<Health>();
            basic = GetComponent<BasicGuard>();
            lastDir = transform.forward;
        }

        void Start()
        {
            if (!utilityControlled) EnterPatrol();
        }

        internal void UseUtilityControl() => utilityControlled = true;

        internal void RememberForUtility(Vector3 position)
        {
            if (!hasSearchCenter || (position - lastSearchCenter).sqrMagnitude >= 1f) searchedCenter = false;
            searchCenter = position;
            hasSearchCenter = true;
        }

        internal void TickUtilityScouting()
        {
            var pathfinder = AStarPathfinder.Instance;
            if (!isActiveAndEnabled || pathfinder == null || pathfinder.grid == null) return;
            if (!utilityScouting)
            {
                utilityScouting = true;
                utilityMovementSpeed = mover.speed;
                EnterPatrol();
            }
            if (hasSearchCenter && !searchedCenter)
            {
                mover.Stop();
                lastSeen = lastSearchCenter = searchCenter;
                searchedCenter = true;
                state = ScoutState.Search;
                searchEnds = Time.time + searchSeconds;
                waiting = false;
                mover.speed = patrolSpeed;
            }

            if (state == ScoutState.Search) DoSearch(pathfinder);
            else DoPatrol(pathfinder);
        }

        internal void StopUtilityScouting()
        {
            if (!utilityScouting) return;
            utilityScouting = false;
            mover.Stop();
            mover.speed = utilityMovementSpeed;
            if (hasSearchCenter) state = ScoutState.Chase;
        }

        void Update()
        {
            if (utilityControlled) return;
            var game = GameSession.Instance;
            var pos = Positioning.Instance;
            var pf = AStarPathfinder.Instance;
            if (game == null || pos == null || pf == null || game.State != RoundState.Playing) return;
            if (health != null && !health.IsAlive) { SetFiring(false); return; }

            bool sees = CanSee(game, pos);
            if (sees)
            {
                Vector3 playerPos = game.player.transform.position;
                lastSeen = playerPos;
                lostTimer = 0f;
                Vector3 moved = playerPos - prevPlayer;
                moved.y = 0f;
                if (hasPrev && moved.sqrMagnitude > 0.0004f) lastDir = moved.normalized;
                prevPlayer = playerPos;
            }
            hasPrev = sees;

            if (state != ScoutState.Chase)
            {
                aware = sees ? aware + Time.deltaTime : Mathf.Max(0f, aware - Time.deltaTime);
                if (aware >= spotSeconds) EnterChase();
            }

            switch (state)
            {
                case ScoutState.Patrol: DoPatrol(pf); break;
                case ScoutState.Chase:  DoChase(sees); break;
                case ScoutState.Search: DoSearch(pf); break;
            }
        }

        // Cone check applies only while not chasing; a guard already on your trail keeps tracking.
        bool CanSee(GameSession game, Positioning pos)
        {
            Vector3 eye = transform.position + Vector3.up * pos.eyeHeight;
            Vector3 target = game.player.viewCamera.transform.position;
            Vector3 flat = target - eye;
            flat.y = 0f;
            if (flat.magnitude > viewRange) return false;
            if (state != ScoutState.Chase && Vector3.Angle(transform.forward, flat) > viewAngle * 0.5f) return false;
            return pos.HasLineOfSight(eye, target);
        }

        void DoPatrol(AStarPathfinder pf)
        {
            if (mover.IsMoving) return;
            if (!waiting) { waiting = true; waitUntil = Time.time + Random.Range(waitMin, waitMax); }
            transform.Rotate(0f, scanSpeed * Time.deltaTime, 0f);   // sweep the view cone
            if (Time.time < waitUntil) return;
            waiting = false;
            if (pf.grid.TryRandomWalkable(transform.position, patrolMin, patrolMax, out Vector3 p))
                mover.MoveTo(p);
        }

        void DoChase(bool sees)
        {
            if (!sees)
            {
                lostTimer += Time.deltaTime;
                if (lostTimer >= loseSeconds) { EnterSearch(); return; }
            }
            Vector3 flat = lastSeen - transform.position;
            flat.y = 0f;
            if (sees && flat.magnitude <= attackDistance)
            {
                mover.Stop();    // clear shot: stand and let BasicGuard fire
                if (flat.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat), 8f * Time.deltaTime);
                return;
            }
            if (Time.time >= nextRepath)
            {
                nextRepath = Time.time + repathSeconds;
                mover.MoveTo(lastSeen);
            }
        }

        void DoSearch(AStarPathfinder pf)
        {
            if (mover.IsMoving) return;
            if (Time.time >= searchEnds) { EnterPatrol(); return; }
            if (!waiting) { waiting = true; waitUntil = Time.time + 1.5f; }
            transform.Rotate(0f, scanSpeed * Time.deltaTime, 0f);
            if (Time.time < waitUntil) return;
            waiting = false;
            mover.speed = patrolSpeed;
            if (pf.grid.TryRandomWalkable(lastSeen, 2f, searchRadius, out Vector3 p))
                mover.MoveTo(p);
        }

        void EnterPatrol()
        {
            state = ScoutState.Patrol;
            mover.speed = patrolSpeed;
            mover.Stop();
            aware = 0f;
            waiting = false;
            SetFiring(false);
        }

        void EnterChase()
        {
            state = ScoutState.Chase;
            mover.speed = chaseSpeed;
            lostTimer = 0f;
            nextRepath = 0f;
            waiting = false;
            SetFiring(true);
        }

        void EnterSearch()
        {
            state = ScoutState.Search;
            mover.speed = chaseSpeed;
            searchEnds = Time.time + searchSeconds;
            aware = 0f;
            waiting = false;
            SetFiring(false);
            if (!mover.MoveTo(lastSeen + lastDir * 3f)) mover.MoveTo(lastSeen);   // head where the player was going
        }

        // BasicGuard shoots in every direction, so it is only switched on while the guard is alert.
        void SetFiring(bool on)
        {
            if (utilityControlled || (TryGetComponent<UtilityEnemyController>(out var utility) && utility.isActiveAndEnabled)) return;
            if (basic == null || basic.enabled == on) return;
            basic.enabled = on;
            if (!on)
            {
                if (basic.warningLight != null) basic.warningLight.enabled = false;
                if (basic.tracer != null) basic.tracer.enabled = false;
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Vector3 o = transform.position + Vector3.up * 1.4f;
            float half = viewAngle * 0.5f;
            Gizmos.DrawLine(o, o + Quaternion.Euler(0f, -half, 0f) * transform.forward * viewRange);
            Gizmos.DrawLine(o, o + Quaternion.Euler(0f, half, 0f) * transform.forward * viewRange);
            Gizmos.DrawLine(o, o + transform.forward * viewRange);
        }
    }
}
