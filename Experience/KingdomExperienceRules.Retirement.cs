using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Read-only, one-row retirement permission. Exactly one typed lease is present.</summary>
	internal sealed class KingdomExperienceRetirementLeaseAllowance
	{
		internal KingdomExperienceAudienceReceipt Audience;
		internal KingdomExperienceBodyReservation Bodies;
	}

	/// <summary>Capacity-only reconstruction for an exact projected source. These APIs never
	/// authorize semantic emission; the caller must already own the persisted source proof.</summary>
	public static partial class KingdomExperienceRules
	{
		/// <summary>Classifies the shared W0 carrier before realm retirement. Civic rows may
		/// remain until their exact visited-ground owners restore them; live audience/body leases
		/// may not disappear with KingdomSystem and must first close through their source lane.</summary>
		public static bool TryDescribeRealmRemovalBlocker(KingdomExperienceLedger Ledger,
			string RealmId, out string Blocker, out string Failure)
		{
			return TryDescribeRealmRemovalBlocker(Ledger, RealmId,
				new List<KingdomExperienceRetirementLeaseAllowance>(), out Blocker, out Failure);
		}

		/// <summary>Ignores only exact source-owned Polity leases authenticated by its current
		/// ledger. Allowances never mutate capacity and cannot excuse foreign or extra rows.</summary>
		internal static bool TryDescribeRealmRemovalBlocker(KingdomExperienceLedger Ledger,
			string RealmId, IList<KingdomExperienceRetirementLeaseAllowance> Allowances,
			out string Blocker, out string Failure)
		{
			Blocker = null; Failure = null;
			if (Ledger == null || Allowances == null || !TryValidate(Ledger, out Failure))
			{
				Failure = Failure ?? "Experience capacity authority is absent.";
				return false;
			}
			bool emptyIdentity = Ledger.RealmId == null
				&& Ledger.Audiences.Count == 0 && Ledger.BodyReservations.Count == 0
				&& Ledger.Offices.Count == 0 && Ledger.Remembrances.Count == 0
				&& Ledger.Voices.Count == 0 && Ledger.FirstFeasts.Count == 0;
			if (!emptyIdentity && Ledger.RealmId != RealmId)
			{
				Failure = "Experience capacity authority belongs to another realm.";
				return false;
			}
			bool[] used = new bool[Allowances.Count];
			int blockedAudiences = 0, blockedBodies = 0;
			for (int i = 0; i < Ledger.Audiences.Count; i++)
				if (!ConsumeExactAllowance(Allowances, used, Ledger.Audiences[i])) blockedAudiences++;
			for (int i = 0; i < Ledger.BodyReservations.Count; i++)
				if (!ConsumeExactAllowance(Allowances, used, Ledger.BodyReservations[i])) blockedBodies++;
			bool extra = false;
			for (int i = 0; i < used.Length; i++) if (!used[i] ||
				!ValidPolityAllowance(Allowances[i])) { extra = true; break; }
			if (blockedAudiences > 0 || blockedBodies > 0 || extra)
			{
				Blocker = blockedAudiences + " civic audience lease(s) and "
					+ blockedBodies + " transient-body lease(s) remain without an exact owner; "
					+ "resolve their named source lanes before preparing removal.";
				if (extra) Blocker += " A retirement allowance is malformed, duplicate, partial, foreign, or extra.";
			}
			return true;
		}

		private static bool ConsumeExactAllowance(
			IList<KingdomExperienceRetirementLeaseAllowance> Values, bool[] Used,
			KingdomExperienceAudienceReceipt Row)
		{
			int found = -1;
			for (int i = 0; i < Values.Count; i++)
				if (ValidPolityAllowance(Values[i]) && Exact(Values[i].Audience, Row))
				{
					if (found >= 0 || Used[i]) return false; found = i;
				}
			if (found < 0) return false; Used[found] = true; return true;
		}

		private static bool ConsumeExactAllowance(
			IList<KingdomExperienceRetirementLeaseAllowance> Values, bool[] Used,
			KingdomExperienceBodyReservation Row)
		{
			int found = -1;
			for (int i = 0; i < Values.Count; i++)
				if (ValidPolityAllowance(Values[i]) && Exact(Values[i].Bodies, Row))
				{
					if (found >= 0 || Used[i]) return false; found = i;
				}
			if (found < 0) return false; Used[found] = true; return true;
		}

		private static bool ValidPolityAllowance(
			KingdomExperienceRetirementLeaseAllowance Value)
		{
			bool audience = Value?.Audience != null, bodies = Value?.Bodies != null;
			return audience != bodies && (audience ? Value.Audience.Lane : Value.Bodies.Lane) ==
				KingdomExperienceLane.PolityCohort;
		}

		private static bool Exact(KingdomExperienceAudienceReceipt A,
			KingdomExperienceAudienceReceipt B)
		{
			return A != null && B != null && A.ReservationId == B.ReservationId &&
				A.RealmId == B.RealmId && A.SettlementId == B.SettlementId &&
				A.SourceId == B.SourceId && A.Lane == B.Lane && A.OptionKind == B.OptionKind &&
				A.CauseTick == B.CauseTick && A.ReservedTick == B.ReservedTick &&
				A.EnableEpoch == B.EnableEpoch;
		}

		private static bool Exact(KingdomExperienceBodyReservation A,
			KingdomExperienceBodyReservation B)
		{
			return A != null && B != null && A.ReservationId == B.ReservationId &&
				A.RealmId == B.RealmId && A.SettlementId == B.SettlementId &&
				A.SourceId == B.SourceId && A.Lane == B.Lane && A.OptionKind == B.OptionKind &&
				A.CauseTick == B.CauseTick && A.ReservedTick == B.ReservedTick &&
				A.EnableEpoch == B.EnableEpoch && A.BodyCount == B.BodyCount;
		}

		/// <summary>Exact bodyless semantic retirement. It owns no projection or source outcome.</summary>
		public static bool TryRetireCivicVoices(KingdomExperienceLedger Ledger,
			string RealmId, long ExpectedRevision, out string Failure)
		{
			Failure = null;
			if (!TryValidate(Ledger, out Failure)
				|| !string.Equals(Ledger.RealmId, RealmId, System.StringComparison.Ordinal))
				return Fail(Failure ?? "civic voice retirement belongs to another realm", out Failure);
			if (Ledger.Voices.Count == 0) return true;
			if (ExpectedRevision != Ledger.Revision || Ledger.Revision == long.MaxValue)
				return Fail("civic voice retirement revision is unavailable", out Failure);
			KingdomExperienceLedger next = Clone(Ledger);
			next.Voices.Clear(); next.Revision++;
			if (!TryValidate(next, out Failure)) return false;
			Ledger.CopyFrom(next); return true;
		}

		public static bool TryRecoverRetirementBodies(KingdomExperienceLedger Ledger,
			long ExpectedRevision, KingdomExperienceBodyReservation Request,
			int ProtectedFoundationBodies, out KingdomExperienceCapacityFault Fault,
			out string Failure)
		{
			return TryRecoverBodiesCore(Ledger, ExpectedRevision, Request,
				ProtectedFoundationBodies, true, out Fault, out Failure);
		}

		public static bool TryRecoverRetirementPresentation(KingdomExperienceLedger Ledger,
			long ExpectedRevision, KingdomExperienceAudienceReceipt Audience,
			KingdomExperienceBodyReservation Bodies, int ProtectedFoundationBodies,
			out KingdomExperienceCapacityFault Fault, out string Failure)
		{
			return TryRecoverPresentationCore(Ledger, ExpectedRevision, Audience, Bodies,
				ProtectedFoundationBodies, true, out Fault, out Failure);
		}

		public static bool TryRecoverDurableBodies(KingdomExperienceLedger Ledger,
			long ExpectedRevision, KingdomExperienceBodyReservation Request,
			int ProtectedFoundationBodies, out KingdomExperienceCapacityFault Fault,
			out string Failure)
		{
			return TryRecoverBodiesCore(Ledger, ExpectedRevision, Request,
				ProtectedFoundationBodies, false, out Fault, out Failure);
		}

		public static bool TryRecoverDurablePresentation(KingdomExperienceLedger Ledger,
			long ExpectedRevision, KingdomExperienceAudienceReceipt Audience,
			KingdomExperienceBodyReservation Bodies, int ProtectedFoundationBodies,
			out KingdomExperienceCapacityFault Fault, out string Failure)
		{
			return TryRecoverPresentationCore(Ledger, ExpectedRevision, Audience, Bodies,
				ProtectedFoundationBodies, false, out Fault, out Failure);
		}
	}
}
