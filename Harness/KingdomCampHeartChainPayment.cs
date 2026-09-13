using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	internal static partial class KingdomCampHeartNativeChecks
	{
		private sealed partial class Frame
		{
			private void SupplyChain(int Target)
			{
				Require(Target == 3 || Target == 4, "unknown heart chain target");
				ChainTarget = Target; ChainFrom = Target == 3 ? "heartwaterstone" : "heartmoot";
				ChainTo = Target == 3 ? "heartmoot" : "heartcourt";
				ChainWater = Target == 3 ? 28 : 50;
				ChainHeart = StandingHeart(); ChainHeartId = ChainHeart.IDIfAssigned;
				Require(KingdomUpgrade.DesignKeyOf(ChainHeart) == ChainFrom
					&& ChainStore?.Inventory != null && ChainStore.Inventory.Objects.Count == 0,
					"supply requires its predecessor and empty dedicated supplemental store");
				var tally = KingdomMaterials.UpgradeCostFor(ChainFrom);
				Require(tally.Total() == (Target == 3 ? 25 : 115), "authored heart material quantity changed");
				ChainSupplyClaim = new KingdomMaterialDebitCost(tally).ToClaimString();
				ChainSupplied.Clear();
				foreach (KingdomMaterial material in Enum.GetValues(typeof(KingdomMaterial)))
					for (int i = 0; i < tally.Get(material); i++)
					{
						var unit = Create(KingdomMaterials.BlueprintFor(material));
						string id = unit.ID;
						Require(!string.IsNullOrEmpty(id) && unit.IDIfAssigned == id
							&& ReferenceEquals(ChainStore.Inventory.AddObject(unit, null, Silent: true, NoStack: true), unit)
							&& ReferenceEquals(unit.InInventory, ChainStore) && unit.CurrentCell == null,
							"synthetic chain material did not retain exact custody");
						ChainSupplied.Add(unit);
					}
				Require(KingdomMaterials.CanPayUpgrade(Zone, ChainFrom, out string failure), failure);
				var assessment = KingdomUpgrade.Assess(System, Zone, ChainHeart, Census(), 50, false);
				Require(KingdomUpgradeRules.IsReady(assessment.Verdict), "supplied heart preflight refused: "
					+ assessment.Verdict + "; reason=" + assessment.Reason);
			}

			private void CheckChainPaid()
			{
				RequireChainSupport();
				Require(System.Stage == GrowthStage.City, "ordinary stage update did not observe supported City");
				Require(KingdomConstruction.TryRead(out var jobs, out string failure), failure);
				KingdomConstructionJob found = null;
				foreach (var job in jobs)
					if (job != null && job.Route == KingdomConstructionRoute.Improvement
						&& job.SubjectId == ChainHeartId && job.TargetKey == ChainTo
						&& KingdomConstruction.Owns(System, Zone, job))
					{
						Require(found == null, "multiple improvement receipts name the chain predecessor"); found = job;
					}
				if (found == null)
				{
					var assessment = KingdomUpgrade.Assess(System, Zone, ChainHeart, Census(), 50, false);
					Require(false, "ordinary settlement pass did not begin paid " + ChainFrom + "->" + ChainTo
						+ "; assessment=" + assessment.Verdict + "; reason=" + assessment.Reason);
				}
				Require(found.Id != JobId && found.Id != ChainJobId
					&& KingdomQuickstartBuildClaims.CleanFirstPayment(found.Claims, ChainWater,
						new KingdomMaterialDebitCost(KingdomMaterials.UpgradeCostFor(ChainFrom)))
					&& found.Claims.MaterialSpent == ChainSupplyClaim,
					"chain improvement payment differs from its exact authored bill");
				ChainJobId = found.Id;
				foreach (var supplied in ChainSupplied)
					Require(!ChainStore.Inventory.Objects.Contains(supplied), "billed unit still in supplemental store");
				RequireChainCustody();
			}

			private void CheckChainComplete()
			{
				RequireChainSupport();
				Require(KingdomConstruction.TryFind(ChainJobId, out var job) && job != null
					&& job.Phase == KingdomConstructionPhase.Complete
					&& job.PhysicalPhase == KingdomPhysicalPhase.EffectsSettled
					&& KingdomConstruction.Owns(System, Zone, job) && job.SubjectId == ChainHeartId
					&& job.TargetKey == ChainTo && job.Route == KingdomConstructionRoute.Improvement,
					"paid chain handover did not finish: " + ChainJobId + "; phase=" + job?.Phase + "; failure=" + job?.Failure);
				var standing = StandingHeart();
				Require(standing.IDIfAssigned == job.OutputId && standing.IDIfAssigned != ChainHeartId
					&& KingdomConstruction.HasReceipt(standing, job) && KingdomUpgrade.IsFunctionallyBuilt(standing)
					&& KingdomUpgrade.DesignKeyOf(standing) == ChainTo && KingdomPlots.HeartRung(Zone) == ChainTarget,
					"paid chain successor identity, receipt, functionality or settled rung differs");
				Require(KingdomArchitectureStamper.TryVerifyComplete(standing, Zone, out string failure), failure);
				Require(KingdomArchitectureStamper.TryExactAnchoredComponent(standing, Zone,
					KingdomPlots.HeartBasinRole, out var basin, out failure)
					&& ReferenceEquals(basin, ChainBasin)
					&& BasinCapacity(standing) == (ChainTarget == 3 ? "160" : "512"),
					"paid chain lost its original basin or expected capacity: " + failure);
				Require(KingdomArchitectureStamper.TryExactAnchoredComponent(standing, Zone,
					StorageRole, out var store, out failure) && ReferenceEquals(store, Store), failure);
				RequireChainCustody();
			}

			private void RequireChainCustody()
			{
				RequireStoreIdentity();
				var units = ContentUnits(out var bodies);
				Require(KingdomCampHeartSaveSnapshotCodec.CustodyDigest(units) == ChainBrushDigest,
					"the original brush custody changed during higher-rung improvement");
				RequireSameBodies(ChainBrush, bodies, "chain sentinel brush");
			}
		}
	}
}
