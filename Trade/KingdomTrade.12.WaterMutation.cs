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
		private static bool ApplyWater(KingdomTradeOperation Operation, Zone Z,
			KingdomSurvey Survey, TradeLiveFrame Frame)
		{
			if (Operation.RequestedWater <= 0) return true;
			if (Frame == null || Survey == null || Survey.Stores == null
				|| Operation.WaterLegs.Count != 0) return false;
			TradePhysicalFrame physical = new TradePhysicalFrame
			{
				Survey = Survey,
				StoreList = Survey.Stores,
				StoreRows = Survey.Stores.ToArray()
			};
			Frame.Physical = physical;
			int planned = 0;
			for (int i = 0; i < Survey.Stores.Count && planned < Operation.RequestedWater
				&& physical.Water.Count < KingdomTradeRules.MaxWaterLegs; i++)
			{
				LiquidVolume vessel = Survey.Stores[i];
				GameObject owner = vessel?.ParentObject;
				bool duplicate = false;
				for (int j = 0; j < physical.Water.Count; j++)
					if (ReferenceEquals(physical.Water[j].Vessel, vessel)) duplicate = true;
				if (duplicate || !ExactDedicated(owner, vessel, Z)
					|| vessel.ComponentLiquids == null) continue;
				int available;
				if (Operation.WaterDirection == KingdomTradeWaterDirection.Debit)
				{
					if (!KingdomLiquids.HasFreshWater(vessel)) continue;
					available = vessel.Volume;
				}
				else
				{
					if (!KingdomLiquids.CanReceiveFreshWater(vessel)) continue;
					available = vessel.MaxVolume - vessel.Volume;
				}
				if (available <= 0) continue;
				int delta = Math.Min(available, Operation.RequestedWater - planned);
				int after = Operation.WaterDirection == KingdomTradeWaterDirection.Debit
					? vessel.Volume - delta : vessel.Volume + delta;
				KingdomTradeWaterLeg leg = new KingdomTradeWaterLeg
				{
					OwnerId = owner.ID,
					ZoneId = Z.ZoneID,
					Capacity = vessel.MaxVolume,
					Before = vessel.Volume,
					Delta = delta,
					After = after,
					BeforeComposition = ComponentFingerprint(vessel),
					AfterComposition = after == 0 ? "empty" : "water=1000",
					State = KingdomTradePhysicalState.Prepared
				};
					Operation.WaterLegs.Add(leg);
					GameObject resolvedOwner;
					LoadedTopologyWitness ownerTopology;
					if (ResolveLoadedObject(owner.ID, Z, out resolvedOwner, out ownerTopology)
						!= LoadedObjectResolution.ExactUnique
						|| !ReferenceEquals(resolvedOwner, owner))
					{
						Quarantine(Operation, "A source vessel owner id was not exact-unique on active settlement ground.");
						return false;
					}
					WaterWitness witness = CaptureWaterWitness(leg, owner, vessel);
				if (witness == null)
				{
					Quarantine(Operation,
						"A source vessel could not be frozen exactly before intent.");
						return false;
					}
					witness.Topology = ownerTopology;
				physical.Water.Add(witness);
				planned += delta;
			}
			RefreshReceiptRows(Frame);
			if (Operation.WaterDirection == KingdomTradeWaterDirection.Debit
				&& planned != Operation.RequestedWater)
			{
				Quarantine(Operation, "The exact source vessels cannot cover the published manifest.");
				return false;
			}
			if (planned == 0) return true;
			Operation.Phase = KingdomTradePhase.ResourceIntent;
			for (int i = 0; i < physical.Water.Count; i++)
			{
				WaterWitness witness = physical.Water[i];
				KingdomTradeWaterLeg leg = witness.Leg;
				if (!ExactAuthority(Frame, KingdomTradePhase.ResourceIntent)
					|| !ExactPhysicalFrame(Frame, Operation, Z))
				{
					ReconcilePhysicalFailure(Frame, Operation, Z,
						"A water frame changed before its exact mutation.");
					return false;
				}
				leg.State = KingdomTradePhysicalState.Intent;
				CallbackWitness callback = CaptureCallbackWitness(Frame);
				LoadedTopologyWitness createTopology = CaptureLoadedTopology();
				if (callback == null || createTopology == null
					|| !ExactLoadedTopology(createTopology))
				{
					leg.State = KingdomTradePhysicalState.Prepared;
					Quarantine(Operation, "A water callback frame could not be frozen before mutation.");
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
					// Liquid callbacks may commit before throwing. Reclassify the exact owner
					// while this attended survey remains the later-pass authority.
					BoundTradeSurvey(Z)?.ObserveCurrentTopology(witness.Owner);
				}
				if (!ExactCallbackWitness(Frame, callback)
					|| !ExactAuthority(Frame, KingdomTradePhase.ResourceIntent))
				{
					return FailDetachedAuthority(Frame,
						"A water callback detached or rewrote its official trade authority.");
				}
				if (leg.State != KingdomTradePhysicalState.Intent
					|| changed != witness.Delta || !ExactPhysicalWithWaterOverride(Frame,
						Operation, Z, witness, true))
				{
					leg.State = KingdomTradePhysicalState.Lost;
					ReconcilePhysicalFailure(Frame, Operation, Z,
						"A water callback changed an exact owner, part, dedication, capacity, composition, or intended delta.");
					return false;
				}
				leg.State = KingdomTradePhysicalState.Proved;
				Operation.ProvedWater = KingdomTradeRules.SaturatingAdd(
					Operation.ProvedWater, witness.Delta);
			}
			Operation.AmbiguousWater = 0;
			return true;
		}

		private static void RefreshSurveyWater(TradePhysicalFrame Physical)
		{
			if (Physical == null || Physical.Survey == null || Physical.StoreList == null) return;
			int stored = 0;
			int room = 0;
			HashSet<GameObject> owners = new HashSet<GameObject>();
			for (int i = 0; i < Physical.StoreList.Count; i++)
			{
				LiquidVolume vessel = Physical.StoreList[i];
				if (vessel == null) continue;
				if (GameObject.Validate(vessel.ParentObject)) owners.Add(vessel.ParentObject);
				if (KingdomLiquids.HasFreshWater(vessel))
					stored = KingdomTradeRules.SaturatingAdd(stored, vessel.Volume);
				if (KingdomLiquids.CanReceiveFreshWater(vessel) && vessel.MaxVolume >= vessel.Volume)
					room = KingdomTradeRules.SaturatingAdd(room, vessel.MaxVolume - vessel.Volume);
			}
			Physical.Survey.StoredWater = stored;
			Physical.Survey.StorageSpace = room;
			// Aggregate was published from the frozen store list; align every cached row
			// without applying those same deltas twice.
			foreach (GameObject owner in owners)
				Physical.Survey.SynchronizeReceiptObject(owner);
		}

		private static bool ExactWaterWitness(WaterWitness Witness, Zone Z, bool After)
		{
			if (!ExactWaterReceipt(Witness) || !ExactLoadedTopology(Witness.Topology)
				|| Witness.Dictionary == null
				|| !ExactDedicated(Witness.Owner, Witness.Vessel, Z)
				|| Witness.Owner.CurrentCell != Witness.Cell || Witness.Cell == null
				|| Witness.Cell.ParentZone != Z
				|| !ReferenceEquals(Witness.Vessel.ComponentLiquids, Witness.Dictionary)
				|| !string.Equals(Witness.Owner.ID, Witness.OwnerId,
					StringComparison.Ordinal)
				|| !string.Equals(Z.ZoneID, Witness.ZoneId, StringComparison.Ordinal)
				|| Witness.Vessel.MaxVolume != Witness.Capacity) return false;
			if (After)
				return Witness.Vessel.Volume == Witness.After
					&& string.Equals(ComponentFingerprint(Witness.Vessel),
						Witness.AfterComposition, StringComparison.Ordinal);
			return Witness.Vessel.Volume == Witness.Before
				&& string.Equals(ComponentFingerprint(Witness.Vessel),
					Witness.BeforeComposition, StringComparison.Ordinal)
				&& ComponentsExact(Witness.Dictionary, Witness.BeforeComponents);
		}

		private static bool ExactDedicated(GameObject Owner, LiquidVolume Vessel, Zone Z)
		{
			return GameObject.Validate(Owner) && Vessel != null && Z != null
				&& Owner.CurrentZone == Z && Vessel.ParentObject == Owner
				&& Owner.CurrentCell != null && Owner.CurrentCell.ParentZone == Z
				&& ReferenceEquals(Owner.GetPart<LiquidVolume>(), Vessel)
				&& Owner.GetIntProperty("KingdomStores") == 1
				&& Vessel.MaxVolume >= 0 && !string.IsNullOrEmpty(Owner.ID);
		}

		private static bool ExactWaterFrame(KingdomSurvey Survey,
			List<LiquidVolume> StoreList, LiquidVolume[] Rows)
		{
			if (Survey == null || !ReferenceEquals(Survey.Stores, StoreList)
				|| StoreList == null || Rows == null || StoreList.Count != Rows.Length) return false;
			for (int i = 0; i < Rows.Length; i++)
				if (!ReferenceEquals(StoreList[i], Rows[i])) return false;
			return true;
		}

		private static bool ComponentsExact(Dictionary<string, int> Current,
			Dictionary<string, int> Expected)
		{
			if (Current == null || Expected == null || Current.Count != Expected.Count) return false;
			foreach (KeyValuePair<string, int> pair in Expected)
			{
				int value;
				if (!Current.TryGetValue(pair.Key, out value) || value != pair.Value) return false;
			}
			return true;
		}

		private static string ComponentFingerprint(LiquidVolume Vessel)
		{
			if (Vessel == null || Vessel.Volume == 0) return "empty";
			if (Vessel.ComponentLiquids == null) return "missing";
			List<string> keys = new List<string>(Vessel.ComponentLiquids.Keys);
			keys.Sort(StringComparer.Ordinal);
			StringBuilder text = new StringBuilder();
			for (int i = 0; i < keys.Count; i++)
			{
				if (i > 0) text.Append('|');
				text.Append(keys[i]).Append('=').Append(Vessel.ComponentLiquids[keys[i]]);
			}
			return text.ToString();
		}

	}
}
