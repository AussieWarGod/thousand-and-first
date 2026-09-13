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
		/// <summary>Durable count of ordinary-turn wait chunks lifecycle-open has already spent
		/// this game, so a repeated attempt after `advance` knows how much budget remains. An int
		/// game state, never a new serialized field.</summary>
		internal const string WaitChunksKey = "r_TAF_ScenarioLifecycleOpenWaitChunks_v1";

		/// <summary>The exact reading TryStockpile reports when none is dedicated yet -- the one
		/// case lifecycle-open's bounded wait is for, as opposed to an ambiguous multiple-stockpile
		/// reading, which waiting can never resolve.</summary>
		internal const string NoStockpileFailure = KingdomQuickstartLifecycleStoreRules.NoneFailure;

		/// <summary>The stockpile this lifecycle pays from, by IDENTITY once opened: the opened
		/// receipt (OpenedKey, a durable string game state that survives the save into session 2)
		/// names the store lifecycle-open read, and every later step resolves that id among the
		/// dedicated stores standing now (KingdomQuickstartLifecycleStoreRules.Select). Native run
		/// 38: the heart's own r_KingdomHeartStockpile joined the bootstrap chest mid-advance, both
		/// lawful; a scan-based "exactly one" refused the save. Before the open, exactly one
		/// candidate resolves and more refuse. Nothing here dedicates or undedicates.</summary>
		internal static bool TryStockpile(XRLGame Game, Zone Zone, out GameObject Stockpile,
			out string Failure)
		{
			Stockpile = null;
			List<string> ids = new List<string>();
			List<GameObject> stores = new List<GameObject>();
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Zone))
			{
				if (!GameObject.Validate(item) || !KingdomMaterials.IsStockpile(item)
					|| item.Inventory == null) continue;
				ids.Add(item.IDIfAssigned ?? "");
				stores.Add(item);
			}
			string bound = KingdomQuickstartLifecycleStoreRules.BoundStoreId(
				Game?.GetStringGameState(OpenedKey));
			if (!KingdomQuickstartLifecycleStoreRules.Select(bound, ids, out string selected,
				out Failure)) return false;
			for (int i = 0; i < ids.Count; i++)
				if (ids[i] == selected && Stockpile == null) Stockpile = stores[i];
			return Stockpile != null;
		}

		/// <summary>The row field naming the store a step paid from and how many dedicated stores
		/// stood at that moment, so the checker can see every step named the SAME store.</summary>
		internal static string StoreClause(Zone Zone, GameObject Stockpile)
		{
			int count = 0;
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Zone))
				if (GameObject.Validate(item) && KingdomMaterials.IsStockpile(item)
					&& item.Inventory != null) count++;
			return "storeId=" + Describe(Stockpile?.IDIfAssigned) + "; stores=" + count;
		}

		/// <summary>
		/// Startup: what the settlement actually holds before anything is spent.
		/// <para>
		/// BOUNDED WAIT FOR DEDICATION. `realize` places no camp kit and dedicates nothing --
		/// dedicating a stockpile is a founder-interactive act
		/// (Core/KingdomCharterPart.Vessels.cs:182, Growth/KingdomMaterials.05.
		/// StockpileAndPaymentGates.cs:166) a sealed script never drives. Refusing the instant
		/// `realize` finishes proved nothing about whether the predicate could ever become true, so
		/// when TryStockpile reads none dedicated yet (never for the ambiguous multiple-stockpile
		/// reading, which waiting can never resolve), this attempt journals the reading as an OK
		/// "still waiting" row and asks the persona to spend one more ordinary
		/// KingdomQuickstartLifecycleOpenWait.ChunkTurns-turn chunk (via its own `advance` line)
		/// before trying again. Only once KingdomQuickstartLifecycleOpenWait.MaxChunks chunks have
		/// passed with the predicate still false does this refuse, naming the last reading. Nothing
		/// here dedicates, mints, or writes the predicate directly.
		/// </para>
		/// </summary>
		internal static string Open(XRLGame Game, Zone Zone, KingdomSystem System, out bool Ok)
		{
			Ok = false;
			if (Present(OpenedKey))
				return Refuse(OpenStep, "the lifecycle was already opened in this game");
			if (!TryStockpile(Game, Zone, out GameObject stockpile, out string stockpileFailure))
			{
				if (stockpileFailure != NoStockpileFailure) return Refuse(OpenStep, stockpileFailure);
				int chunksWaited = Game.GetIntGameState(WaitChunksKey);
				KingdomQuickstartLifecycleOpenWait.Decision decision =
					KingdomQuickstartLifecycleOpenWait.Evaluate(false, chunksWaited);
				if (decision == KingdomQuickstartLifecycleOpenWait.Decision.Exhausted)
					return Refuse(OpenStep, "no dedicated stockpile appeared within "
						+ KingdomQuickstartLifecycleOpenWait.TotalBudgetTurns
						+ " ordinary engine turns of waiting; last reading: " + stockpileFailure);
				Game.SetIntGameState(WaitChunksKey, chunksWaited + 1);
				Ok = true;
				return Stamped("native-lifecycle step=open-wait; " + Identities(System)
					+ "; chunk=" + (chunksWaited + 1) + " of " + KingdomQuickstartLifecycleOpenWait.MaxChunks
					+ "; reading=" + stockpileFailure + "; turns=" + Game.Turns);
			}
			if (!KingdomQuickstartBuildCensus.TakeStock(Zone, stockpile, false,
				out var stock, out string stockFailure)) return Refuse(OpenStep, stockFailure);
			if (!KingdomData.TryGetBuilding(BuildKey, out KingdomRules.BuildEntry entry))
				return Refuse(OpenStep, "the \"" + BuildKey + "\" design is missing from the live catalogue");
			int water = KingdomGrowth.CountStoredWater(Zone);
			if (!KingdomQuickstartSettlementChecks.Observe(Game, Zone, System, "startup", out string settlementFailure))
				return Refuse(OpenStep, settlementFailure);
			string opened = KingdomQuickstartLifecycleStoreRules.OpenedReceipt(Zone.ZoneID,
				stockpile.IDIfAssigned, Game.Turns);
			Game.SetStringGameState(OpenedKey, opened);
			if (!KingdomScenarioDurableState.ProvesExactText(OpenedKey, opened))
				return Refuse(OpenStep, "the lifecycle's own opening receipt did not persist exactly");
			Ok = true;
			return Stamped("native-lifecycle step=startup; " + Identities(System)
				+ "; zone=" + Zone.ZoneID + "; stockpile=" + Describe(stockpile.IDIfAssigned)
				+ "; " + StoreClause(Zone, stockpile)
				+ "; timber=" + Timber(stock) + "; storedWater=" + water
				+ "; designCostDrams=" + entry.CostDrams + "; turns=" + Game.Turns);
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
			if (!TryStockpile(Game, Zone, out GameObject stockpile, out string stockpileFailure))
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
			if (!quoted) return Refuse("quote", quoteFailure);
			string quoteReport = "quote waterDrams=" + quote.WaterDrams
				+ " materialClaim=" + quote.MaterialClaim.ToClaimString() + "; ";

			string blocked = null;
			if (waterBefore < quote.WaterDrams)
				blocked = "water-short; stored=" + waterBefore + "; needed=" + quote.WaterDrams;
			else if (!KingdomMaterials.CanPay(Zone, BuildKey, out string materialBlocker))
				blocked = "materials; blocker=" + Bounded(materialBlocker);
			if (blocked != null)
				return Refuse("canpay", blocked
					+ "; the settlement's own economy has not yet paid for this design");

			bool commissioned = KingdomCommission.Commission(System, BuildKey, null,
				KingdomPlotRules.PlotSize.None, quote, out string commissionFailure);
			if (!commissioned)
				return Refuse("commission", commissionFailure);
			string census = Census(Game, Zone, System, stockpile, before, waterBefore, entry,
				quote, jobsBefore, out KingdomConstructionJob job);
			if (census != null)
				return Refuse("after-commission", census);
			Game.SetStringGameState(JobKey, job.Id);
			if (!KingdomScenarioDurableState.ProvesExactText(JobKey, job.Id))
				return Refuse(BuildStep, "the commissioned job identity did not persist exactly");
			Ok = true;
			return Stamped("native-lifecycle step=paid-commission; " + Identities(System)
				+ "; plotId=" + Describe(job.SubjectId) + "; jobId=" + job.Id + "; "
				+ StoreClause(Zone, stockpile) + "; " + quoteReport
				+ "canpay blocked=false storedWater=" + waterBefore + "; job=" + job.Id
				+ "; timberDebited=1; waterDebited=" + entry.CostDrams
				+ "; phase=" + job.Phase + "; turns=" + Game.Turns);
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

		/// <summary>
		/// Stamps the row with the profile this run was launched from. Every lifecycle row carries
		/// it, so a journal can be bound to its own session rather than assumed into one, and a
		/// run whose profile cannot be named honestly refuses instead of landing an unbound row.
		/// </summary>
		internal static string Stamped(string Report)
		{
			string stamp = KingdomQuickstartLifecycleStamp.Text(
				KingdomScenarioJournal.ProfileRoot());
			return stamp == null
				? "native-lifecycle refused: the launched profile could not be named from its "
					+ "own sealed root"
				: Report + "; " + stamp;
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
		/// strict in both directions. Routed through Stamped(...) like every other lifecycle row,
		/// so an honest refusal still names the profile that produced it instead of landing an
		/// unbound row the checker cannot attribute to a session.</summary>
		internal static string Refuse(string Step, string Reason)
		{
			return Stamped("native-lifecycle refused at " + Step + ": " + Bounded(Reason));
		}
	}
}
