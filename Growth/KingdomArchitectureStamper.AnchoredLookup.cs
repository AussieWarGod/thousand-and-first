using System;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitectureStamper
	{
		/// <summary>Re-proves the exact current layout root and its frozen world pose.</summary>
		internal static bool TryExactLayoutOwner(GameObject Owner, Zone Z,
			out KingdomArchitectureIntent Intent, out ArchitectureLayoutSnapshot Snapshot,
			out string Lot, out string Failure)
		{
			Intent = null;
			Snapshot = null;
			Lot = null;
			Failure = null;
			GameObject exactOwner;
			if (!GameObject.Validate(Owner) || Z == null
				|| string.IsNullOrEmpty(Owner.IDIfAssigned)
				|| !TryReadOwner(Owner, out Intent, out Snapshot, out Lot, out Failure)
				|| !KingdomArchitectureRules.IsManagedSnapshotEncoding(Intent.EncodedSnapshot))
				return Failure != null ? false : Fail(
					"exact layout owner needs a current receipt", out Failure);
			if (KingdomConstruction.FindExactId(Z, Owner.IDIfAssigned, out exactOwner)
					!= KingdomPhysicalLookupState.Exact || !ReferenceEquals(exactOwner, Owner)
				|| Owner.CurrentZone != Z || Owner.CurrentCell != Z.GetCell(
					Intent.MainWorldX, Intent.MainWorldY)
				|| Intent.Rect.X1 < 0 || Intent.Rect.Y1 < 0
				|| Intent.Rect.X2 >= Z.Width || Intent.Rect.Y2 >= Z.Height
				|| Owner.GetStringProperty(KingdomPlots.PlotIdProperty) != Lot)
				return Fail("layout owner left its exact identity, lot, rectangle, or main cell",
					out Failure);
			return true;
		}

		/// <summary>Resolve one authored functional role to the exact receipted component.
		/// Role identity comes from the frozen snapshot; object identity, lot custody, world pose,
		/// and component receipt all come from the completed layout owner.</summary>
		internal static bool TryExactAnchoredComponent(GameObject Owner, Zone Z, string Role,
			out GameObject Exact, out string Failure)
		{
			Exact = null;
			Failure = null;
			if (string.IsNullOrEmpty(Role)
				|| Role.Length > KingdomArchitectureRules.MaxKeyChars
				|| !TryExactLayoutOwner(Owner, Z, out KingdomArchitectureIntent intent,
					out ArchitectureLayoutSnapshot snapshot, out string lot, out Failure))
				return Failure != null ? false : Fail("layout role is malformed", out Failure);

			ArchitecturePlacement anchored = null;
			int matches = 0;
			for (int i = 0; i < snapshot.Placements.Count; i++)
			{
				ArchitecturePlacement placement = snapshot.Placements[i];
				if (AnchorRoleOf(placement.StatefulAnchor) != Role) continue;
				anchored = placement;
				matches++;
			}
			if (matches != 1)
				return Fail("layout role " + Role + " is absent or ambiguous", out Failure);
			if (!TryExactOutput(Owner, Z, intent, lot, anchored, out Exact, out Failure))
				return false;
			return (GameObject.Validate(Exact) && Exact.CurrentZone == Z
				&& Exact.CurrentCell != null && intent.Rect.Contains(
					Exact.CurrentCell.X, Exact.CurrentCell.Y))
				|| Fail("anchored component stands outside its frozen lot", out Failure);
		}

		/// <summary>Proves that Candidate is one exact settled output of Owner's frozen layout.
		/// Used by systems which care about physical custody but do not care which authored role
		/// placed the component. A copied receipt, moved fixture, duplicate slot, or old layout hash
		/// fails through the ordinary output verifier.</summary>
		internal static bool IsExactComponentOf(GameObject Owner, Zone Z, GameObject Candidate)
		{
			if (!GameObject.Validate(Candidate)
				|| !TryExactLayoutOwner(Owner, Z, out KingdomArchitectureIntent intent,
					out ArchitectureLayoutSnapshot snapshot, out string lot, out _)) return false;
			string slot = Candidate.GetStringProperty(ComponentSlotProperty);
			if (string.IsNullOrEmpty(slot)) return false;
			ArchitecturePlacement found = null;
			for (int i = 0; i < snapshot.Placements.Count; i++)
			{
				if (snapshot.Placements[i].Slot != slot) continue;
				if (found != null) return false;
				found = snapshot.Placements[i];
			}
			GameObject exact;
			return found != null && TryExactOutput(Owner, Z, intent, lot, found,
				out exact, out _) && ReferenceEquals(exact, Candidate);
		}

		/// <summary>Re-proves only installed shell pieces. Missing furniture must not erase the
		/// designation; missing shell makes its covered/habitable cells unavailable.</summary>
		internal static bool TryVerifyBenefitShell(GameObject Owner, Zone Z, out string Failure)
		{
			Failure = null;
			if (!TryExactLayoutOwner(Owner, Z, out KingdomArchitectureIntent intent,
				out ArchitectureLayoutSnapshot snapshot, out string lot, out Failure)) return false;
			for (int i = 0; i < snapshot.Placements.Count; i++)
			{
				ArchitecturePlacement placement = snapshot.Placements[i];
				if (placement.Layer != ArchitectureLayer.Structure) continue;
				string id = Owner.GetStringProperty(OutputId(placement));
				GameObject exact;
				if (Owner.GetIntProperty(OutputState(placement)) != 2
					|| KingdomConstruction.FindExactId(Z, id, out exact)
						!= KingdomPhysicalLookupState.Exact
					|| !ExactComponent(Owner, exact, Z, intent, lot, placement, id))
					return Fail("authored shell component is absent, moved, or changed", out Failure);
			}
			return true;
		}

		private static string AnchorRoleOf(string Key)
		{
			int identity = Key == null ? -1 : Key.LastIndexOf('@');
			return identity < 0 ? Key : Key.Substring(0, identity);
		}
	}
}
