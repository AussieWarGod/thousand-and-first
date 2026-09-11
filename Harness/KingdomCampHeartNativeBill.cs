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

			// Read the real job and scaffold at each boundary, including a failed completion.
			// A duration estimate is not evidence that work advanced or that handover succeeded.
			private void RecordJobProgress()
			{
				try
				{
					Evidence.Append("\njob-progress tick=").Append(Game.TimeTicks)
						.Append("; turns=").Append(Game.Turns).Append("; job=").Append(JobId);
					if (!KingdomConstruction.TryFind(JobId, out var job) || job == null)
					{ Evidence.Append("; row=absent"); return; }
					Evidence.Append("; phase=").Append(job.Phase).Append("; physical=")
						.Append(job.PhysicalPhase).Append("; started=").Append(job.StartedTick)
						.Append("; due=").Append(job.DueTick).Append("; updated=").Append(job.UpdatedTick)
						.Append("; failure=").Append(KingdomScenarioRules.Bounded(job.Failure));
					Evidence.Append("; input-receipt-present=").Append(!string.IsNullOrEmpty(job.InputReceipt));
					if (KingdomConstructionRules.TryGetInputReceipt(job, out var input))
						Evidence.Append("; input-phase=").Append(input.TxPhase);
					var improvement = Heart?.GetPart<XRL.World.Parts.r_KingdomImprovement>();
					var scaffold = improvement?.Scaffold?.GetPart<XRL.World.Parts.r_KingdomScaffold>();
					Evidence.Append("; improvement-working=").Append(improvement?.Working)
						.Append("; improvement-due=").Append(improvement?.WorkCompleteTick)
						.Append("; scaffold=").Append(improvement?.Scaffold?.IDIfAssigned)
						.Append("; remaining=").Append(scaffold?.RemainingTicks)
						.Append("; last-worked=").Append(scaffold?.LastWorkedTick);
				}
				catch (Exception error)
				{
					Evidence.Append("; progress-read-error=")
						.Append(KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message));
				}
			}

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
				RequireExactMaterial();
				// Lost is physical net debit, not additional waste. This clean first attempt
				// must debit exactly the authored bill, with no outstanding or excess loss.
				Require(KingdomQuickstartBuildClaims.CleanFirstPayment(found.Claims,
					AuthoredWaterCost, new KingdomMaterialDebitCost(
						KingdomMaterials.UpgradeCostFor(FirstRungKey))),
					"taf-camp-bill-payment-inexact: water requested/spent/outstanding/lost="
						+ found.Claims.WaterRequested + "/" + found.Claims.WaterSpent + "/"
						+ found.Claims.WaterOutstanding + "/" + found.Claims.WaterLost
						+ "; material requested/spent/outstanding/lost="
						+ found.Claims.MaterialRequested + "/" + found.Claims.MaterialSpent
						+ "/" + found.Claims.MaterialOutstanding + "/" + found.Claims.MaterialLost);
				ProveTypedPredecessor(found);
			}

			private void ProveTypedPredecessor(KingdomConstructionJob Job)
			{
				Require(Job.Payload != null && Job.Payload.StartsWith("v2|", StringComparison.Ordinal)
					&& KingdomUpgrade.IsImprovementPredecessorIdentity(System, Zone, Heart, Job),
					"taf-camp-typed-predecessor-positive-unreached");
				// Mutate detached copies only. No malformed receipt is published into the world.
				var malformed = Job.Copy();
				malformed.Payload = "v2|torn";
				Require(!KingdomUpgrade.IsImprovementPredecessorIdentity(System, Zone, Heart, malformed),
					"taf-camp-malformed-payload-accepted");
				var stale = Job.Copy();
				stale.Payload = FirstRungKey;
				Require(!KingdomUpgrade.IsImprovementPredecessorIdentity(System, Zone, Heart, stale),
					"taf-camp-authored-legacy-payload-accepted");
				var foreign = Job.Copy();
				foreign.SubjectId = "not-the-paid-heart";
				Require(!KingdomUpgrade.IsImprovementPredecessorIdentity(System, Zone, Heart, foreign),
					"taf-camp-foreign-predecessor-accepted");
				Require(KingdomConstruction.IsCurrent(Job), "taf-camp-probe-mutated-live-job");
				Evidence.Append("\ntyped-predecessor-accepted=true; malformed-payload-refused=true")
					.Append("; stale-plain-payload-refused=true; foreign-predecessor-refused=true")
					.Append("; live-job-unchanged=true");
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
