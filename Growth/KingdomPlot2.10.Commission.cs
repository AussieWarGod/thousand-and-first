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
		/// Commits a plot against an optional exact quote already shown to the founder. A changed
		/// map, pose, rect, or labour value refuses before debit and asks for a fresh preview.
		/// </summary>
		public static bool Commission(KingdomSystem System, Zone Z,
			KingdomRules.BuildEntry Entry, string SkinKey, KingdomPlotRules.PlotSize Stake,
			KingdomPlotQuote Expected, out string Failure)
		{
			Failure = null;
			if (System == null || Z == null || Entry == null || !TryGetSpec(Entry.Key, out var spec))
			{
				Failure = "No such design.";
				return false;
			}
			if (KingdomPlotRules.HeartRungOf(Entry.Key) > 0)
			{
				// The heart is founded, not commissioned. Its first rung is staked by the rite and
				// every rung above it climbs through the ordinary improvement machinery on the same
				// ground; a second one ordered across the zone would be a second heart, which is
				// the one thing this ladder is not.
				Failure = KingdomPlotRules.RefuseSecondHeart(KingdomPresentation.Rich(System.SeatName));
				return false;
			}
			Failure = KingdomCommission.StageRefusal(System, Entry);
			if (Failure != null)
			{
				return false;
			}
			if (!KingdomZoning.Permits(System, Z.ZoneID, Entry, out string zoningFailure))
			{
				Failure = zoningFailure;
				return false;
			}
			KingdomPlotRules.PlotSize staked = StakedSize(spec, Stake);
			if (!KingdomPlotRules.Allows(System.Stage, staked))
			{
				Failure = KingdomPlotRules.RefuseStage(staked, KingdomPresentation.Rich(System.SeatName), System.Stage);
				return false;
			}
			// The way down is asked before the weather, and for the same reason the strata gate is
			// asked before the district: a lack in the GROUND is the truer answer, and telling a
			// founder their condensing hall wants sky when the rock it would stand in has never
			// been opened names the second-best lack.
			Failure = KingdomDelve.Refusal(System, Z.ZoneID, Entry.Key, Entry.Name);
			if (Failure != null)
			{
				return false;
			}
			bool carved = KingdomPlotRules.IsUnderground(Z.Z);
			if (carved && spec.RequiresSky)
			{
				Failure = KingdomPlotRules.RefuseSky(Entry.Name);
				return false;
			}
			if (KingdomPlotRules.RoofRefusesSky(spec))
			{
				// A tier that declared itself walled, for a design that needs weather. Refused by
				// name rather than raised into something that could never work.
				Failure = KingdomPlotRules.RefuseRoofSky(Entry.Name, spec.Roof);
				return false;
			}
			if (CountBuilt(Z) >= KingdomRules.MaxBuildingsForStage(System.Stage))
			{
				Failure = "There is no more room in the plan. " + KingdomPresentation.Rich(System.SeatName) + " is as built-up as this ground allows, until it grows into something larger.";
				return false;
			}
			if (KingdomPlotRules.WouldExceedBudget(ReadPlots(Z), staked, Z.Width, Z.Height))
			{
				Failure = KingdomPlotRules.RefuseBudget(KingdomPresentation.Rich(System.SeatName));
				return false;
			}
			// Realm logistics may cover a local shortfall. The exact local attempt and routed
			// fallback share one construction receipt after the immutable preview is frozen.
			GroundGrid grid = new GroundGrid(Z);
			if (!TryFindRect(Z, System, Entry, spec, staked, grid, null, out var rect, out var outcome, out var refusal))
			{
				Failure = refusal;
				return false;
			}
			if (!TryPreparePlotPayload(System, Z, rect, Entry.Key, Entry.Category, SkinKey,
				out KingdomArchitectureIntent architecture, out string payload,
				out string architectureFailure))
			{
				Failure = architectureFailure ?? "No authored architecture fits that exact plot.";
				return false;
			}
			Cell mainCell = Z.GetCell(architecture.MainWorldX, architecture.MainWorldY);
			if (mainCell == null)
			{
				Failure = "The authored building's main anchor is outside its plot.";
				return false;
			}
			if (KingdomConstruction.HasActiveAt(System, Z, mainCell))
			{
				Failure = "That ground already has a paid construction receipt in hand.";
				return false;
			}
			long start = The.Game.TimeTicks;
			KingdomPlotRules.PlotRect footprint = PlannedFootprint(Z, rect, spec);
			KingdomPlotRules.RoofState roof = KingdomPlotRules.RoofOnGround(spec.Roof, carved);
			long total = KingdomPlotRules.RaiseTicks(
				KingdomCommission.CraftBuildTicks(Entry.BuildTicks, System.ZoneDistricts.Values),
				grid.CellsOf(rect), footprint, roof, carved);
			if (!KingdomPurpose.TryQuoteCommit(System, Z, Entry.Key,
				out string purposeReceipt, out _, out Failure)) return false;
			if (purposeReceipt != null && Expected == null)
			{
				Failure = "This city purpose must be commissioned from its exact precommit preview; nothing was spent.";
				return false;
			}
			if (Expected != null && (Expected.Payload != payload
				|| Expected.LabourTicks != total || !SameRect(Expected.Rect, rect)
				|| Expected.StakedSize != staked || Expected.MainX != architecture.MainWorldX
				|| Expected.MainY != architecture.MainWorldY
				|| Expected.PurposeReceipt != purposeReceipt))
			{
				Failure = "The ground or production plan changed after its preview. Review the exact plan again; nothing was spent.";
				return false;
			}
			GameObject purposeCargo = null;
			if (purposeReceipt != null && !KingdomPurpose.ResolveCommitCargo(Z, Entry.Key,
				purposeReceipt, out purposeCargo, out Failure)) return false;
			GameObject reciprocalCargo = null;
			if (purposeReceipt != null && !KingdomPurpose.ResolveCommitReciprocalCargo(
				Z, Entry.Key, purposeReceipt, out reciprocalCargo, out Failure)) return false;
			KingdomWaterDebit water = null;
			KingdomMaterialDebit materials = null;
			if (purposeReceipt == null)
			{
				KingdomSurvey survey = KingdomSurvey.Take(Z, System);
				water = survey.ReserveExactWater(Entry.CostDrams);
				materials = KingdomMaterials.ReservePayment(Z, Entry.Key);
			}
			KingdomMaterialDebitCost claim = new KingdomMaterialDebitCost(
				KingdomMaterials.CostFor(Entry.Key), KingdomMaterials.BitCostFor(Entry.Key),
				KingdomMaterials.ExoticCostFor(Entry.Key));
			KingdomConstructionJob job = KingdomConstruction.NewJob(System, Z,
				KingdomConstructionRoute.PlotCommission, mainCell,
				null, Entry.Key, payload, Entry.CostDrams, claim,
				start, start + total);
			job.PhysicalReceipt = purposeReceipt;
			if (!KingdomConstruction.FreezeBuildTruth(job, System, Entry.Defence, true))
			{
				water?.Rollback();
				materials?.Cancel();
				Failure = "The plot's exact build effects could not be frozen.";
				return false;
			}
			KingdomConstructionStartResult funding;
			string fundingFailure;
			if (purposeReceipt == null)
				funding = KingdomConstruction.TryFundNew(job, water, materials,
					out job, out fundingFailure);
			else if (!KingdomPurpose.TryRequiredFundingObjectIds(job,
				out List<string> requiredObjects, out fundingFailure))
				funding = KingdomConstructionStartResult.Refused;
			else funding = KingdomConstruction.TryFundNewRouted(job, requiredObjects,
				out job, out fundingFailure);
			if (funding == KingdomConstructionStartResult.Refused)
			{
				Failure = fundingFailure ?? "The stores could not cover the plot after all.";
				return false;
			}
			if (funding == KingdomConstructionStartResult.Outstanding)
			{
				KingdomGovernanceScope.Commit("commission building");
				System.Ledger.Note(purposeReceipt == null
					? "{{r|The plot commission has a measured receipt still outstanding. Its ground remains queued and no paid claim will be charged twice.}}"
					: "{{r|The purpose commission has a measured receipt outstanding. Retry remains bound to its exact cargo object; if that identity cannot be reproved, the receipt requires inspection rather than substitution.}}");
				return true;
			}
			GameObject works;
			if (!ProjectPlot(System, Z, rect, Entry, spec, grid, SkinKey, carved, job,
				out works, out job, out string projectionFailure))
			{
				KingdomGovernanceScope.Commit("commission building");
				System.Ledger.Note("{{r|The paid plot could not be staked. Its durable receipt remains queued for another pass.}}");
				KingdomLog.Log("construction: plot projection waits: " + projectionFailure);
				return true;
			}
			KingdomGovernanceScope.Commit("commission building");
			KingdomChronicle.Record(System, "ground was staked at " + KingdomPresentation.Rich(System.KingdomDisplayName) + " for " + XRL.Language.Grammar.A(Entry.Name));
			string clause = KingdomLayoutRules.PlacementClause(KingdomLayout.PurposeOfEntry(Entry), outcome);
			MessageQueue.AddPlayerMessage("{{G|A " + KingdomPlotRules.SizeName(staked) + " plot is staked for the " + Entry.Name
				+ ((clause == null) ? "" : (" " + clause)) + ".}}");
			SayYielding(System, works.GetIntProperty(YieldingProperty) == 1, Entry.Name);
			return true;
		}

		private static KingdomPlotRules.PlotRect PlannedFootprint(Zone Z,
			KingdomPlotRules.PlotRect Rect, KingdomPlotRules.PlotSpec Spec)
		{
			HeartFor(Z, Rect, out var heartX, out var heartY);
			return KingdomPlotRules.HeartRungOf(Spec.Key) > 0
				? HeartFootprintFor(Z, Rect, Spec)
				: FootprintFor(Rect, Spec, heartX, heartY);
		}

		/// <summary>
		/// Raises the works on a rect that has already been chosen and surveyed. Spends nothing
		/// and refuses nothing &mdash; every judgement has been made by the time this is called.
		/// </summary>
		/// <returns>The works object, or null when the engine would not create it.</returns>
		public static GameObject Stake(KingdomSystem System, Zone Z, KingdomPlotRules.PlotRect Rect, KingdomRules.BuildEntry Entry, KingdomPlotRules.PlotSpec Spec, GroundGrid Grid, string SkinKey, bool Carved)
		{
			string zoningFailure = null;
			if (System == null || Z == null || Entry == null
				|| !KingdomZoning.Permits(System, Z.ZoneID, Entry, out zoningFailure))
			{
				KingdomLog.Log("architecture: direct plot stake refused: "
					+ (zoningFailure ?? "invalid stake authority"));
				return null;
			}
			if (!TryPreparePlotPayload(System, Z, Rect, Entry.Key, Entry.Category, SkinKey,
				out KingdomArchitectureIntent architecture, out _, out string architectureFailure))
			{
				KingdomLog.Log("architecture: direct plot stake refused: "
					+ (architectureFailure ?? "no authored architecture"));
				return null;
			}
			KingdomConstructionJob legacy = null;
			return Stake(System, Z, Rect, Entry, Spec, Grid, SkinKey, Carved,
				architecture, false, ref legacy);
		}

	}
}
