using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomTrade
	{
		/// <summary>Classifies every frozen physical row before quarantine can seal it.</summary>
		private static void ReconcilePhysicalFailure(TradeLiveFrame Frame,
			KingdomTradeOperation Operation, Zone Z, string Fault)
		{
			if (Operation == null) return;
			int water = 0;
			int ambiguousWater = 0;
			TradePhysicalFrame physical = Frame?.Physical;
			if (physical != null)
			{
				for (int i = 0; i < physical.Water.Count; i++)
				{
					WaterWitness witness = physical.Water[i];
					if (ExactWaterWitness(witness, Z, true))
					{
						witness.Leg.State = KingdomTradePhysicalState.Proved;
						water = KingdomTradeRules.SaturatingAdd(water, witness.Delta);
					}
					else if (ExactWaterWitness(witness, Z, false))
						witness.Leg.State = KingdomTradePhysicalState.Prepared;
					else
					{
						witness.Leg.State = KingdomTradePhysicalState.Lost;
						ambiguousWater = KingdomTradeRules.SaturatingAdd(
							ambiguousWater, witness.Delta);
					}
				}
				int material = 0;
				for (int i = 0; i < physical.Materials.Count; i++)
				{
					MaterialWitness witness = physical.Materials[i];
					if (ExactMaterialWitness(witness, Z) &&
						CountMarker(Z, witness.Marker) == 1)
					{
						witness.Output.State = KingdomTradePhysicalState.Proved;
						material = KingdomTradeRules.SaturatingAdd(material,
							witness.Count);
					}
					else witness.Output.State = KingdomTradePhysicalState.Lost;
				}
				Operation.MaterialProved = material;
				RefreshSurveyWater(physical);
			}
			Operation.ProvedWater = water;
			Operation.AmbiguousWater = System.Math.Max(Operation.AmbiguousWater,
				ambiguousWater);
			Quarantine(Operation, Fault);
		}
	}
}
