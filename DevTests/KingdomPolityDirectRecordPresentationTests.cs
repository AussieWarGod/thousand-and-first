#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomPolityDirectRecordPresentationTests
	{
		[TestCase(KingdomPolityCohortPurpose.Guard, "gate watch")]
		[TestCase(KingdomPolityCohortPurpose.Patrol, "road patrol")]
		[TestCase(KingdomPolityCohortPurpose.Courier, "courier's word")]
		[TestCase(KingdomPolityCohortPurpose.Trader, "travelling trader")]
		[TestCase(KingdomPolityCohortPurpose.Migrant, "migrant's petition")]
		public void DetailedViewsTellTheSemanticLaneWithoutLeakingProof(
			KingdomPolityCohortPurpose Purpose, string Expected)
		{
			KingdomPolityDirectRecord record = Record(
				KingdomPolityDispatchRules.DirectPrefix, Purpose);
			record.EndpointVerb = "SECRET-TOPOLOGY";

			Assert.IsTrue(KingdomPolityDirectRecordPresentationRules.TryBuild(
				record, "1st of Nivvun Ut, 1001 AR", out KingdomPolityDirectRecordView view));
			StringAssert.Contains(Expected, view.Label);
			StringAssert.Contains("frozen matter", view.Body);
			StringAssert.DoesNotContain("SECRET-TOPOLOGY", view.Label + view.Title + view.Body);
			StringAssert.DoesNotContain(record.SourceRef, view.Label + view.Title + view.Body);
			Assert.IsFalse(view.WasAcknowledged);
		}

		[Test]
		public void AggregateViewTellsBoundedSupersessionWithoutInventingVisits()
		{
			KingdomPolityDirectRecord record = Record(
				KingdomPolityDispatchRules.AggregatePrefix,
				KingdomPolityCohortPurpose.Courier);
			record.WindowOrdinal = 7UL;
			record.AcknowledgedTick = 1201L;

			Assert.IsTrue(KingdomPolityDirectRecordPresentationRules.TryBuild(
				record, "2nd of Nivvun Ut, 1001 AR", out KingdomPolityDirectRecordView view));
			StringAssert.Contains("older traffic (7)", view.Label);
			StringAssert.Contains("7 older traffic notices", view.Body);
			StringAssert.Contains("a courier's word", view.Body);
			Assert.IsTrue(view.WasAcknowledged);
		}

		[Test]
		public void InternalIntentAndUnknownPurposeCannotBecomePlayerProse()
		{
			Assert.IsFalse(KingdomPolityDirectRecordPresentationRules.TryBuild(
				Record(KingdomPolityDispatchRules.IntentPrefix,
					KingdomPolityCohortPurpose.Guard), "today", out _));
			Assert.IsFalse(KingdomPolityDirectRecordPresentationRules.TryBuild(
				Record(KingdomPolityDispatchRules.DirectPrefix,
					(KingdomPolityCohortPurpose)255), "today", out _));
		}

		[Test]
		public void CharterEndpointRequiresExactLoadedSettlementAndExplicitAcknowledgement()
		{
			string source = TestMain.ReadRepositoryText(Path.Combine(
				"Core", "KingdomCharterPart.PolityTraffic.cs"));
			StringAssert.Contains("System.OwnedZone(zone.ZoneID)", source);
			StringAssert.Contains("System.SettlementIdForOwnedZone(zone.ZoneID)", source);
			StringAssert.Contains("System.TryFindSettlement(settlementId", source);
			StringAssert.Contains("IncludeAcknowledged: true", source);
			StringAssert.Contains("Popup.ShowYesNo(\"Mark this traffic leaf as read?\")", source);
			StringAssert.Contains("record.RecordId, tick", source);
			StringAssert.Contains("KingdomGovernanceScope.Commit(\"polity traffic record acknowledgement\")", source);
			StringAssert.DoesNotContain("EndpointVerb", source);
			StringAssert.DoesNotContain("SourceRef", source);
		}

		private static KingdomPolityDirectRecord Record(string Prefix,
			KingdomPolityCohortPurpose Purpose)
		{
			KingdomPolityDirectRecord result = new KingdomPolityDirectRecord
			{
				RecordId = Prefix + "record", SourceRef = "taf:cohort:direct-presentation",
				Purpose = Purpose, CauseTick = 1200L
			};
			if (Prefix == KingdomPolityDispatchRules.DirectPrefix &&
				KingdomPolityDispatchRules.AmbientPurpose(Purpose))
				result.AmbientTransaction = Transaction(Purpose, result.SourceRef);
			return result;
		}

		private static KingdomPolityAmbientTransaction Transaction(
			KingdomPolityCohortPurpose Purpose, string CohortId)
		{
			KingdomPolityAmbientTransaction t = new KingdomPolityAmbientTransaction
			{
				Purpose = Purpose, SourcePolityId = "taf:realm:direct-test",
				SourceSettlementId = "taf:settlement:v1:source", SourceSettlementName = "Alpha",
				SourceZoneId = "JoppaWorld.1.1.1.1.10",
				DestinationSettlementId = Purpose == KingdomPolityCohortPurpose.Guard ||
					Purpose == KingdomPolityCohortPurpose.Patrol
						? "taf:settlement:v1:source" : "taf:settlement:v1:destination",
				DestinationSettlementName = Purpose == KingdomPolityCohortPurpose.Guard ||
					Purpose == KingdomPolityCohortPurpose.Patrol ? "Alpha" : "Beta",
				DestinationZoneId =
					Purpose == KingdomPolityCohortPurpose.Guard ||
					Purpose == KingdomPolityCohortPurpose.Patrol ? "JoppaWorld.1.1.1.1.10" :
						"JoppaWorld.1.1.1.2.10",
				LocalLocusRef = Purpose == KingdomPolityCohortPurpose.Guard ||
					Purpose == KingdomPolityCohortPurpose.Patrol ? "taf:locus:direct-test" : null,
				SafeDetail = Purpose == KingdomPolityCohortPurpose.Trader
					? "No exact physical stock accompanies this visit; no trade is offered."
					: Purpose == KingdomPolityCohortPurpose.Migrant
					? "A petitioner asks to enter this settlement; no resident is admitted by the visit."
					: "A bounded player-safe detail.", PreparedTick = 1200L
			};
			if (Purpose == KingdomPolityCohortPurpose.Guard)
				t.FactRefs.Add("taf:fact:witnessed:watch");
			else if (Purpose == KingdomPolityCohortPurpose.Patrol)
				t.FactRefs.Add("taf:fact:route-condition:site");
			else if (Purpose == KingdomPolityCohortPurpose.Migrant)
				t.FactRefs.AddRange(new[] { "taf:fact:capacity", "taf:fact:petition",
					"taf:fact:population" });
			else
			{
				t.NewsRef = "taf:fact:news";
				t.FactRefs.AddRange(new[] { "taf:fact:cause", t.NewsRef });
			}
			t.FactRefs.Sort(StringComparer.Ordinal);
			t.FrozenDigest = KingdomPolityAmbientTransactionRules.FrozenDigest(t);
			t.TransactionId = KingdomPolityRules.ActivationId("taf:ambient-transaction:v1:",
				"polity-ambient-transaction-v1", CohortId, t.FrozenDigest);
			return t;
		}
	}
}
#endif
