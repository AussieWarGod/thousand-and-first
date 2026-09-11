using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Startup census and the paid commission of the construction lifecycle. Every step journals
	/// its own row, so a refusal is attributed to the exact step that produced it, and every
	/// number in those rows is read from production state rather than assumed.
	/// </summary>
	internal static partial class KingdomQuickstartLifecycleSteps
	{
		internal const string BuildKey = "fire";
		internal const string OpenStep = "lifecycle-open";
		internal const string BuildStep = "lifecycle-build";
		internal const string GrownStep = "lifecycle-grown";
		internal const string SaveStep = "lifecycle-save";
		/// <summary>Durable carrier of the commissioned job's identity between verbs. A string
		/// game state, never a new serialized field.</summary>
		internal const string JobKey = "r_TAF_ScenarioLifecycleJob_v1";
		internal const string OpenedKey = "r_TAF_ScenarioLifecycleOpened_v1";

		/// <summary>The settlement's single dedicated stockpile, re-proved by its own reference
		/// and placed cell. More than one is ambiguous and is refused rather than guessed at.</summary>
		internal static bool TryStockpile(Zone Zone, out GameObject Stockpile, out string Failure)
		{
			Stockpile = null;
			Failure = "this settlement has no dedicated stockpile to pay from";
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Zone))
			{
				if (!GameObject.Validate(item) || !KingdomMaterials.IsStockpile(item)
					|| item.Inventory == null) continue;
				if (Stockpile != null)
				{
					Stockpile = null;
					Failure = "more than one dedicated stockpile stands here; the lifecycle refuses "
						+ "to guess which one the settlement pays from";
					return false;
				}
				Stockpile = item;
			}
			if (Stockpile == null) return false;
			Failure = null;
			return true;
		}

		/// <summary>Startup: what the settlement actually holds before anything is spent.</summary>
		internal static string Open(XRLGame Game, Zone Zone, KingdomSystem System, out bool Ok)
		{
			Ok = false;
			if (Present(OpenedKey))
				return Refuse(OpenStep, "the lifecycle was already opened in this game");
			if (!TryStockpile(Zone, out GameObject stockpile, out string stockpileFailure))
				return Refuse(OpenStep, stockpileFailure);
			if (!KingdomQuickstartBuildCensus.TakeStock(Zone, stockpile, false,
				out var stock, out string stockFailure)) return Refuse(OpenStep, stockFailure);
			if (!KingdomData.TryGetBuilding(BuildKey, out KingdomRules.BuildEntry entry))
				return Refuse(OpenStep, "the \"" + BuildKey + "\" design is missing from the live catalogue");
			int water = KingdomGrowth.CountStoredWater(Zone);
			string opened = Zone.ZoneID + "|" + stockpile.IDIfAssigned + "|" + Game.Turns;
			Game.SetStringGameState(OpenedKey, opened);
			if (!KingdomScenarioDurableState.ProvesExactText(OpenedKey, opened))
				return Refuse(OpenStep, "the lifecycle's own opening receipt did not persist exactly");
			Ok = true;
			return "native-lifecycle step=startup; " + Identities(System)
				+ "; zone=" + Zone.ZoneID + "; stockpile=" + Describe(stockpile.IDIfAssigned)
				+ "; timber=" + Timber(stock) + "; storedWater=" + water
				+ "; designCostDrams=" + entry.CostDrams + "; turns=" + Game.Turns;
		}

		/// <summary>Quote, the founder-facing CanPay pre-check, then the exact-quote commission,
		/// with the stockpile and water census taken on both sides of the production call.</summary>
		internal static string Build(XRLGame Game, Zone Zone, KingdomSystem System, out bool Ok)
		{
			Ok = false;
			if (!Present(OpenedKey))
				return Refuse(BuildStep, "the lifecycle was never opened; startup census is missing");
			if (Present(JobKey))
				return Refuse(BuildStep, "this lifecycle already commissioned its one job");
			if (!TryStockpile(Zone, out GameObject stockpile, out string stockpileFailure))
				return Refuse(BuildStep, stockpileFailure);
			if (!KingdomQuickstartBuildCensus.TakeStock(Zone, stockpile, false,
				out var before, out string beforeFailure)) return Refuse(BuildStep, beforeFailure);
			if (!KingdomData.TryGetBuilding(BuildKey, out KingdomRules.BuildEntry entry))
				return Refuse(BuildStep, "the \"" + BuildKey + "\" design is missing from the live catalogue");
			if (!KingdomConstruction.TryRead(out List<KingdomConstructionJob> jobsBefore, out string readFailure))
				return Refuse(BuildStep, readFailure ?? "the construction registry could not be read");
			int waterBefore = KingdomGrowth.CountStoredWater(Zone);

			bool quoted = KingdomPlots.TryQuoteCommission(System, Zone, entry, null,
				KingdomPlotRules.PlotSize.None, out KingdomPlotQuote quote, out string quoteFailure);
			if (!quoted) return "native-lifecycle refused at quote: " + Bounded(quoteFailure);
			string quoteReport = "quote waterDrams=" + quote.WaterDrams
				+ " materialClaim=" + quote.MaterialClaim.ToClaimString() + "; ";

			string blocked = null;
			if (waterBefore < quote.WaterDrams)
				blocked = "water-short; stored=" + waterBefore + "; needed=" + quote.WaterDrams;
			else if (!KingdomMaterials.CanPay(Zone, BuildKey, out string materialBlocker))
				blocked = "materials; blocker=" + Bounded(materialBlocker);
			if (blocked != null)
				return "native-lifecycle refused at canpay: " + blocked
					+ "; the settlement's own economy has not yet paid for this design";

			bool commissioned = KingdomCommission.Commission(System, BuildKey, null,
				KingdomPlotRules.PlotSize.None, quote, out string commissionFailure);
			if (!commissioned)
				return "native-lifecycle refused at commission: " + Bounded(commissionFailure);
			string census = Census(Game, Zone, System, stockpile, before, waterBefore, entry,
				quote, jobsBefore, out KingdomConstructionJob job);
			if (census != null)
				return "native-lifecycle refused after commission: " + Bounded(census);
			Game.SetStringGameState(JobKey, job.Id);
			if (!KingdomScenarioDurableState.ProvesExactText(JobKey, job.Id))
				return Refuse(BuildStep, "the commissioned job identity did not persist exactly");
			Ok = true;
			return "native-lifecycle step=paid-commission; " + Identities(System)
				+ "; plotId=" + Describe(job.SubjectId) + "; jobId=" + job.Id + "; " + quoteReport
				+ "canpay blocked=false storedWater=" + waterBefore + "; job=" + job.Id
				+ "; timberDebited=1; waterDebited=" + entry.CostDrams
				+ "; phase=" + job.Phase + "; turns=" + Game.Turns;
		}

		/// <summary>The physical census on the far side of the production commissioning call:
		/// same chest, exactly one timber spent, exactly the design's drams gone, and exactly one
		/// new paid job whose claims match the quote that was committed.</summary>
		private static string Census(XRLGame Game, Zone Zone, KingdomSystem System,
			GameObject Stockpile, KingdomQuickstartBuildCensus.StockSnapshot Before, int WaterBefore,
			KingdomRules.BuildEntry Entry, KingdomPlotQuote Quote,
			List<KingdomConstructionJob> JobsBefore, out KingdomConstructionJob Job)
		{
			Job = null;
			if (!KingdomQuickstartBuildCensus.TakeStock(Zone, Stockpile, false,
				out var after, out string afterFailure)) return afterFailure;
			if (!KingdomQuickstartBuildCensus.SameStockpile(Before, after, out string sameFailure))
				return sameFailure;
			if (!KingdomQuickstartBuildCensus.ExactSingleDebit(Before, after,
				KingdomMaterial.Timber, 1, out string debitFailure)) return debitFailure;
			int waterAfter = KingdomGrowth.CountStoredWater(Zone);
			if (waterAfter != WaterBefore - Entry.CostDrams)
				return "stored water moved by " + (WaterBefore - waterAfter) + " drams, not the "
					+ "design's exact " + Entry.CostDrams;
			if (!KingdomConstruction.TryRead(out List<KingdomConstructionJob> jobsAfter, out string readFailure))
				return readFailure ?? "the construction registry could not be read after commissioning";
			if (!KingdomQuickstartBuildCensus.TryNewPaidJob(JobsBefore, jobsAfter, Zone, System,
				BuildKey, Entry.CostDrams, Quote, out Job, out string jobFailure)) return jobFailure;
			return null;
		}

		/// <summary>The settlement identities this verb read from live state at the moment it
		/// ran. Never cached, never carried between verbs: each step reads them again.</summary>
		internal static string Identities(KingdomSystem System)
		{
			return "realmId=" + Describe(System.RealmId)
				+ "; cityId=" + Describe(KingdomConstruction.OwnerOf(System));
		}

		internal static int Timber(KingdomQuickstartBuildCensus.StockSnapshot Stock)
		{
			int total = 0;
			foreach (KingdomQuickstartBuildCensus.Row row in Stock.Rows)
				if (row.Material == KingdomMaterial.Timber) total += row.Count;
			return total;
		}

		internal static string Bounded(string Text)
		{
			return KingdomScenarioRules.Bounded(Text ?? "no reason given");
		}

		internal static string Describe(string Id)
		{
			return string.IsNullOrEmpty(Id) ? "unassigned" : Id;
		}

		/// <summary>Whether a durable key carries text, read through the shared observer rather
		/// than by a bare game-state peek.</summary>
		internal static bool Present(string Key)
		{
			KingdomDurableKeyObservation observation = KingdomScenarioDurableState.Observe(Key);
			return observation != null && observation.HasString
				&& !string.IsNullOrEmpty(observation.String);
		}

		/// <summary>A refusal is returned, never journalled here: the scenario runner writes
		/// exactly one row per verb it ran, carrying this text, so a persona's expectations stay
		/// strict in both directions.</summary>
		internal static string Refuse(string Step, string Reason)
		{
			return "native-lifecycle refused at " + Step + ": " + Bounded(Reason);
		}
	}
}
