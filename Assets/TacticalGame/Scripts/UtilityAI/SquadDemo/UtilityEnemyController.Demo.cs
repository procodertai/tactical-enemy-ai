using UnityEngine;

namespace TacticalGame
{
    public sealed partial class UtilityEnemyController
    {
        PathMover demoNavigation;
        MemberOrder demoOrder;
        bool demoRouteStarted, peeking;
        Vector3? demoContact;
        Vector3 demoWatchDirection;
        DemoDifficulty demoDifficulty;
        float demoOrderSince, peekPhaseSince, shelterCooldown, lastHealth;
        int shotsAtShelter, shotsAtPeek;
        public UtilityEnemyController DemoCoverPartner { get; private set; }
        public TacticRole Role => demoOrder.Role;
        public Vector3? DemoDestination => peeking ? demoOrder.PeekPoint : demoOrder.Destination;
        public string Id => target != null ? target.targetId : name;
        public bool Alive => target != null && target.IsAlive;
        public float Health01 => Alive ? target.health.Current / target.health.maximum : 0f;
        public int Ammo => weapon.Ammo;
        public int DemoCapacity => weapon.magazineSize;
        public bool DemoReadyToCover => Alive && weapon.Active && !weapon.IsReloading && Ammo > 0;
        public bool DemoNeedsShelter => Time.time < shelterCooldown ||
            Ammo <= 1 || weapon.ShotsFired - shotsAtShelter >= (demoDifficulty == DemoDifficulty.Hard ? 2 : 3);
        public PathMover DemoNavigation => demoNavigation;
        public bool DemoPeeking => peeking;
        public bool DemoArrived => DemoDestination.HasValue && FlatDistance(transform.position, DemoDestination.Value) < .55f;
        public bool DemoBlocked => demoOrder.HasPoint && demoRouteStarted && !demoNavigation.IsMoving && !DemoArrived;
        public bool DemoCoverRequired => Role == TacticRole.Flanker || Role == TacticRole.Mover || Role == TacticRole.Searcher;
        public SquadSignal Signal => !Alive ? SquadSignal.Idle
            : weapon.IsReloading ? SquadSignal.Reloading
            : DemoBlocked ? SquadSignal.Blocked
            : demoNavigation != null && demoNavigation.IsMoving ? SquadSignal.Moving
            : DemoReadyToCover && (Role == TacticRole.Pinner || Role == TacticRole.Overwatch || Role == TacticRole.Coverer ||
                CurrentAction == EnemyAction.Fire || Role == TacticRole.Support && DemoArrived) ? SquadSignal.Covering
            : DemoArrived ? SquadSignal.Arrived
            : Role == TacticRole.Retreater ? SquadSignal.CoverMe : SquadSignal.Idle;

        internal static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }

        internal void EnableDemo(PathMover navigation)
        {
            SetNavigation(null);
            demoNavigation = navigation;
            demoNavigation.Stop();
            demoWatchDirection = target.body.forward;
            lastHealth = Health01;
        }

        internal void SetDemoDifficulty(DemoDifficulty difficulty)
        {
            demoDifficulty = difficulty;
            demoNavigation.speed = difficulty == DemoDifficulty.Hard ? 4.6f : difficulty == DemoDifficulty.Easy ? 2.7f : 3.6f;
            weapon.damage = difficulty == DemoDifficulty.Hard ? 15f : difficulty == DemoDifficulty.Easy ? 5f : 9f;
            weapon.warningSeconds = difficulty == DemoDifficulty.Hard ? .38f : difficulty == DemoDifficulty.Easy ? 1.2f : .75f;
            weapon.shotInterval = difficulty == DemoDifficulty.Hard ? .52f : difficulty == DemoDifficulty.Easy ? 1.65f : .92f;
            weapon.reloadSeconds = difficulty == DemoDifficulty.Hard ? 1.1f : difficulty == DemoDifficulty.Easy ? 2.1f : 1.6f;
        }

        internal EnemyObservation ReadDemoObservation() => observations.Observe(weapon.range);

        internal bool DemoHasClearShot(EnemyObservation observation) => observation.Visible && weapon.HasClearShot(observation.AimPoint);

        internal void ApplyDemoOrder(MemberOrder order, Vector3? contact, UtilityEnemyController partner)
        {
            if (demoOrder.Role != order.Role || demoOrder.HasPoint != order.HasPoint ||
                (order.HasPoint && FlatDistance(demoOrder.Point, order.Point) > .4f) ||
                (peeking && !order.PeekPoint.HasValue))
            {
                demoNavigation.Stop();
                demoRouteStarted = false;
                peeking = false;
                peekPhaseSince = -1f;
                demoOrderSince = Time.time;
                demoOrder = order;
            }
            else if (!peeking) demoOrder.PeekPoint = order.PeekPoint;
            demoContact = contact;
            DemoCoverPartner = partner;
        }

        void UpdateDemo()
        {
            var game = GameSession.Instance;
            if (!weapon.Active || Time.deltaTime <= 0f)
            {
                if (game == null || !Alive || game.State == RoundState.Won || game.State == RoundState.Lost) demoNavigation.Stop();
                if (!weapon.Active) weapon.HideEffects();
                return;
            }
            float dt = Time.deltaTime;
            weapon.Tick(dt);
            if (Health01 < lastHealth) shelterCooldown = Time.time + 2f;
            lastHealth = Health01;
            var observation = ReadDemoObservation();
            UpdatePeek();
            bool covered = DemoCoverPartner != null && DemoCoverPartner.Signal == SquadSignal.Covering;
            bool coverRequired = DemoCoverRequired;
            bool plannedMovement = DemoDestination.HasValue && !DemoArrived && !DemoBlocked;
            float waitForCover = demoDifficulty == DemoDifficulty.Hard ? .35f : .9f;
            bool canMove = plannedMovement && (!coverRequired || covered || Time.time - demoOrderSince >= waitForCover);
            bool inShelter = Role == TacticRole.Shelter && !peeking && DemoArrived;
            bool fire = DemoHasClearShot(observation) && Ammo > 0 && !inShelter;
            var orderAction = Role == TacticRole.Retreater ? EnemyAction.FallBack :
                Role == TacticRole.Shelter ? EnemyAction.TakeCover :
                Role == TacticRole.Flanker ? EnemyAction.Flank : EnemyAction.Investigate;
            bool hasReserve = weapon.Reserve > 0;
            var reload = Ammo == 0
                ? new ActionScore(EnemyAction.Reload, hasReserve, ActionPriority.Rule, "Empty magazine and spare ammo")
                : new ActionScore(EnemyAction.Reload, hasReserve && !canMove && !fire && Ammo < weapon.magazineSize,
                    .65f, "Reload when safe");
            var move = Role == TacticRole.Shelter || Role == TacticRole.Retreater
                ? new ActionScore(orderAction, canMove, ActionPriority.Rule, "Reach assigned shelter before reloading")
                : new ActionScore(orderAction, canMove, 1f, coverRequired ? "Advance with cover when available" : "Move to assigned position");
            Scores = new[] {
                move,
                reload,
                new ActionScore(EnemyAction.Fire, fire && !(coverRequired && plannedMovement), .85f, "Own eyes and clear shot"),
                new ActionScore(EnemyAction.Hold, true, ActionPriority.Fallback,
                    plannedMovement ? "Waiting for a partner to cover" : "Watch from this position")
            };
            var next = weapon.IsReloading ? EnemyAction.Reload : UtilityScorer.Select(Scores, CurrentAction, false, 0f, 0f, 0f);
            if (!initialized || next != CurrentAction)
            {
                LastSwitch = next + " — " + Role;
                CurrentAction = next;
                initialized = true;
            }
            if (!weapon.IsReloading && canMove && next == orderAction)
            {
                weapon.StopAiming();
                target.body.localRotation = Quaternion.Slerp(target.body.localRotation, Quaternion.identity, dt * 8f);
                if (!demoRouteStarted) { demoRouteStarted = true; demoNavigation.MoveTo(DemoDestination.Value); }
                return;
            }
            if (demoNavigation.IsMoving)
            {
                demoNavigation.Stop();
                demoRouteStarted = false;
            }
            if (next == EnemyAction.Fire)
            {
                Face(observation.AimPoint, dt);
                weapon.TryFire(observation.AimPoint, dt);
                return;
            }
            weapon.StopAiming();
            if (next == EnemyAction.Reload) weapon.TryReload();

            if (demoContact.HasValue && Role != TacticRole.Retreater)
                Face(demoContact.Value, dt);
            else if (!demoContact.HasValue)
            {
                float sweep = Mathf.Sin(Time.time * .95f + transform.position.x * .1f) * 48f;
                Face(transform.position + Quaternion.Euler(0f, sweep, 0f) * demoWatchDirection * 6f, dt);
            }
        }

        void UpdatePeek()
        {
            if (Role != TacticRole.Shelter || !demoOrder.PeekPoint.HasValue || !DemoArrived) return;
            if (peekPhaseSince < 0f)
            {
                peekPhaseSince = Time.time;
                if (!peeking) shotsAtShelter = weapon.ShotsFired;
            }
            if (!peeking)
            {
                if (demoContact.HasValue && Ammo > 0 && !weapon.IsReloading &&
                    Time.time - peekPhaseSince >= (demoDifficulty == DemoDifficulty.Hard ? .65f : 1f))
                    SwitchPeek(true);
                return;
            }
            if (weapon.ShotsFired - shotsAtPeek >= 2 || Time.time - peekPhaseSince >= 2.5f || Ammo == 0)
                SwitchPeek(false);
        }

        void SwitchPeek(bool stepOut)
        {
            peeking = stepOut;
            if (stepOut) shotsAtPeek = weapon.ShotsFired;
            peekPhaseSince = -1f;
            demoRouteStarted = false;
            demoNavigation.Stop();
        }
    }
}
