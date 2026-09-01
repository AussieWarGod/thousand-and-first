using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitectureStamper
	{
		private static bool TryReadSettledExpansionOutputs(GameObject Owner,
			GameObject SuccessorOwner, Zone Z, KingdomArchitectureIntent BeforeIntent,
			KingdomArchitectureIntent Successor, ArchitectureLayoutSnapshot Before,
			ArchitectureLayoutSnapshot After, ArchitectureLayoutDelta Delta, string Lot,
			bool Allow, HashSet<GameObject> Settled, out string Failure)
		{
			Failure = null;
			if (!Allow) return SuccessorOwner == null || Fail(
				"pre-debit envelope proof cannot admit a successor owner", out Failure);
			if (!GameObject.Validate(SuccessorOwner))
				return Fail("durable envelope application has no exact successor owner", out Failure);
			if (!SuccessorOwner.HasIntProperty(SchemaProperty)
				&& !SuccessorOwner.HasStringProperty(SchemaProperty)) return true;
			KingdomArchitectureIntent observed;
			ArchitectureLayoutSnapshot observedSnapshot;
			string observedLot;
			if (!TryReadOwnerHeader(SuccessorOwner, out observed, out observedSnapshot,
				out observedLot, out Failure) || observedLot != Lot
				|| !SameOwnerIntent(observed, Successor)
				|| SuccessorOwner.CurrentZone != Z
				|| SuccessorOwner.CurrentCell != Z.GetCell(Successor.MainWorldX,
					Successor.MainWorldY))
				return UpgradeQuarantine(Owner, Failure
					?? "successor owner carries another envelope receipt", out Failure);
			for (int i = 0; i < After.Placements.Count; i++)
			{
				ArchitecturePlacement afterPlacement = After.Placements[i];
				ArchitecturePlacement beforePlacement;
				bool retained = TryRetainedPair(Delta, afterPlacement, out beforePlacement);
				if (retained)
				{
					if (!TryReadRetainedExpansionOutput(Owner, SuccessorOwner, Z,
						BeforeIntent, Successor, beforePlacement,
						afterPlacement, Lot, Settled, out Failure)) return false;
				}
				else if (!TryReadAddedExpansionOutput(Owner, SuccessorOwner, Z, Successor,
					After, afterPlacement, Lot, Settled, out Failure)) return false;
			}
			return true;
		}

		private static bool TryReadRetainedExpansionOutput(GameObject Owner,
			GameObject Target, Zone Z, KingdomArchitectureIntent BeforeIntent,
			KingdomArchitectureIntent Successor, ArchitecturePlacement BeforePlacement,
			ArchitecturePlacement AfterPlacement, string Lot, HashSet<GameObject> Settled,
			out string Failure)
		{
			Failure = null;
			string idProperty = OutputId(BeforePlacement);
			string id = Owner.GetStringProperty(idProperty);
			string retainProperty = UpgradeRetain(BeforePlacement);
			int retain = Owner.GetIntProperty(retainProperty);
			if (Owner.HasIntProperty(idProperty) || !Owner.HasStringProperty(idProperty)
				|| string.IsNullOrEmpty(id)
				|| id.Length > KingdomConstructionRules.MaxSubjectChars
				|| !Owner.HasIntProperty(retainProperty)
				|| Owner.HasStringProperty(retainProperty) || retain < 0 || retain > 2)
				return UpgradeQuarantine(Owner,
					"retained expansion receipt is absent, partial, or opposite-typed", out Failure);
			ArchitectureOutputPrefix target = RetainedTargetPrefix(Target, AfterPlacement, id);
			if (!KingdomArchitectureReceiptPrefixRules.LegalRetainedTarget(retain, target))
				return UpgradeQuarantine(Owner, "retained expansion target carries an impossible "
					+ "ID/state prefix", out Failure);
			KingdomPhysicalLookupState found = KingdomConstruction.FindExactId(Z, id,
				out GameObject exact);
			if (found != KingdomPhysicalLookupState.Exact)
				return UpgradeQuarantine(Owner, "retained expansion output is absent, duplicated, "
					+ "or moved", out Failure);
			if (target == ArchitectureOutputPrefix.Empty
				|| target == ArchitectureOutputPrefix.StateOnly
				|| retain == 0)
				return ExactComponent(Owner, exact, Z, BeforeIntent, Lot, BeforePlacement, id)
					|| UpgradeQuarantine(Owner, "unpublished retained expansion output changed",
						out Failure);
			if (target == ArchitectureOutputPrefix.Published && retain == 1)
			{
				if (!TryExactRetagPrefix(exact, Z, BeforeIntent, Successor, Lot,
					BeforePlacement, AfterPlacement, id, out Failure))
					return UpgradeQuarantine(Owner, "retained expansion retag prefix is third, "
						+ "duplicated, or moved: " + Failure, out Failure);
				Settled.Add(exact);
				return true;
			}
			if (target == ArchitectureOutputPrefix.Settled
				&& ExactComponent(Owner, exact, Z, Successor, Lot, AfterPlacement, id)
				&& exact.GetIntProperty(ComponentCarriedProperty) == 1)
			{
				Settled.Add(exact);
				return true;
			}
			return UpgradeQuarantine(Owner,
				"retained expansion output does not match its exact receipt phase", out Failure);
		}

		private static bool TryReadAddedExpansionOutput(GameObject Owner, GameObject Target,
			Zone Z, KingdomArchitectureIntent Successor, ArchitectureLayoutSnapshot After,
			ArchitecturePlacement Placement, string Lot, HashSet<GameObject> Settled,
			out string Failure)
		{
			Failure = null;
			ArchitectureOutputPrefix prefix = OwnerOutputPrefix(Target, Placement, null);
			if (prefix == ArchitectureOutputPrefix.Empty) return true;
			string id = Target.GetStringProperty(OutputId(Placement));
			if (prefix == ArchitectureOutputPrefix.Malformed
				|| prefix == ArchitectureOutputPrefix.StateOnly || string.IsNullOrEmpty(id)
				|| id.Length > KingdomConstructionRules.MaxSubjectChars)
				return UpgradeQuarantine(Owner,
					"added expansion output carries a malformed ID/state prefix", out Failure);
			if (prefix == ArchitectureOutputPrefix.IdOnly)
				return TryProveIdFirstOutput(Target, Z, Successor, After, Lot, Placement, id)
					|| UpgradeQuarantine(Owner,
						"added expansion output has foreign ID-first custody", out Failure);
			KingdomPhysicalLookupState found = KingdomConstruction.FindExactId(Z, id,
				out GameObject exact);
			if (found == KingdomPhysicalLookupState.Absent
				&& prefix == ArchitectureOutputPrefix.Published)
				return TryProveIdFirstOutput(Target, Z, Successor, After, Lot, Placement, id)
					|| UpgradeQuarantine(Owner,
						"published expansion output has no exact staging custody", out Failure);
			if (found != KingdomPhysicalLookupState.Exact
				|| !ExactComponent(Target, exact, Z, Successor, Lot, Placement, id))
				return UpgradeQuarantine(Owner, "added expansion output is foreign, duplicated, "
					+ "moved, or changed", out Failure);
			Settled.Add(exact);
			return true;
		}

		private static bool TryRetainedPair(ArchitectureLayoutDelta Delta,
			ArchitecturePlacement After, out ArchitecturePlacement Before)
		{
			Before = null;
			for (int i = 0; i < Delta.RetainedAfter.Count; i++)
				if (ReferenceEquals(Delta.RetainedAfter[i], After)
					|| Delta.RetainedAfter[i].Slot == After.Slot)
				{
					Before = Delta.Retained[i];
					return true;
				}
			return false;
		}
	}
}
