using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitectureStamper
	{
		private static bool TryVerifyLayer(GameObject Owner, Zone Z,
			KingdomArchitectureIntent Intent, ArchitectureLayoutSnapshot Snapshot, string Lot,
			ArchitectureLayer Layer, out string Failure)
		{
			Failure = null;
			for (int i = 0; i < Snapshot.Placements.Count; i++)
			{
				ArchitecturePlacement placement = Snapshot.Placements[i];
				if (placement.Layer != Layer) continue;
				GameObject exact;
				if (!TryExactOutput(Owner, Z, Intent, Lot, placement, out exact, out Failure)) return false;
			}
			return true;
		}

		private static bool TryExactOutput(GameObject Owner, Zone Z,
			KingdomArchitectureIntent Intent, string Lot, ArchitecturePlacement Placement,
			out GameObject Exact, out string Failure)
		{
			Exact = null;
			Failure = null;
			if (Owner.GetIntProperty(OutputState(Placement)) != 2)
				return Fail("layout slot " + Placement.Slot + " is not settled", out Failure);
			string id = Owner.GetStringProperty(OutputId(Placement));
			KingdomPhysicalLookupState state = KingdomConstruction.FindExactId(Z, id, out Exact);
			if (state != KingdomPhysicalLookupState.Exact
				|| !ExactComponent(Exact, Z, Intent, Lot, Placement, id))
				return Quarantine(Owner, "settled layout slot " + Placement.Slot
					+ " is absent, moved, duplicated, or changed", out Failure);
			return true;
		}

		private static bool ExactComponent(GameObject Item, Zone Z,
			KingdomArchitectureIntent Intent, string Lot, ArchitecturePlacement Placement,
			string ExpectedId)
		{
			if (!GameObject.Validate(Item) || Item.ID != ExpectedId || Item.CurrentZone != Z
				|| Item.Blueprint != Placement.Blueprint
				|| Item.GetIntProperty(ComponentSchemaProperty) != ComponentSchema
				|| Item.GetStringProperty(KingdomPlots.PlotIdProperty) != Lot
				|| Item.GetStringProperty(ComponentSlotProperty) != Placement.Slot
				|| Item.GetIntProperty(ComponentLayerProperty) != (int)Placement.Layer
				|| Item.GetStringProperty(ComponentHashProperty) != Intent.SnapshotHash
				|| Item.GetStringProperty(ComponentTokenProperty)
					!= ComponentToken(Lot, Intent.SnapshotHash, Placement)
				|| Item.GetIntProperty(ComponentExistingProperty)
					!= (Placement.ExistingAuthority ? 1 : 0)
				|| Item.GetIntProperty(KingdomPlots.PlotPartProperty)
					!= (Placement.ExistingAuthority ? 0 : 1)) return false;
			string anchor = Item.GetStringProperty(ComponentAnchorProperty);
			if ((Placement.StatefulAnchor ?? "") != (anchor ?? "")) return false;
			ArchitectureLayoutSnapshot snapshot;
			if (!KingdomArchitectureRuntime.TryDecode(Intent, out snapshot, out _)) return false;
			int x;
			int y;
			if (!KingdomArchitectureRuntime.TryWorldPlacement(snapshot, Intent.Rect, Placement,
				out x, out y, out _) || Item.CurrentCell != Z.GetCell(x, y)) return false;
			int count = 0;
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Z) ?? KingdomSurvey.Take(Z);
			foreach (GameObject candidate in survey.ArchitectureComponents)
				if (GameObject.Validate(candidate)
					&& candidate.GetStringProperty(KingdomPlots.PlotIdProperty) == Lot
					&& candidate.GetStringProperty(ComponentSlotProperty) == Placement.Slot) count++;
			return count == 1;
		}

		private static bool CanInsert(GameObject Owner, Zone Z, Cell Cell, string Lot,
			string Hash, ArchitecturePlacement Placement, out string Failure)
		{
			Failure = null;
			if (Cell == null) return Fail("layout slot lies outside its frozen zone", out Failure);
			List<GameObject> objects = Cell.GetObjects();
			for (int i = 0; i < objects.Count; i++)
			{
				GameObject item = objects[i];
				if (!GameObject.Validate(item) || ReferenceEquals(item, Owner)
					|| item.GetIntProperty(KingdomPlots.HeartStakeProperty) == 1) continue;
				if (item.IsCreature || item.IsPlayer())
					return Fail("a living occupant moved onto layout slot " + Placement.Slot,
						out Failure);
				if (item.GetStringProperty(KingdomPlots.PlotIdProperty) == Lot
					&& item.GetStringProperty(ComponentHashProperty) == Hash
					&& item.GetIntProperty(ComponentSchemaProperty) == ComponentSchema) continue;
				if (KingdomPlots.ReadObject(item) == KingdomPlotRules.GroundKind.Bare) continue;
				return Fail("protected or foreign state moved onto layout slot " + Placement.Slot,
					out Failure);
			}
			return true;
		}

		private static void StampComponent(GameObject Item, string Lot, string Hash,
			ArchitecturePlacement Placement)
		{
			Item.SetIntProperty(KingdomPlots.PlotPartProperty,
				Placement.ExistingAuthority ? 0 : 1);
			Item.SetStringProperty(KingdomPlots.PlotIdProperty, Lot);
			Item.SetStringProperty(ComponentSlotProperty, Placement.Slot);
			Item.SetIntProperty(ComponentLayerProperty, (int)Placement.Layer);
			Item.SetStringProperty(ComponentAnchorProperty, Placement.StatefulAnchor,
				RemoveIfNull: true);
			Item.SetStringProperty(ComponentHashProperty, Hash);
			Item.SetStringProperty(ComponentTokenProperty, ComponentToken(Lot, Hash, Placement));
			Item.SetIntProperty(ComponentExistingProperty, Placement.ExistingAuthority ? 1 : 0);
			Item.RemoveIntProperty(ComponentCarriedProperty);
			Item.SetIntProperty(ComponentSchemaProperty, ComponentSchema);
		}

		private static Dictionary<string, GameObject> EmptyExisting()
		{
			return new Dictionary<string, GameObject>(StringComparer.Ordinal);
		}

		private static bool TryExistingBindings(Zone Z, ArchitectureLayoutSnapshot Snapshot,
			KingdomPlotRules.PlotRect Rect, out Dictionary<string, GameObject> Existing,
			out string Failure)
		{
			Existing = EmptyExisting();
			Failure = null;
			for (int i = 0; i < Snapshot.Placements.Count; i++)
			{
				ArchitecturePlacement placement = Snapshot.Placements[i];
				if (!placement.ExistingAuthority) continue;
				int x;
				int y;
				if (!KingdomArchitectureRuntime.TryWorldPlacement(Snapshot, Rect, placement,
					out x, out y, out Failure)) return false;
				GameObject exact;
				if (!TryFindExistingAt(Z, placement, Z.GetCell(x, y), out exact, out Failure))
					return false;
				Existing[placement.Slot] = exact;
			}
			return true;
		}

		private static bool TryFindExistingAt(Zone Z, ArchitecturePlacement Placement,
			Cell ExpectedCell, out GameObject Exact, out string Failure)
		{
			Exact = null;
			Failure = null;
			if (!Placement.ExistingAuthority || Placement.Blueprint != KingdomPlots.HeartRelicBlueprint
				|| ExpectedCell == null)
				return Fail("existing-authority slot is not the immutable first basin", out Failure);
			int count = 0;
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Z) ?? KingdomSurvey.Take(Z);
			foreach (GameObject item in survey.HeartRelics)
			{
				if (!GameObject.Validate(item)
					|| item.GetIntProperty(KingdomPlots.HeartRelicProperty) != 1) continue;
				count++;
				Exact = item;
			}
			if (count != 1 || Exact.Blueprint != Placement.Blueprint
				|| Exact.CurrentCell != ExpectedCell || Exact.CurrentZone != Z)
			{
				Exact = null;
				return Fail("the immutable first basin is absent, duplicated, moved, or misaligned",
					out Failure);
			}
			return true;
		}

		private static bool IsExpectedExisting(GameObject Item,
			Dictionary<string, GameObject> Existing)
		{
			foreach (KeyValuePair<string, GameObject> pair in Existing)
				if (ReferenceEquals(pair.Value, Item)) return true;
			return false;
		}

		private static bool IsExactExistingCore(GameObject Item,
			ArchitecturePlacement Placement, KingdomArchitectureIntent Intent)
		{
			if (!GameObject.Validate(Item) || !Placement.ExistingAuthority
				|| Item.Blueprint != Placement.Blueprint
				|| Item.GetIntProperty(KingdomPlots.HeartRelicProperty) != 1) return false;
			ArchitectureLayoutSnapshot snapshot;
			int x;
			int y;
			return KingdomArchitectureRuntime.TryDecode(Intent, out snapshot, out _)
				&& KingdomArchitectureRuntime.TryWorldPlacement(snapshot, Intent.Rect, Placement,
					out x, out y, out _) && Item.CurrentCell != null
				&& Item.CurrentCell.X == x && Item.CurrentCell.Y == y;
		}

	}
}
