using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	internal static partial class KingdomCampHeartNativeChecks
	{
		private static Frame LoadedChain;
		private static string LoadedChainJob;
		private static List<GameObject> LoadedChainBrush;

		internal static string BeginLoadedChain(XRLGame Game, KingdomCampHeartChainSnapshot Witness)
		{
			Require(LoadedChain == null && LoadedChainJob == null && Witness != null,
				"higher-heart load attempt already claimed or witness missing");
			var observed = CaptureChainSaveFrame(Game, The.ZoneManager?.ActiveZone, Witness.ResidentId,
				Witness.Track.Id, out var records, out var frame);
			KingdomCampHeartChainLoadFacts.Retain(Game, "activated", records);
			KingdomCampHeartChainLoadFacts.RequireExact(observed, KingdomScenarioLoadEntry.SnapshotWire);
			LoadedChain = frame;
			var units = frame.ContentUnits(out var bodies);
			LoadedChainBrush = new List<GameObject>();
			GameObject timber = null;
			foreach (var body in bodies)
			{
				Require(KingdomMaterials.RawCensusCountOf(body) == 1, "saved higher-heart stock is not individual units");
				if (body.Blueprint == KingdomMaterials.BlueprintFor(KingdomMaterial.Brush)) LoadedChainBrush.Add(body);
				else
				{
					Require(timber == null && body.Blueprint == KingdomMaterials.BlueprintFor(KingdomMaterial.Timber),
						"saved higher-heart stock contains an unexpected material");
					timber = body;
				}
			}
			Require(units.Count == 22 && LoadedChainBrush.Count == 21 && timber != null,
				"saved higher-heart stock lacks the original brush or next-job timber");
			Require(KingdomScenarioJournal.Append("camp-heart-chain-loaded", true,
				KingdomCampHeartChainLoadFacts.Identity(observed) + "; brush=21; timber=1; snapshot-sha256="
				+ KingdomScenarioSaveFiles.HashText(KingdomScenarioLoadEntry.SnapshotWire)) == null,
				"higher-heart loaded observation could not be journalled");
			Require(KingdomPlots.RecoverFoundingHeart(frame.System, frame.Zone), "loaded higher heart cannot recover");
			Require(KingdomData.TryGetBuilding("fire", out var entry) && entry.CostDrams == 2,
				"loaded higher-heart fire design differs");
			Require(KingdomConstruction.TryRead(out var before, out string failure), failure);
			int water = frame.Census().StoredWater;
			Require(KingdomPlots.TryQuoteCommission(frame.System, frame.Zone, entry, null, KingdomPlotRules.PlotSize.None,
				out var quote, out failure), failure);
			Require(KingdomPlots.TryHeartRectFor(frame.Zone, 4, out var heartRect)
				&& !KingdomPlotRules.Overlaps(heartRect, KingdomPlotRules.Reserved(quote.Rect)),
				"loaded next fire quote encroaches on completed heart ground");
			Require(KingdomMaterials.CanPay(frame.Zone, "fire", out failure), failure);
			Require(KingdomCommission.Commission(frame.System, "fire", null, KingdomPlotRules.PlotSize.None, quote,
				out failure), failure);
			Require(frame.Census().StoredWater == water - 2 && !frame.Store.Inventory.Objects.Contains(timber),
				"loaded higher-heart fire did not debit exactly two drams and its saved timber");
			Require(KingdomConstruction.TryRead(out var after, out failure), failure);
			Require(KingdomQuickstartBuildCensus.TryNewPaidJob(before, after, frame.Zone, frame.System, "fire", 2,
				quote, out var job, out failure), failure);
			var bill = new KingdomMaterialTally(); bill.Add(KingdomMaterial.Timber, 1);
			Require(job.Id != Witness.JobId && KingdomQuickstartBuildClaims.CleanFirstPayment(job.Claims, 2,
				new KingdomMaterialDebitCost(bill)), "loaded next job reused a receipt or paid a different bill");
			LoadedChainJob = job.Id;
			PreservedLoadedChain(Game, Witness, false);
			return "new-job=" + job.Id + "; prior-job=" + Witness.JobId
				+ "; water-debited=2; timber-debited=1; synthetic-materials-after-load=0; prior-jobs-retained=true";
		}

		internal static string CompleteLoadedChain(XRLGame Game, KingdomCampHeartChainSnapshot Witness)
		{
			var observed = PreservedLoadedChain(Game, Witness, true);
			Require(KingdomConstruction.TryFind(LoadedChainJob, out var job) && job != null
				&& KingdomConstruction.Owns(LoadedChain.System, LoadedChain.Zone, job)
				&& job.Phase == KingdomConstructionPhase.Complete && job.PhysicalPhase == KingdomPhysicalPhase.EffectsSettled
				&& job.TargetKey == "fire" && job.Route == KingdomConstructionRoute.PlotCommission,
				"loaded higher-heart next job did not complete and settle");
			Require(KingdomConstruction.FindExactId(LoadedChain.Zone, job.OutputId, out var output)
				== KingdomPhysicalLookupState.Exact, "loaded higher-heart next output missing or ambiguous");
			ExactGround(LoadedChain.Zone, output);
			Require(KingdomConstruction.HasReceipt(output, job) && KingdomUpgrade.IsFunctionallyBuilt(output)
				&& KingdomUpgrade.DesignKeyOf(output) == "fire" && output.CurrentCell.X == job.X && output.CurrentCell.Y == job.Y
				&& output.IDIfAssigned != Witness.Heart.Id && output.IDIfAssigned != Witness.Basin.Id
				&& output.IDIfAssigned != Witness.Store.Id && output.IDIfAssigned != Witness.Track.Id
				&& output.IDIfAssigned != Witness.ResidentId,
				"loaded higher-heart completed output reused or lost physical identity");
			Require(KingdomArchitectureStamper.TryVerifyComplete(output, LoadedChain.Zone, out string failure), failure);
			var bill = new KingdomMaterialTally(); bill.Add(KingdomMaterial.Timber, 1);
			Require(KingdomQuickstartBuildClaims.CleanFirstPayment(job.Claims, 2, new KingdomMaterialDebitCost(bill)),
				"loaded next-job completion altered its exact paid claims");
			return KingdomCampHeartChainLoadFacts.Identity(observed) + "; new-job=" + job.Id + "; output=" + output.IDIfAssigned
				+ "; phase=Complete; effects-settled=true; brush=21; prior-jobs-retained=true; original-bodies-retained=true";
		}

		private static KingdomCampHeartChainSnapshot PreservedLoadedChain(XRLGame Game,
			KingdomCampHeartChainSnapshot Witness, bool Final)
		{
			Require(LoadedChain != null && LoadedChainJob != null && ReferenceEquals(LoadedChain.Game, Game),
				"loaded higher-heart continuation lost its game or job");
			var observed = CaptureChainSaveFrame(Game, LoadedChain.Zone, Witness.ResidentId, Witness.Track.Id,
				out var records, out var frame);
			frame.RequireLoadedChainPreserved(LoadedChain, Witness, LoadedChainJob);
			frame.ContentUnits(out var brush);
			frame.RequireSameBodies(LoadedChainBrush, brush, "loaded higher-heart original brush");
			Require(observed.GameId == Witness.GameId && observed.RealmId == Witness.RealmId && observed.CityId == Witness.CityId
				&& observed.ZoneId == Witness.ZoneId && observed.Rung == Witness.Rung && observed.JobId == Witness.JobId
				&& observed.Population == Witness.Population, "loaded higher-heart city identity or population changed");
			if (!Final) Require(observed.Turns == Witness.Turns && observed.TimeTicks == Witness.TimeTicks,
				"loaded higher-heart commission advanced the clock before its ordinary wait");
			foreach (var pair in new[] { new[] { observed.Heart, Witness.Heart }, new[] { observed.Basin, Witness.Basin },
				new[] { observed.Store, Witness.Store }, new[] { observed.Track, Witness.Track } })
				Require(pair[0].Id == pair[1].Id && pair[0].X == pair[1].X && pair[0].Y == pair[1].Y,
					"loaded higher-heart original anchor changed");
			if (Final) KingdomCampHeartChainLoadFacts.Retain(Game, "completed", records);
			return observed;
		}

		private sealed partial class Frame
		{
			internal void RequireLoadedChainPreserved(Frame Original, KingdomCampHeartChainSnapshot Witness, string NewJob)
			{
				Require(ReferenceEquals(System, Original.System) && ReferenceEquals(Heart, Original.Heart)
					&& ReferenceEquals(Store, Original.Store) && ReferenceEquals(ChainBasin, Original.ChainBasin)
					&& ReferenceEquals(ChainTrack, Original.ChainTrack), "loaded paid work replaced an original anchor body");
				RequireSameBodies(Original.ChainResidents, ChainResidents, "loaded higher-heart original residents");
				var prior = new Dictionary<string, string>(StringComparer.Ordinal);
				Require(CaptureChainJobs(prior, NewJob) == Witness.JobsDigest,
					"loaded paid work altered a prior paid receipt or introduced another job");
			}
		}
	}
}
