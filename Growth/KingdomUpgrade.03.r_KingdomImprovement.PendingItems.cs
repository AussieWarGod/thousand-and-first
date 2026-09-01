using System;
using XRL;
using XRL.World;

using ThousandAndFirst;

namespace XRL.World.Parts
{
	public partial class r_KingdomImprovement
	{
		private static bool ResumePendingItem(GameObject Source, GameObject Target, Cell Where,
			r_KingdomImprovement Receipt)
		{
			if (Receipt.HandoverItemPhase < 1 || Receipt.HandoverItemPhase > 4
				|| !BoundedIdentity(Receipt.HandoverItemId)
				|| string.IsNullOrEmpty(Receipt.HandoverItemBlueprint)
				|| Receipt.HandoverItemBlueprint.Length > 256
				|| Receipt.HandoverItemCount <= 0
				|| !BoundedIdentity(Receipt.HandoverItemDestinationId)
				|| Receipt.HandoverItemDestinationKind < 1
				|| Receipt.HandoverItemDestinationKind > 2
				|| Receipt.HandoverItemMovedBefore < 0
				|| Receipt.HandoverItemMovedBefore >= MaxHandoverTopologyObjects
				|| Receipt.HandoverItemMovedAfter != Receipt.HandoverItemMovedBefore + 1
				|| (Receipt.HandoverMovedItems != Receipt.HandoverItemMovedBefore
					&& Receipt.HandoverMovedItems != Receipt.HandoverItemMovedAfter))
				return FailHandover(Receipt, "Pending inventory receipt is malformed.");
			GameObject item;
			if (!TryEscrowItem(Source, Target, Where, Receipt, out item)) return false;
			if (ExactDestination(item, Target, Where, Receipt))
				return SettlePendingItem(Target, Where, Receipt, item);
			if (Receipt.HandoverItemPhase >= 3)
				return FailHandover(Receipt,
					"Count-settlement phase lost its exact destination item.");
			if (ExactItemOwner(item, Source, Receipt))
			{
				// Exact source ownership proves prior attempt had no physical effect.
				if (!RetirePendingItem(Receipt, item)) return false;
				return false;
			}
			if (ExactEnteringCell(item, Source, Target, Where, Receipt))
			{
				item.Physics.CurrentCell = null;
				ObserveHandoverMutation(Source, Target, Where, item);
				if (!ExactLooseItem(item, Receipt))
					return FailHandover(Receipt,
						"Cell-entry recovery could not restore the exact escrow item to loose state.");
			}
			if (!ExactLooseItem(item, Receipt))
				return FailHandover(Receipt, "Pending inventory item has an ambiguous owner.");
			Receipt.HandoverItemPhase = 2;
			return PlacePendingItem(Source, Target, Where, Receipt, item);
		}

		private static bool PlacePendingItem(GameObject Source, GameObject Target, Cell Where,
			r_KingdomImprovement Receipt, GameObject Item)
		{
			Inventory destination = Receipt.HandoverItemDestinationKind == 1
				? Target?.Inventory : null;
			GameObject accepted = null;
			try
			{
				if (Receipt.HandoverItemDestinationKind == 1 && destination != null)
					accepted = destination.AddObject(Item, null, Silent: true, NoStack: true);
				else if (Receipt.HandoverItemDestinationKind == 2 && Where != null)
					accepted = Where.AddObject(Item, NoStack: true, Silent: true);
				else return RestoreItem(Source, Target, Where, Receipt, Item,
					"Inventory destination disappeared before AddObject.");
			}
			catch (System.Exception ex)
			{
				ObserveHandoverMutation(Source, Target, Where, Item);
				KingdomSurvey.ObserveAddResultInActive(Source?.CurrentZone
					?? Target?.CurrentZone ?? Where?.ParentZone, Item, accepted);
				if (!ReproveManifestAfterCallback(Source, Target, Where, Receipt)) return false;
				if (ExactHandoverObjects(Source, Target, Receipt)
					&& (Receipt.HandoverItemDestinationKind != 1
						|| ReferenceEquals(destination, Target.Inventory))
					&& ExactDestination(Item, Target, Where, Receipt))
					return SettlePendingItem(Target, Where, Receipt, Item);
				return RestoreItem(Source, Target, Where, Receipt, Item,
					"Inventory AddObject threw: " + ex.Message);
			}
			ObserveHandoverMutation(Source, Target, Where, Item);
			KingdomSurvey.ObserveAddResultInActive(Source?.CurrentZone
				?? Target?.CurrentZone ?? Where?.ParentZone, Item, accepted);
			if (!ReproveManifestAfterCallback(Source, Target, Where, Receipt)) return false;
			if (!ExactHandoverObjects(Source, Target, Receipt))
				return FailHandover(Receipt, "Inventory endpoint changed during AddObject callback.");
			if (Receipt.HandoverItemDestinationKind == 1
				&& !ReferenceEquals(destination, Target.Inventory))
				return FailHandover(Receipt,
					"Inventory AddObject replaced its exact destination inventory.");
			if (ExactDestination(Item, Target, Where, Receipt))
				return SettlePendingItem(Target, Where, Receipt, Item);
			return RestoreItem(Source, Target, Where, Receipt, Item,
				accepted == null ? "Inventory AddObject returned null without an exact effect."
					: ReferenceEquals(accepted, Item)
						? "Inventory destination did not retain exact item ownership."
						: "Inventory AddObject returned a foreign object without an exact effect.");
		}

		private static bool RestoreItem(GameObject Source, GameObject Target, Cell Where,
			r_KingdomImprovement Receipt, GameObject Item, string Failure)
		{
			if (ExactDestination(Item, Target, Where, Receipt))
				return SettlePendingItem(Target, Where, Receipt, Item);
			// Cell.AddObject runs EnvironmentalUpdate after assigning CurrentCell but before
			// Cell.Objects.Add. That exact escrow topology is recoverable: detach only the frozen
			// reference, prove it loose, then restore it to its exact source inventory.
			if (ExactEnteringCell(Item, Source, Target, Where, Receipt))
			{
				Item.Physics.CurrentCell = null;
				ObserveHandoverMutation(Source, Target, Where, Item);
				if (!ExactLooseItem(Item, Receipt))
					return FailHandover(Receipt,
						Failure + " Cell-entry recovery could not prove an exact loose item.");
			}
			if (!ExactLooseItem(Item, Receipt) && !ExactItemOwner(Item, Source, Receipt))
				return FailHandover(Receipt, Failure + " Exact recovery source is unavailable.");
			if (!ExactItemOwner(Item, Source, Receipt))
			{
				if (Source.Inventory == null)
					return FailHandover(Receipt,
						Failure + " Exact source inventory no longer exists.");
				GameObject restored = null;
				try { restored = Source.Inventory.AddObject(Item, null,
					Silent: true, NoStack: true); }
				catch (System.Exception ex)
				{
					ObserveHandoverMutation(Source, Target, Where, Item);
					KingdomSurvey.ObserveAddResultInActive(Source.CurrentZone, Item, restored);
					if (!ReproveManifestAfterCallback(Source, Target, Where, Receipt)) return false;
					return FailHandover(Receipt, Failure + " Recovery threw: " + ex.Message);
				}
				ObserveHandoverMutation(Source, Target, Where, Item);
				KingdomSurvey.ObserveAddResultInActive(Source.CurrentZone, Item, restored);
				if (!ReproveManifestAfterCallback(Source, Target, Where, Receipt)) return false;
			}
			if (!ExactItemOwner(Item, Source, Receipt))
				return FailHandover(Receipt, Failure + " Exact source recovery failed.");
			if (!RetirePendingItem(Receipt, Item)) return false;
			Receipt.HandoverFailure = Failure;
			return false;
		}

		private static bool ExactHandoverObjects(GameObject Source, GameObject Target,
			r_KingdomImprovement Receipt)
		{
			if (!GameObject.Validate(Source) || !GameObject.Validate(Target) || Receipt == null
				|| Source.GetPart<r_KingdomImprovement>() != Receipt
				|| Source.CurrentCell == null || Target.CurrentCell != Source.CurrentCell) return false;
			return ExactHandoverEndpointReceipt(Source, Target, Receipt)
				&& ExactHandoverAuthority(Source, Target, Receipt);
		}

		internal static bool TryPublishHandoverEndpoints(GameObject Source, GameObject Target,
			r_KingdomImprovement Receipt, string ConstructionReceipt)
		{
			if (!GameObject.Validate(Source) || !GameObject.Validate(Target) || Receipt == null
				|| !BoundedIdentity(Source.ID) || !BoundedIdentity(Target.ID)
				|| !BoundedIdentity(ConstructionReceipt))
				return FailHandover(Receipt, "Handover endpoint identity is absent or unbounded.");
			GameObject owner = Receipt.ParentObject;
			string schemaKey = HandoverPrefix + "EndpointSchema";
			int schema = Receipt.HandoverInt("EndpointSchema");
			bool exactPrefix = owner != null && !owner.HasStringProperty(schemaKey)
				&& schema >= 0 && schema <= 1
				&& ExactOrAbsentText(owner, HandoverPrefix + "SourceId", Source.ID)
				&& ExactOrAbsentText(owner, HandoverPrefix + "TargetId", Target.ID)
				&& ExactOrAbsentText(owner, HandoverPrefix + "ConstructionReceipt",
					ConstructionReceipt);
			if (!exactPrefix) return FailHandover(Receipt,
				"Handover endpoint receipt carries a foreign or malformed value.");
			if (schema == 0)
			{
				try
				{
					Receipt.HandoverSourceId = Source.ID;
					Receipt.HandoverTargetId = Target.ID;
					Receipt.HandoverConstructionReceipt = ConstructionReceipt;
					Receipt.HandoverInt("EndpointSchema", 1);
				}
				catch (Exception exception)
				{
					Receipt.HandoverFailure = "Handover endpoint publication remains retryable: "
						+ exception.Message;
					return false;
				}
			}
			return ExactHandoverEndpointReceipt(Source, Target, Receipt) || FailHandover(Receipt,
				"Committed handover endpoint receipt is incomplete or changed.");
		}
		private static bool ExactHandoverEndpointReceipt(GameObject Source, GameObject Target,
			r_KingdomImprovement Receipt)
		{
			GameObject owner = Receipt?.ParentObject;
			return owner != null && owner.HasIntProperty(HandoverPrefix + "EndpointSchema")
				&& Receipt.HandoverInt("EndpointSchema") == 1
				&& !owner.HasStringProperty(HandoverPrefix + "EndpointSchema")
				&& !owner.HasIntProperty(HandoverPrefix + "SourceId")
				&& !owner.HasIntProperty(HandoverPrefix + "TargetId")
				&& !owner.HasIntProperty(HandoverPrefix + "ConstructionReceipt")
				&& Receipt.HandoverSourceId == Source.IDIfAssigned
				&& Receipt.HandoverTargetId == Target.IDIfAssigned
				&& BoundedIdentity(Receipt.HandoverConstructionReceipt);
		}
		private static bool ExactOrAbsentText(GameObject Owner, string Property, string Expected)
		{
			return !Owner.HasIntProperty(Property) && (!Owner.HasStringProperty(Property)
				|| Owner.GetStringProperty(Property) == Expected);
		}
		/// <summary>Reclassifies only the exact roots touched by a durable handover. A loose or
		/// callback-entering item is deliberately absent from the loaded index; a settled ground
		/// item becomes its own root, while an inventory item is recovered through its owner branch.</summary>
		private static void ObserveHandoverMutation(GameObject Source, GameObject Target,
			Cell Where, GameObject Item)
		{
			Zone zone = Source?.CurrentZone ?? Target?.CurrentZone ?? Where?.ParentZone;
			KingdomSurvey survey = KingdomSurvey.ActiveFor(zone);
			if (survey == null) return;
			bool exactGround = GameObject.Validate(Item) && Item.CurrentCell != null
				&& Item.CurrentCell.ParentZone == zone
				&& ReferenceCount(Item.CurrentCell.GetObjects(), Item) == 1;
			if (Item != null && !exactGround) survey.ObserveRemoved(Item);
			if (GameObject.Validate(Source) && Source.CurrentZone == zone)
				survey.ObserveChanged(Source);
			if (!ReferenceEquals(Target, Source) && GameObject.Validate(Target)
				&& Target.CurrentZone == zone) survey.ObserveChanged(Target);
			if (exactGround) survey.ObserveChanged(Item);
		}
		private static bool ExactHandoverAuthority(GameObject Source, GameObject Target,
			r_KingdomImprovement Receipt)
		{
			string frozen = Receipt?.HandoverConstructionReceipt;
			string sourceReceipt = Source?.GetStringProperty(KingdomConstruction.ReceiptProperty);
			string targetReceipt = Target?.GetStringProperty(KingdomConstruction.ReceiptProperty);
			if (string.IsNullOrEmpty(frozen)) return false;
			if (!BoundedIdentity(frozen) || sourceReceipt != frozen || targetReceipt != frozen)
				return false;
			KingdomConstructionJob job;
			Zone zone = Source.CurrentZone;
			KingdomSystem system = The.Game == null
				? null : The.Game.RequireSystem<KingdomSystem>();
			GameObject exactSource;
			GameObject exactTarget;
			return zone != null && Target.CurrentZone == zone
				&& KingdomConstruction.TryFind(frozen, out job)
				&& job.Route == KingdomConstructionRoute.Improvement
				&& !KingdomConstructionRules.IsTerminal(job.Phase)
				&& Receipt.Working && !string.IsNullOrEmpty(Receipt.SuccessorBlueprint)
				&& Receipt.SuccessorBlueprint.Length <= 256
				&& !string.IsNullOrEmpty(Receipt.SuccessorKey)
				&& Receipt.SuccessorKey.Length <= KingdomConstructionRules.MaxTargetChars
				&& Source.GetIntProperty(KingdomUpgrade.BuiltProperty) == 1
				&& Target.GetIntProperty(KingdomUpgrade.BuiltProperty) == 1
				&& Target.Blueprint == Receipt.SuccessorBlueprint
				&& Target.GetStringProperty(KingdomUpgrade.BuildKeyProperty)
					== Receipt.SuccessorKey
				&& job.SubjectId == Source.IDIfAssigned && job.SourceId == Source.IDIfAssigned
				&& job.OutputId == Target.IDIfAssigned && job.TargetKey == Receipt.SuccessorKey
				&& Source.CurrentCell == zone.GetCell(job.X, job.Y)
				&& Target.CurrentCell == Source.CurrentCell
				&& KingdomConstruction.Owns(system, zone, job)
				&& KingdomConstruction.IsCurrent(job)
				&& KingdomConstruction.FindExactId(zone, Source.IDIfAssigned, out exactSource)
					== KingdomPhysicalLookupState.Exact
				&& ReferenceEquals(exactSource, Source)
				&& KingdomConstruction.FindExactId(zone, Target.IDIfAssigned, out exactTarget)
					== KingdomPhysicalLookupState.Exact
				&& ReferenceEquals(exactTarget, Target);
		}
		private static bool ExactCleanupItemState(r_KingdomImprovement Receipt)
		{
			GameObject source = Receipt?.ParentObject;
			Zone zone = source?.CurrentZone;
			GameObject target;
			GameObject item;
			if (source?.CurrentCell == null || zone == null || Receipt.HandoverItemPhase < 1
				|| KingdomConstruction.FindExactId(zone, Receipt.HandoverTargetId, out target)
					!= KingdomPhysicalLookupState.Exact
				|| KingdomConstruction.FindExactId(zone, Receipt.HandoverItemId, out item)
					!= KingdomPhysicalLookupState.Exact || !ExactHandoverObjects(source, target, Receipt)
				|| !GameObject.Validate(item) || item.Physics == null
				|| item.Blueprint != Receipt.HandoverItemBlueprint
				|| item.Count != Receipt.HandoverItemCount) return false;
			if (Receipt.HandoverItemPhase <= 2)
				return item.Physics.InInventory == source
					&& ReferenceCount(source.Inventory?.Objects, item) == 1;
			if (Receipt.HandoverItemDestinationKind == 1)
				return item.Physics.InInventory == target
					&& ReferenceCount(target.Inventory?.Objects, item) == 1;
			return Receipt.HandoverItemDestinationKind == 2 && item.Physics.InInventory == null
				&& item.CurrentCell == source.CurrentCell
				&& ReferenceCount(source.CurrentCell.GetObjects(), item) == 1;
		}
	}
}
