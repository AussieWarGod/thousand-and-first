using System;

namespace ThousandAndFirst
{
	/// <summary>Origin-bound C18 section-one access for O5. Missing is empty; future,
	/// quarantine, malformed, and foreign-realm authorities are never rewritten.</summary>
	public static class KingdomWitnessWorkLease
	{
		public const int SectionId = KingdomCivicMemoryLimits.SectionCivicArtifacts;

		public static bool TryReadAuthority(IKingdomCivicMemoryAuthority Authority,
			string RealmId, out KingdomCivicMemorySectionLease Lease,
			out KingdomCivicArtifactsEnvelope Held, out string Failure)
		{
			Lease = null; Held = null; Failure = null;
			if (Authority == null || !KingdomIdentityRules.IsRealmId(RealmId))
				return Fail("witness-work C18 authority is absent or has noncanonical identity",
					out Failure);
			if (!Authority.TryReadSection(SectionId, out Lease, out Failure)) return false;
			if (Lease == null || Lease.SectionId != SectionId)
				return Fail("C18 returned the wrong witness-work section lease", out Failure);
			if (TryInterpret(Lease.Payload(), RealmId, out Held, out Failure)) return true;
			Lease = null; Held = null; return false;
		}

		public static bool TryInterpret(byte[] Payload, string RealmId,
			out KingdomCivicArtifactsEnvelope Held, out string Failure)
		{
			Held = null; Failure = null;
			if (!KingdomIdentityRules.IsRealmId(RealmId))
				return Fail("witness-work realm identity is noncanonical", out Failure);
			KingdomCivicArtifactsEnvelope value = KingdomCivicArtifactsStore.ReadForRealm(
				Payload == null || Payload.Length == 0 ? null : Payload, RealmId,
				out string readFailure);
			string identityFailure = null;
			bool valid = value != null && KingdomCivicArtifactsStore.TryValidateIdentity(value,
				out identityFailure);
			if (value == null || value.IsOpaqueFuture || value.Quarantined
				|| !string.IsNullOrEmpty(readFailure) || !value.IdentityBound
				|| !string.Equals(value.RealmId, RealmId, StringComparison.Ordinal)
				|| !valid)
				return Fail(readFailure ?? identityFailure ?? value?.Fault
					?? "witness-work C18 authority is future, quarantined, or foreign",
					out Failure);
			Held = value; return true;
		}

		public static bool TryReadBackRow(IKingdomCivicMemoryAuthority Authority,
			string RealmId, string WorkId, out KingdomWitnessWorkReceipt Receipt,
			out string Failure)
		{
			Receipt = null;
			if (!TryReadAuthority(Authority, RealmId, out _,
				out KingdomCivicArtifactsEnvelope held, out Failure)) return false;
			Receipt = KingdomWitnessWorkRules.FindExact(held.WitnessWorks, WorkId);
			return Receipt != null || Fail("C18 holds no exact witness-work row", out Failure);
		}

		internal static bool Fail(string Text, out string Failure)
		{
			Failure = Text; return false;
		}
	}
}
