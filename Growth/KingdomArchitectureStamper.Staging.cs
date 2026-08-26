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
		public static bool TryStageLayer(GameObject Owner, Zone Z, ArchitectureLayer Layer,
			out string Failure)
		{
			Failure = null;
			KingdomArchitectureIntent intent;
			ArchitectureLayoutSnapshot snapshot;
			string lot;
			if (Z == null || !TryReadOwner(Owner, out intent, out snapshot, out lot, out Failure))
				return false;
			int target = (int)Layer;
			if (target < 0 || target > 2) return Fail("layout layer is unknown", out Failure);
			int next = Owner.GetIntProperty(NextLayerProperty);
			if (next > target) return TryVerifyLayer(Owner, Z, intent, snapshot, lot, Layer, out Failure);
			if (next < target) return Fail("layout layers must settle ground, structure, then object",
				out Failure);
			for (int i = 0; i < snapshot.Placements.Count; i++)
			{
				ArchitecturePlacement placement = snapshot.Placements[i];
				if (placement.Layer != Layer) continue;
				if (!TrySettlePlacement(Owner, Z, intent, snapshot, lot, placement, out Failure))
					return false;
			}
			if (!TryVerifyPassabilityThrough(Z, intent, snapshot, lot, Layer, out Failure))
			{
				string rollback;
				bool clean = TryRollbackNewLayout(Owner, Z, intent, snapshot, lot, out rollback);
				return Quarantine(Owner, Failure + (clean ? "; exact new pieces rolled back"
					: "; exact rollback failed: " + rollback), out Failure);
			}
			Owner.SetIntProperty(NextLayerProperty, target + 1);
			return TryVerifyLayer(Owner, Z, intent, snapshot, lot, Layer, out Failure);
		}

		public static bool TryVerifyComplete(GameObject Owner, Zone Z, out string Failure)
		{
			Failure = null;
			KingdomArchitectureIntent intent;
			ArchitectureLayoutSnapshot snapshot;
			string lot;
			if (Z == null || !TryReadOwner(Owner, out intent, out snapshot, out lot, out Failure)
				|| Owner.GetIntProperty(NextLayerProperty) != 3)
			{
				if (Failure == null) Failure = "authored layout is not complete";
				return false;
			}
			for (int i = 0; i < snapshot.Placements.Count; i++)
			{
				ArchitecturePlacement placement = snapshot.Placements[i];
				GameObject exact;
				if (!TryExactOutput(Owner, Z, intent, lot, placement, out exact, out Failure)) return false;
			}
			return TryVerifyPassability(Z, intent, snapshot, lot, out Failure);
		}

		private static bool TrySettlePlacement(GameObject Owner, Zone Z,
			KingdomArchitectureIntent Intent, ArchitectureLayoutSnapshot Snapshot, string Lot,
			ArchitecturePlacement Placement, out string Failure)
		{
			Failure = null;
			string stateProperty = OutputState(Placement);
			string idProperty = OutputId(Placement);
			int state = Owner.GetIntProperty(stateProperty);
			if (state == 2)
			{
				GameObject settled;
				return TryExactOutput(Owner, Z, Intent, Lot, Placement, out settled, out Failure);
			}
			if (state == 1)
			{
				GameObject pending;
				KingdomPhysicalLookupState found = KingdomConstruction.FindExactId(Z,
					Owner.GetStringProperty(idProperty), out pending);
				if (found != KingdomPhysicalLookupState.Exact)
					return Quarantine(Owner, "layout slot " + Placement.Slot
						+ " lost its published output before settlement", out Failure);
				if (Placement.ExistingAuthority && IsExactExistingCore(pending, Placement, Intent))
				{
					StampComponent(pending, Lot, Intent.SnapshotHash, Placement);
					KingdomSurvey.ObserveChangedInActive(Z, pending);
				}
				if (!ExactComponent(pending, Z, Intent, Lot, Placement, Owner.GetStringProperty(idProperty)))
					return Quarantine(Owner, "layout slot " + Placement.Slot
						+ " changed after output publication", out Failure);
				Owner.SetIntProperty(stateProperty, 2);
				return true;
			}
			if (state != 0 || !string.IsNullOrEmpty(Owner.GetStringProperty(idProperty)))
				return Quarantine(Owner, "layout slot " + Placement.Slot
					+ " has a malformed creation receipt", out Failure);

			int x;
			int y;
			if (!KingdomArchitectureRuntime.TryWorldPlacement(Snapshot, Intent.Rect, Placement,
				out x, out y, out Failure)) return false;
			Cell cell = Z.GetCell(x, y);
			GameObject placed;
			if (Placement.ExistingAuthority)
			{
				if (!TryFindExistingAt(Z, Placement, cell, out placed, out Failure)) return false;
				Owner.SetStringProperty(idProperty, placed.ID);
				Owner.SetIntProperty(stateProperty, 1);
				StampComponent(placed, Lot, Intent.SnapshotHash, Placement);
			}
			else
			{
				if (!CanInsert(Owner, Z, cell, Lot, Intent.SnapshotHash, Placement, out Failure))
					return false;
				try { placed = GameObject.Create(Placement.Blueprint); }
				catch (Exception exception)
				{
					return Fail("layout slot " + Placement.Slot + " creation threw: "
						+ exception.Message, out Failure);
				}
				if (!GameObject.Validate(placed))
					return Fail("layout slot " + Placement.Slot + " created no exact object", out Failure);
				StampComponent(placed, Lot, Intent.SnapshotHash, Placement);
				Owner.SetStringProperty(idProperty, placed.ID);
				Owner.SetIntProperty(stateProperty, 1);
				try
				{
					GameObject accepted = cell.AddObject(placed, NoStack: true, Silent: true);
					KingdomSurvey.ObserveAddResultInActive(Z, placed, accepted);
					if (!ReferenceEquals(accepted, placed))
						return Quarantine(Owner, "layout slot " + Placement.Slot
							+ " AddObject replaced its exact output", out Failure);
				}
				catch (Exception exception)
				{
					KingdomSurvey.ObserveCurrentTopologyInActive(Z, placed);
					return Quarantine(Owner, "layout slot " + Placement.Slot
						+ " AddObject threw after output publication: " + exception.Message, out Failure);
				}
			}
			KingdomSurvey.ObserveChangedInActive(Z, placed);
			if (!ExactComponent(placed, Z, Intent, Lot, Placement,
				Owner.GetStringProperty(idProperty)))
				return Quarantine(Owner, "layout slot " + Placement.Slot
					+ " failed exact settlement proof", out Failure);
			Owner.SetIntProperty(stateProperty, 2);
			return true;
		}

	}
}
