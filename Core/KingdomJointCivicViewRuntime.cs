using System;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Read-only native fan-in for D9. Each owner is validated separately.</summary>
	public static class KingdomJointCivicViewRuntime
	{
		public static bool TryRead(KingdomSystem System, Zone LoadedZone,
			GameObject MootBuilding, GameObject EnclaveRoot,
			out KingdomJointCivicView View, out string Failure)
		{
			View = null;
			Failure = null;
			KingdomJointCivicOwnerView creed = ReadCreed(System);
			KingdomJointCivicOwnerView covenant =
				KingdomVillageCovenantRuntime.ReadOwnerForJointView(System);
			KingdomJointCivicOwnerView moot =
				KingdomAssentingMoot.ReadOwnerForJointView(System, LoadedZone, MootBuilding);
			KingdomJointCivicOwnerView enclave = ReadEnclave(System, LoadedZone, EnclaveRoot);
			return KingdomJointCivicViewRules.TryBuild(creed, covenant, moot, enclave,
				out View, out Failure);
		}

		private static KingdomJointCivicOwnerView ReadCreed(KingdomSystem System)
		{
			if (System == null || !System.Founded
				|| !System.TryGetCurrentIdentity(out string realmId, out _))
				return KingdomJointCivicViewAdapters.Invalid("creed",
					"The current realm identity is unavailable.");
			if (string.IsNullOrEmpty(System.DeclaredCreed))
				return KingdomJointCivicViewAdapters.CreedDeclaration(realmId,
					System.FoundedTick, null, null);
			if (!KingdomCreed.CanBeCreed(Factions.GetIfExists(System.DeclaredCreed)))
				return KingdomJointCivicViewAdapters.Invalid("creed",
					"The declared creed no longer passes its native faction gate.");
			return KingdomJointCivicViewAdapters.CreedDeclaration(realmId,
				System.FoundedTick, System.DeclaredCreed, KingdomCreed.Report(System));
		}

		/// <summary>
		/// The hosted enclave, re-proved against ground this realm actually holds today.
		/// <para>
		/// An authority that names a realm and a zone is making a claim, and matching the ids it
		/// was handed only proves the claim is self-consistent. What has to be true for it to be
		/// evidence is that the realm is <i>this</i> realm, that the zone is one this realm still
		/// owns, and that the settlement id is the id of the settlement that owns it &mdash; all
		/// read from the topology already in memory. A zone is never loaded or thawed to make any
		/// of that come out true; ground that is not to hand is ground this view cannot vouch for.
		/// </para>
		/// </summary>
		private static KingdomJointCivicOwnerView ReadEnclave(KingdomSystem System,
			Zone LoadedZone, GameObject EnclaveRoot)
		{
			if (!KingdomHostedArcology.TryReadAuthorityForJointView(System, EnclaveRoot,
				out KingdomHostedArcologyAuthority authority, out string report,
				out bool missing, out string failure))
				return KingdomJointCivicViewAdapters.Invalid("enclave", failure);
			if (missing)
				return KingdomJointCivicViewAdapters.Missing("enclave",
					"No hosted enclave is recorded.");
			if (!System.TryGetCurrentIdentity(out string realmId, out _)
				|| authority == null
				|| !string.Equals(authority.RealmId, realmId, StringComparison.Ordinal))
				return KingdomJointCivicViewAdapters.Invalid("enclave",
					"The hosted enclave names another realm than the one standing.");
			if (LoadedZone == null || !string.Equals(authority.ZoneId, LoadedZone.ZoneID,
				StringComparison.Ordinal))
				return KingdomJointCivicViewAdapters.Invalid("enclave",
					"The hosted enclave is not on the exact loaded ground being read.");
			// These two topology APIs reject both overlap and absence. Do not hand-select the seat
			// or a non-seat claimant: ambiguity must stay invalid rather than acquire precedence.
			if (!System.OwnedZone(authority.ZoneId)
				|| !string.Equals(System.SettlementIdForOwnedZone(authority.ZoneId),
					authority.SettlementId, StringComparison.Ordinal))
				return KingdomJointCivicViewAdapters.Invalid("enclave",
					"The hosted enclave is not on exact current, uniquely owned settlement ground.");
			return KingdomJointCivicViewAdapters.Enclave(authority, report);
		}
	}
}
