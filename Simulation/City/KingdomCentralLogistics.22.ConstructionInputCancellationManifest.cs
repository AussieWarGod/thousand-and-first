using System.Collections.Generic;

using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomCentralLogistics
	{
		/// <summary>Proves the complete phase-aware child graph before cancellation may
		/// move a carrier. Expected receipt cargo may be in the carrier, already returned
		/// on this attended source, or uniquely graveyarded after a proved callback.</summary>
		private static bool ExactConstructionInputCancellationManifest(GameObject body,
			KingdomConstructionJob ownerJob, KingdomConstructionInputReceipt receipt,
			int childOrdinal, Zone sourceZone)
		{
			if (!GameObject.Validate(body) || body.Inventory == null || receipt == null
				|| childOrdinal < 0 || childOrdinal >= receipt.ChildCount
				|| !KingdomOrdinaryCustody.TryCollect(body,
					out List<GameObject> graph, out string _)) return false;
			KingdomConstructionInputChild child = receipt.ChildAt(childOrdinal);
			if (graph.Count > child.CargoCount + 1) return false;
			for (int i = 1; i < graph.Count; i++)
			{
				GameObject item = graph[i];
				int matches = 0;
				for (int j = child.CargoStart; j < child.CargoStart + child.CargoCount; j++)
					if (CancellationCargoIdentity(receipt.CargoAt(j), item)) matches++;
				if (matches != 1 || !ReferenceEquals(item.InInventory, body)
					|| DirectManifestReferenceCount(body, item) != 1) return false;
			}
			for (int i = child.CargoStart; i < child.CargoStart + child.CargoCount; i++)
			{
				KingdomConstructionInputCargoLine cargo = receipt.CargoAt(i);
				GameObject exact = null;
				int matches = 0;
				for (int j = 1; j < graph.Count; j++)
					if (CancellationCargoIdentity(cargo, graph[j]))
					{ exact = graph[j]; matches++; }
				if (matches > 1 || matches == 1
					&& !ExactCancellationManifestItem(ownerJob, receipt, cargo, exact)) return false;
				bool carrierCustody = cargo.CustodyTopology
						== KingdomConstructionInputTopology.CarrierInventory
					|| cargo.CustodyTopology == KingdomConstructionInputTopology.LandingEscrow;
				KingdomConstructionInputSourceLine source =
					receipt.SourceAt(cargo.SourceLineOrdinal);
				bool cancellationWriteCut = cargo.Phase
						== KingdomConstructionInputCargoPhase.ReleaseIntent
					|| cargo.Phase == KingdomConstructionInputCargoPhase.CompensationIntent
					|| cargo.Kind == KingdomConstructionInputKind.Water
						&& cargo.Phase == KingdomConstructionInputCargoPhase.CreateIntent
						&& source.Phase == KingdomConstructionInputSourcePhase.Reserved
					|| cargo.Kind != KingdomConstructionInputKind.Water
						&& cargo.Phase == KingdomConstructionInputCargoPhase.PickupIntent
						&& source.Phase == KingdomConstructionInputSourcePhase.TransferIntent;
				if (matches == 1)
				{
					if (!carrierCustody && !cancellationWriteCut) return false;
					continue;
				}
				if (string.IsNullOrEmpty(cargo.ObjectId))
				{
					if (cargo.Kind != KingdomConstructionInputKind.Water
						|| cargo.Phase != KingdomConstructionInputCargoPhase.CreateIntent
						&& cargo.Phase != KingdomConstructionInputCargoPhase.ReleaseIntent
						&& cargo.Phase != KingdomConstructionInputCargoPhase.Released)
						return false;
					continue;
				}
				bool returned = cargo.Phase == KingdomConstructionInputCargoPhase.CompensationIntent
					|| cargo.Phase == KingdomConstructionInputCargoPhase.ReleaseIntent
					|| cargo.Phase == KingdomConstructionInputCargoPhase.Compensated
					|| cargo.Phase == KingdomConstructionInputCargoPhase.Released;
				if (returned)
				{
					if (ExactCancellationGraveyardWater(ownerJob, receipt, cargo)
						|| ExactReturnedMaterial(sourceZone, receipt, cargo, ownerJob)) continue;
					return false;
				}
				if (!carrierCustody) continue;
				return false;
			}
			return true;
		}

		private static bool ExactCancellationGraveyardWater(KingdomConstructionJob ownerJob,
			KingdomConstructionInputReceipt receipt, KingdomConstructionInputCargoLine cargo)
		{
			if (cargo == null || cargo.Kind != KingdomConstructionInputKind.Water
				|| string.IsNullOrEmpty(cargo.ObjectId)
				|| XRL.The.ZoneManager?.Graveyard?.Objects == null) return false;
			GameObject exact = null; int matches = 0;
			for (int i = 0; i < XRL.The.ZoneManager.Graveyard.Objects.Count; i++)
			{
				GameObject candidate = XRL.The.ZoneManager.Graveyard.Objects[i];
				if (candidate?.IDIfAssigned != cargo.ObjectId) continue;
				exact = candidate; matches++;
			}
			LiquidVolume liquid = exact?.GetPart<LiquidVolume>();
			return matches == 1 && exact.IsInGraveyard() && exact.Blueprint == cargo.Blueprint
				&& exact.HasStringProperty(KingdomConstruction.InputMarkerProperty)
				&& !exact.HasIntProperty(KingdomConstruction.InputMarkerProperty)
				&& exact.GetStringProperty(KingdomConstruction.InputMarkerProperty)
					== cargo.CreationMarker
				&& exact.GetIntProperty("NeverStack") == 1
				&& exact.GetIntProperty(KingdomPorters.StockProperty) == 1
				&& !exact.IsImportant() && exact.Equipped == null && exact.IsTakeable()
				&& !exact.HasTag("AlwaysStack") && liquid != null && !liquid.Sealed
				&& liquid.MaxVolume == cargo.Capacity && liquid.Volume == 0
				&& KingdomOrdinaryCustody.TryProveRetiredEmpty(exact, out string _)
				&& KingdomConstruction.RetiredRoutedInputItemAuthorized(ownerJob, receipt, exact);
		}

		private static bool CancellationCargoIdentity(KingdomConstructionInputCargoLine cargo,
			GameObject item)
		{
			if (!GameObject.Validate(item) || cargo == null) return false;
			if (!string.IsNullOrEmpty(cargo.ObjectId)) return item.IDIfAssigned == cargo.ObjectId;
			return cargo.Kind == KingdomConstructionInputKind.Water
				&& item.HasStringProperty(KingdomConstruction.InputMarkerProperty)
				&& !item.HasIntProperty(KingdomConstruction.InputMarkerProperty)
				&& item.GetStringProperty(KingdomConstruction.InputMarkerProperty)
					== cargo.CreationMarker;
		}

		private static bool ExactCancellationManifestItem(KingdomConstructionJob ownerJob,
			KingdomConstructionInputReceipt receipt, KingdomConstructionInputCargoLine cargo,
			GameObject item)
		{
			string marker = cargo.Kind == KingdomConstructionInputKind.Water
				? cargo.CreationMarker : cargo.CargoKey;
			if (item.Blueprint != cargo.Blueprint || item.IsImportant() || item.Equipped != null
				|| !item.IsTakeable() || item.HasTag("AlwaysStack")
				|| !item.HasStringProperty(KingdomConstruction.InputMarkerProperty)
				|| item.HasIntProperty(KingdomConstruction.InputMarkerProperty)
				|| item.GetStringProperty(KingdomConstruction.InputMarkerProperty) != marker
				|| !KingdomConstruction.RoutedInputItemAuthorized(ownerJob, receipt, item)
				|| !KingdomPurpose.HasProtectedCargoEvidence(item)
					&& item.GetIntProperty("NeverStack") != 1) return false;
			if (cargo.Kind != KingdomConstructionInputKind.Water)
				return item.Count == cargo.Amount
					&& KingdomConstruction.TryInputClassification(item,
						out KingdomConstructionInputKind kind, out string classification)
					&& kind == cargo.Kind && classification == cargo.Classification;
			LiquidVolume liquid = item.GetPart<LiquidVolume>();
			return item.GetIntProperty(KingdomPorters.StockProperty) == 1
				&& liquid != null && !liquid.Sealed && liquid.MaxVolume == cargo.Capacity
				&& (liquid.Volume == 0 || liquid.Volume == cargo.Amount
					&& KingdomLiquids.HasFreshWater(liquid));
		}

		private static bool ExactReturnedMaterial(Zone zone,
			KingdomConstructionInputReceipt receipt, KingdomConstructionInputCargoLine cargo,
			KingdomConstructionJob ownerJob)
		{
			if (zone == null || cargo.Kind == KingdomConstructionInputKind.Water
				|| KingdomSurvey.ActiveFor(zone) == null) return false;
			KingdomConstructionInputSourceLine source = receipt.SourceAt(cargo.SourceLineOrdinal);
			GameObject holder = null; GameObject item = null; int holders = 0; int items = 0;
			if (!KingdomSurvey.ActiveFor(zone).TryLoaded(
				out System.Collections.Generic.IList<GameObject> loaded)
				|| loaded.Count > KingdomOrdinaryCustody.MaxNodes) return false;
			for (int i = 0; i < loaded.Count; i++)
			{
				if (loaded[i].IDIfAssigned == source.HolderId) { holder = loaded[i]; holders++; }
				if (loaded[i].IDIfAssigned == cargo.ObjectId) { item = loaded[i]; items++; }
			}
			if (holders != 1 || items != 1 || !GameObject.Validate(holder)
				|| !GameObject.Validate(item)) return false;
			bool terminal = (cargo.Phase == KingdomConstructionInputCargoPhase.Released
					|| cargo.Phase == KingdomConstructionInputCargoPhase.Compensated)
				&& (source.Phase == KingdomConstructionInputSourcePhase.Restored
					|| source.Phase == KingdomConstructionInputSourcePhase.Compensated);
			bool markerExact = terminal
				? !item.HasStringProperty(KingdomConstruction.InputMarkerProperty)
					&& !item.HasIntProperty(KingdomConstruction.InputMarkerProperty)
				: item.HasStringProperty(KingdomConstruction.InputMarkerProperty)
					&& !item.HasIntProperty(KingdomConstruction.InputMarkerProperty)
					&& (item.GetStringProperty(KingdomConstruction.InputMarkerProperty)
							== cargo.CargoKey
						|| source.Phase == KingdomConstructionInputSourcePhase.RestoreIntent
							&& cargo.Phase == KingdomConstructionInputCargoPhase.ReleaseIntent
							&& item.GetStringProperty(KingdomConstruction.InputMarkerProperty)
								== source.RemainderMarker);
			if (holder.Inventory == null || holder.CurrentCell != zone.GetCell(source.X, source.Y)
				|| !ReferenceEquals(item.InInventory, holder)
				|| DirectManifestReferenceCount(holder, item) != 1
				|| item.Blueprint != source.Blueprint
				|| terminal && item.Count != source.Before
				|| item.Count != source.Before
					&& (item.Count != source.Take || source.ResidualAfter <= 0
						|| source.Phase != KingdomConstructionInputSourcePhase.RestoreIntent
						&& source.Phase != KingdomConstructionInputSourcePhase.CompensationIntent)
				|| item.IsImportant() || item.Equipped != null || !item.IsTakeable()
				|| item.HasTag("AlwaysStack")
				|| !KingdomOrdinaryCustody.TryProveEmpty(item, out string _)
				|| !markerExact
				|| !KingdomConstruction.RoutedInputItemAuthorized(ownerJob, receipt, item)
				|| !KingdomPurpose.HasProtectedCargoEvidence(item)
					&& item.GetIntProperty("NeverStack") != (terminal ? 0 : 1)
				|| !KingdomConstruction.TryInputClassification(item,
					out KingdomConstructionInputKind kind, out string classification)
				|| kind != cargo.Kind || classification != cargo.Classification) return false;
			return true;
		}
	}
}
