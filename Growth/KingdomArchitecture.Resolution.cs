using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitecture
	{
		// --- Frozen resolution -------------------------------------------------------------

		/// <summary>
		/// Selects by frozen tier selectors, compiles the requested cardinal pose, then codec
		/// round-trips it so the caller receives the canonical durable snapshot representation.
		/// </summary>
		public static bool TryResolve(string BuildKey, ArchitectureSelectionContext Context,
			ArchitectureFacing Facing, out ArchitectureLayoutSnapshot Snapshot, out string Failure)
		{
			Snapshot = null;
			Failure = null;
			LoadState frozen = state;
			if (!frozen.Loaded) return ResolveFault("architecture catalogue has not loaded", out Failure);
			ResolvedRecord record;
			if (!TryUniqueRecord(frozen, BuildKey, out record, out Failure)) return false;
			ArchitectureVariantDraft variant;
			if (!KingdomArchitectureRules.TrySelectVariant(record.Tier.Variants, Context,
				out variant, out Failure)) return false;
			return CompileFrozen(frozen, record, variant, Facing, out Snapshot, out Failure);
		}

		/// <summary>
		/// Resolves one exact authored actual-size record. The requested size is identity, not a
		/// minimum or nearest-size hint, so a missing larger map always refuses.
		/// </summary>
		public static bool TryResolve(string BuildKey, string LotType,
			ArchitectureLotSize ActualLotSize, ArchitectureSelectionContext Context,
			ArchitectureFacing Facing, out ArchitectureLayoutSnapshot Snapshot, out string Failure)
		{
			Snapshot = null;
			Failure = null;
			LoadState frozen = state;
			if (!frozen.Loaded) return ResolveFault("architecture catalogue has not loaded", out Failure);
			ResolvedRecord record;
			if (!TryExactRecord(frozen, BuildKey, LotType, ActualLotSize,
				out record, out Failure)) return false;
			ArchitectureVariantDraft variant;
			if (!KingdomArchitectureRules.TrySelectVariant(record.Tier.Variants, Context,
				out variant, out Failure)) return false;
			return CompileFrozen(frozen, record, variant, Facing, out Snapshot, out Failure);
		}

		/// <summary>Exact-variant resolver used by deterministic preview and golden tooling.</summary>
		public static bool TryResolveVariant(string BuildKey, string VariantKey,
			ArchitectureFacing Facing, out ArchitectureLayoutSnapshot Snapshot, out string Failure)
		{
			Snapshot = null;
			Failure = null;
			LoadState frozen = state;
			if (!frozen.Loaded) return ResolveFault("architecture catalogue has not loaded", out Failure);
			ResolvedRecord record;
			if (!TryUniqueRecord(frozen, BuildKey, out record, out Failure)) return false;
			ArchitectureVariantDraft variant = null;
			for (int i = 0; i < record.Tier.Variants.Count; i++)
				if (record.Tier.Variants[i].Key == VariantKey) { variant = record.Tier.Variants[i]; break; }
			if (variant == null)
				return ResolveFault("building " + BuildKey + " has no variant " + (VariantKey ?? "<null>"), out Failure);
			return CompileFrozen(frozen, record, variant, Facing, out Snapshot, out Failure);
		}

		/// <summary>Exact typed-lot variant resolver used by deterministic gallery tooling.</summary>
		public static bool TryResolveVariant(string BuildKey, string LotType,
			ArchitectureLotSize ActualLotSize, string VariantKey, ArchitectureFacing Facing,
			out ArchitectureLayoutSnapshot Snapshot, out string Failure)
		{
			Snapshot = null;
			Failure = null;
			LoadState frozen = state;
			if (!frozen.Loaded) return ResolveFault("architecture catalogue has not loaded", out Failure);
			ResolvedRecord record;
			if (!TryExactRecord(frozen, BuildKey, LotType, ActualLotSize,
				out record, out Failure)) return false;
			ArchitectureVariantDraft variant = FindVariant(record, VariantKey);
			if (variant == null)
				return ResolveFault("building " + BuildKey + " has no variant "
					+ (VariantKey ?? "<null>"), out Failure);
			return CompileFrozen(frozen, record, variant, Facing, out Snapshot, out Failure);
		}

		/// <summary>
		/// Resolves an explicitly requested successor only inside the frozen typed binding named by
		/// the standing receipt. It never rebinds through another plan, type, or actual lot size.
		/// </summary>
		public static bool TryResolveSuccessor(string SuccessorBuildKey, string PlanKey,
			string BindingKey, string LotType, ArchitectureLotSize ActualLotSize,
			ArchitectureSelectionContext Context, ArchitectureFacing Facing,
			out ArchitectureLayoutSnapshot Snapshot, out string Failure)
		{
			return TryResolveSuccessor(null, null, SuccessorBuildKey, PlanKey, BindingKey, LotType,
				ActualLotSize, Context, Facing, out Snapshot, out Failure);
		}

		/// <summary>
		/// Exact successor resolver with the one reviewed cross-binding exception: adjacent rungs
		/// of the founding civic heart may advance S→M→L→XL. Both predecessor and successor
		/// must still exist as exact current typed records; no other cross-size lookup is attempted.
		/// </summary>
		public static bool TryResolveSuccessor(string PredecessorBuildKey,
			string SuccessorBuildKey, string PlanKey, string BindingKey, string LotType,
			ArchitectureLotSize ActualLotSize, ArchitectureSelectionContext Context,
			ArchitectureFacing Facing, out ArchitectureLayoutSnapshot Snapshot,
			out string Failure)
		{
			return TryResolveSuccessor(PredecessorBuildKey, null, SuccessorBuildKey,
				PlanKey, BindingKey, LotType, ActualLotSize, Context, Facing,
				out Snapshot, out Failure);
		}

		/// <summary>
		/// Standing-receipt successor resolver. The predecessor's frozen variant is identity:
		/// current settlement facts cannot restyle an occupied building during tier growth.
		/// </summary>
		public static bool TryResolveSuccessor(string PredecessorBuildKey,
			string PredecessorVariantKey, string SuccessorBuildKey, string PlanKey,
			string BindingKey, string LotType, ArchitectureLotSize ActualLotSize,
			ArchitectureSelectionContext Context, ArchitectureFacing Facing,
			out ArchitectureLayoutSnapshot Snapshot, out string Failure)
		{
			Snapshot = null;
			Failure = null;
			LoadState frozen = state;
			if (!frozen.Loaded) return ResolveFault("architecture catalogue has not loaded", out Failure);
			string type = Fold(LotType);
			Dictionary<string, ResolvedRecord> binding = null;
			ResolvedRecord record = null;
			ResolvedRecord ordinaryPredecessor = null;
			int beforeRung = KingdomPlotRules.HeartRungOf(PredecessorBuildKey);
			int afterRung = KingdomPlotRules.HeartRungOf(SuccessorBuildKey);
			bool heartRequest = beforeRung > 0 || afterRung > 0;
			bool ordinary = !heartRequest && ValidKey(PredecessorBuildKey)
				&& ValidKey(SuccessorBuildKey) && ValidKey(PlanKey)
				&& ValidKey(BindingKey) && ValidKey(type) && KnownLotSize(ActualLotSize)
				&& frozen.RecordsByBinding.TryGetValue(
					BindingRecordKey(PlanKey, BindingKey, type, ActualLotSize), out binding)
				&& binding.TryGetValue(PredecessorBuildKey, out ordinaryPredecessor)
				&& binding.TryGetValue(SuccessorBuildKey, out record)
				&& record.Tier.Level == ordinaryPredecessor.Tier.Level + 1;
			if (!ordinary)
			{
				ResolvedRecord predecessor;
				ArchitectureLotSize successorSize = (ArchitectureLotSize)
					KingdomPlotRules.HeartSizeForRung(afterRung);
				ArchitectureLotSize predecessorSize = (ArchitectureLotSize)
					KingdomPlotRules.HeartSizeForRung(beforeRung);
				if (beforeRung < 1 || afterRung != beforeRung + 1
					|| PlanKey != "civic-heart" || type != "civic"
					|| ActualLotSize != predecessorSize
					|| !ValidKey(BindingKey)
					|| !frozen.Records.TryGetValue(ExactRecordKey(PredecessorBuildKey,
						type, ActualLotSize), out predecessor)
					|| predecessor.View.PlanKey != PlanKey
					|| predecessor.View.BindingKey != BindingKey
					|| !frozen.Records.TryGetValue(ExactRecordKey(SuccessorBuildKey,
						type, successorSize), out record)
					|| record.View.PlanKey != "civic-heart"
					|| Fold(record.View.TypeKey) != "civic"
					|| record.View.LotSize != successorSize
					|| record.Tier.Level != predecessor.Tier.Level + 1)
					return ResolveFault("no valid authored successor "
						+ (SuccessorBuildKey ?? "<null>")
						+ " exists in the frozen typed binding or adjacent civic-heart rung",
						out Failure);
			}
			if (record.Tier.IncomingTransitionMode == ArchitectureTransitionMode.None
				|| (record.View.LotSize != ActualLotSize
					&& !KingdomArchitectureTransitionRules.AllowsLotExpansion(
						record.Tier.IncomingTransitionMode)))
				return ResolveFault("authored successor lacks a compatible incoming transition mode",
					out Failure);
			ArchitectureVariantDraft variant;
			if (PredecessorVariantKey == null)
			{
				if (!KingdomArchitectureRules.TrySelectVariant(record.Tier.Variants, Context,
					out variant, out Failure)) return false;
			}
			else if (!KingdomArchitectureRules.TrySelectFrozenSuccessorVariant(
				record.Tier.Variants, PredecessorVariantKey, out variant, out Failure)) return false;
			return CompileFrozen(frozen, record, variant, Facing, out Snapshot, out Failure);
		}

		private static bool TryUniqueRecord(LoadState Frozen, string BuildKey,
			out ResolvedRecord Record, out string Failure)
		{
			Record = null;
			List<ResolvedRecord> records;
			if (!ValidKey(BuildKey)
				|| !Frozen.RecordsByBuild.TryGetValue(BuildKey, out records))
				return ResolveFault("no valid authored architecture maps building "
					+ (BuildKey ?? "<null>"), out Failure);
			if (records.Count != 1)
				return ResolveFault("building " + BuildKey
					+ " has multiple authored typed lots; exact type and size are required", out Failure);
			Record = records[0];
			Failure = null;
			return true;
		}

		private static bool TryExactRecord(LoadState Frozen, string BuildKey, string LotType,
			ArchitectureLotSize ActualLotSize, out ResolvedRecord Record, out string Failure)
		{
			Record = null;
			string type = Fold(LotType);
			if (!ValidKey(BuildKey) || !ValidKey(type) || !KnownLotSize(ActualLotSize)
				|| !Frozen.Records.TryGetValue(ExactRecordKey(BuildKey, type, ActualLotSize),
					out Record))
				return ResolveFault("no valid authored architecture maps exact typed lot for building "
					+ (BuildKey ?? "<null>"), out Failure);
			Failure = null;
			return true;
		}

		private static ArchitectureVariantDraft FindVariant(ResolvedRecord Record, string VariantKey)
		{
			if (Record == null || Record.Tier == null || Record.Tier.Variants == null) return null;
			for (int i = 0; i < Record.Tier.Variants.Count; i++)
				if (Record.Tier.Variants[i].Key == VariantKey) return Record.Tier.Variants[i];
			return null;
		}

		private static bool CompileFrozen(LoadState Frozen, ResolvedRecord Record,
			ArchitectureVariantDraft Variant, ArchitectureFacing Facing,
			out ArchitectureLayoutSnapshot Snapshot, out string Failure)
		{
			Snapshot = null;
			string mapKey = string.IsNullOrEmpty(Variant.MapKey)
				? Record.Tier.MapKey : Variant.MapKey;
			string paletteKey = string.IsNullOrEmpty(Variant.PaletteKey)
				? Record.Tier.PaletteKey : Variant.PaletteKey;
			ArchitectureMapDraft map;
			ArchitecturePaletteDraft palette;
			if (!Frozen.Maps.TryGetValue(mapKey, out map)
				|| !Frozen.Palettes.TryGetValue(paletteKey, out palette))
				return ResolveFault("frozen architecture reference is unavailable", out Failure);
			ArchitectureCompileRequest request = new ArchitectureCompileRequest
			{
				PlanKey = Record.View.PlanKey, Binding = Record.Binding, Tier = Record.Tier,
				Variant = Variant, Map = map, Palette = palette,
				PoseRegistry = Frozen.PoseRegistry,
				CatalogueFootprintWidth = Record.CatalogueFootprintWidth,
				CatalogueFootprintHeight = Record.CatalogueFootprintHeight,
				CatalogueRoof = Record.CatalogueRoof,
				BuildingBlueprint = Record.View.BuildingBlueprint, Facing = Facing
			};
			ArchitectureLayoutSnapshot compiled;
			if (!KingdomArchitectureRules.TryCompile(request, out compiled, out Failure)) return false;
			string encoded;
			if (!KingdomArchitectureRules.TryEncodeSnapshot(compiled, out encoded, out Failure)) return false;
			return KingdomArchitectureRules.TryDecodeSnapshot(encoded, out Snapshot, out Failure);
		}

	}
}
