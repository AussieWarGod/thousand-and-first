using System;
using System.Collections.Generic;
using HistoryKit;
using XRL;

namespace ThousandAndFirst
{
	public static partial class KingdomFounderHistory
	{
		private const string RealmProperty = "tafRealmId";
		private const string DeathProperty = "tafDeathToken";
		private const string ProofProperty = "tafFounderMemoryProof";

		/// <summary>
		/// Read-only preflight for a schema-1 HistoryKit insertion. Cleanup is allowed only when
		/// every list, index, back-reference, id, marker, and payload proves the exact TAF object.
		/// History's private event allocator is deliberately not rewound; retired ids remain gaps.
		/// </summary>
		private static bool TryInspectLegacyHistory(KingdomFounderHistoryReceipt Receipt,
			out LegacyHistoryCleanupPlan Plan, out string Failure)
		{
			Plan = null;
			Failure = "";
			History history = The.Game?.sultanHistory;
			if (history == null || history.entities == null || history.events == null
				|| history.EntityByID == null)
			{
				Failure = "Qud history is not loaded";
				return false;
			}

			HistoricEntity entity = null;
			int entityIndex = -1;
			int entityMatches = 0;
			for (int i = 0; i < history.entities.Count; i++)
			{
				HistoricEntity candidate = history.entities[i];
				if (candidate != null && string.Equals(candidate.id, Receipt.EntityId,
					StringComparison.Ordinal))
				{
					entityMatches++;
					entity = candidate;
					entityIndex = i;
				}
			}
			if (entityMatches > 1)
				return Quarantine(Receipt, "duplicate schema-1 history entities", out Failure);

			bool indexKey = history.EntityByID.TryGetValue(Receipt.EntityId,
				out HistoricEntity mapped);
			bool indexed = indexKey && mapped != null;
			if (entity == null)
			{
				if (indexKey)
					return Quarantine(Receipt,
						"schema-1 history index carries an unlisted entity", out Failure);
				if (LegacyHistoryEventsRemain(history, Receipt))
					return Quarantine(Receipt,
						"schema-1 history events remain without their owned entity", out Failure);
				Plan = new LegacyHistoryCleanupPlan(history);
				return true;
			}
			if (!indexed || !ReferenceEquals(mapped, entity))
				return Quarantine(Receipt, "schema-1 history entity index diverged", out Failure);
			if (!ReferenceEquals(entity._history, history) || entity.events == null
				|| entity.events.Count < 1 || entity.events.Count > 2)
				return Quarantine(Receipt, "schema-1 history entity graph diverged", out Failure);

			HistoricEvent created = null;
			HistoricEvent marked = null;
			for (int i = 0; i < entity.events.Count; i++)
			{
				HistoricEvent candidate = entity.events[i];
				if (candidate != null && candidate.GetType() == typeof(CreatedHistoricEvent))
				{
					if (created != null)
						return Quarantine(Receipt,
							"schema-1 history has duplicate creation events", out Failure);
					created = candidate;
				}
				else if (candidate != null && string.Equals(candidate.GetEventProperty(
					KingdomFounderHistoryRules.EventMarker), Receipt.ProofId,
					StringComparison.Ordinal))
				{
					if (marked != null)
						return Quarantine(Receipt,
							"schema-1 history has duplicate marked events", out Failure);
					marked = candidate;
				}
				else return Quarantine(Receipt,
					"schema-1 history entity carries an unowned event", out Failure);
			}
			if (!CreatedEventExact(history, entity, created, Receipt))
				return Quarantine(Receipt, "schema-1 creation event diverged", out Failure);
			if (marked != null && !MarkedEventExact(history, entity, marked, Receipt))
				return Quarantine(Receipt, "schema-1 founder event diverged", out Failure);
			if (!LegacyPhaseAllowsEvent(Receipt, marked != null))
				return Quarantine(Receipt,
					"schema-1 history event disagrees with its frozen phase", out Failure);

			List<HistoricEvent> ownedEvents = new List<HistoricEvent> { created };
			if (marked != null) ownedEvents.Add(marked);
			List<int> historyEventIndices = new List<int>();
			for (int i = 0; i < ownedEvents.Count; i++)
			{
				HistoricEvent owned = ownedEvents[i];
				int references = 0;
				int idMatches = 0;
				int at = -1;
				for (int j = 0; j < history.events.Count; j++)
				{
					HistoricEvent candidate = history.events[j];
					if (ReferenceEquals(candidate, owned)) { references++; at = j; }
					if (candidate != null && candidate.id == owned.id) idMatches++;
				}
				if (references != 1 || idMatches != 1 || at < 0)
					return Quarantine(Receipt,
						"schema-1 history event index diverged", out Failure);
				historyEventIndices.Add(at);
			}
			if (OtherEntityOwnsLegacyEvent(history, entity, ownedEvents, Receipt))
				return Quarantine(Receipt,
					"schema-1 history event is shared or duplicated", out Failure);

			Plan = new LegacyHistoryCleanupPlan(history, entity, entityIndex,
				ownedEvents, historyEventIndices);
			return true;
		}

		private static bool CreatedEventExact(History History, HistoricEntity Entity,
			HistoricEvent Event, KingdomFounderHistoryReceipt Receipt)
		{
			return Event != null && Event.id > 0L && Event.year == Receipt.HistoricYear
				&& Event.duration == 0L && ReferenceEquals(Event.entity, Entity)
				&& ReferenceEquals(Event.history, History)
				&& Empty(Event.eventProperties) && Empty(Event.entityProperties)
				&& Empty(Event.addedListProperties) && Empty(Event.removedListProperties)
				&& Empty(Event.perspectives);
		}

		private static bool MarkedEventExact(History History, HistoricEntity Entity,
			HistoricEvent Event, KingdomFounderHistoryReceipt Receipt)
		{
			Dictionary<string, string> expected = ExpectedLegacyEntityProperties(Receipt);
			return Event.GetType() == typeof(HistoricEvent) && Event.id > 0L
				&& Event.year == Receipt.HistoricYear && Event.duration == 0L
				&& ReferenceEquals(Event.entity, Entity) && ReferenceEquals(Event.history, History)
				&& Event.eventProperties != null && Event.eventProperties.Count == 2
				&& Event.GetEventProperty(KingdomFounderHistoryRules.EventMarker) == Receipt.ProofId
				&& Event.GetEventProperty("gospel") == Receipt.Gospel
				&& ExactProperties(Event.entityProperties, expected)
				&& Empty(Event.addedListProperties) && Empty(Event.removedListProperties)
				&& Empty(Event.perspectives)
				&& (Receipt.EventId == 0L || Receipt.EventId == Event.id);
		}

		private static bool LegacyPhaseAllowsEvent(KingdomFounderHistoryReceipt Receipt,
			bool HasMarkedEvent)
		{
			if (Receipt.LegacyPhase == KingdomFounderHistoryPhase.EntityPublished)
				return !HasMarkedEvent && Receipt.EventId == 0L;
			if (Receipt.LegacyPhase == KingdomFounderHistoryPhase.EventPublished
				|| Receipt.LegacyPhase == KingdomFounderHistoryPhase.NotePublished
				|| Receipt.LegacyPhase == KingdomFounderHistoryPhase.Committed)
				return HasMarkedEvent && Receipt.EventId > 0L;
			return Receipt.LegacyPhase == KingdomFounderHistoryPhase.Quarantined;
		}

		private static bool LegacyHistoryEventsRemain(History History,
			KingdomFounderHistoryReceipt Receipt)
		{
			for (int i = 0; i < History.events.Count; i++)
			{
				HistoricEvent candidate = History.events[i];
				if (candidate == null) continue;
				if (string.Equals(candidate.entity?.id, Receipt.EntityId,
					StringComparison.Ordinal)) return true;
				if (Receipt.EventId > 0L && candidate.id == Receipt.EventId) return true;
				if (string.Equals(candidate.GetEventProperty(
					KingdomFounderHistoryRules.EventMarker), Receipt.ProofId,
					StringComparison.Ordinal)) return true;
			}
			return false;
		}

		private static bool OtherEntityOwnsLegacyEvent(History History,
			HistoricEntity Owner, List<HistoricEvent> OwnedEvents,
			KingdomFounderHistoryReceipt Receipt)
		{
			for (int i = 0; i < History.entities.Count; i++)
			{
				HistoricEntity candidate = History.entities[i];
				if (candidate == null || ReferenceEquals(candidate, Owner)
					|| candidate.events == null) continue;
				for (int j = 0; j < candidate.events.Count; j++)
				{
					HistoricEvent item = candidate.events[j];
					if (item == null) continue;
					for (int k = 0; k < OwnedEvents.Count; k++)
						if (ReferenceEquals(item, OwnedEvents[k]) || item.id == OwnedEvents[k].id)
							return true;
					if (string.Equals(item.GetEventProperty(
						KingdomFounderHistoryRules.EventMarker), Receipt.ProofId,
						StringComparison.Ordinal)) return true;
				}
			}
			for (int i = 0; i < History.events.Count; i++)
			{
				HistoricEvent item = History.events[i];
				if (item == null) continue;
				bool owned = false;
				for (int j = 0; j < OwnedEvents.Count; j++)
					if (ReferenceEquals(item, OwnedEvents[j])) { owned = true; break; }
				if (!owned && (string.Equals(item.entity?.id, Receipt.EntityId,
					StringComparison.Ordinal) || string.Equals(item.GetEventProperty(
						KingdomFounderHistoryRules.EventMarker), Receipt.ProofId,
						StringComparison.Ordinal))) return true;
			}
			return false;
		}

		private static Dictionary<string, string> ExpectedLegacyEntityProperties(
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

		private static bool Empty<TKey, TValue>(Dictionary<TKey, TValue> Value)
		{
			return Value == null || Value.Count == 0;
		}

	}
}
