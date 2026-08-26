using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomConstructionRules
	{
		private static KingdomMaterialDebitCost MaterialOutstanding(KingdomConstructionClaims Claims)
		{
			KingdomMaterialDebitCost cost;
			return Claims != null && KingdomMaterialDebitCost.TryParseClaim(Claims.MaterialOutstanding, out cost)
				? cost : new KingdomMaterialDebitCost();
		}

		private static KingdomMaterialDebitCost AddCost(KingdomMaterialDebitCost A,
			KingdomMaterialDebitCost B)
		{
			KingdomMaterialTally materials = A.Materials.Copy();
			KingdomBitTally bits = A.Bits.Copy();
			KingdomExoticTally exotics = A.Exotics.Copy();
			for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
			{
				materials.Add((KingdomMaterial)i, B.Materials.Get((KingdomMaterial)i));
			}
			for (int i = 0; i < KingdomMaterialRules.BitTierCount; i++)
			{
				bits.Add(i, B.Bits.Get(i));
			}
			for (int i = 0; i < KingdomMaterialRules.ExoticCount; i++)
			{
				exotics.Add((KingdomExotic)i, B.Exotics.Get((KingdomExotic)i));
			}
			return new KingdomMaterialDebitCost(materials, bits, exotics);
		}

		private static bool TryAddPaidCost(KingdomMaterialDebitCost A,
			KingdomMaterialDebitCost B, out KingdomMaterialDebitCost Sum)
		{
			Sum = null;
			if (A == null || B == null) return false;
			KingdomMaterialTally materials = new KingdomMaterialTally();
			KingdomBitTally bits = new KingdomBitTally();
			KingdomExoticTally exotics = new KingdomExoticTally();
			for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
			{
				KingdomMaterial kind = (KingdomMaterial)i;
				long value = (long)A.Materials.Get(kind) + B.Materials.Get(kind);
				if (value > int.MaxValue) return false;
				materials.Set(kind, (int)value);
			}
			for (int i = 0; i < KingdomMaterialRules.BitTierCount; i++)
			{
				long value = (long)A.Bits.Get(i) + B.Bits.Get(i);
				if (value > int.MaxValue) return false;
				bits.Set(i, (int)value);
			}
			for (int i = 0; i < KingdomMaterialRules.ExoticCount; i++)
			{
				KingdomExotic kind = (KingdomExotic)i;
				long value = (long)A.Exotics.Get(kind) + B.Exotics.Get(kind);
				if (value > int.MaxValue) return false;
				exotics.Set(kind, (int)value);
			}
			Sum = new KingdomMaterialDebitCost(materials, bits, exotics);
			return true;
		}

		private static bool SameCost(KingdomMaterialDebitCost A, KingdomMaterialDebitCost B)
		{
			return SumMatches(A, B, new KingdomMaterialDebitCost());
		}

		private static bool SumMatches(KingdomMaterialDebitCost Whole,
			KingdomMaterialDebitCost A, KingdomMaterialDebitCost B)
		{
			for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
			{
				KingdomMaterial kind = (KingdomMaterial)i;
				if ((long)A.Materials.Get(kind) + B.Materials.Get(kind) != Whole.Materials.Get(kind)) return false;
			}
			for (int i = 0; i < KingdomMaterialRules.BitTierCount; i++)
			{
				if ((long)A.Bits.Get(i) + B.Bits.Get(i) != Whole.Bits.Get(i)) return false;
			}
			for (int i = 0; i < KingdomMaterialRules.ExoticCount; i++)
			{
				KingdomExotic kind = (KingdomExotic)i;
				if ((long)A.Exotics.Get(kind) + B.Exotics.Get(kind) != Whole.Exotics.Get(kind)) return false;
			}
			return true;
		}

		private static bool Covers(KingdomMaterialDebitCost Whole, KingdomMaterialDebitCost Part)
		{
			for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
			{
				KingdomMaterial kind = (KingdomMaterial)i;
				if (Whole.Materials.Get(kind) < Part.Materials.Get(kind)) return false;
			}
			for (int i = 0; i < KingdomMaterialRules.BitTierCount; i++)
			{
				if (Whole.Bits.Get(i) < Part.Bits.Get(i)) return false;
			}
			for (int i = 0; i < KingdomMaterialRules.ExoticCount; i++)
			{
				KingdomExotic kind = (KingdomExotic)i;
				if (Whole.Exotics.Get(kind) < Part.Exotics.Get(kind)) return false;
			}
			return true;
		}

	}
}
