using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>One bounded, named problem in the merged authored-architecture catalogue.</summary>
	public sealed class KingdomArchitectureFault
	{
		public string Name { get; private set; }
		public string Message { get; private set; }

		internal KingdomArchitectureFault(string Name, string Message)
		{
			this.Name = Name;
			this.Message = Message;
		}

		public override string ToString()
		{
			return Name + ": " + Message;
		}
	}

	/// <summary>
	/// Immutable scalar view of one exact building-to-tier mapping. Preview tools may enumerate
	/// these records, then ask <see cref="KingdomArchitecture.TryResolveVariant"/> for each named
	/// variant and pose. Mutable XML drafts are never exposed.
	/// </summary>
	public sealed class KingdomArchitectureMapping
	{
		private readonly string[] variantKeys;

		public string BuildKey { get; private set; }
		public string BuildingBlueprint { get; private set; }
		public string Category { get; private set; }
		public string PlanKey { get; private set; }
		public string BindingKey { get; private set; }
		public string TierKey { get; private set; }
		public int TierLevel { get; private set; }
		public string TypeKey { get; private set; }
		public ArchitectureLotSize LotSize { get; private set; }
		public ArchitectureFrontage Frontage { get; private set; }
		public string DefaultMapKey { get; private set; }
		public string DefaultPaletteKey { get; private set; }

		/// <summary>A defensive, ordinal list suitable for deterministic gallery generation.</summary>
		public IList<string> VariantKeys
		{
			get { return Array.AsReadOnly((string[])variantKeys.Clone()); }
		}

		internal KingdomArchitectureMapping(string BuildingBlueprint, string Category,
			string PlanKey,
			ArchitectureBindingDraft Binding, ArchitectureTierDraft Tier)
		{
			BuildKey = Tier.BuildKey;
			this.BuildingBlueprint = BuildingBlueprint;
			this.Category = Category;
			this.PlanKey = PlanKey;
			BindingKey = Binding.Key;
			TierKey = Tier.Key;
			TierLevel = Tier.Level;
			TypeKey = Binding.TypeKey;
			LotSize = Binding.Size;
			Frontage = Binding.Frontage;
			DefaultMapKey = Tier.MapKey;
			DefaultPaletteKey = Tier.PaletteKey;
			variantKeys = new string[Tier.Variants.Count];
			for (int i = 0; i < Tier.Variants.Count; i++) variantKeys[i] = Tier.Variants[i].Key;
			Array.Sort(variantKeys, StringComparer.Ordinal);
		}
	}

	/// <summary>
	/// Engine-facing schema-1 loader for authored settlement architecture. Loading is a single
	/// transaction: raw keyed declarations merge first, validation sees the complete merge, and
	/// only the resulting frozen catalogue is published. Resolution never asks KingdomData,
	/// KingdomPlots, or GameObjectFactory what the design means today.
	/// </summary>
	public static class KingdomArchitecture
	{
		public const int Schema = 1;
		public const int MaxFaults = 256;
		public const int MaxMappings = 512;
		private const int MaxStreams = 256;
		private const int MaxTopRecords = 512;
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
		}

		private sealed class ResolvedRecord
		{
			public KingdomArchitectureMapping View;
			public ArchitectureBindingDraft Binding;
			public ArchitectureTierDraft Tier;
		}

		private sealed class LoadState
		{
			public bool Loaded;
			public readonly Dictionary<string, RawPalette> RawPalettes =
				new Dictionary<string, RawPalette>(StringComparer.Ordinal);
			public readonly Dictionary<string, RawMap> RawMaps =
				new Dictionary<string, RawMap>(StringComparer.Ordinal);
			public readonly Dictionary<string, RawPlan> RawPlans =
				new Dictionary<string, RawPlan>(StringComparer.Ordinal);
			public readonly Dictionary<string, ArchitecturePaletteDraft> Palettes =
				new Dictionary<string, ArchitecturePaletteDraft>(StringComparer.Ordinal);
			public readonly Dictionary<string, ArchitectureMapDraft> Maps =
				new Dictionary<string, ArchitectureMapDraft>(StringComparer.Ordinal);
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

		/// <summary>Copies the bounded fault report; callers cannot mutate loader authority.</summary>
		public static IList<KingdomArchitectureFault> InspectFaults()
		{
			return new List<KingdomArchitectureFault>(state.Faults).AsReadOnly();
		}

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

		/// <summary>
		/// Rebuilds from the exact KingdomBuildings entries already merged by the caller. Calling
		/// KingdomData from here would recurse into its in-progress load, so this enumerable is the
		/// only building authority accepted.
		/// </summary>
		public static void Reload(IEnumerable<KingdomRules.BuildEntry> Buildings)
		{
			if (reloading)
			{
				MetricsManager.LogError("ThousandAndFirst KingdomArchitectures: recursive reload refused");
				return;
			}
			reloading = true;
			LoadState next = new LoadState();
			try
			{
				FreezeBuildings(next, Buildings);
				LoadXml(next);
				Materialise(next);
				next.Loaded = true;
			}
			catch (Exception exception)
			{
				AddFault(next, "catalogue", "load failed: " + exception.Message);
				// A catalogue-wide exception has no exact entry boundary. Publish no partial index.
				next.Records.Clear();
				next.RecordsByBuild.Clear();
				next.RecordsByBinding.Clear();
				next.Loaded = true;
			}
			finally
			{
				state = next;
				reloading = false;
			}
			ReportFaults(next);
		}

		/// <summary>Writes the current bounded named report to Qud's error log.</summary>
		public static void ReportFaults()
		{
			ReportFaults(state);
		}

		private static void ReportFaults(LoadState State)
		{
			for (int i = 0; i < State.Faults.Count; i++)
				MetricsManager.LogError("ThousandAndFirst KingdomArchitectures: " + State.Faults[i]);
		}

		private static void FreezeBuildings(LoadState State,
			IEnumerable<KingdomRules.BuildEntry> Buildings)
		{
			if (Buildings == null)
			{
				AddFault(State, "buildings", "the merged KingdomBuildings view is absent");
				return;
			}
			foreach (KingdomRules.BuildEntry entry in Buildings)
			{
				if (entry == null || !ValidKey(entry.Key))
				{
					AddFault(State, "building", "an unnamed or malformed merged building was supplied");
					continue;
				}
				if (State.Buildings.ContainsKey(entry.Key))
				{
					AddFault(State, "building " + entry.Key, "the merged view contains the key twice");
					continue;
				}
				FrozenBuilding frozen = new FrozenBuilding
				{
					Key = entry.Key,
					Blueprint = entry.Blueprint,
					Category = Fold(entry.Category)
				};
				KingdomPlotRules.PlotSpec spec;
				if (KingdomPlots.TryGetSpec(entry.Key, out spec) && spec != null
					&& TryLotSize(spec.Size, out ArchitectureLotSize size))
				{
					frozen.HasPlot = true;
					frozen.LotSize = size;
				}
				State.Buildings.Add(entry.Key, frozen);
			}
		}

		private static void LoadXml(LoadState State)
		{
			int streams = 0;
			foreach (XmlDataHelper xml in DataManager.YieldXMLStreamsWithRoot("KingdomArchitectures"))
			{
				streams++;
				if (streams > MaxStreams)
				{
					AddFault(State, "catalogue", "more than " + MaxStreams
						+ " KingdomArchitectures streams were supplied");
					break;
				}
				try { ParseStream(State, xml); }
				catch (Exception exception)
				{
					AddFault(State, "stream " + streams.ToString(CultureInfo.InvariantCulture),
						"XML parse failed: " + exception.Message);
				}
			}
			if (streams == 0) AddFault(State, "catalogue", "no KingdomArchitectures schema-1 stream was found");
		}

		private static void ParseStream(LoadState State, XmlDataHelper Xml)
		{
			bool foundRoot = false;
			Dictionary<string, Action<XmlDataHelper>> roots =
				new Dictionary<string, Action<XmlDataHelper>>(StringComparer.Ordinal)
				{
					{ "KingdomArchitectures", delegate(XmlDataHelper root)
						{
							foundRoot = true;
							HandleRoot(State, root);
						} }
				};
			Xml.HandleNodes(roots, delegate(XmlDataHelper unknown)
			{
				AddFault(State, "root", "expected uppercase KingdomArchitectures at " + Source(unknown));
				Skip(unknown);
			});
			if (!foundRoot) AddFault(State, "root", "stream did not contain uppercase KingdomArchitectures");
		}

		private static void HandleRoot(LoadState State, XmlDataHelper Xml)
		{
			string schema = Xml.GetAttribute("Schema");
			if (schema != Schema.ToString(CultureInfo.InvariantCulture))
			{
				AddFault(State, "root", "unsupported or absent Schema at " + Source(Xml));
				Skip(Xml);
				return;
			}
			Dictionary<string, Action<XmlDataHelper>> nodes =
				new Dictionary<string, Action<XmlDataHelper>>(StringComparer.Ordinal)
				{
					{ "palette", delegate(XmlDataHelper child) { HandlePalette(State, child); } },
					{ "map", delegate(XmlDataHelper child) { HandleMap(State, child); } },
					{ "plan", delegate(XmlDataHelper child) { HandlePlan(State, child); } }
				};
			Xml.HandleNodes(nodes, delegate(XmlDataHelper unknown) { Unknown(State, unknown); });
		}

		private static void HandlePalette(LoadState State, XmlDataHelper Xml)
		{
			string key = Xml.GetAttribute("Key");
			RawPalette palette = GetPalette(State, key, Source(Xml));
			if (palette == null) { Skip(Xml); return; }
			Dictionary<string, Action<XmlDataHelper>> nodes =
				new Dictionary<string, Action<XmlDataHelper>>(StringComparer.Ordinal)
				{
					{ "slot", delegate(XmlDataHelper child) { HandleSlot(State, palette, child); } }
				};
			Xml.HandleNodes(nodes, delegate(XmlDataHelper unknown) { Unknown(State, unknown); });
		}

		private static void HandleSlot(LoadState State, RawPalette Palette, XmlDataHelper Xml)
		{
			string key = Xml.GetAttribute("Key");
			RawRecord slot = GetRecord(State, Palette.Slots, key,
				KingdomArchitectureRules.MaxPaletteSlots, "palette " + Palette.Key + " slot", Source(Xml));
			if (slot == null) Palette.Overflow = true;
			if (slot != null)
			{
				Set(State, slot, "Blueprint", Xml.GetAttribute("Blueprint"));
				Set(State, slot, "Role", Xml.GetAttribute("Role"));
				Set(State, slot, "Material", Xml.GetAttribute("Material"));
				Set(State, slot, "MinTech", Xml.GetAttribute("MinTech"));
				Set(State, slot, "Knowledge", Xml.GetAttribute("Knowledge"));
				Set(State, slot, "Power", Xml.GetAttribute("Power"));
				Set(State, slot, "Natural", Xml.GetAttribute("Natural"));
			}
			Xml.DoneWithElement();
		}

		private static void HandleMap(LoadState State, XmlDataHelper Xml)
		{
			string key = Xml.GetAttribute("Key");
			RawMap map = GetMap(State, key, Source(Xml));
			if (map == null) { Skip(Xml); return; }
			Set(State, map, "Width", Xml.GetAttribute("Width"));
			Set(State, map, "Height", Xml.GetAttribute("Height"));
			Set(State, map, "DefaultCover", Xml.GetAttribute("DefaultCover"));
			List<string> rows = new List<string>();
			bool rowBlock = false;
			bool rowOverflow = false;
			Dictionary<string, Action<XmlDataHelper>> nodes =
				new Dictionary<string, Action<XmlDataHelper>>(StringComparer.Ordinal)
				{
					{ "glyph", delegate(XmlDataHelper child) { HandleGlyph(State, map, child); } },
					{ "row", delegate(XmlDataHelper child)
						{
							rowBlock = true;
							string cells = child.GetAttribute("Cells");
							if (cells == null || cells.Length > KingdomArchitectureRules.MaxMapArea
								|| HasControl(cells))
							{
								rowOverflow = true;
								cells = null;
							}
							if (rows.Count < KingdomArchitectureRules.MaxMapArea) rows.Add(cells);
							else rowOverflow = true;
							child.DoneWithElement();
						} }
				};
			Xml.HandleNodes(nodes, delegate(XmlDataHelper unknown) { Unknown(State, unknown); });
			if (rowBlock)
			{
				// A declaration owns its entire row block. Never splice later rows into an older map.
				map.Rows = rows;
				map.RowsDeclared = true;
				map.RowsOverflow = rowOverflow;
			}
		}

		private static void HandleGlyph(LoadState State, RawMap Map, XmlDataHelper Xml)
		{
			string character = Xml.GetAttribute("Char");
			RawRecord glyph = GetRecord(State, Map.Glyphs, character,
				KingdomArchitectureRules.MaxGlyphs, "map " + Map.Key + " glyph", Source(Xml));
			if (glyph == null) Map.Overflow = true;
			if (glyph != null)
			{
				Set(State, glyph, "Ground", Xml.GetAttribute("Ground"));
				Set(State, glyph, "Structure", Xml.GetAttribute("Structure"));
				Set(State, glyph, "Object", Xml.GetAttribute("Object"));
				Set(State, glyph, "Claim", Xml.GetAttribute("Claim"));
				SetAlias(State, glyph, "Pass", Xml.GetAttribute("Pass"),
					Xml.GetAttribute("Passability"), "Passability");
				Set(State, glyph, "Cover", Xml.GetAttribute("Cover"));
				SetAlias(State, glyph, "Stateful", Xml.GetAttribute("Stateful"),
					Xml.GetAttribute("StatefulObject"), "StatefulObject");
				Set(State, glyph, "Anchors", Xml.GetAttribute("Anchors"));
			}
			Xml.DoneWithElement();
		}

		private static void HandlePlan(LoadState State, XmlDataHelper Xml)
		{
			string key = Xml.GetAttribute("Key");
			RawPlan plan = GetPlan(State, key, Source(Xml));
			if (plan == null) { Skip(Xml); return; }
			Dictionary<string, Action<XmlDataHelper>> nodes =
				new Dictionary<string, Action<XmlDataHelper>>(StringComparer.Ordinal)
				{
					{ "binding", delegate(XmlDataHelper child) { HandleBinding(State, plan, child); } }
				};
			Xml.HandleNodes(nodes, delegate(XmlDataHelper unknown) { Unknown(State, unknown); });
		}

		private static void HandleBinding(LoadState State, RawPlan Plan, XmlDataHelper Xml)
		{
			string key = Xml.GetAttribute("Key");
			RawBinding binding = GetBinding(State, Plan, key, Source(Xml));
			if (binding == null) { Skip(Xml); return; }
			Set(State, binding, "Type", Xml.GetAttribute("Type"));
			Set(State, binding, "Size", Xml.GetAttribute("Size"));
			SetAlias(State, binding, "Frontage", Xml.GetAttribute("Frontage"),
				Xml.GetAttribute("Facing"), "Facing");
			Dictionary<string, Action<XmlDataHelper>> nodes =
				new Dictionary<string, Action<XmlDataHelper>>(StringComparer.Ordinal)
				{
					{ "tier", delegate(XmlDataHelper child) { HandleTier(State, binding, child); } }
				};
			Xml.HandleNodes(nodes, delegate(XmlDataHelper unknown) { Unknown(State, unknown); });
		}

		private static void HandleTier(LoadState State, RawBinding Binding, XmlDataHelper Xml)
		{
			string key = Xml.GetAttribute("Key");
			RawTier tier = GetTier(State, Binding, key, Source(Xml));
			if (tier == null) { Skip(Xml); return; }
			Set(State, tier, "BuildKey", Xml.GetAttribute("BuildKey"));
			Set(State, tier, "Level", Xml.GetAttribute("Level"));
			SetAlias(State, tier, "Map", Xml.GetAttribute("Map"), Xml.GetAttribute("MapKey"), "MapKey");
			SetAlias(State, tier, "Palette", Xml.GetAttribute("Palette"),
				Xml.GetAttribute("PaletteKey"), "PaletteKey");
			Dictionary<string, Action<XmlDataHelper>> nodes =
				new Dictionary<string, Action<XmlDataHelper>>(StringComparer.Ordinal)
				{
					{ "require", delegate(XmlDataHelper child) { HandleRequirement(State, tier, child); } },
					{ "variant", delegate(XmlDataHelper child) { HandleVariant(State, tier, child); } }
				};
			Xml.HandleNodes(nodes, delegate(XmlDataHelper unknown) { Unknown(State, unknown); });
		}

		private static void HandleRequirement(LoadState State, RawTier Tier, XmlDataHelper Xml)
		{
			string declaredKey = Xml.GetAttribute("Key");
			string role = Xml.GetAttribute("Role");
			string key = declaredKey ?? role;
			bool existed = key != null && Tier.Requirements.ContainsKey(key);
			RawRecord requirement = GetRecord(State, Tier.Requirements, key,
				KingdomArchitectureRules.MaxRequirementsPerTier,
				"tier " + Tier.Key + " requirement", Source(Xml));
			if (requirement == null) Tier.Overflow = true;
			if (requirement != null)
			{
				if (role != null) Set(State, requirement, "Role", role);
				else if (!existed) Set(State, requirement, "Role", declaredKey);
				SetAlias(State, requirement, "Min", Xml.GetAttribute("Min"),
					Xml.GetAttribute("Minimum"), "Minimum");
				SetAlias(State, requirement, "Max", Xml.GetAttribute("Max"),
					Xml.GetAttribute("Maximum"), "Maximum");
			}
			else
			{
				// Mark aliases read even when the missing key prevents a merge.
				Xml.GetAttribute("Min"); Xml.GetAttribute("Minimum");
				Xml.GetAttribute("Max"); Xml.GetAttribute("Maximum");
			}
			Xml.DoneWithElement();
		}

		private static void HandleVariant(LoadState State, RawTier Tier, XmlDataHelper Xml)
		{
			string key = Xml.GetAttribute("Key");
			RawRecord variant = GetRecord(State, Tier.Variants, key,
				KingdomArchitectureRules.MaxVariantsPerTier,
				"tier " + Tier.Key + " variant", Source(Xml));
			if (variant == null) Tier.Overflow = true;
			string priority = Xml.GetAttribute("Priority");
			string map = Xml.GetAttribute("Map");
			string mapKey = Xml.GetAttribute("MapKey");
			string palette = Xml.GetAttribute("Palette");
			string paletteKey = Xml.GetAttribute("PaletteKey");
			string styles = Xml.GetAttribute("Styles");
			string creeds = Xml.GetAttribute("Creeds");
			string cultures = Xml.GetAttribute("Cultures");
			string species = Xml.GetAttribute("Species");
			string genotypes = Xml.GetAttribute("Genotypes");
			string bodies = Xml.GetAttribute("Bodies");
			string terrains = Xml.GetAttribute("Terrains");
			string strata = Xml.GetAttribute("Strata");
			string minStage = Xml.GetAttribute("MinStage");
			string minimumStage = Xml.GetAttribute("MinimumStage");
			string maxStage = Xml.GetAttribute("MaxStage");
			string maximumStage = Xml.GetAttribute("MaximumStage");
			string minTech = Xml.GetAttribute("MinTech");
			string minimumTech = Xml.GetAttribute("MinimumTech");
			string maxTech = Xml.GetAttribute("MaxTech");
			string maximumTech = Xml.GetAttribute("MaximumTech");
			if (variant != null)
			{
				Set(State, variant, "Priority", priority);
				SetAlias(State, variant, "Map", map, mapKey, "MapKey");
				SetAlias(State, variant, "Palette", palette, paletteKey, "PaletteKey");
				Set(State, variant, "Styles", styles);
				Set(State, variant, "Creeds", creeds);
				Set(State, variant, "Cultures", cultures);
				Set(State, variant, "Species", species);
				Set(State, variant, "Genotypes", genotypes);
				Set(State, variant, "Bodies", bodies);
				Set(State, variant, "Terrains", terrains);
				Set(State, variant, "Strata", strata);
				SetAlias(State, variant, "MinStage", minStage, minimumStage, "MinimumStage");
				SetAlias(State, variant, "MaxStage", maxStage, maximumStage, "MaximumStage");
				SetAlias(State, variant, "MinTech", minTech, minimumTech, "MinimumTech");
				SetAlias(State, variant, "MaxTech", maxTech, maximumTech, "MaximumTech");
			}
			Xml.DoneWithElement();
		}

		// --- Raw merge helpers --------------------------------------------------------------

		private static RawPalette GetPalette(LoadState State, string Key, string Origin)
		{
			if (!ValidKey(Key))
			{
				AddFault(State, "palette", "missing or malformed Key at " + Origin);
				return null;
			}
			RawPalette result;
			if (State.RawPalettes.TryGetValue(Key, out result)) return result;
			if (State.RawPalettes.Count >= MaxTopRecords)
			{
				AddFault(State, "palettes", "record bound exceeded");
				return null;
			}
			result = new RawPalette(Key, Origin);
			State.RawPalettes.Add(Key, result);
			return result;
		}

		private static RawMap GetMap(LoadState State, string Key, string Origin)
		{
			if (!ValidKey(Key))
			{
				AddFault(State, "map", "missing or malformed Key at " + Origin);
				return null;
			}
			RawMap result;
			if (State.RawMaps.TryGetValue(Key, out result)) return result;
			if (State.RawMaps.Count >= MaxTopRecords)
			{
				AddFault(State, "maps", "record bound exceeded");
				return null;
			}
			result = new RawMap(Key, Origin);
			State.RawMaps.Add(Key, result);
			return result;
		}

		private static RawPlan GetPlan(LoadState State, string Key, string Origin)
		{
			if (!ValidKey(Key))
			{
				AddFault(State, "plan", "missing or malformed Key at " + Origin);
				return null;
			}
			RawPlan result;
			if (State.RawPlans.TryGetValue(Key, out result)) return result;
			if (State.RawPlans.Count >= MaxTopRecords)
			{
				AddFault(State, "plans", "record bound exceeded");
				return null;
			}
			result = new RawPlan(Key, Origin);
			State.RawPlans.Add(Key, result);
			return result;
		}

		private static RawBinding GetBinding(LoadState State, RawPlan Plan,
			string Key, string Origin)
		{
			if (!ValidKey(Key))
			{
				AddFault(State, "plan " + Plan.Key + " binding",
					"missing or malformed Key at " + Origin);
				return null;
			}
			RawBinding result;
			if (Plan.Bindings.TryGetValue(Key, out result)) return result;
			if (Plan.Bindings.Count >= KingdomArchitectureRules.MaxBindingsPerPlan)
			{
				Plan.Overflow = true;
				AddFault(State, "plan " + Plan.Key, "binding bound exceeded");
				return null;
			}
			result = new RawBinding(Key, Origin);
			Plan.Bindings.Add(Key, result);
			return result;
		}

		private static RawTier GetTier(LoadState State, RawBinding Binding,
			string Key, string Origin)
		{
			if (!ValidKey(Key))
			{
				AddFault(State, "binding " + Binding.Key + " tier",
					"missing or malformed Key at " + Origin);
				return null;
			}
			RawTier result;
			if (Binding.Tiers.TryGetValue(Key, out result)) return result;
			if (Binding.Tiers.Count >= KingdomArchitectureRules.MaxTiersPerBinding)
			{
				Binding.Overflow = true;
				AddFault(State, "binding " + Binding.Key, "tier bound exceeded");
				return null;
			}
			result = new RawTier(Key, Origin);
			Binding.Tiers.Add(Key, result);
			return result;
		}

		private static RawRecord GetRecord(LoadState State,
			Dictionary<string, RawRecord> Records, string Key, int Maximum,
			string Scope, string Origin)
		{
			if (!ValidKey(Key))
			{
				AddFault(State, Scope, "missing or malformed key at " + Origin);
				return null;
			}
			RawRecord result;
			if (Records.TryGetValue(Key, out result)) return result;
			if (Records.Count >= Maximum)
			{
				AddFault(State, Scope, "record bound exceeded");
				return null;
			}
			result = new RawRecord(Key, Origin);
			Records.Add(Key, result);
			return result;
		}

		private static void Set(LoadState State, RawRecord Record, string Name, string Value)
		{
			if (Value == null) return; // omission is inheritance across XML streams.
			if (Value.Length > MaxAttributeChars || HasControl(Value))
			{
				Record.Values.Remove(Name);
				Record.BadAttributes.Add(Name);
				return;
			}
			Record.BadAttributes.Remove(Name);
			Record.Values[Name] = Value;
		}

		private static void SetAlias(LoadState State, RawRecord Record, string Name,
			string Canonical, string Alias, string AliasName)
		{
			if (Canonical != null && Alias != null
				&& !string.Equals(Canonical, Alias, StringComparison.OrdinalIgnoreCase))
			{
				Record.Values.Remove(Name);
				Record.BadAttributes.Add(Name);
				return;
			}
			Set(State, Record, Name, Canonical ?? Alias);
		}

		private static void Unknown(LoadState State, XmlDataHelper Xml)
		{
			AddFault(State, "node " + Xml.Name, "unknown architecture node at " + Source(Xml));
			Skip(Xml);
		}

		private static void Skip(XmlDataHelper Xml)
		{
			Xml.HandleNodes(new Dictionary<string, Action<XmlDataHelper>>(),
				delegate(XmlDataHelper child) { Skip(child); });
		}

		private static string Source(XmlDataHelper Xml)
		{
			try { return Xml.GetSourcePoint(); }
			catch { return "an unknown source"; }
		}

		// --- Full-merge materialisation -----------------------------------------------------

		private static void Materialise(LoadState State)
		{
			List<string> paletteKeys = OrderedKeys(State.RawPalettes);
			for (int i = 0; i < paletteKeys.Count; i++)
			{
				RawPalette raw = State.RawPalettes[paletteKeys[i]];
				ArchitecturePaletteDraft draft;
				if (TryPalette(State, raw, out draft)) State.Palettes.Add(draft.Key, draft);
			}

			List<string> mapKeys = OrderedKeys(State.RawMaps);
			for (int i = 0; i < mapKeys.Count; i++)
			{
				RawMap raw = State.RawMaps[mapKeys[i]];
				ArchitectureMapDraft draft;
				if (TryMap(State, raw, out draft)) State.Maps.Add(draft.Key, draft);
			}

			List<string> planKeys = OrderedKeys(State.RawPlans);
			for (int i = 0; i < planKeys.Count; i++)
			{
				RawPlan raw = State.RawPlans[planKeys[i]];
				ArchitecturePlanDraft draft;
				if (TryPlan(State, raw, out draft)) State.Plans.Add(draft.Key, draft);
			}

			Dictionary<string, int> exactCounts =
				new Dictionary<string, int>(StringComparer.Ordinal);
			bool mappingOverflow = false;
			int mappingDeclarations = 0;
			List<string> convertedPlans = OrderedKeys(State.Plans);
			for (int p = 0; p < convertedPlans.Count; p++)
			{
				ArchitecturePlanDraft plan = State.Plans[convertedPlans[p]];
				for (int b = 0; b < plan.Bindings.Count; b++)
					for (int t = 0; t < plan.Bindings[b].Tiers.Count; t++)
					{
						mappingDeclarations++;
						if (mappingDeclarations > MaxMappings)
						{
							mappingOverflow = true;
							continue;
						}
						ArchitectureBindingDraft binding = plan.Bindings[b];
						string key = ExactRecordKey(binding.Tiers[t].BuildKey,
							Fold(binding.TypeKey), binding.Size);
						int count;
						exactCounts.TryGetValue(key, out count);
						exactCounts[key] = count + 1;
					}
			}
			if (mappingOverflow)
				AddFault(State, "catalogue", "architecture mapping bound exceeded " + MaxMappings);

			HashSet<string> usedMaps = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> usedPalettes = new HashSet<string>(StringComparer.Ordinal);
			for (int p = 0; p < convertedPlans.Count; p++)
			{
				ArchitecturePlanDraft plan = State.Plans[convertedPlans[p]];
				string failure;
				if (!KingdomArchitectureRules.TryValidatePlan(plan, out failure))
				{
					AddFault(State, "plan " + plan.Key, failure);
					continue;
				}
				for (int b = 0; b < plan.Bindings.Count; b++)
				{
					ArchitectureBindingDraft binding = plan.Bindings[b];
					for (int t = 0; t < binding.Tiers.Count; t++)
					{
						ArchitectureTierDraft tier = binding.Tiers[t];
						if (mappingOverflow) continue;
						string exactKey = ExactRecordKey(tier.BuildKey,
							Fold(binding.TypeKey), binding.Size);
						if (exactCounts[exactKey] != 1)
						{
							AddFault(State, "building " + tier.BuildKey + " typed lot "
								+ Fold(binding.TypeKey) + "/" + binding.Size,
								"BuildKey and typed actual lot are declared by more than one architecture tier");
							continue;
						}
						ResolvedRecord record;
						if (TryRecord(State, plan.Key, binding, tier, usedMaps,
							usedPalettes, out record)) IndexRecord(State, record);
					}
				}
			}

			for (int i = 0; i < mapKeys.Count; i++)
				if (State.Maps.ContainsKey(mapKeys[i]) && !usedMaps.Contains(mapKeys[i]))
					AddFault(State, "map " + mapKeys[i], "map is not resolved by any valid tier variant");
			for (int i = 0; i < paletteKeys.Count; i++)
				if (State.Palettes.ContainsKey(paletteKeys[i]) && !usedPalettes.Contains(paletteKeys[i]))
					AddFault(State, "palette " + paletteKeys[i], "palette is not resolved by any valid tier variant");

			List<string> buildingKeys = OrderedKeys(State.Buildings);
			for (int i = 0; i < buildingKeys.Count; i++)
			{
				FrozenBuilding building = State.Buildings[buildingKeys[i]];
				if (building.HasPlot && !State.RecordsByBuild.ContainsKey(building.Key))
					AddFault(State, "building " + building.Key,
						"plot design has no valid authored architecture mapping");
				if (!building.HasPlot || KingdomPlotRules.HeartRungOf(building.Key) > 0) continue;
				for (int value = (int)building.LotSize;
					value <= (int)ArchitectureLotSize.Huge; value++)
				{
					ArchitectureLotSize actualSize = (ArchitectureLotSize)value;
					if (!State.Records.ContainsKey(ExactRecordKey(
						building.Key, Fold(building.Category), actualSize)))
						AddFault(State, "building " + building.Key + " typed lot "
							+ Fold(building.Category) + "/" + actualSize,
							"commissionable actual lot has no exact valid authored architecture mapping");
				}
			}
		}

		private static bool TryPalette(LoadState State, RawPalette Raw,
			out ArchitecturePaletteDraft Draft)
		{
			Draft = null;
			if (Raw.Overflow || Raw.Slots.Count == 0
				|| Raw.Slots.Count > KingdomArchitectureRules.MaxPaletteSlots)
				return Fault(State, "palette " + Raw.Key, "palette is empty or over the slot bound");
			ArchitecturePaletteDraft draft = new ArchitecturePaletteDraft { Key = Raw.Key };
			List<string> keys = OrderedKeys(Raw.Slots);
			for (int i = 0; i < keys.Count; i++)
			{
				RawRecord raw = Raw.Slots[keys[i]];
				if (raw.BadAttributes.Count > 0)
					return Fault(State, "palette " + Raw.Key + " slot " + raw.Key,
						"an explicitly malformed attribute survived the complete merge");
				string blueprint;
				if (!Required(State, raw, "Blueprint", out blueprint)
					|| !ValidBlueprint(blueprint))
					return Fault(State, "palette " + Raw.Key + " slot " + raw.Key,
						"Blueprint is absent or malformed");
				string role = Optional(raw, "Role");
				string material = Optional(raw, "Material");
				string minTech = Optional(raw, "MinTech");
				string knowledge = Optional(raw, "Knowledge");
				string power = Optional(raw, "Power");
				KingdomMaterial parsedMaterial;
				int parsedTech;
				if (!ValidOptionalKey(role) || !ValidOptionalKey(knowledge)
					|| !ValidOptionalKey(power)
					|| !KingdomMaterialRules.TryParseMaterial(material, out parsedMaterial)
					|| !KingdomArchitectureRules.TryParseTech(minTech, out parsedTech))
					return Fault(State, "palette " + Raw.Key + " slot " + raw.Key,
						"role, material, craft rung, knowledge, or power is absent or malformed");
				bool natural;
				if (!OptionalBoolean(State, raw, "Natural", false, out natural)) return false;
				if (!BlueprintExists(blueprint))
					return Fault(State, "palette " + Raw.Key + " slot " + raw.Key,
						"unknown Qud blueprint " + blueprint);
				draft.Slots.Add(new ArchitecturePaletteSlot
				{
					Key = raw.Key, Blueprint = blueprint, Role = role,
					Material = KingdomMaterialRules.MaterialKey(parsedMaterial),
					MinTech = KingdomZoningRules.TechLevelNames[parsedTech],
					Knowledge = knowledge, Power = power, Natural = natural
				});
			}
			Draft = draft;
			return true;
		}

		private static bool TryMap(LoadState State, RawMap Raw, out ArchitectureMapDraft Draft)
		{
			Draft = null;
			if (Raw.BadAttributes.Count > 0)
				return Fault(State, "map " + Raw.Key,
					"an explicitly malformed attribute survived the complete merge");
			int width;
			int height;
			string coverText;
			ArchitectureCover defaultCover;
			if (!RequiredInt(State, Raw, "Width", 1, 255, out width)
				|| !RequiredInt(State, Raw, "Height", 1, 255, out height)
				|| (long)width * height > KingdomArchitectureRules.MaxMapArea
				|| !Required(State, Raw, "DefaultCover", out coverText)
				|| !TryCover(coverText, out defaultCover))
				return Fault(State, "map " + Raw.Key, "dimensions or DefaultCover are malformed");
			if (Raw.Overflow || Raw.RowsOverflow || !Raw.RowsDeclared || Raw.Rows == null
				|| Raw.Rows.Count != height)
				return Fault(State, "map " + Raw.Key, "atomic row block is absent, oversized, or the wrong height");
			for (int i = 0; i < Raw.Rows.Count; i++)
				if (Raw.Rows[i] == null || Raw.Rows[i].Length != width)
					return Fault(State, "map " + Raw.Key, "row " + i + " has the wrong width");
			if (Raw.Glyphs.Count > KingdomArchitectureRules.MaxGlyphs)
				return Fault(State, "map " + Raw.Key, "glyph bound exceeded");
			ArchitectureMapDraft draft = new ArchitectureMapDraft
			{
				Key = Raw.Key, Width = width, Height = height, DefaultCover = defaultCover,
				Rows = new List<string>(Raw.Rows)
			};
			List<string> glyphKeys = OrderedKeys(Raw.Glyphs);
			for (int i = 0; i < glyphKeys.Count; i++)
			{
				RawRecord raw = Raw.Glyphs[glyphKeys[i]];
				if (raw.BadAttributes.Count > 0)
					return Fault(State, "map " + Raw.Key + " glyph " + raw.Key,
						"an explicitly malformed attribute survived the complete merge");
				if (raw.Key == null || raw.Key.Length != 1)
					return Fault(State, "map " + Raw.Key + " glyph", "Char must contain exactly one character");
				ArchitectureGlyphDraft glyph = new ArchitectureGlyphDraft
				{
					Character = raw.Key[0], Ground = Optional(raw, "Ground"),
					Structure = Optional(raw, "Structure"), Object = Optional(raw, "Object")
				};
				if (!OptionalClaim(State, raw, out glyph.Claim)
					|| !OptionalPassability(State, raw, out glyph.Passability)
					|| !OptionalBoolean(State, raw, "Stateful", false, out glyph.StatefulObject))
					return false;
				string cover = Optional(raw, "Cover");
				if (cover != null)
				{
					glyph.HasCover = true;
					if (!TryCover(cover, out glyph.Cover))
						return Fault(State, "map " + Raw.Key + " glyph " + raw.Key,
							"Cover is malformed");
				}
				string anchors = Optional(raw, "Anchors");
				if (anchors != null && !TryList(anchors, KingdomArchitectureRules.MaxAnchors,
					out glyph.Anchors))
					return Fault(State, "map " + Raw.Key + " glyph " + raw.Key,
						"Anchors are malformed or over the bound");
				if (!DirectBlueprintsExist(glyph))
					return Fault(State, "map " + Raw.Key + " glyph " + raw.Key,
						"a direct placement names an unknown Qud blueprint");
				draft.Glyphs.Add(glyph);
			}
			Draft = draft;
			return true;
		}

		private static bool TryPlan(LoadState State, RawPlan Raw, out ArchitecturePlanDraft Draft)
		{
			Draft = null;
			if (Raw.BadAttributes.Count > 0 || Raw.Overflow || Raw.Bindings.Count == 0
				|| Raw.Bindings.Count > KingdomArchitectureRules.MaxBindingsPerPlan)
				return Fault(State, "plan " + Raw.Key, "plan is empty or over the binding bound");
			ArchitecturePlanDraft draft = new ArchitecturePlanDraft { Key = Raw.Key };
			List<string> bindingKeys = OrderedKeys(Raw.Bindings);
			for (int i = 0; i < bindingKeys.Count; i++)
			{
				ArchitectureBindingDraft binding;
				if (!TryBinding(State, Raw.Bindings[bindingKeys[i]], out binding)) return false;
				draft.Bindings.Add(binding);
			}
			Draft = draft;
			return true;
		}

		private static bool TryBinding(LoadState State, RawBinding Raw,
			out ArchitectureBindingDraft Draft)
		{
			Draft = null;
			string type;
			string sizeText;
			string frontageText;
			ArchitectureLotSize size;
			ArchitectureFrontage frontage;
			if (Raw.BadAttributes.Count > 0 || Raw.Overflow || !Required(State, Raw, "Type", out type)
				|| (type = Fold(type)) == null || !Required(State, Raw, "Size", out sizeText)
				|| !TryLotSize(sizeText, out size)
				|| !Required(State, Raw, "Frontage", out frontageText)
				|| !TryFrontage(frontageText, out frontage)
				|| Raw.Tiers.Count == 0
				|| Raw.Tiers.Count > KingdomArchitectureRules.MaxTiersPerBinding)
				return Fault(State, "binding " + Raw.Key,
					"typed lot, frontage, or tier collection is malformed");
			ArchitectureBindingDraft draft = new ArchitectureBindingDraft
				{ Key = Raw.Key, TypeKey = type, Size = size, Frontage = frontage };
			List<string> tierKeys = OrderedKeys(Raw.Tiers);
			for (int i = 0; i < tierKeys.Count; i++)
			{
				ArchitectureTierDraft tier;
				if (!TryTier(State, Raw.Tiers[tierKeys[i]], out tier)) return false;
				draft.Tiers.Add(tier);
			}
			draft.Tiers.Sort(delegate(ArchitectureTierDraft a, ArchitectureTierDraft b)
			{
				int order = a.Level.CompareTo(b.Level);
				return order != 0 ? order : string.CompareOrdinal(a.Key, b.Key);
			});
			Draft = draft;
			return true;
		}

		private static bool TryTier(LoadState State, RawTier Raw, out ArchitectureTierDraft Draft)
		{
			Draft = null;
			string buildKey;
			string map;
			string palette;
			int level;
			if (Raw.BadAttributes.Count > 0 || Raw.Overflow || !Required(State, Raw, "BuildKey", out buildKey)
				|| !ValidKey(buildKey) || !RequiredInt(State, Raw, "Level", 0, int.MaxValue, out level)
				|| !Required(State, Raw, "Map", out map) || !ValidKey(map)
				|| !Required(State, Raw, "Palette", out palette) || !ValidKey(palette)
				|| Raw.Requirements.Count > KingdomArchitectureRules.MaxRequirementsPerTier
				|| Raw.Variants.Count == 0
				|| Raw.Variants.Count > KingdomArchitectureRules.MaxVariantsPerTier)
				return Fault(State, "tier " + Raw.Key, "identity, references, or child bounds are malformed");
			ArchitectureTierDraft draft = new ArchitectureTierDraft
				{ Key = Raw.Key, BuildKey = buildKey, Level = level, MapKey = map, PaletteKey = palette };
			List<string> requirements = OrderedKeys(Raw.Requirements);
			for (int i = 0; i < requirements.Count; i++)
			{
				RawRecord raw = Raw.Requirements[requirements[i]];
				if (raw.BadAttributes.Count > 0)
					return Fault(State, "tier " + Raw.Key + " requirement " + raw.Key,
						"an explicitly malformed attribute survived the complete merge");
				string role;
				int minimum;
				int maximum = 0;
				if (!Required(State, raw, "Role", out role) || !ValidKey(role)
					|| !RequiredInt(State, raw, "Min", 0, int.MaxValue, out minimum)
					|| !OptionalInt(State, raw, "Max", 0, int.MaxValue, 0, out maximum))
					return Fault(State, "tier " + Raw.Key + " requirement " + raw.Key,
						"role or count is malformed");
				draft.Requirements.Add(new ArchitectureAnchorRequirement
					{ Role = role, Minimum = minimum, Maximum = maximum });
			}
			List<string> variants = OrderedKeys(Raw.Variants);
			for (int i = 0; i < variants.Count; i++)
			{
				ArchitectureVariantDraft variant;
				if (!TryVariant(State, Raw.Variants[variants[i]], out variant)) return false;
				draft.Variants.Add(variant);
			}
			Draft = draft;
			return true;
		}

		private static bool TryVariant(LoadState State, RawRecord Raw,
			out ArchitectureVariantDraft Draft)
		{
			Draft = null;
			if (Raw.BadAttributes.Count > 0)
				return Fault(State, "variant " + Raw.Key,
					"an explicitly malformed attribute survived the complete merge");
			int priority;
			if (!OptionalInt(State, Raw, "Priority", int.MinValue, int.MaxValue, 0, out priority))
				return false;
			ArchitectureVariantDraft draft = new ArchitectureVariantDraft
			{
				Key = Raw.Key, Priority = priority, MapKey = Optional(Raw, "Map"),
				PaletteKey = Optional(Raw, "Palette")
			};
			if (!ValidOptionalKey(draft.MapKey) || !ValidOptionalKey(draft.PaletteKey))
				return Fault(State, "variant " + Raw.Key, "map or palette override is malformed");
			bool selector = Has(Raw, "Styles") || Has(Raw, "Creeds")
				|| Has(Raw, "Cultures") || Has(Raw, "Species")
				|| Has(Raw, "Genotypes") || Has(Raw, "Bodies") || Has(Raw, "Terrains")
				|| Has(Raw, "Strata") || Has(Raw, "MinStage") || Has(Raw, "MaxStage")
				|| Has(Raw, "MinTech") || Has(Raw, "MaxTech");
			if (selector)
			{
				ArchitectureSelector parsed = new ArchitectureSelector
				{
					Styles = Optional(Raw, "Styles"), Creeds = Optional(Raw, "Creeds"),
					Cultures = Optional(Raw, "Cultures"), Species = Optional(Raw, "Species"),
					Genotypes = Optional(Raw, "Genotypes"), Bodies = Optional(Raw, "Bodies"),
					Terrains = Optional(Raw, "Terrains"), Strata = Optional(Raw, "Strata")
				};
				if (!OptionalStage(State, Raw, "MinStage", -1, out parsed.MinimumStage)
					|| !OptionalStage(State, Raw, "MaxStage", -1, out parsed.MaximumStage)
					|| !OptionalTech(State, Raw, "MinTech", -1, out parsed.MinimumTech)
					|| !OptionalTech(State, Raw, "MaxTech", -1, out parsed.MaximumTech)) return false;
				draft.Selector = parsed;
			}
			Draft = draft;
			return true;
		}

		private static bool TryRecord(LoadState State, string PlanKey,
			ArchitectureBindingDraft Binding, ArchitectureTierDraft Tier,
			HashSet<string> UsedMaps, HashSet<string> UsedPalettes,
			out ResolvedRecord Record)
		{
			Record = null;
			FrozenBuilding building;
			if (!State.Buildings.TryGetValue(Tier.BuildKey, out building))
				return Fault(State, "tier " + Tier.Key,
					"BuildKey " + Tier.BuildKey + " does not exist in the frozen KingdomBuildings view");
			if (!building.HasPlot)
				return Fault(State, "building " + Tier.BuildKey,
					"architecture tier points at a design with no plot");
			if (building.LotSize > Binding.Size)
				return Fault(State, "building " + Tier.BuildKey,
					"authored binding size is smaller than its merged Plot minimum");
			if (building.Category == null
				|| !string.Equals(building.Category, Fold(Binding.TypeKey), StringComparison.Ordinal))
				return Fault(State, "building " + Tier.BuildKey,
					"architecture Type does not match its merged Category");
			if (!ValidBlueprint(building.Blueprint) || !BlueprintExists(building.Blueprint))
				return Fault(State, "building " + Tier.BuildKey,
					"behavior Blueprint is absent from Qud: " + (building.Blueprint ?? "<null>"));

			for (int v = 0; v < Tier.Variants.Count; v++)
			{
				ArchitectureVariantDraft variant = Tier.Variants[v];
				string mapKey = string.IsNullOrEmpty(variant.MapKey) ? Tier.MapKey : variant.MapKey;
				string paletteKey = string.IsNullOrEmpty(variant.PaletteKey)
					? Tier.PaletteKey : variant.PaletteKey;
				UsedMaps.Add(mapKey);
				UsedPalettes.Add(paletteKey);
				ArchitectureMapDraft map;
				ArchitecturePaletteDraft palette;
				if (!State.Maps.TryGetValue(mapKey, out map))
					return Fault(State, "building " + Tier.BuildKey + " variant " + variant.Key,
						"unresolved map " + mapKey);
				if (!State.Palettes.TryGetValue(paletteKey, out palette))
					return Fault(State, "building " + Tier.BuildKey + " variant " + variant.Key,
						"unresolved palette " + paletteKey);
				for (int facing = (int)ArchitectureFacing.North;
					facing <= (int)ArchitectureFacing.West; facing++)
				{
					ArchitectureCompileRequest request = new ArchitectureCompileRequest
					{
						PlanKey = PlanKey, Binding = Binding, Tier = Tier, Variant = variant,
						Map = map, Palette = palette, BuildingBlueprint = building.Blueprint,
						Facing = (ArchitectureFacing)facing
					};
					ArchitectureLayoutSnapshot snapshot;
					string failure;
					if (!KingdomArchitectureRules.TryCompile(request, out snapshot, out failure))
						return Fault(State, "building " + Tier.BuildKey + " variant " + variant.Key,
							((ArchitectureFacing)facing) + " compile failed: " + failure);
				}
			}
			Record = new ResolvedRecord
			{
				Binding = Binding,
				Tier = Tier,
				View = new KingdomArchitectureMapping(building.Blueprint, building.Category,
					PlanKey, Binding, Tier)
			};
			return true;
		}

		private static void IndexRecord(LoadState State, ResolvedRecord Record)
		{
			KingdomArchitectureMapping view = Record.View;
			State.Records.Add(ExactRecordKey(view.BuildKey, Fold(view.TypeKey), view.LotSize), Record);
			List<ResolvedRecord> byBuild;
			if (!State.RecordsByBuild.TryGetValue(view.BuildKey, out byBuild))
			{
				byBuild = new List<ResolvedRecord>();
				State.RecordsByBuild.Add(view.BuildKey, byBuild);
			}
			byBuild.Add(Record);

			string bindingKey = BindingRecordKey(view.PlanKey, view.BindingKey,
				Fold(view.TypeKey), view.LotSize);
			Dictionary<string, ResolvedRecord> byBinding;
			if (!State.RecordsByBinding.TryGetValue(bindingKey, out byBinding))
			{
				byBinding = new Dictionary<string, ResolvedRecord>(StringComparer.Ordinal);
				State.RecordsByBinding.Add(bindingKey, byBinding);
			}
			byBinding.Add(view.BuildKey, Record);
		}

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
			return TryResolveSuccessor(null, SuccessorBuildKey, PlanKey, BindingKey, LotType,
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
			Snapshot = null;
			Failure = null;
			LoadState frozen = state;
			if (!frozen.Loaded) return ResolveFault("architecture catalogue has not loaded", out Failure);
			string type = Fold(LotType);
			Dictionary<string, ResolvedRecord> binding = null;
			ResolvedRecord record = null;
			int beforeRung = KingdomPlotRules.HeartRungOf(PredecessorBuildKey);
			int afterRung = KingdomPlotRules.HeartRungOf(SuccessorBuildKey);
			bool heartRequest = beforeRung > 0 || afterRung > 0;
			bool ordinary = !heartRequest && ValidKey(SuccessorBuildKey) && ValidKey(PlanKey)
				&& ValidKey(BindingKey) && ValidKey(type) && KnownLotSize(ActualLotSize)
				&& frozen.RecordsByBinding.TryGetValue(
					BindingRecordKey(PlanKey, BindingKey, type, ActualLotSize), out binding)
				&& binding.TryGetValue(SuccessorBuildKey, out record);
			if (!ordinary)
			{
				ResolvedRecord predecessor;
				ArchitectureLotSize successorSize = (ArchitectureLotSize)afterRung;
				if (beforeRung < 1 || afterRung != beforeRung + 1
					|| PlanKey != "civic-heart" || type != "civic"
					|| ActualLotSize != (ArchitectureLotSize)beforeRung
					|| !ValidKey(BindingKey)
					|| !frozen.Records.TryGetValue(ExactRecordKey(PredecessorBuildKey,
						type, ActualLotSize), out predecessor)
					|| predecessor.View.PlanKey != PlanKey
					|| predecessor.View.BindingKey != BindingKey
					|| !frozen.Records.TryGetValue(ExactRecordKey(SuccessorBuildKey,
						type, successorSize), out record)
					|| record.View.PlanKey != "civic-heart"
					|| Fold(record.View.TypeKey) != "civic"
					|| record.View.LotSize != successorSize)
					return ResolveFault("no valid authored successor "
						+ (SuccessorBuildKey ?? "<null>")
						+ " exists in the frozen typed binding or adjacent civic-heart rung",
						out Failure);
			}
			ArchitectureVariantDraft variant;
			if (!KingdomArchitectureRules.TrySelectVariant(record.Tier.Variants, Context,
				out variant, out Failure)) return false;
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
				BuildingBlueprint = Record.View.BuildingBlueprint, Facing = Facing
			};
			ArchitectureLayoutSnapshot compiled;
			if (!KingdomArchitectureRules.TryCompile(request, out compiled, out Failure)) return false;
			string encoded;
			if (!KingdomArchitectureRules.TryEncodeSnapshot(compiled, out encoded, out Failure)) return false;
			return KingdomArchitectureRules.TryDecodeSnapshot(encoded, out Snapshot, out Failure);
		}

		// --- Attribute parsing and validation ---------------------------------------------

		private static bool Required(LoadState State, RawRecord Raw, string Name, out string Value)
		{
			Value = null;
			if (Raw.BadAttributes.Contains(Name) || !Raw.Values.TryGetValue(Name, out Value)
				|| string.IsNullOrEmpty(Value))
				return Fault(State, Raw.Key + " " + Name, "required attribute is absent or malformed");
			return true;
		}

		private static string Optional(RawRecord Raw, string Name)
		{
			string result;
			return Raw.BadAttributes.Contains(Name) || !Raw.Values.TryGetValue(Name, out result)
				? null : result;
		}

		private static bool Has(RawRecord Raw, string Name)
		{
			return Raw.BadAttributes.Contains(Name) || Raw.Values.ContainsKey(Name);
		}

		private static bool RequiredInt(LoadState State, RawRecord Raw, string Name,
			int Minimum, int Maximum, out int Value)
		{
			Value = 0;
			string text;
			if (!Required(State, Raw, Name, out text)
				|| !int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out Value)
				|| Value < Minimum || Value > Maximum)
				return Fault(State, Raw.Key + " " + Name, "integer is outside its bound");
			return true;
		}

		private static bool OptionalInt(LoadState State, RawRecord Raw, string Name,
			int Minimum, int Maximum, int Default, out int Value)
		{
			Value = Default;
			if (Raw.BadAttributes.Contains(Name))
				return Fault(State, Raw.Key + " " + Name, "integer attribute is malformed");
			string text;
			if (!Raw.Values.TryGetValue(Name, out text)) return true;
			if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out Value)
				|| Value < Minimum || Value > Maximum)
				return Fault(State, Raw.Key + " " + Name, "integer is outside its bound");
			return true;
		}

		private static bool OptionalBoolean(LoadState State, RawRecord Raw, string Name,
			bool Default, out bool Value)
		{
			Value = Default;
			if (Raw.BadAttributes.Contains(Name))
				return Fault(State, Raw.Key + " " + Name, "boolean attribute is malformed");
			string text;
			if (!Raw.Values.TryGetValue(Name, out text)) return true;
			if (!TryBoolean(text, out Value))
				return Fault(State, Raw.Key + " " + Name, "expected yes/no, true/false, or 1/0");
			return true;
		}

		private static bool OptionalClaim(LoadState State, RawRecord Raw, out bool Value)
		{
			Value = false;
			if (Raw.BadAttributes.Contains("Claim"))
				return Fault(State, Raw.Key + " Claim", "claim attribute is malformed");
			string text;
			if (!Raw.Values.TryGetValue("Claim", out text)) return true;
			string folded = Fold(text);
			if (folded == "building" || folded == "yard" || folded == "claimed")
			{
				Value = true;
				return true;
			}
			if (folded == "none" || folded == "unclaimed")
			{
				Value = false;
				return true;
			}
			if (TryBoolean(text, out Value)) return true;
			return Fault(State, Raw.Key + " Claim", "expected building, yard, claimed, or a boolean");
		}

		private static bool OptionalPassability(LoadState State, RawRecord Raw,
			out ArchitecturePassability Value)
		{
			Value = ArchitecturePassability.Walkable;
			if (Raw.BadAttributes.Contains("Pass"))
				return Fault(State, Raw.Key + " Pass", "passability attribute is malformed");
			string text;
			if (!Raw.Values.TryGetValue("Pass", out text)) return true;
			string folded = Fold(text);
			if (folded == "walk" || folded == "walkable") Value = ArchitecturePassability.Walkable;
			else if (folded == "block" || folded == "blocked") Value = ArchitecturePassability.Blocked;
			else if (folded == "adjacent") Value = ArchitecturePassability.Adjacent;
			else return Fault(State, Raw.Key + " Pass", "unknown passability " + text);
			return true;
		}

		private static bool OptionalStage(LoadState State, RawRecord Raw, string Name,
			int Default, out int Value)
		{
			Value = Default;
			if (Raw.BadAttributes.Contains(Name))
				return Fault(State, Raw.Key + " " + Name, "stage selector is malformed");
			string text;
			if (!Raw.Values.TryGetValue(Name, out text)) return true;
			int numeric;
			GrowthStage stage;
			if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out numeric)
				&& numeric >= (int)GrowthStage.Camp && numeric <= (int)GrowthStage.City)
				Value = numeric;
			else if (Enum.TryParse(text, true, out stage) && KingdomRules.IsKnownStage(stage))
				Value = (int)stage;
			else return Fault(State, Raw.Key + " " + Name, "unknown growth stage " + text);
			return true;
		}

		private static bool OptionalTech(LoadState State, RawRecord Raw, string Name,
			int Default, out int Value)
		{
			Value = Default;
			if (Raw.BadAttributes.Contains(Name))
				return Fault(State, Raw.Key + " " + Name, "technology selector is malformed");
			string text;
			if (!Raw.Values.TryGetValue(Name, out text)) return true;
			int numeric;
			TechLevel tech;
			if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out numeric)
				&& numeric >= (int)TechLevel.Hands && numeric <= (int)TechLevel.Arclight)
				Value = numeric;
			else if (Enum.TryParse(text, true, out tech) && KingdomZoningRules.IsKnownTechLevel(tech))
				Value = (int)tech;
			else return Fault(State, Raw.Key + " " + Name, "unknown craft rung " + text);
			return true;
		}

		private static bool TryBoolean(string Text, out bool Value)
		{
			Value = false;
			if (string.IsNullOrWhiteSpace(Text)) return false;
			switch (Text.Trim().ToLowerInvariant())
			{
			case "yes": case "true": case "1": Value = true; return true;
			case "no": case "false": case "0": Value = false; return true;
			default: return false;
			}
		}

		private static bool TryCover(string Text, out ArchitectureCover Value)
		{
			Value = ArchitectureCover.Open;
			string folded = Fold(Text);
			if (folded == "open") Value = ArchitectureCover.Open;
			else if (folded == "soft") Value = ArchitectureCover.Soft;
			else if (folded == "walled") Value = ArchitectureCover.Walled;
			else if (folded == "natural" || folded == "carved") Value = ArchitectureCover.Natural;
			else return false;
			return true;
		}

		private static bool TryFrontage(string Text, out ArchitectureFrontage Value)
		{
			Value = ArchitectureFrontage.Heart;
			string folded = Fold(Text);
			if (folded == "heart") Value = ArchitectureFrontage.Heart;
			else if (folded == "road") Value = ArchitectureFrontage.Road;
			else return false;
			return true;
		}

		private static bool TryLotSize(string Text, out ArchitectureLotSize Value)
		{
			Value = 0;
			string folded = Fold(Text);
			if (folded == "s" || folded == "small") Value = ArchitectureLotSize.Small;
			else if (folded == "m" || folded == "medium") Value = ArchitectureLotSize.Medium;
			else if (folded == "l" || folded == "large") Value = ArchitectureLotSize.Large;
			else if (folded == "xl" || folded == "huge") Value = ArchitectureLotSize.Huge;
			else return false;
			return true;
		}

		private static bool TryLotSize(KingdomPlotRules.PlotSize Size,
			out ArchitectureLotSize Value)
		{
			Value = 0;
			switch (Size)
			{
			case KingdomPlotRules.PlotSize.Small: Value = ArchitectureLotSize.Small; return true;
			case KingdomPlotRules.PlotSize.Medium: Value = ArchitectureLotSize.Medium; return true;
			case KingdomPlotRules.PlotSize.Large: Value = ArchitectureLotSize.Large; return true;
			case KingdomPlotRules.PlotSize.Huge: Value = ArchitectureLotSize.Huge; return true;
			default: return false;
			}
		}

		private static bool KnownLotSize(ArchitectureLotSize Size)
		{
			return Size == ArchitectureLotSize.Small || Size == ArchitectureLotSize.Medium
				|| Size == ArchitectureLotSize.Large || Size == ArchitectureLotSize.Huge;
		}

		private static string ExactRecordKey(string BuildKey, string FoldedType,
			ArchitectureLotSize ActualLotSize)
		{
			// Newlines cannot occur in validated keys, making this bounded identity injective.
			return BuildKey + "\n" + FoldedType + "\n"
				+ ((int)ActualLotSize).ToString(CultureInfo.InvariantCulture);
		}

		private static string BindingRecordKey(string PlanKey, string BindingKey,
			string FoldedType, ArchitectureLotSize ActualLotSize)
		{
			return PlanKey + "\n" + BindingKey + "\n" + FoldedType + "\n"
				+ ((int)ActualLotSize).ToString(CultureInfo.InvariantCulture);
		}

		private static bool TryList(string Text, int Maximum, out List<string> Values)
		{
			Values = new List<string>();
			if (string.IsNullOrWhiteSpace(Text)) return false;
			string[] fields = Text.Split(',');
			if (fields.Length > Maximum) return false;
			HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < fields.Length; i++)
			{
				string field = fields[i].Trim();
				if (!ValidKey(field) || !seen.Add(field)) return false;
				Values.Add(field);
			}
			return true;
		}

		private static bool DirectBlueprintsExist(ArchitectureGlyphDraft Glyph)
		{
			return DirectBlueprintExists(Glyph.Ground) && DirectBlueprintExists(Glyph.Structure)
				&& DirectBlueprintExists(Glyph.Object);
		}

		private static bool DirectBlueprintExists(string Token)
		{
			return string.IsNullOrEmpty(Token) || Token[0] == '$' || BlueprintExists(Token);
		}

		private static bool BlueprintExists(string Blueprint)
		{
			try { return GameObjectFactory.Factory.HasBlueprint(Blueprint); }
			catch { return false; }
		}

		private static bool ValidKey(string Value)
		{
			return !string.IsNullOrEmpty(Value)
				&& Value.Length <= KingdomArchitectureRules.MaxKeyChars
				&& Value == Value.Trim() && !HasControl(Value);
		}

		private static bool ValidOptionalKey(string Value)
		{
			return string.IsNullOrEmpty(Value) || ValidKey(Value);
		}

		private static bool ValidBlueprint(string Value)
		{
			return !string.IsNullOrEmpty(Value)
				&& Value.Length <= KingdomArchitectureRules.MaxBlueprintChars
				&& Value == Value.Trim() && !HasControl(Value);
		}

		private static bool HasControl(string Value)
		{
			if (Value == null) return false;
			for (int i = 0; i < Value.Length; i++) if (char.IsControl(Value[i])) return true;
			return false;
		}

		private static string Fold(string Value)
		{
			return string.IsNullOrWhiteSpace(Value) ? null : Value.Trim().ToLowerInvariant();
		}

		private static List<string> OrderedKeys<T>(Dictionary<string, T> Values)
		{
			List<string> result = new List<string>(Values.Keys);
			result.Sort(StringComparer.Ordinal);
			return result;
		}

		private static List<ResolvedRecord> OrderedRecords(
			Dictionary<string, ResolvedRecord> Values)
		{
			List<ResolvedRecord> result = new List<ResolvedRecord>(Values.Values);
			result.Sort(delegate(ResolvedRecord left, ResolvedRecord right)
			{
				int order = string.CompareOrdinal(left.View.BuildKey, right.View.BuildKey);
				if (order != 0) return order;
				order = string.CompareOrdinal(Fold(left.View.TypeKey), Fold(right.View.TypeKey));
				if (order != 0) return order;
				order = left.View.LotSize.CompareTo(right.View.LotSize);
				if (order != 0) return order;
				order = string.CompareOrdinal(left.View.PlanKey, right.View.PlanKey);
				if (order != 0) return order;
				return string.CompareOrdinal(left.View.BindingKey, right.View.BindingKey);
			});
			return result;
		}

		private static bool ResolveFault(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}

		private static bool Fault(LoadState State, string Name, string Message)
		{
			AddFault(State, Name, Message);
			return false;
		}

		private static void AddFault(LoadState State, string Name, string Message)
		{
			if (State.FaultOverflow) return;
			Name = string.IsNullOrWhiteSpace(Name) ? "catalogue" : Name.Trim();
			Message = string.IsNullOrWhiteSpace(Message) ? "unknown fault" : Message.Trim();
			string identity = Name + "\n" + Message;
			if (!State.FaultKeys.Add(identity)) return;
			if (State.Faults.Count < MaxFaults)
			{
				State.Faults.Add(new KingdomArchitectureFault(Name, Message));
				return;
			}
			if (!State.FaultOverflow)
			{
				State.FaultOverflow = true;
				State.Faults[MaxFaults - 1] = new KingdomArchitectureFault("catalogue",
					"fault report exceeded " + MaxFaults + " entries; later faults were suppressed");
			}
		}
	}
}
