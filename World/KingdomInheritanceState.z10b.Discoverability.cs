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
