using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitecture
	{
		/// <summary>
		/// Catalogue-facing proof that one ordinary chain edge has a usable adjacent expansion in
		/// the frozen architecture transaction. Both endpoints must be exact typed records in one
		/// plan, one tier apart; the successor must authorize expansion and preserve every frozen
		/// predecessor variant. This grants no ground: live preflight still proves the containing
		/// envelope cell by cell before debit and again before application.
		/// </summary>
		internal static bool HasAuthorizedEnvelopeSuccessor(string PredecessorBuildKey,
			string SuccessorBuildKey, string LotType,
			KingdomPlotRules.PlotSize PredecessorPlot,
			KingdomPlotRules.PlotSize SuccessorPlot)
		{
			LoadState frozen = state;
			ArchitectureLotSize beforeSize;
			ArchitectureLotSize afterSize;
			string type = Fold(LotType);
			ResolvedRecord before;
			ResolvedRecord after;
			if (!frozen.Loaded || KingdomPlotRules.HeartRungOf(PredecessorBuildKey) != 0
				|| KingdomPlotRules.HeartRungOf(SuccessorBuildKey) != 0
				|| !ValidKey(PredecessorBuildKey) || !ValidKey(SuccessorBuildKey)
				|| !ValidKey(type) || !TryLotSize(PredecessorPlot, out beforeSize)
				|| !TryLotSize(SuccessorPlot, out afterSize)
				|| (int)afterSize != (int)beforeSize + 1
				|| !frozen.Records.TryGetValue(ExactRecordKey(PredecessorBuildKey, type,
					beforeSize), out before)
				|| !frozen.Records.TryGetValue(ExactRecordKey(SuccessorBuildKey, type,
					afterSize), out after)
				|| before.View.PlanKey != after.View.PlanKey
				|| after.Tier.Level != before.Tier.Level + 1
				|| !KingdomArchitectureTransitionRules.AllowsLotExpansion(
					after.Tier.IncomingTransitionMode)) return false;
			for (int i = 0; i < before.Tier.Variants.Count; i++)
			{
				ArchitectureVariantDraft successor;
				string failure;
				if (!KingdomArchitectureRules.TrySelectFrozenSuccessorVariant(
					after.Tier.Variants, before.Tier.Variants[i].Key, out successor,
					out failure)) return false;
			}
			return before.Tier.Variants.Count > 0;
		}

		/// <summary>
		/// Whether the frozen standing binding already declares this immediate ordinary successor.
		/// A declared same-size edge owns its refusal (including Replacement); envelope fallback may
		/// never bypass it merely because resolution or delta validation refused.
		/// </summary>
		internal static bool HasExactOrdinarySuccessor(string PredecessorBuildKey,
			string SuccessorBuildKey, string PlanKey, string BindingKey, string LotType,
			ArchitectureLotSize StandingLotSize)
		{
			LoadState frozen = state;
			string type = Fold(LotType);
			Dictionary<string, ResolvedRecord> binding;
			ResolvedRecord predecessor;
			ResolvedRecord successor;
			return frozen.Loaded && KingdomPlotRules.HeartRungOf(PredecessorBuildKey) == 0
				&& KingdomPlotRules.HeartRungOf(SuccessorBuildKey) == 0
				&& ValidKey(PredecessorBuildKey) && ValidKey(SuccessorBuildKey)
				&& ValidKey(PlanKey) && ValidKey(BindingKey) && ValidKey(type)
				&& KnownLotSize(StandingLotSize)
				&& frozen.RecordsByBinding.TryGetValue(BindingRecordKey(PlanKey, BindingKey,
					type, StandingLotSize), out binding)
				&& binding.TryGetValue(PredecessorBuildKey, out predecessor)
				&& binding.TryGetValue(SuccessorBuildKey, out successor)
				&& successor.Tier.Level == predecessor.Tier.Level + 1;
		}

		/// <summary>
		/// Resolves the adjacent larger exact record in one ordinary frozen lineage. This is not a
		/// nearest-size fallback for commissioning: it is used only after the same-size successor
		/// path has refused, and only an authored expansion-mode immediate tier may cross one rung.
		/// </summary>
		internal static bool TryResolveExpandingSuccessor(string PredecessorBuildKey,
			string PredecessorVariantKey, string SuccessorBuildKey, string PlanKey,
			string BindingKey, string LotType, ArchitectureLotSize StandingLotSize,
			ArchitectureFacing Facing, out ArchitectureLayoutSnapshot Snapshot,
			out string Failure)
		{
			Snapshot = null;
			Failure = null;
			LoadState frozen = state;
			if (!frozen.Loaded)
				return ResolveFault("architecture catalogue has not loaded", out Failure);
			string type = Fold(LotType);
			ResolvedRecord predecessor;
			if (KingdomPlotRules.HeartRungOf(PredecessorBuildKey) != 0
				|| KingdomPlotRules.HeartRungOf(SuccessorBuildKey) != 0
				|| !ValidKey(PredecessorBuildKey) || !ValidKey(PredecessorVariantKey)
				|| !ValidKey(SuccessorBuildKey) || !ValidKey(PlanKey)
				|| !ValidKey(BindingKey) || !ValidKey(type) || !KnownLotSize(StandingLotSize)
				|| !frozen.Records.TryGetValue(ExactRecordKey(PredecessorBuildKey, type,
					StandingLotSize), out predecessor)
				|| predecessor.View.PlanKey != PlanKey
				|| predecessor.View.BindingKey != BindingKey)
				return ResolveFault("standing plot has no exact ordinary frozen lineage",
					out Failure);

			ArchitectureLotSize targetSize =
				(ArchitectureLotSize)((int)StandingLotSize + 1);
			ResolvedRecord target;
			if (!KnownLotSize(targetSize)
				|| !frozen.Records.TryGetValue(ExactRecordKey(SuccessorBuildKey, type,
					targetSize), out target)
				|| target.View.PlanKey != PlanKey || Fold(target.View.TypeKey) != type
				|| target.Tier.Level != predecessor.Tier.Level + 1
				|| !KingdomArchitectureTransitionRules.AllowsLotExpansion(
					target.Tier.IncomingTransitionMode))
				return ResolveFault("no adjacent larger authored successor "
					+ (SuccessorBuildKey ?? "<null>")
					+ " continues the exact frozen plan and type with expansion authority",
					out Failure);
			ArchitectureVariantDraft variant;
			if (!KingdomArchitectureRules.TrySelectFrozenSuccessorVariant(
				target.Tier.Variants, PredecessorVariantKey, out variant,
				out Failure)) return false;
			return CompileFrozen(frozen, target, variant, Facing, out Snapshot, out Failure);
		}
	}
}
