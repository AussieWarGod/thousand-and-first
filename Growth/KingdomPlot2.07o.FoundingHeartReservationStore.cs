using System;
using System.Collections.Generic;
using XRL;

namespace ThousandAndFirst
{
	public static partial class KingdomPlots
	{
		private sealed class FoundingHeartReservationStore
		{
			internal readonly XRLGame Game;
			private readonly Dictionary<string, string> Strings;
			private readonly Dictionary<string, int> Ints;
			private readonly Dictionary<string, long> Longs;
			private readonly Dictionary<string, object> Objects;
			private readonly Dictionary<string, bool> Booleans;

			internal FoundingHeartReservationStore()
			{
				Game = The.Game;
				Strings = Game?.StringGameState; Ints = Game?.IntGameState;
				Longs = Game?.Int64GameState; Objects = Game?.ObjectGameState;
				Booleans = Game?.BooleanGameState;
			}

			internal bool Current
			{
				get
				{
					return Game != null && ReferenceEquals(The.Game, Game)
						&& ReferenceEquals(Game.StringGameState, Strings) && Ordinal(Strings)
						&& ReferenceEquals(Game.IntGameState, Ints) && Ordinal(Ints)
						&& ReferenceEquals(Game.Int64GameState, Longs) && Ordinal(Longs)
						&& ReferenceEquals(Game.ObjectGameState, Objects) && Ordinal(Objects)
						&& ReferenceEquals(Game.BooleanGameState, Booleans) && Ordinal(Booleans);
				}
			}

			private static bool Ordinal<T>(Dictionary<string, T> Table)
			{
				return Table != null && (ReferenceEquals(Table.Comparer, EqualityComparer<string>.Default)
					|| ReferenceEquals(Table.Comparer, StringComparer.Ordinal));
			}

			internal KingdomDurableKeyObservation Observe(string Key)
			{
				if (!Current || string.IsNullOrEmpty(Key)) return null;
				try
				{
					bool present = Game.HasStringGameState(Key);
					KingdomDurableKeyObservation row = new KingdomDurableKeyObservation {
						HasString = present, String = present ? Game.GetStringGameState(Key, null) : null,
						HasInt = Game.HasIntGameState(Key), HasInt64 = Game.HasInt64GameState(Key),
						HasObject = Game.HasObjectGameState(Key), HasBoolean = Game.HasBooleanGameState(Key) };
					return Current ? row : null;
				}
				catch { return null; }
			}

			internal bool Ensure(string Key, string Expected)
			{
				if (!KingdomFoundingHeartReservationState.TryExpected(Key, Expected, Observe(Key), out bool absent)) return false;
				if (absent)
				{
					if (!Current || !KingdomFoundingHeartReservationState.TryExpected(Key, Expected,
						Observe(Key), out absent) || !absent) return false;
					// Add cannot overwrite an intervening value, unlike the engine's indexer setter.
					try { Strings.Add(Key, Expected); }
					catch { }
				}
				return Current && KingdomFoundingHeartReservationState.TryExpected(Key, Expected,
					Observe(Key), out absent) && !absent;
			}

			internal bool CheckPlan(KingdomFoundingHeartPlan Plan, bool AllowAbsent = false)
			{
				if (!Current || !KingdomFoundingHeartRules.Valid(Plan)) return false;
				string receipt = KingdomFoundingHeartRules.Encode(Plan);
				for (int slot = 0; slot <= KingdomFoundingHeartRules.SlotCount; slot++)
				{
					string role = slot == KingdomFoundingHeartRules.SlotCount ? "final" : "slot-" + slot;
					string id = KingdomFoundingHeartRules.StableId(Plan.TransactionId, Plan.ZoneId, role);
					string key = FoundingHeartReservationPrefix + id;
					if (!KingdomFoundingHeartReservationState.TryExpected(key,
						FoundingHeartReservation(Plan, id, role), Observe(key), out bool absent)
						|| absent && !AllowAbsent) return false;
				}
				return Current && receipt != null && KingdomFoundingHeartRules.Encode(Plan) == receipt;
			}

			internal bool TryAudit(out Dictionary<string, string> Reservations)
			{
				Reservations = null;
				if (!Current) return false;
				try
				{
					HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
					int scanned = 0;
					if (!AddKeys(Strings, keys, ref scanned) || !AddKeys(Ints, keys, ref scanned)
						|| !AddKeys(Longs, keys, ref scanned) || !AddKeys(Objects, keys, ref scanned)
						|| !AddKeys(Booleans, keys, ref scanned) || !Current) return false;
					List<KeyValuePair<string, KingdomDurableKeyObservation>> rows =
						new List<KeyValuePair<string, KingdomDurableKeyObservation>>();
					foreach (string key in keys)
						rows.Add(new KeyValuePair<string, KingdomDurableKeyObservation>(key, Observe(key)));
					if (!KingdomFoundingHeartReservationState.TryAudit(rows, MaximumFoundingHeartCustodyObjects,
						out Dictionary<string, string> exact) || !Current) return false;
					Reservations = exact;
					return true;
				}
				catch { return false; }
			}

			private static bool AddKeys<T>(Dictionary<string, T> Table, HashSet<string> Keys, ref int Scanned)
			{
				if (Table.Count > MaximumFoundingHeartCustodyObjects - Scanned) return false;
				Scanned += Table.Count;
				foreach (string key in Table.Keys)
				{
					if (key == null) return false;
					if (key.StartsWith(FoundingHeartReservationPrefix, StringComparison.Ordinal)) Keys.Add(key);
				}
				return true;
			}

			internal bool Retains(Dictionary<string, string> Before, bool AllowAdditional)
			{
				if (Before == null || !TryAudit(out Dictionary<string, string> current)
					|| !AllowAdditional && current.Count != Before.Count) return false;
				foreach (KeyValuePair<string, string> row in Before)
					if (!current.TryGetValue(row.Key, out string value) || value != row.Value) return false;
				return Current;
			}
		}

		private static bool HasAnyFoundingHeartReservation(string Key)
		{
			return KingdomScenarioStateShape.Classify(new FoundingHeartReservationStore().Observe(Key), out _)
				!= KingdomDurableKeyShape.Absent;
		}
	}
}
