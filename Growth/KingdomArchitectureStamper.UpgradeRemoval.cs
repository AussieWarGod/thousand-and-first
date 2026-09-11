using System;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitectureStamper
	{

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
				// Removals run in their own phase, before any retained component is retagged
				// (UpgradeApplication.cs:126-138), so no successor generation stands yet.
				|| !ExactComponent(Owner, exact, Z, Before, Lot, Placement, id, null))
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
						&& ExactComponent(Owner, afterThrow, Z, Before, Lot, Placement, id, null));
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
				&& ExactComponent(Owner, after, Z, Before, Lot, Placement, id, null));
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

	}
}
