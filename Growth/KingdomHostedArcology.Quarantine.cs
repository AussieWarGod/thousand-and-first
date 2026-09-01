using System;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>Exact authority transition for a hosted shell that can no longer prove itself.
	/// Projection slots are cleared only after the active carrier is reproved byte-for-byte.</summary>
	public static partial class KingdomHostedArcology
	{
		private static bool TryQuarantineAuthority(r_KingdomArcology Root, string Reason,
			out string Failure)
		{
			Failure = null;
			GameObject shell = Root?.ParentObject;
			Zone zone = shell?.CurrentZone;
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			string fault = QuarantineReason(Reason);
			if (The.Game == null || system == null || !system.Founded
				|| !GameObject.Validate(shell) || shell.GetPart<r_KingdomArcology>() != Root
				|| zone == null || string.IsNullOrEmpty(shell.IDIfAssigned)
				|| string.IsNullOrEmpty(system.RealmId))
				return QuarantineFail("hosted quarantine lacks an exact loaded authority", out Failure);
			if (!TryReadAuthoritySlots(out KingdomHostedArcologyAuthority first,
				out KingdomHostedArcologyAuthority second, out Failure)) return false;
			int slot = first != null && first.RealmId == system.RealmId ? 0
				: second != null && second.RealmId == system.RealmId ? 1 : -1;
			KingdomHostedArcologyAuthority current = slot == 0 ? first : slot == 1 ? second : null;
			string settlement = system.SettlementIdForOwnedZone(zone.ZoneID);
			if (current == null || current.ZoneId != zone.ZoneID
				|| current.SettlementId != settlement || current.CarrierId != shell.IDIfAssigned
				|| (current.Phase != KingdomHostedAuthorityPhase.Active
					&& current.Phase != KingdomHostedAuthorityPhase.Quarantined))
				return QuarantineFail("hosted quarantine does not name this exact carrier", out Failure);
			if (current.Phase == KingdomHostedAuthorityPhase.Active)
			{
				string expected = KingdomHostedArcologyReceiptCodec.EncodeAuthority(current);
				KingdomHostedArcologyAuthority changed = new KingdomHostedArcologyAuthority {
					Phase = KingdomHostedAuthorityPhase.Quarantined,
					RealmId = current.RealmId, SettlementId = current.SettlementId,
					ZoneId = current.ZoneId, CarrierId = current.CarrierId,
					ConstructionJobId = current.ConstructionJobId, Fault = fault };
				string encoded = KingdomHostedArcologyReceiptCodec.EncodeAuthority(changed);
				if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(encoded)
					|| The.Game.GetStringGameState(AuthoritySlotKeys[slot], "") != expected)
					return QuarantineFail("hosted authority changed before quarantine", out Failure);
				The.Game.SetStringGameState(AuthoritySlotKeys[slot], encoded);
				if (The.Game.GetStringGameState(AuthoritySlotKeys[slot], "") != encoded)
					return QuarantineFail("hosted quarantine did not persist exactly", out Failure);
				current = changed;
			}
			if (!ClearHostedProjectionSlots(slot, out string clearFailure))
				KingdomLog.Log("hosted quarantine projection clear failed: " + clearFailure);
			Root.QuarantineReason = current.Fault;
			return true;
		}

		private static bool ClearHostedProjectionSlots(int Slot, out string Failure)
		{
			Failure = null;
			return ClearHostedProjectionSlot(Slot,
				KingdomHostedArcologyTopology.WardLotKey, out Failure)
				&& ClearHostedProjectionSlot(Slot,
					KingdomHostedArcologyTopology.TerraceLotKey, out Failure);
		}

		private static bool ClearHostedProjectionSlot(int Slot, string LotKey,
			out string Failure)
		{
			Failure = null;
			int key = DepartureKeyIndex(Slot, LotKey);
			if (The.Game == null || key < 0)
				return QuarantineFail("hosted projection clear has no exact slot", out Failure);
			The.Game.SetStringGameState(DepartureSlotKeys[key], "");
			return The.Game.GetStringGameState(DepartureSlotKeys[key], "") == ""
				|| QuarantineFail("hosted projection slot did not clear exactly", out Failure);
		}

		private static string QuarantineReason(string Reason)
		{
			if (string.IsNullOrEmpty(Reason)) return "ambiguous hosted-shell evidence";
			return Reason.Length <= 512 ? Reason : Reason.Substring(0, 512);
		}

		private static bool QuarantineFail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
