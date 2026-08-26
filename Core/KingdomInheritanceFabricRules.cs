using System;

namespace ThousandAndFirst
{
	/// <summary>Pure condition and sampling rules for visibly weathered inherited fabric.</summary>
	internal static class KingdomInheritanceFabricRules
	{
		internal const int BatteredCoveragePercent = 25;
		internal const int HalfRuinedCoveragePercent = 50;
		internal const int RuinedCoveragePercent = 100;

		internal static int WearFor(KingdomInheritWorkState State, int Condition)
		{
			if (State != KingdomInheritWorkState.Standing
				&& State != KingdomInheritWorkState.Derelict) return 0;
			if (Condition < 0 || Condition > 100) return KingdomMaterialRules.MaxWearPercent;
			int wear = 100 - Condition;
			return wear > KingdomMaterialRules.MaxWearPercent
				? KingdomMaterialRules.MaxWearPercent : wear;
		}

		internal static KingdomVisualStateKind VisualStateFor(int Wear)
		{
			if (Wear >= KingdomMaterialRules.HalfWreckedWearPercent)
				return KingdomVisualStateKind.Ruined;
			if (Wear >= KingdomMaterialRules.BadlyUsedWearPercent)
				return KingdomVisualStateKind.HalfRuined;
			return Wear > 0 ? KingdomVisualStateKind.Battered : KingdomVisualStateKind.Sound;
		}

		/// <summary>Marks bounded structure/object cells, never floors or travel geometry.</summary>
		internal static bool MarksComponent(KingdomInheritWorkState State, int Condition,
			ArchitectureLayer Layer, string SnapshotHash, string Slot)
		{
			int wear = WearFor(State, Condition);
			if (wear <= 0 || (Layer != ArchitectureLayer.Structure
				&& Layer != ArchitectureLayer.Object)
				|| string.IsNullOrEmpty(SnapshotHash) || string.IsNullOrEmpty(Slot)) return false;
			int coverage = wear >= KingdomMaterialRules.HalfWreckedWearPercent
				? RuinedCoveragePercent
				: (wear >= KingdomMaterialRules.BadlyUsedWearPercent
					? HalfRuinedCoveragePercent : BatteredCoveragePercent);
			return StableBucket(SnapshotHash, Slot) < coverage;
		}

		private static int StableBucket(string SnapshotHash, string Slot)
		{
			unchecked
			{
				uint hash = 2166136261u;
				string text = SnapshotHash + "|" + Slot;
				for (int i = 0; i < text.Length; i++)
				{
					hash ^= text[i];
					hash *= 16777619u;
				}
				return (int)(hash % 100u);
			}
		}
	}
}
