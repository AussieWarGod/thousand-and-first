using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitectureStamper
	{
		/// <summary>
		/// Repairs only the ID-first cut emitted by TrySettlePlacement. The exact detached staging
		/// root (or immutable existing-authority object) must prove the ID before state publication.
		/// Every other malformed pair is foreign and quarantines the layout owner.
		/// </summary>
		private static bool TryReadOwnerForStaging(GameObject Owner, Zone Z,
			out KingdomArchitectureIntent Intent, out ArchitectureLayoutSnapshot Snapshot,
			out string Lot, out string Failure)
		{
			Intent = null;
			Snapshot = null;
			Lot = null;
			Failure = null;
			if (!TryReadOwnerHeader(Owner, out Intent, out Snapshot, out Lot, out Failure))
				return false;
			for (int i = 0; i < Snapshot.Placements.Count; i++)
			{
				ArchitecturePlacement placement = Snapshot.Placements[i];
				ArchitectureOutputPrefix prefix = OwnerOutputPrefix(Owner, placement, null);
				if (prefix == ArchitectureOutputPrefix.IdOnly)
				{
					string id = Owner.GetStringProperty(OutputId(placement));
					if (string.IsNullOrEmpty(id)
						|| id.Length > KingdomConstructionRules.MaxSubjectChars
						|| !TryProveIdFirstOutput(Owner, Z, Intent, Snapshot, Lot,
							placement, id))
						return Quarantine(Owner, "layout slot " + placement.Slot
							+ " has a foreign ID-first publication cut", out Failure);
					try { Owner.SetIntProperty(OutputState(placement), 1); }
					catch (System.Exception exception)
					{
						return Fail("layout slot " + placement.Slot
							+ " ID-first recovery remains retryable: " + exception.Message,
							out Failure);
					}
				}
				else if (prefix == ArchitectureOutputPrefix.Malformed
					|| prefix == ArchitectureOutputPrefix.StateOnly)
					return Quarantine(Owner, "layout slot " + placement.Slot
						+ " has an impossible output publication prefix", out Failure);
			}
			return TryReadOwner(Owner, out Intent, out Snapshot, out Lot, out Failure);
		}

		private static bool TryProveIdFirstOutput(GameObject Owner, Zone Z,
			KingdomArchitectureIntent Intent, ArchitectureLayoutSnapshot Snapshot, string Lot,
			ArchitecturePlacement Placement, string Id)
		{
			if (Placement.ExistingAuthority)
				return TryFindExistingAt(Z, Placement,
					ExpectedPlacementCell(Z, Intent, Snapshot, Placement),
					out GameObject existing, out _) && existing.IDIfAssigned == Id;
			KingdomPhysicalLookupState state = FindStagingRootForPlacement(Lot,
				Intent.SnapshotHash, Placement, out GameObject rooted);
			return state == KingdomPhysicalLookupState.Exact
				&& rooted.IDIfAssigned == Id;
		}

		private static Cell ExpectedPlacementCell(Zone Z, KingdomArchitectureIntent Intent,
			ArchitectureLayoutSnapshot Snapshot, ArchitecturePlacement Placement)
		{
			if (Z == null || !KingdomArchitectureRuntime.TryWorldPlacement(Snapshot,
				Intent.Rect, Placement, out int x, out int y, out _)) return null;
			return Z.GetCell(x, y);
		}
	}
}
