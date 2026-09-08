#if TAF_TESTS
using NUnit.Framework;
using NUnit.Framework.Legacy;

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
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryEffectReceipt("pair:one", 7,
				"operation:one", KingdomPurposeKind.Harvest, out string receipt));
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryEffectAttempt(receipt, 3,
				KingdomPurposeEffectCallbackKind.HarvestSeed, "object:one", 2, 8, 1,
				A, B, out string encoded));
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryReadEffectAttempt(encoded, receipt,
				out KingdomPurposeEffectAttempt attempt));
			ClassicAssert.AreEqual(A, attempt.BeforeRosterDigest);
			ClassicAssert.AreEqual(B, attempt.AfterRosterDigest);
			ClassicAssert.AreEqual(encoded, KingdomPurposePortfolioRules.EncodeEffectAttempt(attempt));

			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryEffectAttempt(receipt, 3,
				KingdomPurposeEffectCallbackKind.HarvestSeed, "object:one", 2, 8, 1,
				A, A, out _));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryEffectAttempt(receipt, 3,
				KingdomPurposeEffectCallbackKind.HarvestSeed, "object:one", 2, 8, 1,
				A.Substring(1), B, out _));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryEffectAttempt(receipt, 3,
				KingdomPurposeEffectCallbackKind.HarvestSeed, "object:one", 2, 8, 1,
				A.ToUpperInvariant(), B, out _));
		}

		[Test]
		public void EveryCallbackKindRoundTripsAndForeignScopeNeverReads()
		{
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryEffectReceipt("pair", 9,
				"operation", KingdomPurposeKind.Deep, out string receipt));
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryEffectReceipt("pair", 9,
				"other", KingdomPurposeKind.Deep, out string foreign));
			for (int raw = (int)KingdomPurposeEffectCallbackKind.RefineRaw;
				raw <= (int)KingdomPurposeEffectCallbackKind.HarvestStaple; raw++)
			{
				ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryEffectAttempt(receipt, raw - 1,
					(KingdomPurposeEffectCallbackKind)raw, "object-" + raw, raw, raw + 2,
					raw - 1, A, B, out string encoded));
				ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryReadEffectAttempt(encoded,
					receipt, out KingdomPurposeEffectAttempt copy));
				ClassicAssert.AreEqual((KingdomPurposeEffectCallbackKind)raw, copy.Callback);
				ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryReadEffectAttempt(encoded,
					foreign, out _));
				ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryReadEffectAttempt(encoded + "x",
					receipt, out _));
			}
		}

		[Test]
		public void FramedEffectReceiptsAreInjectiveForDelimiterBearingIdentities()
		{
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryEffectReceipt("a:b", 12, "c",
				KingdomPurposeKind.Forge, out string first));
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryEffectReceipt("a", 12, "b:c",
				KingdomPurposeKind.Forge, out string second));
			ClassicAssert.AreNotEqual(first, second);
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryEffectProductReceipt(first,
				KingdomPurposeEffectProductRole.Refined, out string refined));
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryEffectProductReceipt(first,
				KingdomPurposeEffectProductRole.Seed, out string seed));
			ClassicAssert.AreNotEqual(refined, seed);
		}

		[Test]
		public void ProductHighWaterRecordRoundTripsEveryLegalPartialBatch()
		{
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryEffectReceipt("pair", 3, "op",
				KingdomPurposeKind.Harvest, out string receipt));
			for (int refined = 0; refined <= 1; refined++)
			for (int seed = 0; seed <= 1; seed++)
			for (int staple = 0; staple <= 6; staple++)
			{
				KingdomPurposeEffectProductRecord record = new KingdomPurposeEffectProductRecord
					{ Refined = refined, Seed = seed, Staple = staple };
				ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryEffectProductRecord(receipt,
					record, out string encoded));
				ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryReadEffectProductRecord(encoded,
					receipt, out KingdomPurposeEffectProductRecord copy));
				ClassicAssert.AreEqual(refined, copy.Refined);
				ClassicAssert.AreEqual(seed, copy.Seed);
				ClassicAssert.AreEqual(staple, copy.Staple);
			}
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryEffectProductRecord(receipt,
				new KingdomPurposeEffectProductRecord { Refined = 2 }, out _));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryEffectProductRecord(receipt,
				new KingdomPurposeEffectProductRecord { Seed = 2 }, out _));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryEffectProductRecord(receipt,
				new KingdomPurposeEffectProductRecord { Staple = 7 }, out _));
		}

		[Test]
		public void MarkerPresenceAndOwnershipStaySeparate()
		{
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.EffectMarkerIsPresent(false, false));
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.EffectMarkerIsPresent(true, false));
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.EffectMarkerIsPresent(false, true));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.EffectMarkerIsOurs(
				"receipt", 4, true, 5, true, "receipt"));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.EffectMarkerIsOurs(
				"receipt", 4, true, 4, true, "foreign"));
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.EffectMarkerIsOurs(
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
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.PurposeCargoIsProtected(
				new KingdomPurposeCargoEvidence()));
		}

		private static void AssertProtected(KingdomPurposeCargoEvidence evidence)
		{
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.PurposeCargoIsProtected(evidence));
		}
	}
}
#endif
