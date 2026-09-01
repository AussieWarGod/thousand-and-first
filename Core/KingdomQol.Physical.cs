using System;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomQol
	{
		/// <summary>Reads one standing root's effective current tags from an exact maintained
		/// survey. Catalogue <c>Provides</c> enters only as a ceiling inside the benefit index.</summary>
		public static bool TryPhysicalOfferOf(GameObject Work, KingdomSurvey Survey,
			out string[] Offer, out string Failure)
		{
			Offer = KingdomQolRules.NoTags;
			Failure = null;
			if (!GameObject.Validate(Work) || Survey == null || Survey.Ground == null
				|| !ReferenceEquals(Work.CurrentZone, Survey.Ground))
			{
				Failure = "Physical quality-of-life evidence needs one root on the surveyed ground.";
				return false;
			}
			if (!Survey.TryBenefits(out KingdomBenefitIndex benefits, out Failure)) return false;
			return TryPhysicalOfferOf(Work, benefits, out Offer, out Failure);
		}

		/// <summary>Pass-scoped overload for callers already sharing one physical reading.</summary>
		public static bool TryPhysicalOfferOf(GameObject Work, KingdomBenefitIndex Benefits,
			out string[] Offer, out string Failure)
		{
			Offer = KingdomQolRules.NoTags;
			Failure = null;
			string rootId = GameObject.Validate(Work) ? Work.IDIfAssigned : null;
			if (string.IsNullOrEmpty(rootId) || Benefits == null)
			{
				Failure = "Physical quality-of-life evidence lacks an assigned root or current reading.";
				return false;
			}
			KingdomBenefitReading reading = Benefits.ReadingForRoot(rootId);
			if (reading?.Designation == null
				|| !string.Equals(reading.Designation.RootId, rootId, StringComparison.Ordinal))
			{
				Failure = "The root has no exact current building designation.";
				return false;
			}
			Offer = reading.Provides.ToArray();
			return true;
		}
	}
}
