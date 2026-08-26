using System;
using System.Collections.Generic;
using System.IO;
using Genkit;
using Qud.API;
using XRL;
using XRL.CharacterBuilds.Qud;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;
using XRL.World.WorldBuilders;

namespace ThousandAndFirst
{
	public sealed partial class KingdomInheritanceState
	{
		private bool TryGetReservation(out KingdomSealRecord Legacy, out KingdomSealReceipt Receipt)
		{
			Legacy = null;
			Receipt = null;
			KingdomSealFault fault;
			string detail;
			return LegacyText.Length > 0 && ReceiptText.Length > 0
				&& LegacyText.Length <= KingdomSealFormat.MaxFileChars
				&& ReceiptText.Length <= KingdomSealFormat.MaxFileChars
				&& KingdomSealRecord.TryParse(LegacyText, out Legacy, out fault, out detail)
				&& KingdomSealReceipt.TryParse(ReceiptText, out Receipt)
				&& CanonicalReservation(Legacy, Receipt, The.Game == null ? "" : The.Game.GameID)
				&& Legacy.Compose() == LegacyText && Receipt.Compose() == ReceiptText;
		}

		private static bool CanonicalReservation(KingdomSealRecord Legacy,
			KingdomSealReceipt Receipt, string TargetGameId)
		{
			return Legacy != null && Receipt != null
				&& Legacy.Status == KingdomSealStatus.Promoted && Legacy.IsResolved
				&& Receipt.State == KingdomSealReceiptState.Reserved
				&& Receipt.LineageId == Legacy.LineageId && Receipt.LegacyId == Legacy.LegacyId
				&& Receipt.TargetGameId == TargetGameId;
		}

		private void SetRepair(string Detail)
		{
			FailureDetail = Bound(Detail, MaxFailureChars);
			if (Phase != KingdomInheritancePhase.RepairRequired)
			{
				if (KingdomInheritanceStateRules.CanTransition(Phase,
					KingdomInheritancePhase.RepairRequired))
				{
					PhaseValue = (int)KingdomInheritancePhase.RepairRequired;
				}
				else
				{
					PhaseValue = (int)KingdomInheritancePhase.RepairRequired;
				}
			}
			try
			{
				LogFailure(FailureDetail);
			}
			catch (Exception)
			{
				// Neutralization must not be undone by a diagnostic sink failure.
			}
		}

		private void Transition(KingdomInheritancePhase Next)
		{
			if (!KingdomInheritanceStateRules.CanTransition(Phase, Next))
			{
				SetRepair("the inherited target attempted an invalid phase transition from "
					+ Phase.ToString() + " to " + Next.ToString());
				return;
			}
			PhaseValue = (int)Next;
		}

		private void AnnounceFailure()
		{
			if (FailureAnnounced || string.IsNullOrEmpty(FailureDetail)
				|| (Phase != KingdomInheritancePhase.Refused
					&& Phase != KingdomInheritancePhase.RepairRequired))
			{
				return;
			}
			FailureAnnounced = true;
			MessageQueue.AddPlayerMessage("&yAn inherited kingdom could not enter this world: &Y"
				+ FailureDetail);
		}

		private void ResetNewGame()
		{
			SerializationVersion = CurrentSerializationVersion;
			PhaseValue = (int)KingdomInheritancePhase.Empty;
			LegacyText = "";
			ReceiptText = "";
			CommittedReceiptText = "";
			TargetZoneId = "";
			TargetTerrainBlueprint = "";
			TargetTerrainRank = -1;
			SecretId = "";
			SiteName = "";
			FailureDetail = "";
			ApplyStatusValue = -1;
			ApplyFaultValue = -1;
			ApplicationMarker = "";
			FailureAnnounced = false;
			ReleasePending = false;
			OwnsSkipTerrainBuilders = false;
			OwnsNoBiomes = false;
			OwnsZoneName = false;
			RecoveryDisabled = false;
			RetryAuthorized = false;
			ProfileReceiptWasCommitted = false;
			ProfileCommittedReceipt = null;
			ReservationLease = null;
			ReservedMap = null;
			ReservedWorldInfo = null;
			TargetX = -1;
			TargetY = -1;
			ReservedTerrainTag = "";
		}

		private void DisableRecovery(string Detail)
		{
			SerializationVersion = CurrentSerializationVersion;
			PhaseValue = (int)KingdomInheritancePhase.RepairRequired;
			LegacyText = "";
			ReceiptText = "";
			CommittedReceiptText = "";
			TargetZoneId = "";
			TargetTerrainBlueprint = "";
			TargetTerrainRank = -1;
			SecretId = "";
			SiteName = "";
			FailureDetail = Bound(Detail, MaxFailureChars);
			ApplyStatusValue = -1;
			ApplyFaultValue = -1;
			ApplicationMarker = "";
			FailureAnnounced = false;
			ReleasePending = false;
			OwnsSkipTerrainBuilders = false;
			OwnsNoBiomes = false;
			OwnsZoneName = false;
			RecoveryDisabled = true;
			RetryAuthorized = false;
			ProfileReceiptWasCommitted = false;
			ProfileCommittedReceipt = null;
			ReservationLease = null;
			ReservedMap = null;
			ReservedWorldInfo = null;
			TargetX = -1;
			TargetY = -1;
			ReservedTerrainTag = "";
			try
			{
				LogFailure(FailureDetail);
			}
			catch (Exception)
			{
				// Neutralization must not be undone by a diagnostic sink failure.
			}
		}

		private static string ComposeMapNote(KingdomSealRecord Legacy)
		{
			string name = KingdomSealRules.SanitizeText(Legacy.SettlementName,
				KingdomSealRecord.MaxNameChars);
			if (string.IsNullOrEmpty(name))
			{
				name = "an inherited settlement";
			}
			return "the inherited seat of " + name;
		}

		private static string Category(KingdomSealRecord Legacy)
		{
			return Legacy.InheritedState <= (int)KingdomRules.InheritedState.Faded
				? "Settlements" : "Historic Sites";
		}

		private static string Bound(string Value, int MaxChars)
		{
			if (string.IsNullOrEmpty(Value))
			{
				return "";
			}
			return Value.Length <= MaxChars ? Value : Value.Substring(0, MaxChars);
		}

		private static string Nonempty(string Value, string Fallback)
		{
			return string.IsNullOrEmpty(Value) ? Fallback : Value;
		}

		private static string AppendFailure(string Existing, string Addition)
		{
			if (string.IsNullOrEmpty(Addition))
			{
				return Existing ?? "";
			}
			return Bound(string.IsNullOrEmpty(Existing) ? Addition : Existing + "; " + Addition,
				MaxFailureChars);
		}

		private static void LogFailure(string Detail)
		{
			MetricsManager.LogWarning("ThousandAndFirst inheritance: " + Detail);
		}
	}
}
