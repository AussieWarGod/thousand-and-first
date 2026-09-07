#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;

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
			ClassicAssert.IsTrue(KingdomConstructionInputPlanRules.TryPlan("job", 0,
				MaterialClaim(KingdomMaterial.Timber, 2), "required", candidates,
				out var plan, out var fault), fault.ToString());
			ClassicAssert.AreEqual(2, plan.LineCount);
			ClassicAssert.AreEqual("required", plan.LineAt(0).Candidate.SourceObjectId);
			ClassicAssert.AreEqual(1, plan.LineAt(0).Take);
			ClassicAssert.AreEqual("near-stack", plan.LineAt(1).Candidate.SourceObjectId);
			ClassicAssert.AreEqual(1, plan.LineAt(1).Take);
			ClassicAssert.IsNotNull(plan.LineAt(1).RemainderMarker);

			candidates[1] = Material("near-stack", "holder-n", 3, 1, timber, true);
			ClassicAssert.IsFalse(KingdomConstructionInputPlanRules.TryPlan("job", 0,
				MaterialClaim(KingdomMaterial.Timber, 2), "required", candidates,
				out plan, out fault));
			ClassicAssert.AreEqual(KingdomConstructionInputPlanFault.UnsafeStack, fault);
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
			ClassicAssert.IsTrue(KingdomConstructionInputPlanRules.TryPlanWithRequiredObjects(
				"portfolio-job", 0, MaterialClaim(KingdomMaterial.Timber, 3),
				required, candidates, out var plan, out var fault), fault.ToString());
			ClassicAssert.AreEqual(2, plan.RequiredObjectCount);
			ClassicAssert.AreEqual("legacy", plan.RequiredObjectAt(0));
			ClassicAssert.AreEqual("reciprocal", plan.RequiredObjectAt(1));
			ClassicAssert.AreEqual("legacy", plan.LineAt(0).Candidate.SourceObjectId);
			ClassicAssert.AreEqual("reciprocal", plan.LineAt(1).Candidate.SourceObjectId);
			ClassicAssert.AreEqual("spare", plan.LineAt(2).Candidate.SourceObjectId);

			List<KingdomConstructionInputChild> children =
				new List<KingdomConstructionInputChild>();
			for (int i = 0; i < plan.ChildCount; i++)
				children.Add(Child(plan.ChildAt(i), 500 + i));
			ClassicAssert.IsTrue(KingdomConstructionInputPlanRules.TryCreateReceipt(plan,
				"portfolio-receipt", "owner", 0, "target", 9, 9, A, 1, 0, 0,
				EmptyClaim(), EmptyClaim(), children, out var receipt, out fault),
				fault.ToString());
			ClassicAssert.AreEqual(KingdomConstructionInputRules.Schema, receipt.Schema);
			ClassicAssert.AreEqual(2, receipt.RequiredObjectCount);
			ClassicAssert.IsTrue(receipt.RequiresObject("legacy"));
			ClassicAssert.IsTrue(receipt.RequiresObject("reciprocal"));
			ClassicAssert.IsTrue(KingdomConstructionInputRules.TryEncode(receipt,
				out string encoded, out var receiptFault), receiptFault.ToString());
			ClassicAssert.IsTrue(KingdomConstructionInputRules.TryDecode(encoded,
				out var decoded, out receiptFault), receiptFault.ToString());
			ClassicAssert.AreEqual("legacy", decoded.RequiredObjectAt(0));
			ClassicAssert.AreEqual("reciprocal", decoded.RequiredObjectAt(1));

			ClassicAssert.IsFalse(KingdomConstructionInputPlanRules.TryPlanWithRequiredObjects(
				"duplicate", 0, MaterialClaim(KingdomMaterial.Timber, 2),
				new[] { "legacy", "legacy" }, candidates, out plan, out fault));
			ClassicAssert.AreEqual(KingdomConstructionInputPlanFault.RequiredObject, fault);
			ClassicAssert.IsFalse(KingdomConstructionInputPlanRules.TryPlanWithRequiredObjects(
				"missing", 0, MaterialClaim(KingdomMaterial.Timber, 2),
				new[] { "legacy", "absent" }, candidates, out plan, out fault));
			ClassicAssert.AreEqual(KingdomConstructionInputPlanFault.RequiredObject, fault);
		}

		[Test]
		public void MixedClassificationUsesCanonicalDebitAndReturnsCandidateOrder()
		{
			KingdomBitTally unitBits = new KingdomBitTally();
			unitBits.Set(0, 1); unitBits.Set(2, 1);
			ClassicAssert.IsTrue(KingdomConstructionInputPlanRules.TryUnitClassification(
				KingdomMaterialDebitSourceKind.Material, (int)KingdomMaterial.Timber, null,
				out var materialKind, out var material));
			ClassicAssert.IsTrue(KingdomConstructionInputPlanRules.TryUnitClassification(
				KingdomMaterialDebitSourceKind.Exotic, (int)KingdomExotic.Gem, null,
				out var exoticKind, out var exotic));
			ClassicAssert.IsTrue(KingdomConstructionInputPlanRules.TryUnitClassification(
				KingdomMaterialDebitSourceKind.BitStock, 0, unitBits,
				out var bitKind, out var bits));
			ClassicAssert.AreEqual(KingdomConstructionInputKind.Material, materialKind);
			ClassicAssert.AreEqual(KingdomConstructionInputKind.Exotic, exoticKind);
			ClassicAssert.AreEqual(KingdomConstructionInputKind.Bit, bitKind);

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
			ClassicAssert.IsTrue(KingdomConstructionInputPlanRules.TryPlan("mixed", 0, claim,
				null, candidates, out var plan, out var fault), fault.ToString());
			ClassicAssert.AreEqual(3, plan.LineCount);
			ClassicAssert.AreEqual("gem", plan.LineAt(0).Candidate.SourceObjectId);
			ClassicAssert.AreEqual("bits", plan.LineAt(1).Candidate.SourceObjectId);
			ClassicAssert.AreEqual("timber", plan.LineAt(2).Candidate.SourceObjectId);
		}

		[Test]
		public void WaterUsesSettlementAggregateReserveAndChainsSixtyFourDramCasks()
		{
			var candidates = new[]
			{
				Water("water-1", "cistern-1", 100, 150, 10, 30, 1),
				Water("water-2", "cistern-2", 40, 150, 10, 30, 2)
			};
			ClassicAssert.IsTrue(KingdomConstructionInputPlanRules.TryPlan("water-job", 100,
				EmptyClaim(), null, candidates, out var plan, out var fault), fault.ToString());
			ClassicAssert.AreEqual(10, plan.DailyWaterUpkeep);
			ClassicAssert.AreEqual(2, plan.LineCount);
			ClassicAssert.AreEqual(100, plan.LineAt(0).Before);
			ClassicAssert.AreEqual(64, plan.LineAt(0).Take);
			ClassicAssert.AreEqual(36, plan.LineAt(1).Before);
			ClassicAssert.AreEqual(36, plan.LineAt(1).Take);
			ClassicAssert.AreEqual(1, plan.ChildCount);
			ClassicAssert.AreEqual(2, plan.ChildAt(0).CargoCount);

			var child = Child(plan.ChildAt(0), 101);
			ClassicAssert.IsTrue(KingdomConstructionInputPlanRules.TryCreateReceipt(plan,
				"water-receipt", "owner", 0, "target", 9, 9, A, 1, 0, 0,
				EmptyClaim(), EmptyClaim(), new[] { child }, out var receipt, out fault),
				fault.ToString());
			ClassicAssert.AreEqual(30, receipt.WaterReserveFloor);
			ClassicAssert.AreEqual("Cistern", receipt.SourceAt(0).Blueprint);
			ClassicAssert.AreEqual("EmptyWaterskin", receipt.CargoAt(0).Blueprint);
			ClassicAssert.AreEqual(64, receipt.CargoAt(0).Capacity);
			ClassicAssert.IsTrue(KingdomConstructionInputRules.TryValidate(receipt, out var receiptFault),
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
			ClassicAssert.IsTrue(KingdomConstructionInputPlanRules.TryPlan("two-water", 100,
				EmptyClaim(), null, candidates, out var plan, out var fault), fault.ToString());
			ClassicAssert.AreEqual(5, plan.DailyWaterUpkeep);
			ClassicAssert.AreEqual(2, plan.LineCount);
			ClassicAssert.AreEqual(54, plan.LineAt(0).Take);
			ClassicAssert.AreEqual(46, plan.LineAt(1).Take);
			ClassicAssert.LessOrEqual(plan.LineAt(0).Take, 64);
			ClassicAssert.LessOrEqual(plan.LineAt(1).Take, 64);
		}

		[Test]
		public void PackingUsesTwelveObjectsAndRefusesMoreThanSixteenEndpoints()
		{
			var oneSource = new[] { Water("water", "cistern", 832, 832, 0, 0, 1) };
			ClassicAssert.IsTrue(KingdomConstructionInputPlanRules.TryPlan("pack", 832,
				EmptyClaim(), null, oneSource, out var plan, out var fault), fault.ToString());
			ClassicAssert.AreEqual(13, plan.LineCount);
			ClassicAssert.AreEqual(2, plan.ChildCount);
			ClassicAssert.AreEqual(12, plan.ChildAt(0).CargoCount);
			ClassicAssert.AreEqual(1, plan.ChildAt(1).CargoCount);

			List<KingdomConstructionInputCandidate> many =
				new List<KingdomConstructionInputCandidate>();
			string timber = UnitMaterial(KingdomMaterial.Timber);
			for (int i = 0; i < 17; i++)
				many.Add(Material("item-" + i, "holder-" + i, 1, 1, timber, false));
			ClassicAssert.IsFalse(KingdomConstructionInputPlanRules.TryPlan("too-many", 0,
				MaterialClaim(KingdomMaterial.Timber, 17), null, many,
				out plan, out fault));
			ClassicAssert.AreEqual(KingdomConstructionInputPlanFault.Child, fault);
		}

		[Test]
		public void DurableLeasesExcludeExactSourcesAndRejectCrossReceiptOverlap()
		{
			var candidate = Water("water", "cistern", 10, 16, 0, 6, 1);
			ClassicAssert.IsTrue(KingdomConstructionInputPlanRules.TryPlan("lease-job", 10,
				EmptyClaim(), null, new[] { candidate }, out var plan, out var fault));
			var child = Child(plan.ChildAt(0), 301);
			ClassicAssert.IsTrue(KingdomConstructionInputPlanRules.TryCreateReceipt(plan,
				"lease-a", "owner", 0, "target", 9, 9, A, 1, 0, 0,
				EmptyClaim(), EmptyClaim(), new[] { child }, out var first, out fault));
			ClassicAssert.IsTrue(KingdomConstructionInputPlanRules.TryCollectDurableLeases(
				new[] { first }, out var leases, out fault));
			ClassicAssert.IsTrue(leases.Contains("source-zone", "cistern", "water"));
			ClassicAssert.IsTrue(leases.ContainsObject("water"));
			ClassicAssert.IsTrue(KingdomConstructionInputPlanRules.TryCreateReceipt(plan,
				"lease-b", "owner", 0, "target", 9, 9, A, 1, 0, 0,
				EmptyClaim(), EmptyClaim(), new[] { child }, out var second, out fault));
			ClassicAssert.IsFalse(KingdomConstructionInputPlanRules.TryCollectDurableLeases(
				new[] { first, second }, out leases, out fault));
			ClassicAssert.AreEqual(KingdomConstructionInputPlanFault.Duplicate, fault);

			ClassicAssert.IsTrue(KingdomConstructionInputRules.TryTransitionTransaction(first,
				first.Revision, first.TxPhase, KingdomConstructionInputTxPhase.Quarantined,
				out var quarantined, out var receiptFault), receiptFault.ToString());
			ClassicAssert.IsTrue(KingdomConstructionInputRules.IsTerminal(quarantined));
			ClassicAssert.IsTrue(KingdomConstructionInputPlanRules.TryCollectDurableLeases(
				new[] { quarantined }, out leases, out fault));
			ClassicAssert.IsTrue(leases.Contains("source-zone", "cistern", "water"),
				"ambiguous quarantined custody must stay leased");

			ClassicAssert.IsTrue(KingdomConstructionInputRules.TryTransitionTransaction(first,
				first.Revision, first.TxPhase, KingdomConstructionInputTxPhase.Reserved,
				out var reserved, out receiptFault));
			ClassicAssert.IsTrue(KingdomConstructionInputRules.TryTransitionTransaction(reserved,
				reserved.Revision, reserved.TxPhase, KingdomConstructionInputTxPhase.SourcePending,
				out var pending, out receiptFault));
			ClassicAssert.IsTrue(KingdomConstructionInputRules.TryTransitionCargo(pending,
				pending.Revision, 0, KingdomConstructionInputCargoPhase.Planned,
				KingdomConstructionInputCargoPhase.CreateIntent, out var creating,
				out receiptFault));
			ClassicAssert.IsTrue(KingdomConstructionInputRules.TryUpdateCargoEvidence(creating,
				creating.Revision, 0, "water-cargo",
				KingdomConstructionInputTopology.Invalid, null, null, -1, -1,
				null, null, 0, 0, out var evidenced, out receiptFault));
			ClassicAssert.IsTrue(KingdomConstructionInputLeaseRules.TryBuild(new[] { evidenced },
				out var shared, out fault), fault.ToString());
			ClassicAssert.IsTrue(shared.ContainsObject("water"));
			ClassicAssert.IsTrue(shared.ContainsObject("water-cargo"));
			ClassicAssert.IsTrue(shared.ContainsHolder("cistern"));
			ClassicAssert.IsFalse(shared.ContainsHolder("another-cistern"));
			ClassicAssert.IsTrue(shared.TryWaterHold("settlement", out int held, out int floor));
			ClassicAssert.AreEqual(10, held);
			ClassicAssert.AreEqual(6, floor);
		}

		[Test]
		public void SharedWaterAllowanceProtectsFloorButLetsUpkeepSpendIt()
		{
			ClassicAssert.IsTrue(KingdomConstructionInputLeaseRules.TryAvailableWater(
				30, 6, true, out int ordinary));
			ClassicAssert.AreEqual(24, ordinary);
			ClassicAssert.IsTrue(KingdomConstructionInputLeaseRules.TryAvailableWater(
				30, 6, false, out int upkeep));
			ClassicAssert.AreEqual(30, upkeep);
			ClassicAssert.IsFalse(KingdomConstructionInputLeaseRules.TryAvailableWater(
				-1, 6, true, out _));
			ClassicAssert.IsFalse(KingdomConstructionInputLeaseRules.TryAvailableWater(
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
