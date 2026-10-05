using System;
using System.Collections.Generic;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	internal static partial class KingdomCampHeartNativeChecks
	{
		/// <summary>The tent bill production charges, read from the running catalogue
		/// (Growth/KingdomPlot2.10.Commission.cs builds the same three-part claim).</summary>
		internal static KingdomMaterialDebitCost TentClaim => new KingdomMaterialDebitCost(
			KingdomMaterials.CostFor(ClaimKey), KingdomMaterials.BitCostFor(ClaimKey),
			KingdomMaterials.ExoticCostFor(ClaimKey));
		internal static int TentBrush => KingdomMaterials.CostFor(ClaimKey).Get(KingdomMaterial.Brush);
		internal static int TentUpgradeBrush =>
			KingdomMaterials.UpgradeCostFor(ClaimKey).Get(KingdomMaterial.Brush);
		internal static int PaidTentBrushUnits =>
			KingdomCampHeartTentRules.PaidTentBrush(TentBrush, TentUpgradeBrush);
		internal static int SavedBrushUnits =>
			KingdomCampHeartTentRules.SavedBrush(TentBrush, TentUpgradeBrush);
		private sealed partial class Frame
		{
			internal string TentJobId;
			internal bool PaysTent;
			internal readonly List<string> MintedTentInputs = new List<string>();

			private bool SealedPaysTent()
			{
				Require(KingdomScenarioScript.TryRead(out var script, out string failure),
					failure ?? "the sealed camp heart script is absent");
				return TargetRung == 2 && KingdomCampHeartScript.PaysTent(script);
			}

			private void RequirePaidTentArithmetic()
			{
				if (!PaysTent) return;
				int capacity = KingdomSurvey.StockCapacityOf(Store);
				Require(KingdomCampHeartTentRules.Lawful(TentBrush, TentUpgradeBrush,
					MintedStoneUnits + MintedTimberUnits, capacity),
					"the catalogue tent bill leaves no lawful sentinel: tent-brush=" + TentBrush
					+ "; upgrade-brush=" + TentUpgradeBrush + "; capacity=" + capacity);
			}

			/// <summary>Paid-tent scripts quote the exact production commission first, read-only,
			/// and lock it; only then are the tent's non-brush catalogue inputs minted at this
			/// phase-2 boundary, after the rung-2 bill left the store. The commit must match the
			/// quote before any debit (Growth/KingdomPlot2.10.Commission.cs).</summary>
			private KingdomPlotQuote QuotePaidTent(KingdomRules.BuildEntry Entry)
			{
				Require(KingdomPlots.TryQuoteCommission(System, Zone, Entry, null,
					KingdomPlotRules.PlotSize.None, out var quote, out string failure) && quote != null,
					"taf-camp-tent-quote-refused: " + KingdomScenarioRules.Bounded(failure));
				KingdomMaterialDebitCost claim = TentClaim;
				Require(quote.WaterDrams == Entry.CostDrams && quote.MaterialClaim != null
					&& quote.MaterialClaim.ToClaimString() == claim.ToClaimString(),
					"taf-camp-tent-quote-bill: the quote differs from the catalogue tent bill "
					+ claim.ToClaimString());
				Evidence.Append("\npaid-tent quote rect=").Append(quote.Rect.X1).Append(',')
					.Append(quote.Rect.Y1).Append(' ').Append(quote.Rect.X2).Append(',')
					.Append(quote.Rect.Y2).Append("; labour-ticks=").Append(quote.LabourTicks)
					.Append("; water=").Append(quote.WaterDrams).Append("; claim=")
					.Append(claim.ToClaimString());
				RequireChainQuote(quote);
				MintTentShortfall(claim);
				return quote;
			}

			private void MintTentShortfall(KingdomMaterialDebitCost Claim)
			{
				var present = ContentUnits(out _);
				string brush = KingdomMaterials.BlueprintFor(KingdomMaterial.Brush);
				Require(present.Count == Unasked && present.TrueForAll(unit => unit != null
						&& unit.Blueprint == brush && unit.RawCount == 1)
					&& Claim.Bits.IsEmpty() && Claim.Exotics.IsEmpty()
					&& Claim.Materials.Get(KingdomMaterial.Brush) <= Unasked,
					"taf-camp-tent-boundary: the store is not its unasked brush or the bill outgrew it: "
					+ KingdomCampHeartNativeCensus.Describe(present));
				foreach (KingdomMaterial material in Enum.GetValues(typeof(KingdomMaterial)))
					if (material != KingdomMaterial.Brush)
						Mint(material, Claim.Materials.Get(material), MintedTentInputs);
				Require(present.Count + MintedTentInputs.Count <= KingdomSurvey.StockCapacityOf(Store),
					"taf-camp-tent-boundary: the tent inputs would overfill the store");
				Evidence.Append("; synthetic-tent-inputs=").Append(MintedTentInputs.Count);
			}

			/// <summary>The paid tent's exact output, whose own canvas-only improvement must never
			/// have begun: neither a working improvement nor an improvement receipt names it.
			/// </summary>
			private GameObject IdlePaidTent(List<KingdomConstructionJob> Jobs)
			{
				TentJobId = PaidTent(this, Jobs);
				Require(KingdomConstruction.TryFind(TentJobId, out var job) && job != null,
					"paid tent job absent");
				Require(KingdomConstruction.FindExactId(Zone, job.OutputId, out GameObject tent)
					== KingdomPhysicalLookupState.Exact, "paid tent output absent");
				var improvement = tent.GetPart<r_KingdomImprovement>();
				Require(improvement == null || !improvement.Working,
					"the paid tent's own improvement began before the fixture held it");
				foreach (var other in Jobs)
					Require(other == null || other.Route != KingdomConstructionRoute.Improvement
						|| other.SubjectId != tent.IDIfAssigned,
						"the paid tent carries an improvement receipt before the fixture held it");
				return tent;
			}
		}

		private static string PaidTent(Frame Frame, List<KingdomConstructionJob> Jobs)
		{
			KingdomConstructionJob found = null;
			foreach (var job in Jobs)
			{
				if (job?.TargetKey != ClaimKey || job.OwnerKey != KingdomConstruction.OwnerOf(Frame.System)
					|| job.ZoneId != Frame.Zone.ZoneID) continue;
				Require(found == null, "camp has more than one tent job");
				found = job;
			}
			Require(KingdomData.TryGetBuilding(ClaimKey, out var entry) && entry != null,
				"the catalogue has no tent design");
			Require(found != null && found.Id != Frame.JobId && found.Route == KingdomConstructionRoute.PlotCommission
				&& (found.Phase == KingdomConstructionPhase.Working || found.Phase == KingdomConstructionPhase.Complete)
				&& KingdomQuickstartBuildClaims.CleanFirstPayment(found.Claims, entry.CostDrams, TentClaim),
				"camp tent has no distinct clean receipt for the catalogue tent bill " + TentClaim.ToClaimString());
			Require(KingdomConstruction.FindExactId(Frame.Zone, found.OutputId, out GameObject output) == KingdomPhysicalLookupState.Exact,
				"paid camp tent output is absent or ambiguous");
			ExactGround(Frame.Zone, output);
			Require(KingdomConstruction.HasReceipt(output, found) && output.CurrentCell.X == found.X
				&& output.CurrentCell.Y == found.Y, "paid camp tent output has lost its receipt or anchor");
			Require(found.Phase == KingdomConstructionPhase.Working
				? output.GetPart<r_KingdomPlotWorks>()?.DesignKey == ClaimKey
				: found.PhysicalPhase == KingdomPhysicalPhase.EffectsSettled
					&& KingdomUpgrade.IsFunctionallyBuilt(output) && KingdomUpgrade.DesignKeyOf(output) == ClaimKey,
				"paid camp tent output does not match its working or completed receipt");
			return found.Id;
		}
	}
}
