using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// The cold-load session: the second, separate process. It loads the save the lifecycle
	/// session wrote, re-proves by reference what that session witnessed, and then takes a real
	/// further action on the loaded world.
	///
	/// <para>Nothing here restores anything from the witness. The witness is compared against, not
	/// applied: every identity and count is read again from the loaded game, and a difference is a
	/// refusal rather than a repair. The next action commissions a NEW job with its own identity;
	/// it never reuses or revives the completed one, and a production refusal is journalled as it
	/// came rather than retried into a pass.</para>
	/// </summary>
	internal static partial class KingdomQuickstartLifecycleLoad
	{
		internal const string LoadedRow = "lifecycle-loaded";
		internal const string NextRow = "lifecycle-next";

		/// <summary>Cold-load proof: same realm and city, the completed job's receipt still
		/// linkable to the building standing on its own cell, that building still reading as
		/// built under the commissioned design key, the dedicated stockpile still holding its
		/// post-debit counts, and the same save identity.</summary>
		internal static void VerifyLoaded(XRLGame Game, KingdomQuickstartLifecycleSnapshot Witness)
		{
			string failure = Compare(Game, Witness, out string observed);
			KingdomScenarioJournal.Append(LoadedRow, failure == null, failure == null
				? KingdomQuickstartLifecycleSteps.Stamped("native-lifecycle step=cold-load; " + observed)
				: KingdomQuickstartLifecycleSteps.Refuse("cold-load", failure));
			if (failure != null)
				throw new InvalidOperationException("cold-load proof refused: " + failure);
		}

		private static string Compare(XRLGame Game, KingdomQuickstartLifecycleSnapshot Witness,
			out string Observed)
		{
			Observed = null;
			if (Game == null || Witness == null) return "no loaded game or no save witness";
			if (Game.GameID != Witness.GameId)
				return "the loaded game is not the saved game: saveId differs";
			KingdomSystem system = Game.GetSystem<KingdomSystem>();
			if (system == null || !system.Founded)
				return "the loaded game carries no founded settlement";
			if (system.RealmId != Witness.RealmId)
				return "the loaded realm identity differs from the saved one";
			string cityId = KingdomConstruction.OwnerOf(system);
			if (cityId != Witness.CityId)
				return "the loaded settlement identity differs from the saved one";
			Zone zone = The.ZoneManager?.ActiveZone;
			if (zone == null || zone.ZoneID != Witness.ZoneId)
				return "the loaded active zone is not the zone that was saved";
			if (!KingdomConstruction.TryRead(out List<KingdomConstructionJob> jobs, out string readFailure))
				return readFailure ?? "the construction registry could not be read after loading";
			KingdomConstructionJob job = null;
			foreach (KingdomConstructionJob candidate in jobs)
				if (candidate?.Id == Witness.JobId) job = candidate;
			Cell cell = zone.GetCell(Witness.X, Witness.Y);
			if (cell == null) return "the saved building's own cell is not in the loaded zone";
			GameObject building = null;
			foreach (GameObject item in cell.GetObjects())
			{
				if (!GameObject.Validate(item) || item.IDIfAssigned != Witness.BuildingId) continue;
				if (building != null) return "two loaded objects claim the saved building identity";
				building = item;
			}
			if (building == null)
				return "the saved building identity does not stand on its own cell after loading";
			// Custody by reference: the object's own Physics cell and that cell's own zone, not
			// an x,y match. The observed values below are read from THIS object, never echoed
			// from the witness.
			if (building.Physics == null || building.Physics._CurrentCell == null
				|| !ReferenceEquals(building.Physics._CurrentCell, cell)
				|| !ReferenceEquals(building.Physics._CurrentCell.ParentZone, zone))
				return "the loaded building is not physically held by its own saved cell and zone";
			if (building.GetIntProperty("KingdomBuilt") != 1)
				return "the loaded building no longer reads as a finished settlement building";
			// The production predicate as-is, Growth/KingdomUpgrade.09.RegistryAndIdentity.cs:165-169.
			if (!KingdomUpgrade.IsFunctionallyBuilt(building))
				return "the loaded building is not functionally built by the production predicate";
			if (building.GetStringProperty(KingdomUpgrade.BuildKeyProperty) != Witness.DesignKey)
				return "the loaded building's design key differs from the commissioned one";
			// The completed job row may be compacted out of the live registry by the load, but
			// the BUILDING keeps its construction receipt property either way
			// (Growth/KingdomConstruction.Physical.cs:10-23), so the link is read from the
			// standing object rather than skipped when no row survives.
			string receipt = building.GetStringProperty(KingdomConstruction.ReceiptProperty);
			if (receipt != Witness.JobId)
				return "the loaded building's construction receipt names " + Describe(receipt)
					+ ", not the saved job";
			int matching = 0;
			foreach (KingdomConstructionJob candidate in jobs)
				if (candidate?.Id == Witness.JobId) matching++;
			if (matching > 1)
				return "more than one retained registry row claims the saved job identity";
			if (job != null && !KingdomConstruction.HasReceipt(building, job))
				return "the retained job row is no longer linkable to the loaded building";
			string plot = KingdomQuickstartLifecycleSteps.Observed(building);
			if (plot != Witness.PlotId)
				return "the loaded building records plot " + plot + ", not the saved " + Witness.PlotId;
			if (!KingdomQuickstartLifecycleSteps.TryStockpile(zone, out GameObject stockpile,
				out string stockpileFailure)) return stockpileFailure;
			if (!KingdomQuickstartBuildCensus.TakeStock(zone, stockpile, false,
				out var stock, out string stockFailure)) return stockFailure;
			int timber = KingdomQuickstartLifecycleSteps.Timber(stock);
			if (timber != Witness.Timber)
				return "the loaded stockpile holds " + timber + " timber, not the saved " + Witness.Timber;
			int water = KingdomGrowth.CountStoredWater(zone);
			if (water != Witness.StoredWater)
				return "the loaded settlement holds " + water + " drams, not the saved " + Witness.StoredWater;
			// Every value here was read from the loaded game a moment ago: the system, the
			// standing object, its own cell and that cell's zone. None is copied from the witness.
			Observed = "realmId=" + system.RealmId + "; cityId=" + cityId + "; saveId=" + Game.GameID
				+ "; plotId=" + plot + "; buildingId=" + Describe(building.IDIfAssigned)
				+ "; completedReceiptId=" + Describe(receipt)
				+ "; at=" + building.Physics._CurrentCell.X + "," + building.Physics._CurrentCell.Y
				+ "; zone=" + building.Physics._CurrentCell.ParentZone.ZoneID
				+ "; built=1; functional=true; jobRowRetained=" + (job != null) + "; timber=" + timber
				+ "; storedWater=" + water + "; turns=" + Game.Turns;
			return null;
		}

		/// <summary>
		/// The further action on the loaded world: a real quote, the founder-facing CanPay
		/// pre-check, and a real commission that mints its OWN job. The new identity must differ
		/// from the completed one -- a "next action" that reported the finished job again would be
		/// proving the load, not proving that the loaded world still works.
		/// </summary>
		internal static void Next(XRLGame Game, KingdomQuickstartLifecycleSnapshot Witness)
		{
			string observed;
			string failure = Act(Game, Witness, out observed);
			KingdomScenarioJournal.Append(NextRow, failure == null, failure == null
				? KingdomQuickstartLifecycleSteps.Stamped("native-lifecycle step=next-action; " + observed)
				: KingdomQuickstartLifecycleSteps.Refuse("next-action", failure));
		}

		private static string Act(XRLGame Game, KingdomQuickstartLifecycleSnapshot Witness, out string Observed)
		{
			Observed = null;
			Zone zone = The.ZoneManager?.ActiveZone;
			KingdomSystem system = Game?.GetSystem<KingdomSystem>();
			if (zone == null || system == null || !system.Founded)
				return "no founded settlement on the loaded game";
			if (!KingdomData.TryGetBuilding(KingdomQuickstartLifecycleSteps.BuildKey,
				out KingdomRules.BuildEntry entry))
				return "the design is missing from the loaded catalogue";
			if (!KingdomQuickstartLifecycleSteps.TryStockpile(zone, out GameObject stockpile,
				out string stockpileFailure)) return stockpileFailure;
			if (!KingdomQuickstartBuildCensus.TakeStock(zone, stockpile, false,
				out var before, out string beforeFailure)) return beforeFailure;
			// Run 46b: production pays from ANY dedicated store, so the debit is judged over all.
			TimberByStore(zone, out List<string> storeIds, out List<int> timberBefore, out List<string> unreadBefore);
			if (!KingdomConstruction.TryRead(out List<KingdomConstructionJob> jobsBefore, out string readFailure))
				return readFailure ?? "the construction registry could not be read after loading";
			int waterBefore = KingdomGrowth.CountStoredWater(zone);
			if (!KingdomPlots.TryQuoteCommission(system, zone, entry, null,
				KingdomPlotRules.PlotSize.None, out KingdomPlotQuote quote, out string quoteFailure))
				return "the loaded world refused a new quote: "
					+ KingdomScenarioRules.Bounded(quoteFailure);
			if (waterBefore < quote.WaterDrams)
				return "the loaded settlement cannot pay the new quote: stored=" + waterBefore
					+ "; needed=" + quote.WaterDrams;
			if (!KingdomMaterials.CanPay(zone, KingdomQuickstartLifecycleSteps.BuildKey, out string blocker))
				return "the loaded settlement cannot pay materials: "
					+ KingdomScenarioRules.Bounded(blocker);
			if (!KingdomCommission.Commission(system, KingdomQuickstartLifecycleSteps.BuildKey, null,
				KingdomPlotRules.PlotSize.None, quote, out string commissionFailure))
				return "the loaded world refused the new commission: "
					+ KingdomScenarioRules.Bounded(commissionFailure);
			if (!KingdomQuickstartBuildCensus.TakeStock(zone, stockpile, false,
				out var after, out string afterFailure)) return afterFailure;
			if (!KingdomQuickstartBuildCensus.SameStockpile(before, after, out string sameFailure))
				return sameFailure;
			TimberByStore(zone, out List<string> storeIdsAfter, out List<int> timberAfter, out List<string> unreadAfter);
			if (!SameStores(storeIds, storeIdsAfter))
				return "the dedicated store set changed across the new commission";
			if (!KingdomQuickstartLifecycleDebitRules.Judge(storeIds, timberBefore, timberAfter,
				unreadBefore, unreadAfter, 1, out string debitFailure)) return debitFailure;
			string debit = KingdomQuickstartLifecycleDebitRules.Describe(storeIds, timberBefore, timberAfter);
			int waterAfter = KingdomGrowth.CountStoredWater(zone);
			if (waterAfter != waterBefore - entry.CostDrams)
				return "the new commission moved " + (waterBefore - waterAfter)
					+ " drams, not the design's exact " + entry.CostDrams;
			if (!KingdomConstruction.TryRead(out List<KingdomConstructionJob> jobsAfter, out string readAfter))
				return readAfter ?? "the construction registry could not be re-read";
			if (!KingdomQuickstartBuildCensus.TryNewPaidJob(jobsBefore, jobsAfter, zone, system,
				KingdomQuickstartLifecycleSteps.BuildKey, entry.CostDrams, quote,
				out KingdomConstructionJob job, out string jobFailure)) return jobFailure;
			if (job.Id == Witness.JobId)
				return "the next action reported the completed job's own identity; a further action "
					+ "must mint its own job";
			// Observed on the loaded game: the new job's own identity, and the building and plot
			// read again from the standing object rather than echoed from the witness.
			string standing = Standing(zone, Witness, out string plotId, out string buildingId);
			if (standing != null) return standing;
			Observed = "realmId=" + system.RealmId + "; cityId=" + KingdomConstruction.OwnerOf(system)
				+ "; saveId=" + Game.GameID + "; buildingId=" + buildingId + "; plotId=" + plotId
				+ "; jobId=" + job.Id + "; newJobId=" + job.Id
				+ "; completedJobId=" + Witness.JobId + "; " + debit + "; timberDebited=1"
				+ "; waterDebited=" + entry.CostDrams + "; turns=" + Game.Turns;
			return null;
		}

		/// <summary>Re-reads the standing building and its plot on the loaded game, by the saved
		/// identity, and hands back what THIS object says about itself.</summary>
		private static string Standing(Zone Zone, KingdomQuickstartLifecycleSnapshot Witness,
			out string PlotId, out string BuildingId)
		{
			PlotId = null;
			BuildingId = null;
			Cell cell = Zone?.GetCell(Witness.X, Witness.Y);
			if (cell == null) return "the saved building's own cell is not in the loaded zone";
			GameObject building = null;
			foreach (GameObject item in cell.GetObjects())
			{
				if (!GameObject.Validate(item) || item.IDIfAssigned != Witness.BuildingId) continue;
				if (building != null) return "two loaded objects claim the saved building identity";
				building = item;
			}
			if (building == null) return "the saved building no longer stands on its own cell";
			if (!KingdomUpgrade.IsFunctionallyBuilt(building))
				return "the standing building is no longer functionally built";
			PlotId = KingdomQuickstartLifecycleSteps.Observed(building);
			BuildingId = Describe(building.IDIfAssigned);
			return null;
		}

		private static string Describe(string Id)
		{
			return string.IsNullOrEmpty(Id) ? "unassigned" : Id;
		}
	}
}
