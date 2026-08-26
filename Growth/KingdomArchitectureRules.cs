using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>The four fixed plot envelopes authored by the settlement catalogue.</summary>
	public enum ArchitectureLotSize : byte
	{
		Small = 1,
		Medium = 2,
		Large = 3,
		Huge = 4
	}

	/// <summary>The side of a lot its authored north/front edge faces in the world.</summary>
	public enum ArchitectureFacing : byte
	{
		North = 0,
		East = 1,
		South = 2,
		West = 3
	}

	/// <summary>Semantic frontage resolved by the runtime into a fixed world facing.</summary>
	public enum ArchitectureFrontage : byte
	{
		Heart = 0,
		Road = 1
	}

	/// <summary>One of the three permanent object layers an authored map may place.</summary>
	public enum ArchitectureLayer : byte
	{
		Ground = 0,
		Structure = 1,
		Object = 2
	}

	/// <summary>Semantic movement truth used before an engine blueprint is available.</summary>
	public enum ArchitecturePassability : byte
	{
		Walkable = 0,
		Blocked = 1,
		Adjacent = 2
	}

	/// <summary>Whether one claimed cell is under sky, a roof, a wall roof, or natural rock.</summary>
	public enum ArchitectureCover : byte
	{
		Open = 0,
		Soft = 1,
		Walled = 2,
		Natural = 3
	}

	/// <summary>Where an actor stands to use an anchor.</summary>
	public enum ArchitectureAnchorAccess : byte
	{
		OnCell = 0,
		Adjacent = 1
	}

	/// <summary>Whether a target plan belongs to the standing lot or needs a true restake.</summary>
	public enum ArchitectureSetChange : byte
	{
		SameSet = 0,
		Restake = 1
	}

	/// <summary>One immutable point in canonical or world coordinates.</summary>
	public struct ArchitecturePoint : IEquatable<ArchitecturePoint>
	{
		public readonly int X;
		public readonly int Y;

		public ArchitecturePoint(int X, int Y)
		{
			this.X = X;
			this.Y = Y;
		}

		public bool Equals(ArchitecturePoint Other)
		{
			return X == Other.X && Y == Other.Y;
		}

		public override bool Equals(object Object)
		{
			return Object is ArchitecturePoint && Equals((ArchitecturePoint)Object);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return X * 397 ^ Y;
			}
		}
	}

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

	public sealed class ArchitectureGlyphDraft
	{
		public char Character;
		public string Ground;
		public string Structure;
		public string Object;
		public bool Claim;
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
		/// Runtime-owned behavior root placed at main. It is validated here but deliberately absent
		/// from scenery placements, receipts, and deltas so runtime identity/state can survive growth.
		/// </summary>
		public string BuildingBlueprint;
		public ArchitectureFacing Facing;
	}

	// --- Materialised records -------------------------------------------------------------

	public sealed class ArchitectureCellState
	{
		public int X;
		public int Y;
		public bool Claim;
		public ArchitecturePassability Passability;
		public ArchitectureCover Cover;
	}

	public sealed class ArchitecturePlacement
	{
		public ArchitectureLayer Layer;
		public int X;
		public int Y;
		public string Blueprint;
		public string Slot;
		/// <summary>Canonical material key frozen from the palette slot that authored this piece.</summary>
		public string Material;
		/// <summary>Canonical minimum craft-rung key frozen from the palette slot.</summary>
		public string MinTech;
		/// <summary>Optional roster gate required to author this exact placement.</summary>
		public string Knowledge;
		/// <summary>Optional power authority; nonempty values need frozen runtime proof.</summary>
		public string Power;
		/// <summary>Natural scenery is authored truth but does not consume the paid build claim.</summary>
		public bool Natural;
		/// <summary>Bind an immutable pre-existing world relic; never create or clear it.</summary>
		public bool ExistingAuthority;
		/// <summary>Stable anchor whose state must survive a tier delta; null for stateless pieces.</summary>
		public string StatefulAnchor;
	}

	public sealed class ArchitectureAnchor
	{
		public string Key;
		public int X;
		public int Y;
		public ArchitectureAnchorAccess Access;
	}

	public sealed class ArchitectureLayoutSnapshot
	{
		public string PlanKey;
		public string BindingKey;
		public string BuildKey;
		public string TierKey;
		public string VariantKey;
		public string PaletteKey;
		public string LotType;
		public ArchitectureLotSize LotSize;
		public ArchitectureFacing Facing;
		public int Width;
		public int Height;
		public int MainX;
		public int MainY;
		public List<ArchitectureCellState> Cells = new List<ArchitectureCellState>();
		/// <summary>Authored scenery only. Runtime-owned main behavior root is never included.</summary>
		public List<ArchitecturePlacement> Placements = new List<ArchitecturePlacement>();
		public List<ArchitectureAnchor> Anchors = new List<ArchitectureAnchor>();
	}

	public sealed class ArchitectureCellDelta
	{
		public int X;
		public int Y;
		public ArchitectureCellState Before;
		public ArchitectureCellState After;
	}

	public sealed class ArchitectureLayoutDelta
	{
		/// <summary>
		/// Scenery-only exact delta. Caller must preserve the runtime-owned behavior root at main.
		/// </summary>
		public ArchitectureLayoutSnapshot Before;
		public ArchitectureLayoutSnapshot After;
		public List<ArchitecturePlacement> Retained = new List<ArchitecturePlacement>();
		/// <summary>Successor-side partner for each retained predecessor placement.</summary>
		public List<ArchitecturePlacement> RetainedAfter = new List<ArchitecturePlacement>();
		public List<ArchitecturePlacement> Removed = new List<ArchitecturePlacement>();
		public List<ArchitecturePlacement> Added = new List<ArchitecturePlacement>();
		public List<ArchitectureCellDelta> Cells = new List<ArchitectureCellDelta>();
	}

	/// <summary>One exact time × labour × infrastructure accrual result.</summary>
	public sealed class ArchitectureLabourProgress
	{
		public long PreviousTick;
		public long NextTick;
		public long RemainingTicks;
		public long WorkedTicks;
		public long CompletionTick;
		public bool Complete;
	}

	/// <summary>
	/// Engine-free authored architecture laws. The engine-facing loader may parse XML into the
	/// draft records above; only this class is allowed to turn those drafts into a durable map.
	/// </summary>
	public static class KingdomArchitectureRules
	{
		public const int LegacySnapshotSchema = 1;
		public const int SnapshotSchema = 2;
		public const int MaxKeyChars = 128;
		public const int MaxBlueprintChars = 256;
		public const int MaxSelectorChars = 256;
		public const int MaxSelectorTokens = 16;
		public const int MaxPaletteSlots = 128;
		public const int MaxGlyphs = 96;
		public const int MaxMapArea = 280;
		public const int MaxPlacements = 512;
		public const int MaxAnchors = 64;
		public const int MaxBindingsPerPlan = 16;
		public const int MaxTiersPerBinding = 16;
		public const int MaxVariantsPerTier = 32;
		public const int MaxRequirementsPerTier = 32;
		/// <summary>Hard binary envelope for one canonical authored-layout receipt. Eight KiB keeps
		/// save properties bounded while leaving measured headroom above the densest shipped XL
		/// design. The independent architecture gate reproduces the codec and proves every authored
		/// tier/variant against this exact value.</summary>
		public const int MaxSnapshotPayloadBytes = 8192;

		/// <summary>Outer text envelope for the version, base64 payload, separator, and SHA-256.
		/// Deliberately a little wider than the largest encoding of
		/// <see cref="MaxSnapshotPayloadBytes"/> so the binary cap remains the controlling bound.</summary>
		public const int MaxSnapshotChars = 11264;
		private const ushort NoAnchorIndex = ushort.MaxValue;
		private const byte NoKnowledgeIndex = byte.MaxValue;
		private const byte NoPowerIndex = byte.MaxValue;
		private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

		// --- Lot dimensions and poses ------------------------------------------------------

		public static bool TryCanonicalDimensions(ArchitectureLotSize Size, out int Width, out int Height)
		{
			switch (Size)
			{
			case ArchitectureLotSize.Small: Width = 5; Height = 4; return true;
			case ArchitectureLotSize.Medium: Width = 8; Height = 6; return true;
			case ArchitectureLotSize.Large: Width = 12; Height = 9; return true;
			case ArchitectureLotSize.Huge: Width = 20; Height = 14; return true;
			default: Width = 0; Height = 0; return false;
			}
		}

		public static bool TryDimensions(ArchitectureLotSize Size, ArchitectureFacing Facing,
			out int Width, out int Height)
		{
			if (!TryCanonicalDimensions(Size, out Width, out Height) || !KnownFacing(Facing))
			{
				Width = 0;
				Height = 0;
				return false;
			}
			if (Facing == ArchitectureFacing.East || Facing == ArchitectureFacing.West)
			{
				int swap = Width;
				Width = Height;
				Height = swap;
			}
			return true;
		}

		public static bool TryWorldDimensions(int CanonicalWidth, int CanonicalHeight,
			ArchitectureFacing Facing, out int Width, out int Height)
		{
			Width = 0;
			Height = 0;
			if (CanonicalWidth <= 0 || CanonicalHeight <= 0
				|| (long)CanonicalWidth * CanonicalHeight > MaxMapArea || !KnownFacing(Facing))
				return false;
			if (Facing == ArchitectureFacing.East || Facing == ArchitectureFacing.West)
			{
				Width = CanonicalHeight;
				Height = CanonicalWidth;
			}
			else
			{
				Width = CanonicalWidth;
				Height = CanonicalHeight;
			}
			return true;
		}

		/// <summary>Rotates a canonical cell into a world rect whose low corner is Origin.</summary>
		public static bool TryToWorld(int OriginX, int OriginY, int CanonicalWidth,
			int CanonicalHeight, ArchitectureFacing Facing, int U, int V, out int X, out int Y)
		{
			X = 0;
			Y = 0;
			if (!TryWorldDimensions(CanonicalWidth, CanonicalHeight, Facing, out _, out _)
				|| U < 0 || U >= CanonicalWidth || V < 0 || V >= CanonicalHeight)
				return false;
			long relativeX;
			long relativeY;
			switch (Facing)
			{
			case ArchitectureFacing.North:
				relativeX = U; relativeY = V; break;
			case ArchitectureFacing.East:
				relativeX = CanonicalHeight - 1 - V; relativeY = U; break;
			case ArchitectureFacing.South:
				relativeX = CanonicalWidth - 1 - U; relativeY = CanonicalHeight - 1 - V; break;
			case ArchitectureFacing.West:
				relativeX = V; relativeY = CanonicalWidth - 1 - U; break;
			default:
				return false;
			}
			long worldX = (long)OriginX + relativeX;
			long worldY = (long)OriginY + relativeY;
			if (worldX < int.MinValue || worldX > int.MaxValue
				|| worldY < int.MinValue || worldY > int.MaxValue) return false;
			X = (int)worldX;
			Y = (int)worldY;
			return true;
		}

		/// <summary>Inverse of <see cref="TryToWorld"/> for a cell inside the posed rect.</summary>
		public static bool TryToCanonical(int OriginX, int OriginY, int CanonicalWidth,
			int CanonicalHeight, ArchitectureFacing Facing, int X, int Y, out int U, out int V)
		{
			U = 0;
			V = 0;
			if (!TryWorldDimensions(CanonicalWidth, CanonicalHeight, Facing,
				out int worldWidth, out int worldHeight)) return false;
			long relativeX = (long)X - OriginX;
			long relativeY = (long)Y - OriginY;
			if (relativeX < 0 || relativeX >= worldWidth
				|| relativeY < 0 || relativeY >= worldHeight) return false;
			switch (Facing)
			{
			case ArchitectureFacing.North:
				U = (int)relativeX; V = (int)relativeY; break;
			case ArchitectureFacing.East:
				U = (int)relativeY; V = CanonicalHeight - 1 - (int)relativeX; break;
			case ArchitectureFacing.South:
				U = CanonicalWidth - 1 - (int)relativeX;
				V = CanonicalHeight - 1 - (int)relativeY; break;
			case ArchitectureFacing.West:
				U = CanonicalWidth - 1 - (int)relativeY; V = (int)relativeX; break;
			default:
				return false;
			}
			return U >= 0 && U < CanonicalWidth && V >= 0 && V < CanonicalHeight;
		}

		// --- Typed lot binding --------------------------------------------------------------

		public static bool TryClassifySetChange(string CurrentType, ArchitectureLotSize CurrentSize,
			string TargetType, ArchitectureLotSize TargetSize, out ArchitectureSetChange Change)
		{
			Change = ArchitectureSetChange.Restake;
			string current = FoldType(CurrentType);
			string target = FoldType(TargetType);
			if (current == null || target == null
				|| !KnownLotSize(CurrentSize) || !KnownLotSize(TargetSize)) return false;
			Change = CurrentSize == TargetSize && current == target
				? ArchitectureSetChange.SameSet : ArchitectureSetChange.Restake;
			return true;
		}

		// --- Plan and variant validation ----------------------------------------------------

		public static bool TryValidatePlan(ArchitecturePlanDraft Plan, out string Failure)
		{
			Failure = null;
			if (Plan == null || !ValidKey(Plan.Key)
				|| Plan.Bindings == null || Plan.Bindings.Count == 0
				|| Plan.Bindings.Count > MaxBindingsPerPlan)
				return Fail("plan is absent, unnamed, empty, or over the binding bound", out Failure);
			HashSet<string> bindingKeys = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> sets = new HashSet<string>(StringComparer.Ordinal);
			for (int b = 0; b < Plan.Bindings.Count; b++)
			{
				ArchitectureBindingDraft binding = Plan.Bindings[b];
				string type = binding == null ? null : FoldType(binding.TypeKey);
				if (binding == null || !ValidKey(binding.Key) || !bindingKeys.Add(binding.Key)
					|| type == null || !KnownLotSize(binding.Size) || !KnownFrontage(binding.Frontage)
					|| binding.Tiers == null || binding.Tiers.Count == 0
					|| binding.Tiers.Count > MaxTiersPerBinding)
					return Fail("plan has a bad, duplicate, empty, or oversized binding", out Failure);
				string set = type + "|" + ((int)binding.Size).ToString(CultureInfo.InvariantCulture);
				if (!sets.Add(set)) return Fail("plan declares the same type and size twice", out Failure);
				HashSet<string> tierKeys = new HashSet<string>(StringComparer.Ordinal);
				HashSet<string> buildKeys = new HashSet<string>(StringComparer.Ordinal);
				for (int t = 0; t < binding.Tiers.Count; t++)
				{
					ArchitectureTierDraft tier = binding.Tiers[t];
					if (tier == null || !ValidKey(tier.Key) || !tierKeys.Add(tier.Key)
						|| !ValidKey(tier.BuildKey) || !buildKeys.Add(tier.BuildKey)
						|| tier.Level < 0 || !ValidKey(tier.MapKey) || !ValidKey(tier.PaletteKey)
						|| tier.Requirements == null || tier.Requirements.Count > MaxRequirementsPerTier)
						return Fail("binding has a bad or duplicate tier", out Failure);
					for (int r = 0; r < tier.Requirements.Count; r++)
					{
						ArchitectureAnchorRequirement requirement = tier.Requirements[r];
						if (requirement == null || !ValidKey(requirement.Role)
							|| requirement.Minimum < 0 || requirement.Maximum < 0
							|| (requirement.Maximum > 0 && requirement.Maximum < requirement.Minimum))
							return Fail("tier has a malformed anchor requirement", out Failure);
					}
					for (int p = 0; p < t; p++)
						if (binding.Tiers[p].Level == tier.Level)
							return Fail("binding declares the same tier level twice", out Failure);
					if (!TryValidateVariants(tier.Variants, out Failure)) return false;
				}
			}
			return true;
		}

		public static bool TryValidateVariants(IList<ArchitectureVariantDraft> Variants,
			out string Failure)
		{
			Failure = null;
			if (Variants == null || Variants.Count == 0 || Variants.Count > MaxVariantsPerTier)
				return Fail("variant list is empty or over the bound", out Failure);
			HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
			bool fallback = false;
			for (int i = 0; i < Variants.Count; i++)
			{
				ArchitectureVariantDraft variant = Variants[i];
				if (variant == null || !ValidKey(variant.Key) || !keys.Add(variant.Key)
					|| (!string.IsNullOrEmpty(variant.MapKey) && !ValidKey(variant.MapKey))
					|| (!string.IsNullOrEmpty(variant.PaletteKey) && !ValidKey(variant.PaletteKey))
					|| !ValidSelector(variant.Selector, out Failure))
					return Failure != null ? false : Fail("variant is bad or duplicated", out Failure);
				if (Unconditional(variant.Selector)) fallback = true;
			}
			if (!fallback) return Fail("variant list has no unconditional fallback", out Failure);
			return true;
		}

		public static bool TrySelectVariant(IList<ArchitectureVariantDraft> Variants,
			ArchitectureSelectionContext Context, out ArchitectureVariantDraft Variant,
			out string Failure)
		{
			Variant = null;
			if (!TryValidateVariants(Variants, out Failure)) return false;
			ArchitectureSelectionContext context = Context ?? new ArchitectureSelectionContext();
			int bestSpecificity = int.MinValue;
			for (int i = 0; i < Variants.Count; i++)
			{
				ArchitectureVariantDraft candidate = Variants[i];
				if (!SelectorMatches(candidate.Selector, context)) continue;
				int specificity = SelectorSpecificity(candidate.Selector);
				if (Variant == null || candidate.Priority > Variant.Priority
					|| (candidate.Priority == Variant.Priority && specificity > bestSpecificity)
					|| (candidate.Priority == Variant.Priority && specificity == bestSpecificity
						&& string.CompareOrdinal(candidate.Key, Variant.Key) < 0))
				{
					Variant = candidate;
					bestSpecificity = specificity;
				}
			}
			if (Variant == null) return Fail("no variant matched despite a validated fallback", out Failure);
			Failure = null;
			return true;
		}

		// --- Compilation --------------------------------------------------------------------

		public static bool TryCompile(ArchitectureCompileRequest Request,
			out ArchitectureLayoutSnapshot Snapshot, out string Failure)
		{
			Snapshot = null;
			Failure = null;
			if (Request == null || !ValidKey(Request.PlanKey) || Request.Binding == null
				|| Request.Tier == null || Request.Variant == null || Request.Map == null
				|| Request.Palette == null || !ValidBlueprint(Request.BuildingBlueprint)
				|| !KnownFacing(Request.Facing))
				return Fail("compile request is incomplete or malformed", out Failure);
			ArchitectureBindingDraft binding = Request.Binding;
			ArchitectureTierDraft tier = Request.Tier;
			ArchitectureVariantDraft variant = Request.Variant;
			ArchitectureMapDraft map = Request.Map;
			ArchitecturePaletteDraft palette = Request.Palette;
			string type = FoldType(binding.TypeKey);
			if (!ValidKey(binding.Key) || type == null || !KnownLotSize(binding.Size)
				|| !KnownFrontage(binding.Frontage) || !ValidKey(tier.Key) || !ValidKey(tier.BuildKey)
				|| !ValidKey(tier.MapKey) || !ValidKey(tier.PaletteKey)
				|| !ValidKey(variant.Key) || !ValidSelector(variant.Selector, out Failure)
				|| !ValidKey(map.Key) || !ValidKey(palette.Key))
				return Fail("selected plan metadata is malformed", out Failure);
			string expectedMap = string.IsNullOrEmpty(variant.MapKey) ? tier.MapKey : variant.MapKey;
			string expectedPalette = string.IsNullOrEmpty(variant.PaletteKey)
				? tier.PaletteKey : variant.PaletteKey;
			if (expectedMap != map.Key || expectedPalette != palette.Key)
				return Fail("selected map or palette does not match the tier and variant", out Failure);
			if (!TryCanonicalDimensions(binding.Size, out int lotWidth, out int lotHeight)
				|| map.Width != lotWidth || map.Height != lotHeight
				|| (long)map.Width * map.Height > MaxMapArea
				|| map.Rows == null || map.Rows.Count != map.Height
				|| map.Glyphs == null || map.Glyphs.Count > MaxGlyphs
				|| !KnownCover(map.DefaultCover))
				return Fail("map dimensions, rows, glyph count, or default cover are invalid", out Failure);
			if (!TryPalette(palette, out Dictionary<string, ArchitecturePaletteSlot> slots,
				out Failure)) return false;
			Dictionary<char, ArchitectureGlyphDraft> glyphs = new Dictionary<char, ArchitectureGlyphDraft>();
			for (int i = 0; i < map.Glyphs.Count; i++)
			{
				ArchitectureGlyphDraft glyph = map.Glyphs[i];
				if (glyph == null || glyph.Character < '!' || glyph.Character > '~'
					|| glyph.Character == '.' || glyphs.ContainsKey(glyph.Character)
					|| !KnownPassability(glyph.Passability)
					|| (glyph.HasCover && !KnownCover(glyph.Cover))
					|| glyph.Anchors == null || glyph.Anchors.Count > MaxAnchors)
					return Fail("map has a malformed, reserved, or duplicate glyph", out Failure);
				if (!TryValidateGlyph(glyph, slots, out Failure)) return false;
				glyphs.Add(glyph.Character, glyph);
			}

			ArchitectureLayoutSnapshot snapshot = new ArchitectureLayoutSnapshot
			{
				PlanKey = Request.PlanKey,
				BindingKey = binding.Key,
				BuildKey = tier.BuildKey,
				TierKey = tier.Key,
				VariantKey = variant.Key,
				PaletteKey = palette.Key,
				LotType = type,
				LotSize = binding.Size,
				Facing = Request.Facing,
				Width = map.Width,
				Height = map.Height,
				MainX = -1,
				MainY = -1
			};
			HashSet<string> anchorKeys = new HashSet<string>(StringComparer.Ordinal);
			int buildingCount = 0;
			for (int y = 0; y < map.Height; y++)
			{
				string row = map.Rows[y];
				if (row == null || row.Length != map.Width)
					return Fail("map row width does not match its declaration", out Failure);
				for (int x = 0; x < map.Width; x++)
				{
					char symbol = row[x];
					ArchitectureGlyphDraft glyph = null;
					if (symbol != '.' && !glyphs.TryGetValue(symbol, out glyph))
						return Fail("map row uses an undefined glyph", out Failure);
					ArchitectureCellState cell = new ArchitectureCellState
					{
						X = x,
						Y = y,
						Claim = glyph != null && glyph.Claim,
						Passability = glyph == null ? ArchitecturePassability.Walkable : glyph.Passability,
						Cover = glyph == null ? ArchitectureCover.Open
							: (glyph.HasCover ? glyph.Cover : map.DefaultCover)
					};
					snapshot.Cells.Add(cell);
					if (glyph == null) continue;
					if (!cell.Claim && (HasSceneryToken(glyph.Ground)
						|| HasSceneryToken(glyph.Structure) || HasSceneryToken(glyph.Object)))
						return Fail("map places scenery on an unclaimed cell", out Failure);

					List<ArchitectureAnchor> cellAnchors = new List<ArchitectureAnchor>();
					for (int a = 0; a < glyph.Anchors.Count; a++)
					{
						string role = glyph.Anchors[a];
						string key = role == "main" ? role : StableAnchorKey(role, x, y);
						if (!ValidKey(key) || !anchorKeys.Add(key))
							return Fail("map has a malformed or duplicate anchor", out Failure);
						ArchitectureAnchor anchor = new ArchitectureAnchor
						{
							Key = key,
							X = x,
							Y = y,
							Access = glyph.Passability == ArchitecturePassability.Walkable
								? ArchitectureAnchorAccess.OnCell : ArchitectureAnchorAccess.Adjacent
						};
						cellAnchors.Add(anchor);
						snapshot.Anchors.Add(anchor);
					}

					bool hasBuilding = false;
					if (!TryAddPlacement(snapshot, ArchitectureLayer.Ground, x, y, glyph.Ground,
						false, cellAnchors, slots, ref hasBuilding, out Failure)
						|| !TryAddPlacement(snapshot, ArchitectureLayer.Structure, x, y, glyph.Structure,
						false, cellAnchors, slots, ref hasBuilding, out Failure)
						|| !TryAddPlacement(snapshot, ArchitectureLayer.Object, x, y, glyph.Object,
						glyph.StatefulObject, cellAnchors, slots, ref hasBuilding, out Failure))
						return false;
					if (hasBuilding)
					{
						buildingCount++;
						if (!ContainsAnchor(cellAnchors, "main"))
							return Fail("$building must share its cell with the main anchor", out Failure);
						snapshot.MainX = x;
						snapshot.MainY = y;
					}
					else if (ContainsAnchor(cellAnchors, "main"))
						return Fail("main anchor must share its cell with $building", out Failure);
				}
			}
			if (buildingCount != 1) return Fail("map must place exactly one $building", out Failure);
			if (snapshot.Placements.Count > MaxPlacements || snapshot.Anchors.Count > MaxAnchors)
				return Fail("compiled placements or anchors exceed the bound", out Failure);
			SortSnapshot(snapshot);
			if (!TryValidateTopology(snapshot, tier.Requirements, out Failure)) return false;
			if (!TryEncodeSnapshot(snapshot, out _, out Failure)) return false;
			Snapshot = snapshot;
			return true;
		}

		// --- Topology -----------------------------------------------------------------------

		public static bool TryValidateTopology(ArchitectureLayoutSnapshot Snapshot,
			IList<ArchitectureAnchorRequirement> Requirements, out string Failure)
		{
			return TryValidateTopologyCore(Snapshot, Requirements, false, out Failure);
		}

		private static bool TryValidateTopologyCore(ArchitectureLayoutSnapshot Snapshot,
			IList<ArchitectureAnchorRequirement> Requirements, bool AllowLegacyPlacementTruth,
			out string Failure)
		{
			if (!TryValidateSnapshotShape(Snapshot, AllowLegacyPlacementTruth, out Failure)) return false;
			Dictionary<int, ArchitectureCellState> cells = CellDictionary(Snapshot.Cells, Snapshot.Width);
			List<ArchitectureAnchor> entrances = new List<ArchitectureAnchor>();
			for (int i = 0; i < Snapshot.Anchors.Count; i++)
			{
				ArchitectureAnchor anchor = Snapshot.Anchors[i];
				if (AnchorRole(anchor.Key) == "entrance:public") entrances.Add(anchor);
			}
			if (entrances.Count == 0) return Fail("map has no entrance:public anchor", out Failure);
			Queue<ArchitecturePoint> frontier = new Queue<ArchitecturePoint>();
			HashSet<int> reached = new HashSet<int>();
			for (int i = 0; i < entrances.Count; i++)
			{
				ArchitectureAnchor entrance = entrances[i];
				ArchitectureCellState cell = cells[CellKey(entrance.X, entrance.Y, Snapshot.Width)];
				if (!cell.Claim || cell.Passability != ArchitecturePassability.Walkable
					|| !ClaimBoundary(cells, Snapshot.Width, Snapshot.Height, entrance.X, entrance.Y))
					return Fail("public entrance is not a walkable claimed boundary cell", out Failure);
				int key = CellKey(entrance.X, entrance.Y, Snapshot.Width);
				if (reached.Add(key)) frontier.Enqueue(new ArchitecturePoint(entrance.X, entrance.Y));
			}
			int[] dx = new int[4] { 0, 1, 0, -1 };
			int[] dy = new int[4] { -1, 0, 1, 0 };
			while (frontier.Count > 0)
			{
				ArchitecturePoint current = frontier.Dequeue();
				for (int d = 0; d < 4; d++)
				{
					int x = current.X + dx[d];
					int y = current.Y + dy[d];
					if (x < 0 || x >= Snapshot.Width || y < 0 || y >= Snapshot.Height) continue;
					int key = CellKey(x, y, Snapshot.Width);
					ArchitectureCellState cell = cells[key];
					if (cell.Claim && cell.Passability == ArchitecturePassability.Walkable
						&& reached.Add(key)) frontier.Enqueue(new ArchitecturePoint(x, y));
				}
			}
			for (int i = 0; i < Snapshot.Anchors.Count; i++)
			{
				ArchitectureAnchor anchor = Snapshot.Anchors[i];
				bool accessible = anchor.Access == ArchitectureAnchorAccess.OnCell
					? reached.Contains(CellKey(anchor.X, anchor.Y, Snapshot.Width))
					: AdjacentReached(anchor.X, anchor.Y, Snapshot.Width, Snapshot.Height, reached);
				if (!accessible) return Fail("anchor " + anchor.Key + " is unreachable", out Failure);
			}
			if (Requirements != null)
			{
				if (Requirements.Count > MaxRequirementsPerTier)
					return Fail("anchor requirements exceed the bound", out Failure);
				for (int r = 0; r < Requirements.Count; r++)
				{
					ArchitectureAnchorRequirement requirement = Requirements[r];
					if (requirement == null || !ValidKey(requirement.Role)
						|| requirement.Minimum < 0 || requirement.Maximum < 0
						|| (requirement.Maximum > 0 && requirement.Maximum < requirement.Minimum))
						return Fail("anchor requirement is malformed", out Failure);
					int count = 0;
					for (int i = 0; i < Snapshot.Anchors.Count; i++)
						if (AnchorMatchesRole(Snapshot.Anchors[i].Key, requirement.Role)) count++;
					if (count < requirement.Minimum
						|| (requirement.Maximum > 0 && count > requirement.Maximum))
						return Fail("anchor role " + requirement.Role + " has the wrong count", out Failure);
				}
			}
			return true;
		}

		// --- Canonical snapshot codec -------------------------------------------------------

		public static bool TryEncodeSnapshot(ArchitectureLayoutSnapshot Snapshot,
			out string Encoded, out string Failure)
		{
			return TryEncodeSnapshotVersion(Snapshot, SnapshotSchema, out Encoded, out Failure);
		}

		private static bool TryEncodeSnapshotVersion(ArchitectureLayoutSnapshot Snapshot,
			int Schema, out string Encoded, out string Failure)
		{
			Encoded = null;
			Failure = null;
			bool legacy = Schema == LegacySnapshotSchema;
			if ((!legacy && Schema != SnapshotSchema)
				|| !TryValidateTopologyCore(Snapshot, null, legacy, out Failure)) return false;
			if (legacy && !LegacyPlacementTruthOnly(Snapshot))
				return Fail("legacy snapshot placement truth is not empty", out Failure);
			List<ArchitectureCellState> cells = new List<ArchitectureCellState>(Snapshot.Cells);
			List<ArchitectureAnchor> anchors = new List<ArchitectureAnchor>(Snapshot.Anchors);
			List<ArchitecturePlacement> placements = new List<ArchitecturePlacement>(Snapshot.Placements);
			cells.Sort(CompareCells);
			anchors.Sort(delegate(ArchitectureAnchor A, ArchitectureAnchor B)
			{
				return string.CompareOrdinal(A.Key, B.Key);
			});
			placements.Sort(ComparePlacements);
			List<string> blueprints = BlueprintTable(placements);
			Dictionary<string, byte> blueprintIndexes = new Dictionary<string, byte>(StringComparer.Ordinal);
			for (int i = 0; i < blueprints.Count; i++) blueprintIndexes[blueprints[i]] = (byte)i;
			List<string> materials = legacy ? new List<string>() : PlacementTextTable(placements, 0);
			List<string> techs = legacy ? new List<string>() : PlacementTextTable(placements, 1);
			List<string> knowledge = legacy ? new List<string>() : PlacementTextTable(placements, 2);
			List<string> powers = legacy ? new List<string>() : PlacementTextTable(placements, 3);
			Dictionary<string, byte> materialIndexes = new Dictionary<string, byte>(StringComparer.Ordinal);
			Dictionary<string, byte> techIndexes = new Dictionary<string, byte>(StringComparer.Ordinal);
			Dictionary<string, byte> knowledgeIndexes = new Dictionary<string, byte>(StringComparer.Ordinal);
			Dictionary<string, byte> powerIndexes = new Dictionary<string, byte>(StringComparer.Ordinal);
			for (int i = 0; i < materials.Count; i++) materialIndexes[materials[i]] = (byte)i;
			for (int i = 0; i < techs.Count; i++) techIndexes[techs[i]] = (byte)i;
			for (int i = 0; i < knowledge.Count; i++) knowledgeIndexes[knowledge[i]] = (byte)i;
			for (int i = 0; i < powers.Count; i++) powerIndexes[powers[i]] = (byte)i;
			Dictionary<string, ushort> anchorIndexes = new Dictionary<string, ushort>(StringComparer.Ordinal);
			for (int i = 0; i < anchors.Count; i++) anchorIndexes[anchors[i].Key] = (ushort)i;
			byte[] payload;
			try
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8))
				{
					writer.Write((byte)'T');
					writer.Write((byte)'A');
					writer.Write((byte)'F');
					writer.Write((byte)Schema);
					WriteText(writer, Snapshot.PlanKey, MaxKeyChars);
					WriteText(writer, Snapshot.BindingKey, MaxKeyChars);
					WriteText(writer, Snapshot.BuildKey, MaxKeyChars);
					WriteText(writer, Snapshot.TierKey, MaxKeyChars);
					WriteText(writer, Snapshot.VariantKey, MaxKeyChars);
					WriteText(writer, Snapshot.PaletteKey, MaxKeyChars);
					WriteText(writer, Snapshot.LotType, MaxKeyChars);
					writer.Write((byte)Snapshot.LotSize);
					writer.Write((byte)Snapshot.Facing);
					writer.Write((byte)Snapshot.Width);
					writer.Write((byte)Snapshot.Height);
					writer.Write((byte)Snapshot.MainX);
					writer.Write((byte)Snapshot.MainY);
					writer.Write((byte)blueprints.Count);
					for (int i = 0; i < blueprints.Count; i++)
						WriteText(writer, blueprints[i], MaxBlueprintChars);
					if (!legacy)
					{
						writer.Write((byte)materials.Count);
						for (int i = 0; i < materials.Count; i++)
							WriteText(writer, materials[i], MaxKeyChars);
						writer.Write((byte)techs.Count);
						for (int i = 0; i < techs.Count; i++)
							WriteText(writer, techs[i], MaxKeyChars);
						writer.Write((byte)knowledge.Count);
						for (int i = 0; i < knowledge.Count; i++)
							WriteText(writer, knowledge[i], MaxKeyChars);
						writer.Write((byte)powers.Count);
						for (int i = 0; i < powers.Count; i++)
							WriteText(writer, powers[i], MaxKeyChars);
					}
					writer.Write((ushort)cells.Count);
					for (int i = 0; i < cells.Count; i++)
					{
						ArchitectureCellState cell = cells[i];
						writer.Write((byte)cell.X);
						writer.Write((byte)cell.Y);
						int flags = (cell.Claim ? 1 : 0)
							| ((int)cell.Passability << 1) | ((int)cell.Cover << 3);
						writer.Write((byte)flags);
					}
					writer.Write((byte)anchors.Count);
					for (int i = 0; i < anchors.Count; i++)
					{
						ArchitectureAnchor anchor = anchors[i];
						WriteText(writer, anchor.Key, MaxKeyChars);
						writer.Write((byte)anchor.X);
						writer.Write((byte)anchor.Y);
						writer.Write((byte)anchor.Access);
					}
					writer.Write((ushort)placements.Count);
					for (int i = 0; i < placements.Count; i++)
					{
						ArchitecturePlacement placement = placements[i];
						writer.Write((byte)placement.Layer);
						writer.Write((byte)placement.X);
						writer.Write((byte)placement.Y);
						writer.Write(blueprintIndexes[placement.Blueprint]);
						writer.Write(string.IsNullOrEmpty(placement.StatefulAnchor)
							? NoAnchorIndex : anchorIndexes[placement.StatefulAnchor]);
						if (!legacy)
						{
							writer.Write(materialIndexes[placement.Material]);
							writer.Write(techIndexes[placement.MinTech]);
							writer.Write((byte)((placement.Natural ? 1 : 0)
								| (placement.ExistingAuthority ? 2 : 0)));
							writer.Write(string.IsNullOrEmpty(placement.Knowledge)
								? NoKnowledgeIndex : knowledgeIndexes[placement.Knowledge]);
							writer.Write(string.IsNullOrEmpty(placement.Power)
								? NoPowerIndex : powerIndexes[placement.Power]);
						}
					}
					writer.Flush();
					payload = stream.ToArray();
				}
			}
			catch (Exception exception)
			{
				return Fail("snapshot encoding failed: " + exception.Message, out Failure);
			}
			if (payload.Length > MaxSnapshotPayloadBytes)
				return Fail("snapshot payload exceeds the byte bound ("
					+ payload.Length.ToString(CultureInfo.InvariantCulture) + " > "
					+ MaxSnapshotPayloadBytes.ToString(CultureInfo.InvariantCulture) + ")", out Failure);
			string hash = Hash(payload);
			string encoded = "a" + Schema.ToString(CultureInfo.InvariantCulture) + "|"
				+ Convert.ToBase64String(payload) + "|" + hash;
			if (encoded.Length > MaxSnapshotChars)
				return Fail("snapshot exceeds the character bound", out Failure);
			Encoded = encoded;
			return true;
		}

		public static bool TryDecodeSnapshot(string Encoded, out ArchitectureLayoutSnapshot Snapshot,
			out string Failure)
		{
			Snapshot = null;
			Failure = null;
			if (string.IsNullOrEmpty(Encoded) || Encoded.Length > MaxSnapshotChars)
				return Fail("snapshot is empty or over the character bound", out Failure);
			string[] terms = Encoded.Split('|');
			int schema = terms.Length == 3 && terms[0] == "a1" ? LegacySnapshotSchema
				: (terms.Length == 3 && terms[0] == "a2" ? SnapshotSchema : 0);
			if (schema == 0)
				return Fail("snapshot version is unsupported", out Failure);
			if (!CanonicalHash(terms[2])) return Fail("snapshot hash is malformed", out Failure);
			byte[] payload;
			try { payload = Convert.FromBase64String(terms[1]); }
			catch { return Fail("snapshot payload is not base64", out Failure); }
			if (payload.Length == 0 || payload.Length > MaxSnapshotPayloadBytes)
				return Fail("snapshot payload is empty or over the byte bound", out Failure);
			if (Hash(payload) != terms[2]) return Fail("snapshot hash does not match its payload", out Failure);
			ArchitectureLayoutSnapshot parsed;
			try
			{
				using (MemoryStream stream = new MemoryStream(payload, false))
				using (BinaryReader reader = new BinaryReader(stream, StrictUtf8))
				{
					if (reader.ReadByte() != (byte)'T' || reader.ReadByte() != (byte)'A'
						|| reader.ReadByte() != (byte)'F' || reader.ReadByte() != schema)
						return Fail("snapshot payload header is unsupported", out Failure);
					parsed = new ArchitectureLayoutSnapshot
					{
						PlanKey = ReadText(reader, MaxKeyChars),
						BindingKey = ReadText(reader, MaxKeyChars),
						BuildKey = ReadText(reader, MaxKeyChars),
						TierKey = ReadText(reader, MaxKeyChars),
						VariantKey = ReadText(reader, MaxKeyChars),
						PaletteKey = ReadText(reader, MaxKeyChars),
						LotType = ReadText(reader, MaxKeyChars),
						LotSize = (ArchitectureLotSize)reader.ReadByte(),
						Facing = (ArchitectureFacing)reader.ReadByte(),
						Width = reader.ReadByte(),
						Height = reader.ReadByte(),
						MainX = reader.ReadByte(),
						MainY = reader.ReadByte()
					};
					int blueprintCount = reader.ReadByte();
					if (blueprintCount > MaxPaletteSlots) throw new InvalidDataException("blueprint table bound");
					List<string> blueprints = new List<string>();
					for (int i = 0; i < blueprintCount; i++)
						blueprints.Add(ReadText(reader, MaxBlueprintChars));
					List<string> materials = new List<string>();
					List<string> techs = new List<string>();
					List<string> knowledge = new List<string>();
					List<string> powers = new List<string>();
					if (schema == SnapshotSchema)
					{
						int materialCount = reader.ReadByte();
						if (materialCount > MaxPaletteSlots)
							throw new InvalidDataException("material table bound");
						for (int i = 0; i < materialCount; i++)
							materials.Add(ReadText(reader, MaxKeyChars));
						int techCount = reader.ReadByte();
						if (techCount > MaxPaletteSlots)
							throw new InvalidDataException("tech table bound");
						for (int i = 0; i < techCount; i++)
							techs.Add(ReadText(reader, MaxKeyChars));
						int knowledgeCount = reader.ReadByte();
						if (knowledgeCount > MaxPaletteSlots)
							throw new InvalidDataException("knowledge table bound");
						for (int i = 0; i < knowledgeCount; i++)
							knowledge.Add(ReadText(reader, MaxKeyChars));
						int powerCount = reader.ReadByte();
						if (powerCount > MaxPaletteSlots)
							throw new InvalidDataException("power table bound");
						for (int i = 0; i < powerCount; i++)
							powers.Add(ReadText(reader, MaxKeyChars));
					}
					int cellCount = reader.ReadUInt16();
					if (cellCount > MaxMapArea) throw new InvalidDataException("cell bound");
					for (int i = 0; i < cellCount; i++)
					{
						int x = reader.ReadByte();
						int y = reader.ReadByte();
						int flags = reader.ReadByte();
						if ((flags & ~31) != 0) throw new InvalidDataException("cell flags");
						parsed.Cells.Add(new ArchitectureCellState
						{
							X = x,
							Y = y,
							Claim = (flags & 1) != 0,
							Passability = (ArchitecturePassability)((flags >> 1) & 3),
							Cover = (ArchitectureCover)((flags >> 3) & 3)
						});
					}
					int anchorCount = reader.ReadByte();
					if (anchorCount > MaxAnchors) throw new InvalidDataException("anchor bound");
					for (int i = 0; i < anchorCount; i++)
					{
						parsed.Anchors.Add(new ArchitectureAnchor
						{
							Key = ReadText(reader, MaxKeyChars),
							X = reader.ReadByte(),
							Y = reader.ReadByte(),
							Access = (ArchitectureAnchorAccess)reader.ReadByte()
						});
					}
					int placementCount = reader.ReadUInt16();
					if (placementCount > MaxPlacements) throw new InvalidDataException("placement bound");
					for (int i = 0; i < placementCount; i++)
					{
						ArchitectureLayer layer = (ArchitectureLayer)reader.ReadByte();
						int x = reader.ReadByte();
						int y = reader.ReadByte();
						int blueprint = reader.ReadByte();
						int anchor = reader.ReadUInt16();
						if (blueprint >= blueprints.Count || (anchor != NoAnchorIndex && anchor >= parsed.Anchors.Count))
							throw new InvalidDataException("placement reference");
						int material = -1;
						int tech = -1;
						bool natural = false;
						bool existing = false;
						int knowledgeIndex = NoKnowledgeIndex;
						int powerIndex = NoPowerIndex;
						if (schema == SnapshotSchema)
						{
							material = reader.ReadByte();
							tech = reader.ReadByte();
							int truthFlags = reader.ReadByte();
							if (material >= materials.Count || tech >= techs.Count || truthFlags > 3)
								throw new InvalidDataException("placement truth reference");
							natural = (truthFlags & 1) != 0;
							existing = (truthFlags & 2) != 0;
							knowledgeIndex = reader.ReadByte();
							if (knowledgeIndex != NoKnowledgeIndex
								&& knowledgeIndex >= knowledge.Count)
								throw new InvalidDataException("placement knowledge reference");
							powerIndex = reader.ReadByte();
							if (powerIndex != NoPowerIndex && powerIndex >= powers.Count)
								throw new InvalidDataException("placement power reference");
						}
						parsed.Placements.Add(new ArchitecturePlacement
						{
							Layer = layer,
							X = x,
							Y = y,
							Blueprint = blueprints[blueprint],
							Slot = SlotFor(layer, x, y),
							Material = schema == SnapshotSchema ? materials[material] : null,
							MinTech = schema == SnapshotSchema ? techs[tech] : null,
							Knowledge = schema == SnapshotSchema && knowledgeIndex != NoKnowledgeIndex
								? knowledge[knowledgeIndex] : null,
							Power = schema == SnapshotSchema && powerIndex != NoPowerIndex
								? powers[powerIndex] : null,
							Natural = natural,
							ExistingAuthority = existing,
							StatefulAnchor = anchor == NoAnchorIndex ? null : parsed.Anchors[anchor].Key
						});
					}
					if (stream.Position != stream.Length) throw new InvalidDataException("trailing bytes");
				}
			}
			catch (Exception exception)
			{
				return Fail("snapshot payload is malformed: " + exception.Message, out Failure);
			}
			if (!TryValidateTopologyCore(parsed, null, schema == LegacySnapshotSchema, out Failure)) return false;
			if (!TryEncodeSnapshotVersion(parsed, schema, out string canonical, out Failure)
				|| canonical != Encoded)
				return Failure != null ? false : Fail("snapshot is not canonical", out Failure);
			Snapshot = parsed;
			return true;
		}

		public static bool TrySnapshotHash(ArchitectureLayoutSnapshot Snapshot,
			out string SnapshotHash, out string Failure)
		{
			SnapshotHash = null;
			if (!TryEncodeSnapshot(Snapshot, out string encoded, out Failure)) return false;
			SnapshotHash = encoded.Substring(encoded.LastIndexOf('|') + 1);
			return true;
		}

		public static bool TryEncodedSnapshotHash(string Encoded,
			out string SnapshotHash, out string Failure)
		{
			SnapshotHash = null;
			ArchitectureLayoutSnapshot ignored;
			if (!TryDecodeSnapshot(Encoded, out ignored, out Failure)) return false;
			SnapshotHash = Encoded.Substring(Encoded.LastIndexOf('|') + 1);
			return true;
		}

		public static bool IsCurrentSnapshotEncoding(string Encoded)
		{
			return Encoded != null && Encoded.StartsWith("a2|", StringComparison.Ordinal);
		}

		// --- Exact tier delta ---------------------------------------------------------------

		/// <summary>
		/// Builds exact scenery work while refusing a moved main or changed old stateful fixture.
		/// Main behavior object is intentionally outside both snapshots and must survive in runtime.
		/// </summary>
		public static bool TryBuildDelta(ArchitectureLayoutSnapshot Before,
			ArchitectureLayoutSnapshot After, out ArchitectureLayoutDelta Delta, out string Failure)
		{
			Delta = null;
			if (!TryValidateTopology(Before, null, out Failure)
				|| !TryValidateTopology(After, null, out Failure)) return false;
			bool heartAccretion = IsAdjacentHeartAccretion(Before, After);
			if (FoldType(Before.LotType) != FoldType(After.LotType)
				|| Before.Facing != After.Facing
				|| (!heartAccretion && Before.LotSize != After.LotSize))
				return Fail("layout delta crosses a typed lot set or changes its pose", out Failure);
			if (!heartAccretion && (Before.MainX != After.MainX || Before.MainY != After.MainY))
				return Fail("layout delta moves the main behavior anchor", out Failure);
			if (heartAccretion)
				return TryBuildHeartAccretionDelta(Before, After, out Delta, out Failure);
			Dictionary<string, ArchitecturePlacement> oldBySlot = PlacementDictionary(Before.Placements);
			Dictionary<string, ArchitecturePlacement> newBySlot = PlacementDictionary(After.Placements);
			Dictionary<string, ArchitecturePlacement> oldState = StatefulDictionary(Before.Placements);
			Dictionary<string, ArchitecturePlacement> newState = StatefulDictionary(After.Placements);
			foreach (KeyValuePair<string, ArchitecturePlacement> pair in oldState)
			{
				if (!newState.TryGetValue(pair.Key, out ArchitecturePlacement next)
					|| !SamePlacement(pair.Value, next))
					return Fail("stateful anchor " + pair.Key + " would move, change, or disappear", out Failure);
			}
			ArchitectureLayoutDelta delta = new ArchitectureLayoutDelta { Before = Before, After = After };
			foreach (KeyValuePair<string, ArchitecturePlacement> pair in oldBySlot)
			{
				if (newBySlot.TryGetValue(pair.Key, out ArchitecturePlacement next)
					&& SamePlacement(pair.Value, next)) delta.Retained.Add(pair.Value);
				else delta.Removed.Add(pair.Value);
			}
			foreach (KeyValuePair<string, ArchitecturePlacement> pair in newBySlot)
			{
				if (!oldBySlot.TryGetValue(pair.Key, out ArchitecturePlacement previous)
					|| !SamePlacement(previous, pair.Value)) delta.Added.Add(pair.Value);
			}
			delta.Retained.Sort(ComparePlacements);
			for (int i = 0; i < delta.Retained.Count; i++)
				delta.RetainedAfter.Add(newBySlot[delta.Retained[i].Slot]);
			delta.Removed.Sort(ComparePlacementsReverse);
			delta.Added.Sort(ComparePlacements);
			Dictionary<string, ArchitectureCellState> oldCells = CoordinateCells(Before.Cells);
			Dictionary<string, ArchitectureCellState> newCells = CoordinateCells(After.Cells);
			HashSet<string> coordinates = new HashSet<string>(oldCells.Keys, StringComparer.Ordinal);
			coordinates.UnionWith(newCells.Keys);
			List<string> orderedCoordinates = new List<string>(coordinates);
			orderedCoordinates.Sort(StringComparer.Ordinal);
			for (int i = 0; i < orderedCoordinates.Count; i++)
			{
				string coordinate = orderedCoordinates[i];
				oldCells.TryGetValue(coordinate, out ArchitectureCellState before);
				newCells.TryGetValue(coordinate, out ArchitectureCellState after);
				if (!SameCell(before, after))
				{
					ArchitectureCellState source = before ?? after;
					delta.Cells.Add(new ArchitectureCellDelta
						{ X = source.X, Y = source.Y, Before = before, After = after });
				}
			}
			Delta = delta;
			return true;
		}

		private static bool IsAdjacentHeartAccretion(ArchitectureLayoutSnapshot Before,
			ArchitectureLayoutSnapshot After)
		{
			if (Before == null || After == null || Before.PlanKey != "civic-heart"
				|| After.PlanKey != "civic-heart" || FoldType(Before.LotType) != "civic"
				|| FoldType(After.LotType) != "civic") return false;
			int beforeRung = KingdomPlotRules.HeartRungOf(Before.BuildKey);
			int afterRung = KingdomPlotRules.HeartRungOf(After.BuildKey);
			return beforeRung > 0 && afterRung == beforeRung + 1
				&& (int)After.LotSize == (int)Before.LotSize + 1;
		}

		private static bool TryBuildHeartAccretionDelta(ArchitectureLayoutSnapshot Before,
			ArchitectureLayoutSnapshot After, out ArchitectureLayoutDelta Delta,
			out string Failure)
		{
			Delta = null;
			ArchitectureLayoutDelta delta = new ArchitectureLayoutDelta
				{ Before = Before, After = After };
			Dictionary<string, ArchitecturePlacement> afterByRelative =
				RelativePlacements(After);
			HashSet<string> retainedAfterSlots = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Before.Placements.Count; i++)
			{
				ArchitecturePlacement oldPlacement = Before.Placements[i];
				ArchitecturePlacement next;
				if (afterByRelative.TryGetValue(RelativePlacementKey(Before, oldPlacement), out next)
					&& SameHeartPlacement(oldPlacement, next))
				{
					delta.Retained.Add(oldPlacement);
					delta.RetainedAfter.Add(next);
					retainedAfterSlots.Add(next.Slot);
				}
				else delta.Removed.Add(oldPlacement);
			}
			for (int i = 0; i < After.Placements.Count; i++)
				if (!retainedAfterSlots.Contains(After.Placements[i].Slot))
					delta.Added.Add(After.Placements[i]);
			if (delta.Removed.Count != 0)
				return Fail("heart accretion would remove or replace existing authored fabric",
					out Failure);
			for (int stateful = 0; stateful < Before.Placements.Count; stateful++)
			{
				ArchitecturePlacement prior = Before.Placements[stateful];
				if (string.IsNullOrEmpty(prior.StatefulAnchor)) continue;
				int retained = delta.Retained.IndexOf(prior);
				if (retained < 0 || retained >= delta.RetainedAfter.Count
					|| AnchorRole(prior.StatefulAnchor)
						!= AnchorRole(delta.RetainedAfter[retained].StatefulAnchor))
					return Fail("heart stateful anchor " + prior.StatefulAnchor
						+ " would move, change, or disappear", out Failure);
			}
			Dictionary<string, ArchitectureCellState> oldCells = RelativeCells(Before);
			Dictionary<string, ArchitectureCellState> newCells = RelativeCells(After);
			// Heart tiers accrete. Every old claimed-cell contract remains exactly where it was
			// relative to the stable behavior root. A later tier may put a stronger authored roof
			// over retained open/soft yard, but may never reopen it or change natural fabric.
			foreach (KeyValuePair<string, ArchitectureCellState> pair in oldCells)
			{
				ArchitectureCellState next;
				if (!newCells.TryGetValue(pair.Key, out next)
					|| !SameHeartCell(Before, pair.Value, After, next))
					return Fail("heart accretion would remove or alter existing authored cell fabric",
						out Failure);
			}
			List<string> ordered = new List<string>(newCells.Keys);
			ordered.Sort(StringComparer.Ordinal);
			for (int i = 0; i < ordered.Count; i++)
			{
				ArchitectureCellState oldCell;
				ArchitectureCellState newCell;
				oldCells.TryGetValue(ordered[i], out oldCell);
				newCells.TryGetValue(ordered[i], out newCell);
				if (oldCell == null)
				{
					delta.Cells.Add(new ArchitectureCellDelta
						{ X = newCell.X, Y = newCell.Y, Before = null, After = newCell });
				}
				else if (oldCell.Cover != newCell.Cover)
				{
					delta.Cells.Add(new ArchitectureCellDelta
						{ X = newCell.X, Y = newCell.Y, Before = oldCell, After = newCell });
				}
			}
			Delta = delta;
			Failure = null;
			return true;
		}

		private static Dictionary<string, ArchitecturePlacement> RelativePlacements(
			ArchitectureLayoutSnapshot Snapshot)
		{
			Dictionary<string, ArchitecturePlacement> result =
				new Dictionary<string, ArchitecturePlacement>(StringComparer.Ordinal);
			for (int i = 0; i < Snapshot.Placements.Count; i++)
				result[RelativePlacementKey(Snapshot, Snapshot.Placements[i])] =
					Snapshot.Placements[i];
			return result;
		}

		private static string RelativePlacementKey(ArchitectureLayoutSnapshot Snapshot,
			ArchitecturePlacement Placement)
		{
			return ((int)Placement.Layer).ToString(CultureInfo.InvariantCulture) + ":"
				+ (Placement.X - Snapshot.MainX).ToString(CultureInfo.InvariantCulture) + ":"
				+ (Placement.Y - Snapshot.MainY).ToString(CultureInfo.InvariantCulture);
		}

		private static Dictionary<string, ArchitectureCellState> RelativeCells(
			ArchitectureLayoutSnapshot Snapshot)
		{
			Dictionary<string, ArchitectureCellState> result =
				new Dictionary<string, ArchitectureCellState>(StringComparer.Ordinal);
			for (int i = 0; i < Snapshot.Cells.Count; i++)
			{
				ArchitectureCellState cell = Snapshot.Cells[i];
				result[(cell.X - Snapshot.MainX).ToString(CultureInfo.InvariantCulture) + ":"
					+ (cell.Y - Snapshot.MainY).ToString(CultureInfo.InvariantCulture)] = cell;
			}
			return result;
		}

		private static bool SameHeartPlacement(ArchitecturePlacement A,
			ArchitecturePlacement B)
		{
			return A != null && B != null && A.Layer == B.Layer && A.Blueprint == B.Blueprint
				&& A.Material == B.Material && A.MinTech == B.MinTech
				&& A.Knowledge == B.Knowledge && A.Power == B.Power
				&& A.Natural == B.Natural && A.ExistingAuthority == B.ExistingAuthority
				&& AnchorRole(A.StatefulAnchor) == AnchorRole(B.StatefulAnchor);
		}

		private static bool SameHeartCell(ArchitectureLayoutSnapshot ALayout,
			ArchitectureCellState A, ArchitectureLayoutSnapshot BLayout, ArchitectureCellState B)
		{
			if (A == null || B == null) return A == B;
			return A.X - ALayout.MainX == B.X - BLayout.MainX
				&& A.Y - ALayout.MainY == B.Y - BLayout.MainY
				&& A.Claim == B.Claim && A.Passability == B.Passability
				&& PermittedHeartCoverTransition(A.Cover, B.Cover);
		}

		private static bool PermittedHeartCoverTransition(ArchitectureCover Before,
			ArchitectureCover After)
		{
			if (Before == After) return true;
			return (Before == ArchitectureCover.Open
				&& (After == ArchitectureCover.Soft || After == ArchitectureCover.Walled))
				|| (Before == ArchitectureCover.Soft && After == ArchitectureCover.Walled);
		}

		// --- Time x labour x infrastructure ------------------------------------------------

		public static ArchitectureLabourProgress AdvanceLabour(long LastTick, long TimeTick,
			long RemainingTicks, int LabourPercent, int InfrastructurePercent)
		{
			ArchitectureLabourProgress result = new ArchitectureLabourProgress
			{
				PreviousTick = LastTick,
				NextTick = LastTick,
				RemainingTicks = RemainingTicks > 0 ? RemainingTicks : 0,
				WorkedTicks = 0,
				CompletionTick = RemainingTicks <= 0 ? LastTick : 0,
				Complete = RemainingTicks <= 0
			};
			if (result.Complete || TimeTick <= LastTick) return result;
			result.NextTick = TimeTick; // idle time is spent, never banked.
			int labour = ClampPercent(LabourPercent);
			int infrastructure = ClampPercent(InfrastructurePercent);
			int factor = labour * infrastructure;
			long elapsed = TimeTick - LastTick;
			long available = ScaleByTenThousand(elapsed, factor);
			if (available <= 0) return result;
			long worked = available < result.RemainingTicks ? available : result.RemainingTicks;
			result.WorkedTicks = worked;
			result.RemainingTicks -= worked;
			if (result.RemainingTicks > 0) return result;
			result.Complete = true;
			long low = 1;
			long high = elapsed;
			while (low < high)
			{
				long middle = low + (high - low) / 2;
				if (ScaleByTenThousand(middle, factor) >= worked) high = middle;
				else low = middle + 1;
			}
			result.CompletionTick = LastTick + low;
			return result;
		}

		// --- Private compiler helpers -------------------------------------------------------

		private static bool TryPalette(ArchitecturePaletteDraft Palette,
			out Dictionary<string, ArchitecturePaletteSlot> Slots, out string Failure)
		{
			Slots = null;
			Failure = null;
			if (Palette == null || !ValidKey(Palette.Key) || Palette.Slots == null
				|| Palette.Slots.Count == 0 || Palette.Slots.Count > MaxPaletteSlots)
				return Fail("palette is absent, empty, or over the bound", out Failure);
			Dictionary<string, ArchitecturePaletteSlot> slots =
				new Dictionary<string, ArchitecturePaletteSlot>(StringComparer.Ordinal);
			for (int i = 0; i < Palette.Slots.Count; i++)
			{
				ArchitecturePaletteSlot slot = Palette.Slots[i];
				KingdomMaterial material;
				int tech;
				if (slot == null || !ValidKey(slot.Key) || !ValidBlueprint(slot.Blueprint)
					|| slot.Blueprint[0] == '$' || !ValidOptionalKey(slot.Role)
					|| !KingdomMaterialRules.TryParseMaterial(slot.Material, out material)
					|| !TryParseTech(slot.MinTech, out tech) || !ValidOptionalKey(slot.Knowledge)
					|| !ValidOptionalKey(slot.Power)
					|| slots.ContainsKey(slot.Key))
					return Fail("palette has a malformed or duplicate slot", out Failure);
				slots.Add(slot.Key, slot);
			}
			Slots = slots;
			return true;
		}

		private static bool TryValidateGlyph(ArchitectureGlyphDraft Glyph,
			Dictionary<string, ArchitecturePaletteSlot> Slots, out string Failure)
		{
			Failure = null;
			HashSet<string> anchors = new HashSet<string>(StringComparer.Ordinal);
			int statefulAnchors = 0;
			for (int i = 0; i < Glyph.Anchors.Count; i++)
			{
				string anchor = Glyph.Anchors[i];
				if (!ValidKey(anchor) || !anchors.Add(anchor))
					return Fail("glyph has a malformed or duplicate anchor", out Failure);
				if (anchor != "main" && !anchor.StartsWith("entrance:", StringComparison.Ordinal))
					statefulAnchors++;
			}
			if (!ValidGlyphToken(Glyph.Ground, ArchitectureLayer.Ground, Slots)
				|| !ValidGlyphToken(Glyph.Structure, ArchitectureLayer.Structure, Slots)
				|| !ValidGlyphToken(Glyph.Object, ArchitectureLayer.Object, Slots))
				return Fail("glyph has a malformed or unresolved placement token", out Failure);
			if (Glyph.Object == "$building" && !Glyph.StatefulObject)
				return Fail("$building must be declared stateful", out Failure);
			if (!string.IsNullOrEmpty(Glyph.Object) && Glyph.Object != "$building"
				&& statefulAnchors > 0 && !Glyph.StatefulObject)
				return Fail("functional object anchor must belong to a stateful fixture", out Failure);
			if (Glyph.StatefulObject && (string.IsNullOrEmpty(Glyph.Object)
				|| (Glyph.Object != "$building" && statefulAnchors != 1)))
				return Fail("stateful fixture must own exactly one stable functional anchor", out Failure);
			return true;
		}

		private static bool ValidGlyphToken(string Token, ArchitectureLayer Layer,
			Dictionary<string, ArchitecturePaletteSlot> Slots)
		{
			if (string.IsNullOrEmpty(Token)) return true;
			if (Token == "$building") return Layer == ArchitectureLayer.Object;
			// Scenery must resolve through a palette slot so its durable receipt can freeze
			// material, minimum-tech, and natural truth.
			if (Token[0] != '$') return false;
			string slot = Token.Substring(1);
			return ValidKey(slot) && Slots.ContainsKey(slot);
		}

		private static bool HasSceneryToken(string Token)
		{
			return !string.IsNullOrEmpty(Token) && Token != "$building";
		}

		private static bool TryAddPlacement(ArchitectureLayoutSnapshot Snapshot,
			ArchitectureLayer Layer, int X, int Y, string Token, bool Stateful,
			IList<ArchitectureAnchor> CellAnchors,
			Dictionary<string, ArchitecturePaletteSlot> Slots,
			ref bool HasBuilding, out string Failure)
		{
			Failure = null;
			if (string.IsNullOrEmpty(Token))
			{
				if (Stateful) return Fail("stateful object glyph has no object", out Failure);
				return true;
			}
			if (Token == "$building")
			{
				if (Layer != ArchitectureLayer.Object || HasBuilding)
					return Fail("$building is only valid once on the object layer", out Failure);
				// Root behavior is owned by commission/upgrade runtime. Recording it as removable
				// scenery would let an authored delta destroy its ID, inventory, parts, and save state.
				HasBuilding = true;
				return true;
			}
			if (Token[0] != '$')
				return Fail("map scenery must reference a palette slot", out Failure);
			string key = Token.Substring(1);
			ArchitecturePaletteSlot slot;
			if (!ValidKey(key) || !Slots.TryGetValue(key, out slot))
				return Fail("map references an unknown palette slot", out Failure);
			string blueprint = slot.Blueprint;
			if (!ValidBlueprint(blueprint)) return Fail("map placement blueprint is malformed", out Failure);
			KingdomMaterial material;
			int tech;
			if (!KingdomMaterialRules.TryParseMaterial(slot.Material, out material)
				|| !TryParseTech(slot.MinTech, out tech))
				return Fail("map placement palette truth is malformed", out Failure);
			string statefulAnchor = null;
			if (Stateful)
			{
				for (int i = 0; i < CellAnchors.Count; i++)
				{
					string anchorKey = CellAnchors[i].Key;
					if (anchorKey == "main"
						|| anchorKey.StartsWith("entrance:", StringComparison.Ordinal)) continue;
					if (statefulAnchor != null)
						return Fail("stateful fixture must own exactly one stable functional anchor", out Failure);
					statefulAnchor = anchorKey;
				}
				if (statefulAnchor == null)
					return Fail("stateful fixture has no stable functional anchor", out Failure);
			}
			Snapshot.Placements.Add(new ArchitecturePlacement
			{
				Layer = Layer,
				X = X,
				Y = Y,
				Blueprint = blueprint,
				Slot = SlotFor(Layer, X, Y),
				Material = KingdomMaterialRules.MaterialKey(material),
				MinTech = KingdomZoningRules.TechLevelNames[tech],
				Knowledge = slot.Knowledge,
				Power = slot.Power,
				Natural = slot.Natural,
				ExistingAuthority = blueprint == "r_KingdomFirstBasin",
				StatefulAnchor = statefulAnchor
			});
			if (Snapshot.Placements.Count > MaxPlacements)
				return Fail("map placements exceed the bound", out Failure);
			return true;
		}

		private static bool TryValidateSnapshotShape(ArchitectureLayoutSnapshot Snapshot,
			bool AllowLegacyPlacementTruth, out string Failure)
		{
			Failure = null;
			if (Snapshot == null || !ValidKey(Snapshot.PlanKey) || !ValidKey(Snapshot.BindingKey)
				|| !ValidKey(Snapshot.BuildKey) || !ValidKey(Snapshot.TierKey)
				|| !ValidKey(Snapshot.VariantKey) || !ValidKey(Snapshot.PaletteKey)
				|| FoldType(Snapshot.LotType) == null || !KnownLotSize(Snapshot.LotSize)
				|| !KnownFacing(Snapshot.Facing))
				return Fail("snapshot metadata is malformed", out Failure);
			if (!TryCanonicalDimensions(Snapshot.LotSize, out int lotWidth, out int lotHeight)
				|| Snapshot.Width != lotWidth || Snapshot.Height != lotHeight
				|| (long)Snapshot.Width * Snapshot.Height > MaxMapArea
				|| Snapshot.MainX < 0 || Snapshot.MainX >= Snapshot.Width
				|| Snapshot.MainY < 0 || Snapshot.MainY >= Snapshot.Height)
				return Fail("snapshot dimensions or main coordinate are invalid", out Failure);
			if (Snapshot.Cells == null || Snapshot.Cells.Count != Snapshot.Width * Snapshot.Height
				|| Snapshot.Placements == null || Snapshot.Placements.Count > MaxPlacements
				|| Snapshot.Anchors == null || Snapshot.Anchors.Count == 0
				|| Snapshot.Anchors.Count > MaxAnchors)
				return Fail("snapshot collections are absent, incomplete, or over bounds", out Failure);
			HashSet<int> cells = new HashSet<int>();
			for (int i = 0; i < Snapshot.Cells.Count; i++)
			{
				ArchitectureCellState cell = Snapshot.Cells[i];
				if (cell == null || cell.X < 0 || cell.X >= Snapshot.Width
					|| cell.Y < 0 || cell.Y >= Snapshot.Height
					|| !KnownPassability(cell.Passability) || !KnownCover(cell.Cover)
					|| !cells.Add(CellKey(cell.X, cell.Y, Snapshot.Width)))
					return Fail("snapshot has a malformed or duplicate cell", out Failure);
			}
			HashSet<string> anchorKeys = new HashSet<string>(StringComparer.Ordinal);
			Dictionary<string, ArchitectureAnchor> anchors = new Dictionary<string, ArchitectureAnchor>(StringComparer.Ordinal);
			int main = 0;
			for (int i = 0; i < Snapshot.Anchors.Count; i++)
			{
				ArchitectureAnchor anchor = Snapshot.Anchors[i];
				if (anchor == null || !ValidKey(anchor.Key) || !anchorKeys.Add(anchor.Key)
					|| anchor.X < 0 || anchor.X >= Snapshot.Width || anchor.Y < 0 || anchor.Y >= Snapshot.Height
					|| !KnownAccess(anchor.Access))
					return Fail("snapshot has a malformed or duplicate anchor", out Failure);
				anchors[anchor.Key] = anchor;
				if (anchor.Key == "main")
				{
					main++;
					if (anchor.X != Snapshot.MainX || anchor.Y != Snapshot.MainY)
						return Fail("snapshot main metadata and anchor disagree", out Failure);
				}
			}
			if (main != 1) return Fail("snapshot must have exactly one main anchor", out Failure);
			HashSet<string> slots = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> stateful = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> blueprints = new HashSet<string>(StringComparer.Ordinal);
			Dictionary<int, ArchitectureCellState> placementCells = CellDictionary(
				Snapshot.Cells, Snapshot.Width);
			for (int i = 0; i < Snapshot.Placements.Count; i++)
			{
				ArchitecturePlacement placement = Snapshot.Placements[i];
				KingdomMaterial material;
				int tech;
				bool legacyTruth = AllowLegacyPlacementTruth
					&& string.IsNullOrEmpty(placement == null ? null : placement.Material)
					&& string.IsNullOrEmpty(placement == null ? null : placement.MinTech);
				if (placement == null || !KnownLayer(placement.Layer)
					|| placement.X < 0 || placement.X >= Snapshot.Width
					|| placement.Y < 0 || placement.Y >= Snapshot.Height
					|| !ValidBlueprint(placement.Blueprint)
					|| (!legacyTruth && (!KingdomMaterialRules.TryParseMaterial(
						placement.Material, out material) || KingdomMaterialRules.MaterialKey(material)
						!= placement.Material || !TryParseTech(placement.MinTech, out tech)
						|| KingdomZoningRules.TechLevelNames[tech] != placement.MinTech
						|| !ValidOptionalKey(placement.Knowledge)
						|| !ValidOptionalKey(placement.Power)
						|| placement.ExistingAuthority
							!= (placement.Blueprint == "r_KingdomFirstBasin")
						|| (placement.ExistingAuthority && placement.Natural)))
					|| placement.Slot != SlotFor(placement.Layer, placement.X, placement.Y)
					|| !placementCells[CellKey(placement.X, placement.Y, Snapshot.Width)].Claim
					|| !slots.Add(placement.Slot))
					return Fail("snapshot has a malformed or duplicate placement", out Failure);
				blueprints.Add(placement.Blueprint);
				if (!string.IsNullOrEmpty(placement.StatefulAnchor))
				{
					if (!anchors.TryGetValue(placement.StatefulAnchor, out ArchitectureAnchor anchor)
						|| anchor.X != placement.X || anchor.Y != placement.Y
						|| !stateful.Add(placement.StatefulAnchor))
						return Fail("stateful placement anchor is missing, moved, or duplicated", out Failure);
				}
			}
			if (blueprints.Count > MaxPaletteSlots)
				return Fail("snapshot blueprint table exceeds the bound", out Failure);
			return true;
		}

		private static bool ValidSelector(ArchitectureSelector Selector, out string Failure)
		{
			Failure = null;
			if (Selector == null) return true;
			if (!ValidTagExpression(Selector.Styles) || !ValidTagExpression(Selector.Creeds)
				|| !ValidTagExpression(Selector.Cultures) || !ValidTagExpression(Selector.Species)
				|| !ValidTagExpression(Selector.Genotypes) || !ValidTagExpression(Selector.Bodies)
				|| !ValidTagExpression(Selector.Terrains) || !ValidTagExpression(Selector.Strata))
				return Fail("selector tag expression is malformed", out Failure);
			if (Selector.MinimumStage < -1 || Selector.MaximumStage < -1
				|| Selector.MinimumTech < -1 || Selector.MaximumTech < -1
				|| (Selector.MinimumStage >= 0 && Selector.MaximumStage >= 0
					&& Selector.MinimumStage > Selector.MaximumStage)
				|| (Selector.MinimumTech >= 0 && Selector.MaximumTech >= 0
					&& Selector.MinimumTech > Selector.MaximumTech))
				return Fail("selector numeric range is malformed", out Failure);
			return true;
		}

		private static bool SelectorMatches(ArchitectureSelector Selector,
			ArchitectureSelectionContext Context)
		{
			if (Selector == null) return true;
			return TagAccepts(Selector.Styles, Context.Style)
				&& TagAccepts(Selector.Creeds, Context.Creed)
				&& TagSetAccepts(Selector.Cultures, Context.Cultures)
				&& TagSetAccepts(Selector.Species, Context.Species)
				&& TagSetAccepts(Selector.Genotypes, Context.Genotypes)
				&& TagSetAccepts(Selector.Bodies, Context.Bodies)
				&& TagAccepts(Selector.Terrains, Context.Terrain)
				&& TagAccepts(Selector.Strata, Context.Stratum)
				&& (Selector.MinimumStage < 0 || Context.Stage >= Selector.MinimumStage)
				&& (Selector.MaximumStage < 0 || Context.Stage <= Selector.MaximumStage)
				&& (Selector.MinimumTech < 0 || Context.Tech >= Selector.MinimumTech)
				&& (Selector.MaximumTech < 0 || Context.Tech <= Selector.MaximumTech);
		}

		private static int SelectorSpecificity(ArchitectureSelector Selector)
		{
			if (Selector == null) return 0;
			int result = 0;
			if (ConditionalExpression(Selector.Styles)) result++;
			if (ConditionalExpression(Selector.Creeds)) result++;
			if (ConditionalExpression(Selector.Cultures)) result++;
			if (ConditionalExpression(Selector.Species)) result++;
			if (ConditionalExpression(Selector.Genotypes)) result++;
			if (ConditionalExpression(Selector.Bodies)) result++;
			if (ConditionalExpression(Selector.Terrains)) result++;
			if (ConditionalExpression(Selector.Strata)) result++;
			if (Selector.MinimumStage >= 0) result++;
			if (Selector.MaximumStage >= 0) result++;
			if (Selector.MinimumTech >= 0) result++;
			if (Selector.MaximumTech >= 0) result++;
			return result;
		}

		private static bool Unconditional(ArchitectureSelector Selector)
		{
			return Selector == null || (!ConditionalExpression(Selector.Styles)
				&& !ConditionalExpression(Selector.Creeds)
				&& !ConditionalExpression(Selector.Cultures)
				&& !ConditionalExpression(Selector.Species)
				&& !ConditionalExpression(Selector.Genotypes)
				&& !ConditionalExpression(Selector.Bodies)
				&& !ConditionalExpression(Selector.Terrains)
				&& !ConditionalExpression(Selector.Strata)
				&& Selector.MinimumStage < 0 && Selector.MaximumStage < 0
				&& Selector.MinimumTech < 0 && Selector.MaximumTech < 0);
		}

		private static bool ConditionalExpression(string Expression)
		{
			return !string.IsNullOrWhiteSpace(Expression)
				&& !string.Equals(Expression.Trim(), "all", StringComparison.OrdinalIgnoreCase);
		}

		private static bool TagAccepts(string Expression, string Value)
		{
			if (string.IsNullOrWhiteSpace(Expression)) return true;
			string value = (Value ?? "").Trim();
			bool hasPositive = false;
			bool positiveMatch = false;
			string[] tokens = Expression.Split(',');
			for (int i = 0; i < tokens.Length; i++)
			{
				string token = tokens[i].Trim();
				bool negative = token.Length > 1 && token[0] == '!';
				string name = negative ? token.Substring(1) : token;
				if (negative)
				{
					if (string.Equals(name, value, StringComparison.OrdinalIgnoreCase)) return false;
				}
				else
				{
					hasPositive = true;
					if (name == "*" || string.Equals(name, "all", StringComparison.OrdinalIgnoreCase)
						|| string.Equals(name, value, StringComparison.OrdinalIgnoreCase)) positiveMatch = true;
				}
			}
			return !hasPositive || positiveMatch;
		}

		/// <summary>Set-valued identity selector. Any explicitly excluded live fact refuses the
		/// variant; otherwise one positive fact must match when positives are named. A pure exclusion
		/// matches a city carrying none of its refused facts. Empty/all preserve existing wildcard
		/// semantics. Caller supplies canonical bounded facts, but comparison remains case-insensitive
		/// because Qud identity vocabularies preserve display case.</summary>
		private static bool TagSetAccepts(string Expression, IList<string> Values)
		{
			if (string.IsNullOrWhiteSpace(Expression)) return true;
			bool hasPositive = false;
			bool positiveMatch = false;
			string[] tokens = Expression.Split(',');
			for (int i = 0; i < tokens.Length; i++)
			{
				string token = tokens[i].Trim();
				bool negative = token.Length > 1 && token[0] == '!';
				string name = negative ? token.Substring(1) : token;
				if (negative)
				{
					for (int j = 0; Values != null && j < Values.Count; j++)
						if (string.Equals(name, Values[j], StringComparison.OrdinalIgnoreCase))
							return false;
				}
				else
				{
					hasPositive = true;
					if (name == "*" || string.Equals(name, "all", StringComparison.OrdinalIgnoreCase))
					{
						positiveMatch = true;
						continue;
					}
					for (int j = 0; Values != null && j < Values.Count; j++)
						if (string.Equals(name, Values[j], StringComparison.OrdinalIgnoreCase))
						{
							positiveMatch = true;
							break;
						}
				}
			}
			return !hasPositive || positiveMatch;
		}

		private static bool ValidTagExpression(string Expression)
		{
			if (string.IsNullOrEmpty(Expression)) return true;
			if (Expression.Length > MaxSelectorChars) return false;
			string[] tokens = Expression.Split(',');
			if (tokens.Length > MaxSelectorTokens) return false;
			for (int i = 0; i < tokens.Length; i++)
			{
				string token = tokens[i].Trim();
				if (token.Length == 0 || token.Length > 64 || token == "!" || HasControl(token)) return false;
			}
			return true;
		}

		private static string FoldType(string Value)
		{
			if (string.IsNullOrWhiteSpace(Value)) return null;
			string folded = Value.Trim().ToLowerInvariant();
			return ValidKey(folded) ? folded : null;
		}

		private static bool ValidKey(string Value)
		{
			return !string.IsNullOrEmpty(Value) && Value.Length <= MaxKeyChars
				&& Value == Value.Trim() && !HasControl(Value);
		}

		private static bool ValidOptionalKey(string Value)
		{
			return string.IsNullOrEmpty(Value) || ValidKey(Value);
		}

		public static bool TryParseTech(string Value, out int Tech)
		{
			Tech = -1;
			if (string.IsNullOrEmpty(Value) || Value != Value.Trim()) return false;
			for (int i = 0; i < KingdomZoningRules.TechLevelNames.Length; i++)
			{
				if (Value == KingdomZoningRules.TechLevelNames[i])
				{
					Tech = i;
					return true;
				}
			}
			return false;
		}

		private static bool ValidBlueprint(string Value)
		{
			return !string.IsNullOrEmpty(Value) && Value.Length <= MaxBlueprintChars
				&& Value == Value.Trim() && !HasControl(Value);
		}

		private static bool HasControl(string Value)
		{
			for (int i = 0; i < Value.Length; i++) if (char.IsControl(Value[i])) return true;
			return false;
		}

		private static bool KnownLotSize(ArchitectureLotSize Value)
		{
			return Value >= ArchitectureLotSize.Small && Value <= ArchitectureLotSize.Huge;
		}

		private static bool KnownFacing(ArchitectureFacing Value)
		{
			return Value >= ArchitectureFacing.North && Value <= ArchitectureFacing.West;
		}

		private static bool KnownFrontage(ArchitectureFrontage Value)
		{
			return Value == ArchitectureFrontage.Heart || Value == ArchitectureFrontage.Road;
		}

		private static bool KnownLayer(ArchitectureLayer Value)
		{
			return Value >= ArchitectureLayer.Ground && Value <= ArchitectureLayer.Object;
		}

		private static bool KnownPassability(ArchitecturePassability Value)
		{
			return Value >= ArchitecturePassability.Walkable && Value <= ArchitecturePassability.Adjacent;
		}

		private static bool KnownCover(ArchitectureCover Value)
		{
			return Value >= ArchitectureCover.Open && Value <= ArchitectureCover.Natural;
		}

		private static bool KnownAccess(ArchitectureAnchorAccess Value)
		{
			return Value == ArchitectureAnchorAccess.OnCell || Value == ArchitectureAnchorAccess.Adjacent;
		}

		private static string SlotFor(ArchitectureLayer Layer, int X, int Y)
		{
			char prefix = Layer == ArchitectureLayer.Ground ? 'g'
				: (Layer == ArchitectureLayer.Structure ? 's' : 'o');
			return prefix + ":" + X.ToString("D2", CultureInfo.InvariantCulture)
				+ ":" + Y.ToString("D2", CultureInfo.InvariantCulture);
		}

		private static int CellKey(int X, int Y, int Width)
		{
			return Y * Width + X;
		}

		private static Dictionary<int, ArchitectureCellState> CellDictionary(
			IList<ArchitectureCellState> Cells, int Width)
		{
			Dictionary<int, ArchitectureCellState> result = new Dictionary<int, ArchitectureCellState>();
			for (int i = 0; i < Cells.Count; i++)
				result[CellKey(Cells[i].X, Cells[i].Y, Width)] = Cells[i];
			return result;
		}

		private static bool ClaimBoundary(Dictionary<int, ArchitectureCellState> Cells,
			int Width, int Height, int X, int Y)
		{
			int[] dx = new int[4] { 0, 1, 0, -1 };
			int[] dy = new int[4] { -1, 0, 1, 0 };
			for (int i = 0; i < 4; i++)
			{
				int x = X + dx[i];
				int y = Y + dy[i];
				if (x < 0 || x >= Width || y < 0 || y >= Height) return true;
				if (!Cells[CellKey(x, y, Width)].Claim) return true;
			}
			return false;
		}

		private static bool AdjacentReached(int X, int Y, int Width, int Height,
			HashSet<int> Reached)
		{
			int[] dx = new int[4] { 0, 1, 0, -1 };
			int[] dy = new int[4] { -1, 0, 1, 0 };
			for (int i = 0; i < 4; i++)
			{
				int x = X + dx[i];
				int y = Y + dy[i];
				if (x >= 0 && x < Width && y >= 0 && y < Height
					&& Reached.Contains(CellKey(x, y, Width))) return true;
			}
			return false;
		}

		private static bool AnchorMatchesRole(string Key, string Role)
		{
			string keyRole = AnchorRole(Key);
			if (Role.IndexOf(':') >= 0) return keyRole == Role;
			int separator = keyRole.IndexOf(':');
			return separator < 0 ? keyRole == Role : keyRole.Substring(0, separator) == Role;
		}

		private static string AnchorRole(string Key)
		{
			int identity = Key == null ? -1 : Key.LastIndexOf('@');
			return identity < 0 ? Key : Key.Substring(0, identity);
		}

		private static string StableAnchorKey(string Role, int X, int Y)
		{
			return Role + "@" + X.ToString(CultureInfo.InvariantCulture) + ","
				+ Y.ToString(CultureInfo.InvariantCulture);
		}

		private static bool ContainsAnchor(IList<ArchitectureAnchor> Anchors, string Key)
		{
			for (int i = 0; i < Anchors.Count; i++) if (Anchors[i].Key == Key) return true;
			return false;
		}

		private static void SortSnapshot(ArchitectureLayoutSnapshot Snapshot)
		{
			Snapshot.Cells.Sort(CompareCells);
			Snapshot.Placements.Sort(ComparePlacements);
			Snapshot.Anchors.Sort(delegate(ArchitectureAnchor A, ArchitectureAnchor B)
			{
				return string.CompareOrdinal(A.Key, B.Key);
			});
		}

		private static int CompareCells(ArchitectureCellState A, ArchitectureCellState B)
		{
			int compare = A.Y.CompareTo(B.Y);
			return compare != 0 ? compare : A.X.CompareTo(B.X);
		}

		private static int ComparePlacements(ArchitecturePlacement A, ArchitecturePlacement B)
		{
			int compare = ((int)A.Layer).CompareTo((int)B.Layer);
			if (compare != 0) return compare;
			compare = A.Y.CompareTo(B.Y);
			return compare != 0 ? compare : A.X.CompareTo(B.X);
		}

		private static int ComparePlacementsReverse(ArchitecturePlacement A, ArchitecturePlacement B)
		{
			return -ComparePlacements(A, B);
		}

		private static List<string> BlueprintTable(IList<ArchitecturePlacement> Placements)
		{
			HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Placements.Count; i++) seen.Add(Placements[i].Blueprint);
			List<string> result = new List<string>(seen);
			result.Sort(StringComparer.Ordinal);
			return result;
		}

		private static List<string> PlacementTextTable(
			IList<ArchitecturePlacement> Placements, int Field)
		{
			HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Placements.Count; i++)
			{
				string value = Field == 0 ? Placements[i].Material
					: (Field == 1 ? Placements[i].MinTech
						: (Field == 2 ? Placements[i].Knowledge : Placements[i].Power));
				if (!string.IsNullOrEmpty(value)) seen.Add(value);
			}
			List<string> result = new List<string>(seen);
			result.Sort(StringComparer.Ordinal);
			return result;
		}

		private static bool LegacyPlacementTruthOnly(ArchitectureLayoutSnapshot Snapshot)
		{
			if (Snapshot == null || Snapshot.Placements == null) return false;
			for (int i = 0; i < Snapshot.Placements.Count; i++)
			{
				ArchitecturePlacement placement = Snapshot.Placements[i];
				if (placement == null || !string.IsNullOrEmpty(placement.Material)
					|| !string.IsNullOrEmpty(placement.MinTech)
					|| !string.IsNullOrEmpty(placement.Knowledge) || placement.Natural
					|| !string.IsNullOrEmpty(placement.Power)
					|| placement.ExistingAuthority) return false;
			}
			return true;
		}

		private static void WriteText(BinaryWriter Writer, string Text, int MaximumChars)
		{
			if (Text == null || Text.Length > MaximumChars) throw new InvalidDataException("text bound");
			byte[] bytes = StrictUtf8.GetBytes(Text);
			if (bytes.Length > ushort.MaxValue) throw new InvalidDataException("text byte bound");
			Writer.Write((ushort)bytes.Length);
			Writer.Write(bytes);
		}

		private static string ReadText(BinaryReader Reader, int MaximumChars)
		{
			int length = Reader.ReadUInt16();
			if (length > MaximumChars * 4) throw new InvalidDataException("text byte bound");
			byte[] bytes = Reader.ReadBytes(length);
			if (bytes.Length != length) throw new EndOfStreamException();
			string result = StrictUtf8.GetString(bytes);
			if (result.Length > MaximumChars || StrictUtf8.GetByteCount(result) != length)
				throw new InvalidDataException("text character bound");
			return result;
		}

		private static string Hash(byte[] Payload)
		{
			byte[] digest;
			using (SHA256 sha = SHA256.Create()) digest = sha.ComputeHash(Payload);
			StringBuilder result = new StringBuilder(64);
			for (int i = 0; i < digest.Length; i++)
				result.Append(digest[i].ToString("x2", CultureInfo.InvariantCulture));
			return result.ToString();
		}

		private static bool CanonicalHash(string Value)
		{
			if (Value == null || Value.Length != 64) return false;
			for (int i = 0; i < Value.Length; i++)
				if (!((Value[i] >= '0' && Value[i] <= '9') || (Value[i] >= 'a' && Value[i] <= 'f')))
					return false;
			return true;
		}

		private static Dictionary<string, ArchitecturePlacement> PlacementDictionary(
			IList<ArchitecturePlacement> Placements)
		{
			Dictionary<string, ArchitecturePlacement> result =
				new Dictionary<string, ArchitecturePlacement>(StringComparer.Ordinal);
			for (int i = 0; i < Placements.Count; i++) result[Placements[i].Slot] = Placements[i];
			return result;
		}

		private static Dictionary<string, ArchitecturePlacement> StatefulDictionary(
			IList<ArchitecturePlacement> Placements)
		{
			Dictionary<string, ArchitecturePlacement> result =
				new Dictionary<string, ArchitecturePlacement>(StringComparer.Ordinal);
			for (int i = 0; i < Placements.Count; i++)
				if (!string.IsNullOrEmpty(Placements[i].StatefulAnchor))
					result[Placements[i].StatefulAnchor] = Placements[i];
			return result;
		}

		private static Dictionary<string, ArchitectureCellState> CoordinateCells(
			IList<ArchitectureCellState> Cells)
		{
			Dictionary<string, ArchitectureCellState> result =
				new Dictionary<string, ArchitectureCellState>(StringComparer.Ordinal);
			for (int i = 0; i < Cells.Count; i++)
				result[CoordinateKey(Cells[i].X, Cells[i].Y)] = Cells[i];
			return result;
		}

		private static string CoordinateKey(int X, int Y)
		{
			return X.ToString("D2", CultureInfo.InvariantCulture) + ":"
				+ Y.ToString("D2", CultureInfo.InvariantCulture);
		}

		private static bool SamePlacement(ArchitecturePlacement A, ArchitecturePlacement B)
		{
			return A != null && B != null && A.Layer == B.Layer && A.X == B.X && A.Y == B.Y
				&& A.Slot == B.Slot && A.Blueprint == B.Blueprint
				&& A.Material == B.Material && A.MinTech == B.MinTech
				&& A.Knowledge == B.Knowledge
				&& A.Power == B.Power
				&& A.Natural == B.Natural && A.ExistingAuthority == B.ExistingAuthority
				&& A.StatefulAnchor == B.StatefulAnchor;
		}

		private static bool SameCell(ArchitectureCellState A, ArchitectureCellState B)
		{
			if (A == null || B == null) return A == B;
			return A.X == B.X && A.Y == B.Y && A.Claim == B.Claim
				&& A.Passability == B.Passability && A.Cover == B.Cover;
		}

		private static int ClampPercent(int Value)
		{
			if (Value <= 0) return 0;
			return Value >= 100 ? 100 : Value;
		}

		private static long ScaleByTenThousand(long Ticks, int Factor)
		{
			if (Ticks <= 0 || Factor <= 0) return 0;
			if (Factor >= 10000) return Ticks;
			return Ticks / 10000L * Factor + Ticks % 10000L * Factor / 10000L;
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}
	}
}
