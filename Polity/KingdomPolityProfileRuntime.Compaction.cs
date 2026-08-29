using System.Globalization;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityProfileRuntime
	{
		private static bool TryCompactForCapacity(KingdomPolityLedger Ledger, long Tick,
			bool Requested, out string Failure)
		{
			Failure = null;
			if (!Requested || !HasCompactableRevision(Ledger)) return true;
			string receipt = KingdomPolityRules.ActivationId(
				"taf:compaction:profile-runtime:v1:",
				"polity-profile-runtime-compaction-v1", Ledger.RealmId,
				Ledger.Revision.ToString(CultureInfo.InvariantCulture),
				Tick.ToString(CultureInfo.InvariantCulture));
			return KingdomPolityRules.TryCompactRetiredProfiles(Ledger, receipt, Tick,
				out Failure);
		}

		private static bool HasCompactableRevision(KingdomPolityLedger Ledger)
		{
			for (int i = 0; i < Ledger.Profiles.Count; i++)
			{
				KingdomPolityProfileRevision profile = Ledger.Profiles[i];
				if (profile.Revision == 1) continue;
				bool pinned = false;
				for (int p = 0; p < Ledger.Polities.Count && !pinned; p++)
					pinned = Ledger.Polities[p].ProfileId == profile.ProfileId &&
						Ledger.Polities[p].ProfileRevision == profile.Revision;
				for (int c = 0; c < Ledger.Cohorts.Count && !pinned; c++)
					pinned = Ledger.Cohorts[c].ProfileId == profile.ProfileId &&
						Ledger.Cohorts[c].ProfileRevision == profile.Revision;
				if (!pinned) return true;
			}
			return false;
		}
	}
}
