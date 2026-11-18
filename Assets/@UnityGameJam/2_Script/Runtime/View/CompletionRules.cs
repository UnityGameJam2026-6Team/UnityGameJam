using UnityGameJam.Contract;

namespace UnityGameJam.View
{
    internal static class CompletionRules
    {
        internal static bool IsFullyClean(IMatchReadModel match)
        {
            if (match.FacilityCleanliness < 100f) { return false; }
            foreach (ItemSnapshot item in match.Items)
            {
                if (item.State == EItemState.OnFloor || item.State == EItemState.InDischargeArea ||
                    item.State == EItemState.Carried)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
