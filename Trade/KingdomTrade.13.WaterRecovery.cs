using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomTrade
	{
		private static bool TryBindPersistedPhysicalFrame(TradeLiveFrame Frame,
			KingdomTradeOperation Operation, Zone Z, KingdomSurvey Survey)
		{
			if (Frame == null || Operation == null || Z == null) return false;
			if (Frame.Physical != null) return ExactPhysicalFrame(Frame, Operation, Z);
			TradePhysicalFrame physical = new TradePhysicalFrame();
			if (Operation.WaterLegs != null && Operation.WaterLegs.Count > 0)
			{
				if (Survey == null || Survey.Stores == null) return false;
				physical.Survey = Survey;
				physical.StoreList = Survey.Stores;
				physical.StoreRows = Survey.Stores.ToArray();
				int provedWater = 0;
				for (int i = 0; i < Operation.WaterLegs.Count; i++)
				{
					KingdomTradeWaterLeg leg = Operation.WaterLegs[i];
					if (leg?.State == KingdomTradePhysicalState.Skipped)
					{
						if (!KingdomTradeRules.ValidSkippedPolityWaterLeg(Operation, leg)) return false;
						continue;
					}
					if (leg == null || (leg.State != KingdomTradePhysicalState.Prepared
							&& leg.State != KingdomTradePhysicalState.Proved && (leg.State !=
							KingdomTradePhysicalState.Intent || Operation.Kind !=
							KingdomTradeOperationKind.PolityConsignmentDelivery))) return false;
					GameObject owner;
					LoadedTopologyWitness topology;
					if (ResolveLoadedObject(leg.OwnerId, Z, out owner, out topology)
						!= LoadedObjectResolution.ExactUnique) return false;
					LiquidVolume vessel = owner?.GetPart<LiquidVolume>();
					if (!ExactDedicated(owner, vessel, Z) || vessel.ComponentLiquids == null
						|| !ContainsReference(physical.StoreRows, vessel)) return false;
					WaterWitness witness = CaptureWaterWitness(leg, owner, vessel);
					if (witness == null) return false;
					witness.Topology = topology;
					if (leg.State == KingdomTradePhysicalState.Intent)
					{
						KingdomTradeWaterIntentResolution resolution = KingdomTradeRules.
							ClassifyPolityWaterIntent(leg, vessel.MaxVolume, vessel.Volume,
								ComponentFingerprint(vessel));
						bool exactBefore = resolution == KingdomTradeWaterIntentResolution.Before &&
							ExactWaterWitness(witness, Z, false);
						bool exactAfter = resolution == KingdomTradeWaterIntentResolution.After &&
							ExactWaterWitness(witness, Z, true);
						if (exactBefore) leg.State = KingdomTradePhysicalState.Prepared;
						else if (exactAfter) leg.State = KingdomTradePhysicalState.Proved;
						else
						{
							leg.State = KingdomTradePhysicalState.Lost;
							Operation.AmbiguousWater = Math.Max(Operation.AmbiguousWater,
								Math.Max(1, Operation.RequestedWater - provedWater));
							Quarantine(Operation,
								"A persisted consignment debit is neither its exact before nor after state.");
							return false;
						}
					}
					if (!ExactWaterWitness(witness, Z,
						leg.State == KingdomTradePhysicalState.Proved)) return false;
					if (leg.State == KingdomTradePhysicalState.Proved)
						provedWater = KingdomTradeRules.SaturatingAdd(provedWater, leg.Delta);
					physical.Water.Add(witness);
				}
				Operation.ProvedWater = provedWater;
				Operation.AmbiguousWater = 0;
			}
			Frame.Physical = physical;
			if (Operation.MaterialOutputs != null)
			{
				int proved = 0;
				for (int i = 0; i < Operation.MaterialOutputs.Count; i++)
				{
					KingdomTradeMaterialOutput output = Operation.MaterialOutputs[i];
					if (output == null) return false;
					if (output.State == KingdomTradePhysicalState.Proved
						|| output.State == KingdomTradePhysicalState.Intent)
					{
						GameObject destination;
						LoadedTopologyWitness destinationTopology;
						if (ResolveLoadedObject(output.DestinationOwnerId, Z, out destination,
							out destinationTopology) != LoadedObjectResolution.ExactUnique) return false;
						GameObject item;
						LoadedTopologyWitness itemTopology;
						if (ResolveLoadedObject(output.OutputId, Z, out item, out itemTopology)
							!= LoadedObjectResolution.ExactUnique
							|| !ExactLoadedTopology(destinationTopology)) return false;
						InventoryWitness inventory;
						if (!TryCaptureInventory(physical, destination, Z, out inventory)) return false;
						MaterialWitness witness = CaptureMaterialWitness(output, item,
							destination, inventory);
						if (witness == null) return false;
						witness.Topology = itemTopology;
						physical.Materials.Add(witness);
						if (!ExactMaterialWitness(witness, Z)
							|| CountMarker(Z, witness.Marker) != 1) return false;
						output.State = KingdomTradePhysicalState.Proved;
						proved = KingdomTradeRules.SaturatingAdd(proved, witness.Count);
					}
					else if (output.State == KingdomTradePhysicalState.Prepared
						|| output.State == KingdomTradePhysicalState.CreateIntent
						|| output.State == KingdomTradePhysicalState.CleanupIntent) return false;
				}
				Operation.MaterialProved = proved;
			}
			return ExactPhysicalFrame(Frame, Operation, Z);
		}

		private static bool ResumePreparedWater(KingdomTradeOperation Operation, Zone Z,
			TradeLiveFrame Frame)
		{
			TradePhysicalFrame physical = Frame?.Physical;
			if (physical == null) return Operation.WaterLegs == null
				|| Operation.WaterLegs.Count == 0;
			for (int i = 0; i < physical.Water.Count; i++)
			{
				WaterWitness witness = physical.Water[i];
				KingdomTradeWaterLeg leg = witness.Leg;
				if (leg.State == KingdomTradePhysicalState.Proved) continue;
				if (!RequirePolityConsignmentRecipient(Frame.System, Operation, Z,
					"resumed water debit leg")) return false;
				if (leg.State != KingdomTradePhysicalState.Prepared
					|| !ExactAuthority(Frame, KingdomTradePhase.ResourceIntent)
					|| !ExactPhysicalFrame(Frame, Operation, Z))
				{
					ReconcilePhysicalFailure(Frame, Operation, Z,
						"A resumed water frame changed before its exact mutation.");
					return false;
				}
				leg.State = KingdomTradePhysicalState.Intent;
				CallbackWitness callback = CaptureCallbackWitness(Frame);
				if (callback == null)
				{
					leg.State = KingdomTradePhysicalState.Prepared;
					Quarantine(Operation, "A resumed water callback frame could not be frozen before mutation.");
					return false;
				}
				int changed;
				try
				{
					changed = Operation.WaterDirection == KingdomTradeWaterDirection.Debit
						? KingdomLiquids.Drain(witness.Vessel, witness.Delta)
						: KingdomLiquids.Fill(witness.Vessel, "water", witness.Delta);
				}
				finally
				{
					BoundTradeSurvey(Z)?.ObserveCurrentTopology(witness.Owner);
				}
				if (!ExactCallbackWitness(Frame, callback)
					|| !ExactAuthority(Frame, KingdomTradePhase.ResourceIntent))
					return FailDetachedAuthority(Frame,
						"A resumed water callback detached its official trade authority.");
				if (leg.State != KingdomTradePhysicalState.Intent
					|| changed != witness.Delta || !ExactPhysicalWithWaterOverride(Frame,
						Operation, Z, witness, true))
				{
					leg.State = KingdomTradePhysicalState.Lost;
					ReconcilePhysicalFailure(Frame, Operation, Z,
						"A resumed water callback lost its exact physical proof.");
					return false;
				}
				leg.State = KingdomTradePhysicalState.Proved;
				Operation.ProvedWater = KingdomTradeRules.SaturatingAdd(
					Operation.ProvedWater, witness.Delta);
				if (!RequirePolityConsignmentRecipient(Frame.System, Operation, Z,
					"resumed post-debit landing")) return false;
			}
			int proved = 0;
			for (int i = 0; i < physical.Water.Count; i++)
				if (physical.Water[i].Leg.State == KingdomTradePhysicalState.Proved)
					proved = KingdomTradeRules.SaturatingAdd(proved,
						physical.Water[i].Delta);
			Operation.ProvedWater = proved;
			Operation.AmbiguousWater = Math.Max(0, Operation.RequestedWater - proved);
			return true;
		}

		private static bool ContainsReference(LiquidVolume[] Rows, LiquidVolume Value)
		{
			if (Rows == null) return false;
			for (int i = 0; i < Rows.Length; i++)
				if (ReferenceEquals(Rows[i], Value)) return true;
			return false;
		}

		private static bool ExactPhysicalFrame(TradeLiveFrame Frame,
			KingdomTradeOperation Operation, Zone Z)
		{
			TradePhysicalFrame physical = Frame?.Physical;
			if (physical == null)
			{
				bool empty = Operation != null && (Operation.WaterLegs == null
					|| Operation.WaterLegs.Count == 0) && (Operation.MaterialOutputs == null
					|| Operation.MaterialOutputs.Count == 0);
				return empty && (Operation.ProjectionState != KingdomTradePhysicalState.Proved
					|| ExactProjectionWitness(Frame, Operation, Z));
			}
			if (physical.StoreList != null
				&& !ExactWaterFrame(physical.Survey, physical.StoreList, physical.StoreRows))
				return false;
			for (int i = 0; i < physical.Water.Count; i++)
			{
				WaterWitness witness = physical.Water[i];
				if (witness?.Leg == null) return false;
				bool after = witness.Leg.State == KingdomTradePhysicalState.Proved;
				if (witness.Leg.State != KingdomTradePhysicalState.Prepared
					&& witness.Leg.State != KingdomTradePhysicalState.Proved) return false;
				if (!ExactWaterWitness(witness, Z, after)) return false;
			}
			for (int i = 0; i < physical.Inventories.Count; i++)
				if (!ExactInventory(physical.Inventories[i], Z)) return false;
			for (int i = 0; i < physical.Materials.Count; i++)
				if (!ExactMaterialWitness(physical.Materials[i], Z)
					|| CountMarker(Z, physical.Materials[i].Marker) != 1) return false;
			if (Operation != null && Operation.ProjectionState == KingdomTradePhysicalState.Proved
				&& !ExactProjectionWitness(Frame, Operation, Z)) return false;
			return true;
		}

		private static bool ExactPhysicalWithWaterOverride(TradeLiveFrame Frame,
			KingdomTradeOperation Operation, Zone Z, WaterWitness Override, bool After)
		{
			TradePhysicalFrame physical = Frame?.Physical;
			if (physical == null || (physical.StoreList != null
				&& !ExactWaterFrame(physical.Survey, physical.StoreList, physical.StoreRows))) return false;
			for (int i = 0; i < physical.Water.Count; i++)
			{
				WaterWitness witness = physical.Water[i];
				bool expectedAfter = ReferenceEquals(witness, Override) ? After
					: witness.Leg.State == KingdomTradePhysicalState.Proved;
				if (!ReferenceEquals(witness, Override)
					&& witness.Leg.State != KingdomTradePhysicalState.Prepared
					&& witness.Leg.State != KingdomTradePhysicalState.Proved) return false;
				if (!ExactWaterWitness(witness, Z, expectedAfter)) return false;
			}
			for (int i = 0; i < physical.Inventories.Count; i++)
				if (!ExactInventory(physical.Inventories[i], Z)) return false;
			for (int i = 0; i < physical.Materials.Count; i++)
				if (!ExactMaterialWitness(physical.Materials[i], Z)
					|| CountMarker(Z, physical.Materials[i].Marker) != 1) return false;
			if (Operation != null && Operation.ProjectionState == KingdomTradePhysicalState.Proved
				&& !ExactProjectionWitness(Frame, Operation, Z)) return false;
			return true;
		}

	}
}
