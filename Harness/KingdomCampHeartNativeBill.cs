using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>The production bill, read off the production receipt. A store that no longer
	/// holds a unit has only stopped holding it; what proves the settlement SPENT it is the
	/// construction job's own committed claim, which is what this shard reads.</summary>
	internal static partial class KingdomCampHeartNativeChecks
	{
		private sealed partial class Frame
		{
			internal string JobId;
			internal int BilledWater;
			internal string BilledMaterial;
			internal string AuthoredMaterial;

			/// <summary>Finds the settlement's own active Improvement job for this heart and
			/// proves it committed EXACTLY the authored water and material bill. Water drawn by
			/// ordinary resident upkeep never appears here: <c>Claims.WaterSpent</c> is the
			/// construction receipt's own committed debit. An overcharge is a fault in both
			/// directions, and a charged material the authored bill never names is a fault too.
			/// </summary>
			internal void RequireBillDebited()
			{
				List<KingdomConstructionJob> jobs;
				Require(KingdomConstruction.TryOwnedActive(System, Zone, out jobs) && jobs != null,
					"taf-camp-bill-unreadable: the construction registry refused to be read");
				KingdomConstructionJob found = null;
				for (int i = 0; i < jobs.Count; i++)
				{
					KingdomConstructionJob job = jobs[i];
					if (job == null || job.Route != KingdomConstructionRoute.Improvement
						|| job.SubjectId != HeartId) continue;
					Require(found == null,
						"taf-camp-bill-ambiguous: two improvement jobs claim this heart");
					found = job;
				}
				Require(found != null, "taf-camp-bill-absent: no active improvement job names the "
					+ "heart as its subject");
				Require(found.TargetKey == SecondRungKey,
					"taf-camp-bill-wrong-target: the improvement job targets " + found.TargetKey);
				Require(found.Claims != null,
					"taf-camp-bill-unclaimed: the improvement job carries no claim record");
				JobId = found.Id;
				BilledWater = found.Claims.WaterSpent;
				BilledMaterial = found.Claims.MaterialSpent ?? "";
				// EXACT, not "at least". The authored bill is the whole bill.
				Require(BilledWater == AuthoredWaterCost,
					"taf-camp-bill-water-inexact: the committed claim spent " + BilledWater
						+ " dram(s) against the authored " + AuthoredWaterCost);
				Require(found.Claims.WaterOutstanding == 0 && found.Claims.WaterLost == 0,
					"taf-camp-bill-water-unsettled: outstanding="
						+ found.Claims.WaterOutstanding + " lost=" + found.Claims.WaterLost);
				RequireExactMaterial();
				Require(string.IsNullOrEmpty(found.Claims.MaterialOutstanding)
					&& string.IsNullOrEmpty(found.Claims.MaterialLost),
					"taf-camp-bill-material-unsettled: outstanding="
						+ found.Claims.MaterialOutstanding + " lost=" + found.Claims.MaterialLost);
			}

			/// <summary>The committed material claim, compared kind by kind against the authored
			/// transition cost read from the production catalogue. Every material the production
			/// enum knows is compared, so an extra charged kind cannot hide.</summary>
			private void RequireExactMaterial()
			{
				KingdomMaterialDebitCost spent = null;
				Require(KingdomMaterialDebitCost.TryParseClaim(BilledMaterial, out spent)
					&& spent != null && spent.Materials != null,
					"taf-camp-bill-material-unreadable: '"
						+ KingdomScenarioRules.Bounded(BilledMaterial) + "'");
				KingdomMaterialTally authored = KingdomMaterials.UpgradeCostFor(FirstRungKey);
				Require(authored != null,
					"taf-camp-bill-authored-absent: the authored transition cost is missing");
				List<KingdomCampHeartNativeCensus.Charge> want =
					new List<KingdomCampHeartNativeCensus.Charge>();
				List<KingdomCampHeartNativeCensus.Charge> got =
					new List<KingdomCampHeartNativeCensus.Charge>();
				foreach (KingdomMaterial material
					in Enum.GetValues(typeof(KingdomMaterial)) as KingdomMaterial[])
				{
					int authoredUnits = authored.Get(material);
					if (authoredUnits != 0) want.Add(
						new KingdomCampHeartNativeCensus.Charge(material.ToString(),
							authoredUnits));
					int spentUnits = spent.Materials.Get(material);
					if (spentUnits != 0) got.Add(
						new KingdomCampHeartNativeCensus.Charge(material.ToString(), spentUnits));
				}
				List<string> faults = KingdomCampHeartNativeCensus.BillFaults(want, got);
				Require(faults.Count == 0, "taf-camp-bill-material-inexact: "
					+ KingdomCampHeartNativeCensus.Join(faults, 6));
				AuthoredMaterial = KingdomCampHeartNativeCensus.DescribeCharges(want);
			}
		}
	}
}
