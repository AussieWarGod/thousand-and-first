#if TAF_TESTS
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomPurposeEffectEvidenceTests
	{
		private const string A = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
		private const string B = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

		[Test]
		public void AttemptBindsDistinctCanonicalBeforeAndAfterRosters()
		{
			Assert.IsTrue(KingdomPurposePortfolioRules.TryEffectReceipt("pair:one", 7,
				"operation:one", KingdomPurposeKind.Harvest, out string receipt));
			Assert.IsTrue(KingdomPurposePortfolioRules.TryEffectAttempt(receipt, 3,
				KingdomPurposeEffectCallbackKind.HarvestSeed, "object:one", 2, 8, 1,
				A, B, out string encoded));
			Assert.IsTrue(KingdomPurposePortfolioRules.TryReadEffectAttempt(encoded, receipt,
				out KingdomPurposeEffectAttempt attempt));
			Assert.AreEqual(A, attempt.BeforeRosterDigest);
			Assert.AreEqual(B, attempt.AfterRosterDigest);
			Assert.AreEqual(encoded, KingdomPurposePortfolioRules.EncodeEffectAttempt(attempt));

			Assert.IsFalse(KingdomPurposePortfolioRules.TryEffectAttempt(receipt, 3,
				KingdomPurposeEffectCallbackKind.HarvestSeed, "object:one", 2, 8, 1,
				A, A, out _));
			Assert.IsFalse(KingdomPurposePortfolioRules.TryEffectAttempt(receipt, 3,
				KingdomPurposeEffectCallbackKind.HarvestSeed, "object:one", 2, 8, 1,
				A.Substring(1), B, out _));
			Assert.IsFalse(KingdomPurposePortfolioRules.TryEffectAttempt(receipt, 3,
				KingdomPurposeEffectCallbackKind.HarvestSeed, "object:one", 2, 8, 1,
				A.ToUpperInvariant(), B, out _));
		}

		[Test]
		public void EveryCallbackKindRoundTripsAndForeignScopeNeverReads()
		{
			Assert.IsTrue(KingdomPurposePortfolioRules.TryEffectReceipt("pair", 9,
				"operation", KingdomPurposeKind.Deep, out string receipt));
			Assert.IsTrue(KingdomPurposePortfolioRules.TryEffectReceipt("pair", 9,
				"other", KingdomPurposeKind.Deep, out string foreign));
			for (int raw = (int)KingdomPurposeEffectCallbackKind.RefineRaw;
				raw <= (int)KingdomPurposeEffectCallbackKind.HarvestStaple; raw++)
			{
				Assert.IsTrue(KingdomPurposePortfolioRules.TryEffectAttempt(receipt, raw - 1,
					(KingdomPurposeEffectCallbackKind)raw, "object-" + raw, raw, raw + 2,
					raw - 1, A, B, out string encoded));
				Assert.IsTrue(KingdomPurposePortfolioRules.TryReadEffectAttempt(encoded,
					receipt, out KingdomPurposeEffectAttempt copy));
				Assert.AreEqual((KingdomPurposeEffectCallbackKind)raw, copy.Callback);
				Assert.IsFalse(KingdomPurposePortfolioRules.TryReadEffectAttempt(encoded,
					foreign, out _));
				Assert.IsFalse(KingdomPurposePortfolioRules.TryReadEffectAttempt(encoded + "x",
					receipt, out _));
			}
		}

		[Test]
		public void FramedEffectReceiptsAreInjectiveForDelimiterBearingIdentities()
		{
			Assert.IsTrue(KingdomPurposePortfolioRules.TryEffectReceipt("a:b", 12, "c",
				KingdomPurposeKind.Forge, out string first));
			Assert.IsTrue(KingdomPurposePortfolioRules.TryEffectReceipt("a", 12, "b:c",
				KingdomPurposeKind.Forge, out string second));
			Assert.AreNotEqual(first, second);
			Assert.IsTrue(KingdomPurposePortfolioRules.TryEffectProductReceipt(first,
				KingdomPurposeEffectProductRole.Refined, out string refined));
			Assert.IsTrue(KingdomPurposePortfolioRules.TryEffectProductReceipt(first,
				KingdomPurposeEffectProductRole.Seed, out string seed));
			Assert.AreNotEqual(refined, seed);
		}

		[Test]
		public void ProductHighWaterRecordRoundTripsEveryLegalPartialBatch()
		{
			Assert.IsTrue(KingdomPurposePortfolioRules.TryEffectReceipt("pair", 3, "op",
				KingdomPurposeKind.Harvest, out string receipt));
			for (int refined = 0; refined <= 1; refined++)
			for (int seed = 0; seed <= 1; seed++)
			for (int staple = 0; staple <= 6; staple++)
			{
				KingdomPurposeEffectProductRecord record = new KingdomPurposeEffectProductRecord
					{ Refined = refined, Seed = seed, Staple = staple };
				Assert.IsTrue(KingdomPurposePortfolioRules.TryEffectProductRecord(receipt,
					record, out string encoded));
				Assert.IsTrue(KingdomPurposePortfolioRules.TryReadEffectProductRecord(encoded,
					receipt, out KingdomPurposeEffectProductRecord copy));
				Assert.AreEqual(refined, copy.Refined);
				Assert.AreEqual(seed, copy.Seed);
				Assert.AreEqual(staple, copy.Staple);
			}
			Assert.IsFalse(KingdomPurposePortfolioRules.TryEffectProductRecord(receipt,
				new KingdomPurposeEffectProductRecord { Refined = 2 }, out _));
			Assert.IsFalse(KingdomPurposePortfolioRules.TryEffectProductRecord(receipt,
				new KingdomPurposeEffectProductRecord { Seed = 2 }, out _));
			Assert.IsFalse(KingdomPurposePortfolioRules.TryEffectProductRecord(receipt,
				new KingdomPurposeEffectProductRecord { Staple = 7 }, out _));
		}

		[Test]
		public void MarkerPresenceAndOwnershipStaySeparate()
		{
			Assert.IsFalse(KingdomPurposePortfolioRules.EffectMarkerIsPresent(false, false));
			Assert.IsTrue(KingdomPurposePortfolioRules.EffectMarkerIsPresent(true, false));
			Assert.IsTrue(KingdomPurposePortfolioRules.EffectMarkerIsPresent(false, true));
			Assert.IsFalse(KingdomPurposePortfolioRules.EffectMarkerIsOurs(
				"receipt", 4, true, 5, true, "receipt"));
			Assert.IsFalse(KingdomPurposePortfolioRules.EffectMarkerIsOurs(
				"receipt", 4, true, 4, true, "foreign"));
			Assert.IsTrue(KingdomPurposePortfolioRules.EffectMarkerIsOurs(
				"receipt", 4, true, 4, true, "receipt"));
		}

		[Test]
		public void EveryPhysicalCheckpointProtectsItsCarrierFromOrdinaryUse()
		{
			AssertProtected(new KingdomPurposeCargoEvidence { EffectAttempt = true });
			AssertProtected(new KingdomPurposeCargoEvidence { EffectReady = true });
			AssertProtected(new KingdomPurposeCargoEvidence { EffectOffer = true });
			AssertProtected(new KingdomPurposeCargoEvidence { EffectCount = true });
			AssertProtected(new KingdomPurposeCargoEvidence { EffectFault = true });
			AssertProtected(new KingdomPurposeCargoEvidence { EffectMark = true });
			AssertProtected(new KingdomPurposeCargoEvidence { EffectIndex = true });
			Assert.IsFalse(KingdomPurposePortfolioRules.PurposeCargoIsProtected(
				new KingdomPurposeCargoEvidence()));
		}

		private static void AssertProtected(KingdomPurposeCargoEvidence evidence)
		{
			Assert.IsTrue(KingdomPurposePortfolioRules.PurposeCargoIsProtected(evidence));
		}
	}
}
#endif
