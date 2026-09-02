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
		private bool EnsureReservationLease(KingdomSeal Seal, KingdomSealReceipt Reserved,
			out string Failure)
		{
			Failure = "";
			KingdomSealReservationLease held = GetReservationLease(Reserved);
			if (held != null)
			{
				ReservationLease = held;
				return true;
			}
			KingdomSealReservationLease resumed = null;
			if (Seal == null || Reserved == null
				|| !Seal.TryResumeImport(Reserved, out resumed, out Failure) || resumed == null)
			{
				Failure = Nonempty(Failure, "the exact reservation lease was unavailable");
				return false;
			}
			try
			{
				ReservationLease = KingdomInheritanceLeaseOwner.Hold(
					The.Game == null ? "" : The.Game.GameID, Reserved, resumed);
				return ReservationLease != null;
			}
			catch (Exception ex)
			{
				resumed.Dispose();
				Failure = "the resumed reservation could not become the process owner: " + ex.Message;
				return false;
			}
		}

		private KingdomSealReservationLease GetReservationLease(KingdomSealReceipt Reserved)
		{
			if (ReservationLease != null && ReservationLease.IsHeld
				&& ReservationLease.Matches(Reserved))
			{
				return ReservationLease;
			}
			return KingdomInheritanceLeaseOwner.Get(The.Game == null ? "" : The.Game.GameID,
				Reserved);
		}

		private void HoldUnreleased(string GameId, KingdomSealReceipt Receipt,
			KingdomSealReservationLease Lease)
		{
			if (Lease == null)
			{
				return;
			}
			ReservationLease = Lease;
			try
			{
				ReservationLease = Receipt == null
					? KingdomInheritanceLeaseOwner.HoldUnknown(GameId, Lease)
					: KingdomInheritanceLeaseOwner.Hold(GameId, Receipt, Lease);
			}
			catch (Exception ex)
			{
				LogFailure("a live inheritance lease could not enter the process owner: " + ex.Message);
			}
		}

		private bool TryGetCommittedReceipt(out KingdomSealReceipt Receipt)
		{
			Receipt = null;
			KingdomSealRecord legacy;
			KingdomSealReceipt reserved;
			return !string.IsNullOrEmpty(CommittedReceiptText)
				&& CommittedReceiptText.Length <= KingdomSealFormat.MaxFileChars
				&& TryGetReservation(out legacy, out reserved)
				&& KingdomSealReceipt.TryParse(CommittedReceiptText, out Receipt)
				&& Receipt != null && Receipt.Compose() == CommittedReceiptText
				&& Receipt.State == KingdomSealReceiptState.Committed
				&& Receipt.LineageId == reserved.LineageId
				&& Receipt.LegacyId == reserved.LegacyId
				&& Receipt.TargetGameId == reserved.TargetGameId
				&& Receipt.WrittenTick >= reserved.WrittenTick;
		}

		private bool RestoreMutableReservation(out string Failure)
		{
			Failure = "";
			if (ReservedMap == null)
			{
				// No site was removed yet, or a later runtime build deliberately kept it consumed.
				return Phase == KingdomInheritancePhase.Reserved || TargetX < 0 || TargetY < 0;
			}
			if (TargetX < 0 || TargetY < 0 || string.IsNullOrEmpty(ReservedTerrainTag))
			{
				Failure = "the exact mutable coordinate or terrain tag was lost";
				return false;
			}
			if (ReservedMap.GetMutable(TargetX, TargetY) == 0)
			{
				ReservedMap.AddMutableLocation(Location2D.Get(TargetX, TargetY), ReservedTerrainTag, 1);
			}
			if (ReservedMap.GetMutable(TargetX, TargetY) != 1)
			{
				Failure = "the exact mutable cell did not return to value one";
				return false;
			}
			ReservedMap = null;
			ReservedWorldInfo = null;
			TargetX = -1;
			TargetY = -1;
			ReservedTerrainTag = "";
			return true;
		}
	}
}
