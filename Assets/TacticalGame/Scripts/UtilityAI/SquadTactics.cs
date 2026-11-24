using System;
using UnityEngine;

namespace TacticalGame
{
    public enum SquadPlay { Freestyle, PinAndFlank, BoundingAdvance, CoveredRetreat }
    public enum TacticRole { None, Pinner, Flanker, Mover, Overwatch, Retreater, Coverer }
    public enum SquadSignal { Idle, Covering, Moving, CoverMe, Reloading }

    public struct MemberSnapshot
    {
        public string Id;
        public bool Alive;
        public float Health;            // 0..1
        public int Ammo, Capacity;
        public bool SeesPlayer;
        public Vector3 Position;
        public bool HasFlankPoint; public Vector3 FlankPoint;
        public bool HasCoverPoint; public Vector3 CoverPoint;
    }

    public struct SquadSnapshot
    {
        public MemberSnapshot[] Members;
        public bool KnowsTarget;
        public Vector3 TargetPoint;
    }

    public struct PlayScore
    {
        public SquadPlay Play; public bool Available; public float Score; public string Reason;
        public PlayScore(SquadPlay play, bool available, float score, string reason)
        { Play = play; Available = available; Score = score; Reason = reason; }
    }

    public struct MemberOrder { public TacticRole Role; public bool HasPoint; public Vector3 Point; }

    // Squad level of the two-level Utility AI: scores "plays" and hands out roles. Pure, no scene access.
    public static class SquadTactics
    {
        public const float HurtThreshold = .35f;          // below this a member is "hurt" and wants out
        public const float RetreatBase = .70f, RetreatRange = .30f;
        public const float PinBase = .55f, PinRange = .25f;
        public const float BoundBase = .50f, BoundRange = .30f;
        public const float FreestyleScore = .30f;
        public const float MinHold = 2f;                  // seconds a play is kept while still available
        public const float SwitchMargin = .08f;
        public const float BoundStep = 4f;                // metres a bounding mover advances per order

        static float Dist(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x, y = a.y - b.y, z = a.z - b.z;
            return (float)Math.Sqrt(x * x + y * y + z * z);
        }

        // Pinner = seeing member with the most ammo (-1 if nobody sees the player).
        static int PickPinner(SquadSnapshot s)
        {
            int best = -1;
            for (int i = 0; i < s.Members.Length; i++)
            {
                var m = s.Members[i];
                if (m.Alive && m.SeesPlayer && (best < 0 || m.Ammo > s.Members[best].Ammo)) best = i;
            }
            return best;
        }

        // Flanker = nearest other healthy member with a flank point (-1 if none).
        static int PickFlanker(SquadSnapshot s, int pinner)
        {
            int best = -1; float bestD = float.MaxValue;
            for (int i = 0; i < s.Members.Length; i++)
            {
                var m = s.Members[i];
                if (i == pinner || !m.Alive || m.Health < HurtThreshold || !m.HasFlankPoint) continue;
                float d = pinner >= 0 ? Dist(m.Position, s.Members[pinner].Position) : 0f;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        public static PlayScore[] Evaluate(SquadSnapshot s)
        {
            int alive = 0, healthy = 0, lowest = -1; bool anySees = false; float healthySum = 0f, aliveSum = 0f;
            for (int i = 0; i < s.Members.Length; i++)
            {
                var m = s.Members[i];
                if (!m.Alive) continue;
                alive++; aliveSum += m.Health; anySees |= m.SeesPlayer;
                if (m.Health >= HurtThreshold) { healthy++; healthySum += m.Health; }
                if (lowest < 0 || m.Health < s.Members[lowest].Health) lowest = i;
            }
            var scores = new PlayScore[4];

            bool retreat = lowest >= 0 && s.Members[lowest].Health < HurtThreshold && alive >= 2;
            float low = lowest >= 0 ? s.Members[lowest].Health : 1f;
            scores[(int)SquadPlay.CoveredRetreat] = new PlayScore(SquadPlay.CoveredRetreat, retreat,
                RetreatBase + RetreatRange * (1f - low / HurtThreshold),
                !retreat ? "needs a member under 35% health and a teammate to cover"
                    : $"{RetreatBase:F2} + {RetreatRange:F2} x (1 - lowest {low:F2}/{HurtThreshold:F2})");

            int pinner = PickPinner(s), flanker = pinner >= 0 ? PickFlanker(s, pinner) : -1;
            bool pin = anySees && healthy >= 2 && flanker >= 0;
            float avgHealthy = healthy > 0 ? healthySum / healthy : 0f;
            scores[(int)SquadPlay.PinAndFlank] = new PlayScore(SquadPlay.PinAndFlank, pin,
                PinBase + PinRange * avgHealthy,
                pin ? $"{PinBase:F2} + {PinRange:F2} x avg healthy {avgHealthy:F2}"
                    : !anySees ? "nobody sees the player" : healthy < 2 ? "needs 2 healthy members" : "no healthy member has a flank point");

            bool bound = !anySees && s.KnowsTarget && alive >= 2;
            float avgAlive = alive > 0 ? aliveSum / alive : 0f;
            scores[(int)SquadPlay.BoundingAdvance] = new PlayScore(SquadPlay.BoundingAdvance, bound,
                BoundBase + BoundRange * avgAlive,
                bound ? $"{BoundBase:F2} + {BoundRange:F2} x avg health {avgAlive:F2}"
                    : anySees ? "player already seen" : !s.KnowsTarget ? "no known player position" : "needs 2 alive members");

            scores[(int)SquadPlay.Freestyle] = new PlayScore(SquadPlay.Freestyle, true, FreestyleScore, "fallback, always 0.30");
            return scores;
        }

        // Hysteresis: keep the current play while it is available and young, then only switch for a clear lead.
        public static SquadPlay Select(PlayScore[] scores, SquadPlay current, float heldSeconds)
        {
            PlayScore best = scores[(int)SquadPlay.Freestyle];
            foreach (var p in scores) if (p.Available && p.Score > best.Score) best = p;
            var cur = scores[(int)current];
            if (!cur.Available) return best.Play;
            if (heldSeconds < MinHold) return current;
            return best.Score > cur.Score + SwitchMargin ? best.Play : current;
        }

        public static MemberOrder[] Assign(SquadPlay play, SquadSnapshot s, int boundMoverIndex)
        {
            var orders = new MemberOrder[s.Members.Length]; // default = None
            switch (play)
            {
                case SquadPlay.CoveredRetreat:
                {
                    int low = -1;
                    for (int i = 0; i < orders.Length; i++)
                        if (s.Members[i].Alive && (low < 0 || s.Members[i].Health < s.Members[low].Health)) low = i;
                    for (int i = 0; i < orders.Length; i++)
                        if (s.Members[i].Alive) orders[i].Role = i == low ? TacticRole.Retreater : TacticRole.Coverer;
                    if (low >= 0 && s.Members[low].HasCoverPoint) { orders[low].HasPoint = true; orders[low].Point = s.Members[low].CoverPoint; }
                    break;
                }
                case SquadPlay.PinAndFlank:
                {
                    int pin = PickPinner(s), flank = pin >= 0 ? PickFlanker(s, pin) : -1;
                    for (int i = 0; i < orders.Length; i++)
                        if (s.Members[i].Alive) orders[i].Role = i == pin ? TacticRole.Pinner : i == flank ? TacticRole.Flanker : TacticRole.Overwatch;
                    if (flank >= 0) { orders[flank].HasPoint = true; orders[flank].Point = s.Members[flank].FlankPoint; }
                    break;
                }
                case SquadPlay.BoundingAdvance:
                {
                    int count = 0;
                    foreach (var m in s.Members) if (m.Alive) count++;
                    if (count == 0) break;
                    int pick = ((boundMoverIndex % count) + count) % count, seen = 0;
                    for (int i = 0; i < orders.Length; i++)
                    {
                        if (!s.Members[i].Alive) continue;
                        if (seen++ != pick) { orders[i].Role = TacticRole.Overwatch; continue; }
                        orders[i].Role = TacticRole.Mover; orders[i].HasPoint = true;
                        Vector3 from = s.Members[i].Position, d = s.TargetPoint - from; float len = Dist(s.TargetPoint, from);
                        orders[i].Point = len <= BoundStep ? s.TargetPoint : from + d * (BoundStep / len);
                    }
                    break;
                }
            }
            return orders;
        }
    }
}
