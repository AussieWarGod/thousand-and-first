using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomRaids
	{
		internal static bool TryInspectRecoveryQuests(KingdomSystem System,
			out List<string> Keys, out string Failure)
		{
			Keys = new List<string>(); Failure = null;
			if (The.Game == null || System == null)
				return Fail("quest teardown has no live authority", out Failure);
			List<KingdomRaidIncident> incidents = new List<KingdomRaidIncident>();
			if (!Collect(System.LifecycleBook, incidents, out Failure)) return false;
			List<KingdomSettlement> others = System.NonSeatSettlements();
			for (int i = 0; i < others.Count; i++)
				if (!Collect(others[i]?.LifecycleBook, incidents, out Failure)) return false;
			if (System.Seceded != null
				&& !Collect(System.Seceded.LifecycleBook, incidents, out Failure)) return false;
			foreach (KeyValuePair<string, Quest> row in The.Game.Quests)
			{
				Quest quest = row.Value;
				if (!IsOwnedRecoveryQuest(quest)) continue;
				KingdomRaidIncident incident = FindIncident(incidents, quest);
				if (incident == null || !RecoveryQuestShape(quest, incident, false)
					|| row.Key != quest.ID)
					return Fail("a TAF raid-recovery quest diverged from its exact base shape",
						out Failure);
				Keys.Add(row.Key);
			}
			Keys.Sort(StringComparer.Ordinal);
			return true;
		}

		/// <summary>Directly retires exact active base quests without completion or rewards.</summary>
		internal static bool TryRetireRecoveryQuests(KingdomSystem System,
			out int Removed, out string Failure)
		{
			Removed = 0;
			if (!TryInspectRecoveryQuests(System, out List<string> keys, out Failure)) return false;
			for (int i = 0; i < keys.Count; i++)
				if (!The.Game.Quests.Remove(keys[i]))
					return Fail("an exact raid-recovery quest changed before retirement",
						out Failure);
			Removed = keys.Count;
			return true;
		}

		private static bool Collect(KingdomLifecycleBook Book,
			List<KingdomRaidIncident> Incidents, out string Failure)
		{
			Failure = null;
			KingdomRaidLedger ledger = Book?.RaidLedger;
			if (ledger == null || ledger.Version != KingdomRaidLedger.CurrentVersion
				|| ledger.OpaqueFuturePayload != null || ledger.Incidents == null)
				return Fail("raid ledger cannot authenticate quest projections", out Failure);
			for (int i = 0; i < ledger.Incidents.Count; i++)
				if (ledger.Incidents[i] != null) Incidents.Add(ledger.Incidents[i]);
			return true;
		}

		private static KingdomRaidIncident FindIncident(IList<KingdomRaidIncident> Incidents,
			Quest Quest)
		{
			KingdomRaidIncident found = null;
			for (int i = 0; i < Incidents.Count; i++)
			{
				KingdomRaidIncident row = Incidents[i];
				if (row.RecoveryQuestId != Quest.ID || row.Id != Quest.GetProperty("IncidentId"))
					continue;
				if (found != null) return null;
				found = row;
			}
			return found;
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
