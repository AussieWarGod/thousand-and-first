using System;
using System.Collections.Generic;
using HistoryKit;

namespace ThousandAndFirst
{
	public static partial class KingdomFounderHistory
	{
		private const string RealmProperty = "tafRealmId";
		private const string DeathProperty = "tafDeathToken";
		private const string ProofProperty = "tafFounderMemoryProof";

		private static bool TryEnsureEntity(History History,
			KingdomFounderHistoryReceipt Receipt, out HistoricEntity Entity,
			out string Failure)
		{
			Entity = null;
			Failure = "";
			int count = 0;
			for (int i = 0; i < History.entities.Count; i++)
			{
				HistoricEntity candidate = History.entities[i];
				if (candidate != null && string.Equals(candidate.id, Receipt.EntityId,
					StringComparison.Ordinal))
				{
					count++;
					Entity = candidate;
				}
			}
			if (count > 1)
				return Quarantine(Receipt, "duplicate namespaced history entities", out Failure);
			if (Entity == null)
			{
				if (History.EntityByID.TryGetValue(Receipt.EntityId, out HistoricEntity indexed)
					&& indexed != null)
					return Quarantine(Receipt, "history index carries an unlisted entity", out Failure);
				Entity = History.CreateEntity(Receipt.EntityId, Receipt.HistoricYear);
				Receipt.Phase = KingdomFounderHistoryPhase.EntityPublished;
			}
			if (!History.EntityByID.TryGetValue(Receipt.EntityId, out HistoricEntity mapped)
				|| !ReferenceEquals(mapped, Entity))
				return Quarantine(Receipt, "history entity index diverged", out Failure);
			return ValidateEntityShape(Entity, Receipt, allowMissingEvent: true, out Failure)
				|| Quarantine(Receipt, Failure, out Failure);
		}

		private static bool TryEnsureEvent(History History, HistoricEntity Entity,
			KingdomFounderHistoryReceipt Receipt, out string Failure)
		{
			Failure = "";
			HistoricEvent found = null;
			int marked = 0;
			for (int i = 0; i < Entity.events.Count; i++)
			{
				HistoricEvent candidate = Entity.events[i];
				if (candidate != null && string.Equals(candidate.GetEventProperty(
					KingdomFounderHistoryRules.EventMarker), Receipt.ProofId,
					StringComparison.Ordinal))
				{
					marked++;
					found = candidate;
				}
			}
			if (marked > 1)
				return Quarantine(Receipt, "duplicate founder-memory history events", out Failure);
			if (found == null)
			{
				if (!ValidateEntityShape(Entity, Receipt, allowMissingEvent: true, out Failure))
					return Quarantine(Receipt, Failure, out Failure);
				HistoricEvent created = new HistoricEvent
				{
					duration = 0L,
					eventProperties = new Dictionary<string, string>
					{
						{ KingdomFounderHistoryRules.EventMarker, Receipt.ProofId },
						{ "gospel", Receipt.Gospel }
					},
					entityProperties = ExpectedEntityProperties(Receipt)
				};
				Entity.ApplyEvent(created, Receipt.HistoricYear);
				found = created;
			}
			if (!ValidateEvent(History, Entity, found, Receipt, out Failure))
				return Quarantine(Receipt, Failure, out Failure);
			if (Receipt.EventId > 0L && Receipt.EventId != found.id)
				return Quarantine(Receipt, "history event id changed", out Failure);
			Receipt.EventId = found.id;
			if (Receipt.Phase < KingdomFounderHistoryPhase.EventPublished)
				Receipt.Phase = KingdomFounderHistoryPhase.EventPublished;
			return true;
		}

		private static bool ValidateEntityShape(HistoricEntity Entity,
			KingdomFounderHistoryReceipt Receipt, bool allowMissingEvent, out string Failure)
		{
			Failure = "";
			if (Entity == null || Entity.events == null || Entity.events.Count < 1
				|| Entity.events.Count > 2)
			{
				Failure = "history entity carries an unexpected event graph";
				return false;
			}
			int created = 0;
			int marked = 0;
			for (int i = 0; i < Entity.events.Count; i++)
			{
				HistoricEvent item = Entity.events[i];
				if (item is CreatedHistoricEvent && item.year == Receipt.HistoricYear
					&& item.duration == 0L) created++;
				else if (item != null && string.Equals(item.GetEventProperty(
					KingdomFounderHistoryRules.EventMarker), Receipt.ProofId,
					StringComparison.Ordinal)) marked++;
				else
				{
					Failure = "history entity carries an unowned event";
					return false;
				}
			}
			if (created != 1 || marked > 1 || (!allowMissingEvent && marked != 1))
			{
				Failure = "history entity event cardinality diverged";
				return false;
			}
			return true;
		}

		private static bool ValidateEvent(History History, HistoricEntity Entity,
			HistoricEvent Event, KingdomFounderHistoryReceipt Receipt, out string Failure)
		{
			Failure = "";
			Dictionary<string, string> expected = ExpectedEntityProperties(Receipt);
			if (Event == null || Event.id <= 0L || Event.year != Receipt.HistoricYear
				|| Event.duration != 0L || !ReferenceEquals(Event.entity, Entity)
				|| !ReferenceEquals(Event.history, History)
				|| Event.eventProperties == null || Event.eventProperties.Count != 2
				|| Event.GetEventProperty(KingdomFounderHistoryRules.EventMarker) != Receipt.ProofId
				|| Event.GetEventProperty("gospel") != Receipt.Gospel
				|| !ExactProperties(Event.entityProperties, expected)
				|| !ReferenceEquals(History.GetEvent(Event.id), Event))
			{
				Failure = "founder-memory history event diverged";
				return false;
			}
			return ValidateEntityShape(Entity, Receipt, allowMissingEvent: false, out Failure);
		}

		private static Dictionary<string, string> ExpectedEntityProperties(
			KingdomFounderHistoryReceipt Receipt)
		{
			return new Dictionary<string, string>
			{
				{ "type", KingdomFounderHistoryRules.EntityType },
				{ "period", "0" },
				{ "name", KingdomFounderHistoryRules.EntityName(Receipt) },
				{ RealmProperty, Receipt.RealmId },
				{ DeathProperty, Receipt.DeathToken },
				{ ProofProperty, Receipt.ProofId }
			};
		}

		private static bool ExactProperties(Dictionary<string, string> Actual,
			Dictionary<string, string> Expected)
		{
			if (Actual == null || Actual.Count != Expected.Count) return false;
			foreach (KeyValuePair<string, string> pair in Expected)
				if (!Actual.TryGetValue(pair.Key, out string value)
					|| !string.Equals(value, pair.Value, StringComparison.Ordinal)) return false;
			return true;
		}
	}
}
