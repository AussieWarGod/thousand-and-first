using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	internal static partial class KingdomCampHeartNativeChecks
	{
		private static Frame LoadedCamp;
		private static string NextJobId;

		internal static string BeginLoaded(XRLGame Game, KingdomCampHeartSaveSnapshot Witness)
		{
			Require(LoadedCamp == null && NextJobId == null && Witness != null, "camp load attempt already claimed");
			Require(KingdomScenarioDurableState.ProvesExactText(KingdomScenarioSaveFiles.SnapshotKey,
				KingdomScenarioLoadEntry.SnapshotWire), "loaded camp snapshot state differs from its sealed witness");
			Zone zone = The.ZoneManager?.ActiveZone;
			var observed = CaptureSaveWitness(Game, zone);
			Require(KingdomCampHeartSaveSnapshotCodec.TryEncode(observed, out string wire)
				&& wire == KingdomScenarioLoadEntry.SnapshotWire, "loaded camp objects, book, custody, water or turns differ");
			Frame frame = ObserveCamp(Game, zone);
			LoadedCamp = frame;
			frame.ContentUnits(out var bodies);
			frame.RetainedBrushBodies = new List<GameObject>();
			foreach (var body in bodies)
				if (body.Blueprint == KingdomMaterials.BlueprintFor(KingdomMaterial.Brush)) frame.RetainedBrushBodies.Add(body);
			Require(KingdomScenarioJournal.Append("camp-heart-loaded", true,
				"rung=2; basin=48; brush=21; timber=1; heart=" + observed.HeartId + "; store=" + observed.StoreId
				+ "; time-ticks=" + observed.TimeTicks + "; fire=" + observed.FireId + "; tent-job=" + observed.TentJobId + "; snapshot-sha256=" + KingdomScenarioSaveFiles.HashText(wire)) == null,
				"loaded camp proof journal unavailable");
			Require(KingdomPlots.RecoverFoundingHeart(frame.System, zone), "loaded camp founding heart cannot recover");
			Require(KingdomData.TryGetBuilding("fire", out var entry) && entry.CostDrams == 2,
				"loaded fire design or its expected bill is absent");
			Require(KingdomConstruction.TryRead(out var before, out string failure), failure ?? "construction registry unreadable");
			int water = frame.Census().StoredWater;
			Require(KingdomPlots.TryQuoteCommission(frame.System, zone, entry, null, KingdomPlotRules.PlotSize.None,
				out var quote, out failure), failure ?? "loaded camp refused a new fire quote");
			Require(KingdomMaterials.CanPay(zone, "fire", out failure), failure ?? "loaded camp cannot pay fire materials");
			Require(KingdomCommission.Commission(frame.System, "fire", null, KingdomPlotRules.PlotSize.None, quote,
				out failure), failure ?? "loaded camp refused a paid fire commission");
			Require(frame.Census().StoredWater == water - entry.CostDrams, "loaded fire did not debit exactly two drams");
			Require(KingdomConstruction.TryRead(out var after, out failure), failure ?? "paid registry unreadable");
			Require(KingdomQuickstartBuildCensus.TryNewPaidJob(before, after, zone, frame.System, "fire", entry.CostDrams,
				quote, out var job, out failure), failure ?? "loaded commission did not mint exactly one paid job");
			var timberBill = new KingdomMaterialTally();
			timberBill.Add(KingdomMaterial.Timber, 1);
			Require(job.Id != Witness.UpgradeJobId && job.Id != Witness.TentJobId
				&& job.Claims.MaterialSpent == new KingdomMaterialDebitCost(timberBill).ToClaimString(),
				"loaded job reused the upgrade or paid a different material bill");
			NextJobId = job.Id;
			PreservedLoaded(Game, Witness);
			return "new-job=" + job.Id + "; upgrade-job=" + observed.UpgradeJobId
				+ "; water-debited=2; timber-debited=1; synthetic-materials-after-load=0";
		}

		internal static string CompleteLoaded(XRLGame Game, KingdomCampHeartSaveSnapshot Witness)
		{
			Frame frame = PreservedLoaded(Game, Witness);
			Require(KingdomConstruction.TryRead(out var jobs, out string failure), failure ?? "completed registry unreadable");
			KingdomConstructionJob finished = null;
			foreach (var job in jobs)
			{
				if (job?.Id != NextJobId) continue;
				Require(finished == null, "loaded next job has duplicate registry identities");
				finished = job;
			}
			Require(finished != null && finished.Phase == KingdomConstructionPhase.Complete && finished.PhysicalPhase == KingdomPhysicalPhase.EffectsSettled
				&& finished.OwnerKey == KingdomConstruction.OwnerOf(frame.System) && finished.ZoneId == frame.Zone.ZoneID
				&& finished.TargetKey == "fire" && finished.Route == KingdomConstructionRoute.PlotCommission,
				"loaded next fire job did not complete and settle its effects");
			Require(KingdomConstruction.FindExactId(frame.Zone, finished.OutputId, out var output) == KingdomPhysicalLookupState.Exact,
				"loaded completed fire output is absent or ambiguous");
			ExactGround(frame.Zone, output);
			Require(KingdomConstruction.HasReceipt(output, finished) && KingdomUpgrade.IsFunctionallyBuilt(output)
				&& KingdomUpgrade.DesignKeyOf(output) == "fire" && output.CurrentCell.X == finished.X
				&& output.CurrentCell.Y == finished.Y && !ReferenceEquals(output, frame.Fire),
				"loaded completed fire has no distinct functional standing output with its own receipt");
			return "new-job=" + finished.Id + "; phase=" + finished.Phase + "; effects-settled=true; output="
				+ output.IDIfAssigned + "; heart=" + frame.HeartId + "; store=" + frame.StoreId
				+ "; fire=" + frame.FireId + "; brush=21; turns=" + Game.Turns;
		}

		private static Frame PreservedLoaded(XRLGame Game, KingdomCampHeartSaveSnapshot Witness)
		{
			Require(LoadedCamp != null && NextJobId != null && ReferenceEquals(LoadedCamp.Game, Game),
				"loaded camp no longer owns this next-job attempt");
			Frame frame = ObserveCamp(Game, LoadedCamp.Zone);
			Require(ReferenceEquals(frame.System, LoadedCamp.System) && ReferenceEquals(frame.Heart, LoadedCamp.Heart)
				&& ReferenceEquals(frame.Store, LoadedCamp.Store) && ReferenceEquals(frame.Fire, LoadedCamp.Fire)
				&& frame.HeartId == Witness.HeartId && frame.StoreId == Witness.StoreId && frame.FireId == Witness.FireId
				&& frame.JobId == Witness.UpgradeJobId && frame.TentJobId == Witness.TentJobId && frame.System.RealmId == Witness.RealmId
				&& KingdomConstruction.OwnerOf(frame.System) == Witness.CityId && frame.Zone.ZoneID == Witness.ZoneId
				&& frame.Heart.CurrentCell.X == Witness.HeartX && frame.Heart.CurrentCell.Y == Witness.HeartY
				&& frame.StoreCell.X == Witness.StoreX && frame.StoreCell.Y == Witness.StoreY
				&& frame.FireCell.X == Witness.FireX && frame.FireCell.Y == Witness.FireY,
				"loaded paid work replaced or moved the saved camp");
			var units = frame.ContentUnits(out var bodies);
			Require(units.Count == SavedBrushUnits
				&& KingdomCampHeartSaveSnapshotCodec.CustodyDigest(units) == Witness.BrushDigest,
				"loaded paid work changed unspent brush or failed to remove the timber");
			frame.RequireSameBodies(LoadedCamp.RetainedBrushBodies, bodies, "loaded brush");
			return frame;
		}
	}
}
