using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Engine boundary for semantic capabilities. Every query uses one exact live
	/// designation/provider snapshot and fails closed; no catalogue category is runtime proof.</summary>
	internal static class KingdomCapabilityRuntime
	{
		internal static bool TryIndex(Zone Z, KingdomSurvey Survey, string Consumer,
			out KingdomBenefitIndex Benefits)
		{
			return KingdomReach.TryActiveBenefits(Z, Survey,
				Consumer ?? "capability", out Benefits);
		}

		internal static int Count(Zone Z, KingdomSurvey Survey, string Capability,
			string Consumer)
		{
			return TryIndex(Z, Survey, Consumer, out KingdomBenefitIndex benefits)
				? KingdomBenefitCapabilities.Count(benefits.Readings, Capability) : 0;
		}

		internal static bool HasRoot(Zone Z, KingdomSurvey Survey, GameObject Root,
			string Capability, string Consumer)
		{
			return GameObject.Validate(Root) && ReferenceEquals(Root.CurrentZone, Z)
				&& !string.IsNullOrEmpty(Root.IDIfAssigned)
				&& TryIndex(Z, Survey, Consumer, out KingdomBenefitIndex benefits)
				&& KingdomBenefitCapabilities.Has(
					benefits.ReadingForRoot(Root.IDIfAssigned), Capability);
		}

		internal static List<GameObject> Roots(Zone Z, KingdomSurvey Survey,
			string Capability, string Consumer)
		{
			List<GameObject> result = new List<GameObject>();
			if (!TryIndex(Z, Survey, Consumer, out KingdomBenefitIndex benefits)) return result;
			IReadOnlyList<KingdomBenefitReading> readings = benefits.Readings;
			for (int i = 0; i < readings.Count; i++)
			{
				KingdomBenefitReading reading = readings[i];
				if (!KingdomBenefitCapabilities.Has(reading, Capability)
					|| !KingdomReach.TryRoot(Z, reading, out GameObject root)) continue;
				bool ours = reading.Designation.ProviderId == "taf.architecture"
					|| reading.Designation.ProviderId == "taf.adoption";
				if (!ours || KingdomUpgrade.IsFunctionallyBuilt(root)) result.Add(root);
			}
			result.Sort((a, b) => string.CompareOrdinal(a.IDIfAssigned, b.IDIfAssigned));
			return result;
		}
	}
}
