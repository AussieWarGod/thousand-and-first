using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>Genuine post-boot build proof for quickstart-build: drives the exact three-call
	/// production commissioning sequence the Charter UI itself drives for a plotted design
	/// (Core/KingdomCharterPart.Commission.cs:70-105) -- quote, CanPay/water pre-check, then the
	/// exact-quote commit -- never the bare non-plot overload. Outside any harness BindPass.
	///
	/// 1. KingdomPlots.TryQuoteCommission(system, zone, entry, null, PlotSize.None, out quote,
	///    out quoteFailure) -- :73-74.
	/// 2. The UI's own water/CanPay pre-check -- :89-93: stored &lt; quote.WaterDrams, else
	///    KingdomMaterials.CanPay(zone, "fire", out materialBlocker). A block here means the real
	///    UI never reaches its confirm popup, so the harness never calls Commission either.
	/// 3. KingdomCommission.Commission(system, "fire", null, PlotSize.None, quote, out failure)
	///    -- :103-104, committing that exact quote.
	///
	/// Runs ONLY after Harness/KingdomQuickstartBootTest.cs has already journaled a completed,
	/// unmodified QUICKSTART-BOOT-COMPLETE row; this phase never reorders or relabels that proof.
	/// Every step is journaled separately so a refusal is attributed to its exact step. The
	/// harness binds no survey, mints no stock, and reads IDIfAssigned only for reporting -- an
	/// unassigned starter child id is never itself a refusal. A production refusal at any step
	/// (the current stock-scope defect this persona exists to catch) is a distinct terminal
	/// outcome, never converted into a harness error: the persona is RED-by-design on unfixed
	/// main and green only once the production fix lands.</summary>
	internal static class KingdomQuickstartBuildTest
	{
		private const string BuildKey = "fire";

		internal static void Run(XRLGame Game, Zone Zone, string ObservedReceipt, string Command)
		{
			KingdomScenarioJournal.Append("QUICKSTART-BUILD-BEGIN", true,
				Command + "; genuine-production-commission=true");
			string stopStep = TryRun(Game, Zone, ObservedReceipt, out string refusal, out bool refused);
			bool ok = stopStep == null;
			string outcome = ok
				? Command + "; commissioned=true; timber-debit=exact; water-debit=exact"
					+ "; job-projected=true; survey-scope-clear=true; boot-only=false; build-refused=false"
				: refused
					? Command + "; build-refused=true; boot-only=false; step=" + stopStep
						+ "; survey-scope-clear=" + KingdomQuickstartBuildCensus.SurveyScopeClear()
						+ "; refusal=" + refusal
					: Command + "; harness-error=true; boot-only=false; build-refused=false; step="
						+ stopStep + "; " + refusal;
			KingdomScenarioJournal.Append("QUICKSTART-BUILD-COMPLETE", ok, outcome);
		}

		/// <summary>Returns null on full success; otherwise the step name where it stopped, with
		/// Refusal set to the founder-facing (or harness-internal) reason and Refused distinguishing
		/// a genuine production refusal from an unexpected harness fault.</summary>
		private static string TryRun(XRLGame Game, Zone Zone, string ObservedReceipt, out string Refusal, out bool Refused)
		{
			Refusal = null; Refused = false;
			try
			{
				if (Game == null || Zone == null || !ReferenceEquals(The.Game, Game)
					|| !ReferenceEquals(The.ZoneManager?.ActiveZone, Zone))
				{ Refusal = "build phase lost its exact game or zone authority"; return "authority"; }
				if (!KingdomQuickstartRules.TryDecode(ObservedReceipt, out KingdomQuickstartReceipt receipt))
				{ Refusal = "observed boot receipt could not be decoded"; return "authority"; }
				if (!KingdomQuickstartBuildCensus.TryStockpile(Zone, receipt.StockpileObjectId,
					out GameObject stockpile, out string stockpileFailure))
				{ Refusal = stockpileFailure; return "authority"; }
				if (!KingdomQuickstartBuildCensus.TryWater(Zone, receipt.WaterObjectId,
					out var waterBefore, out string waterFailure))
				{ Refusal = waterFailure; return "authority"; }
				if (!KingdomQuickstartBuildCensus.TakeStock(Zone, stockpile, true,
					out var stockBefore, out string beforeFailure))
				{ Refusal = beforeFailure; return "census-before"; }
				if (!KingdomQuickstartBuildCensus.SurveyScopeClear())
				{ Refusal = "a survey scope was already bound before the production call"; return "authority"; }
				KingdomSystem system = Game.GetSystem<KingdomSystem>();
				if (system == null || !system.Founded)
				{ Refusal = "founded kingdom authority is missing"; return "authority"; }
				if (!KingdomData.TryGetBuilding(BuildKey, out KingdomRules.BuildEntry entry))
				{ Refusal = "the \"" + BuildKey + "\" design is missing from the live catalogue"; return "authority"; }
				if (!KingdomConstruction.TryRead(out List<KingdomConstructionJob> jobsBefore, out string readFailure))
				{ Refusal = readFailure ?? "the construction registry could not be read before commissioning"; return "authority"; }

				// Step 1: quote (Core/KingdomCharterPart.Commission.cs:73-74).
				bool quoted = KingdomPlots.TryQuoteCommission(system, Zone, entry, null,
					KingdomPlotRules.PlotSize.None, out KingdomPlotQuote quote, out string quoteFailure);
				KingdomScenarioJournal.Append("QUICKSTART-BUILD-QUOTE", quoted,
					quoted ? "waterDrams=" + quote.WaterDrams : "quoteFailure=" + quoteFailure);
				if (!KingdomQuickstartBuildCensus.SurveyScopeClear())
				{ Refusal = "a survey scope leaked out of the quote step"; return "authority"; }
				if (!quoted)
				{ Refused = quoteFailure != null; Refusal = quoteFailure ?? "quote refused without a reason"; return "quote"; }

				// Step 2: the UI's own water/CanPay pre-check (:89-93). A block here means the
				// real UI never reaches its confirm popup, so Commission is never called either.
				int stored = KingdomGrowth.CountStoredWater(Zone);
				string blocked = null;
				if (stored < quote.WaterDrams)
					blocked = "water-short; stored=" + stored + "; needed=" + quote.WaterDrams;
				else if (!KingdomMaterials.CanPay(Zone, BuildKey, out string materialBlocker))
					blocked = "materials; blocker=" + materialBlocker;
				KingdomScenarioJournal.Append("QUICKSTART-BUILD-CANPAY", blocked == null,
					blocked == null ? "blocked=false" : "blocked=true; reason=" + blocked);
				if (!KingdomQuickstartBuildCensus.SurveyScopeClear())
				{ Refusal = "a survey scope leaked out of the CanPay step"; return "authority"; }
				if (blocked != null)
				{ Refused = true; Refusal = blocked; return "canpay"; }

				// Step 3: commit the exact quote (:103-104).
				bool commissioned = KingdomCommission.Commission(system, BuildKey, null,
					KingdomPlotRules.PlotSize.None, quote, out string commissionFailure);
				KingdomScenarioJournal.Append("QUICKSTART-BUILD-COMMISSION", commissioned,
					commissioned ? "commissioned=true" : "commissionFailure=" + commissionFailure);
				if (!KingdomQuickstartBuildCensus.SurveyScopeClear())
				{ Refusal = "a survey scope leaked out of the production commissioning call"; return "authority"; }
				if (!commissioned)
				{
					Refused = commissionFailure != null;
					Refusal = commissionFailure ?? "production commissioning refused without a reason";
					return "commission";
				}

				if (!KingdomQuickstartBuildCensus.TakeStock(Zone, stockpile, false,
					out var stockAfter, out string afterFailure))
				{ Refusal = afterFailure; return "census-after"; }
				if (!KingdomQuickstartBuildCensus.SameStockpile(stockBefore, stockAfter, out string sameFailure))
				{ Refusal = sameFailure; return "census-after"; }
				if (!KingdomQuickstartBuildCensus.ExactSingleDebit(stockBefore, stockAfter,
					KingdomMaterial.Timber, 1, out string debitFailure))
				{ Refusal = debitFailure; return "census-after"; }
				if (!KingdomQuickstartBuildCensus.TryWater(Zone, receipt.WaterObjectId,
					out var waterAfter, out string waterAfterFailure))
				{ Refusal = waterAfterFailure; return "census-after"; }
				if (!KingdomQuickstartBuildCensus.ExactWaterDebit(waterBefore, waterAfter, entry.CostDrams,
					out string waterDebitFailure))
				{ Refusal = waterDebitFailure; return "census-after"; }
				if (!KingdomConstruction.TryRead(out List<KingdomConstructionJob> jobsAfter, out string readAfterFailure))
				{ Refusal = readAfterFailure ?? "the construction registry could not be read after commissioning"; return "census-after"; }
				if (!KingdomQuickstartBuildCensus.TryNewPaidJob(jobsBefore, jobsAfter, Zone, system, BuildKey,
					entry.CostDrams, quote, out _, out string jobFailure))
				{ Refusal = jobFailure; return "census-after"; }
				return null;
			}
			catch (Exception error)
			{
				Refusal = "build phase threw " + error.GetType().Name;
				return "exception";
			}
		}
	}
}
