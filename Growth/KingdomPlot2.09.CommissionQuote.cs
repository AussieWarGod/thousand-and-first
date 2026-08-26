using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomPlots
	{
		/// <summary>
		/// Resolves the exact production map, pose, lot, and labour quote a commission will use.
		/// No resource is reserved and no object/property/zone is changed. Callers may therefore show
		/// this, escape, and prove cancellation was mutation-free. The commit path resolves again and
		/// requires byte-identical payload authority before it spends.
		/// </summary>
		public static bool TryQuoteCommission(KingdomSystem System, Zone Z,
			KingdomRules.BuildEntry Entry, string SkinKey, KingdomPlotRules.PlotSize Stake,
			out KingdomPlotQuote Quote, out string Failure)
		{
			Quote = null;
			Failure = null;
			if (System == null || Z == null || Entry == null || !TryGetSpec(Entry.Key, out var spec))
			{
				Failure = "No such plotted design.";
				return false;
			}
			if (KingdomPlotRules.HeartRungOf(Entry.Key) > 0)
			{
				Failure = KingdomPlotRules.RefuseSecondHeart(KingdomPresentation.Rich(System.SeatName));
				return false;
			}
			Failure = KingdomCommission.StageRefusal(System, Entry);
			if (Failure != null) return false;
			if (!KingdomZoning.Permits(System, Z.ZoneID, Entry, out Failure)) return false;
			KingdomPlotRules.PlotSize staked = StakedSize(spec, Stake);
			if (!KingdomPlotRules.Allows(System.Stage, staked))
			{
				Failure = KingdomPlotRules.RefuseStage(staked, KingdomPresentation.Rich(System.SeatName), System.Stage);
				return false;
			}
			Failure = KingdomDelve.Refusal(System, Z.ZoneID, Entry.Key, Entry.Name);
			if (Failure != null) return false;
			bool carved = KingdomPlotRules.IsUnderground(Z.Z);
			if (carved && spec.RequiresSky)
			{
				Failure = KingdomPlotRules.RefuseSky(Entry.Name);
				return false;
			}
			if (KingdomPlotRules.RoofRefusesSky(spec))
			{
				Failure = KingdomPlotRules.RefuseRoofSky(Entry.Name, spec.Roof);
				return false;
			}
			if (CountBuilt(Z) >= KingdomRules.MaxBuildingsForStage(System.Stage))
			{
				Failure = "There is no more room in the plan. " + KingdomPresentation.Rich(System.SeatName)
					+ " is as built-up as this ground allows, until it grows into something larger.";
				return false;
			}
			if (KingdomPlotRules.WouldExceedBudget(ReadPlots(Z), staked, Z.Width, Z.Height))
			{
				Failure = KingdomPlotRules.RefuseBudget(KingdomPresentation.Rich(System.SeatName));
				return false;
			}
			GroundGrid grid = new GroundGrid(Z);
			if (!TryFindRect(Z, System, Entry, spec, staked, grid, null,
				out KingdomPlotRules.PlotRect rect, out KingdomLayoutRules.LayoutOutcome outcome,
				out Failure)) return false;
			if (!TryPreparePlotPayload(System, Z, rect, Entry.Key, Entry.Category, SkinKey,
				out KingdomArchitectureIntent architecture, out string payload, out Failure))
				return false;
			Cell main = Z.GetCell(architecture.MainWorldX, architecture.MainWorldY);
			if (main == null || KingdomConstruction.HasActiveAt(System, Z, main))
			{
				Failure = main == null
					? "The authored building's main anchor is outside its plot."
					: "That ground already has a paid construction receipt in hand.";
				return false;
			}
			long total = KingdomPlotRules.RaiseTicks(
				KingdomCommission.CraftBuildTicks(Entry.BuildTicks, System.ZoneDistricts.Values),
				grid.CellsOf(rect), PlannedFootprint(Z, rect, spec),
				KingdomPlotRules.RoofOnGround(spec.Roof, carved), carved);
			if (total < 1L)
			{
				Failure = "The exact plot labour quote is empty.";
				return false;
			}
			if (!KingdomPurpose.TryQuoteCommit(System, Z, Entry.Key,
				out string purposeReceipt, out _, out Failure)) return false;
			Quote = new KingdomPlotQuote
			{
				Rect = rect, StakedSize = staked, Outcome = outcome,
				Architecture = architecture, Payload = payload, LabourTicks = total,
				WaterDrams = Entry.CostDrams,
				MaterialClaim = new KingdomMaterialDebitCost(KingdomMaterials.CostFor(Entry.Key),
					KingdomMaterials.BitCostFor(Entry.Key), KingdomMaterials.ExoticCostFor(Entry.Key)),
				MainX = architecture.MainWorldX, MainY = architecture.MainWorldY,
				PurposeReceipt = purposeReceipt
			};
			return true;
		}

		// --- Staking ----------------------------------------------------------------------

		/// <summary>
		/// Issues one plot-sized commission. Runs every check a single-cell commission runs, in
		/// the same order and with the same refusals, plus the four a plot adds: the tier's stage
		/// gate, the weather gate underground, the zone's road budget, and the ground itself.
		/// </summary>
		/// <param name="System">The realm; founded, and holding this ground.</param>
		/// <param name="Z">The zone.</param>
		/// <param name="Entry">The design.</param>
		/// <param name="SkinKey">The founder's chosen look, or null.</param>
		/// <param name="Failure">A founder-facing sentence when this returns false; null
		/// otherwise. Every refusal names what would lift it.</param>
		/// <returns>True once the ground is staked and the water is spent.</returns>
		public static bool Commission(KingdomSystem System, Zone Z, KingdomRules.BuildEntry Entry, string SkinKey, out string Failure)
		{
			return Commission(System, Z, Entry, SkinKey, KingdomPlotRules.PlotSize.None, out Failure);
		}

		/// <summary>
		/// Issues one plot-sized commission on ground of the founder's own choosing. Identical to
		/// the overload above in every check it runs, with one decision added: how much ground is
		/// staked. Never less than the design asks for, never more than the settlement has grown
		/// into, and the ceiling that choice sets is refused BY NAME later rather than quietly
		/// worked around (<see cref="GrowRefused"/>).
		/// </summary>
		/// <param name="Stake">The tier of plot to lay, from
		/// <see cref="KingdomPlotRules.StakeableSizes"/>.
		/// <see cref="KingdomPlotRules.PlotSize.None"/> stakes the design's own.</param>
		public static bool Commission(KingdomSystem System, Zone Z, KingdomRules.BuildEntry Entry, string SkinKey, KingdomPlotRules.PlotSize Stake, out string Failure)
		{
			return Commission(System, Z, Entry, SkinKey, Stake, null, out Failure);
		}

	}
}
