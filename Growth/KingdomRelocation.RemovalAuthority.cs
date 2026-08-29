using XRL.World;

namespace ThousandAndFirst
{
	internal sealed class KingdomRelocationRemovalPlan
	{
		internal Zone Zone;
		internal string ExpectedWire;
		internal KingdomRelocationReceipt Receipt;
	}

	public static partial class KingdomRelocation
	{
		internal static bool TryPrepareRealmRemoval(KingdomSystem System, Zone Zone,
			out KingdomRelocationRemovalPlan Plan, out string Failure)
		{
			Plan = null; Failure = null;
			string raw = Zone?.GetZoneProperty(ReceiptProperty, null);
			if (string.IsNullOrEmpty(raw)) return true;
			KingdomRelocationReceipt receipt = null;
			string encoded = null;
			if (System == null || Zone == null || System.RealmId == null
				|| !TryRead(Zone, out receipt, out encoded, out Failure)
				|| receipt.RealmId != System.RealmId)
				return RemovalFail(Failure ?? "relocation authority belongs to another realm",
					out Failure);
			if (receipt.Phase == KingdomRelocationPhase.Quarantined
				&& !RecoverablyRolledBack(receipt))
				return RemovalFail("quarantined relocation has not restored its exact current lot",
					out Failure);
			Plan = new KingdomRelocationRemovalPlan
			{
				Zone = Zone, ExpectedWire = encoded, Receipt = receipt
			};
			return true;
		}

		internal static bool TryRetireForRealmRemoval(KingdomRelocationRemovalPlan Plan,
			out string Failure)
		{
			Failure = null;
			if (Plan == null) return true;
			Zone zone = Plan.Zone;
			KingdomRelocationReceipt receipt = null;
			string expected = null;
			if (zone == null || zone.GetZoneProperty(ReceiptProperty, null) != Plan.ExpectedWire
				|| !TryRead(zone, out receipt, out expected, out Failure))
				return RemovalFail(Failure ?? "relocation changed after removal preview", out Failure);
			if (receipt.Phase == KingdomRelocationPhase.Complete)
			{
				CleanCompletedArtifacts(zone, receipt);
				return TryRetire(zone, expected, receipt, out Failure);
			}
			if (!RecoverablyRolledBack(receipt))
			{
				RollbackAndQuarantine(zone, expected, receipt,
					"The founder prepared the realm for assembly removal.");
				if (!TryRead(zone, out receipt, out expected, out Failure)
					|| !RecoverablyRolledBack(receipt))
					return RemovalFail(Failure ?? "relocation rollback did not restore exact source ground",
						out Failure);
			}
			return RetireRecoveredRollback(zone, expected, receipt, out Failure);
		}

		internal static void CollectRealmRemovalArtifacts(KingdomRelocationRemovalPlan Plan,
			System.Collections.Generic.HashSet<GameObject> Into)
		{
			if (Plan?.Zone == null || Plan.Receipt?.Moves == null || Into == null) return;
			for (int i = 0; i < Plan.Receipt.Moves.Count; i++)
			{
				KingdomRelocationMove move = Plan.Receipt.Moves[i];
				AddExact(Plan.Zone, move?.FrameId, Into);
				for (int j = 0; j < (move?.StakeIds?.Length ?? 0); j++)
					AddExact(Plan.Zone, move.StakeIds[j], Into);
			}
		}

		internal static bool CollectRealmRemovalCustody(KingdomRelocationRemovalPlan Plan,
			System.Collections.Generic.List<GameObject> Into, out string Failure)
		{
			Failure = null;
			if (Plan?.Receipt?.Moves == null || Into == null) return true;
			for (int i = 0; i < Plan.Receipt.Moves.Count; i++)
			{
				KingdomRelocationMove move = Plan.Receipt.Moves[i];
				for (int j = 0; j < move.Rows.Count; j++)
					if (!AddEscrow(Plan.Receipt, move.Rows[j].ObjectId, false, Into,
						out Failure)) return false;
				for (int j = 0; j < move.Clearance.Count; j++)
					if (!AddEscrow(Plan.Receipt, move.Clearance[j].ObjectId, true, Into,
						out Failure)) return false;
			}
			return true;
		}

		private static bool AddEscrow(KingdomRelocationReceipt Receipt, string Id,
			bool Clearance, System.Collections.Generic.List<GameObject> Into,
			out string Failure)
		{
			Failure = null; GameObject item = Escrow(Receipt, Id, Clearance);
			if (item == null) return true;
			if (!GameObject.Validate(item) || item.CurrentCell != null)
				return RemovalFail("relocation escrow points to invalid or already-grounded value",
					out Failure);
			if (!Into.Contains(item)) Into.Add(item);
			return true;
		}

		private static void AddExact(Zone Zone, string Id,
			System.Collections.Generic.HashSet<GameObject> Into)
		{
			if (KingdomConstruction.FindExactId(Zone, Id, out GameObject item)
				== KingdomPhysicalLookupState.Exact) Into.Add(item);
		}

		private static bool RetireRecoveredRollback(Zone Zone, string Expected,
			KingdomRelocationReceipt Receipt, out string Failure)
		{
			Failure = null;
			if (!RecoverablyRolledBack(Receipt)
				|| Zone.GetZoneProperty(ReceiptProperty, null) != Expected)
				return RemovalFail("relocation rollback changed before retirement", out Failure);
			for (int i = 0; i < Receipt.Moves.Count; i++)
			{
				KingdomRelocationMove move = Receipt.Moves[i];
				for (int j = 0; j < move.Rows.Count; j++)
					if (Escrow(Receipt, move.Rows[j].ObjectId, false) != null)
						return RemovalFail("relocation lot escrow remains after rollback", out Failure);
				for (int j = 0; j < move.Clearance.Count; j++)
					if (Escrow(Receipt, move.Clearance[j].ObjectId, true) != null)
						return RemovalFail("relocation clearance escrow remains after rollback", out Failure);
				RemoveFrames(Zone, move);
			}
			Zone.SetZoneProperty(LastReceiptProperty, Expected);
			if (Zone.GetZoneProperty(LastReceiptProperty, null) != Expected
				|| Zone.GetZoneProperty(ReceiptProperty, null) != Expected)
				return RemovalFail("relocation rollback history did not persist exactly", out Failure);
			Zone.RemoveZoneProperty(ReceiptProperty);
			Zone.RemoveZoneProperty(FaultProperty);
			return string.IsNullOrEmpty(Zone.GetZoneProperty(ReceiptProperty, null))
				|| RemovalFail("relocation authority remains after exact rollback", out Failure);
		}

		private static bool RecoverablyRolledBack(KingdomRelocationReceipt Receipt)
		{
			if (Receipt == null || Receipt.Phase != KingdomRelocationPhase.Quarantined
				|| Receipt.CurrentMove < 0 || Receipt.CurrentMove >= Receipt.Moves.Count
				|| Receipt.Moves[Receipt.CurrentMove].Phase !=
					KingdomRelocationMovePhase.RolledBack) return false;
			KingdomRelocationMove move = Receipt.Moves[Receipt.CurrentMove];
			for (int i = 0; i < move.Rows.Count; i++)
				if (move.Rows[i].State != KingdomRelocationRowState.Source) return false;
			for (int i = 0; i < move.Clearance.Count; i++)
				if (move.Clearance[i].State != KingdomRelocationClearState.Standing) return false;
			return true;
		}

		private static bool RemovalFail(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}
	}
}
