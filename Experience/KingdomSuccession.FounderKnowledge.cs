using System;
using System.Collections.Generic;
using Qud.API;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public sealed partial class KingdomSuccession
	{
		/// <summary>Reveals only notes stamped by one exact founder death, then adds one exact,
		/// deduplicated giver-location note for each still-open quest. Quest state remains entirely
		/// game-scoped and untouched; the corpse contributes memory and navigation only.</summary>
		internal bool TryRestoreFounderKnowledge(string DeathToken, string FounderName,
			out int Revealed, out int QuestMarks)
		{
			Revealed = 0; QuestMarks = 0;
			if (string.IsNullOrEmpty(DeathToken)) return false;
			string attribute = KingdomSuccessionRules.FounderAttribute(DeathToken);
			foreach (IBaseJournalEntry entry in JournalAPI.GetAllNotes())
			{
				if (entry == null || entry.Revealed || entry.Attributes == null
					|| !entry.Attributes.Contains(attribute)) continue;
				entry.Reveal("the remains of " + (string.IsNullOrEmpty(FounderName)
					? "the founder" : FounderName), Silent: true);
				if (entry.Revealed) Revealed++;
			}
			QuestMarks = RestoreQuestOriginMarks(DeathToken, FounderName); return true;
		}

		private static int RestoreQuestOriginMarks(string DeathToken, string FounderName)
		{
			XRLGame game = The.Game;
			if (game == null || game.Quests == null) return 0;
			int marked = 0;
			foreach (KeyValuePair<string, Quest> pair in game.Quests)
			{
				Quest quest = pair.Value;
				if (quest == null || quest.Finished
					|| string.IsNullOrEmpty(quest.QuestGiverLocationZoneID)) continue;
				string questId = string.IsNullOrEmpty(quest.ID) ? pair.Key : quest.ID;
				if (string.IsNullOrEmpty(questId)) questId = quest.Name;
				string secretId = KingdomSuccessionRules.QuestOriginSecretId(DeathToken, questId);
				if (string.IsNullOrEmpty(secretId))
				{
					KingdomLog.Log("succession: an open quest origin exceeded its identity bound");
					continue;
				}
				try
				{
					JournalMapNote note = JournalAPI.GetMapNote(secretId);
					if (note == null)
					{
						JournalAPI.AddMapNote(quest.QuestGiverLocationZoneID,
							KingdomSuccessionRules.QuestMarkNote(quest.Name,
								quest.QuestGiverName), "general", new string[]
							{
								KingdomSuccessionRules.FounderAttribute(DeathToken),
								KingdomSuccessionRules.QuestOriginAttribute
							}, secretId, revealed: true, sold: false, time: -1L, silent: true);
						note = JournalAPI.GetMapNote(secretId);
					}
					if (note == null || !string.Equals(note.ZoneID,
						quest.QuestGiverLocationZoneID, StringComparison.Ordinal)
						|| note.Attributes == null
						|| !note.Attributes.Contains(KingdomSuccessionRules.QuestOriginAttribute))
					{
						KingdomLog.Log("succession: a quest-origin secret identity conflicted; the existing note was left untouched");
						continue;
					}
					if (!note.Revealed) note.Reveal("the remains of "
						+ (string.IsNullOrEmpty(FounderName) ? "the founder" : FounderName),
						Silent: true);
					if (note.Revealed) marked++;
				}
				catch (Exception ex)
				{
					MetricsManager.LogError("ThousandAndFirst: quest-origin map note failed", ex);
					KingdomLog.Log("succession: one quest-origin map note failed ("
						+ ex.GetType().Name + ")");
				}
			}
			return marked;
		}
	}
}
