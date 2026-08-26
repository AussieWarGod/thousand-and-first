using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// Engine-free authored architecture laws. The engine-facing loader may parse XML into the
	/// draft records above; only this class is allowed to turn those drafts into a durable map.
	/// </summary>
	public static partial class KingdomArchitectureRules
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

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}
	}
}
