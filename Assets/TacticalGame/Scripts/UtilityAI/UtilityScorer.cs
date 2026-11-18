using System;

namespace TacticalGame
{
    public enum EnemyAction { Reload, Fire, Hold }

    public struct UtilityInputs
    {
        public float HealthFraction;
        public int Ammo, Capacity, Reserve;
        public bool Visible;
        public float Distance, Range;
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
            return new[] {
                new ActionScore(EnemyAction.Reload, reload,
                    input.Ammo <= 0 ? 1f : !input.Visible ? .95f : .2f + .5f * missing + .45f * (1f - health),
                    !reload ? "Full magazine or no reserve" : "Missing ammo and health pressure; safer without sight"),
                new ActionScore(EnemyAction.Fire, fire, .65f + .25f * health,
                    !fire ? "Needs visible target in range and loaded ammo" : "Clear target; confidence increases with health"),
                new ActionScore(EnemyAction.Hold, true, .1f, "Remain stationary")
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
