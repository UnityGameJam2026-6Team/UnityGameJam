using System.Collections.Generic;
using System.Linq;
using UnityGameJam.Contract;
using UnityGameJam.Proposal;

namespace UnityGameJam.Minion
{
    internal static class ProposalCandidateFinder
    {
        public static (EZoneId[] Zones, EProposalReason Reason) Find(MinionBrain minion,
            FacilityLayout layout, WorkerCapability capability, IReadOnlyCollection<MinionBrain> others)
        {
            EZoneId? current = minion.AssignedZone;
            if (!current.HasValue) { return (System.Array.Empty<EZoneId>(), default); }

            EFloor floor = ZoneDisplayName.GetFloor(current.Value);
            EZoneId? currentPosition = minion.CurrentZone;
            EZoneId[] candidates = layout.Zones.Select(area => area.Zone)
                .Where(zone => zone != current && zone != currentPosition && !minion.Memory.IsKnownClean(zone))
                .Distinct()
                .OrderBy(zone => ZoneDisplayName.GetFloor(zone) != floor)
                .ThenBy(zone => zone).ToArray();

            EZoneId[] knownWork = candidates.Where(zone => minion.Memory.HasKnownWork(zone, minion.Worker, capability)).ToArray();
            if (knownWork.Length > 0) { return (PreferFloor(knownWork, floor), EProposalReason.KnownWork); }
            EZoneId[] unvisited = candidates.Where(zone => !minion.Memory.HasVisited(zone)).ToArray();
            if (unvisited.Length > 0) { return (PreferFloor(unvisited, floor), EProposalReason.Explore); }
            EZoneId[] occupied = candidates.Where(zone => others.Any(other => other != minion && other.IsWorkingIn(zone))).ToArray();
            return (PreferFloor(occupied, floor), EProposalReason.OtherWorker);
        }

        private static EZoneId[] PreferFloor(EZoneId[] candidates, EFloor floor)
        {
            if (candidates.Length == 0 || ZoneDisplayName.GetFloor(candidates[0]) != floor)
            {
                return candidates;
            }
            return candidates.TakeWhile(zone => ZoneDisplayName.GetFloor(zone) == floor).ToArray();
        }
    }
}
