using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public sealed partial class KingdomSuccession
	{
		private const string ChosenSeatDeed =
			"the founder's custom raised another life while the realm's senior heir kept the Charter";

		private void TrySettleSelectionConsequence(KingdomSystem System, string Context)
		{
			if (string.IsNullOrEmpty(PendingSelectionReceipt)) return;
			KingdomSuccessionSelectionReceipt receipt;
			if (!KingdomSuccessionSelectionReceipt.TryDecode(PendingSelectionReceipt, out receipt))
			{
				KingdomLog.Log("succession: malformed selection receipt remains after " + Context);
				return;
			}
			if (!string.Equals(receipt.DeathToken, CompletedDeathToken,
				StringComparison.Ordinal)) return;
			if (!string.IsNullOrEmpty(PendingSealAccessionToken)
				&& !PendingSealAccessionReady) return;
			if (!receipt.CostsTheSeat)
			{
				PendingSelectionReceipt = "";
				return;
			}
			if (string.Equals(CompletedSeatConsequenceToken, receipt.DeathToken,
				StringComparison.Ordinal))
			{
				PendingSelectionReceipt = "";
				return;
			}
			if (string.IsNullOrEmpty(ActiveSeatClimbToken))
			{
				string keeperName;
				if (!TryIdentifyLawHeirKeeper(System, receipt, out keeperName))
				{
					KingdomLog.Log("succession: exact senior heir could not hold the Charter during "
						+ Context);
					return;
				}
				ActiveSeatClimbRealmId = receipt.RealmId;
				ActiveSeatClimbToken = receipt.DeathToken;
				ActiveSeatKeeperResidentId = receipt.LawHeirResidentId;
				ActiveSeatKeeperName = keeperName;
			}
			if (!string.Equals(ActiveSeatClimbRealmId, receipt.RealmId,
				StringComparison.Ordinal)
				|| !string.Equals(ActiveSeatClimbToken, receipt.DeathToken,
					StringComparison.Ordinal))
			{
				KingdomLog.Log("succession: a different chosen-seat climb already owns the realm");
				return;
			}
			RemoveCurrentCharterAbility();
			if (System?.ExiledRealmArchive != null
				&& string.Equals(System.ExiledRealmArchive.RealmId, receipt.RealmId,
					StringComparison.Ordinal) && System.Exiled)
			{
				CompletedSeatConsequenceToken = receipt.DeathToken;
				PendingSelectionReceipt = "";
				return;
			}
			if (System == null || !System.Founded
				|| !string.Equals(System.RealmId, receipt.RealmId, StringComparison.Ordinal))
			{
				KingdomLog.Log("succession: chosen-seat consequence awaits its exact realm after "
					+ Context);
				return;
			}
			string refusal;
			if (!System.Exile(ChosenSeatDeed, Forced: true, out refusal))
			{
				KingdomLog.Log("succession: chosen-seat consequence remains after " + Context
					+ " (" + (refusal ?? "exile transition incomplete") + ")");
				return;
			}
			if (System.ExiledRealmArchive != null
				&& string.Equals(System.ExiledRealmArchive.RealmId, receipt.RealmId,
					StringComparison.Ordinal) && System.Exiled)
			{
				CompletedSeatConsequenceToken = receipt.DeathToken;
				PendingSelectionReceipt = "";
			}
		}

		internal bool WithholdsCharter(KingdomSystem System)
		{
			if (System == null || string.IsNullOrEmpty(ActiveSeatClimbRealmId)
				|| string.IsNullOrEmpty(ActiveSeatClimbToken)) return false;
			if (string.Equals(System.RealmId, ActiveSeatClimbRealmId, StringComparison.Ordinal))
				return true;
			return !System.Founded && System.ExiledRealmArchive != null
				&& string.Equals(System.ExiledRealmArchive.RealmId, ActiveSeatClimbRealmId,
					StringComparison.Ordinal);
		}

		private void ReconcileAbandonedSeatClimb(KingdomSystem System)
		{
			if (System == null || !System.Founded || string.IsNullOrEmpty(ActiveSeatClimbRealmId)
				|| string.Equals(System.RealmId, ActiveSeatClimbRealmId,
					StringComparison.Ordinal)) return;
			string abandonedToken = ActiveSeatClimbToken;
			ActiveSeatClimbRealmId = "";
			ActiveSeatClimbToken = "";
			ActiveSeatKeeperResidentId = 0;
			ActiveSeatKeeperName = "";
			KingdomSuccessionSelectionReceipt receipt;
			if (KingdomSuccessionSelectionReceipt.TryDecode(PendingSelectionReceipt, out receipt)
				&& string.Equals(receipt.DeathToken, abandonedToken, StringComparison.Ordinal))
				PendingSelectionReceipt = "";
			KingdomLog.Log("succession: chosen-seat climb closed when another realm was founded");
		}

		internal bool ChosenSeatBlocksReturn(KingdomSystem System, out string Refusal)
		{
			Refusal = null;
			bool active = WithholdsCharter(System) && System.ExiledRealmArchive != null
				&& string.Equals(System.ExiledRealmArchive.RealmId, ActiveSeatClimbRealmId,
					StringComparison.Ordinal);
			if (!active || KingdomSuccessionRules.ChosenSeatMayReturn(true,
				System.ExiledRealmRegard())) return false;
			Refusal = "The senior heir still holds the Charter. Earn trusted regard with "
				+ KingdomPresentation.Rich(System.ExiledDisplayName) + " under "
				+ KingdomPresentation.Rich(ActiveSeatKeeperName)
				+ " before asking to claim the seat.";
			return true;
		}

		internal void CompleteChosenSeatClimb(string RealmId)
		{
			if (!string.Equals(ActiveSeatClimbRealmId, RealmId, StringComparison.Ordinal)) return;
			ActiveSeatClimbRealmId = "";
			ActiveSeatClimbToken = "";
			ActiveSeatKeeperResidentId = 0;
			ActiveSeatKeeperName = "";
			PendingSelectionReceipt = "";
		}

		private static bool TryIdentifyLawHeirKeeper(KingdomSystem System,
			KingdomSuccessionSelectionReceipt Receipt, out string KeeperName)
		{
			KeeperName = null;
			if (System == null || !System.Founded) return false;
			string seatedName;
			bool seated = ExactResident(System.City, Receipt.LawHeirResidentId,
				out seatedName);
			KingdomSettlement holder = null;
			string nonSeatName = null;
			List<KingdomSettlement> nonSeat = System.NonSeatSettlements();
			for (int i = 0; i < nonSeat.Count; i++)
			{
				if (!ExactResident(nonSeat[i]?.City, Receipt.LawHeirResidentId,
					out string candidateName)) continue;
				if (holder != null) return false;
				holder = nonSeat[i];
				nonSeatName = candidateName;
			}
			if (seated == (holder != null)) return false;
			KeeperName = seated ? seatedName : nonSeatName;
			return true;
		}

		private static bool ExactResident(KingdomCityBook Book, int ResidentId,
			out string Name)
		{
			Name = null;
			KingdomCityState state;
			KingdomCityFault fault;
			if (Book == null || !Book.TryRead(out state, out fault)) return false;
			int found = 0;
			for (int i = 0; i < state.ResidentCount; i++)
			{
				KingdomResidentRow row;
				if (state.TryResident(i, out row) && row.ResidentId == ResidentId
					&& row.Standing == KingdomResidentStanding.Resident
					&& !string.IsNullOrEmpty(row.Name))
				{
					Name = row.Name;
					found++;
				}
			}
			return found == 1;
		}

		private static void RemoveCurrentCharterAbility()
		{
			try
			{
				The.Player?.GetPart<KingdomCharterPart>()?.RemoveAbility();
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: chosen-seat Charter removal failed", ex);
			}
		}
	}
}
