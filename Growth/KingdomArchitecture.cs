using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// Engine-facing schema-1 loader for authored settlement architecture. Loading is a single
	/// transaction: raw keyed declarations merge first, validation sees the complete merge, and
	/// only the resulting frozen catalogue is published. Resolution never asks KingdomData,
	/// KingdomPlots, or GameObjectFactory what the design means today.
	/// </summary>
	public static partial class KingdomArchitecture
	{
		public const int Schema = 1;
		public const int MaxFaults = 256;
		public const int MaxMappings = 1024;
		private const int MaxStreams = 256;
		// The authored catalogue already carries 514 map records (2026-08 census); 512 refused
		// the last rows in the live engine and cascaded into unresolved-variant faults. The cap
		// exists to bound hostile or runaway XML, not to ration lawful authored content, so it
		// carries headroom over the census.
		private const int MaxTopRecords = 1024;
		private const int MaxAttributeChars = 4096;

		private class RawRecord
		{
			public readonly string Key;
			public readonly string Origin;
			public readonly Dictionary<string, string> Values =
				new Dictionary<string, string>(StringComparer.Ordinal);
			public readonly HashSet<string> BadAttributes =
				new HashSet<string>(StringComparer.Ordinal);
			public bool Overflow;

			public RawRecord(string Key, string Origin)
			{
				this.Key = Key;
				this.Origin = Origin;
			}
		}

		private sealed class RawPalette : RawRecord
		{
			public readonly Dictionary<string, RawRecord> Slots =
				new Dictionary<string, RawRecord>(StringComparer.Ordinal);
			public RawPalette(string Key, string Origin) : base(Key, Origin) { }
		}

		private sealed class RawMap : RawRecord
		{
			public readonly Dictionary<string, RawRecord> Glyphs =
				new Dictionary<string, RawRecord>(StringComparer.Ordinal);
			public List<string> Rows;
			public bool RowsDeclared;
			public bool RowsOverflow;
			public RawMap(string Key, string Origin) : base(Key, Origin) { }
		}

		private sealed class RawPose : RawRecord
		{
			public RawPose(string Blueprint, string Origin) : base(Blueprint, Origin) { }
		}

		private sealed class RawTier : RawRecord
		{
			public readonly Dictionary<string, RawRecord> Requirements =
				new Dictionary<string, RawRecord>(StringComparer.Ordinal);
			public readonly Dictionary<string, RawRecord> Variants =
				new Dictionary<string, RawRecord>(StringComparer.Ordinal);
			public RawTier(string Key, string Origin) : base(Key, Origin) { }
		}

		private sealed class RawBinding : RawRecord
		{
			public readonly Dictionary<string, RawTier> Tiers =
				new Dictionary<string, RawTier>(StringComparer.Ordinal);
			public RawBinding(string Key, string Origin) : base(Key, Origin) { }
		}

		private sealed class RawPlan : RawRecord
		{
			public readonly Dictionary<string, RawBinding> Bindings =
				new Dictionary<string, RawBinding>(StringComparer.Ordinal);
			public RawPlan(string Key, string Origin) : base(Key, Origin) { }
		}

		internal sealed class FrozenBuilding
		{
			public string Key;
			public string Blueprint;
			public string Category;
			public bool HasPlot;
			public ArchitectureLotSize LotSize;
			public int FootprintWidth;
			public int FootprintHeight;
			public KingdomPlotRules.RoofState Roof;
		}

		private sealed class ResolvedRecord
		{
			public KingdomArchitectureMapping View;
			public ArchitectureBindingDraft Binding;
			public ArchitectureTierDraft Tier;
			public int CatalogueFootprintWidth;
			public int CatalogueFootprintHeight;
			public KingdomPlotRules.RoofState CatalogueRoof;
		}

		private sealed class LoadState
		{
			public bool Loaded;
			public readonly Dictionary<string, RawPalette> RawPalettes =
				new Dictionary<string, RawPalette>(StringComparer.Ordinal);
			public readonly Dictionary<string, RawMap> RawMaps =
				new Dictionary<string, RawMap>(StringComparer.Ordinal);
			public readonly Dictionary<string, RawPose> RawPoses =
				new Dictionary<string, RawPose>(StringComparer.Ordinal);
			public readonly Dictionary<string, RawPlan> RawPlans =
				new Dictionary<string, RawPlan>(StringComparer.Ordinal);
			public readonly Dictionary<string, ArchitecturePaletteDraft> Palettes =
				new Dictionary<string, ArchitecturePaletteDraft>(StringComparer.Ordinal);
			public readonly Dictionary<string, ArchitectureMapDraft> Maps =
				new Dictionary<string, ArchitectureMapDraft>(StringComparer.Ordinal);
			public ArchitecturePoseRegistry PoseRegistry = ArchitecturePoseRegistry.Empty;
			public readonly Dictionary<string, ArchitecturePlanDraft> Plans =
				new Dictionary<string, ArchitecturePlanDraft>(StringComparer.Ordinal);
			public readonly Dictionary<string, FrozenBuilding> Buildings =
				new Dictionary<string, FrozenBuilding>(StringComparer.Ordinal);
			/// <summary>Exact BuildKey + folded lot type + authored actual-size index.</summary>
			public readonly Dictionary<string, ResolvedRecord> Records =
				new Dictionary<string, ResolvedRecord>(StringComparer.Ordinal);
			/// <summary>Compatibility buckets. A BuildKey-only lookup is legal only at count one.</summary>
			public readonly Dictionary<string, List<ResolvedRecord>> RecordsByBuild =
				new Dictionary<string, List<ResolvedRecord>>(StringComparer.Ordinal);
			/// <summary>Frozen PlanKey + BindingKey + typed actual lot successor index.</summary>
			public readonly Dictionary<string, Dictionary<string, ResolvedRecord>> RecordsByBinding =
				new Dictionary<string, Dictionary<string, ResolvedRecord>>(StringComparer.Ordinal);
			public readonly List<KingdomArchitectureFault> Faults =
				new List<KingdomArchitectureFault>();
			public readonly HashSet<string> FaultKeys =
				new HashSet<string>(StringComparer.Ordinal);
			public bool FaultOverflow;
		}

		private static LoadState state = new LoadState();
		private static bool reloading;

		public static bool Loaded { get { return state.Loaded; } }
		public static bool Healthy { get { return state.Loaded && state.Faults.Count == 0; } }
		public static int MappingCount { get { return state.Records.Count; } }
		public static int FaultCount { get { return state.Faults.Count; } }

		/// <summary>Copies all valid mappings in ordinal exact-record order.</summary>
		public static IList<KingdomArchitectureMapping> InspectMappings()
		{
			List<ResolvedRecord> records = OrderedRecords(state.Records);
			List<KingdomArchitectureMapping> result =
				new List<KingdomArchitectureMapping>(records.Count);
			for (int i = 0; i < records.Count; i++) result.Add(records[i].View);
			return result.AsReadOnly();
		}

		/// <summary>Legacy BuildKey-only lookup. Ambiguous multi-size mappings fail closed.</summary>
		public static bool TryGetMapping(string BuildKey, out KingdomArchitectureMapping Mapping)
		{
			Mapping = null;
			List<ResolvedRecord> records;
			if (!ValidKey(BuildKey)
				|| !state.RecordsByBuild.TryGetValue(BuildKey, out records) || records.Count != 1)
				return false;
			Mapping = records[0].View;
			return true;
		}

		/// <summary>Exact lookup for one authored typed lot; never falls back to another size.</summary>
		public static bool TryGetMapping(string BuildKey, string LotType,
			ArchitectureLotSize ActualLotSize, out KingdomArchitectureMapping Mapping)
		{
			Mapping = null;
			ResolvedRecord record;
			string type = Fold(LotType);
			if (!ValidKey(BuildKey) || !ValidKey(type) || !KnownLotSize(ActualLotSize)
				|| !state.Records.TryGetValue(ExactRecordKey(BuildKey, type, ActualLotSize),
					out record)) return false;
			Mapping = record.View;
			return true;
		}

	}
}
