using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitecture
	{
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

	}
}
