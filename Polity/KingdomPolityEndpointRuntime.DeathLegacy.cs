using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityEndpointRuntime
	{
		private static bool TryRewriteLegacyDeathIntent(Zone Zone, string LegacyWire,
			KingdomPolityDeathIntentRecord Intent, out string Failure)
		{
			Failure = null;
			if (!KingdomPolityDeathIntentRules.TryEncode(Intent, out string current, out Failure))
				return false;
			string key = KingdomPolityPhysicalCustodyRules.DeathIntentKey(Intent.ProjectionId,
				Intent.ObjectId);
			if (!TryReadExactDeathIntentSlot(Zone, key, out bool present, out bool exact,
				out string actual, out Failure) || !present || !exact || actual != LegacyWire)
				return FailPhysical("legacy death intent changed before migration", out Failure);
			try { Zone.SetZoneProperty(key, current); }
			catch (Exception ex)
			{
				bool read = TryReadExactDeathIntentSlot(Zone, key, out present, out exact,
					out actual, out string inspectFailure);
				KingdomPolityLegacyRewriteRecovery recovery =
					KingdomPolityPhysicalCustodyRules.ClassifyLegacyRewriteRecovery(read,
						present, exact, actual == current, actual == LegacyWire);
				if (recovery == KingdomPolityLegacyRewriteRecovery.Applied)
					{ Failure = null; return true; }
				if (recovery == KingdomPolityLegacyRewriteRecovery.OldBytesPreserved)
					return FailPhysical("legacy death intent migration failed before write: " +
						ex.Message, out Failure);
				return FailPhysical(inspectFailure ??
					"legacy death intent migration left ambiguous bytes", out Failure);
			}
			if (TryReadExactDeathIntentSlot(Zone, key, out present, out exact, out actual,
				out Failure) && present && exact && actual == current) return true;
			return FailPhysical(Failure ??
				"legacy death intent migration did not install exact current bytes", out Failure);
		}
	}
}
