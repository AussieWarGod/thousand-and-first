using System;
using System.Collections.Generic;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Engine-free facts the paid heart chain reads by target rung, held once so the supply step,
	/// the payment check and the handover probe can never name different designs, and the one
	/// city-book reading the capital seed gates on. Both test projects execute these.
	/// </summary>
	internal static class KingdomCampHeartChainRules
	{
		/// <summary>The design a paid chain improvement climbs FROM, or null off the chain.</summary>
		internal static string PredecessorKey(int Target)
		{
			switch (Target)
			{
				case 3: return "heartwaterstone";
				case 4: return "heartmoot";
				case 5: return "heartcourt";
				default: return null;
			}
		}

		/// <summary>The design a paid chain improvement climbs TO, which is the key production
		/// hands its handover (the job's TargetKey), or null off the chain.</summary>
		internal static string SuccessorKey(int Target)
		{
			switch (Target)
			{
				case 3: return "heartmoot";
				case 4: return "heartcourt";
				case 5: return "arcology";
				default: return null;
			}
		}

		/// <summary>
		/// Whether the published city book holds exactly ONE work row for this hall on this zone
		/// carrying the crown design. The crown is resolved from the book's design column, which
		/// matches the catalogue key or its blueprint without regard to case
		/// (Growth/KingdomCrownDiscovery.cs:121-143), and a hall the book has not read cannot crown
		/// anything, so the capital seed asks this before it asks whether the ground is crowned.
		/// </summary>
		internal static bool CrownBookHolds(IList<int> WorkIds, IList<string> WorkZoneIds,
			IList<string> WorkDesignKeys, int HallId, string ZoneId, string CrownKey,
			string CrownBlueprint)
		{
			if (WorkIds == null || WorkZoneIds == null || WorkDesignKeys == null
				|| string.IsNullOrEmpty(ZoneId) || string.IsNullOrEmpty(CrownKey)
				|| WorkZoneIds.Count < WorkIds.Count || WorkDesignKeys.Count < WorkIds.Count)
				return false;
			int rows = 0;
			for (int i = 0; i < WorkIds.Count; i++)
			{
				if (WorkIds[i] != HallId
					|| !string.Equals(WorkZoneIds[i], ZoneId, StringComparison.Ordinal)) continue;
				string design = WorkDesignKeys[i];
				if (string.Equals(design, CrownKey, StringComparison.OrdinalIgnoreCase)
					|| (!string.IsNullOrEmpty(CrownBlueprint)
						&& string.Equals(design, CrownBlueprint, StringComparison.OrdinalIgnoreCase)))
					rows++;
			}
			return rows == 1;
		}
	}
}
