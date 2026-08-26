using System;
using System.Collections.Generic;

using Qud.API;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	public static partial class KingdomExpeditions
	{
		private static List<ResidentChoice> EligibleResidents(KingdomSystem System,
			KingdomCityState State, KingdomJobTable Jobs, string Here)
		{
			List<ResidentChoice> choices = new List<ResidentChoice>();
			for (int i = 0; i < State.ResidentCount; i++)
			{
				KingdomResidentRow row;
				string zoneId;
				if (!State.TryResident(i, out row) || row.Standing != KingdomResidentStanding.Resident
					|| row.ResidentId <= 0 || string.IsNullOrEmpty(row.Name)
					|| HasExpedition(Jobs, row.ResidentId)
					|| !KingdomResidents.TryBoundZone(System, row.ResidentId,
						KingdomBindingKind.Resident, out zoneId)
					|| !string.Equals(zoneId, Here, StringComparison.Ordinal)) continue;
				KingdomBodyPresence presence = KingdomResidents.PresenceOfKey(System, row.ResidentId,
					KingdomBindingKind.Resident, zoneId);
				if (presence == KingdomBodyPresence.None) continue;
				choices.Add(new ResidentChoice { Row = row, ZoneId = zoneId });
			}
			choices.Sort(delegate(ResidentChoice a, ResidentChoice b)
			{
				int byName = string.Compare(a.Row.Name, b.Row.Name, StringComparison.Ordinal);
				return (byName != 0) ? byName : a.Row.ResidentId.CompareTo(b.Row.ResidentId);
			});
			return choices;
		}

		private static List<TargetChoice> VisitedTargets(KingdomSystem System, string SourceZoneId,
			long StartTick)
		{
			List<TargetChoice> choices = new List<TargetChoice>();
			HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; JournalAPI.MapNotes != null && i < JournalAPI.MapNotes.Count; i++)
			{
				JournalMapNote note = JournalAPI.MapNotes[i];
				string zoneId = (note == null) ? null : note.ZoneID;
				KingdomExpeditionQuote quote;
				if (note == null || !note.Revealed || !note.Visited || note.LastVisit < 0L
					|| string.IsNullOrEmpty(zoneId) || !seen.Add(zoneId)
					|| RealmHolds(System, zoneId)
					|| !KingdomExpeditionRules.TryQuote(SourceZoneId, zoneId, StartTick, out quote)) continue;
				choices.Add(new TargetChoice
				{
					Note = note,
					ZoneId = zoneId,
					Name = SafeName(ConsoleLib.Console.ColorUtility.StripFormatting(note.Text), zoneId),
					Quote = quote
				});
			}
			choices.Sort(delegate(TargetChoice a, TargetChoice b)
			{
				int byName = string.Compare(a.Name, b.Name, StringComparison.Ordinal);
				return (byName != 0) ? byName : string.CompareOrdinal(a.ZoneId, b.ZoneId);
			});
			return choices;
		}

		private static List<KingdomJobRow> ExpeditionRows(KingdomJobTable Table)
		{
			List<KingdomJobRow> rows = new List<KingdomJobRow>();
			for (int i = 0; i < Table.Count; i++)
			{
				KingdomJobRow row;
				if (Table.TryAt(i, out row) && row.Kind == KingdomJobKind.Expedition) rows.Add(row);
			}
			rows.Sort((a, b) => a.JobId.CompareTo(b.JobId));
			return rows;
		}

		private static bool HasExpedition(KingdomJobTable Table, int ResidentId)
		{
			if (Table == null || ResidentId <= 0) return false;
			for (int i = 0; i < Table.Count; i++)
			{
				KingdomJobRow row;
				if (Table.TryAt(i, out row) && row.Kind == KingdomJobKind.Expedition
					&& row.SubjectId == ResidentId) return true;
			}
			return false;
		}

		private static bool LedgerHasRoom(KingdomSystem System, string ZoneId,
			KingdomJobTable Jobs)
		{
			KingdomLedger ledger = LedgerFor(System, ZoneId);
			if (ledger == null) return false;
			ledger.Normalize();
			int promised = 0;
			for (int i = 0; Jobs != null && i < Jobs.Count; i++)
			{
				KingdomJobRow row;
				if (Jobs.TryAt(i, out row) && row.Kind == KingdomJobKind.Expedition
					&& LedgerFor(System, row.SourceZoneId) == ledger) promised++;
			}
			return ledger.ExpeditionLines.Count + promised < KingdomJobRules.MaxOpenJobs;
		}

		private static KingdomLedger LedgerFor(KingdomSystem System, string ZoneId)
		{
			if (System == null || string.IsNullOrEmpty(ZoneId)) return null;
			if (System.ClaimedZones != null && System.ClaimedZones.Contains(ZoneId))
				return System.Ledger;
			if (System.Away != null && System.Away.ClaimedZones != null
				&& System.Away.ClaimedZones.Contains(ZoneId)) return System.Away.Ledger;
			return null;
		}

		private static bool RealmHolds(KingdomSystem System, string ZoneId)
		{
			return System != null && ((System.ClaimedZones != null
				&& System.ClaimedZones.Contains(ZoneId)) || (System.Away != null
				&& System.Away.ClaimedZones != null && System.Away.ClaimedZones.Contains(ZoneId)));
		}

	}
}
