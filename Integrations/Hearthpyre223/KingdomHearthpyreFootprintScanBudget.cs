using System.Collections.Generic;

namespace ThousandAndFirst.Integrations.Hearthpyre223
{
	/// <summary>One exact Hearth footprint callback's aggregate read-work boundary. Container
	/// bounds prevent one hostile registry; this shared budget prevents bounded registries from
	/// multiplying into an unbounded nested scan.</summary>
	internal sealed class KingdomHearthpyreFootprintScanBudget
	{
		internal const int MaxRegistryEntries = 65536;
		internal const int MaxInspectionWork = 16 * MaxRegistryEntries;
		internal const string LimitFailure =
			"Hearthpyre footprint inspection work budget exceeded";

		internal int Used { get; private set; }
		internal bool Exhausted { get; private set; }

		internal bool TryCharge(int Entries)
		{
			if (Exhausted || Entries < 0 || Entries > MaxRegistryEntries
				|| Used > MaxInspectionWork - Entries)
			{
				Exhausted = true; return false;
			}
			Used += Entries; return true;
		}

		internal static bool TryAccount(IReadOnlyList<int> Groups,
			out int Used, out string Failure)
		{
			KingdomHearthpyreFootprintScanBudget budget =
				new KingdomHearthpyreFootprintScanBudget();
			if (Groups == null || Groups.Count > MaxInspectionWork)
			{
				Used = 0; Failure = LimitFailure; return false;
			}
			for (int i = 0; i < Groups.Count; i++)
				if (!budget.TryCharge(Groups[i]))
				{
					Used = budget.Used; Failure = LimitFailure; return false;
				}
			Used = budget.Used; Failure = null; return true;
		}
	}
}
