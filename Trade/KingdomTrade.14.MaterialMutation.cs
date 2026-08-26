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
		private static void FreezeMaterials(KingdomTradeOperation Operation,
			KingdomMaterialTally Tally)
		{
			string[] rows = new string[KingdomMaterialRules.MaterialCount];
			int total = 0;
			for (int i = 0; i < rows.Length; i++)
			{
				int amount = Tally == null ? 0 : Tally.Get((KingdomMaterial)i);
				rows[i] = amount.ToString(global::System.Globalization.CultureInfo.InvariantCulture);
				total = KingdomTradeRules.SaturatingAdd(total, amount);
			}
			Operation.MaterialClaim = string.Join("|", rows);
			Operation.MaterialRequested = total;
		}

		private static bool ApplyMaterials(KingdomTradeOperation Operation, Zone Z,
			TradeLiveFrame Frame)
		{
			int[] amounts;
			if (!TryMaterialClaim(Operation.MaterialClaim, out amounts))
			{
				Quarantine(Operation,
					"The frozen material load is malformed and was not minted.");
				return false;
			}
			KingdomMaterials.MaterialStock stock = KingdomMaterials.Stock(Z);
			GameObject destination = null;
			for (int i = 0; stock != null && stock.Stockpiles != null
				&& i < stock.Stockpiles.Count; i++)
			{
				GameObject candidate = stock.Stockpiles[i];
				if (GameObject.Validate(candidate) && candidate.CurrentZone == Z
					&& candidate.Inventory != null
					&& candidate.GetIntProperty(KingdomMaterials.StockpileProperty) == 1)
				{
					destination = candidate;
					break;
				}
			}
			if (destination == null || string.IsNullOrEmpty(destination.ID))
			{
				Quarantine(Operation,
					"The material load had no exact stockpile owner and remains quarantined on the caravan.");
				return false;
			}
			GameObject resolvedDestination;
			LoadedTopologyWitness destinationTopology;
			if (ResolveLoadedObject(destination.ID, Z, out resolvedDestination,
				out destinationTopology) != LoadedObjectResolution.ExactUnique
				|| !ReferenceEquals(resolvedDestination, destination))
				return QuarantineFalse(Operation,
					"The material destination id was not exact-unique on active settlement ground.");
			if (Frame.Physical == null) Frame.Physical = new TradePhysicalFrame();
			InventoryWitness inventory;
			if (!TryCaptureInventory(Frame.Physical, destination, Z, out inventory))
				return QuarantineFalse(Operation,
					"The material destination inventory could not be captured exactly.");
			List<GameObject> made = new List<GameObject>();
			List<KingdomTradeMaterialOutput> receipts =
				new List<KingdomTradeMaterialOutput>();
			List<MaterialWitness> candidates = new List<MaterialWitness>();
			Operation.Phase = KingdomTradePhase.ResourceIntent;
			for (int i = 0; i < amounts.Length; i++)
			{
				if (amounts[i] <= 0) continue;
				string blueprint = KingdomMaterials.MaterialBlueprints[i];
				KingdomTradeMaterialOutput output = new KingdomTradeMaterialOutput
				{
					Marker = KingdomTradeRules.MaterialMarker(Operation.Id, i),
					Blueprint = blueprint,
					Count = amounts[i],
					DestinationOwnerId = destination.ID,
					ZoneId = Z.ZoneID,
					State = KingdomTradePhysicalState.CreateIntent,
					CleanupState = KingdomTradePhysicalState.None
				};
				Operation.MaterialOutputs.Add(output);
				RefreshReceiptRows(Frame);
				if (!ExactAuthority(Frame, KingdomTradePhase.ResourceIntent)
					|| !ExactPhysicalFrame(Frame, Operation, Z)
					|| !ExactCreatedMaterials(candidates, 0))
				{
					output.State = KingdomTradePhysicalState.Lost;
					CleanupCreatedMaterials(Operation, Z, Frame, candidates);
					return QuarantineFalse(Operation,
						"The material frame changed before its creation callback.");
					}
					CallbackWitness callback = CaptureCallbackWitness(Frame);
					LoadedTopologyWitness createTopology = CaptureLoadedTopology();
					if (callback == null || createTopology == null
						|| !ExactLoadedTopology(createTopology))
				{
					output.State = KingdomTradePhysicalState.Lost;
					return QuarantineFalse(Operation, "Material creation frame could not be frozen.");
				}
				GameObject item = GameObject.Create(blueprint);
				if (!ExactCallbackWitness(Frame, callback)
					|| !ExactAuthority(Frame, KingdomTradePhase.ResourceIntent)
					|| !ExactLoadedTopology(createTopology))
					return FailDetachedAuthority(Frame,
						"A material creation callback detached its official trade authority.");
				if (!ReferenceEquals(Operation.MaterialOutputs[
						Operation.MaterialOutputs.Count - 1], output)
					|| output.State != KingdomTradePhysicalState.CreateIntent
					|| !string.Equals(output.Marker,
						KingdomTradeRules.MaterialMarker(Operation.Id, i), StringComparison.Ordinal)
					|| !string.Equals(output.Blueprint, blueprint, StringComparison.Ordinal)
					|| output.Count != amounts[i]
					|| !string.Equals(output.DestinationOwnerId, destination.ID,
						StringComparison.Ordinal)
					|| !string.Equals(output.ZoneId, Z.ZoneID, StringComparison.Ordinal)
					|| !ExactPhysicalFrame(Frame, Operation, Z)
					|| !ExactCreatedMaterials(candidates, 0)
					|| !GameObject.Validate(item) || string.IsNullOrEmpty(item.ID)
					|| !string.Equals(item.Blueprint, blueprint, StringComparison.Ordinal))
				{
					output.State = KingdomTradePhysicalState.Lost;
					CleanupCreatedMaterials(Operation, Z, Frame, candidates);
					return QuarantineFalse(Operation,
						"A material output blueprint could not be bound before placement.");
				}
				item.Count = amounts[i];
				item.SetStringProperty(MaterialProperty, output.Marker);
				output.OutputId = item.ID;
				output.State = KingdomTradePhysicalState.Prepared;
				MaterialWitness witness = CaptureMaterialWitness(output, item,
					destination, inventory);
				if (!ExactCreatedMaterial(witness))
				{
					output.State = KingdomTradePhysicalState.Lost;
					if (witness != null) candidates.Add(witness);
					CleanupCreatedMaterials(Operation, Z, Frame, candidates);
					return QuarantineFalse(Operation,
						"A created material output changed before its placement intent.");
				}
				made.Add(item);
				receipts.Add(output);
				candidates.Add(witness);
			}
			for (int i = 0; i < made.Count; i++)
			{
				KingdomTradeMaterialOutput output = receipts[i];
				GameObject item = made[i];
				MaterialWitness witness = candidates[i];
				if (!ExactAuthority(Frame, KingdomTradePhase.ResourceIntent)
					|| !ExactPhysicalFrame(Frame, Operation, Z)
					|| !ExactCreatedMaterials(candidates, i)
					|| !ExactInventory(inventory, Z))
				{
					output.State = KingdomTradePhysicalState.Lost;
					MarkUnplacedCleanupLost(candidates, i);
					ReconcilePhysicalFailure(Frame, Operation, Z,
						"The material frame changed before its AddObject callback.");
					return false;
				}
				output.State = KingdomTradePhysicalState.Intent;
				CallbackWitness callback = CaptureCallbackWitness(Frame);
				LoadedTopologyWitness addTopology = CaptureLoadedTopology();
				if (callback == null || addTopology == null
					|| !ExactLoadedTopology(addTopology))
				{
					output.State = KingdomTradePhysicalState.Lost;
					return QuarantineFalse(Operation, "Material AddObject frame could not be frozen.");
				}
				GameObject added = null;
				try
				{
					added = inventory.Inventory.AddObject(item, null, Silent: true);
				}
				finally
				{
					BoundTradeSurvey(Z)?.ObserveCurrentTopology(inventory.Owner);
					KingdomSurvey.ObserveAddResultInActive(Z, item, added);
				}
				if (!ExactCallbackWitness(Frame, callback)
					|| !ExactAuthority(Frame, KingdomTradePhase.ResourceIntent)
					|| !ExactLoadedTopologyWithDelta(addTopology, item, null,
						inventory.Owner, false))
					return FailDetachedAuthority(Frame,
						"A material AddObject callback detached its official trade authority.");
				RefreshPhysicalTopologies(Frame.Physical);
				witness.Topology = CaptureLoadedTopology();
				if (!ReferenceEquals(added, item) || output.State != KingdomTradePhysicalState.Intent
					|| !ExactPhysicalWithInventoryAppend(Frame, Operation, Z,
						inventory, witness)
					|| !ExactCreatedMaterials(candidates, i + 1))
				{
					output.State = KingdomTradePhysicalState.Lost;
					MarkUnplacedCleanupLost(candidates, i + 1);
					ReconcilePhysicalFailure(Frame, Operation, Z,
						"A material AddObject callback did not leave the exact output at its bound owner.");
					return false;
				}
				inventory.Rows = AppendRow(inventory.Rows, item);
				Frame.Physical.Materials.Add(witness);
				output.State = KingdomTradePhysicalState.Proved;
				Operation.MaterialProved = KingdomTradeRules.SaturatingAdd(
					Operation.MaterialProved, witness.Count);
				if (!ExactPhysicalFrame(Frame, Operation, Z))
				{
					ReconcilePhysicalFailure(Frame, Operation, Z,
						"The material frame changed before its durable checkpoint.");
					return false;
				}
			}
			return true;
		}

	}
}
