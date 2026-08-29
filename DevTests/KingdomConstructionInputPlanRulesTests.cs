#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public class KingdomConstructionInputPlanRulesTests
	{
		private static readonly string A = new string('a', 64);
		private static readonly string C = new string('c', 64);

		[Test]
		public void RequiredWholeObjectWinsBeforeNearestAndPartialAlwaysStackRefuses()
		{
			string timber = UnitMaterial(KingdomMaterial.Timber);
			var candidates = new[]
			{
				Material("required", "holder-r", 1, 9, timber, false),
				Material("near-stack", "holder-n", 3, 1, timber, false)
			};
			Assert.IsTrue(KingdomConstructionInputPlanRules.TryPlan("job", 0,
				MaterialClaim(KingdomMaterial.Timber, 2), "required", candidates,
				out var plan, out var fault), fault.ToString());
			Assert.AreEqual(2, plan.LineCount);
			Assert.AreEqual("required", plan.LineAt(0).Candidate.SourceObjectId);
			Assert.AreEqual(1, plan.LineAt(0).Take);
			Assert.AreEqual("near-stack", plan.LineAt(1).Candidate.SourceObjectId);
			Assert.AreEqual(1, plan.LineAt(1).Take);
			Assert.IsNotNull(plan.LineAt(1).RemainderMarker);

			candidates[1] = Material("near-stack", "holder-n", 3, 1, timber, true);
			Assert.IsFalse(KingdomConstructionInputPlanRules.TryPlan("job", 0,
				MaterialClaim(KingdomMaterial.Timber, 2), "required", candidates,
				out plan, out fault));
			Assert.AreEqual(KingdomConstructionInputPlanFault.UnsafeStack, fault);
		}

		[Test]
		public void OrderedRequiredObjectSetSelectsEveryWholeIdentityAndRoundTrips()
		{
			string timber = UnitMaterial(KingdomMaterial.Timber);
			var candidates = new[]
			{
				Material("spare", "holder-s", 1, 1, timber, false),
				Material("legacy", "holder-l", 1, 9, timber, false),
				Material("reciprocal", "holder-r", 1, 8, timber, false)
			};
			string[] required = { "legacy", "reciprocal" };
			Assert.IsTrue(KingdomConstructionInputPlanRules.TryPlanWithRequiredObjects(
				"portfolio-job", 0, MaterialClaim(KingdomMaterial.Timber, 3),
				required, candidates, out var plan, out var fault), fault.ToString());
			Assert.AreEqual(2, plan.RequiredObjectCount);
			Assert.AreEqual("legacy", plan.RequiredObjectAt(0));
			Assert.AreEqual("reciprocal", plan.RequiredObjectAt(1));
			Assert.AreEqual("legacy", plan.LineAt(0).Candidate.SourceObjectId);
			Assert.AreEqual("reciprocal", plan.LineAt(1).Candidate.SourceObjectId);
			Assert.AreEqual("spare", plan.LineAt(2).Candidate.SourceObjectId);

			List<KingdomConstructionInputChild> children =
				new List<KingdomConstructionInputChild>();
			for (int i = 0; i < plan.ChildCount; i++)
				children.Add(Child(plan.ChildAt(i), 500 + i));
			Assert.IsTrue(KingdomConstructionInputPlanRules.TryCreateReceipt(plan,
				"portfolio-receipt", "owner", 0, "target", 9, 9, A, 1, 0, 0,
				EmptyClaim(), EmptyClaim(), children, out var receipt, out fault),
				fault.ToString());
			Assert.AreEqual(KingdomConstructionInputRules.Schema, receipt.Schema);
			Assert.AreEqual(2, receipt.RequiredObjectCount);
			Assert.IsTrue(receipt.RequiresObject("legacy"));
			Assert.IsTrue(receipt.RequiresObject("reciprocal"));
			Assert.IsTrue(KingdomConstructionInputRules.TryEncode(receipt,
				out string encoded, out var receiptFault), receiptFault.ToString());
			Assert.IsTrue(KingdomConstructionInputRules.TryDecode(encoded,
				out var decoded, out receiptFault), receiptFault.ToString());
			Assert.AreEqual("legacy", decoded.RequiredObjectAt(0));
			Assert.AreEqual("reciprocal", decoded.RequiredObjectAt(1));

			Assert.IsFalse(KingdomConstructionInputPlanRules.TryPlanWithRequiredObjects(
				"duplicate", 0, MaterialClaim(KingdomMaterial.Timber, 2),
				new[] { "legacy", "legacy" }, candidates, out plan, out fault));
			Assert.AreEqual(KingdomConstructionInputPlanFault.RequiredObject, fault);
			Assert.IsFalse(KingdomConstructionInputPlanRules.TryPlanWithRequiredObjects(
				"missing", 0, MaterialClaim(KingdomMaterial.Timber, 2),
				new[] { "legacy", "absent" }, candidates, out plan, out fault));
			Assert.AreEqual(KingdomConstructionInputPlanFault.RequiredObject, fault);
		}

		[Test]
		public void MixedClassificationUsesCanonicalDebitAndReturnsCandidateOrder()
		{
			KingdomBitTally unitBits = new KingdomBitTally();
			unitBits.Set(0, 1); unitBits.Set(2, 1);
			Assert.IsTrue(KingdomConstructionInputPlanRules.TryUnitClassification(
				KingdomMaterialDebitSourceKind.Material, (int)KingdomMaterial.Timber, null,
				out var materialKind, out var material));
			Assert.IsTrue(KingdomConstructionInputPlanRules.TryUnitClassification(
				KingdomMaterialDebitSourceKind.Exotic, (int)KingdomExotic.Gem, null,
				out var exoticKind, out var exotic));
			Assert.IsTrue(KingdomConstructionInputPlanRules.TryUnitClassification(
				KingdomMaterialDebitSourceKind.BitStock, 0, unitBits,
				out var bitKind, out var bits));
			Assert.AreEqual(KingdomConstructionInputKind.Material, materialKind);
			Assert.AreEqual(KingdomConstructionInputKind.Exotic, exoticKind);
			Assert.AreEqual(KingdomConstructionInputKind.Bit, bitKind);

			var candidates = new[]
			{
				Stock(exoticKind, exotic, "gem", "holder-e", 1),
				Stock(bitKind, bits, "bits", "holder-b", 2),
				Stock(materialKind, material, "timber", "holder-m", 3)
			};
			KingdomMaterialTally materials = new KingdomMaterialTally();
			materials.Set(KingdomMaterial.Timber, 1);
			KingdomExoticTally exotics = new KingdomExoticTally();
			exotics.Set(KingdomExotic.Gem, 1);
			string claim = new KingdomMaterialDebitCost(materials, unitBits, exotics)
				.ToClaimString();
			Assert.IsTrue(KingdomConstructionInputPlanRules.TryPlan("mixed", 0, claim,
				null, candidates, out var plan, out var fault), fault.ToString());
			Assert.AreEqual(3, plan.LineCount);
			Assert.AreEqual("gem", plan.LineAt(0).Candidate.SourceObjectId);
			Assert.AreEqual("bits", plan.LineAt(1).Candidate.SourceObjectId);
			Assert.AreEqual("timber", plan.LineAt(2).Candidate.SourceObjectId);
		}

		[Test]
		public void WaterUsesSettlementAggregateReserveAndChainsSixtyFourDramCasks()
		{
			var candidates = new[]
			{
				Water("water-1", "cistern-1", 100, 150, 10, 30, 1),
				Water("water-2", "cistern-2", 40, 150, 10, 30, 2)
			};
			Assert.IsTrue(KingdomConstructionInputPlanRules.TryPlan("water-job", 100,
				EmptyClaim(), null, candidates, out var plan, out var fault), fault.ToString());
			Assert.AreEqual(10, plan.DailyWaterUpkeep);
			Assert.AreEqual(2, plan.LineCount);
			Assert.AreEqual(100, plan.LineAt(0).Before);
			Assert.AreEqual(64, plan.LineAt(0).Take);
			Assert.AreEqual(36, plan.LineAt(1).Before);
			Assert.AreEqual(36, plan.LineAt(1).Take);
			Assert.AreEqual(1, plan.ChildCount);
			Assert.AreEqual(2, plan.ChildAt(0).CargoCount);

			var child = Child(plan.ChildAt(0), 101);
			Assert.IsTrue(KingdomConstructionInputPlanRules.TryCreateReceipt(plan,
				"water-receipt", "owner", 0, "target", 9, 9, A, 1, 0, 0,
				EmptyClaim(), EmptyClaim(), new[] { child }, out var receipt, out fault),
				fault.ToString());
			Assert.AreEqual(30, receipt.WaterReserveFloor);
			Assert.AreEqual("Cistern", receipt.SourceAt(0).Blueprint);
			Assert.AreEqual("EmptyWaterskin", receipt.CargoAt(0).Blueprint);
			Assert.AreEqual(64, receipt.CargoAt(0).Capacity);
			Assert.IsTrue(KingdomConstructionInputRules.TryValidate(receipt, out var receiptFault),
				receiptFault.ToString());
		}

		[Test]
		public void WaterReserveIsIndependentForEverySourceSettlement()
		{
			var candidates = new[]
			{
				Water("water-a", "cistern-a", 60, 60, 0, 6, 1,
					"settlement-a", "zone-a"),
				Water("water-b", "cistern-b", 60, 60, 0, 9, 2,
					"settlement-b", "zone-b")
			};
			Assert.IsTrue(KingdomConstructionInputPlanRules.TryPlan("two-water", 100,
				EmptyClaim(), null, candidates, out var plan, out var fault), fault.ToString());
			Assert.AreEqual(5, plan.DailyWaterUpkeep);
			Assert.AreEqual(2, plan.LineCount);
			Assert.AreEqual(54, plan.LineAt(0).Take);
			Assert.AreEqual(46, plan.LineAt(1).Take);
			Assert.LessOrEqual(plan.LineAt(0).Take, 64);
			Assert.LessOrEqual(plan.LineAt(1).Take, 64);
		}

		[Test]
		public void PackingUsesTwelveObjectsAndRefusesMoreThanSixteenEndpoints()
		{
			var oneSource = new[] { Water("water", "cistern", 832, 832, 0, 0, 1) };
			Assert.IsTrue(KingdomConstructionInputPlanRules.TryPlan("pack", 832,
				EmptyClaim(), null, oneSource, out var plan, out var fault), fault.ToString());
			Assert.AreEqual(13, plan.LineCount);
			Assert.AreEqual(2, plan.ChildCount);
			Assert.AreEqual(12, plan.ChildAt(0).CargoCount);
			Assert.AreEqual(1, plan.ChildAt(1).CargoCount);

			List<KingdomConstructionInputCandidate> many =
				new List<KingdomConstructionInputCandidate>();
			string timber = UnitMaterial(KingdomMaterial.Timber);
			for (int i = 0; i < 17; i++)
				many.Add(Material("item-" + i, "holder-" + i, 1, 1, timber, false));
			Assert.IsFalse(KingdomConstructionInputPlanRules.TryPlan("too-many", 0,
				MaterialClaim(KingdomMaterial.Timber, 17), null, many,
				out plan, out fault));
			Assert.AreEqual(KingdomConstructionInputPlanFault.Child, fault);
		}

		[Test]
		public void DurableLeasesExcludeExactSourcesAndRejectCrossReceiptOverlap()
		{
			var candidate = Water("water", "cistern", 10, 16, 0, 6, 1);
			Assert.IsTrue(KingdomConstructionInputPlanRules.TryPlan("lease-job", 10,
				EmptyClaim(), null, new[] { candidate }, out var plan, out var fault));
			var child = Child(plan.ChildAt(0), 301);
			Assert.IsTrue(KingdomConstructionInputPlanRules.TryCreateReceipt(plan,
				"lease-a", "owner", 0, "target", 9, 9, A, 1, 0, 0,
				EmptyClaim(), EmptyClaim(), new[] { child }, out var first, out fault));
			Assert.IsTrue(KingdomConstructionInputPlanRules.TryCollectDurableLeases(
				new[] { first }, out var leases, out fault));
			Assert.IsTrue(leases.Contains("source-zone", "cistern", "water"));
			Assert.IsTrue(leases.ContainsObject("water"));
			Assert.IsTrue(KingdomConstructionInputPlanRules.TryCreateReceipt(plan,
				"lease-b", "owner", 0, "target", 9, 9, A, 1, 0, 0,
				EmptyClaim(), EmptyClaim(), new[] { child }, out var second, out fault));
			Assert.IsFalse(KingdomConstructionInputPlanRules.TryCollectDurableLeases(
				new[] { first, second }, out leases, out fault));
			Assert.AreEqual(KingdomConstructionInputPlanFault.Duplicate, fault);

			Assert.IsTrue(KingdomConstructionInputRules.TryTransitionTransaction(first,
				first.Revision, first.TxPhase, KingdomConstructionInputTxPhase.Quarantined,
				out var quarantined, out var receiptFault), receiptFault.ToString());
			Assert.IsTrue(KingdomConstructionInputRules.IsTerminal(quarantined));
			Assert.IsTrue(KingdomConstructionInputPlanRules.TryCollectDurableLeases(
				new[] { quarantined }, out leases, out fault));
			Assert.IsTrue(leases.Contains("source-zone", "cistern", "water"),
				"ambiguous quarantined custody must stay leased");

			Assert.IsTrue(KingdomConstructionInputRules.TryTransitionTransaction(first,
				first.Revision, first.TxPhase, KingdomConstructionInputTxPhase.Reserved,
				out var reserved, out receiptFault));
			Assert.IsTrue(KingdomConstructionInputRules.TryTransitionTransaction(reserved,
				reserved.Revision, reserved.TxPhase, KingdomConstructionInputTxPhase.SourcePending,
				out var pending, out receiptFault));
			Assert.IsTrue(KingdomConstructionInputRules.TryTransitionCargo(pending,
				pending.Revision, 0, KingdomConstructionInputCargoPhase.Planned,
				KingdomConstructionInputCargoPhase.CreateIntent, out var creating,
				out receiptFault));
			Assert.IsTrue(KingdomConstructionInputRules.TryUpdateCargoEvidence(creating,
				creating.Revision, 0, "water-cargo",
				KingdomConstructionInputTopology.Invalid, null, null, -1, -1,
				null, null, 0, 0, out var evidenced, out receiptFault));
			Assert.IsTrue(KingdomConstructionInputLeaseRules.TryBuild(new[] { evidenced },
				out var shared, out fault), fault.ToString());
			Assert.IsTrue(shared.ContainsObject("water"));
			Assert.IsTrue(shared.ContainsObject("water-cargo"));
			Assert.IsTrue(shared.ContainsHolder("cistern"));
			Assert.IsFalse(shared.ContainsHolder("another-cistern"));
			Assert.IsTrue(shared.TryWaterHold("settlement", out int held, out int floor));
			Assert.AreEqual(10, held);
			Assert.AreEqual(6, floor);
		}

		[Test]
		public void SharedWaterAllowanceProtectsFloorButLetsUpkeepSpendIt()
		{
			Assert.IsTrue(KingdomConstructionInputLeaseRules.TryAvailableWater(
				30, 6, true, out int ordinary));
			Assert.AreEqual(24, ordinary);
			Assert.IsTrue(KingdomConstructionInputLeaseRules.TryAvailableWater(
				30, 6, false, out int upkeep));
			Assert.AreEqual(30, upkeep);
			Assert.IsFalse(KingdomConstructionInputLeaseRules.TryAvailableWater(
				-1, 6, true, out _));
			Assert.IsFalse(KingdomConstructionInputLeaseRules.TryAvailableWater(
				30, -1, true, out _));
		}

		private static KingdomConstructionInputCandidate Material(string id, string holder,
			int count, int route, string classification, bool alwaysStack)
		{
			return new KingdomConstructionInputCandidate(KingdomConstructionInputKind.Material,
				classification, "settlement", "source-zone", holder, id,
				KingdomConstructionInputTopology.ContainerInventory, 1, 1, "Timber",
				count, count, 0, 0, route, 0, alwaysStack);
		}

		private static KingdomConstructionInputCandidate Stock(KingdomConstructionInputKind kind,
			string classification, string id, string holder, int route)
		{
			return new KingdomConstructionInputCandidate(kind, classification, "settlement",
				"source-zone", holder, id,
				KingdomConstructionInputTopology.ContainerInventory, 1, 1, "StockObject",
				1, 1, 0, 0, route, 0, false);
		}

		private static KingdomConstructionInputCandidate Water(string id, string holder,
			int count, int stock, int prior, int floor, int route)
		{
			return Water(id, holder, count, stock, prior, floor, route,
				"settlement", "source-zone");
		}

		private static KingdomConstructionInputCandidate Water(string id, string holder,
			int count, int stock, int prior, int floor, int route,
			string settlement, string zone)
		{
			return new KingdomConstructionInputCandidate(KingdomConstructionInputKind.Water,
				KingdomConstructionInputRules.WaterClassification, settlement, zone,
				holder, id, KingdomConstructionInputTopology.LiquidVessel, 1, 1, "Cistern",
				count, stock, prior, floor, route, 0, false);
		}

		private static KingdomConstructionInputChild Child(
			KingdomConstructionInputPlannedChild draft, int job)
		{
			return new KingdomConstructionInputChild(draft.Ordinal, job, job,
				draft.CargoStart, draft.CargoCount,
				KingdomConstructionInputCargoShape.OpaqueObjectManifest, job + 1000,
				draft.SourceObjectId, draft.SourceZoneId, draft.SourceX, draft.SourceY,
				job + 2000, null, "target", 9, 9, 20, C, 1, 0);
		}

		private static string MaterialClaim(KingdomMaterial material, int count)
		{
			KingdomMaterialTally tally = new KingdomMaterialTally(); tally.Set(material, count);
			return new KingdomMaterialDebitCost(tally).ToClaimString();
		}

		private static string UnitMaterial(KingdomMaterial material)
		{ return MaterialClaim(material, 1); }
		private static string EmptyClaim()
		{ return new KingdomMaterialDebitCost().ToClaimString(); }
	}
}
#endif
