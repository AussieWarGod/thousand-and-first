using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public sealed partial class KingdomBenefitIndex
	{
		private static bool TryProviderCell(GameObject Item, KingdomBenefitScope Scope,
			out Cell Cell, out GameObject Holder, out bool InContainer)
		{
			Cell = null; Holder = null; InContainer = false;
			if (!GameObject.Validate(Item) || Item.IsCreature || Item.IsPlayer()
				|| Item.Count != 1 || Item.Equipped != null) return false;
			if (Item.InInventory == null)
			{
				Cell = Item.CurrentCell;
				return Cell != null && ReferenceEquals(Item.CurrentZone, Cell.ParentZone);
			}
			if (Scope != KingdomBenefitScope.Container) return false;
			Holder = Item.InInventory; InContainer = true;
			if (!GameObject.Validate(Holder) || Holder.IsCreature || Holder.IsPlayer()
				|| Holder.InInventory != null || Holder.Equipped != null
				|| Holder.Inventory == null || !ReferenceEquals(Item.InInventory, Holder)
				|| !Holder.Inventory.Objects.Contains(Item)) return false;
			Cell = Holder.CurrentCell;
			return Cell != null && ReferenceEquals(Holder.CurrentZone, Cell.ParentZone);
		}

		private static bool TryAssign(GameObject Item, List<KingdomDesignationMatch> Matches,
			KingdomDesignationIndex Designations, out KingdomDesignationMatch Match,
			out KingdomBenefitFault Fault, out string Detail)
		{
			Match = default(KingdomDesignationMatch); Fault = KingdomBenefitFault.None; Detail = null;
			bool hasId = Item.HasStringProperty(AssignmentIdentityProperty)
				|| Item.HasIntProperty(AssignmentIdentityProperty);
			bool hasRevision = Item.HasStringProperty(AssignmentRevisionProperty)
				|| Item.HasIntProperty(AssignmentRevisionProperty);
			if (hasId || hasRevision)
			{
				if (Matches.Count != 1)
					return Refuse(Matches.Count == 0
						? KingdomBenefitFault.OutsideDesignation
						: KingdomBenefitFault.AmbiguousDesignation,
						"assigned provider must still occupy exactly one accepting designation",
						out Fault, out Detail);
				if (!hasId || !hasRevision || Item.HasIntProperty(AssignmentIdentityProperty)
					|| Item.HasIntProperty(AssignmentRevisionProperty))
					return Refuse(KingdomBenefitFault.StaleAssignment,
						"provider assignment receipt is incomplete", out Fault, out Detail);
				string id = Item.GetStringProperty(AssignmentIdentityProperty);
				string revision = Item.GetStringProperty(AssignmentRevisionProperty);
				KingdomBenefitDesignation exact = Designations.FindExact(id);
				if (exact == null || exact.Revision != revision)
					return Refuse(KingdomBenefitFault.StaleAssignment,
						"provider assignment names no current designation revision", out Fault, out Detail);
				int found = 0;
				for (int i = 0; i < Matches.Count; i++)
					if (ReferenceEquals(Matches[i].Designation, exact)) { Match = Matches[i]; found++; }
				if (found != 1)
					return Refuse(KingdomBenefitFault.StaleAssignment,
						"assigned provider is outside its exact designated scope", out Fault, out Detail);
				return true;
			}
			if (Matches.Count == 0)
				return Refuse(KingdomBenefitFault.OutsideDesignation,
					"provider is outside every accepting designation", out Fault, out Detail);
			if (Matches.Count != 1)
				return Refuse(KingdomBenefitFault.AmbiguousDesignation,
					"provider overlaps more than one accepting designation", out Fault, out Detail);
			Match = Matches[0]; return true;
		}

		private static bool Refuse(KingdomBenefitFault Value, string Message,
			out KingdomBenefitFault Fault, out string Detail)
		{
			Fault = Value; Detail = Message; return false;
		}

	}
}
