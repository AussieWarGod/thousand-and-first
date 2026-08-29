using System;
using System.Collections.Generic;
using System.Globalization;

namespace ThousandAndFirst
{
	/// <summary>Assigns an ambient endpoint row to current or one exact active external polity.</summary>
	public static class KingdomPolityRivalTrafficRules
	{
		public static bool TryAssign(KingdomPolityLedger Ledger, KingdomPolityDueWork Due,
			out KingdomPolityTrafficAssignment Assignment, out string Failure)
		{
			Assignment = null; Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) || !ValidDue(Due))
				return Fail(Failure ?? "ambient polity due row is invalid", out Failure);
			KingdomPolityRecord current = FindCurrent(Ledger);
			if (current == null) return Fail("ambient traffic has no active current polity",
				out Failure);
			Assignment = Current(Due, current.PolityId);
			KingdomPolityRecord external = FindExternal(Ledger);
			KingdomPolityRelation relation = external == null ? null :
				FindRelation(Ledger, external.PolityId, current.PolityId);
			if (external == null || relation == null || !HasFactionProjection(Ledger, external) ||
				!ExternalWindow(Due) || !EligibleExternal(Due.Purpose, relation.Band)) return true;
			Assignment = External(Due, external.PolityId, relation);
			return ValidAssignment(Assignment) || Fail(
				"ambient external traffic assignment is invalid", out Failure);
		}

		public static bool ValidAssignment(KingdomPolityTrafficAssignment Value)
		{
			if (Value == null || !ValidDue(Value.Work) ||
				!KingdomPolityRules.SemanticId(Value.PolityId)) return false;
			if (!Value.External) return string.IsNullOrEmpty(Value.RelationId) &&
				string.IsNullOrEmpty(Value.CauseDigest);
			return KingdomPolityRules.TypedId(Value.RelationId, "taf:relation:") &&
				KingdomPolityRules.Digest(Value.CauseDigest) &&
				Value.Work.SourceRef.StartsWith("taf:event:polity-due:v1:",
					StringComparison.Ordinal);
		}

		private static KingdomPolityTrafficAssignment Current(KingdomPolityDueWork Due,
			string PolityId)
		{
			return new KingdomPolityTrafficAssignment
			{
				Work = Copy(Due), PolityId = PolityId
			};
		}

		private static KingdomPolityTrafficAssignment External(KingdomPolityDueWork Due,
			string PolityId, KingdomPolityRelation Relation)
		{
			List<string> evidence = new List<string>(Relation.SourceRefs);
			string digest = KingdomPolityRules.ActivationDigest(
				"polity-external-traffic-cause-v1", Due.SourceRef, PolityId,
				Relation.RelationId, ((byte)Relation.Band).ToString(CultureInfo.InvariantCulture),
				Relation.ChangedTick.ToString(CultureInfo.InvariantCulture),
				KingdomPolityRules.ActivationDigest("polity-relation-source-set-v1", evidence));
			KingdomPolityDueWork work = Copy(Due);
			string ordinal = Due.WindowOrdinal.ToString(CultureInfo.InvariantCulture);
			string purpose = ((byte)Due.Purpose).ToString(CultureInfo.InvariantCulture);
			work.CohortId = KingdomPolityRules.ActivationId("taf:cohort:polity-due:v1:",
				"polity-external-traffic-cohort-v1", PolityId, Due.SettlementId,
				ordinal, purpose, digest);
			work.EventStreamId = KingdomPolityRules.ActivationId(
				"taf:stream:polity-due:v1:", "polity-external-traffic-stream-v1",
				PolityId, Due.SettlementId, ordinal);
			work.SourceRef = KingdomPolityRules.ActivationId("taf:event:polity-due:v1:",
				"polity-external-traffic-event-v1", PolityId, Due.SettlementId,
				ordinal, purpose, digest);
			return new KingdomPolityTrafficAssignment
			{
				Work = work, PolityId = PolityId, RelationId = Relation.RelationId,
				External = true, CauseDigest = digest
			};
		}

		private static bool EligibleExternal(KingdomPolityCohortPurpose Purpose,
			KingdomPolityRelationBand Band)
		{
			if (Purpose == KingdomPolityCohortPurpose.Guard) return false;
			if (Purpose == KingdomPolityCohortPurpose.Patrol)
				return Band >= KingdomPolityRelationBand.Contact;
			if (Purpose == KingdomPolityCohortPurpose.Courier)
				return Band != KingdomPolityRelationBand.Hostile;
			if (Purpose == KingdomPolityCohortPurpose.Trader)
				return Band == KingdomPolityRelationBand.Contact ||
					Band == KingdomPolityRelationBand.Neutral ||
					Band == KingdomPolityRelationBand.Pact ||
					Band == KingdomPolityRelationBand.Truce;
			return Purpose == KingdomPolityCohortPurpose.Migrant &&
				Band == KingdomPolityRelationBand.Pact;
		}

		private static bool ExternalWindow(KingdomPolityDueWork Due)
		{
			return (((Due.WindowOrdinal + (ulong)Due.EndpointOrdinal) & 1UL) == 1UL);
		}

		private static KingdomPolityRecord FindCurrent(KingdomPolityLedger Ledger)
		{
			for (int i = 0; i < Ledger.Polities.Count; i++)
				if (Ledger.Polities[i].Source == KingdomPolitySource.CurrentRealm &&
					Ledger.Polities[i].Lifecycle == KingdomPolityLifecycle.Active)
					return Ledger.Polities[i];
			return null;
		}

		private static KingdomPolityRecord FindExternal(KingdomPolityLedger Ledger)
		{
			for (int i = 0; i < Ledger.Polities.Count; i++)
				if ((Ledger.Polities[i].Source == KingdomPolitySource.ImportedLegacy ||
					 Ledger.Polities[i].Source == KingdomPolitySource.AuthoredRival) &&
					Ledger.Polities[i].Lifecycle == KingdomPolityLifecycle.Active)
					return Ledger.Polities[i];
			return null;
		}

		private static KingdomPolityRelation FindRelation(KingdomPolityLedger Ledger,
			string External, string Current)
		{
			KingdomPolityRelation reverse = null;
			for (int i = 0; i < Ledger.Relations.Count; i++)
			{
				KingdomPolityRelation relation = Ledger.Relations[i];
				if (relation.FromPolityId == External && relation.ToPolityId == Current)
					return relation;
				if (relation.FromPolityId == Current && relation.ToPolityId == External)
					reverse = relation;
			}
			return reverse;
		}

		private static bool HasFactionProjection(KingdomPolityLedger Ledger,
			KingdomPolityRecord Polity)
		{
			for (int i = 0; i < Ledger.Projections.Count; i++)
			{
				KingdomPolityProjectionReceipt row = Ledger.Projections[i];
				if (row.Kind == KingdomPolityProjectionKind.Faction &&
					row.SourceRef == Polity.PolityId &&
					row.Phase == KingdomPolityProjectionPhase.Committed &&
					KingdomPolityAuthority.Contains(row.ObjectIds, Polity.ProjectedFactionId)) return true;
			}
			return false;
		}

		private static bool ValidDue(KingdomPolityDueWork Due)
		{
			return Due != null && Due.EndpointOrdinal >= 0 &&
				Due.EndpointOrdinal < KingdomPolityDispatchRules.MaximumEndpoints &&
				KingdomPolityRules.TypedId(Due.CohortId, "taf:cohort:") &&
				KingdomPolityRules.TypedId(Due.EventStreamId, "taf:stream:") &&
				KingdomPolityRules.SemanticId(Due.SourceRef) &&
				KingdomPolityRules.TypedId(Due.SettlementId, "taf:settlement:v1:") &&
				Due.Purpose >= KingdomPolityCohortPurpose.Guard &&
				Due.Purpose <= KingdomPolityCohortPurpose.Migrant &&
				Due.Purpose != KingdomPolityCohortPurpose.Envoy &&
				Due.Purpose != KingdomPolityCohortPurpose.Warband &&
				Due.CauseTick >= 0L && Due.StayUntilTick >= Due.CauseTick &&
				Due.MemberCount >= 1 && Due.MemberCount <=
					KingdomPolityCohortRules.MaximumVisibleMembers &&
				Due.EndpointVerb == KingdomPolityDispatchRules.EndpointVerb(Due.Purpose);
		}

		private static KingdomPolityDueWork Copy(KingdomPolityDueWork Source)
		{
			return new KingdomPolityDueWork
			{
				EndpointOrdinal = Source.EndpointOrdinal, CohortId = Source.CohortId,
				EventStreamId = Source.EventStreamId, SourceRef = Source.SourceRef,
				SettlementId = Source.SettlementId, Purpose = Source.Purpose,
				WindowOrdinal = Source.WindowOrdinal, CauseTick = Source.CauseTick,
				StayUntilTick = Source.StayUntilTick, MemberCount = Source.MemberCount,
				EndpointVerb = Source.EndpointVerb
			};
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
