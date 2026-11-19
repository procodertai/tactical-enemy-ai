using System;
using TacticalGame.Coordination;

namespace TacticalGame
{
    public enum EnemyAction { Reload, Fire, Hold, Investigate, TakeCover, Flank, FallBack, Support }

    public struct UtilityInputs
    {
        public float HealthFraction;
        public int Ammo, Capacity, Reserve;
        public bool Visible;
        public float Distance, Range;
        public bool CanMove, HasKnownTarget, AtTacticalPosition;
        public SquadRole Role;
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
        public static ActionScore[] Evaluate(UtilityInputs input)
        {
            float health = Math.Max(0f, Math.Min(1f, input.HealthFraction));
            float missing = 1f - Math.Max(0f, Math.Min(1f, (float)input.Ammo / Math.Max(1, input.Capacity)));
            bool reload = input.Ammo < input.Capacity && input.Reserve > 0;
            bool fire = input.Visible && input.Distance <= input.Range && input.Ammo > 0;
            bool investigate = input.CanMove && input.HasKnownTarget && !fire;
            bool tactical = input.CanMove && input.HasKnownTarget && input.Role != SquadRole.Unassigned;
            return new[] {
                new ActionScore(EnemyAction.Reload, reload,
                    input.Ammo <= 0 ? 1f : !input.Visible ? .95f : .2f + .5f * missing + .45f * (1f - health),
                    !reload ? "Full magazine or no reserve" : "Missing ammo and health pressure; safer without sight"),
                new ActionScore(EnemyAction.Fire, fire, .65f + .25f * health,
                    !fire ? "Needs visible target in range and loaded ammo" : "Clear target; confidence increases with health"),
                new ActionScore(EnemyAction.Hold, true, .1f, "Remain stationary"),
                new ActionScore(EnemyAction.Investigate, investigate, .55f, "Move toward the latest known position"),
                new ActionScore(EnemyAction.TakeCover, tactical && health <= .55f, .96f, "Low health favors cover"),
                new ActionScore(EnemyAction.Flank, tactical && input.Role == SquadRole.Flanker &&
                    (!input.AtTacticalPosition || !fire) && (!fire || input.Distance > 7f), .96f, "Take a separate firing position"),
                new ActionScore(EnemyAction.FallBack, tactical && health <= .30f, 1.1f, "Withdraw at critical health"),
                new ActionScore(EnemyAction.Support, tactical && input.Role == SquadRole.Support && !fire,
                    .72f, "Hold a covered support position")
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
