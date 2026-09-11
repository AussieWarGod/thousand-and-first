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
	internal static class KingdomQuickstartLifecycleLoad
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
				: "native-lifecycle refused at cold-load: " + KingdomScenarioRules.Bounded(failure));
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
			if (building.GetIntProperty("KingdomBuilt") != 1)
				return "the loaded building no longer reads as a finished settlement building";
			if (building.GetStringProperty(KingdomUpgrade.BuildKeyProperty) != Witness.DesignKey)
				return "the loaded building's design key differs from the commissioned one";
			// The completed job may have been compacted out of the live registry by the load; what
			// must still hold is that whatever row remains is linkable to this exact building.
			if (job != null && !KingdomConstruction.HasReceipt(building, job))
				return "the retained job row is no longer linkable to the loaded building";
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
			Observed = "realmId=" + system.RealmId + "; cityId=" + cityId + "; saveId=" + Game.GameID
				+ "; plotId=" + Describe(Witness.PlotId) + "; buildingId=" + Describe(building.IDIfAssigned)
				+ "; built=1; jobRowRetained=" + (job != null) + "; timber=" + timber
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
				: "native-lifecycle refused at next-action: " + KingdomScenarioRules.Bounded(failure));
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
			if (!KingdomQuickstartBuildCensus.ExactSingleDebit(before, after,
				KingdomMaterial.Timber, 1, out string debitFailure)) return debitFailure;
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
			Observed = "realmId=" + system.RealmId + "; cityId=" + KingdomConstruction.OwnerOf(system)
				+ "; saveId=" + Game.GameID + "; newJobId=" + job.Id
				+ "; completedJobId=" + Witness.JobId + "; timberDebited=1"
				+ "; waterDebited=" + entry.CostDrams + "; turns=" + Game.Turns;
			return null;
		}

		private static string Describe(string Id)
		{
			return string.IsNullOrEmpty(Id) ? "unassigned" : Id;
		}
	}
}
