#if TAF_TESTS
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomPurposeEffectLegacyFixtureTests
	{
		// Captured from the final 47-field operation writer. This must never be rebuilt by
		// EncodeLegacyPair inside a test: a coupled reader/writer drift would then stay green.
		private const string FrozenV1 =
			"pv1;1:1;4:pair;5:realm;1:7;1:3;1:4;6:city-a;6:city-b;6:work-a;0:;6:zone-a" +
			";6:zone-b;7:input-a;8:output-a;7:input-b;8:output-b;6:gate-a;6:gate-b" +
			";64:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" +
			";1:1;1:0;1:0;0:;0:;1:2;1:0;479:pv1;1:1;9:operation;1:1;1:3;1:4" +
			";1:1;1:1;1:0;0:;0:;0:;0:;6:zone-a;6:zone-b;7:input-a;8:output-a" +
			";7:input-b;6:gate-a;6:gate-b" +
			";64:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa;0:;2:12" +
			";1:0;1:0;1:0;1:0;1:0" +
			";52:v1|m:0,0,0,6,0,2,0,0,0|b:0,0,0,0,0,0,0,0,0|e:0,0,0,0" +
			";52:v1|m:0,0,0,0,0,0,0,0,0|b:0,0,0,0,0,0,0,0,0|e:0,0,0,0" +
			";52:v1|m:0,0,0,0,0,0,0,0,0|b:0,0,0,0,0,0,0,0,0|e:0,0,0,0" +
			";0:;0:;0:;0:;0:;0:;0:;0:;0:;1:0;4:pair;1:7;6:city-a;6:city-b" +
			";6:work-a;0:;17:purpose-operation;1:1;0:;1:2;12:purpose-pair";

		// Same receipt after one valid orphan-publication CAS. The outer pair schema stays v1;
		// its nested operation is the current 48-field v2 wire and carries Exempt explicitly.
		private const string FrozenV2AfterOrphan =
			"pv1;1:1;4:pair;5:realm;1:7;1:3;1:4;6:city-a;6:city-b;6:work-a;0:;6:zone-a" +
			";6:zone-b;7:input-a;8:output-a;7:input-b;8:output-b;6:gate-a;6:gate-b" +
			";64:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" +
			";1:1;1:0;1:0;0:;0:;1:9;1:2;485:pv1;1:2;9:operation;1:1;1:3;1:4" +
			";1:1;1:1;1:0;0:;0:;0:;0:;6:zone-a;6:zone-b;7:input-a;8:output-a" +
			";7:input-b;6:gate-a;6:gate-b" +
			";64:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa;0:;2:12" +
			";1:0;1:0;1:0;1:0;1:0" +
			";52:v1|m:0,0,0,6,0,2,0,0,0|b:0,0,0,0,0,0,0,0,0|e:0,0,0,0" +
			";52:v1|m:0,0,0,0,0,0,0,0,0|b:0,0,0,0,0,0,0,0,0|e:0,0,0,0" +
			";52:v1|m:0,0,0,0,0,0,0,0,0|b:0,0,0,0,0,0,0,0,0|e:0,0,0,0" +
			";0:;0:;0:;0:;0:;0:;0:;0:;0:;1:0;4:pair;1:7;6:city-a;6:city-b" +
			";6:work-a;0:;3:255;17:purpose-operation;1:2;0:;1:2" +
			";12:purpose-pair";

		[Test]
		public void FrozenHistoricalV1ReadsExactlyAndDoesNotSelfMigrate()
		{
			Assert.IsFalse(KingdomPurposePortfolioRules.TryDecodePair(FrozenV1, out _));
			Assert.IsTrue(KingdomPurposePortfolioRules.TryDecodePairAny(FrozenV1,
				out KingdomPurposePairReceipt pair, out bool legacy));
			Assert.IsTrue(legacy);
			Assert.IsTrue(pair.LegacyWire);
			Assert.AreEqual(KingdomPurposePairPhase.BootstrapOutstanding, pair.Phase);
			Assert.AreEqual(KingdomPurposeKind.Deep, pair.Operation.SourceKind);
			Assert.AreEqual(KingdomPurposePortfolioRules.PurposeEffectExempt,
				pair.Operation.EffectStep);
			Assert.AreEqual(FrozenV1, KingdomPurposePortfolioRules.EncodeLegacyPair(pair));
		}

		[Test]
		public void FirstAuthorizedPublicationMigratesOnceToFrozenCurrentWire()
		{
			Assert.IsTrue(KingdomPurposePortfolioRules.TryDecodePairAny(FrozenV1,
				out KingdomPurposePairReceipt before, out bool legacy));
			Assert.IsTrue(legacy);
			KingdomPurposePairReceipt after = before.Copy();
			after.Phase = KingdomPurposePairPhase.Orphaned;
			after.ResumePhase = before.Phase;
			after.Revision++;
			Assert.IsTrue(KingdomPurposePortfolioRules.ValidTransition(before, after,
				out KingdomPurposePairFault transitionFault), transitionFault.ToString());

			after.LegacyWire = false;
			Assert.AreEqual(FrozenV2AfterOrphan,
				KingdomPurposePortfolioRules.EncodePair(after));
			Assert.IsTrue(KingdomPurposePortfolioRules.TryDecodePairAny(
				FrozenV2AfterOrphan, out KingdomPurposePairReceipt current, out legacy));
			Assert.IsFalse(legacy);
			Assert.IsFalse(current.LegacyWire);
			Assert.AreEqual(KingdomPurposePortfolioRules.PurposeEffectExempt,
				current.Operation.EffectStep);
			Assert.AreEqual(FrozenV2AfterOrphan,
				KingdomPurposePortfolioRules.EncodePair(current));
		}

		[Test]
		public void FrozenFixtureRejectsTornAndNonExemptLegacyVariants()
		{
			Assert.IsFalse(KingdomPurposePortfolioRules.TryDecodePairAny(
				FrozenV1 + "x", out _, out _));
			Assert.IsTrue(KingdomPurposePortfolioRules.TryDecodePairAny(FrozenV1,
				out KingdomPurposePairReceipt pair, out _));
			pair.Operation.EffectStep = KingdomPurposePortfolioRules.PurposeEffectNone;
			Assert.IsNull(KingdomPurposePortfolioRules.EncodeLegacyPair(pair));
		}
	}
}
#endif
