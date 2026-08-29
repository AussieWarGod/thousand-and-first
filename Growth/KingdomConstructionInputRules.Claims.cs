using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomConstructionInputRules
	{
		internal static bool TryParseMaterialClaim(string Claim,
			out KingdomMaterialDebitCost Cost)
		{
			Cost = null;
			if (!ValidText(Claim, MaxClaimChars, false)
				|| !KingdomMaterialDebitCost.TryParseClaim(Claim, out Cost)) return false;
			return string.Equals(Claim, Cost.ToClaimString(), StringComparison.Ordinal);
		}

		internal static bool TryValidateMaterialPlan(KingdomConstructionInputReceipt Receipt,
			out KingdomConstructionInputFault Fault)
		{
			KingdomMaterialDebitCost requested;
			if (!TryParseMaterialClaim(Receipt.MaterialRequestedClaim, out requested))
				return Refuse(KingdomConstructionInputFault.Claim, out Fault);
			List<KingdomMaterialDebitSource> rows = new List<KingdomMaterialDebitSource>();
			for (int i = 0; i < Receipt.SourceCount; i++)
			{
				KingdomConstructionInputSourceLine line = Receipt.SourceAt(i);
				if (line.Kind == KingdomConstructionInputKind.Water)
				{
					if (line.Classification != WaterClassification)
						return Refuse(KingdomConstructionInputFault.Claim, out Fault);
					continue;
				}
				KingdomMaterialDebitSource source;
				if (!TryUnitSource(line, out source))
					return Refuse(KingdomConstructionInputFault.Claim, out Fault);
				rows.Add(source);
			}
			KingdomMaterialDebitPlan plan;
			KingdomMaterialDebitFault ignored;
			if (!KingdomMaterialDebitRules.TryPlan(requested, rows, out plan, out ignored)
				|| plan.Steps.Count != rows.Count)
				return Refuse(KingdomConstructionInputFault.Claim, out Fault);
			bool[] seen = new bool[Receipt.SourceCount];
			for (int i = 0; i < plan.Steps.Count; i++)
			{
				KingdomMaterialDebitStep step = plan.Steps[i];
				if (step.Source < 0 || step.Source >= Receipt.SourceCount || seen[step.Source])
					return Refuse(KingdomConstructionInputFault.Claim, out Fault);
				seen[step.Source] = true;
				KingdomConstructionInputSourceLine line = Receipt.SourceAt(step.Source);
				KingdomMaterialDebitSource source;
				if (!TryUnitSource(line, out source) || step.Original != line.Before
					|| step.Taken != line.Take || step.Kind != source.Kind
					|| step.KindIndex != source.KindIndex || !SameBits(step.UnitBits, source.UnitBits))
					return Refuse(KingdomConstructionInputFault.Claim, out Fault);
			}
			for (int i = 0; i < Receipt.SourceCount; i++)
				if (Receipt.SourceAt(i).Kind != KingdomConstructionInputKind.Water && !seen[i])
					return Refuse(KingdomConstructionInputFault.Claim, out Fault);
			Fault = KingdomConstructionInputFault.None;
			return true;
		}

		public static bool ClaimsBeforeExact(KingdomConstructionInputReceipt Receipt,
			int WaterSpent, int WaterOutstanding, int WaterLost, string MaterialSpentClaim,
			string MaterialOutstandingClaim, string MaterialLostClaim)
		{
			KingdomConstructionInputFault ignored;
			return TryValidate(Receipt, out ignored) && WaterSpent == Receipt.PriorWaterSpent
				&& WaterOutstanding == Receipt.WaterRequested && WaterLost == Receipt.PriorWaterLost
				&& string.Equals(MaterialSpentClaim, Receipt.PriorMaterialSpentClaim,
					StringComparison.Ordinal)
				&& string.Equals(MaterialOutstandingClaim, Receipt.MaterialRequestedClaim,
					StringComparison.Ordinal)
				&& string.Equals(MaterialLostClaim, Receipt.PriorMaterialLostClaim,
					StringComparison.Ordinal);
		}

		public static bool CommittedClaimsExact(KingdomConstructionInputReceipt Receipt,
			int WaterSpent, int WaterOutstanding, int WaterLost, string MaterialSpentClaim,
			string MaterialOutstandingClaim, string MaterialLostClaim)
		{
			KingdomConstructionInputFault validation;
			if (!TryValidate(Receipt, out validation)
				|| Receipt.TxPhase != KingdomConstructionInputTxPhase.Committed)
				return false;
			long spent = (long)Receipt.PriorWaterSpent + Receipt.WaterRequested;
			long lost = (long)Receipt.PriorWaterLost + Receipt.WaterRequested;
			if (spent > int.MaxValue || lost > int.MaxValue || WaterSpent != spent
				|| WaterLost != lost || WaterOutstanding != 0) return false;
			KingdomMaterialDebitCost priorSpent;
			KingdomMaterialDebitCost requested;
			KingdomMaterialDebitCost priorLost;
			KingdomMaterialDebitCost physical;
			KingdomMaterialDebitCost empty = new KingdomMaterialDebitCost();
			if (!TryParseMaterialClaim(Receipt.PriorMaterialSpentClaim, out priorSpent)
				|| !TryParseMaterialClaim(Receipt.MaterialRequestedClaim, out requested)
				|| !TryParseMaterialClaim(Receipt.PriorMaterialLostClaim, out priorLost)
				|| !TryPhysicalLoss(Receipt, out physical)) return false;
			KingdomMaterialDebitCost expectedSpent;
			KingdomMaterialDebitCost expectedLost;
			return TryAdd(priorSpent, requested, out expectedSpent)
				&& TryAdd(priorLost, physical, out expectedLost)
				&& string.Equals(MaterialSpentClaim, expectedSpent.ToClaimString(), StringComparison.Ordinal)
				&& string.Equals(MaterialLostClaim, expectedLost.ToClaimString(), StringComparison.Ordinal)
				&& string.Equals(MaterialOutstandingClaim, empty.ToClaimString(), StringComparison.Ordinal);
		}

		private static bool TryUnitSource(KingdomConstructionInputSourceLine Line,
			out KingdomMaterialDebitSource Source)
		{
			Source = null;
			KingdomMaterialDebitCost unit;
			if (!TryParseMaterialClaim(Line.Classification, out unit)) return false;
			int index;
			if (Line.Kind == KingdomConstructionInputKind.Material
				&& unit.Bits.IsEmpty() && unit.Exotics.IsEmpty()
				&& OneMaterial(unit.Materials, out index))
				Source = new KingdomMaterialDebitSource(Line.Ordinal,
					KingdomMaterialDebitSourceKind.Material, index, Line.Before);
			else if (Line.Kind == KingdomConstructionInputKind.Exotic
				&& unit.Materials.IsEmpty() && unit.Bits.IsEmpty()
				&& OneExotic(unit.Exotics, out index))
				Source = new KingdomMaterialDebitSource(Line.Ordinal,
					KingdomMaterialDebitSourceKind.Exotic, index, Line.Before);
			else if (Line.Kind == KingdomConstructionInputKind.Bit
				&& unit.Materials.IsEmpty() && unit.Exotics.IsEmpty() && !unit.Bits.IsEmpty())
				Source = new KingdomMaterialDebitSource(Line.Ordinal,
					KingdomMaterialDebitSourceKind.BitStock, 0, Line.Before, unit.Bits);
			return Source != null;
		}

		private static bool OneMaterial(KingdomMaterialTally Tally, out int Index)
		{
			Index = -1;
			for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
			{
				int value = Tally.Get((KingdomMaterial)i);
				if (value != 0 && (value != 1 || Index >= 0)) return false;
				if (value == 1) Index = i;
			}
			return Index >= 0;
		}

		private static bool OneExotic(KingdomExoticTally Tally, out int Index)
		{
			Index = -1;
			for (int i = 0; i < KingdomMaterialRules.ExoticCount; i++)
			{
				int value = Tally.Get((KingdomExotic)i);
				if (value != 0 && (value != 1 || Index >= 0)) return false;
				if (value == 1) Index = i;
			}
			return Index >= 0;
		}

		private static bool SameBits(KingdomBitTally A, KingdomBitTally B)
		{
			for (int i = 0; i < KingdomMaterialRules.BitTierCount; i++)
				if (A.Get(i) != B.Get(i)) return false;
			return true;
		}

		private static bool CostCovers(KingdomMaterialDebitCost Available,
			KingdomMaterialDebitCost Required)
		{
			if (Available == null || Required == null) return false;
			for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
				if (Available.Materials.Get((KingdomMaterial)i)
					< Required.Materials.Get((KingdomMaterial)i)) return false;
			for (int i = 0; i < KingdomMaterialRules.BitTierCount; i++)
				if (Available.Bits.Get(i) < Required.Bits.Get(i)) return false;
			for (int i = 0; i < KingdomMaterialRules.ExoticCount; i++)
				if (Available.Exotics.Get((KingdomExotic)i)
					< Required.Exotics.Get((KingdomExotic)i)) return false;
			return true;
		}

		private static bool TryPhysicalLoss(KingdomConstructionInputReceipt Receipt,
			out KingdomMaterialDebitCost Loss)
		{
			Loss = new KingdomMaterialDebitCost();
			for (int i = 0; i < Receipt.SourceCount; i++)
			{
				KingdomConstructionInputSourceLine line = Receipt.SourceAt(i);
				if (line.Kind == KingdomConstructionInputKind.Water) continue;
				KingdomMaterialDebitCost unit;
				KingdomMaterialDebitCost scaled;
				if (!TryParseMaterialClaim(line.Classification, out unit)
					|| !TryScale(unit, line.Take, out scaled) || !TryAdd(Loss, scaled, out Loss)) return false;
			}
			return true;
		}

		private static bool TryScale(KingdomMaterialDebitCost Value, int Count,
			out KingdomMaterialDebitCost Result)
		{
			Result = null;
			KingdomMaterialTally materials = new KingdomMaterialTally();
			KingdomBitTally bits = new KingdomBitTally();
			KingdomExoticTally exotics = new KingdomExoticTally();
			if (Count < 0) return false;
			for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
				if (!SetScaled(materials, (KingdomMaterial)i, Value.Materials.Get((KingdomMaterial)i), Count)) return false;
			for (int i = 0; i < KingdomMaterialRules.BitTierCount; i++)
				if (!SetScaled(bits, i, Value.Bits.Get(i), Count)) return false;
			for (int i = 0; i < KingdomMaterialRules.ExoticCount; i++)
				if (!SetScaled(exotics, (KingdomExotic)i, Value.Exotics.Get((KingdomExotic)i), Count)) return false;
			Result = new KingdomMaterialDebitCost(materials, bits, exotics);
			return true;
		}

		private static bool TryAdd(KingdomMaterialDebitCost A, KingdomMaterialDebitCost B,
			out KingdomMaterialDebitCost Result)
		{
			Result = null;
			KingdomMaterialTally m = A.Materials.Copy(); KingdomBitTally b = A.Bits.Copy();
			KingdomExoticTally e = A.Exotics.Copy();
			for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
				if (!TryAddValue(m, (KingdomMaterial)i, B.Materials.Get((KingdomMaterial)i))) return false;
			for (int i = 0; i < KingdomMaterialRules.BitTierCount; i++)
				if (!TryAddValue(b, i, B.Bits.Get(i))) return false;
			for (int i = 0; i < KingdomMaterialRules.ExoticCount; i++)
				if (!TryAddValue(e, (KingdomExotic)i, B.Exotics.Get((KingdomExotic)i))) return false;
			Result = new KingdomMaterialDebitCost(m, b, e); return true;
		}

		private static bool SetScaled(KingdomMaterialTally T, KingdomMaterial K, int V, int C)
		{ long n = (long)V * C; if (n > int.MaxValue) return false; T.Set(K, (int)n); return true; }
		private static bool SetScaled(KingdomBitTally T, int K, int V, int C)
		{ long n = (long)V * C; if (n > int.MaxValue) return false; T.Set(K, (int)n); return true; }
		private static bool SetScaled(KingdomExoticTally T, KingdomExotic K, int V, int C)
		{ long n = (long)V * C; if (n > int.MaxValue) return false; T.Set(K, (int)n); return true; }
		private static bool TryAddValue(KingdomMaterialTally T, KingdomMaterial K, int V)
		{ long n = (long)T.Get(K) + V; if (n > int.MaxValue) return false; T.Set(K, (int)n); return true; }
		private static bool TryAddValue(KingdomBitTally T, int K, int V)
		{ long n = (long)T.Get(K) + V; if (n > int.MaxValue) return false; T.Set(K, (int)n); return true; }
		private static bool TryAddValue(KingdomExoticTally T, KingdomExotic K, int V)
		{ long n = (long)T.Get(K) + V; if (n > int.MaxValue) return false; T.Set(K, (int)n); return true; }
	}
}
