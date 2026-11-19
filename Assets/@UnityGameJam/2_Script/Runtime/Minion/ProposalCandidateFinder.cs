using System;
using System.Collections.Generic;
using System.Linq;
using UnityGameJam.Contract;
using UnityGameJam.Proposal;

namespace UnityGameJam.Minion
{
    internal static class ProposalCandidateFinder
    {
        public static (EZoneId[] Zones, EProposalReason Reason, IReadOnlyDictionary<EZoneId, EWorkerType> Coworkers) Find(
            MinionBrain minion, FacilityLayout layout, WorkerCapability capability, IReadOnlyCollection<MinionBrain> others,
            bool canBorrowMop, EContaminationType? preferredTask = null)
        {
            EZoneId? current = minion.AssignedZone;
            if (!current.HasValue) { return (Array.Empty<EZoneId>(), default, null); }

            EZoneId? currentPosition = minion.CurrentZone;
            var candidates = new List<(EZoneId Zone, int Cost, int PreferredCount, int WorkCount, EProposalReason Reason)>();
            var coworkers = new Dictionary<EZoneId, EWorkerType>();
            foreach (EZoneId zone in layout.Zones.Select(area => area.Zone).Distinct())
            {
                if (zone == current || zone == currentPosition || minion.Memory.IsKnownClean(zone)) { continue; }
                int workCount = minion.Memory.KnownTaskCount(zone, minion.Worker, capability, canBorrowMop);
                bool hasKnownWork = workCount > 0;
                int preferredCount = preferredTask is EContaminationType type && (canBorrowMop || type != EContaminationType.FloorStain)
                    ? minion.Memory.KnownTaskCount(zone, type) : 0;
                if (!hasKnownWork && (minion.Memory.HasCompletedSurvey(zone) || HasOtherAssignment(minion, zone, others))) { continue; }
                MinionBrain coworker = others.FirstOrDefault(other => other != minion && other.IsWorkingIn(zone));
                if (coworker != null) { coworkers.Add(zone, coworker.Worker); }
                int cost = Rank(minion.Floor, ZoneDisplayName.GetFloor(zone), hasKnownWork, coworker != null);
                if (preferredCount > 0) { cost -= 3; }
                EProposalReason reason = coworker != null ? EProposalReason.OtherWorker
                    : hasKnownWork ? EProposalReason.KnownWork : EProposalReason.Explore;
                candidates.Add((zone, cost, preferredCount, workCount, reason));
            }
            var sorted = candidates.OrderBy(candidate => candidate.Cost).ThenByDescending(candidate => candidate.PreferredCount)
                .ThenByDescending(candidate => candidate.WorkCount).ThenBy(candidate => candidate.Zone).ToArray();
            return (sorted.Select(candidate => candidate.Zone).ToArray(), sorted.FirstOrDefault().Reason, coworkers);
        }

        internal static bool HasOtherAssignment(MinionBrain minion, EZoneId zone, IEnumerable<MinionBrain> others) =>
            others.Any(other => other != minion && other.AssignedZone == zone);

        internal static int Rank(EFloor current, EFloor destination, bool hasKnownWork, bool hasCoworker) =>
            3 * Math.Abs((int)current - (int)destination) - (hasKnownWork ? 12 : 0) - (hasCoworker ? 5 : 0);
    }
}
