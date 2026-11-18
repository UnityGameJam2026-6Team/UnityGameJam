using System.Collections.Generic;
using System.Linq;
using UnityGameJam.Contract;
using UnityGameJam.Proposal;

namespace UnityGameJam.Minion
{
    internal static class ProposalCandidateFinder
    {
        public static (EZoneId Zone, EProposalReason Reason)? Find(MinionBrain minion,
            FacilityLayout layout, WorkerCapability capability, IReadOnlyCollection<MinionBrain> others)
        {
            EZoneId? current = minion.AssignedZone;
            if (!current.HasValue) { return null; }

            EFloor floor = ZoneDisplayName.GetFloor(current.Value);
            EZoneId[] candidates = layout.Zones.Select(area => area.Zone)
                .Where(zone => zone != current && !minion.Memory.IsKnownClean(zone))
                .OrderBy(zone => ZoneDisplayName.GetFloor(zone) != floor)
                .ThenBy(zone => zone).ToArray();

            foreach (EZoneId zone in candidates)
            {
                if (minion.Memory.HasKnownWork(zone, minion.Worker, capability)) { return (zone, EProposalReason.KnownWork); }
            }
            foreach (EZoneId zone in candidates)
            {
                if (!minion.Memory.HasVisited(zone)) { return (zone, EProposalReason.Explore); }
            }
            foreach (EZoneId zone in candidates)
            {
                if (others.Any(other => other != minion && other.IsWorkingIn(zone))) { return (zone, EProposalReason.OtherWorker); }
            }
            return null;
        }
    }
}
