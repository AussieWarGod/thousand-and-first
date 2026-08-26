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
		private static bool ReconcileMaterialOutputs(KingdomTradeOperation Operation, Zone Z,
			TradeLiveFrame Frame)
		{
			int proved = 0;
			for (int i = 0; i < Operation.MaterialOutputs.Count; i++)
			{
				KingdomTradeMaterialOutput output = Operation.MaterialOutputs[i];
				if (output.State == KingdomTradePhysicalState.Prepared
					|| output.State == KingdomTradePhysicalState.CreateIntent
					|| output.CleanupState == KingdomTradePhysicalState.CleanupIntent)
				{
					output.State = KingdomTradePhysicalState.Lost;
					if (output.CleanupState == KingdomTradePhysicalState.CleanupIntent)
						output.CleanupState = KingdomTradePhysicalState.Lost;
					return QuarantineFalse(Operation,
						"A reloaded material creation or cleanup frame was uninspectable and was not replayed.");
				}
				if (output.State == KingdomTradePhysicalState.Proved)
					proved = KingdomTradeRules.SaturatingAdd(proved, output.Count);
				else if (output.State == KingdomTradePhysicalState.Intent)
					return QuarantineFalse(Operation,
						"A reloaded material Add intent lacked one exact live topology and was not replayed.");
			}
			Operation.MaterialProved = proved;
			return ExactPhysicalFrame(Frame, Operation, Z);
		}

		private static bool ExactMaterial(MaterialWitness Witness, Zone Z)
		{
			if (!ExactMaterialReceipt(Witness)) return false;
			GameObject item = Witness.Item;
			GameObject destination = Witness.Destination;
			return GameObject.Validate(item) && GameObject.Validate(destination) && Z != null
				&& destination.CurrentZone == Z && destination.Inventory != null
				&& destination.GetIntProperty(KingdomMaterials.StockpileProperty) == 1
				&& string.Equals(destination.ID, Witness.DestinationOwnerId,
					StringComparison.Ordinal)
				&& string.Equals(Z.ZoneID, Witness.ZoneId, StringComparison.Ordinal)
				&& string.Equals(item.ID, Witness.OutputId, StringComparison.Ordinal)
				&& string.Equals(item.Blueprint, Witness.Blueprint, StringComparison.Ordinal)
				&& item.Count == Witness.Count && item.InInventory == destination
				&& destination.Inventory.Objects.Contains(item)
				&& string.Equals(item.GetStringProperty(MaterialProperty), Witness.Marker,
					StringComparison.Ordinal);
		}

		private static bool ExactCreatedMaterial(MaterialWitness Witness)
		{
			if (!ExactMaterialReceipt(Witness)) return false;
			GameObject item = Witness.Item;
			return GameObject.Validate(item) && item.InInventory == null
				&& item.CurrentCell == null && string.Equals(item.ID, Witness.OutputId,
					StringComparison.Ordinal)
				&& string.Equals(item.Blueprint, Witness.Blueprint, StringComparison.Ordinal)
				&& item.Count == Witness.Count && string.Equals(item.GetStringProperty(
					MaterialProperty), Witness.Marker, StringComparison.Ordinal);
		}

		private static bool ExactCreatedMaterials(List<MaterialWitness> Witnesses,
			int Start)
		{
			if (Witnesses == null || Start < 0 || Start > Witnesses.Count) return false;
			for (int i = Start; i < Witnesses.Count; i++)
				if (!ExactCreatedMaterial(Witnesses[i])) return false;
			return true;
		}

		private static bool TryCaptureInventory(TradePhysicalFrame Physical,
			GameObject Owner, Zone Z, out InventoryWitness Witness)
		{
			Witness = null;
			if (Physical == null || !GameObject.Validate(Owner) || Owner.CurrentZone != Z
				|| Owner.CurrentCell == null || Owner.CurrentCell.ParentZone != Z
				|| Owner.GetIntProperty(KingdomMaterials.StockpileProperty) != 1
				|| Owner.Inventory == null || Owner.Inventory.Objects == null
				|| !ReferenceEquals(Owner.GetPart<Inventory>(), Owner.Inventory)) return false;
			for (int i = 0; i < Physical.Inventories.Count; i++)
			{
				InventoryWitness existing = Physical.Inventories[i];
				if (!ReferenceEquals(existing.Owner, Owner)) continue;
				if (!ExactInventory(existing, Z)) return false;
				Witness = existing;
				return true;
			}
			Witness = new InventoryWitness
			{
				Owner = Owner,
				Inventory = Owner.Inventory,
				Objects = Owner.Inventory.Objects,
				Rows = Owner.Inventory.Objects.ToArray()
			};
			Physical.Inventories.Add(Witness);
			return ExactInventory(Witness, Z);
		}

		private static bool ExactInventory(InventoryWitness Witness, Zone Z)
		{
			if (Witness == null || !GameObject.Validate(Witness.Owner)
				|| Witness.Owner.CurrentZone != Z || Witness.Owner.CurrentCell == null
				|| Witness.Owner.CurrentCell.ParentZone != Z
				|| Witness.Owner.GetIntProperty(KingdomMaterials.StockpileProperty) != 1
				|| !ReferenceEquals(Witness.Owner.Inventory, Witness.Inventory)
				|| !ReferenceEquals(Witness.Owner.GetPart<Inventory>(), Witness.Inventory)
				|| Witness.Inventory == null || Witness.Inventory.ParentObject != Witness.Owner
				|| !ReferenceEquals(Witness.Inventory.Objects, Witness.Objects)
				|| Witness.Objects == null || Witness.Rows == null
				|| Witness.Objects.Count != Witness.Rows.Length) return false;
			for (int i = 0; i < Witness.Rows.Length; i++)
				if (!ReferenceEquals(Witness.Objects[i], Witness.Rows[i])) return false;
			return true;
		}

		private static bool ExactMaterialWitness(MaterialWitness Witness, Zone Z)
		{
			return ExactMaterialReceipt(Witness)
				&& ExactLoadedTopology(Witness.Topology)
				&& ExactInventory(Witness.Inventory, Z)
				&& ReferenceEquals(Witness.Destination, Witness.Inventory.Owner)
				&& ExactMaterial(Witness, Z);
		}

		private static bool ExactPhysicalWithInventoryAppend(TradeLiveFrame Frame,
			KingdomTradeOperation Operation, Zone Z, InventoryWitness Target,
			MaterialWitness Added)
		{
			TradePhysicalFrame physical = Frame?.Physical;
			if (physical == null || Target == null || Added == null
				|| !ReferenceEquals(Added.Inventory, Target) || Target.Objects == null
				|| Target.Rows == null || Target.Objects.Count != Target.Rows.Length + 1)
				return false;
			if (!GameObject.Validate(Target.Owner) || Target.Owner.CurrentZone != Z
				|| Target.Owner.CurrentCell == null || Target.Owner.CurrentCell.ParentZone != Z
				|| Target.Owner.GetIntProperty(KingdomMaterials.StockpileProperty) != 1
				|| !ReferenceEquals(Target.Owner.Inventory, Target.Inventory)
				|| !ReferenceEquals(Target.Owner.GetPart<Inventory>(), Target.Inventory)
				|| !ReferenceEquals(Target.Inventory.Objects, Target.Objects)) return false;
			for (int i = 0; i < Target.Rows.Length; i++)
				if (!ReferenceEquals(Target.Objects[i], Target.Rows[i])) return false;
			if (!ReferenceEquals(Target.Objects[Target.Rows.Length], Added.Item)
				|| !ReferenceEquals(Added.Destination, Target.Owner)
				|| !ExactMaterial(Added, Z)
				|| CountMarker(Z, Added.Marker) != 1) return false;
			if (physical.StoreList != null
				&& !ExactWaterFrame(physical.Survey, physical.StoreList, physical.StoreRows)) return false;
			for (int i = 0; i < physical.Water.Count; i++)
			{
				WaterWitness water = physical.Water[i];
				if (water.Leg.State != KingdomTradePhysicalState.Prepared
					&& water.Leg.State != KingdomTradePhysicalState.Proved) return false;
				if (!ExactWaterWitness(water, Z,
					water.Leg.State == KingdomTradePhysicalState.Proved)) return false;
			}
			for (int i = 0; i < physical.Inventories.Count; i++)
				if (!ReferenceEquals(physical.Inventories[i], Target)
					&& !ExactInventory(physical.Inventories[i], Z)) return false;
			for (int i = 0; i < physical.Materials.Count; i++)
				if (!ExactMaterialWitness(physical.Materials[i], Z)
					|| CountMarker(Z, physical.Materials[i].Marker) != 1) return false;
			return true;
		}

		private static GameObject[] AppendRow(GameObject[] Rows, GameObject Item)
		{
			GameObject[] next = new GameObject[Rows.Length + 1];
			Array.Copy(Rows, next, Rows.Length);
			next[Rows.Length] = Item;
			return next;
		}

		private static bool CleanupCreatedMaterials(KingdomTradeOperation Operation, Zone Z,
			TradeLiveFrame Frame, List<MaterialWitness> Witnesses)
		{
			bool exact = true;
			for (int i = 0; Witnesses != null && i < Witnesses.Count; i++)
			{
				MaterialWitness witness = Witnesses[i];
				GameObject item = witness?.Item;
				KingdomTradeMaterialOutput output = witness?.Output;
				if (output == null)
				{
					exact = false;
					continue;
				}
				output.State = KingdomTradePhysicalState.Lost;
				if (!ExactAuthority(Frame, KingdomTradePhase.ResourceIntent)
					|| !ExactPhysicalFrame(Frame, Operation, Z)
					|| !ExactCreatedMaterial(witness))
				{
					output.CleanupState = KingdomTradePhysicalState.Lost;
					exact = false;
					continue;
				}
				output.CleanupState = KingdomTradePhysicalState.CleanupIntent;
				CallbackWitness callback = CaptureCallbackWitness(Frame);
				LoadedTopologyWitness cleanupTopology = CaptureLoadedTopology();
				if (callback == null || cleanupTopology == null
					|| !ExactLoadedTopology(cleanupTopology))
				{
					output.CleanupState = KingdomTradePhysicalState.Lost;
					exact = false;
					continue;
				}
				try
				{
					item.Obliterate();
				}
				finally
				{
					BoundTradeSurvey(Z)?.ObserveCurrentTopology(witness.Inventory?.Owner);
					BoundTradeSurvey(Z)?.ObserveCurrentTopology(item);
				}
				if (!ExactCallbackWitness(Frame, callback)
					|| !ExactAuthority(Frame, KingdomTradePhase.ResourceIntent)
					|| !ExactLoadedTopology(cleanupTopology))
				{
					FailDetachedAuthority(Frame,
						"A material cleanup callback detached its official trade authority.");
					output.CleanupState = KingdomTradePhysicalState.Lost;
					return false;
				}
				output.CleanupState = output.CleanupState == KingdomTradePhysicalState.CleanupIntent
					&& ExactMaterialReceipt(witness) && !GameObject.Validate(item)
					&& ExactPhysicalFrame(Frame, Operation, Z)
					? KingdomTradePhysicalState.Proved : KingdomTradePhysicalState.Lost;
				if (output.CleanupState != KingdomTradePhysicalState.Proved) exact = false;
			}
			return exact;
		}

		private static void MarkUnplacedCleanupLost(List<MaterialWitness> Witnesses, int Start)
		{
			if (Witnesses == null) return;
			for (int i = Start; i < Witnesses.Count; i++)
			{
				KingdomTradeMaterialOutput output = Witnesses[i]?.Output;
				if (output == null) continue;
				output.State = KingdomTradePhysicalState.Lost;
				output.CleanupState = KingdomTradePhysicalState.Lost;
			}
		}

		private static int CountMarker(Zone Z, string Marker)
		{
			if (Z == null || string.IsNullOrEmpty(Marker)) return 0;
			KingdomSurvey survey = BoundTradeSurvey(Z);
			IList<GameObject> objects;
			if (survey == null || !survey.TryLoaded(out objects) || objects == null)
				return int.MaxValue;
			int count = 0;
			for (int i = 0; i < objects.Count; i++)
				if (GameObject.Validate(objects[i]) && string.Equals(
					objects[i].GetStringProperty(MaterialProperty),
					Marker, StringComparison.Ordinal)) count++;
			return count;
		}

		private static bool TryMaterialClaim(string Claim, out int[] Amounts)
		{
			Amounts = null;
			if (string.IsNullOrEmpty(Claim)
				|| Claim.Length > KingdomTradeRules.MaxClaimChars) return false;
			int separators = 0;
			for (int i = 0; i < Claim.Length; i++)
				if (Claim[i] == '|') separators++;
			if (separators != KingdomMaterialRules.MaterialCount - 1) return false;
			string[] rows = Claim.Split('|');
			if (rows.Length != KingdomMaterialRules.MaterialCount) return false;
			Amounts = new int[rows.Length];
			for (int i = 0; i < rows.Length; i++)
			{
				if (rows[i].Length == 0 || rows[i].Length > 10
					|| !int.TryParse(rows[i], global::System.Globalization.NumberStyles.None,
					global::System.Globalization.CultureInfo.InvariantCulture, out Amounts[i])
					|| Amounts[i] < 0 || Amounts[i].ToString(
						global::System.Globalization.CultureInfo.InvariantCulture) != rows[i]) return false;
			}
			return true;
		}

	}
}
