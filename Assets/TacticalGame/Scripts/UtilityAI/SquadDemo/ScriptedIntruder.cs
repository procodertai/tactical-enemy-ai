using System.Collections.Generic;
using UnityEngine;

namespace TacticalGame
{
    public enum IntruderScenario { Idle, Exposed, Hide, Ambush }

    // Drives the (disabled) player body so the presenter can watch the guards without playing.
    public sealed class ScriptedIntruder : MonoBehaviour
    {
        const float Speed = 3.2f, FireEvery = 1.1f, FireRange = 25f, Damage = 10f, Floor = .15f, LatticeStep = 1.5f;
        enum Phase { Idle, Walking, Striking, Standing }

        public IntruderScenario Scenario { get; private set; }
        public string Status { get; private set; } = "waiting";
        public readonly List<TargetDummy> guards = new List<TargetDummy>(); // filled by the director

        Phase phase;
        List<Vector3> path;
        int index;
        float groundY, fireLeft, strikeLeft;
        int strikesLeft;
        TargetDummy ambushTarget;
        Vector3 faceDir;
        LineRenderer tracer;
        float tracerUntil;
        PlayerController player;

        void Awake() { player = GetComponent<PlayerController>(); }

        public void Stop()
        {
            Scenario = IntruderScenario.Idle;
            phase = Phase.Idle;
            path = null;
            Status = "waiting";
        }

        public void Begin(IntruderScenario scenario, TargetDummy target)
        {
            Scenario = scenario; ambushTarget = target; path = null; phase = Phase.Idle;
            groundY = transform.position.y;
            Vector3 goal = transform.position;
            bool found = scenario == IntruderScenario.Ambush
                ? target != null && target.IsAlive && Reachable(target.transform.position)
                : Pick(scenario == IntruderScenario.Exposed, out goal);
            if (!found) { Debug.LogWarning("Squad demo: no " + scenario + " position found; intruder stays put."); Status = "no valid position, standing still"; phase = Phase.Standing; fireLeft = 1e9f; return; }
            goal = scenario == IntruderScenario.Ambush ? target.transform.position : goal;
            path = AStarPathfinder.Instance.FindPath(transform.position, goal);
            index = 0; phase = Phase.Walking; fireLeft = FireEvery;
            Status = scenario == IntruderScenario.Hide ? "walking to hiding spot" : scenario == IntruderScenario.Exposed ? "walking into the open" : "sneaking up on " + target.targetId;
        }

        bool Reachable(Vector3 goal) { return AStarPathfinder.Instance != null && AStarPathfinder.Instance.FindPath(transform.position, goal) != null; }

        Vector3 Eye(Vector3 feet) => feet + Vector3.up * (Positioning.Instance != null ? Positioning.Instance.eyeHeight : 1.45f);

        // Scan the map on a lattice and keep the best-scoring reachable point; deterministic, no hard-coded coordinates.
        bool Pick(bool exposed, out Vector3 best)
        {
            best = transform.position;
            var pf = AStarPathfinder.Instance; var pos = Positioning.Instance;
            if (pf == null || pf.grid == null || pos == null) return false;
            var alive = new List<TargetDummy>();
            Vector3 centroid = Vector3.zero;
            foreach (var g in guards) if (g != null && g.IsAlive) { alive.Add(g); centroid += g.transform.position; }
            if (alive.Count == 0) return false;
            centroid /= alive.Count;
            var scored = new List<KeyValuePair<float, Vector3>>();
            var grid = pf.grid;
            for (float x = -grid.gridWorldSize.x / 2f; x < grid.gridWorldSize.x / 2f; x += LatticeStep)
                for (float z = -grid.gridWorldSize.y / 2f; z < grid.gridWorldSize.y / 2f; z += LatticeStep)
                {
                    var node = grid.NodeFromWorldPoint(grid.transform.position + new Vector3(x, 0f, z));
                    if (!node.walkable) continue;
                    var feet = new Vector3(node.worldPos.x, groundY, node.worldPos.z);
                    int seen = 0; float nearest = float.MaxValue;
                    foreach (var g in alive)
                    {
                        float d = Vector3.Distance(feet, g.transform.position);
                        nearest = Mathf.Min(nearest, d);
                        if (pos.HasLineOfSight(Eye(feet), Eye(g.transform.position))) seen++;
                    }
                    float fromMe = Vector3.Distance(feet, transform.position);
                    if (exposed)
                    {
                        if (seen < 2 || nearest < 10f || nearest > 16f) continue;
                        scored.Add(new KeyValuePair<float, Vector3>(seen * 10f - Mathf.Abs(nearest - 13f) - .05f * fromMe, feet));
                    }
                    else
                    {
                        float c = Vector3.Distance(feet, centroid);
                        if (seen > 0 || c < 10f || c > 20f) continue;
                        scored.Add(new KeyValuePair<float, Vector3>(-Mathf.Abs(c - 15f) - .05f * fromMe, feet));
                    }
                }
            scored.Sort((a, b) => b.Key.CompareTo(a.Key));
            for (int i = 0; i < scored.Count && i < 10; i++)
                if (pf.FindPath(transform.position, scored[i].Value) != null) { best = scored[i].Value; return true; } // unreachable spots are skipped
            return false;
        }

        void Update()
        {
            var game = GameSession.Instance;
            if (game == null || !game.CanControl) return;
            float dt = Time.deltaTime;
            if (phase == Phase.Walking) Walk(dt);
            else if (phase == Phase.Striking) Strike(dt);
            else if (phase == Phase.Standing && Scenario != IntruderScenario.Hide) ReturnFire(dt);
            if (tracer != null && Time.time > tracerUntil) tracer.enabled = false;
            Turn(dt);
        }

        void Walk(float dt)
        {
            if (Scenario == IntruderScenario.Ambush && ambushTarget != null && ambushTarget.IsAlive)
            {
                Vector3 g = ambushTarget.transform.position;
                if (Vector3.Distance(transform.position, g) < 14f && Positioning.Instance != null && Positioning.Instance.HasLineOfSight(Eye(transform.position), Eye(g)))
                { StartStrike(); return; }
            }
            if (path == null || index >= path.Count)
            {
                phase = Phase.Standing; fireLeft = FireEvery;
                Status = Scenario == IntruderScenario.Hide ? "hiding, not shooting" : "standing in open, returning fire";
                return;
            }
            Vector3 to = new Vector3(path[index].x, groundY, path[index].z);
            Vector3 step = Vector3.MoveTowards(transform.position, to, Speed * dt);
            Vector3 dir = to - transform.position;
            if (dir.sqrMagnitude > .0001f) faceDir = dir.normalized;
            // The path already avoids solids. Keep the player collider active for the guards' sight and shot queries.
            transform.position = step;
            Physics.SyncTransforms();
            if ((to - step).sqrMagnitude < .01f) index++;
        }

        void StartStrike()
        {
            phase = Phase.Striking; strikesLeft = 3; strikeLeft = .2f; Status = "ambushing guard " + ambushTarget.targetId;
        }

        void Strike(float dt)
        {
            strikeLeft -= dt;
            if (strikeLeft > 0f) return;
            var h = ambushTarget != null ? ambushTarget.health : null;
            if (h != null && h.IsAlive && strikesLeft > 0)
            {
                // Split the damage so three hits leave the guard near 30% health.
                float amount = Mathf.Max(0f, h.Current - .3f * h.maximum) / strikesLeft;
                if (amount > 0f) Shoot(ambushTarget, amount);
                strikesLeft--; strikeLeft = .35f;
            }
            if (strikesLeft <= 0 || h == null || !h.IsAlive)
            { phase = Phase.Standing; fireLeft = FireEvery; Status = "standing in open, returning fire"; }
        }

        void ReturnFire(float dt)
        {
            fireLeft -= dt;
            if (fireLeft > 0f) return;
            fireLeft = FireEvery;
            var pos = Positioning.Instance;
            if (pos == null) return;
            Vector3 eye = player != null && player.viewCamera != null ? player.viewCamera.transform.position : Eye(transform.position);
            TargetDummy pick = null; float bestDist = FireRange;
            foreach (var g in guards)
            {
                if (g == null || !g.IsAlive) continue;
                float d = Vector3.Distance(eye, g.transform.position);
                // Never take a guard under the floor so the demo cannot end by itself.
                if (d >= bestDist || (g.health.Current - Damage) / g.health.maximum < Floor) continue;
                if (!pos.HasLineOfSight(eye, Eye(g.transform.position))) continue;
                pick = g; bestDist = d;
            }
            if (pick != null) Shoot(pick, Damage);
        }

        void Shoot(TargetDummy guard, float amount)
        {
            guard.health.TakeDamage(amount);
            GuardPerception.BroadcastNoise(transform.position, 22f);
            Vector3 from = Eye(transform.position) - Vector3.up * .15f, to = Eye(guard.transform.position) - Vector3.up * .45f;
            Vector3 flat = guard.transform.position - transform.position; flat.y = 0f;
            if (flat.sqrMagnitude > .01f) { faceDir = flat.normalized; }
            ShowTracer(from, to);
        }

        void ShowTracer(Vector3 from, Vector3 to)
        {
            if (tracer == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                if (shader == null) return;
                tracer = new GameObject("Intruder Tracer").AddComponent<LineRenderer>();
                tracer.transform.SetParent(transform, false);
                tracer.material = new Material(shader) { color = new Color(1f, .85f, .3f) };
                tracer.positionCount = 2; tracer.startWidth = tracer.endWidth = .05f; tracer.useWorldSpace = true;
            }
            tracer.SetPosition(0, from); tracer.SetPosition(1, to);
            tracer.enabled = true; tracerUntil = Time.time + .08f;
        }

        void Turn(float dt)
        {
            if (faceDir.sqrMagnitude > .01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(faceDir), 360f * dt);
            if (player != null && player.viewCamera != null) player.viewCamera.transform.localRotation = Quaternion.identity; // keep the view level
        }
    }
}
