using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Engine-free law for a building designation and the physical providers inside it.
	/// A catalogue amount is a ceiling, never a source. A provider contributes only after the
	/// runtime proves one unambiguous building owns its current physical cell.</summary>
	public static class KingdomBenefitEmbodimentRules
	{
		public const int MaxProvidersPerZone = 4096;
		public const int MaxProviderPartsPerObject = 16;
		public const int MaxObservationLimitRowsPerZone = 1;

		public static int EffectiveAmount(int DesignatedCap, int PhysicalAmount)
		{
			if (DesignatedCap <= 0 || PhysicalAmount <= 0) return 0;
			return PhysicalAmount < DesignatedCap ? PhysicalAmount : DesignatedCap;
		}

		/// <summary>Scales one proved physical structure by its current operational percent.
		/// Zero crew, a stopped work, or an invalid physical amount supplies nothing. Uses a
		/// widened product so hostile extension values cannot wrap into positive supply.</summary>
		public static int OperationalStructureAmount(int PhysicalAmount,
			int EffectivenessPercent)
		{
			if (PhysicalAmount <= 0 || EffectivenessPercent <= 0) return 0;
			if (EffectivenessPercent >= 100) return PhysicalAmount;
			return (int)((long)PhysicalAmount * EffectivenessPercent / 100L);
		}

		/// <summary>Spatial/custody decision shared by authored and player-placed furniture.
		/// Provenance neither grants nor denies service: authored, player-placed, and modded
		/// furniture at the same cell are equivalent. Overlap fails closed.</summary>
		public static bool ProviderBelongs(bool PhysicalRoot, int ContainingDesignations)
		{
			return PhysicalRoot && ContainingDesignations == 1;
		}

		/// <summary>Authored Interior includes covered walk and furniture-adjacent cells, never walls.</summary>
		public static bool AuthoredInterior(bool Building, bool Covered, bool Blocked)
		{
			return Building && Covered && !Blocked;
		}

		/// <summary>Counts the blocked/natural physical shell without consuming Interior cells.</summary>
		public static int StructuralShellCells(IReadOnlyList<KingdomBenefitCell> Cells)
		{
			int physical = 0;
			for (int i = 0; Cells != null && i < Cells.Count; i++)
			{
				KingdomBenefitCell cell = Cells[i];
				if ((cell.Use & KingdomBenefitCellUse.Building) == 0
					|| (cell.Use & KingdomBenefitCellUse.Interior) != 0) continue;
				if (cell.Cover == KingdomBenefitCover.Walled
					|| cell.Cover == KingdomBenefitCover.Natural) physical++;
			}
			return physical;
		}

		public static List<KindAmount> Clamp(IReadOnlyList<KindAmount> Caps,
			IReadOnlyList<KindAmount> Physical)
		{
			List<KindAmount> result = new List<KindAmount>();
			if (Caps == null || Physical == null) return result;
			for (int i = 0; i < Caps.Count; i++)
			{
				string kind = Fold(Caps[i].Kind);
				if (kind.Length == 0 || Caps[i].Amount <= 0) continue;
				int supplied = 0;
				for (int j = 0; j < Physical.Count; j++)
					if (Fold(Physical[j].Kind) == kind && Physical[j].Amount > 0)
						supplied = SaturatingAdd(supplied, Physical[j].Amount);
				int effective = EffectiveAmount(Caps[i].Amount, supplied);
				if (effective > 0) result.Add(new KindAmount(kind, effective));
			}
			return result;
		}

		public static string[] AcceptedTags(IReadOnlyList<string> Designated,
			IReadOnlyList<string> Physical)
		{
			if (Designated == null || Physical == null) return new string[0];
			List<string> result = new List<string>();
			for (int i = 0; i < Designated.Count; i++)
			{
				string wanted = Fold(Designated[i]);
				if (wanted.Length == 0 || Contains(result, wanted)) continue;
				for (int j = 0; j < Physical.Count; j++)
					if (Fold(Physical[j]) == wanted)
					{
						result.Add(wanted);
						break;
					}
			}
			return result.ToArray();
		}

		private static int SaturatingAdd(int A, int B)
		{
			long total = (long)A + B;
			return total >= int.MaxValue ? int.MaxValue : (int)total;
		}

		private static bool Contains(List<string> Values, string Value)
		{
			for (int i = 0; i < Values.Count; i++)
				if (Values[i] == Value) return true;
			return false;
		}

		private static string Fold(string Value)
		{
			return (Value ?? "").Trim().ToLowerInvariant();
		}
	}
}
