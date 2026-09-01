using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitecture
	{
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
			string footprintText;
			int footprintX = 0;
			int footprintY = 0;
			int footprintWidth = 0;
			int footprintHeight = 0;
			if (!RequiredInt(State, Raw, "Width", 1, 255, out width)
				|| !RequiredInt(State, Raw, "Height", 1, 255, out height)
				|| (long)width * height > KingdomArchitectureRules.MaxMapArea
				|| !Required(State, Raw, "DefaultCover", out coverText)
				|| !TryCover(coverText, out defaultCover))
				return Fault(State, "map " + Raw.Key, "dimensions or DefaultCover are malformed");
			footprintText = Optional(Raw, "Footprint");
			if (footprintText != null && !TryFootprint(footprintText, width, height,
				out footprintX, out footprintY, out footprintWidth, out footprintHeight))
				return Fault(State, "map " + Raw.Key,
					"Footprint must be canonical X,Y,WxH wholly inside the map");
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
				HasFootprint = footprintText != null, FootprintX = footprintX,
				FootprintY = footprintY, FootprintWidth = footprintWidth,
				FootprintHeight = footprintHeight,
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
				if (!RequiredClaim(State, raw, out glyph.Claim)
					|| !OptionalPassability(State, raw, out glyph.Passability)
					|| !TryGlyphOrientations(State, raw, glyph)
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
			string transitionText;
			int level;
			if (Raw.BadAttributes.Count > 0 || Raw.Overflow || !Required(State, Raw, "BuildKey", out buildKey)
				|| !ValidKey(buildKey) || !RequiredInt(State, Raw, "Level", 0, int.MaxValue, out level)
				|| !Required(State, Raw, "Map", out map) || !ValidKey(map)
				|| !Required(State, Raw, "Palette", out palette) || !ValidKey(palette)
				|| Raw.Requirements.Count > KingdomArchitectureRules.MaxRequirementsPerTier
				|| Raw.Variants.Count == 0
				|| Raw.Variants.Count > KingdomArchitectureRules.MaxVariantsPerTier)
				return Fault(State, "tier " + Raw.Key, "identity, references, or child bounds are malformed");
			transitionText = Optional(Raw, "Transition");
			ArchitectureTransitionMode transitionMode = ArchitectureTransitionMode.None;
			if ((transitionText == null && level != 0)
				|| (transitionText != null && !KingdomArchitectureTransitionRules.TryParseMode(
					transitionText, out transitionMode))
				|| !KingdomArchitectureTransitionRules.ValidTierMode(level, transitionMode))
				return Fault(State, "tier " + Raw.Key,
					"base tiers use none; later tiers require additive, additive-expand, "
					+ "renovate, renovate-expand, or replacement");
			ArchitectureTierDraft draft = new ArchitectureTierDraft
				{
					Key = Raw.Key, BuildKey = buildKey, Level = level,
					IncomingTransitionMode = transitionMode, MapKey = map, PaletteKey = palette
				};
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

	}
}
