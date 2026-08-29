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
		private static bool TryBeginUpgradeReceipt(GameObject Owner, GameObject Target,
			KingdomArchitectureIntent Successor, string Lot, ArchitectureLayoutDelta Delta,
			out string Failure)
		{
			Failure = null;
			if (!GameObject.Validate(Target) || string.IsNullOrEmpty(Target.IDIfAssigned)
				|| Target.IDIfAssigned.Length > KingdomConstructionRules.MaxSubjectChars)
				return Fail("authored successor has no bounded exact identity", out Failure);
			try
			{
				Owner.RemoveIntProperty(UpgradeSchemaProperty);
				Owner.SetStringProperty(UpgradeTargetProperty, Target.IDIfAssigned);
				Owner.SetStringProperty(UpgradeHashProperty, Successor.SnapshotHash);
				Owner.SetStringProperty(UpgradeLotProperty, Lot);
				Owner.SetIntProperty(UpgradePhaseProperty, 0);
				Owner.SetStringProperty(UpgradeFaultProperty, null, RemoveIfNull: true);
				for (int i = 0; i < Delta.Removed.Count; i++)
					Owner.RemoveIntProperty(UpgradeRemove(Delta.Removed[i]));
				for (int i = 0; i < Delta.Retained.Count; i++)
					Owner.RemoveIntProperty(UpgradeRetain(Delta.Retained[i]));
				Owner.SetIntProperty(UpgradeSchemaProperty, UpgradeSchema);
			}
			catch (Exception exception)
			{
				try { Owner.RemoveIntProperty(UpgradeSchemaProperty); } catch { }
				return Fail("authored upgrade receipt write threw: " + exception.Message, out Failure);
			}
			return TryReadUpgradeReceipt(Owner, Target, Successor, Lot, out Failure);
		}

		private static bool TryReadUpgradeReceipt(GameObject Owner, GameObject Target,
			KingdomArchitectureIntent Successor, string Lot, out string Failure)
		{
			Failure = null;
			if (Owner == null || Target == null || !Owner.HasIntProperty(UpgradeSchemaProperty)
				|| Owner.HasStringProperty(UpgradeSchemaProperty)
				|| Owner.GetIntProperty(UpgradeSchemaProperty) != UpgradeSchema
				|| Owner.GetStringProperty(UpgradeTargetProperty) != Target.IDIfAssigned
				|| Owner.GetStringProperty(UpgradeHashProperty) != Successor.SnapshotHash
				|| Owner.GetStringProperty(UpgradeLotProperty) != Lot)
				return Fail("authored upgrade receipt is absent, partial, unknown, or changed",
					out Failure);
			string fault = Owner.GetStringProperty(UpgradeFaultProperty);
			if (!string.IsNullOrEmpty(fault))
				return Fail("authored upgrade is quarantined: " + Bounded(fault), out Failure);
			int phase = Owner.GetIntProperty(UpgradePhaseProperty);
			if (!Owner.HasIntProperty(UpgradePhaseProperty) || phase < 0 || phase > 4)
				return Fail("authored upgrade phase is absent or malformed", out Failure);
			return true;
		}

		private static bool ExactSuccessorOwner(GameObject Target,
			KingdomArchitectureIntent Successor, string Lot, out string Failure)
		{
			KingdomArchitectureIntent intent;
			ArchitectureLayoutSnapshot snapshot;
			string observedLot;
			return TryReadOwner(Target, out intent, out snapshot, out observedLot, out Failure)
				&& observedLot == Lot && intent.SnapshotHash == Successor.SnapshotHash
				&& SameRect(intent.Rect, Successor.Rect)
				&& intent.MainWorldX == Successor.MainWorldX
				&& intent.MainWorldY == Successor.MainWorldY;
		}

		private static bool TryRemoveUpgradeSlot(GameObject Owner, Zone Z,
			KingdomArchitectureIntent Before, string Lot, ArchitecturePlacement Placement,
			out string Failure)
		{
			Failure = null;
			string stateProperty = UpgradeRemove(Placement);
			int state = Owner.GetIntProperty(stateProperty);
			if (Owner.HasStringProperty(stateProperty) || state < 0 || state > 2)
				return Fail("authored removal receipt for slot " + Placement.Slot + " is malformed",
					out Failure);
			string id = Owner.GetStringProperty(OutputId(Placement));
			if (state == 2)
				return KingdomConstruction.FindExactId(Z, id, out _)
					== KingdomPhysicalLookupState.Absent || Fail("removed authored slot "
						+ Placement.Slot + " reappeared", out Failure);
			GameObject exact;
			if (KingdomConstruction.FindExactId(Z, id, out exact)
				!= KingdomPhysicalLookupState.Exact
				|| !ExactComponent(exact, Z, Before, Lot, Placement, id)
				|| !TryRemovableComponent(exact, Placement, out Failure))
				return Failure != null ? false : Fail("authored removal source " + Placement.Slot
					+ " is absent, duplicated, moved, or changed", out Failure);
			if (state == 0) Owner.SetIntProperty(stateProperty, 1);
			bool removed;
			try { removed = exact.Destroy(null, Silent: true); }
			catch (Exception exception)
			{
				KingdomSurvey.ObserveCurrentTopologyInActive(Z, exact);
				return Fail("authored removal " + Placement.Slot + " threw: "
					+ exception.Message, out Failure);
			}
			if (removed && !GameObject.Validate(exact))
				KingdomSurvey.ObserveRemovedFromActive(Z, exact);
			if (!removed || GameObject.Validate(exact)
				|| KingdomConstruction.FindExactId(Z, id, out _)
					!= KingdomPhysicalLookupState.Absent)
				return Fail("authored removal " + Placement.Slot
					+ " was vetoed or changed during callback", out Failure);
			Owner.SetIntProperty(stateProperty, 2);
			return true;
		}

		private static bool TryCarryUpgradeSlot(GameObject Owner, GameObject Target, Zone Z,
			KingdomArchitectureIntent Before, KingdomArchitectureIntent After, string Lot,
			ArchitecturePlacement BeforePlacement, ArchitecturePlacement AfterPlacement,
			out string Failure)
		{
			Failure = null;
			if (BeforePlacement == null || AfterPlacement == null)
				return Fail("authored retained placement pair is absent", out Failure);
			string stateProperty = UpgradeRetain(BeforePlacement);
			int state = Owner.GetIntProperty(stateProperty);
			if (Owner.HasStringProperty(stateProperty) || state < 0 || state > 2)
				return Fail("authored retained receipt for slot " + BeforePlacement.Slot + " is malformed",
					out Failure);
			string id = Owner.GetStringProperty(OutputId(BeforePlacement));
			if (state == 0)
			{
				GameObject old;
				if (KingdomConstruction.FindExactId(Z, id, out old)
					!= KingdomPhysicalLookupState.Exact
					|| !ExactComponent(old, Z, Before, Lot, BeforePlacement, id))
					return Fail("retained authored slot " + BeforePlacement.Slot
						+ " changed before successor publication", out Failure);
				Target.SetStringProperty(OutputId(AfterPlacement), id);
				Target.SetIntProperty(OutputState(AfterPlacement), 1);
				Owner.SetIntProperty(stateProperty, 1);
				state = 1;
			}
			if (state == 1)
			{
				GameObject exact;
				if (KingdomConstruction.FindExactId(Z, id, out exact)
					!= KingdomPhysicalLookupState.Exact)
					return Fail("retained authored slot " + BeforePlacement.Slot
						+ " vanished after identity publication", out Failure);
				if (ExactComponent(exact, Z, Before, Lot, BeforePlacement, id))
				{
					StampComponent(exact, Lot, After.SnapshotHash, AfterPlacement);
					exact.SetIntProperty(ComponentCarriedProperty, 1);
				}
				else if (!ExactComponent(exact, Z, After, Lot, AfterPlacement, id))
					return Fail("retained authored slot " + BeforePlacement.Slot
						+ " changed during successor retag", out Failure);
				else exact.SetIntProperty(ComponentCarriedProperty, 1);
				Target.SetIntProperty(OutputState(AfterPlacement), 2);
				Owner.SetIntProperty(stateProperty, 2);
				state = 2;
			}
			GameObject settled;
			return state == 2 && KingdomConstruction.FindExactId(Z, id, out settled)
				== KingdomPhysicalLookupState.Exact
				&& ExactComponent(settled, Z, After, Lot, AfterPlacement, id)
				&& settled.GetIntProperty(ComponentCarriedProperty) == 1
				|| Fail("retained authored slot " + BeforePlacement.Slot
					+ " did not settle on the successor", out Failure);
		}

		private static string UpgradeRemove(ArchitecturePlacement Placement)
		{
			return UpgradeRemovePrefix + PropertySlot(Placement.Slot);
		}

		private static string UpgradeRetain(ArchitecturePlacement Placement)
		{
			return UpgradeRetainPrefix + PropertySlot(Placement.Slot);
		}

		private static bool UpgradeFail(GameObject Owner, string Message, out string Failure)
		{
			Failure = Bounded(Message ?? "authored upgrade refused without a reason");
			try { Owner.SetStringProperty(UpgradeFaultProperty, Failure); } catch { }
			return false;
		}

		private static bool SameRect(KingdomPlotRules.PlotRect A,
			KingdomPlotRules.PlotRect B)
		{
			return A.X1 == B.X1 && A.Y1 == B.Y1 && A.X2 == B.X2 && A.Y2 == B.Y2;
		}

	}
}
