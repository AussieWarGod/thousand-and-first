#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	internal sealed class ArchitectureCorpus
	{
		internal readonly Dictionary<string, ArchitecturePaletteDraft> Palettes =
			new Dictionary<string, ArchitecturePaletteDraft>(StringComparer.Ordinal);
		internal readonly Dictionary<string, ArchitectureMapDraft> Maps =
			new Dictionary<string, ArchitectureMapDraft>(StringComparer.Ordinal);
		internal readonly List<ArchitectureCorpusCase> Cases = new List<ArchitectureCorpusCase>();
	}

	internal sealed class ArchitectureCorpusCase
	{
		internal string PlanKey;
		internal ArchitectureBindingDraft Binding;
		internal ArchitectureTierDraft Tier;
		internal ArchitectureVariantDraft Variant;
	}

	internal static class KingdomArchitectureCorpusFixture
	{
		internal static ArchitectureCorpus Load()
		{
			ArchitectureCorpus result = new ArchitectureCorpus();
			string root = Path.Combine(TestMain.RepositoryRoot, "Architecture");
			XDocument[] documents = Directory.GetFiles(root, "KingdomArchitectures*.xml")
				.OrderBy(path => path, StringComparer.Ordinal).Select(XDocument.Load).ToArray();
			for (int i = 0; i < documents.Length; i++)
			{
				foreach (XElement raw in documents[i].Root.Elements("palette"))
				{
					ArchitecturePaletteDraft palette = Palette(raw);
					result.Palettes.Add(palette.Key, palette);
				}
				foreach (XElement raw in documents[i].Root.Elements("map"))
				{
					ArchitectureMapDraft map = Map(raw);
					result.Maps.Add(map.Key, map);
				}
			}
			for (int i = 0; i < documents.Length; i++)
				foreach (XElement plan in documents[i].Root.Elements("plan"))
					AddPlan(result, plan);
			return result;
		}

		internal static ArchitectureCompileRequest Request(ArchitectureCorpus corpus,
			ArchitectureCorpusCase item, ArchitectureFacing facing)
		{
			string mapKey = string.IsNullOrEmpty(item.Variant.MapKey)
				? item.Tier.MapKey : item.Variant.MapKey;
			string paletteKey = string.IsNullOrEmpty(item.Variant.PaletteKey)
				? item.Tier.PaletteKey : item.Variant.PaletteKey;
			return new ArchitectureCompileRequest
			{
				PlanKey = item.PlanKey,
				Binding = item.Binding,
				Tier = item.Tier,
				Variant = item.Variant,
				Map = corpus.Maps[mapKey],
				Palette = corpus.Palettes[paletteKey],
				BuildingBlueprint = "r_KingdomArchitectureCorpusRoot",
				Facing = facing
			};
		}

		private static ArchitecturePaletteDraft Palette(XElement raw)
		{
			ArchitecturePaletteDraft result = new ArchitecturePaletteDraft
				{ Key = Text(raw, "Key") };
			foreach (XElement slot in raw.Elements("slot"))
				result.Slots.Add(new ArchitecturePaletteSlot
				{
					Key = Text(slot, "Key"),
					Blueprint = Text(slot, "Blueprint"),
					Role = Optional(slot, "Role"),
					Material = Optional(slot, "Material"),
					MinTech = Optional(slot, "MinTech"),
					Knowledge = Optional(slot, "Knowledge"),
					Power = Optional(slot, "Power"),
					Natural = Text(slot, "Natural") == "yes"
				});
			return result;
		}

		private static ArchitectureMapDraft Map(XElement raw)
		{
			ArchitectureMapDraft result = new ArchitectureMapDraft
			{
				Key = Text(raw, "Key"),
				Width = Number(raw, "Width"),
				Height = Number(raw, "Height"),
				DefaultCover = Cover(Text(raw, "DefaultCover"))
			};
			foreach (XElement glyph in raw.Elements("glyph"))
			{
				ArchitectureGlyphDraft item = new ArchitectureGlyphDraft
				{
					Character = Text(glyph, "Char")[0],
					Ground = Optional(glyph, "Ground"),
					Structure = Optional(glyph, "Structure"),
					Object = Optional(glyph, "Object"),
					Claim = !string.IsNullOrEmpty(Optional(glyph, "Claim")),
					Passability = Passability(Text(glyph, "Pass")),
					Cover = Cover(Text(glyph, "Cover")),
					HasCover = true,
					StatefulObject = Optional(glyph, "Stateful") == "yes"
				};
				string anchors = Optional(glyph, "Anchors");
				if (anchors != null)
					item.Anchors.AddRange(anchors.Split(',').Select(value => value.Trim()));
				result.Glyphs.Add(item);
			}
			result.Rows.AddRange(raw.Elements("row").Select(row => Text(row, "Cells")));
			return result;
		}

		private static void AddPlan(ArchitectureCorpus corpus, XElement raw)
		{
			string planKey = Text(raw, "Key");
			foreach (XElement bindingXml in raw.Elements("binding"))
			{
				ArchitectureBindingDraft binding = new ArchitectureBindingDraft
				{
					Key = Text(bindingXml, "Key"),
					TypeKey = Text(bindingXml, "Type"),
					Size = Size(Text(bindingXml, "Size")),
					Frontage = Frontage(Text(bindingXml, "Facing"))
				};
				foreach (XElement tierXml in bindingXml.Elements("tier"))
				{
					ArchitectureTierDraft tier = new ArchitectureTierDraft
					{
						Key = Text(tierXml, "Key"),
						BuildKey = Text(tierXml, "BuildKey"),
						Level = Number(tierXml, "Level"),
						MapKey = Text(tierXml, "Map"),
						PaletteKey = Text(tierXml, "Palette")
					};
					foreach (XElement requirement in tierXml.Elements("require"))
						tier.Requirements.Add(new ArchitectureAnchorRequirement
							{
								Role = Text(requirement, "Role"),
								Minimum = Number(requirement, "Min"),
								Maximum = Number(requirement, "Max", 0)
							});
					foreach (XElement variantXml in tierXml.Elements("variant"))
					{
						ArchitectureVariantDraft variant = new ArchitectureVariantDraft
						{
							Key = Text(variantXml, "Key"),
							Priority = Number(variantXml, "Priority"),
							MapKey = Optional(variantXml, "Map"),
							PaletteKey = Optional(variantXml, "Palette")
						};
						tier.Variants.Add(variant);
						corpus.Cases.Add(new ArchitectureCorpusCase
							{
								PlanKey = planKey, Binding = binding,
								Tier = tier, Variant = variant
							});
					}
					binding.Tiers.Add(tier);
				}
			}
		}

		private static string Text(XElement element, string name)
		{
			return (string)element.Attribute(name) ?? "";
		}

		private static string Optional(XElement element, string name)
		{
			string value = (string)element.Attribute(name);
			return string.IsNullOrEmpty(value) ? null : value;
		}

		private static int Number(XElement element, string name, int fallback = -1)
		{
			return int.TryParse(Optional(element, name), out int value) ? value : fallback;
		}

		private static ArchitectureLotSize Size(string value)
		{
			return value == "S" ? ArchitectureLotSize.Small
				: value == "M" ? ArchitectureLotSize.Medium
				: value == "L" ? ArchitectureLotSize.Large : ArchitectureLotSize.Huge;
		}

		private static ArchitectureFrontage Frontage(string value)
		{
			return value == "road" ? ArchitectureFrontage.Road : ArchitectureFrontage.Heart;
		}

		private static ArchitecturePassability Passability(string value)
		{
			return value == "blocked" ? ArchitecturePassability.Blocked
				: value == "adjacent" ? ArchitecturePassability.Adjacent
				: ArchitecturePassability.Walkable;
		}

		private static ArchitectureCover Cover(string value)
		{
			return value == "soft" ? ArchitectureCover.Soft
				: value == "walled" ? ArchitectureCover.Walled
				: value == "natural" ? ArchitectureCover.Natural : ArchitectureCover.Open;
		}
	}
}
#endif
