using System;
using System.Collections.Generic;
using XRL.World;
using ThousandAndFirst.Simulation.City;

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
				Require(tally.Total() == (Target == 3 ? 25 : 121), "authored heart material quantity changed");
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
				var assessment = AssessChain(out string context);
				Require(KingdomUpgradeRules.IsReady(assessment.Verdict), "supplied heart preflight refused: "
					+ assessment.Verdict + "; reason=" + assessment.Reason + "; " + context);
				if (Target == 3) { ProveChainEnvelopeOccupancy(); ProveChainRoadWear(); }
				else ProveChainSurveyStakes();
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
					var assessment = AssessChain(out string context);
					Require(false, "ordinary settlement pass did not begin paid " + ChainFrom + "->" + ChainTo
						+ "; assessment=" + assessment.Verdict + "; reason=" + assessment.Reason
						+ "; " + context);
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
				Require(KingdomPlots.RecoverFoundingHeart(System, Zone),
					"founding recovery refused while the next paid heart improvement is working");
				if (ChainTarget == 3)
					KingdomCampHeartChainHandoverOccupancy.Arm(FixtureResidents[0], ChainJobId);
			}

			private void CheckChainComplete()
			{
				RequireChainTrack();
				RequireChainSupport();
				if (ChainTarget == 3) Require(KingdomCampHeartChainHandoverOccupancy.Proved,
					"post-payment resident clearance and refusal cases were not witnessed");
				if (ChainTarget == 3) Require(KingdomCampHeartChainRetryFault.Proved,
					"controlled obstruction and Outstanding handover retry were not witnessed");
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

			private KingdomUpgrade.Assessment AssessChain(out string Context)
			{
				var survey = Census();
				var active = new List<string>();
				foreach (var root in survey.Improvements)
					if (root.GetPart<XRL.World.Parts.r_KingdomImprovement>()?.Working == true)
						active.Add("working:" + root.IDIfAssigned + ":" + root.Blueprint);
				foreach (var root in survey.Built)
					if (KingdomConstruction.ReceiptBlocksCurrent(root))
						active.Add("receipt:" + root.IDIfAssigned + ":" + root.Blueprint);
				int free = Math.Max(0, System.Population - System.AssignedCrew);
				var assessment = KingdomUpgrade.Assess(System, Zone, ChainHeart, survey, free, active.Count > 0);
				var part = ChainHeart.GetPart<XRL.World.Parts.r_KingdomImprovement>();
				Context = "population=" + System.Population + "; assigned=" + System.AssignedCrew
					+ "; water-crew=" + System.WaterCrew + "; free=" + free
					+ "; needed=" + assessment.CrewNeeded + "; competing=" + string.Join(",", active)
					+ "; announced=" + (part == null ? "absent"
						: ((KingdomUpgradeRules.UpgradeVerdict)part.AnnouncedReason).ToString())
					+ "; enabled=" + KingdomUpgrade.Enabled
					+ "; automatic-work=" + KingdomMaster.AutomaticWorkAllowed(System);
				if (assessment.Successor != null)
					Context += "; zoning=" + KingdomZoning.Judge(System, Zone.ZoneID, assessment.Successor).Verdict;
				var notes = System.Ledger.Notes;
				for (int i = Math.Max(0, notes.Count - 8); i < notes.Count; i++)
					Context += "; ledger=" + notes[i];
				if (!KingdomUpgradeRules.IsReady(assessment.Verdict))
					Context += ChainGroundFailureContext(assessment.Reason);
				return assessment;
			}

			private string ChainGroundFailureContext(string Reason)
			{
				int at = Reason?.LastIndexOf(" at ", StringComparison.Ordinal) ?? -1;
				if (at < 0) return "; ground-witness=no-coordinate";
				string[] point = Reason.Substring(at + 4).Split(',');
				if (point.Length != 2 || !int.TryParse(point[0], out int x)
					|| !int.TryParse(point[1], out int y) || x < 0 || x >= Zone.Width
					|| y < 0 || y >= Zone.Height) return "; ground-witness=unparsed-coordinate";
				var cell = Zone.GetCell(x, y);
				if (cell == null) return "; ground-witness=missing-cell";
				string detail = "; ground-witness=" + x + "," + y;
				if (KingdomPlots.TryReadRect(ChainHeart, out var before))
					detail += "; inside-predecessor=" + before.Contains(x, y);
				foreach (var item in cell.GetObjects())
				{
					if (!GameObject.Validate(item)) { detail += "; invalid-object=true"; continue; }
					detail += "; object=" + item.IDIfAssigned + "/" + item.Blueprint
						+ "/ground=" + KingdomPlots.ReadObject(item)
						+ "/creature=" + item.IsCreature + "/player=" + item.IsPlayer()
						+ "/citizen=" + KingdomCitizenship.BelongsTo(System, item)
						+ "/resident=" + KingdomResidents.IdOf(item)
						+ "/fixture=" + FixtureResidents.Contains(item)
						+ "/heart-stake=" + item.GetIntProperty(KingdomPlots.HeartStakeProperty)
						+ "/plot=" + item.GetStringProperty(KingdomPlots.PlotIdProperty)
						+ "/slot=" + item.GetStringProperty(KingdomArchitectureStamper.ComponentSlotProperty);
				}
				return detail;
			}
		}
	}
}
