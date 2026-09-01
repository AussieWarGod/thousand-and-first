using System;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomVocationServiceRuntime
	{
		/// <summary>Joins an exact completed-work receipt to its current designated root and
		/// requires live physical bed capacity. Catalogue roof declarations supply no evidence.</summary>
		private static bool IsShelter(KingdomCurrentCityEvidenceRuntime.Context Context,
			KingdomCurrentCityEvidenceRuntime.BuiltWorkSnapshot Evidence,
			KingdomBenefitIndex Benefits)
		{
			if (Context?.Zone == null || Evidence == null || Benefits == null) return false;
			GameObject root = null;
			foreach (GameObject candidate in KingdomSurvey.ObjectsFor(Context.Zone))
			{
				if (!KingdomUpgrade.IsFunctionallyBuilt(candidate)
					|| !string.Equals(candidate.GetStringProperty(
						KingdomConstruction.ReceiptProperty), Evidence.WorkReceiptId,
						StringComparison.Ordinal)) continue;
				if (root != null) return false;
				root = candidate;
			}
			if (!KingdomLodging.TryHomeReading(root, Benefits,
				out KingdomBenefitReading reading, out _)
				|| KingdomLodging.IsCondemned(root)
				|| !string.Equals(reading.Designation.BuildingKey, Evidence.DesignKey,
					StringComparison.Ordinal)) return false;
			return Benefits.AmountForRoot(root.IDIfAssigned, "roof") > 0;
		}
	}
}
