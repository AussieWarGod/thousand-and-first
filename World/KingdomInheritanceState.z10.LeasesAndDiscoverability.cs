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

		private void HideDiscoverability(Zone Zone)
		{
			RemoveLocationFinders(Zone);
			JournalMapNote note = string.IsNullOrEmpty(SecretId) ? null : JournalAPI.GetMapNote(SecretId);
			if (note != null && note.ZoneID == TargetZoneId)
			{
				JournalAPI.DeleteMapNote(note);
			}
			string failure;
			if (!TryRemoveOwnedZoneName(out failure))
			{
				throw new InvalidDataException(failure);
			}
		}

		private void RestoreDiscoverability(Zone Zone)
		{
			KingdomSealRecord legacy;
			KingdomSealReceipt receipt;
			if (Zone == null || Zone.ZoneID != TargetZoneId
				|| !TryGetReservation(out legacy, out receipt))
			{
				throw new InvalidDataException("the exact committed target is unavailable for discovery");
			}
			EnsureOwnedMapNote(legacy);
			if (!OwnsZoneName)
			{
				if (HasAnyZoneNameFootprint())
				{
					throw new InvalidDataException(
						"the committed inherited zone has an unowned zone-name footprint");
				}
				OwnsZoneName = true;
			}
			SetOwnedZoneName();
			List<GameObject> objects = Zone.GetObjects();
			GameObject keeper = null;
			for (int i = objects.Count - 1; i >= 0; i--)
			{
				LocationFinder finder = objects[i].GetPart<LocationFinder>();
				if (finder == null || finder.ID != SecretId)
				{
					continue;
				}
				if (keeper == null)
				{
					keeper = objects[i];
					finder.Value = 1;
				}
				else
				{
					objects[i].Obliterate(null, Silent: true);
				}
			}
			if (keeper == null)
			{
				new XRL.World.ZoneBuilders.AddLocationFinder
				{
					SecretID = SecretId,
					Value = 1
				}.BuildZone(Zone);
			}
		}

		private void TryRestoreDiscoverability(Zone Zone)
		{
			try
			{
				RestoreDiscoverability(Zone);
			}
			catch (Exception ex)
			{
				BestEffortHideBrokenDiscovery(Zone);
				RecordDiscoveryFailure(ex.Message);
			}
		}

		private void EnsureOwnedMapNote(KingdomSealRecord Legacy)
		{
			if (Legacy == null)
			{
				throw new InvalidDataException("the inherited map note lost its legacy payload");
			}
			string expectedCategory = Category(Legacy);
			string expectedText = ComposeMapNote(Legacy);
			JournalMapNote note = JournalAPI.GetMapNote(SecretId);
			if (note != null && note.ZoneID != TargetZoneId)
			{
				throw new InvalidDataException("the inherited map-note id belongs to another zone");
			}
			if (note != null && !KingdomInheritanceStateRules.IsUsableOwnedMapNote(
				true, true, note.Attributes != null, note.Category, note.Text,
				expectedCategory, expectedText))
			{
				JournalAPI.DeleteMapNote(note);
				note = null;
			}
			if (note == null)
			{
				JournalAPI.AddMapNote(TargetZoneId, expectedText, expectedCategory,
					new string[4] { "settlement", "historic", "taf", "inheritance" },
					SecretId, revealed: true, sold: false, 0L, silent: true);
				note = JournalAPI.GetMapNote(SecretId);
			}
			if (!KingdomInheritanceStateRules.IsUsableOwnedMapNote(note != null,
				note != null && note.ZoneID == TargetZoneId,
				note != null && note.Attributes != null, note == null ? null : note.Category,
				note == null ? null : note.Text, expectedCategory, expectedText))
			{
				throw new InvalidDataException("the inherited map note was not recreated canonically");
			}
		}

		private void BestEffortHideBrokenDiscovery(Zone Zone)
		{
			try
			{
				RemoveLocationFinders(Zone);
			}
			catch (Exception)
			{
			}
			try
			{
				JournalMapNote note = string.IsNullOrEmpty(SecretId)
					? null : JournalAPI.GetMapNote(SecretId);
				if (note != null && note.ZoneID == TargetZoneId)
				{
					JournalAPI.DeleteMapNote(note);
				}
			}
			catch (Exception)
			{
			}
		}

	}
}
