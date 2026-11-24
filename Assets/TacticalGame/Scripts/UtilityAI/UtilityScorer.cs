using System;

namespace TacticalGame
{
    public enum EnemyAction { Reload, Fire, Hold, Chase, Retreat, Advance }

    public struct UtilityInputs
    {
        public float HealthFraction;
        public int Ammo, Capacity, Reserve;
        public bool Visible;
        public float Distance, Range;
        public bool KnowsTarget, CanMove, HasCover; // KnowsTarget: a player position is known, seen or remembered
        public float UnderFire;                     // 1 right after a hit, fading to 0
        public TacticRole Role;                     // squad order; None = individual behaviour only
        public bool HasOrderPoint;                  // the squad gave this guard a destination
        public bool PartnerCovering;                // some teammate currently signals Covering
    }

    public struct ActionScore
    {
        public EnemyAction Action;
        public bool Available;
        public float Score;
        public string Reason;
        public ActionScore(EnemyAction action, bool available, float score, string reason)
        { Action = action; Available = available; Score = score; Reason = reason; }
    }

    // Pure decision logic: no scene access or knowledge of the player's transform.
    public static class UtilityScorer
    {
        public const float OrderBonus = .30f;       // squad role bonus on the action the role exists for
        public const float AdvanceBase = .55f;
        public const float GateHold = .45f;         // Hold bonus while a mover waits for the cover signal
        public const float OverwatchHold = .50f;
        public const float RetreatCap = .99f;       // why: an empty-magazine Reload (1.00) must still beat a bonus-ed Retreat

        public static ActionScore[] Evaluate(UtilityInputs input)
        {
            float health = Math.Max(0f, Math.Min(1f, input.HealthFraction));
            float missing = 1f - Math.Max(0f, Math.Min(1f, (float)input.Ammo / Math.Max(1, input.Capacity)));
            bool reload = input.Ammo < input.Capacity && input.Reserve > 0;
            bool fire = input.Visible && input.Distance <= input.Range && input.Ammo > 0;
            bool chase = input.CanMove && input.KnowsTarget && input.Ammo > 0 && (!input.Visible || input.Distance > input.Range);
            bool retreat = input.CanMove && input.KnowsTarget && input.HasCover;
            var role = input.Role;
            bool mover = role == TacticRole.Flanker || role == TacticRole.Mover;
            bool gateOpen = input.CanMove && input.HasOrderPoint && mover && input.PartnerCovering;
            if (mover || role == TacticRole.Overwatch) chase = false;
            if (role == TacticRole.Retreater) retreat = input.CanMove && input.KnowsTarget && (input.HasCover || input.HasOrderPoint);
            float underFire = Math.Max(0f, Math.Min(1f, input.UnderFire));
            float fireScore = .65f + .25f * health, retreatScore = .9f * (1f - health) * (1f - health) + .3f * underFire;
            string fireReason = !fire ? "Needs visible target in range and loaded ammo" : $"0.65 + 0.25 x health {health:F2}";
            string retreatReason = !retreat ? "Needs a reachable cover marker" : $"0.90 x hurt {1f - health:F2}^2 + 0.30 x under fire {input.UnderFire:F2}";
            if (role == TacticRole.Pinner || role == TacticRole.Coverer) { fireScore += OrderBonus; if (fire) fireReason += $" + squad order {OrderBonus:F2}"; }
            if (role == TacticRole.Retreater)
            {
                retreatScore = Math.Min(RetreatCap, retreatScore + OrderBonus);
                if (retreat) retreatReason += $" + squad order {OrderBonus:F2} (cap {RetreatCap:F2})";
            }
            float holdScore = .1f; string holdReason = "fallback, always 0.10";
            if (mover && !gateOpen) { holdScore += GateHold; holdReason = $"0.10 + {GateHold:F2} waiting for cover signal"; }
            else if (role == TacticRole.Overwatch) { holdScore += OverwatchHold; holdReason = $"0.10 + {OverwatchHold:F2} overwatch: covering teammate"; }
            bool advance = gateOpen;
            return new[] {
                new ActionScore(EnemyAction.Reload, reload,
                    input.Ammo <= 0 ? 1f : !input.Visible ? .95f : .2f + .5f * missing + .45f * (1f - health),
                    // When available, the reason is the score's own arithmetic so the overlay can show it.
                    !reload ? "Full magazine or no reserve" : input.Ammo <= 0 ? "empty magazine = 1.00"
                        : !input.Visible ? "player hidden, safe to reload = 0.95"
                        : $"0.20 + 0.50 x missing {missing:F2} + 0.45 x hurt {1f - health:F2}"),
                new ActionScore(EnemyAction.Fire, fire, fireScore, fireReason),
                new ActionScore(EnemyAction.Hold, true, holdScore, holdReason),
                new ActionScore(EnemyAction.Chase, chase, .35f + .45f * health,
                    mover ? "squad order: move only under cover" : role == TacticRole.Overwatch ? "squad order: hold overwatch"
                    : !chase ? "Needs a known player position that is out of sight or out of range" : $"0.35 + 0.45 x health {health:F2}"),
                new ActionScore(EnemyAction.Retreat, retreat, retreatScore, retreatReason),
                new ActionScore(EnemyAction.Advance, advance, AdvanceBase + OrderBonus,
                    advance ? $"{AdvanceBase:F2} + squad order {OrderBonus:F2}"
                        : !(mover && input.HasOrderPoint && input.CanMove) ? "no squad move order" : "waiting: no teammate covering")
            };
        }

        public static EnemyAction Select(ActionScore[] scores, EnemyAction current, bool initialized,
            float heldSeconds, float minimumHold, float margin)
        {
            ActionScore best = scores[0];
            foreach (var score in scores)
                if (score.Available && (!best.Available || score.Score > best.Score)) best = score;
            foreach (var score in scores)
                if (initialized && score.Action == current && score.Available &&
                    (heldSeconds < minimumHold || best.Score <= score.Score + margin)) return current;
            return best.Action;
        }
    }
}
