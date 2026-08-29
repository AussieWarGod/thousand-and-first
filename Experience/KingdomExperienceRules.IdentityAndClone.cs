using System;

namespace ThousandAndFirst
{
	public static partial class KingdomExperienceRules
	{
		public static KingdomExperienceOptionReceipt UnobservedOption(
			KingdomExperienceOptionKind Kind)
		{
			return new KingdomExperienceOptionReceipt
			{
				Kind = Kind,
				State = KingdomExperienceOptionState.Unobserved,
				FutureCauseFloorTick = long.MaxValue
			};
		}

		public static void Normalize(KingdomExperienceLedger Ledger)
		{
			if (Ledger == null) return;
			if (Ledger.SchemaState == KingdomExperienceSchemaState.Unknown)
			{
				if (Ledger.OpaqueWireVersion > KingdomExperienceCodec.CurrentWireVersion
					&& Ledger.OpaqueFuturePayload != null) return;
				Quarantine(Ledger, "Current experience wire falsely claims future authority.");
				return;
			}
			if (Ledger.SchemaState == KingdomExperienceSchemaState.Quarantined) return;
			if (!TryValidate(Ledger, out string failure)) Quarantine(Ledger, failure);
		}

		public static void Quarantine(KingdomExperienceLedger Ledger, string Failure)
		{
			if (Ledger == null) return;
			Ledger.SchemaState = KingdomExperienceSchemaState.Quarantined;
			Ledger.SchemaFault = Text(Failure, true) ? Failure : "Experience authority is invalid.";
		}

		public static bool TryBindEmptyIdentity(KingdomExperienceLedger Ledger, string RealmId,
			out string Failure)
		{
			Failure = null;
			if (Ledger == null || !TypedId(RealmId, "taf:realm:"))
				return Fail("experience bind input is invalid", out Failure);
			Normalize(Ledger);
			if (Ledger.SchemaState != KingdomExperienceSchemaState.Compatible)
				return Fail("experience ledger is not compatible", out Failure);
			if (Ledger.IdentityBound)
				return string.Equals(Ledger.RealmId, RealmId, StringComparison.Ordinal)
					|| Fail("experience ledger is bound to another realm", out Failure);
			KingdomExperienceLedger candidate = Clone(Ledger);
			candidate.RealmId = RealmId; candidate.IdentityBound = true; candidate.Revision = 1L;
			if (!TryValidate(candidate, out Failure)) return false;
			Ledger.CopyFrom(candidate); return true;
		}

		public static bool TryRebindEmptyIdentity(KingdomExperienceLedger Ledger, string RealmId,
			out string Failure)
		{
			Failure = null;
			if (Ledger == null || !TypedId(RealmId, "taf:realm:"))
				return Fail("experience rebind input is invalid", out Failure);
			Normalize(Ledger);
			if (Ledger.SchemaState != KingdomExperienceSchemaState.Compatible)
				return Fail("experience ledger is not compatible", out Failure);
			if (!Ledger.IdentityBound) return TryBindEmptyIdentity(Ledger, RealmId, out Failure);
			if (string.Equals(Ledger.RealmId, RealmId, StringComparison.Ordinal)) return true;
			if (Ledger.Audiences.Count > 0 || Ledger.BodyReservations.Count > 0
				|| Ledger.Offices.Count > 0 || Ledger.Remembrances.Count > 0
				|| Ledger.Voices.Count > 0 || Ledger.FirstFeasts.Count > 0)
				return Fail("experience authority requires explicit realm retirement", out Failure);
			if (Ledger.Revision == long.MaxValue)
				return Fail("experience revision is exhausted", out Failure);
			KingdomExperienceLedger candidate = new KingdomExperienceLedger
			{
				RealmId = RealmId, IdentityBound = true, Revision = Ledger.Revision + 1L
			};
			if (!TryValidate(candidate, out Failure)) return false;
			Ledger.CopyFrom(candidate); return true;
		}

		public static KingdomExperienceLedger Clone(KingdomExperienceLedger Ledger)
		{
			if (!TryValidate(Ledger, out string failure))
				throw new InvalidOperationException("Cannot clone invalid experience authority: " + failure);
			return KingdomExperienceCodec.DecodeEnvelopeRaw(
				KingdomExperienceCodec.EncodeEnvelope(Ledger));
		}

		internal static KingdomExperienceOptionReceipt OptionFor(KingdomExperienceLedger L,
			KingdomExperienceOptionKind Kind)
		{
			if (L == null) return null;
			if (Kind == KingdomExperienceOptionKind.CivicStory) return L.Story;
			if (Kind == KingdomExperienceOptionKind.CivicKnowledge) return L.Knowledge;
			if (Kind == KingdomExperienceOptionKind.AmbientUse) return L.Ambient;
			return null;
		}
	}
}
