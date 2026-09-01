using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.DevTests
{
	[TestFixture]
	public sealed class KingdomPolityEndpointObservationRulesTests
	{
		private const string Settlement =
			"taf:settlement:v1:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

		[Test]
		public void GuardUsesCanonicalPositiveWitnessAndNeverPopulationOrStage()
		{
			Assert.IsTrue(KingdomPolityEndpointObservationRules.TryGuard(
				KingdomPolityTestData.Realm, Settlement,
				new[] { "zone-z", "zone-a", "zone-b" }, new long[] { 90L, 70L, 80L },
				new[] { 9, 3, 0 }, out KingdomPolityEndpointObservation first,
				out string failure), failure);
			Assert.IsNotNull(first);
			StringAssert.StartsWith("taf:fact:witnessed:", first.CauseRef);
			StringAssert.StartsWith("taf:locus:witnessed-watch:v1:", first.LocusRef);
			StringAssert.Contains("3 defense", first.Detail);
			Assert.IsTrue(KingdomPolityEndpointObservationRules.TryGuard(
				KingdomPolityTestData.Realm, Settlement,
				new[] { "zone-b" }, new long[] { 80L }, new[] { 0 },
				out KingdomPolityEndpointObservation absent, out failure), failure);
			Assert.IsNull(absent);
		}

		[Test]
		public void PatrolUsesExactCanonicalPersistedWorkCondition()
		{
			Assert.IsTrue(KingdomPolityEndpointObservationRules.TryCondition(
				KingdomPolityTestData.Realm, Settlement,
				new[] { "zone-z", "zone-a" }, new long[] { 100L, 80L },
				new[] { 4, 9 }, new[] { "zone-z", "zone-a" },
				new[] { "cistern", "watchhouse" }, new[] { 45, 100 },
				new long[] { 90L, 70L }, out KingdomPolityEndpointObservation first,
				out string failure), failure);
			Assert.IsNotNull(first);
			StringAssert.StartsWith("taf:fact:route-condition:", first.CauseRef);
			StringAssert.StartsWith("taf:locus:site-condition:v1:", first.LocusRef);
			StringAssert.Contains("sound at 100", first.Detail);
			Assert.IsTrue(KingdomPolityEndpointObservationRules.TryCondition(
				KingdomPolityTestData.Realm, Settlement,
				new[] { "zone-z", "zone-a" }, new long[] { 100L, 80L },
				new[] { 9, 4 }, new[] { "zone-a", "zone-z" },
				new[] { "watchhouse", "cistern" }, new[] { 100, 45 },
				new long[] { 70L, 90L }, out KingdomPolityEndpointObservation reordered,
				out failure), failure);
			Assert.AreEqual(first.CauseRef, reordered.CauseRef);
			Assert.AreEqual(first.LocusRef, reordered.LocusRef);
		}

		[Test]
		public void UnwitnessedAndMalformedRowsFailClosedWithoutInventingCause()
		{
			Assert.IsTrue(KingdomPolityEndpointObservationRules.TryCondition(
				KingdomPolityTestData.Realm, Settlement, new[] { "zone-a" }, new long[] { 0L },
				new[] { 1 }, new[] { "zone-a" }, new[] { "house" }, new[] { 80 },
				new long[] { 0L }, out KingdomPolityEndpointObservation absent,
				out string failure), failure);
			Assert.IsNull(absent);
			Assert.IsFalse(KingdomPolityEndpointObservationRules.TryGuard(
				KingdomPolityTestData.Realm, Settlement, new[] { "zone-a" },
				new long[] { 1L, 2L }, new[] { 1 }, out _, out failure));
			Assert.IsFalse(KingdomPolityEndpointObservationRules.TryCondition(
				KingdomPolityTestData.Realm, Settlement, new[] { "zone-a" }, new long[] { 1L },
				new[] { 1 }, new[] { "foreign-zone" }, new[] { "house" }, new[] { 80 },
				new long[] { 1L }, out _, out failure));
		}

		[Test]
		public void GuardAndPatrolFactsFreezeIntoDistinctAmbientTransactions()
		{
			Assert.IsTrue(KingdomPolityEndpointObservationRules.TryGuard(
				KingdomPolityTestData.Realm, Settlement, new[] { "zone-a" },
				new long[] { 10L }, new[] { 2 }, out KingdomPolityEndpointObservation guard,
				out string failure), failure);
			Assert.IsTrue(KingdomPolityEndpointObservationRules.TryCondition(
				KingdomPolityTestData.Realm, Settlement, new[] { "zone-a" }, new long[] { 10L },
				new[] { 1 }, new[] { "zone-a" }, new[] { "watchhouse" }, new[] { 90 },
				new long[] { 9L }, out KingdomPolityEndpointObservation patrol,
				out failure), failure);
			KingdomPolityEndpointFacts endpoint = new KingdomPolityEndpointFacts
			{
				SettlementId = Settlement, SettlementName = "Beta", ZoneId = "zone-a",
				Population = 3, GuardCauseRef = guard.CauseRef,
				GuardProtectedLocusRef = guard.LocusRef, GuardWitnessDetail = guard.Detail,
				PatrolCauseRef = patrol.CauseRef,
				PatrolConditionLocusRef = patrol.LocusRef,
				PatrolConditionDetail = patrol.Detail
			};
			foreach (KingdomPolityCohortPurpose purpose in new[]
				{ KingdomPolityCohortPurpose.Guard, KingdomPolityCohortPurpose.Patrol })
			{
				KingdomPolityDueWork work = new KingdomPolityDueWork
				{
					CohortId = "taf:cohort:observation-" + purpose,
					EventStreamId = "taf:stream:observation-" + purpose,
					SourceRef = "taf:event:observation-" + purpose,
					SettlementId = Settlement, Purpose = purpose, CauseTick = 10L,
					CauseRef = purpose == KingdomPolityCohortPurpose.Guard
						? guard.CauseRef : patrol.CauseRef
				};
				Assert.IsTrue(KingdomPolityAmbientTransactionRules.TryFreeze(
					KingdomPolityTestData.Realm, KingdomPolityTestData.Realm, work,
					new List<KingdomPolityEndpointFacts> { endpoint },
					out KingdomPolityAmbientTransaction transaction, out failure), failure);
				Assert.AreEqual(purpose, transaction.Purpose);
				Assert.AreEqual(work.CauseRef, transaction.FactRefs[0]);
			}
		}
	}
}
