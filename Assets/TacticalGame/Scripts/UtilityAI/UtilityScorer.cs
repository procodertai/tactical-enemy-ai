using System;
using TacticalGame.Coordination;

namespace TacticalGame
{
    public enum EnemyAction { Reload, Fire, Hold, Investigate, TakeCover, Flank, FallBack, Support }
    public enum ActionPriority { Fallback, Scored, Rule }

    public struct UtilityInputs
    {
        public float HealthFraction;
        public int Ammo, Capacity, Reserve;
        public bool Visible, ShotBlocked;
        public float Distance, Range;
        public bool CanMove, HasKnownTarget, AtTacticalPosition;
        public bool CoverReady, FlankReady, RetreatReady, SupportReady;
        public bool SettlingAfterDefense;
        public SquadRole Role;
    }

    public struct ActionScore
    {
        public EnemyAction Action;
        public bool Available;
        public float? Score;
        public ActionPriority Priority;
        public string Reason;
        public ActionScore(EnemyAction action, bool available, float score, string reason)
            : this(action, available, ActionPriority.Scored, reason)
        {
            Score = score;
        }

        public ActionScore(EnemyAction action, bool available, ActionPriority priority, string reason)
        {
            Action = action;
            Available = available;
            Priority = priority;
            Score = null;
            Reason = reason;
        }
    }

    // Pure decision logic: no scene access or knowledge of the player's transform.
    public static class UtilityScorer
    {
        public static ActionScore[] Evaluate(UtilityInputs input)
        {
            float health = Math.Max(0f, Math.Min(1f, input.HealthFraction));
            float missing = 1f - Math.Max(0f, Math.Min(1f, (float)input.Ammo / Math.Max(1, input.Capacity)));
            bool reload = input.Ammo < input.Capacity && input.Reserve > 0;
            bool fire = CanFire(input);
            bool movementReady = input.CanMove && input.HasKnownTarget && !input.SettlingAfterDefense;
            bool investigate = movementReady && !fire;
            bool tactical = movementReady && input.Role != SquadRole.Unassigned;
            var reloadChoice = input.Ammo <= 0
                ? new ActionScore(EnemyAction.Reload, reload, ActionPriority.Rule, reload ? "Empty magazine and spare ammo" : "No reserve")
                : new ActionScore(EnemyAction.Reload, reload,
                    // Visible: at most 0.62, below Fire's 0.65 floor, so a guard with a shot never reloads early in the open.
                    !input.Visible ? .4f + .6f * missing : .2f + .5f * missing,
                    !reload ? "Full magazine or no reserve" : "Missing ammo; worth more out of sight");
            return new[] {
                new ActionScore(EnemyAction.FallBack, tactical && input.RetreatReady && health <= .30f,
                    ActionPriority.Rule, "Withdraw at critical health"),
                reloadChoice,
                new ActionScore(EnemyAction.Fire, fire, .65f + .25f * health,
                    !fire ? "Needs a clear shot in range and loaded ammo" : "Clear target; confidence increases with health"),
                new ActionScore(EnemyAction.Investigate, investigate, .55f, "Move toward the latest known position"),
                // Crosses Investigate (0.55) near 54% health and Fire near 38%: wounded guards break off, healthy ones keep shooting.
                new ActionScore(EnemyAction.TakeCover, tactical && input.CoverReady, 1.2f * (1f - health), "Lost health favors cover"),
                new ActionScore(EnemyAction.Flank, tactical && input.FlankReady && input.Role == SquadRole.Flanker &&
                    (!input.AtTacticalPosition || !fire) && (!fire || input.Distance > 7f), .96f, "Take a separate firing position"),
                new ActionScore(EnemyAction.Support, tactical && input.SupportReady && input.Role == SquadRole.Support && !fire,
                    .72f, "Hold a covered support position"),
                new ActionScore(EnemyAction.Hold, true, ActionPriority.Fallback, "No other action is available")
            };
        }

        public static EnemyAction SelectChaseOnly(UtilityInputs input)
        {
            if (input.Ammo <= 0 && input.Reserve > 0) return EnemyAction.Reload;
            if (CanFire(input)) return EnemyAction.Fire;
            if (input.CanMove && input.HasKnownTarget) return EnemyAction.Investigate;
            return EnemyAction.Hold;
        }

        public static bool CanFire(UtilityInputs input) => input.Visible && !input.ShotBlocked &&
            input.Distance <= input.Range && input.Ammo > 0;

        public static EnemyAction Select(ActionScore[] scores, EnemyAction current, bool initialized,
            float heldSeconds, float minimumHold, float margin)
        {
            ActionScore best = default;
            // Equal priorities preserve the policy order, including when two rules apply.
            foreach (var score in scores)
                if (score.Available && (!best.Available || score.Priority > best.Priority ||
                    (score.Priority == best.Priority && score.Score > best.Score))) best = score;
            if (!best.Available) return EnemyAction.Hold;
            if (best.Priority != ActionPriority.Rule && initialized)
            {
                foreach (var score in scores)
                    if (score.Action == current && score.Available &&
                        (heldSeconds < minimumHold || best.Score <= score.Score + margin)) return current;
            }
            return best.Action;
        }
    }
}
