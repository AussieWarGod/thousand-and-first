using System.Collections.Generic;

namespace ThousandAndFirst
{
	// --- Public XML-shaped draft records -------------------------------------------------

	public sealed class ArchitecturePaletteSlot
	{
		public string Key;
		public string Blueprint;
		public string Role;
		public string Material;
		public string MinTech;
		public string Knowledge;
		public string Power;
		public bool Natural;
	}

	public sealed class ArchitecturePaletteDraft
	{
		public string Key;
		public List<ArchitecturePaletteSlot> Slots = new List<ArchitecturePaletteSlot>();
	}

	/// <summary>
	/// One exact semantic fixture family. Cardinal siblings change presentation, not function;
	/// connected and invariant families preserve the semantic base blueprint.
	/// </summary>
	public sealed class ArchitecturePoseDraft
	{
		public string Blueprint;
		public ArchitecturePoseMode Mode;
		public string North;
		public string East;
		public string South;
		public string West;
	}

	public sealed class ArchitectureGlyphDraft
	{
		public char Character;
		public string Ground;
		public string Structure;
		public string Object;
		public bool HasGroundOrientation;
		public ArchitectureFacing GroundOrientation;
		public bool HasStructureOrientation;
		public ArchitectureFacing StructureOrientation;
		public bool HasObjectOrientation;
		public ArchitectureFacing ObjectOrientation;
		public ArchitectureClaim Claim;
		public ArchitecturePassability Passability;
		public ArchitectureCover Cover;
		public bool HasCover;
		public bool StatefulObject;
		public List<string> Anchors = new List<string>();
	}

	public sealed class ArchitectureMapDraft
	{
		public string Key;
		public int Width;
		public int Height;
		public ArchitectureCover DefaultCover;
		/// <summary>True only when XML explicitly authored the canonical X,Y,WxH rectangle.</summary>
		public bool HasFootprint;
		public int FootprintX;
		public int FootprintY;
		public int FootprintWidth;
		public int FootprintHeight;
		public List<ArchitectureGlyphDraft> Glyphs = new List<ArchitectureGlyphDraft>();
		public List<string> Rows = new List<string>();
	}

	/// <summary>Data-only selector in one architecture lane. Style/creed/terrain/stratum compare
	/// one settlement value; culture/species/genotype/body compare bounded live fact sets. A null or
	/// empty tag expression is a wildcard.</summary>
	public sealed class ArchitectureSelector
	{
		public string Styles;
		public string Creeds;
		public string Cultures;
		public string Species;
		public string Genotypes;
		public string Bodies;
		public string Terrains;
		public string Strata;
		public int MinimumStage;
		public int MaximumStage;
		public int MinimumTech;
		public int MaximumTech;

		public ArchitectureSelector()
		{
			MinimumStage = -1;
			MaximumStage = -1;
			MinimumTech = -1;
			MaximumTech = -1;
		}
	}

	/// <summary>Frozen inputs used to choose one variant before its full snapshot is receipted.
	/// Identity lists are canonical, sorted, positive facts; selection never retains mutable
	/// settlement dictionaries.</summary>
	public sealed class ArchitectureSelectionContext
	{
		public string Style;
		/// <summary>Canonical style key plus compatibility aliases. Empty falls back to
		/// <see cref="Style"/> for external callers compiled before aliases existed.</summary>
		public IList<string> StyleKeys = new List<string>();
		public string Creed;
		public IList<string> Cultures = new List<string>();
		public IList<string> Species = new List<string>();
		public IList<string> Genotypes = new List<string>();
		public IList<string> Bodies = new List<string>();
		public string Terrain;
		public string Stratum;
		public int Stage;
		public int Tech;
	}

	public sealed class ArchitectureVariantDraft
	{
		public string Key;
		public int Priority;
		public string MapKey;
		public string PaletteKey;
		public ArchitectureSelector Selector;
	}

	public sealed class ArchitectureAnchorRequirement
	{
		public string Role;
		public int Minimum;
		/// <summary>Zero means no upper bound.</summary>
		public int Maximum;
	}

	public sealed class ArchitectureTierDraft
	{
		public string Key;
		public string BuildKey;
		public int Level;
		/// <summary>How this tier may be reached from its immediate authored predecessor.</summary>
		public ArchitectureTransitionMode IncomingTransitionMode;
		public string MapKey;
		public string PaletteKey;
		public List<ArchitectureAnchorRequirement> Requirements = new List<ArchitectureAnchorRequirement>();
		public List<ArchitectureVariantDraft> Variants = new List<ArchitectureVariantDraft>();
	}

	public sealed class ArchitectureBindingDraft
	{
		public string Key;
		public string TypeKey;
		public ArchitectureLotSize Size;
		public ArchitectureFrontage Frontage;
		public List<ArchitectureTierDraft> Tiers = new List<ArchitectureTierDraft>();
	}

	public sealed class ArchitecturePlanDraft
	{
		public string Key;
		public List<ArchitectureBindingDraft> Bindings = new List<ArchitectureBindingDraft>();
	}

	/// <summary>The already-selected records required to compile one concrete future building.</summary>
	public sealed class ArchitectureCompileRequest
	{
		public string PlanKey;
		public ArchitectureBindingDraft Binding;
		public ArchitectureTierDraft Tier;
		public ArchitectureVariantDraft Variant;
		public ArchitectureMapDraft Map;
		public ArchitecturePaletteDraft Palette;
		/// <summary>
		/// Legacy engine-free inspection input. The public compiler refuses nonempty raw pose drafts;
		/// third-party cardinal families must enter through merged XML and the audited loader.
		/// </summary>
		public IList<ArchitecturePoseDraft> Poses;
		/// <summary>Loader-owned, behavior-parity-audited pose authority.</summary>
		internal ArchitecturePoseRegistry PoseRegistry;
		/// <summary>
		/// Runtime-owned behavior root placed at main. It is validated here but deliberately absent
		/// from scenery placements, receipts, and deltas so runtime identity/state can survive growth.
		/// </summary>
		public string BuildingBlueprint;
		/// <summary>Raw merged catalogue dimensions. Zero/zero means this tier fills its actual lot.</summary>
		public int CatalogueFootprintWidth;
		public int CatalogueFootprintHeight;
		public KingdomPlotRules.RoofState CatalogueRoof =
			(KingdomPlotRules.RoofState)byte.MaxValue;
		public ArchitectureFacing Facing;
	}
}
