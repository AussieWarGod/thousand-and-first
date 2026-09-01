using System;
using System.Collections.Generic;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>One read-only boundary for carries that include durable attended outputs.</summary>
	public static class KingdomObservedBenefitProjection
	{
		public static bool TryCarries(GameObject Root, KingdomBenefitReading Live,
			out List<KindAmount> Carries, out string Failure)
		{
			Carries = null; Failure = null;
			if (!GameObject.Validate(Root) || Root.CurrentZone == null
				|| string.IsNullOrEmpty(Root.IDIfAssigned) || Live?.Designation == null
				|| Live.Designation.RootId != Root.IDIfAssigned
				|| Live.Designation.ZoneId != Root.CurrentZone.ZoneID)
				return Fail("observed benefit root does not match its live designation", out Failure);
			int roof = 0;
			int luxury = 0;
			bool hosted = KingdomUpgrade.DesignKeyOf(Root) == KingdomHostedArcology.ArcologyKey
				&& Root.GetPart<r_KingdomArcology>() != null;
			if (hosted && !KingdomHostedArcology.TryWardPhysical(
				Root, out roof, out luxury, out Failure)) return false;
			int effectiveness = hosted ? KingdomWear.EffectivenessOf(Root) : 100;
			return KingdomObservedBenefitProjectionRules.TryProject(Live.Carries,
				roof, luxury, effectiveness, out Carries, out Failure);
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
