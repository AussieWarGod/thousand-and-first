using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitectureStamper
	{
		private static bool TryReadOwnerHeader(GameObject Owner,
			out KingdomArchitectureIntent Intent, out ArchitectureLayoutSnapshot Snapshot,
			out string LotId, out string Failure)
		{
			Intent = null;
			Snapshot = null;
			LotId = null;
			Failure = null;
			if (Owner == null || !Owner.HasIntProperty(SchemaProperty)
				|| Owner.HasStringProperty(SchemaProperty)
				|| Owner.GetIntProperty(SchemaProperty) != LayoutSchema)
				return Fail("layout owner receipt is absent, partial, or unknown", out Failure);
			if (Owner.HasIntProperty(FaultProperty))
				return Fail("layout owner fault carries an opposite-type value", out Failure);
			string fault = Owner.GetStringProperty(FaultProperty);
			if (!string.IsNullOrEmpty(fault))
				return Fail("layout owner is quarantined: " + Bounded(fault), out Failure);
			if (Owner.HasIntProperty(LotIdProperty) || Owner.HasIntProperty(HashProperty)
				|| Owner.HasStringProperty(NextLayerProperty))
				return Fail("layout owner header carries an opposite-type value", out Failure);
			string lot = Owner.GetStringProperty(LotIdProperty);
			string hash = Owner.GetStringProperty(HashProperty);
			if (!ValidLotId(lot) || hash == null || hash.Length != 64
				|| !KingdomArchitectureRuntime.TryRead(Owner, out Intent, out Failure)
				|| !KingdomArchitectureRuntime.TryDecode(Intent, out Snapshot, out Failure)
				|| !KingdomArchitectureRules.IsManagedSnapshotEncoding(Intent.EncodedSnapshot)
				|| hash != Intent.SnapshotHash)
				return Failure != null ? false : Fail(
					"layout owner scalars disagree with its snapshot", out Failure);
			int next = Owner.GetIntProperty(NextLayerProperty);
			if (!Owner.HasIntProperty(NextLayerProperty) || next < 0 || next > 3)
				return Fail("layout owner stage is absent or malformed", out Failure);
			LotId = lot;
			return true;
		}

		private static bool TryAcceptNewOwnerPrefix(GameObject Owner,
			KingdomArchitectureIntent Intent, ArchitectureLayoutSnapshot Snapshot, string Lot,
			int Next, bool Copy, GameObject Source, out string Failure)
		{
			Failure = null;
			if (Owner.HasStringProperty(SchemaProperty)
				|| !OwnerStringPrefix(Owner, LotIdProperty, Lot)
				|| !OwnerStringPrefix(Owner, HashProperty, Intent.SnapshotHash)
				|| !OwnerIntPrefix(Owner, NextLayerProperty, Next)
				|| Owner.HasStringProperty(FaultProperty)
				|| Owner.HasIntProperty(FaultProperty))
				return Fail("layout owner prefix carries a third or opposite-type value",
					out Failure);
			for (int i = 0; i < Snapshot.Placements.Count; i++)
			{
				ArchitecturePlacement placement = Snapshot.Placements[i];
				string expected = Copy ? Source.GetStringProperty(OutputId(placement)) : null;
				ArchitectureOutputPrefix prefix = OwnerOutputPrefix(Owner, placement, expected);
				bool legal = Copy
					? prefix == ArchitectureOutputPrefix.Empty
						|| prefix == ArchitectureOutputPrefix.IdOnly
						|| prefix == ArchitectureOutputPrefix.Settled
					: prefix == ArchitectureOutputPrefix.Empty;
				if (!legal)
					return Fail("layout slot " + placement.Slot
						+ " carries an impossible owner publication prefix", out Failure);
			}
			return true;
		}

		private static ArchitectureOutputPrefix OwnerOutputPrefix(GameObject Owner,
			ArchitecturePlacement Placement, string ExpectedId)
		{
			string state = OutputState(Placement);
			string id = OutputId(Placement);
			return KingdomArchitectureReceiptPrefixRules.ClassifyOutput(
				Owner.HasIntProperty(state), Owner.GetIntProperty(state),
				Owner.HasStringProperty(state), Owner.HasStringProperty(id),
				Owner.GetStringProperty(id), Owner.HasIntProperty(id), ExpectedId);
		}

		private static bool OwnerStringPrefix(GameObject Owner, string Property,
			string Expected)
		{
			return KingdomArchitectureReceiptPrefixRules.ExactOrAbsentString(
				Owner.HasStringProperty(Property), Owner.GetStringProperty(Property),
				Owner.HasIntProperty(Property), Expected);
		}

		private static bool OwnerIntPrefix(GameObject Owner, string Property, int Expected)
		{
			return KingdomArchitectureReceiptPrefixRules.ExactOrAbsentInt(
				Owner.HasIntProperty(Property), Owner.GetIntProperty(Property),
				Owner.HasStringProperty(Property), Expected);
		}

		private static bool SameOwnerIntent(KingdomArchitectureIntent A,
			KingdomArchitectureIntent B)
		{
			return A != null && B != null && A.EncodedSnapshot == B.EncodedSnapshot
				&& A.SnapshotHash == B.SnapshotHash && SameRect(A.Rect, B.Rect)
				&& A.MainWorldX == B.MainWorldX && A.MainWorldY == B.MainWorldY;
		}

		private static bool ExactCopiedOwner(GameObject Target, GameObject Source,
			KingdomArchitectureIntent Expected, ArchitectureLayoutSnapshot Snapshot, string Lot,
			out string Failure)
		{
			KingdomArchitectureIntent observed;
			ArchitectureLayoutSnapshot ignored;
			string observedLot;
			if (!TryReadOwner(Target, out observed, out ignored, out observedLot, out Failure)
				|| observedLot != Lot || !SameOwnerIntent(observed, Expected)) return false;
			for (int i = 0; i < Snapshot.Placements.Count; i++)
				if (Target.GetStringProperty(OutputId(Snapshot.Placements[i]))
					!= Source.GetStringProperty(OutputId(Snapshot.Placements[i])))
					return Fail("copied layout owner changed a settled output identity", out Failure);
			return true;
		}
	}
}
