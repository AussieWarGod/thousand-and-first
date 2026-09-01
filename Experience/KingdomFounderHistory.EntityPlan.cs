using System;
using System.Collections.Generic;
using HistoryKit;

namespace ThousandAndFirst
{
	public static partial class KingdomFounderHistory
	{
		/// <summary>Rollback-capable mutation plan built only after exact schema-1 proof.</summary>
		private sealed class LegacyHistoryCleanupPlan
		{
			private readonly History History;
			private readonly HistoricEntity Entity;
			private readonly int EntityIndex;
			private readonly List<HistoricEvent> Events;
			private readonly List<int> EventIndices;
			private bool Applied;

			internal LegacyHistoryCleanupPlan(History History)
				: this(History, null, -1, new List<HistoricEvent>(), new List<int>()) { }

			internal LegacyHistoryCleanupPlan(History History, HistoricEntity Entity,
				int EntityIndex, List<HistoricEvent> Events, List<int> EventIndices)
			{
				this.History = History;
				this.Entity = Entity;
				this.EntityIndex = EntityIndex;
				this.Events = Events;
				this.EventIndices = EventIndices;
			}

			internal bool Apply(out string Failure)
			{
				Failure = "";
				if (Entity == null) { Applied = true; return true; }
				Applied = true;
				try
				{
					List<int> descending = new List<int>(EventIndices);
					descending.Sort();
					descending.Reverse();
					for (int i = 0; i < descending.Count; i++)
						History.events.RemoveAt(descending[i]);
					History.entities.RemoveAt(EntityIndex);
					if (!History.EntityByID.Remove(Entity.id))
						throw new InvalidOperationException("history index removal refused");
					return true;
				}
				catch (Exception ex)
				{
					Rollback();
					Failure = "schema-1 history cleanup threw " + ex.GetType().Name;
					return false;
				}
			}

			internal bool Absent()
			{
				if (Entity == null) return true;
				if (History.entities.Contains(Entity)
					|| History.EntityByID.ContainsKey(Entity.id)) return false;
				for (int i = 0; i < Events.Count; i++)
					if (History.events.Contains(Events[i])) return false;
				return true;
			}

			internal void Rollback()
			{
				if (!Applied || Entity == null) return;
				if (!History.entities.Contains(Entity))
					History.entities.Insert(EntityIndex, Entity);
				History.EntityByID[Entity.id] = Entity;
				List<int> ascending = new List<int>(EventIndices);
				ascending.Sort();
				for (int i = 0; i < ascending.Count; i++)
				{
					int original = ascending[i];
					HistoricEvent owned = null;
					for (int j = 0; j < EventIndices.Count; j++)
						if (EventIndices[j] == original) { owned = Events[j]; break; }
					if (owned != null && !History.events.Contains(owned))
						History.events.Insert(original, owned);
				}
				Applied = false;
			}
		}
	}
}
