using System;

namespace ThousandAndFirst
{
	public static partial class KingdomMaterialDebitRules
	{
		internal static KingdomMaterialDebitResult EmptyResult(KingdomMaterialDebitOutcome Outcome,
			KingdomMaterialDebitFault Fault, KingdomMaterialDebitCost Requested, string Failure)
		{
			KingdomMaterialDebitCost request = Requested ?? new KingdomMaterialDebitCost();
			return new KingdomMaterialDebitResult(Outcome, Fault, request,
				new KingdomMaterialDebitCost(), request, new KingdomMaterialDebitCost(), 0, Failure,
				Outcome != KingdomMaterialDebitOutcome.InvalidReservation);
		}

		internal static KingdomMaterialDebitCost Credit(KingdomMaterialDebitCost Requested,
			KingdomMaterialDebitCost Lost)
		{
			KingdomMaterialTally materials = new KingdomMaterialTally();
			KingdomBitTally bits = new KingdomBitTally();
			KingdomExoticTally exotics = new KingdomExoticTally();
			for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
			{
				KingdomMaterial kind = (KingdomMaterial)i;
				materials.Set(kind, Math.Min(Requested.Materials.Get(kind), Lost.Materials.Get(kind)));
			}
			for (int i = 0; i < KingdomMaterialRules.BitTierCount; i++)
			{
				bits.Set(i, Math.Min(Requested.Bits.Get(i), Lost.Bits.Get(i)));
			}
			for (int i = 0; i < KingdomMaterialRules.ExoticCount; i++)
			{
				KingdomExotic kind = (KingdomExotic)i;
				exotics.Set(kind, Math.Min(Requested.Exotics.Get(kind), Lost.Exotics.Get(kind)));
			}
			return new KingdomMaterialDebitCost(materials, bits, exotics);
		}

		internal static KingdomMaterialDebitCost Subtract(KingdomMaterialDebitCost Whole,
			KingdomMaterialDebitCost Part)
		{
			KingdomMaterialTally materials = new KingdomMaterialTally();
			KingdomBitTally bits = new KingdomBitTally();
			KingdomExoticTally exotics = new KingdomExoticTally();
			for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
			{
				KingdomMaterial kind = (KingdomMaterial)i;
				materials.Set(kind, Whole.Materials.Get(kind) - Part.Materials.Get(kind));
			}
			for (int i = 0; i < KingdomMaterialRules.BitTierCount; i++)
			{
				bits.Set(i, Whole.Bits.Get(i) - Part.Bits.Get(i));
			}
			for (int i = 0; i < KingdomMaterialRules.ExoticCount; i++)
			{
				KingdomExotic kind = (KingdomExotic)i;
				exotics.Set(kind, Whole.Exotics.Get(kind) - Part.Exotics.Get(kind));
			}
			return new KingdomMaterialDebitCost(materials, bits, exotics);
		}
	}
}
