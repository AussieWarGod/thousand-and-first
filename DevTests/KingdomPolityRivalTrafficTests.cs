using System;
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.DevTests
{
	[TestFixture]
	public sealed class KingdomPolityRivalTrafficTests
	{
		[Test]
		public void ExternalTrafficFailsClosedWithoutAnExactSourceSettlement()
		{
			KingdomPolityLedger ledger = KingdomPolityTestData.Full();
			EnableExternalFaction(ledger);
			byte[] before = KingdomPolityCodec.EncodeEnvelope(ledger);
			KingdomPolityDueWork due = Due(KingdomPolityCohortPurpose.Trader, 1UL, 0,
				KingdomPolityTestData.Settlement, 2);
			ClassicAssert.IsTrue(KingdomPolityRivalTrafficRules.TryAssign(ledger, due,
				out KingdomPolityTrafficAssignment first, out string failure), failure);
			ClassicAssert.IsFalse(first.External); ClassicAssert.AreEqual(KingdomPolityTestData.Realm,
				first.PolityId);
			ClassicAssert.IsNull(first.RelationId);
			ClassicAssert.AreEqual(due.CohortId, first.Work.CohortId);
			StringAssert.StartsWith("taf:cohort:polity-due:v1:", first.Work.CohortId);
			StringAssert.StartsWith("taf:stream:polity-due:v1:", first.Work.EventStreamId);
			StringAssert.StartsWith("taf:event:polity-due:v1:", first.Work.SourceRef);
			ClassicAssert.IsTrue(KingdomPolityRivalTrafficRules.ValidAssignment(first));
			ClassicAssert.IsTrue(KingdomPolityRivalTrafficRules.TryAssign(ledger, due,
				out KingdomPolityTrafficAssignment retry, out failure), failure);
			ClassicAssert.AreEqual(first.Work.CohortId, retry.Work.CohortId);
			ClassicAssert.IsNull(first.CauseDigest); ClassicAssert.IsNull(retry.CauseDigest);
			CollectionAssert.AreEqual(before, KingdomPolityCodec.EncodeEnvelope(ledger));
			first.External = true;
			first.PolityId = KingdomPolityTestData.Rival;
			first.RelationId = "taf:relation:rival-current";
			first.CauseDigest = KingdomPolityRules.ActivationDigest(
				"forged-external-traffic", first.Work.SourceRef);
			ClassicAssert.IsFalse(KingdomPolityRivalTrafficRules.ValidAssignment(first));
		}

		[Test]
		public void GuardAndIneligibleRelationsRemainCurrentWithoutFallbackWar()
		{
			KingdomPolityLedger ledger = KingdomPolityTestData.Full();
			EnableExternalFaction(ledger);
			AssertCurrent(ledger, Due(KingdomPolityCohortPurpose.Guard, 1UL, 0,
				KingdomPolityTestData.Settlement, 2));
			AssertCurrent(ledger, Due(KingdomPolityCohortPurpose.Migrant, 1UL, 0,
				KingdomPolityTestData.Settlement, 2));
			KingdomPolityRelation relation = Relation(ledger, "taf:relation:rival-current");
			relation.Band = KingdomPolityRelationBand.Hostile;
			ClassicAssert.IsTrue(KingdomPolityRules.TryValidate(ledger, out string failure), failure);
			AssertCurrent(ledger, Due(KingdomPolityCohortPurpose.Trader, 1UL, 0,
				KingdomPolityTestData.Settlement, 2));
			KingdomPolityDueWork patrol = Due(KingdomPolityCohortPurpose.Patrol, 1UL, 0,
				KingdomPolityTestData.Settlement, 2);
			ClassicAssert.IsTrue(KingdomPolityRivalTrafficRules.TryAssign(ledger, patrol,
				out KingdomPolityTrafficAssignment assignment, out failure), failure);
			ClassicAssert.IsFalse(assignment.External);
			ClassicAssert.AreEqual(KingdomPolityCohortPurpose.Patrol, assignment.Work.Purpose);
		}

		[Test]
		public void MissingOwnedFactionProjectionAndEvenWindowsFailSafeToCurrent()
		{
			KingdomPolityLedger ledger = KingdomPolityTestData.Full();
			EnableExternalFaction(ledger);
			AssertCurrent(ledger, Due(KingdomPolityCohortPurpose.Trader, 2UL, 0,
				KingdomPolityTestData.Settlement, 2));
			for (int i = ledger.Projections.Count - 1; i >= 0; i--)
				if (ledger.Projections[i].SourceRef == KingdomPolityTestData.Rival &&
					ledger.Projections[i].Kind == KingdomPolityProjectionKind.Faction)
					ledger.Projections.RemoveAt(i);
			ClassicAssert.IsTrue(KingdomPolityRules.TryValidate(ledger, out string failure), failure);
			AssertCurrent(ledger, Due(KingdomPolityCohortPurpose.Trader, 1UL, 0,
				KingdomPolityTestData.Settlement, 2));
		}

		[Test]
		public void ThreeEndpointsResolveIndependentlyWithoutRemoteStateOrIdentityReuse()
		{
			KingdomPolityLedger ledger = KingdomPolityTestData.Full();
			EnableExternalFaction(ledger);
			string[] settlements =
			{
				KingdomPolityTestData.Settlement,
				"taf:settlement:v1:dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
				"taf:settlement:v1:eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee"
			};
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < settlements.Length; i++)
			{
				KingdomPolityDueWork due = Due(KingdomPolityCohortPurpose.Courier,
					(ulong)(1 + i), i, settlements[i], 3);
				ClassicAssert.IsTrue(KingdomPolityRivalTrafficRules.TryAssign(ledger, due,
					out KingdomPolityTrafficAssignment assignment, out string failure), failure);
				ClassicAssert.IsFalse(assignment.External);
				ClassicAssert.AreEqual(settlements[i], assignment.Work.SettlementId);
				ClassicAssert.IsTrue(ids.Add(assignment.Work.CohortId));
			}
		}

		private static void AssertCurrent(KingdomPolityLedger Ledger,
			KingdomPolityDueWork Due)
		{
			ClassicAssert.IsTrue(KingdomPolityRivalTrafficRules.TryAssign(Ledger, Due,
				out KingdomPolityTrafficAssignment assignment, out string failure), failure);
			ClassicAssert.IsFalse(assignment.External);
			ClassicAssert.AreEqual(KingdomPolityTestData.Realm, assignment.PolityId);
			ClassicAssert.AreEqual(Due.CohortId, assignment.Work.CohortId);
			ClassicAssert.IsNull(assignment.RelationId); ClassicAssert.IsNull(assignment.CauseDigest);
		}

		private static KingdomPolityDueWork Due(KingdomPolityCohortPurpose Purpose,
			ulong Window, int EndpointOrdinal, string SettlementId, int EndpointCount)
		{
			KingdomPolityEndpointFacts endpoint = new KingdomPolityEndpointFacts
			{
				SettlementId = SettlementId, IsSeat = EndpointOrdinal == 0, Population = 8,
				Stage = 2, ShopTier = 2, KnownStorageSpace = 2,
				GuardCauseRef = "taf:fact:watch:test",
				PatrolCauseRef = "taf:fact:patrol:test",
				CourierCauseRef = "taf:fact:courier:test",
				TraderCauseRef = "taf:fact:market:test",
				MigrantCauseRef = "taf:fact:room:test"
			};
			long cause = (long)Window * KingdomPolityDispatchRules.PeriodTicks;
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryCreateForPurpose(
				KingdomPolityTestData.Realm, endpoint, EndpointCount, Window, cause, Purpose,
				out KingdomPolityDueWork work, out string failure), failure);
			work.EndpointOrdinal = EndpointOrdinal; return work;
		}

		private static KingdomPolityRelation Relation(KingdomPolityLedger Ledger, string Id)
		{
			for (int i = 0; i < Ledger.Relations.Count; i++)
				if (Ledger.Relations[i].RelationId == Id) return Ledger.Relations[i];
			return null;
		}

		private static void EnableExternalFaction(KingdomPolityLedger Ledger)
		{
			for (int i = 0; i < Ledger.Projections.Count; i++)
				if (Ledger.Projections[i].SourceRef == KingdomPolityTestData.Rival &&
					Ledger.Projections[i].Kind == KingdomPolityProjectionKind.Faction)
				{
					Ledger.Projections[i].Phase = KingdomPolityProjectionPhase.Committed;
					Ledger.Projections[i].CommittedTick = 31L;
					Ledger.Projections[i].ObjectIds.Add("taf:faction:rival");
				}
			ClassicAssert.IsTrue(KingdomPolityRules.TryValidate(Ledger, out string failure), failure);
		}
	}
}
