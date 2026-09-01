using System;
using XRL;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public sealed partial class KingdomSuccession
	{
		/// <summary>Read-only projection of exact resident identities frozen by succession.
		/// It neither repairs nor clears a receipt; torn state refuses every transition.</summary>
		internal bool TryProjectResidentTransitionAuthority(KingdomSystem System,
			GameObject Body, int ResidentId, out KingdomSuccessionResidentAuthority Authority,
			out bool Protected)
		{
			Authority = default(KingdomSuccessionResidentAuthority);
			Protected = false;
			if (LoadFailed || System == null || !GameObject.Validate(Body) || ResidentId <= 0)
				return false;
			if (SuccessionDisabled) return ScopedResidentStateEmpty();

			string pending = PendingDeathToken ?? "";
			string completed = CompletedDeathToken ?? "";
			KingdomSuccessionSelectionReceipt selection =
				default(KingdomSuccessionSelectionReceipt);
			bool hasSelection = !string.IsNullOrEmpty(PendingSelectionReceipt);
			if (hasSelection && (!KingdomSuccessionSelectionReceipt.TryDecode(
				PendingSelectionReceipt, out selection)
				|| !OwnsRealm(System, selection.RealmId)
				|| !string.Equals(selection.DeathToken, pending, StringComparison.Ordinal)
					&& !string.Equals(selection.DeathToken, completed,
						StringComparison.Ordinal))) return false;

			bool accessionOwner = false;
			bool repairOwner = false;
			string repairSettlement = null;
			string repairName = null;
			if (!TryProjectPendingRite(Body, ResidentId, pending, hasSelection, selection,
				ref accessionOwner, ref Protected)) return false;
			if (!TryProjectRepair(System, Body, ResidentId, pending, ref accessionOwner,
				ref repairOwner, ref repairSettlement, ref repairName)) return false;
			if (hasSelection && !TryProjectSelection(Body, ResidentId, selection,
				pending, ref accessionOwner, ref Protected)) return false;
			if (!TryProjectSeatKeeper(System, Body, ResidentId, hasSelection, selection,
				ref Protected)) return false;
			Authority = new KingdomSuccessionResidentAuthority(accessionOwner, repairOwner,
				repairSettlement, repairName);
			return true;
		}

		private bool TryProjectPendingRite(GameObject Body, int ResidentId, string Pending,
			bool HasSelection, KingdomSuccessionSelectionReceipt Selection,
			ref bool AccessionOwner, ref bool Protected)
		{
			bool anyIdentity = PendingRiteStage != MourningRiteStage.None
				|| PendingHeirResidentId != 0 || !string.IsNullOrEmpty(PendingHeirObjectId)
				|| !string.IsNullOrEmpty(PendingHeirName)
				|| !string.IsNullOrEmpty(PendingHeirZoneId);
			if (string.IsNullOrEmpty(Pending))
				return !anyIdentity;
			if (!ValidDeathToken(Pending)) return false;
			if (LegacyPhysicalRiteUnavailable)
				return PendingAccessionRepairResidentId > 0 && !anyIdentity;
			if (PendingRiteStage < MourningRiteStage.Frozen
				|| PendingRiteStage > MourningRiteStage.BodyCrossed
				|| PendingHeirResidentId <= 0 || string.IsNullOrEmpty(PendingHeirObjectId)
				|| string.IsNullOrEmpty(PendingHeirName)
				|| string.IsNullOrEmpty(PendingHeirZoneId) || !HasSelection
				|| Selection.HeirResidentId != PendingHeirResidentId
				|| !string.Equals(Selection.HeirName, PendingHeirName,
					StringComparison.Ordinal)
				|| !string.Equals(Selection.DeathToken, Pending,
					StringComparison.Ordinal)) return false;
			if (PendingHeirResidentId != ResidentId) return true;
			if (!BodyMatches(Body, ResidentId, PendingHeirName, PendingHeirObjectId,
				PendingHeirZoneId)) return false;
			AccessionOwner = true;
			Protected = true;
			return true;
		}

		private bool TryProjectRepair(KingdomSystem System, GameObject Body, int ResidentId,
			string Pending, ref bool AccessionOwner, ref bool RepairOwner,
			ref string RepairSettlement, ref string RepairName)
		{
			bool legacySeated = ReadLegacyAccessionRepairSeated();
			bool any = PendingAccessionRepairResidentId != 0
				|| !string.IsNullOrEmpty(PendingAccessionRepairFounderName)
				|| !string.IsNullOrEmpty(PendingAccessionRepairHeirName)
				|| !string.IsNullOrEmpty(PendingAccessionRepairSettlementId)
				|| legacySeated || PendingAccessionRepairArrivedTick != 0L
				|| !string.IsNullOrEmpty(PendingAccessionRepairKeptCreeds);
			if (!any) return true;
			if (PendingAccessionRepairResidentId <= 0 || !ValidDeathToken(Pending)
				|| string.IsNullOrEmpty(PendingAccessionRepairHeirName)
				|| PendingAccessionRepairHeirName.Length > KingdomSealRecord.MaxNameChars
				|| !KingdomIdentityRules.IsSettlementId(
					PendingAccessionRepairSettlementId) || legacySeated
				|| PendingPhase != InterregnumPhase.RiteDue
				|| !LegacyPhysicalRiteUnavailable
					&& (PendingRiteStage != MourningRiteStage.BodyCrossed
						|| PendingHeirResidentId != PendingAccessionRepairResidentId
						|| !string.Equals(PendingHeirName,
							PendingAccessionRepairHeirName, StringComparison.Ordinal))) return false;
			GameObject player = The.Player;
			if (!BodyMatches(player, PendingAccessionRepairResidentId,
				PendingAccessionRepairHeirName,
				LegacyPhysicalRiteUnavailable ? null : PendingHeirObjectId,
				LegacyPhysicalRiteUnavailable ? null : PendingHeirZoneId)
				|| !player.IsPlayer()) return false;
			if (PendingAccessionRepairResidentId != ResidentId) return true;
			if (!ReferenceEquals(player, Body)) return false;
			AccessionOwner = true; RepairOwner = true;
			RepairSettlement = PendingAccessionRepairSettlementId;
			RepairName = PendingAccessionRepairHeirName;
			return true;
		}

		private static bool TryProjectSelection(GameObject Body, int ResidentId,
			KingdomSuccessionSelectionReceipt Selection, string Pending,
			ref bool AccessionOwner, ref bool Protected)
		{
			if (Selection.HeirResidentId == ResidentId)
			{
				if (!BodyMatches(Body, ResidentId, Selection.HeirName, null, null))
					return false;
				Protected = true;
				if (string.Equals(Selection.DeathToken, Pending,
					StringComparison.Ordinal)) AccessionOwner = true;
			}
			if (Selection.LawHeirResidentId == ResidentId)
			{
				if (!BodyMatches(Body, ResidentId, Selection.LawHeirName, null, null))
					return false;
				Protected = true;
			}
			return true;
		}

		private bool TryProjectSeatKeeper(KingdomSystem System, GameObject Body,
			int ResidentId, bool HasSelection, KingdomSuccessionSelectionReceipt Selection,
			ref bool Protected)
		{
			bool any = !string.IsNullOrEmpty(ActiveSeatClimbRealmId)
				|| !string.IsNullOrEmpty(ActiveSeatClimbToken)
				|| ActiveSeatKeeperResidentId != 0
				|| !string.IsNullOrEmpty(ActiveSeatKeeperName);
			if (!any) return true;
			if (string.IsNullOrEmpty(ActiveSeatClimbRealmId)
				|| string.IsNullOrEmpty(ActiveSeatClimbToken)
				|| ActiveSeatKeeperResidentId <= 0
				|| string.IsNullOrEmpty(ActiveSeatKeeperName)
				|| !OwnsRealm(System, ActiveSeatClimbRealmId)
				|| !ValidDeathToken(ActiveSeatClimbToken)
				|| !HasSelection && !string.Equals(ActiveSeatClimbToken,
					CompletedSeatConsequenceToken, StringComparison.Ordinal)
				|| HasSelection && (!string.Equals(Selection.DeathToken,
					ActiveSeatClimbToken, StringComparison.Ordinal)
					|| Selection.LawHeirResidentId != ActiveSeatKeeperResidentId
					|| !string.Equals(Selection.LawHeirName, ActiveSeatKeeperName,
						StringComparison.Ordinal))) return false;
			if (ActiveSeatKeeperResidentId != ResidentId) return true;
			if (!BodyMatches(Body, ResidentId, ActiveSeatKeeperName, null, null))
				return false;
			Protected = true;
			return true;
		}

		private bool ScopedResidentStateEmpty()
		{
			return string.IsNullOrEmpty(PendingDeathToken)
				&& string.IsNullOrEmpty(PendingSelectionReceipt)
				&& PendingHeirResidentId == 0 && string.IsNullOrEmpty(PendingHeirObjectId)
				&& string.IsNullOrEmpty(PendingHeirName)
				&& string.IsNullOrEmpty(PendingHeirZoneId)
				&& PendingRiteStage == MourningRiteStage.None
				&& PendingAccessionRepairResidentId == 0
				&& string.IsNullOrEmpty(PendingAccessionRepairFounderName)
				&& string.IsNullOrEmpty(PendingAccessionRepairHeirName)
				&& string.IsNullOrEmpty(PendingAccessionRepairSettlementId)
				&& !ReadLegacyAccessionRepairSeated()
				&& PendingAccessionRepairArrivedTick == 0L
				&& string.IsNullOrEmpty(PendingAccessionRepairKeptCreeds)
				&& string.IsNullOrEmpty(ActiveSeatClimbRealmId)
				&& string.IsNullOrEmpty(ActiveSeatClimbToken)
				&& ActiveSeatKeeperResidentId == 0
				&& string.IsNullOrEmpty(ActiveSeatKeeperName);
		}

		private static bool ValidDeathToken(string Token)
		{
			return KingdomSuccessionRules.TryReadDeathToken(Token, out int _, out long _);
		}

		private static bool OwnsRealm(KingdomSystem System, string RealmId)
		{
			return !string.IsNullOrEmpty(RealmId) && (string.Equals(System?.RealmId,
				RealmId, StringComparison.Ordinal) || string.Equals(
				System?.ExiledRealmArchive?.RealmId, RealmId, StringComparison.Ordinal));
		}

		private static bool BodyMatches(GameObject Body, int ResidentId, string Name,
			string ObjectId, string ZoneId)
		{
			return GameObject.Validate(Body) && Body.IsAlive
				&& Body.GetIntProperty(KingdomResidents.ResidentIdProperty) == ResidentId
				&& string.Equals(Body.GetStringProperty("KingdomName"), Name,
					StringComparison.Ordinal)
				&& (string.IsNullOrEmpty(ObjectId) || string.Equals(Body.IDIfAssigned,
					ObjectId, StringComparison.Ordinal))
				&& (string.IsNullOrEmpty(ZoneId) || string.Equals(Body.CurrentZone?.ZoneID,
					ZoneId, StringComparison.Ordinal));
		}
	}
}
