using System;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitectureStamper
	{
		/// <summary>
		/// Completes an interrupted retained-component retag. Every mutable scalar may carry only
		/// its predecessor or successor value; schema is republished last.
		/// </summary>
		private static bool TryRetagUpgradeComponent(GameObject Owner, GameObject Item, Zone Z,
			KingdomArchitectureIntent Before, KingdomArchitectureIntent After, string Lot,
			ArchitecturePlacement BeforePlacement, ArchitecturePlacement AfterPlacement,
			string Id, out string Failure)
		{
			Failure = null;
			if (!TryExactRetagPrefix(Item, Z, Before, After, Lot, BeforePlacement,
				AfterPlacement, Id, out Failure))
				return UpgradeQuarantine(Owner, "retained authored slot "
					+ BeforePlacement.Slot + " carries a third, duplicate, or moved value during retag: "
					+ Failure, out Failure);
			try
			{
				Item.RemoveIntProperty(ComponentSchemaProperty);
				Item.SetIntProperty(KingdomPlots.PlotPartProperty,
					AfterPlacement.ExistingAuthority ? 0 : 1);
				Item.SetStringProperty(KingdomPlots.PlotIdProperty, Lot);
				Item.SetStringProperty(ComponentSlotProperty, AfterPlacement.Slot);
				Item.SetIntProperty(ComponentLayerProperty, (int)AfterPlacement.Layer);
				Item.SetStringProperty(ComponentAnchorProperty, AfterPlacement.StatefulAnchor,
					RemoveIfNull: true);
				Item.SetStringProperty(ComponentHashProperty, After.SnapshotHash);
				Item.SetStringProperty(ComponentTokenProperty,
					ComponentToken(Lot, After.SnapshotHash, AfterPlacement));
				Item.SetIntProperty(ComponentExistingProperty,
					AfterPlacement.ExistingAuthority ? 1 : 0);
				Item.SetIntProperty(ComponentCarriedProperty, 1);
				Item.SetIntProperty(r_KingdomScaffold.PendingImprovementSuccessorProperty, 1);
				Item.SetIntProperty(ComponentSchemaProperty, ComponentSchema);
				KingdomSurvey.ObserveChangedInActive(Z, Item);
			}
			catch (Exception exception)
			{
				Failure = "retained authored slot " + BeforePlacement.Slot
					+ " retag remains retryable: " + exception.Message;
				return false;
			}
			return ExactComponent(Owner, Item, Z, After, Lot, AfterPlacement, Id)
				&& Item.GetIntProperty(ComponentCarriedProperty) == 1
				|| UpgradeQuarantine(Owner, "retained authored slot "
					+ BeforePlacement.Slot + " did not settle after retag", out Failure);
		}

		/// <summary>Exact old/new scalar grammar for a physically unique retained output.</summary>
		private static bool TryExactRetagPrefix(GameObject Item, Zone Z,
			KingdomArchitectureIntent Before, KingdomArchitectureIntent After, string Lot,
			ArchitecturePlacement BeforePlacement, ArchitecturePlacement AfterPlacement,
			string Id, out string Failure)
		{
			Failure = null;
			if (!GameObject.Validate(Item) || Item.IDIfAssigned != Id || Item.CurrentZone != Z
				|| Item.Blueprint != BeforePlacement.Blueprint
				|| BeforePlacement.Blueprint != AfterPlacement.Blueprint
				|| KingdomConstruction.FindExactId(Z, Id, out GameObject byId)
					!= KingdomPhysicalLookupState.Exact || !ReferenceEquals(byId, Item)
				|| !SameUpgradeWorldCell(Z, Item, Before, After, BeforePlacement, AfterPlacement)
				|| !OldOrNewInt(Item, KingdomPlots.PlotPartProperty,
					BeforePlacement.ExistingAuthority ? 0 : 1,
					AfterPlacement.ExistingAuthority ? 0 : 1)
				|| !OldOrNewString(Item, KingdomPlots.PlotIdProperty, Lot, Lot)
				|| !OldOrNewString(Item, ComponentSlotProperty, BeforePlacement.Slot,
					AfterPlacement.Slot)
				|| !OldOrNewInt(Item, ComponentLayerProperty, (int)BeforePlacement.Layer,
					(int)AfterPlacement.Layer)
				|| !OldOrNewString(Item, ComponentAnchorProperty,
					BeforePlacement.StatefulAnchor, AfterPlacement.StatefulAnchor)
				|| !OldOrNewString(Item, ComponentHashProperty, Before.SnapshotHash,
					After.SnapshotHash)
				|| !OldOrNewString(Item, ComponentTokenProperty,
					ComponentToken(Lot, Before.SnapshotHash, BeforePlacement),
					ComponentToken(Lot, After.SnapshotHash, AfterPlacement))
				|| !OldOrNewInt(Item, ComponentExistingProperty,
					BeforePlacement.ExistingAuthority ? 1 : 0,
					AfterPlacement.ExistingAuthority ? 1 : 0)
				|| Item.HasStringProperty(ComponentSchemaProperty)
				|| (Item.HasIntProperty(ComponentSchemaProperty)
					&& Item.GetIntProperty(ComponentSchemaProperty) != ComponentSchema)
				|| Item.HasStringProperty(ComponentCarriedProperty)
				|| (Item.HasIntProperty(ComponentCarriedProperty)
					&& Item.GetIntProperty(ComponentCarriedProperty) != 1)
				|| Item.HasStringProperty(r_KingdomScaffold.PendingImprovementSuccessorProperty)
				|| (Item.HasIntProperty(r_KingdomScaffold.PendingImprovementSuccessorProperty)
					&& Item.GetIntProperty(
						r_KingdomScaffold.PendingImprovementSuccessorProperty) != 1))
				return Fail("retained component scalar or identity prefix is not exact old/new",
					out Failure);
			int matches = 0;
			string oldToken = ComponentToken(Lot, Before.SnapshotHash, BeforePlacement);
			string newToken = ComponentToken(Lot, After.SnapshotHash, AfterPlacement);
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Z) ?? KingdomSurvey.Take(Z);
			for (int i = 0; i < survey.Objects.Count; i++)
			{
				GameObject candidate = survey.Objects[i];
				if (!GameObject.Validate(candidate)
					|| candidate.GetStringProperty(KingdomPlots.PlotIdProperty) != Lot) continue;
				string slot = candidate.GetStringProperty(ComponentSlotProperty);
				string token = candidate.GetStringProperty(ComponentTokenProperty);
				bool exactToken = token == oldToken || token == newToken;
				bool sameSlotAndCell = candidate.CurrentCell == Item.CurrentCell
					&& (slot == BeforePlacement.Slot || slot == AfterPlacement.Slot);
				if (!exactToken && !sameSlotAndCell) continue;
				matches++;
				if (!ReferenceEquals(candidate, Item))
					return Fail("retained component slot is physically duplicated", out Failure);
			}
			return matches == 1 || Fail("retained component is absent from the exact zone survey",
				out Failure);
		}

		private static bool SameUpgradeWorldCell(Zone Z, GameObject Item,
			KingdomArchitectureIntent Before, KingdomArchitectureIntent After,
			ArchitecturePlacement BeforePlacement, ArchitecturePlacement AfterPlacement)
		{
			if (!KingdomArchitectureRuntime.TryDecode(Before, out ArchitectureLayoutSnapshot oldMap,
				out _) || !KingdomArchitectureRuntime.TryDecode(After,
				out ArchitectureLayoutSnapshot newMap, out _)
				|| !KingdomArchitectureRuntime.TryWorldPlacement(oldMap, Before.Rect,
					BeforePlacement, out int oldX, out int oldY, out _)
				|| !KingdomArchitectureRuntime.TryWorldPlacement(newMap, After.Rect,
					AfterPlacement, out int newX, out int newY, out _)) return false;
			return oldX == newX && oldY == newY && Item.CurrentCell == Z.GetCell(newX, newY);
		}

		private static bool OldOrNewInt(GameObject Item, string Property, int Old, int Next)
		{
			return KingdomArchitectureReceiptPrefixRules.OldOrNewInt(
				Item.HasIntProperty(Property), Item.GetIntProperty(Property),
				Item.HasStringProperty(Property), Old, Next);
		}

		private static bool OldOrNewString(GameObject Item, string Property, string Old,
			string Next)
		{
			return KingdomArchitectureReceiptPrefixRules.OldOrNewString(
				Item.HasStringProperty(Property), Item.GetStringProperty(Property),
				Item.HasIntProperty(Property), Old, Next);
		}
	}
}
