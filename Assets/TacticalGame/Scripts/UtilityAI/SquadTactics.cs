using UnityEngine;

namespace TacticalGame
{
    public enum SquadPlay { Freestyle, PinAndFlank, BoundingAdvance, CoveredRetreat, SplitSearch, Contain }
    public enum TacticRole { None, Pinner, Flanker, Mover, Overwatch, Retreater, Coverer, Searcher, Shelter, Scout, Support }
    public enum SquadSignal { Idle, Covering, Moving, CoverMe, Reloading, Arrived, Blocked }
    public enum DemoDifficulty { Easy, Medium, Hard }

    public struct MemberSnapshot
    {
        public bool Alive, SeesPlayer, HasClearShot, CanCover, NeedsCover;
        public float Health;
        public int Ammo;
        public Vector3 Position;
        public Vector3? Flank, Cover, Peek, LeftSearch, RightSearch, Step, Patrol;
    }

    public struct SquadSnapshot
    {
        public MemberSnapshot[] Members;
        public bool KnowsTarget;
        public int NextMover;
        public DemoDifficulty Difficulty;
    }

    public struct PlayScore
    {
        public SquadPlay Play;
        public bool Available;
        public float Score;
        public string Reason;

        public PlayScore(SquadPlay play, bool available, float score, string reason)
        { Play = play; Available = available; Score = score; Reason = reason; }
    }

    public struct MemberOrder
    {
        public TacticRole Role;
        public Vector3? Destination;
        public Vector3? PeekPoint;
        public bool HasPoint => Destination.HasValue;
        public Vector3 Point => Destination.GetValueOrDefault();

        public MemberOrder(TacticRole role, Vector3? destination = null, Vector3? peekPoint = null)
        { Role = role; Destination = destination; PeekPoint = peekPoint; }
    }

    public static class SquadTactics
    {
        public const float HurtThreshold = .35f;
        public const float MinHold = 1.1f;
        public const float SwitchMargin = .07f;

        static int Watcher(MemberSnapshot[] members, int excluding = -1, bool requireClearShot = false)
        {
            int best = -1;
            for (int i = 0; i < members.Length; i++)
            {
                var member = members[i];
                if (i == excluding || !member.Alive || !member.CanCover || (requireClearShot && (!member.HasClearShot || member.NeedsCover))) continue;
                int priority = (member.HasClearShot ? 10 : 0) + (!member.NeedsCover ? 20 : 0) + member.Ammo;
                int previous = best < 0 ? -1 : (members[best].HasClearShot ? 10 : 0) +
                    (!members[best].NeedsCover ? 20 : 0) + members[best].Ammo;
                if (priority > previous)
                    best = i;
            }
            return best;
        }

        static int Wounded(MemberSnapshot[] members)
        {
            int best = -1;
            for (int i = 0; i < members.Length; i++)
                if (members[i].Alive && members[i].Cover.HasValue && members[i].Health < HurtThreshold &&
                    (best < 0 || members[i].Health < members[best].Health)) best = i;
            return best;
        }

        static int Flanker(MemberSnapshot[] members, int watcher)
        {
            for (int i = 0; i < members.Length; i++)
                if (i != watcher && members[i].Alive && members[i].Health >= HurtThreshold && members[i].Flank.HasValue &&
                    !(members[i].NeedsCover && members[i].Cover.HasValue))
                    return i;
            return -1;
        }

        static bool SearchPair(MemberSnapshot[] members, int watcher, out int left, out int right)
        {
            left = right = -1;
            for (int i = 0; i < members.Length; i++)
            {
                if (i == watcher || !members[i].Alive || !members[i].LeftSearch.HasValue) continue;
                for (int j = 0; j < members.Length; j++)
                {
                    if (j == i || j == watcher || !members[j].Alive || !members[j].RightSearch.HasValue) continue;
                    if (Vector3.Distance(members[i].LeftSearch.Value, members[j].RightSearch.Value) < 3f) continue;
                    left = i; right = j;
                    return true;
                }
            }
            return false;
        }

        static int NextStepper(SquadSnapshot snapshot)
        {
            var members = snapshot.Members;
            for (int i = 0; i < members.Length; i++)
            {
                int index = (snapshot.NextMover + i) % members.Length;
                if (members[index].Alive && members[index].Step.HasValue && Watcher(members, index) >= 0)
                    return index;
            }
            return -1;
        }

        public static PlayScore[] Evaluate(SquadSnapshot snapshot)
        {
            var members = snapshot.Members;
            int watch = Watcher(members), wounded = Wounded(members);
            bool seen = false;
            foreach (var member in members) seen |= member.Alive && member.SeesPlayer;
            int pinner = Watcher(members, requireClearShot: true);
            bool flank = pinner >= 0 && Flanker(members, pinner) >= 0;
            bool search = !seen && snapshot.KnowsTarget && watch >= 0 && SearchPair(members, watch, out _, out _);
            bool advance = !seen && snapshot.KnowsTarget && NextStepper(snapshot) >= 0;
            bool retreat = snapshot.KnowsTarget && wounded >= 0 && Watcher(members, wounded) >= 0;
            return new[] {
                new PlayScore(SquadPlay.Freestyle, true, .20f, "Scout a sector while teammates watch and support"),
                new PlayScore(SquadPlay.PinAndFlank, flank, .86f, flank ? "Clear main angle and reachable side route" : "Needs a loaded watcher and an open side route"),
                new PlayScore(SquadPlay.BoundingAdvance, advance, .64f, advance ? "One reachable approach, move in turns" : "Needs a reachable step and a watcher"),
                new PlayScore(SquadPlay.CoveredRetreat, retreat, 1f, retreat ? "Wounded guard has a reachable shelter" : "Needs a wounded guard, shelter and a watcher"),
                new PlayScore(SquadPlay.SplitSearch, search, .80f, search ? "Check two different sides of the reported area" : "Needs two search routes and a watcher"),
                new PlayScore(SquadPlay.Contain, seen, .68f, seen ? "Change position to recover a clear shot" : "Requires visual contact")
            };
        }

        public static SquadPlay Select(PlayScore[] scores, SquadPlay current, float heldSeconds, float minimumHold = MinHold)
        {
            PlayScore best = scores[0];
            foreach (var candidate in scores)
                if (candidate.Available && candidate.Score > best.Score) best = candidate;
            var previous = scores[(int)current];
            if (!previous.Available || best.Play == SquadPlay.CoveredRetreat) return best.Play;
            if (heldSeconds < minimumHold || best.Score <= previous.Score + SwitchMargin) return current;
            return best.Play;
        }

        public static MemberOrder[] Assign(SquadPlay play, SquadSnapshot snapshot)
        {
            var members = snapshot.Members;
            var orders = new MemberOrder[members.Length];
            if (play == SquadPlay.Freestyle) return Patrol(snapshot, orders);
            int wounded = play == SquadPlay.CoveredRetreat ? Wounded(members) : -1;
            int stepper = play == SquadPlay.BoundingAdvance ? NextStepper(snapshot) : -1;
            int watch = Watcher(members, wounded >= 0 ? wounded : stepper);
            for (int i = 0; i < members.Length; i++)
                if (members[i].Alive) orders[i] = new MemberOrder(TacticRole.Overwatch);

            switch (play)
            {
                case SquadPlay.PinAndFlank:
                    watch = Watcher(members, requireClearShot: true);
                    int flank = Flanker(members, watch);
                    if (watch >= 0) orders[watch] = new MemberOrder(TacticRole.Pinner);
                    if (flank >= 0) orders[flank] = new MemberOrder(TacticRole.Flanker, members[flank].Flank);
                    SendSupport(members, orders, watch, flank, snapshot.Difficulty);
                    break;
                case SquadPlay.SplitSearch:
                    if (watch >= 0 && SearchPair(members, watch, out int left, out int right))
                    {
                        orders[left] = new MemberOrder(TacticRole.Searcher, members[left].LeftSearch);
                        orders[right] = new MemberOrder(TacticRole.Searcher, members[right].RightSearch);
                    }
                    break;
                case SquadPlay.BoundingAdvance:
                    if (stepper >= 0) orders[stepper] = new MemberOrder(TacticRole.Mover, members[stepper].Step);
                    if (snapshot.Difficulty == DemoDifficulty.Hard)
                        SendSupport(members, orders, watch, stepper, snapshot.Difficulty);
                    break;
                case SquadPlay.CoveredRetreat:
                    if (watch >= 0) orders[watch] = new MemberOrder(TacticRole.Coverer);
                    if (wounded >= 0) orders[wounded] = new MemberOrder(TacticRole.Retreater, members[wounded].Cover);
                    SendSupport(members, orders, watch, wounded, snapshot.Difficulty);
                    break;
                case SquadPlay.Contain:
                    if (watch < 0)
                        for (int i = 0; i < members.Length && watch < 0; i++)
                            if (members[i].Alive && members[i].SeesPlayer) watch = i;
                    bool clear = watch >= 0 && members[watch].HasClearShot && !members[watch].NeedsCover;
                    if (watch >= 0) orders[watch] = new MemberOrder(clear ? TacticRole.Pinner : TacticRole.Overwatch);
                    bool sent = false;
                    for (int i = 0; i < members.Length; i++)
                    {
                        if (i == watch || !members[i].Alive) continue;
                        orders[i] = SupportOrder(members[i]);
                        for (int earlier = 0; earlier < i && orders[i].HasPoint; earlier++)
                        {
                            if (!orders[earlier].HasPoint || Vector3.Distance(orders[i].Point, orders[earlier].Point) >= 1.8f) continue;
                            var alternate = members[i].Step;
                            orders[i] = alternate.HasValue && Vector3.Distance(alternate.Value, orders[earlier].Point) >= 1.8f
                                ? new MemberOrder(TacticRole.Support, alternate)
                                : new MemberOrder(TacticRole.Overwatch);
                        }
                        sent |= orders[i].HasPoint;
                    }
                    if (!sent && !clear && watch >= 0) orders[watch] = SupportOrder(members[watch]);
                    break;
            }
            return orders;
        }

        static MemberOrder[] Patrol(SquadSnapshot snapshot, MemberOrder[] orders)
        {
            var members = snapshot.Members;
            int scout = -1;
            for (int offset = 0; offset < members.Length; offset++)
            {
                int index = (snapshot.NextMover + offset) % members.Length;
                if (!members[index].Alive || !members[index].Patrol.HasValue) continue;
                scout = index;
                break;
            }
            if (scout < 0) return orders;
            orders[scout] = new MemberOrder(TacticRole.Scout, members[scout].Patrol);
            int watcher = Watcher(members, scout);
            if (watcher >= 0) orders[watcher] = new MemberOrder(TacticRole.Overwatch);
            for (int i = 0; i < members.Length; i++)
                if (i != scout && i != watcher && members[i].Alive)
                    orders[i] = new MemberOrder(TacticRole.Support, members[i].Patrol);
            return orders;
        }

        static void SendSupport(MemberSnapshot[] members, MemberOrder[] orders, int watcher, int lead, DemoDifficulty difficulty)
        {
            for (int i = 0; i < members.Length; i++)
            {
                if (i == watcher || i == lead || !members[i].Alive) continue;
                if (difficulty == DemoDifficulty.Easy && !members[i].NeedsCover && !members[i].Peek.HasValue) continue;
                var support = SupportOrder(members[i]);
                if (lead >= 0 && support.HasPoint && orders[lead].HasPoint &&
                    Vector3.Distance(support.Point, orders[lead].Point) < 2f)
                    support = new MemberOrder(TacticRole.Overwatch);
                orders[i] = support;
            }
        }

        static MemberOrder SupportOrder(MemberSnapshot member)
        {
            if (member.Cover.HasValue && (member.NeedsCover || member.Peek.HasValue))
                return new MemberOrder(TacticRole.Shelter, member.Cover, member.Peek);
            if (member.Flank.HasValue || member.Step.HasValue)
                return new MemberOrder(TacticRole.Support, member.Flank ?? member.Step);
            return new MemberOrder(TacticRole.Overwatch);
        }
    }
}
