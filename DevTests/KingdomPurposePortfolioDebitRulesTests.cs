#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomPurposePortfolioDebitRulesTests
	{
		[Test]
		public void ExactLocalDebitCodecBindsEveryPhysicalBeforeAfterRow()
		{
			KingdomPurposeLocalDebitReceipt debit = Receipt();
			string encoded = KingdomPurposePortfolioRules.EncodeLocalDebit(debit);
			Assert.IsNotNull(encoded);
			Assert.IsTrue(KingdomPurposePortfolioRules.TryDecodeLocalDebit(encoded, out var copy));
			Assert.AreEqual(encoded, KingdomPurposePortfolioRules.EncodeLocalDebit(copy));
			copy.Lines[1].ContainerId = "another-store";
			Assert.IsNull(KingdomPurposePortfolioRules.EncodeLocalDebit(copy));
			copy = debit.Copy();
			copy.Lines[2].ObjectId = copy.Lines[1].ObjectId;
			Assert.IsNull(KingdomPurposePortfolioRules.EncodeLocalDebit(copy));
			copy = debit.Copy();
			copy.Lines[0].After++;
			Assert.IsNull(KingdomPurposePortfolioRules.EncodeLocalDebit(copy));
		}

		[Test]
		public void LocalPlanCannotAppearBeforeIntentOrChangeAfterPublication()
		{
			KingdomPurposePairReceipt pair = Pair();
			Assert.IsTrue(KingdomPurposePortfolioRules.TryCreateOperation(pair, "operation", 1,
				KingdomPurposeKind.Deep, true, false, null, null, null, null, null,
				out var operation, out var fault), fault.ToString());
			operation.LocalDebitReceipt = KingdomPurposePortfolioRules.EncodeLocalDebit(Receipt());
			Assert.IsFalse(KingdomPurposePortfolioRules.ValidOperation(operation, out _));
			operation.Phase = KingdomPurposeOperationPhase.LocalDebitPending;
			Assert.IsTrue(KingdomPurposePortfolioRules.ValidOperation(operation, out fault),
				fault.ToString());
			KingdomPurposeOperationReceipt changed = operation.Copy();
			changed.LocalDebitReceipt = KingdomPurposePortfolioRules.EncodeLocalDebit(
				ChangedReceipt());
			changed.Revision++;
			Assert.IsFalse(KingdomPurposePortfolioRules.ValidOperationTransition(operation, changed));
		}

		private static KingdomPurposeLocalDebitReceipt Receipt()
		{
			return new KingdomPurposeLocalDebitReceipt
			{
				PairId = "pair", PairEpoch = 7, OperationId = "operation",
				SourceSettlementId = "city-a", SourceZoneId = "zone-a",
				SourceWorkId = "work-a", SourceInputStoreId = "input-a",
				WaterRequested = 12, MaterialRequested = Claim(),
				Lines = new List<KingdomPurposeDebitLine>
				{
					new KingdomPurposeDebitLine { Kind = KingdomPurposeDebitKind.Water,
						ContainerId = "water-vessel", ObjectId = "water-vessel", Blueprint = "",
						Before = 20, After = 8, TypeIndex = -1, Capacity = 32 },
					new KingdomPurposeDebitLine { Kind = KingdomPurposeDebitKind.Material,
						ContainerId = "input-a", ObjectId = "stone-stack", Blueprint = "Stone",
						Before = 10, After = 4, TypeIndex = (int)KingdomMaterial.Stone },
					new KingdomPurposeDebitLine { Kind = KingdomPurposeDebitKind.Material,
						ContainerId = "input-a", ObjectId = "scrap-stack", Blueprint = "Scrap Metal",
						Before = 5, After = 3, TypeIndex = (int)KingdomMaterial.Scrap }
				}
			};
		}

		private static KingdomPurposeLocalDebitReceipt ChangedReceipt()
		{
			KingdomPurposeLocalDebitReceipt changed = Receipt();
			changed.Lines[0].ContainerId = "other-vessel";
			changed.Lines[0].ObjectId = "other-vessel";
			return changed;
		}

		private static string Claim()
		{
			KingdomMaterialTally tally = new KingdomMaterialTally();
			tally.Set(KingdomMaterial.Stone, 6);
			tally.Set(KingdomMaterial.Scrap, 2);
			return new KingdomMaterialDebitCost(tally).ToClaimString();
		}

		private static KingdomPurposePairReceipt Pair()
		{
			Assert.IsTrue(KingdomPurposePortfolioRules.TryCreatePair("pair", "realm", 7,
				KingdomPurposeKind.Deep, KingdomPurposeKind.Forge, "city-a", "city-b", "work-a",
				null, "zone-a", "zone-b", "input-a", "output-a", "input-b", "output-b",
				"gate-a", "gate-b",
				"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
				out var pair, out _));
			return pair;
		}
	}
}
#endif
