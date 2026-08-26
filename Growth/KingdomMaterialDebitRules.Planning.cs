using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomMaterialDebitRules
	{
		private static List<KingdomMaterialDebitSource> UniqueValidSources(
			IList<KingdomMaterialDebitSource> Sources)
		{
			List<KingdomMaterialDebitSource> unique = new List<KingdomMaterialDebitSource>();
			HashSet<int> seen = new HashSet<int>();
			for (int i = 0; i < Sources.Count; i++)
			{
				KingdomMaterialDebitSource source = Sources[i];
				if (source == null || source.Source < 0 || source.Count <= 0 || !seen.Add(source.Source))
				{
					continue;
				}
				if (!SourceShapeValid(source))
				{
					continue;
				}
				unique.Add(source);
			}
			return unique;
		}

		private static bool SourceShapeValid(KingdomMaterialDebitSource Source)
		{
			switch (Source.Kind)
			{
			case KingdomMaterialDebitSourceKind.Material:
				return Source.KindIndex >= 0 && Source.KindIndex < KingdomMaterialRules.MaterialCount;
			case KingdomMaterialDebitSourceKind.Exotic:
				return Source.KindIndex >= 0 && Source.KindIndex < KingdomMaterialRules.ExoticCount;
			case KingdomMaterialDebitSourceKind.BitStock:
				return !Source.UnitBits.IsEmpty();
			default:
				return false;
			}
		}

		private static bool PlanMaterials(KingdomMaterialTally Cost,
			List<KingdomMaterialDebitSource> Sources, List<KingdomMaterialDebitStep> Steps)
		{
			for (int kind = 0; kind < KingdomMaterialRules.MaterialCount; kind++)
			{
				int remaining = Cost.Get((KingdomMaterial)kind);
				for (int i = 0; i < Sources.Count && remaining > 0; i++)
				{
					KingdomMaterialDebitSource source = Sources[i];
					if (source.Kind != KingdomMaterialDebitSourceKind.Material || source.KindIndex != kind)
					{
						continue;
					}
					int take = Math.Min(source.Count, remaining);
					Steps.Add(new KingdomMaterialDebitStep(source.Source, source.Kind,
						source.KindIndex, source.Count, take));
					remaining -= take;
				}
				if (remaining > 0) return false;
			}
			return true;
		}

		private static bool PlanExotics(KingdomExoticTally Cost,
			List<KingdomMaterialDebitSource> Sources, List<KingdomMaterialDebitStep> Steps)
		{
			for (int kind = 0; kind < KingdomMaterialRules.ExoticCount; kind++)
			{
				int remaining = Cost.Get((KingdomExotic)kind);
				for (int i = 0; i < Sources.Count && remaining > 0; i++)
				{
					KingdomMaterialDebitSource source = Sources[i];
					if (source.Kind != KingdomMaterialDebitSourceKind.Exotic || source.KindIndex != kind)
					{
						continue;
					}
					int take = Math.Min(source.Count, remaining);
					Steps.Add(new KingdomMaterialDebitStep(source.Source, source.Kind,
						source.KindIndex, source.Count, take));
					remaining -= take;
				}
				if (remaining > 0) return false;
			}
			return true;
		}

		private static bool PlanBits(KingdomBitTally Cost,
			List<KingdomMaterialDebitSource> Sources, List<KingdomMaterialDebitStep> Steps)
		{
			KingdomBitTally owed = Cost.Copy();
			List<KingdomMaterialDebitSource> bits = new List<KingdomMaterialDebitSource>();
			for (int i = 0; i < Sources.Count; i++)
			{
				if (Sources[i].Kind == KingdomMaterialDebitSourceKind.BitStock)
				{
					bits.Add(Sources[i]);
				}
			}
			bits.Sort(delegate(KingdomMaterialDebitSource A, KingdomMaterialDebitSource B)
			{
				long a = BitWorth(A.UnitBits);
				long b = BitWorth(B.UnitBits);
				int compare = a.CompareTo(b);
				return (compare != 0) ? compare : A.Source.CompareTo(B.Source);
			});
			for (int i = 0; i < bits.Count && !owed.IsEmpty(); i++)
			{
				KingdomMaterialDebitSource source = bits[i];
				int take = UnitsWanted(owed, source.UnitBits, source.Count);
				if (take <= 0)
				{
					continue;
				}
				Steps.Add(new KingdomMaterialDebitStep(source.Source, source.Kind,
					0, source.Count, take, source.UnitBits));
				for (int tier = 0; tier < KingdomMaterialRules.BitTierCount; tier++)
				{
					long yielded = (long)source.UnitBits.Get(tier) * take;
					int reduction = (yielded >= owed.Get(tier)) ? owed.Get(tier) : (int)yielded;
					owed.Add(tier, -reduction);
				}
			}
			return owed.IsEmpty();
		}

		private static int UnitsWanted(KingdomBitTally Owed, KingdomBitTally Unit, int Available)
		{
			long wanted = 0L;
			for (int i = 0; i < KingdomMaterialRules.BitTierCount; i++)
			{
				int owed = Owed.Get(i);
				int per = Unit.Get(i);
				if (owed <= 0 || per <= 0)
				{
					continue;
				}
				long units = ((long)owed + per - 1L) / per;
				if (units > wanted) wanted = units;
			}
			if (wanted <= 0L) return 0;
			return (wanted >= Available) ? Available : (int)wanted;
		}

		private static long BitWorth(KingdomBitTally Worth)
		{
			long total = 0L;
			for (int i = 0; i < KingdomMaterialRules.BitTierCount; i++)
			{
				total += (long)Worth.Get(i) * (i + 1L);
			}
			return total;
		}

		private static void AddLost(KingdomMaterialDebitStep Step, int Removed,
			KingdomMaterialTally Materials, KingdomBitTally Bits, KingdomExoticTally Exotics)
		{
			switch (Step.Kind)
			{
			case KingdomMaterialDebitSourceKind.Material:
				Materials.Add((KingdomMaterial)Step.KindIndex, Removed);
				break;
			case KingdomMaterialDebitSourceKind.Exotic:
				Exotics.Add((KingdomExotic)Step.KindIndex, Removed);
				break;
			case KingdomMaterialDebitSourceKind.BitStock:
				for (int i = 0; i < KingdomMaterialRules.BitTierCount; i++)
				{
					long amount = (long)Step.UnitBits.Get(i) * Removed;
					Bits.Add(i, amount >= int.MaxValue ? int.MaxValue : (int)amount);
				}
				break;
			}
		}
	}
}
