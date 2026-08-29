using System;
using XRL;

namespace ThousandAndFirst
{
	/// <summary>Production bridge from one closed physical event into a bounded C18 offer.</summary>
	internal static class KingdomWitnessWorkLifecycleRuntime
	{
		internal static bool TryCaptureClosed(KingdomSystem System,
			KingdomWitnessWorkSource Source, long ObservedTick, out bool Recorded,
			out string Failure)
		{
			Recorded = false; Failure = null;
			if (System == null || Source == null || ObservedTick < Source.ClosedTick
				|| !KingdomMaster.NewWorkAllowed(System)
				|| !System.TryGetCurrentIdentity(out string realmId, out string settlementId)
				|| !string.Equals(Source.SettlementId, settlementId, StringComparison.Ordinal))
			{
				Failure = "closed witness event no longer belongs to the exact current realm";
				return false;
			}
			if (!KingdomExperienceRuntime.TryObserveConfiguredOptions(System, ObservedTick,
				out Failure)) return false;
			if (!KingdomExperienceRules.CanEmit(System.Experience,
				KingdomExperienceOptionKind.CivicStory, Source.ClosedTick)) return true;
			KingdomCivicMemorySystem memory = The.Game?.GetSystem<KingdomCivicMemorySystem>();
			if (memory == null)
			{
				Failure = "C18 civic memory is absent"; return false;
			}
			return KingdomWitnessWorkCommit.TryCaptureClosed(memory, realmId, Source,
				out Recorded, out _, out Failure);
		}
	}
}
