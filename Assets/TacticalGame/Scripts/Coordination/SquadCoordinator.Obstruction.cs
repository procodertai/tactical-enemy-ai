using System;
using System.Collections.Generic;
using UnityEngine;

namespace TacticalGame.Coordination
{
    public sealed partial class SquadCoordinator
    {
        sealed class ObstructionPair
        {
            public string Priority, Yielding;
            public float Started, Reported;
            public bool RequestedReplan;
        }
        readonly Dictionary<string, ObstructionPair> obstructionPairs = new Dictionary<string, ObstructionPair>(StringComparer.Ordinal);
        readonly Dictionary<string, YieldInstruction> yieldInstructions = new Dictionary<string, YieldInstruction>(StringComparer.Ordinal);
        readonly List<string> stalePairs = new List<string>();
        float ObstructionLifetime => Positive(settings != null ? settings.ObstructionReportLifetime : .75f, .75f);
        float ReplanDelay => Positive(settings != null ? settings.ObstructionReplanSeconds : 2f, 2f);
        float BlockingDistance => Positive(settings != null ? settings.ObstructionDistance : 2.5f, 2.5f);

        public YieldInstruction ReportObstruction(string enemyId, string blockingEnemyId)
        {
            metrics.ObstructionReports++;
            if (!CoordinationEnabled) return new YieldInstruction(enemyId, blockingEnemyId, YieldAction.CoordinationDisabled, 0);
            if (enemyId == blockingEnemyId || !LiveMember(enemyId, out var member) || !LiveMember(blockingEnemyId, out var blocker))
                return new YieldInstruction(enemyId, blockingEnemyId, YieldAction.InvalidRequest, 0);
            TickObstructions();
            if (Vector3.Distance(member.Position, blocker.Position) > BlockingDistance)
                return new YieldInstruction(enemyId, blockingEnemyId, YieldAction.None, 0);
            string priority = StringComparer.Ordinal.Compare(enemyId, blockingEnemyId) < 0 ? enemyId : blockingEnemyId;
            string yielding = priority == enemyId ? blockingEnemyId : enemyId;
            string key = priority + "\u001f" + yielding;
            if (!obstructionPairs.TryGetValue(key, out var pair))
            {
                pair = new ObstructionPair { Priority = priority, Yielding = yielding, Started = Time.time };
                obstructionPairs.Add(key, pair); Log(yielding + " yields to " + priority);
            }
            pair.Reported = Time.time;
            bool replan = Time.time >= pair.Started + ReplanDelay;
            if (replan && !pair.RequestedReplan)
            {
                pair.RequestedReplan = true; metrics.ReplanRequests++;
                if (memberClaims.TryGetValue(yielding, out int marker)) RemoveClaim(marker, DestinationReleaseReason.Obstruction);
                Log(yielding + " needs another route or destination.");
            }
            TickObstructions();
            TacticalStateChanged?.Invoke();
            return yieldInstructions[enemyId]; // Navigation executes this instruction; no paths are planned here.
        }

        public bool TryGetYieldInstruction(string enemyId, out YieldInstruction instruction)
        {
            instruction = default;
            if (!CoordinationEnabled || enemyId == null) return false;
            TickObstructions();
            return yieldInstructions.TryGetValue(enemyId, out instruction);
        }

        void TickObstructions()
        {
            stalePairs.Clear();
            foreach (var pair in obstructionPairs)
                if (Time.time - pair.Value.Reported > ObstructionLifetime ||
                    !LiveMember(pair.Value.Priority, out var priority) || !LiveMember(pair.Value.Yielding, out var yielding) ||
                    Vector3.Distance(priority.Position, yielding.Position) > BlockingDistance)
                    stalePairs.Add(pair.Key);
            foreach (string key in stalePairs) obstructionPairs.Remove(key);
            // Rebuild from the still-valid pair reports. Lower IDs always retain priority, preventing wait cycles.
            yieldInstructions.Clear();
            foreach (var pair in obstructionPairs.Values)
            {
                MergeInstruction(new YieldInstruction(pair.Priority, pair.Yielding, YieldAction.Proceed, 0));
                MergeInstruction(new YieldInstruction(pair.Yielding, pair.Priority,
                    pair.RequestedReplan ? YieldAction.Replan : YieldAction.Wait, pair.Started + ReplanDelay));
            }
        }

        void MergeInstruction(YieldInstruction instruction)
        {
            if (!yieldInstructions.TryGetValue(instruction.EnemyId, out var existing) ||
                (instruction.Action == YieldAction.Replan && existing.Action != YieldAction.Replan) ||
                (instruction.Action == YieldAction.Wait && existing.Action == YieldAction.Proceed))
                yieldInstructions[instruction.EnemyId] = instruction;
        }

        void RemoveObstructionsFor(string enemyId)
        {
            stalePairs.Clear();
            foreach (var pair in obstructionPairs)
                if (pair.Value.Priority == enemyId || pair.Value.Yielding == enemyId) stalePairs.Add(pair.Key);
            foreach (string key in stalePairs) obstructionPairs.Remove(key);
            TickObstructions();
        }
    }
}
