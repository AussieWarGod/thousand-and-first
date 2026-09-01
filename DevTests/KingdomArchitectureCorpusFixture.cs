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
		internal readonly List<ArchitecturePoseDraft> Poses = new List<ArchitecturePoseDraft>();
		internal readonly Dictionary<string, int[]> Footprints =
			new Dictionary<string, int[]>(StringComparer.Ordinal);
		internal readonly Dictionary<string, KingdomPlotRules.RoofState> Roofs =
			new Dictionary<string, KingdomPlotRules.RoofState>(StringComparer.Ordinal);
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
			XDocument catalogue = XDocument.Load(Path.Combine(TestMain.RepositoryRoot,
				"RuntimeData", "KingdomBuildings.xml"));
			foreach (XElement building in catalogue.Root.Elements("building"))
			{
				string key = Text(building, "Key");
				if (string.IsNullOrEmpty(Optional(building, "Plot"))) continue;
				string footprint = Optional(building, "Footprint");
				if (footprint != null) result.Footprints.Add(key, CatalogueFootprint(footprint));
				result.Roofs.Add(key, CatalogueRoof(building));
			}
			string root = Path.Combine(TestMain.RepositoryRoot, "Architecture");
			XDocument[] documents = Directory.GetFiles(root, "KingdomArchitectures*.xml")
				.OrderBy(path => path, StringComparer.Ordinal).Select(XDocument.Load).ToArray();
			for (int i = 0; i < documents.Length; i++)
			{
				foreach (XElement raw in documents[i].Root.Elements("pose"))
					result.Poses.Add(Pose(raw));
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
			if (!KingdomArchitectureRules.TryCreatePoseRegistry(corpus.Poses, null,
				out ArchitecturePoseRegistry poses, out string poseFailure))
				throw new InvalidDataException(poseFailure);
			string mapKey = string.IsNullOrEmpty(item.Variant.MapKey)
				? item.Tier.MapKey : item.Variant.MapKey;
			string paletteKey = string.IsNullOrEmpty(item.Variant.PaletteKey)
				? item.Tier.PaletteKey : item.Variant.PaletteKey;
			corpus.Footprints.TryGetValue(item.Tier.BuildKey, out int[] footprint);
			return new ArchitectureCompileRequest
			{
				PlanKey = item.PlanKey,
				Binding = item.Binding,
				Tier = item.Tier,
				Variant = item.Variant,
				Map = corpus.Maps[mapKey],
				Palette = corpus.Palettes[paletteKey],
				PoseRegistry = poses,
				BuildingBlueprint = "r_KingdomArchitectureCorpusRoot",
				CatalogueFootprintWidth = footprint == null ? 0 : footprint[0],
				CatalogueFootprintHeight = footprint == null ? 0 : footprint[1],
				CatalogueRoof = corpus.Roofs[item.Tier.BuildKey],
				Facing = facing
			};
		}

		private static ArchitecturePoseDraft Pose(XElement raw)
		{
			if (!KingdomArchitectureRules.TryParsePoseMode(Text(raw, "Mode"),
				out ArchitecturePoseMode mode))
				throw new InvalidDataException("unknown fixture pose mode " + Text(raw, "Mode"));
			return new ArchitecturePoseDraft
			{
				Blueprint = Text(raw, "Blueprint"), Mode = mode,
				North = Optional(raw, "North"), East = Optional(raw, "East"),
				South = Optional(raw, "South"), West = Optional(raw, "West")
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
			ApplyMapFootprint(result, Optional(raw, "Footprint"));
			foreach (XElement glyph in raw.Elements("glyph"))
			{
				ArchitectureGlyphDraft item = new ArchitectureGlyphDraft
				{
					Character = Text(glyph, "Char")[0],
					Ground = Optional(glyph, "Ground"),
					Structure = Optional(glyph, "Structure"),
					Object = Optional(glyph, "Object"),
					HasGroundOrientation = Has(glyph, "GroundOrientation"),
					GroundOrientation = Orientation(Optional(glyph, "GroundOrientation")),
					HasStructureOrientation = Has(glyph, "StructureOrientation"),
					StructureOrientation = Orientation(Optional(glyph, "StructureOrientation")),
					HasObjectOrientation = Has(glyph, "ObjectOrientation"),
					ObjectOrientation = Orientation(Optional(glyph, "ObjectOrientation")),
					Claim = Claim(Text(glyph, "Claim")),
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
						IncomingTransitionMode = TransitionMode(tierXml),
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

		private static ArchitectureTransitionMode TransitionMode(XElement tier)
		{
			string text = Optional(tier, "Transition");
			if (string.IsNullOrEmpty(text)) return ArchitectureTransitionMode.None;
			if (!KingdomArchitectureTransitionRules.TryParseMode(text,
				out ArchitectureTransitionMode mode))
				throw new InvalidDataException("unknown architecture transition mode " + text);
			return mode;
		}

		private static string Optional(XElement element, string name)
		{
			string value = (string)element.Attribute(name);
			return string.IsNullOrEmpty(value) ? null : value;
		}

		private static bool Has(XElement element, string name)
		{
			return element.Attribute(name) != null;
		}

		private static ArchitectureFacing Orientation(string value)
		{
			return value == "east" ? ArchitectureFacing.East
				: value == "south" ? ArchitectureFacing.South
				: value == "west" ? ArchitectureFacing.West : ArchitectureFacing.North;
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

		private static ArchitectureClaim Claim(string value)
		{
			if (value == "building") return ArchitectureClaim.Building;
			if (value == "yard") return ArchitectureClaim.Yard;
			throw new InvalidDataException("unknown architecture claim " + value);
		}

		private static void ApplyMapFootprint(ArchitectureMapDraft map, string value)
		{
			if (value == null) return;
			string[] terms = value.Split(',');
			string[] size = terms.Length == 3 ? terms[2].Split('x') : new string[0];
			if (terms.Length != 3 || size.Length != 2) throw new InvalidDataException(value);
			map.HasFootprint = true;
			map.FootprintX = int.Parse(terms[0]);
			map.FootprintY = int.Parse(terms[1]);
			map.FootprintWidth = int.Parse(size[0]);
			map.FootprintHeight = int.Parse(size[1]);
		}

		private static int[] CatalogueFootprint(string value)
		{
			string[] terms = value.Split('x');
			if (terms.Length != 2) throw new InvalidDataException(value);
			return new int[] { int.Parse(terms[0]), int.Parse(terms[1]) };
		}

		private static KingdomPlotRules.RoofState CatalogueRoof(XElement building)
		{
			string value = Optional(building, "Roof");
			if (value == null) return Optional(building, "Open") == "yes"
				? KingdomPlotRules.RoofState.Open : KingdomPlotRules.RoofState.Walled;
			return value == "Open" ? KingdomPlotRules.RoofState.Open
				: value == "Soft" ? KingdomPlotRules.RoofState.Soft
				: value == "Carved" ? KingdomPlotRules.RoofState.Carved
				: KingdomPlotRules.RoofState.Walled;
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
