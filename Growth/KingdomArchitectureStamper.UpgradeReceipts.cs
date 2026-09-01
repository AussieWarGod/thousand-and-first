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
			bool complete;
			if (!TryAcceptUpgradeHeaderPrefix(Owner, Target, Successor, Lot, Delta,
				out complete, out Failure)) return false;
			if (complete) return true;
			try
			{
				Owner.SetStringProperty(UpgradeTargetProperty, Target.IDIfAssigned);
				Owner.SetStringProperty(UpgradeHashProperty, Successor.SnapshotHash);
				Owner.SetStringProperty(UpgradeLotProperty, Lot);
				Owner.SetIntProperty(UpgradePhaseProperty, 0);
				Owner.SetStringProperty(UpgradeFaultProperty, null, RemoveIfNull: true);
				for (int i = 0; i < Delta.Removed.Count; i++)
					Owner.SetIntProperty(UpgradeRemove(Delta.Removed[i]), 0);
				for (int i = 0; i < Delta.Retained.Count; i++)
					Owner.SetIntProperty(UpgradeRetain(Delta.Retained[i]), 0);
				Owner.SetIntProperty(UpgradeSchemaProperty, UpgradeSchema);
			}
			catch (Exception exception)
			{
				string ignored;
				if (TryReadUpgradeReceipt(Owner, Target, Successor, Lot, Delta,
					out ignored)) return true;
				if (Owner.HasIntProperty(UpgradeSchemaProperty)
					|| Owner.HasStringProperty(UpgradeSchemaProperty))
					return UpgradeQuarantine(Owner,
						"authored upgrade header committed ambiguously after an exception",
						out Failure);
				return Fail("authored upgrade receipt publication remains retryable: "
					+ exception.Message, out Failure);
			}
			return TryReadUpgradeReceipt(Owner, Target, Successor, Lot, Delta, out Failure);
		}

		private static bool TryReadUpgradeReceipt(GameObject Owner, GameObject Target,
			KingdomArchitectureIntent Successor, string Lot, ArchitectureLayoutDelta Delta,
			out string Failure)
		{
			Failure = null;
			if (Owner == null || Target == null || !Owner.HasIntProperty(UpgradeSchemaProperty)
				|| Owner.HasStringProperty(UpgradeSchemaProperty)
				|| Owner.GetIntProperty(UpgradeSchemaProperty) != UpgradeSchema
				|| Owner.HasIntProperty(UpgradeTargetProperty)
				|| Owner.HasIntProperty(UpgradeHashProperty)
				|| Owner.HasIntProperty(UpgradeLotProperty)
				|| !Owner.HasStringProperty(UpgradeTargetProperty)
				|| !Owner.HasStringProperty(UpgradeHashProperty)
				|| !Owner.HasStringProperty(UpgradeLotProperty)
				|| Owner.GetStringProperty(UpgradeTargetProperty) != Target.IDIfAssigned
				|| Owner.GetStringProperty(UpgradeHashProperty) != Successor.SnapshotHash
					|| Owner.GetStringProperty(UpgradeLotProperty) != Lot || Delta == null)
					return Fail("authored upgrade receipt is absent, partial, unknown, or changed",
						out Failure);
			if (Owner.HasIntProperty(UpgradeFaultProperty))
				return Fail("authored upgrade fault has an opposite-type collision", out Failure);
			string fault = Owner.GetStringProperty(UpgradeFaultProperty);
			if (!string.IsNullOrEmpty(fault))
				return Fail("authored upgrade is quarantined: " + Bounded(fault), out Failure);
			int phase = Owner.GetIntProperty(UpgradePhaseProperty);
			if (!Owner.HasIntProperty(UpgradePhaseProperty)
				|| Owner.HasStringProperty(UpgradePhaseProperty) || phase < 0 || phase > 5)
				return Fail("authored upgrade phase is absent or malformed", out Failure);
			for (int i = 0; i < Delta.Removed.Count; i++)
				if (!ExactUpgradeState(Owner, UpgradeRemove(Delta.Removed[i])))
					return Fail("authored removal receipt is absent or malformed", out Failure);
			for (int i = 0; i < Delta.Retained.Count; i++)
				if (!ExactUpgradeState(Owner, UpgradeRetain(Delta.Retained[i])))
					return Fail("authored retain receipt is absent or malformed", out Failure);
			return true;
		}

		private static bool ExactUpgradeState(GameObject Owner, string Property)
		{
			if (!Owner.HasIntProperty(Property) || Owner.HasStringProperty(Property)) return false;
			int state = Owner.GetIntProperty(Property);
			return state >= 0 && state <= 2;
		}

		private static bool ExactSuccessorOwner(GameObject Target,
			KingdomArchitectureIntent Successor, string Lot, out string Failure)
		{
			Failure = null;
			KingdomArchitectureIntent intent;
			int next = Target == null ? -1 : Target.GetIntProperty(NextLayerProperty);
			if (Target == null || !Target.HasIntProperty(SchemaProperty)
				|| Target.HasStringProperty(SchemaProperty)
				|| Target.GetIntProperty(SchemaProperty) != LayoutSchema
				|| Target.HasIntProperty(FaultProperty)
				|| !string.IsNullOrEmpty(Target.GetStringProperty(FaultProperty))
				|| Target.HasIntProperty(LotIdProperty)
				|| Target.HasIntProperty(HashProperty)
				|| Target.HasStringProperty(NextLayerProperty)
				|| Target.GetStringProperty(LotIdProperty) != Lot
				|| Target.GetStringProperty(HashProperty) != Successor.SnapshotHash
				|| !Target.HasIntProperty(NextLayerProperty) || next < 0 || next > 3
				|| !KingdomArchitectureRuntime.TryRead(Target, out intent, out Failure)
				|| intent.SnapshotHash != Successor.SnapshotHash
				|| !SameRect(intent.Rect, Successor.Rect)
				|| intent.MainWorldX != Successor.MainWorldX
				|| intent.MainWorldY != Successor.MainWorldY)
				return Failure != null ? false : Fail(
					"successor layout owner header is absent, malformed, or changed", out Failure);
			return true;
		}

		private static bool TryRemoveUpgradeSlot(GameObject Owner, Zone Z,
			KingdomArchitectureIntent Before, string Lot, ArchitecturePlacement Placement,
			out string Failure)
		{
			Failure = null;
			string stateProperty = UpgradeRemove(Placement);
			int state = Owner.GetIntProperty(stateProperty);
			if (!Owner.HasIntProperty(stateProperty) || Owner.HasStringProperty(stateProperty)
				|| state < 0 || state > 2)
				return UpgradeQuarantine(Owner, "authored removal receipt for slot "
					+ Placement.Slot + " is malformed",
					out Failure);
			string id = Owner.GetStringProperty(OutputId(Placement));
			if (state == 2)
				return KingdomConstruction.FindGlobalLiveId(id, out _)
					== KingdomPhysicalLookupState.Absent || UpgradeQuarantine(Owner,
						"removed authored slot " + Placement.Slot + " reappeared", out Failure);
			GameObject exact;
			KingdomPhysicalLookupState found = KingdomConstruction.FindGlobalLiveId(id, out exact);
			if (state == 1 && found == KingdomPhysicalLookupState.Absent)
			{
				Owner.SetIntProperty(stateProperty, 2);
				return true;
			}
			if (found != KingdomPhysicalLookupState.Exact
				|| !ExactComponent(Owner, exact, Z, Before, Lot, Placement, id))
				return UpgradeQuarantine(Owner, "authored removal source " + Placement.Slot
					+ " is absent, duplicated, moved, or changed", out Failure);
			if (!TryRemovableComponent(exact, Placement, out Failure))
				return UpgradeQuarantine(Owner, Failure, out Failure);
			if (state == 0) Owner.SetIntProperty(stateProperty, 1);
			bool removed;
			try { removed = exact.Destroy(null, Silent: true); }
			catch (Exception exception)
			{
				KingdomSurvey.ObserveCurrentTopologyInActive(Z, exact);
				found = KingdomConstruction.FindGlobalLiveId(id, out GameObject afterThrow);
				KingdomExactRemovalAction aftermath =
					KingdomConstructionRules.GlobalRemovalAftermath(found,
						ReferenceEquals(afterThrow, exact), found == KingdomPhysicalLookupState.Exact
						&& ExactComponent(Owner, afterThrow, Z, Before, Lot, Placement, id));
				if (aftermath == KingdomExactRemovalAction.ProvedAbsent)
				{
					Owner.SetIntProperty(stateProperty, 2);
					return true;
				}
				if (aftermath == KingdomExactRemovalAction.InvokeOnce)
					return Fail("authored removal " + Placement.Slot
						+ " threw before changing exact state: " + exception.Message,
						out Failure);
				return UpgradeQuarantine(Owner, "authored removal " + Placement.Slot
					+ " threw after ambiguous physical change: " + exception.Message,
					out Failure);
			}
			if (removed && !GameObject.Validate(exact))
				KingdomSurvey.ObserveRemovedFromActive(Z, exact);
			found = KingdomConstruction.FindGlobalLiveId(id, out GameObject after);
			KingdomExactRemovalAction result = KingdomConstructionRules.GlobalRemovalAftermath(
				found, ReferenceEquals(after, exact), found == KingdomPhysicalLookupState.Exact
				&& ExactComponent(Owner, after, Z, Before, Lot, Placement, id));
			if (result == KingdomExactRemovalAction.ProvedAbsent)
			{
				Owner.SetIntProperty(stateProperty, 2);
				return true;
			}
			if (result == KingdomExactRemovalAction.InvokeOnce)
				return Fail("authored removal " + Placement.Slot
					+ (removed ? " reported success without changing exact state"
						: " was vetoed before changing exact state"), out Failure);
			return UpgradeQuarantine(Owner, "authored removal " + Placement.Slot
				+ " changed ambiguously during callback", out Failure);
		}

		private static bool TryCarryUpgradeSlot(GameObject Owner, GameObject Target, Zone Z,
			KingdomArchitectureIntent Before, KingdomArchitectureIntent After, string Lot,
			ArchitecturePlacement BeforePlacement, ArchitecturePlacement AfterPlacement,
			out string Failure)
		{
			Failure = null;
			if (BeforePlacement == null || AfterPlacement == null)
				return UpgradeQuarantine(Owner,
					"authored retained placement pair is absent", out Failure);
			string stateProperty = UpgradeRetain(BeforePlacement);
			int state = Owner.GetIntProperty(stateProperty);
			if (!Owner.HasIntProperty(stateProperty) || Owner.HasStringProperty(stateProperty)
				|| state < 0 || state > 2)
				return UpgradeQuarantine(Owner, "authored retained receipt for slot "
					+ BeforePlacement.Slot + " is malformed",
					out Failure);
			string idProperty = OutputId(BeforePlacement);
			string id = Owner.GetStringProperty(idProperty);
			if (Owner.HasIntProperty(idProperty) || string.IsNullOrEmpty(id)
				|| id.Length > KingdomConstructionRules.MaxSubjectChars)
				return UpgradeQuarantine(Owner, "retained authored slot "
					+ BeforePlacement.Slot + " has no exact predecessor identity", out Failure);
			ArchitectureOutputPrefix targetPrefix = RetainedTargetPrefix(Target,
				AfterPlacement, id);
			if (!KingdomArchitectureReceiptPrefixRules.LegalRetainedTarget(state,
				targetPrefix))
				return UpgradeQuarantine(Owner, "retained successor slot "
					+ AfterPlacement.Slot + " carries an impossible publication prefix",
					out Failure);
			if (state == 0)
			{
				GameObject old;
				if (KingdomConstruction.FindExactId(Z, id, out old)
					!= KingdomPhysicalLookupState.Exact
					|| !ExactComponent(Owner, old, Z, Before, Lot, BeforePlacement, id))
					return UpgradeQuarantine(Owner, "retained authored slot " + BeforePlacement.Slot
						+ " changed before successor publication", out Failure);
				if (targetPrefix == ArchitectureOutputPrefix.Empty
					&& !TrySetUpgradeInt(Target, OutputState(AfterPlacement), 1,
						"retained successor state publication", out Failure)) return false;
				if ((targetPrefix == ArchitectureOutputPrefix.Empty
						|| targetPrefix == ArchitectureOutputPrefix.StateOnly)
					&& !TrySetUpgradeString(Target, OutputId(AfterPlacement), id,
						"retained successor identity publication", out Failure)) return false;
				if (RetainedTargetPrefix(Target, AfterPlacement, id)
					!= ArchitectureOutputPrefix.Published)
					return UpgradeQuarantine(Owner, "retained successor slot "
						+ AfterPlacement.Slot + " did not publish its exact prefix", out Failure);
				if (!TrySetUpgradeInt(Owner, stateProperty, 1,
					"retained predecessor receipt publication", out Failure)) return false;
				state = 1;
				targetPrefix = ArchitectureOutputPrefix.Published;
			}
			if (state == 1)
			{
				GameObject exact;
				if (KingdomConstruction.FindExactId(Z, id, out exact)
					!= KingdomPhysicalLookupState.Exact)
					return UpgradeQuarantine(Owner, "retained authored slot " + BeforePlacement.Slot
						+ " vanished after identity publication", out Failure);
				if (targetPrefix == ArchitectureOutputPrefix.Published)
				{
					if (!TryRetagUpgradeComponent(Owner, exact, Z, Before, After, Lot,
						BeforePlacement, AfterPlacement, id, out Failure)) return false;
					if (!TrySetUpgradeInt(Target, OutputState(AfterPlacement), 2,
						"retained successor settlement", out Failure)) return false;
				}
				else if (!ExactComponent(Owner, exact, Z, After, Lot, AfterPlacement, id)
					|| exact.GetIntProperty(ComponentCarriedProperty) != 1)
					return UpgradeQuarantine(Owner, "settled retained successor slot "
						+ AfterPlacement.Slot + " is absent, moved, duplicated, or changed",
						out Failure);
				if (!TrySetUpgradeInt(Owner, stateProperty, 2,
					"retained predecessor settlement", out Failure)) return false;
				state = 2;
			}
			GameObject settled;
			return state == 2
				&& Target.GetStringProperty(OutputId(AfterPlacement)) == id
				&& Target.GetIntProperty(OutputState(AfterPlacement)) == 2
				&& KingdomConstruction.FindExactId(Z, id, out settled)
				== KingdomPhysicalLookupState.Exact
				&& ExactComponent(Owner, settled, Z, After, Lot, AfterPlacement, id)
				&& settled.GetIntProperty(ComponentCarriedProperty) == 1
				|| UpgradeQuarantine(Owner, "retained authored slot " + BeforePlacement.Slot
					+ " did not settle on the successor", out Failure);
		}

	}
}
