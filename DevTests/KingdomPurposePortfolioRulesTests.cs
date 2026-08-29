#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomPurposePortfolioRulesTests
	{
		private const string D = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

		[Test]
		public void CatalogueIsExactlySymmetricFiveCycleAndTenDirections()
		{
			IList<KingdomPurposePortfolioRecipe> rows = KingdomPurposePortfolioRules.AllRecipes();
			Assert.AreEqual(10, rows.Count);
			HashSet<string> directed = new HashSet<string>();
			for (int i = 0; i < rows.Count; i++)
			{
				var row = rows[i];
				Assert.IsTrue(directed.Add(row.Source + ">" + row.Destination));
				Assert.IsTrue(KingdomPurposePortfolioRules.TryRecipe(
					row.Destination, row.Source, out _), row.CargoKey);
				Assert.AreEqual(2, KingdomPurposePortfolioRules.Partners(row.Source).Count);
				Assert.IsFalse(KingdomPurposePortfolioRules.Compatible(row.Source, row.Source));
			}
			for (int a = 1; a <= 5; a++)
				for (int b = 1; b <= 5; b++)
					Assert.AreEqual(directed.Contains((KingdomPurposeKind)a + ">"
						+ (KingdomPurposeKind)b), KingdomPurposePortfolioRules.Compatible(
						(KingdomPurposeKind)a, (KingdomPurposeKind)b));
		}

		[Test]
		public void ExactRecipesFreezeWaterFoodMaterialsAndCargoContent()
		{
			AssertRecipe(KingdomPurposeKind.Deep, KingdomPurposeKind.Forge,
				"deep-ore-assay", 12, 0, "stone:6,scrap:2", KingdomMaterial.Scrap, 0);
			AssertRecipe(KingdomPurposeKind.Forge, KingdomPurposeKind.Deep,
				"drill-crown", 16, 0, "shapedstone:2,workedmetal:4",
				KingdomMaterial.WorkedMetal, 0);
			AssertRecipe(KingdomPurposeKind.Forge, KingdomPurposeKind.Harvest,
				"irrigation-manifold", 16, 0, "shapedtimber:2,workedmetal:3",
				KingdomMaterial.WorkedMetal, 0);
			AssertRecipe(KingdomPurposeKind.Harvest, KingdomPurposeKind.Forge,
				"quench-provision-lot", 12, 8, "shapedtimber:1",
				KingdomMaterial.ShapedTimber, 6);
			AssertRecipe(KingdomPurposeKind.Harvest, KingdomPurposeKind.Flesh,
				"sterile-culture-mash", 10, 8, "workedmetal:1",
				KingdomMaterial.WorkedMetal, 6);
			AssertRecipe(KingdomPurposeKind.Flesh, KingdomPurposeKind.Harvest,
				"blightproof-seed-graft", 12, 4, "brush:4", KingdomMaterial.Brush, 0);
			AssertRecipe(KingdomPurposeKind.Flesh, KingdomPurposeKind.Chrome,
				"living-neural-lattice", 16, 4, "brush:4,workedmetal:1",
				KingdomMaterial.WorkedMetal, 0);
			AssertRecipe(KingdomPurposeKind.Chrome, KingdomPurposeKind.Flesh,
				"psybernetic-control-wafer", 16, 0, "scrap:4,workedmetal:2",
				KingdomMaterial.WorkedMetal, 0);
			AssertRecipe(KingdomPurposeKind.Chrome, KingdomPurposeKind.Deep,
				"strata-sense-coil", 14, 0, "scrap:4,workedmetal:2",
				KingdomMaterial.WorkedMetal, 0);
			AssertRecipe(KingdomPurposeKind.Deep, KingdomPurposeKind.Chrome,
				"conductor-assay", 10, 0, "stone:4,scrap:2", KingdomMaterial.Scrap, 0);
		}

		[Test]
		public void PairCodecIsCanonicalAndTamperRejecting()
		{
			KingdomPurposePairReceipt pair = Pair();
			string encoded = KingdomPurposePortfolioRules.EncodePair(pair);
			Assert.IsNotNull(encoded);
			Assert.IsTrue(KingdomPurposePortfolioRules.TryDecodePair(encoded, out var decoded));
			Assert.AreEqual(encoded, KingdomPurposePortfolioRules.EncodePair(decoded));
			Assert.IsFalse(KingdomPurposePortfolioRules.TryDecodePair(encoded + "x", out _));
			pair.SecondSettlementId = pair.FirstSettlementId;
			Assert.IsNull(KingdomPurposePortfolioRules.EncodePair(pair));
		}

		[Test]
		public void BootstrapReturnAndAlternationAreOneWayAndIdentityBound()
		{
			KingdomPurposePairReceipt frozen = Pair();
			Assert.IsTrue(KingdomPurposePortfolioRules.TryCreateOperation(frozen, "op-1", 1,
				KingdomPurposeKind.Deep, true, false, null, null, null, null, null,
				out var bootstrap, out var fault), fault.ToString());
			KingdomPurposePairReceipt running = frozen.Copy();
			running.BootstrapUsed = true;
			running.Phase = KingdomPurposePairPhase.BootstrapOutstanding;
			running.Operation = bootstrap;
			running.NextOperationOrdinal++;
			running.Revision++;
			Assert.IsTrue(KingdomPurposePortfolioRules.ValidTransition(frozen, running, out fault),
				fault.ToString());

			KingdomPurposePairReceipt delivered = Deliver(running, "cargo-1", "job-1",
				KingdomPurposePairPhase.SecondPending);
			Assert.IsTrue(KingdomPurposePortfolioRules.ValidPair(delivered, out fault),
				fault.ToString());
			Assert.IsFalse(KingdomPurposePortfolioRules.ValidTransition(delivered, running, out _),
				"bootstrap exemption cannot be rewound");

			Assert.IsTrue(KingdomPurposePortfolioRules.TryCreateOperation(delivered, "op-2", 2,
				KingdomPurposeKind.Forge, false, true, null, null, null, null, "work-forge",
				out var returned, out fault), fault.ToString());
			KingdomPurposePairReceipt returnRunning = delivered.Copy();
			returnRunning.SecondWorkId = "work-forge";
			returnRunning.ReturnUsed = true;
			returnRunning.Phase = KingdomPurposePairPhase.ReturnOutstanding;
			returnRunning.Operation = returned;
			returnRunning.NextOperationOrdinal++;
			returnRunning.Revision++;
			Assert.IsTrue(KingdomPurposePortfolioRules.ValidTransition(
				delivered, returnRunning, out fault), fault.ToString());

			KingdomPurposePairReceipt awaiting = Deliver(returnRunning, "cargo-2", "job-2",
				KingdomPurposePairPhase.CargoAwaitingActivation);
			Assert.IsTrue(KingdomPurposePortfolioRules.TryCreateOperation(awaiting, "op-3", 3,
				KingdomPurposeKind.Deep, false, false, awaiting.Operation.OutputCargoId,
				awaiting.Operation.OutputCargoReceipt, null, null, null,
				out var activation, out fault), fault.ToString());
			KingdomPurposePairReceipt activationRunning = awaiting.Copy();
			activationRunning.Phase = KingdomPurposePairPhase.OperationOutstanding;
			activationRunning.NextKind = KingdomPurposeKind.Deep;
			activationRunning.Operation = activation;
			activationRunning.NextOperationOrdinal++;
			activationRunning.Revision++;
			Assert.IsTrue(KingdomPurposePortfolioRules.ValidTransition(
				awaiting, activationRunning, out fault), fault.ToString());
			KingdomPurposePairReceipt activationAwaiting = Deliver(activationRunning,
				"cargo-3", "job-3", KingdomPurposePairPhase.CargoAwaitingConsumption);
			KingdomPurposePairReceipt active = activationAwaiting.Copy();
			active.Phase = KingdomPurposePairPhase.Active;
			active.NextKind = KingdomPurposeKind.Forge;
			active.CreditCargoId = activationAwaiting.Operation.OutputCargoId;
			active.CreditCargoReceipt = activationAwaiting.Operation.OutputCargoReceipt;
			active.Operation = null;
			active.Revision++;
			Assert.IsTrue(KingdomPurposePortfolioRules.ValidTransition(
				activationAwaiting, active, out fault),
				fault.ToString());

			Assert.IsTrue(KingdomPurposePortfolioRules.TryCreateOperation(active, "op-4", 4,
				KingdomPurposeKind.Forge, false, false, active.CreditCargoId,
				active.CreditCargoReceipt, null, null, null, out var normal, out fault), fault.ToString());
			KingdomPurposePairReceipt normalRunning = active.Copy();
			normalRunning.Phase = KingdomPurposePairPhase.OperationOutstanding;
			normalRunning.Operation = normal;
			normalRunning.CreditCargoId = null;
			normalRunning.CreditCargoReceipt = null;
			normalRunning.NextOperationOrdinal++;
			normalRunning.Revision++;
			Assert.IsTrue(KingdomPurposePortfolioRules.ValidTransition(active, normalRunning,
				out fault), fault.ToString());
			KingdomPurposePairReceipt normalAwaiting = Deliver(normalRunning, "cargo-4", "job-4",
				KingdomPurposePairPhase.CargoAwaitingConsumption);
			KingdomPurposePairReceipt reciprocal = normalAwaiting.Copy();
			reciprocal.Phase = KingdomPurposePairPhase.Active;
			reciprocal.NextKind = KingdomPurposeKind.Deep;
			reciprocal.CreditCargoId = normalAwaiting.Operation.OutputCargoId;
			reciprocal.CreditCargoReceipt = normalAwaiting.Operation.OutputCargoReceipt;
			reciprocal.Operation = null;
			reciprocal.Revision++;
			Assert.IsTrue(KingdomPurposePortfolioRules.ValidTransition(
				normalAwaiting, reciprocal, out fault), fault.ToString());
			Assert.IsFalse(KingdomPurposePortfolioRules.TryCreateOperation(active, "wrong", 4,
				KingdomPurposeKind.Deep, false, false, active.CreditCargoId,
				active.CreditCargoReceipt, null, null, null, out _, out _));
		}

		[Test]
		public void SecondEndpointAdoptionIsAuthenticatedAtomicAndOneTime()
		{
			KingdomPurposePairReceipt frozen = Pair();
			Assert.IsTrue(KingdomPurposePortfolioRules.TryCreateOperation(frozen, "adopt-op-1", 1,
				KingdomPurposeKind.Deep, true, false, null, null, null, null, null,
				out var bootstrap, out var fault), fault.ToString());
			KingdomPurposePairReceipt running = frozen.Copy();
			running.BootstrapUsed = true;
			running.Phase = KingdomPurposePairPhase.BootstrapOutstanding;
			running.Operation = bootstrap;
			running.NextOperationOrdinal++;
			running.Revision++;
			Assert.IsTrue(KingdomPurposePortfolioRules.ValidTransition(frozen, running, out fault),
				fault.ToString());
			KingdomPurposePairReceipt delivered = Deliver(running, "adopt-cargo", "adopt-job",
				KingdomPurposePairPhase.SecondPending);
			Assert.IsTrue(KingdomPurposePortfolioRules.TryRouteDigest(delivered.RealmId,
				delivered.FirstSettlementId, delivered.SecondSettlementId,
				delivered.FirstGateKey, delivered.SecondGateKey, delivered.FirstZoneId,
				delivered.SecondZoneId, delivered.FirstInputStoreId,
				delivered.FirstOutputStoreId, "authored-input-b", "authored-output-b",
				out string digest));
			Assert.IsFalse(KingdomPurposePortfolioRules.TryCreateOperationWithSecondEndpoint(
				delivered, "adopt-op-2", 2, KingdomPurposeKind.Forge, null, null,
				"work-forge", "authored-input-b", "authored-output-b", D,
				out _, out _), "changed stores need their exact recomputed route digest");
			Assert.IsTrue(KingdomPurposePortfolioRules.TryCreateOperationWithSecondEndpoint(
				delivered, "adopt-op-2", 2, KingdomPurposeKind.Forge, null, null,
				"work-forge", "authored-input-b", "authored-output-b", digest,
				out var returned, out fault), fault.ToString());
			Assert.AreEqual("input-b", delivered.SecondInputStoreId);
			Assert.AreEqual("output-b", delivered.SecondOutputStoreId);
			Assert.AreEqual(D, delivered.RouteDigest,
				"the pure factory may authenticate a candidate but cannot mutate its parent pair");
			KingdomPurposePairReceipt adopted = delivered.Copy();
			adopted.SecondWorkId = "work-forge";
			adopted.SecondInputStoreId = "authored-input-b";
			adopted.SecondOutputStoreId = "authored-output-b";
			adopted.RouteDigest = digest;
			adopted.ReturnUsed = true;
			adopted.Phase = KingdomPurposePairPhase.ReturnOutstanding;
			adopted.Operation = returned;
			adopted.NextOperationOrdinal++;
			adopted.Revision++;
			Assert.IsTrue(KingdomPurposePortfolioRules.ValidTransition(delivered, adopted,
				out fault), fault.ToString());

			KingdomPurposePairReceipt later = adopted.Copy();
			later.SecondInputStoreId = "later-input-b";
			Assert.IsTrue(KingdomPurposePortfolioRules.TryRouteDigest(later.RealmId,
				later.FirstSettlementId, later.SecondSettlementId, later.FirstGateKey,
				later.SecondGateKey, later.FirstZoneId, later.SecondZoneId,
				later.FirstInputStoreId, later.FirstOutputStoreId, later.SecondInputStoreId,
				later.SecondOutputStoreId, out later.RouteDigest));
			later.Operation.SourceInputStoreId = later.SecondInputStoreId;
			later.Operation.RouteDigest = later.RouteDigest;
			later.Revision++;
			Assert.IsTrue(KingdomPurposePortfolioRules.ValidPair(later, out fault),
				fault.ToString());
			Assert.IsFalse(KingdomPurposePortfolioRules.ValidTransition(adopted, later, out _),
				"a populated SecondWorkId permanently closes the endpoint-adoption seam");

			KingdomPurposePairReceipt firstChanged = adopted.Copy();
			firstChanged.FirstInputStoreId = "illicit-input-a";
			Assert.IsTrue(KingdomPurposePortfolioRules.TryRouteDigest(firstChanged.RealmId,
				firstChanged.FirstSettlementId, firstChanged.SecondSettlementId,
				firstChanged.FirstGateKey, firstChanged.SecondGateKey, firstChanged.FirstZoneId,
				firstChanged.SecondZoneId, firstChanged.FirstInputStoreId,
				firstChanged.FirstOutputStoreId, firstChanged.SecondInputStoreId,
				firstChanged.SecondOutputStoreId, out firstChanged.RouteDigest));
			firstChanged.Operation.DestinationInputStoreId = firstChanged.FirstInputStoreId;
			firstChanged.Operation.RouteDigest = firstChanged.RouteDigest;
			firstChanged.Revision++;
			Assert.IsTrue(KingdomPurposePortfolioRules.ValidPair(firstChanged, out fault),
				fault.ToString());
			Assert.IsFalse(KingdomPurposePortfolioRules.ValidTransition(
				adopted, firstChanged, out _), "the adoption seam cannot rewrite city one");
		}

		[Test]
		public void AccountingRejectsOverDebitAndDerivesOutstanding()
		{
			KingdomPurposePairReceipt pair = Pair();
			Assert.IsTrue(KingdomPurposePortfolioRules.TryCreateOperation(pair, "op", 1,
				KingdomPurposeKind.Deep, true, false, null, null, null, null, null,
				out var operation, out _));
			operation.Phase = KingdomPurposeOperationPhase.LocalDebitPending;
			operation.WaterSpent = 5;
			Assert.IsTrue(KingdomPurposePortfolioRules.TryOutstanding(operation,
				out int water, out int food, out _));
			Assert.AreEqual(7, water);
			Assert.AreEqual(0, food);
			operation.WaterSpent = 13;
			Assert.IsFalse(KingdomPurposePortfolioRules.ValidOperation(operation, out var fault));
			Assert.AreEqual(KingdomPurposePairFault.Accounting, fault);
		}

		private static KingdomPurposePairReceipt Pair()
		{
			Assert.IsTrue(KingdomPurposePortfolioRules.TryCreatePair("pair", "realm", 7,
				KingdomPurposeKind.Deep, KingdomPurposeKind.Forge, "city-a", "city-b", "work-deep",
				null,
				"zone-a", "zone-b", "input-a", "output-a", "input-b", "output-b",
				"gate-a", "gate-b", D, out var pair, out var fault), fault.ToString());
			return pair;
		}

		private static KingdomPurposePairReceipt Deliver(KingdomPurposePairReceipt start,
			string cargoId, string jobId, KingdomPurposePairPhase finalPhase)
		{
			KingdomPurposePairReceipt current = start;
			if (!current.Operation.BootstrapExemption && !current.Operation.ReturnExemption)
			{
				current = Advance(current, KingdomPurposeOperationPhase.InputDebitPending,
					inputBefore: D);
				current = Advance(current, KingdomPurposeOperationPhase.InputDebited,
					inputAfter: D);
			}
			current = Advance(current, KingdomPurposeOperationPhase.LocalDebitPending);
			KingdomPurposeOperationReceipt paid = current.Operation.Copy();
			paid.WaterSpent = paid.WaterRequested;
			paid.FoodSpent = paid.FoodRequested;
			paid.MaterialSpent = paid.MaterialRequested;
			paid.Phase = KingdomPurposeOperationPhase.LocalDebited;
			paid.Revision++;
			current = WithOperation(current, paid, current.Phase);
			current = Advance(current, KingdomPurposeOperationPhase.EffectPending,
				before: D);
			if (KingdomPurposePortfolioRules.TryEffectTerminalStep(
				current.Operation.SourceKind, out int terminal))
			{
				for (int step = 1; step < terminal; step++)
				{
					KingdomPurposeOperationReceipt stepped = current.Operation.Copy();
					stepped.EffectStep = step;
					stepped.Revision++;
					current = WithOperation(current, stepped, current.Phase);
				}
				KingdomPurposeOperationReceipt applied = current.Operation.Copy();
				applied.EffectStep = terminal;
				applied.Phase = KingdomPurposeOperationPhase.EffectApplied;
				applied.EffectAfterDigest = D;
				applied.Revision++;
				current = WithOperation(current, applied, current.Phase);
			}
			else current = Advance(current, KingdomPurposeOperationPhase.EffectApplied,
				after: D);
			current = Advance(current, KingdomPurposeOperationPhase.OutputPending,
				outputBefore: D);
			Assert.IsTrue(KingdomPurposePortfolioRules.TryCreateCargo(current,
				current.Operation, cargoId, jobId, out var cargo, out var fault), fault.ToString());
			KingdomPurposeOperationReceipt adopted = current.Operation.Copy();
			adopted.OutputCargoId = cargoId;
			adopted.OutputCargoReceipt = KingdomPurposePortfolioRules.EncodeCargo(cargo);
			adopted.TransportJobId = jobId;
			adopted.Revision++;
			current = WithOperation(current, adopted, current.Phase);
			current = Advance(current, KingdomPurposeOperationPhase.Dispatching,
				outputAfter: D);
			current = Advance(current, KingdomPurposeOperationPhase.PickupComplete);
			current = Advance(current, KingdomPurposeOperationPhase.LandingPending);
			current = Advance(current, KingdomPurposeOperationPhase.Delivered,
				pairPhase: finalPhase);
			return current;
		}

		private static KingdomPurposePairReceipt Advance(KingdomPurposePairReceipt pair,
			KingdomPurposeOperationPhase phase, string before = null, string after = null,
			string inputBefore = null, string inputAfter = null,
			string outputBefore = null, string outputAfter = null,
			KingdomPurposePairPhase? pairPhase = null)
		{
			KingdomPurposeOperationReceipt op = pair.Operation.Copy();
			op.Phase = phase;
			if (phase == KingdomPurposeOperationPhase.LocalDebitPending)
				op.LocalDebitReceipt = KingdomPurposePortfolioTestData.LocalDebit(op);
			if (before != null) op.EffectBeforeDigest = before;
			if (after != null) op.EffectAfterDigest = after;
			if (inputBefore != null) op.InputBeforeDigest = inputBefore;
			if (inputAfter != null) op.InputAfterDigest = inputAfter;
			if (outputBefore != null) op.OutputBeforeDigest = outputBefore;
			if (outputAfter != null) op.OutputAfterDigest = outputAfter;
			op.Revision++;
			return WithOperation(pair, op, pairPhase ?? pair.Phase);
		}

		private static KingdomPurposePairReceipt WithOperation(KingdomPurposePairReceipt pair,
			KingdomPurposeOperationReceipt operation, KingdomPurposePairPhase phase)
		{
			KingdomPurposePairReceipt next = pair.Copy();
			next.Operation = operation;
			next.Phase = phase;
			if (phase == KingdomPurposePairPhase.CargoAwaitingConsumption)
				next.NextKind = operation.DestinationKind;
			next.Revision++;
			Assert.IsTrue(KingdomPurposePortfolioRules.ValidTransition(pair, next, out var fault),
				fault.ToString() + " " + pair.Phase + "/" + pair.Operation.Phase + " -> "
				+ next.Phase + "/" + next.Operation.Phase);
			return next;
		}

		private static void AssertRecipe(KingdomPurposeKind source,
			KingdomPurposeKind destination, string key, int water, int food,
			string material, KingdomMaterial embodied, int carriedFood)
		{
			Assert.IsTrue(KingdomPurposePortfolioRules.TryRecipe(source, destination, out var row));
			Assert.AreEqual(key, row.CargoKey);
			Assert.AreEqual(water, row.WaterDrams);
			Assert.AreEqual(food, row.FoodServings);
			Assert.AreEqual(embodied, row.EmbodiedMaterial);
			Assert.AreEqual(1, row.EmbodiedUnits);
			Assert.AreEqual(carriedFood, row.CarriedFood);
			Assert.IsTrue(KingdomMaterialRules.TryParseMaterialCost(material,
				out var tally, out var error), error);
			Assert.AreEqual(new KingdomMaterialDebitCost(tally).ToClaimString(), row.MaterialClaim);
		}
	}
}
#endif
