using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	internal static partial class KingdomSealRules
	{
		/// <summary>
		/// The seat's ground: the zone holding the most of the settlement's works, ties broken by
		/// zone id so the answer never depends on the order a dictionary happened to enumerate in.
		/// <para>
		/// The MVP inherits one seat zone (<c>DECISIONS.md:222-227</c>). Which one is not a
		/// judgement call about importance: it is where the settlement most is.
		/// </para>
		/// </summary>
		public static string ChooseGround(Simulation.City.KingdomCityBook Book, IList<string> ClaimedZones)
		{
			Dictionary<string, int> counts = new Dictionary<string, int>();
			if (Book != null)
			{
				for (int i = 0; i < Book.WorkZoneIds.Count; i++)
				{
					string zone = Book.WorkZoneIds[i];
					if (string.IsNullOrEmpty(zone))
					{
						continue;
					}
					int count;
					counts[zone] = (counts.TryGetValue(zone, out count) ? count : 0) + 1;
				}
			}
			string best = null;
			int bestCount = -1;
			foreach (KeyValuePair<string, int> pair in counts)
			{
				if (pair.Value > bestCount || (pair.Value == bestCount && string.CompareOrdinal(pair.Key, best) < 0))
				{
					best = pair.Key;
					bestCount = pair.Value;
				}
			}
			if (best != null)
			{
				return best;
			}
			// A settlement with no works at all still stood somewhere. The first claimed zone in
			// ordinal order is the honest answer, and an unfounded one has no ground to name.
			string firstClaim = null;
			if (ClaimedZones != null)
			{
				for (int i = 0; i < ClaimedZones.Count; i++)
				{
					string zone = ClaimedZones[i];
					if (!string.IsNullOrEmpty(zone) && (firstClaim == null || string.CompareOrdinal(zone, firstClaim) < 0))
					{
						firstClaim = zone;
					}
				}
			}
			return firstClaim ?? "";
		}

		private static int DefenceOf(Simulation.City.KingdomCityBook Book, string Ground)
		{
			if (Book == null || string.IsNullOrEmpty(Ground))
			{
				return 0;
			}
			for (int i = 0; i < Book.ZoneIds.Count && i < Book.ZoneDefences.Count; i++)
			{
				if (Book.ZoneIds[i] == Ground)
				{
					return Book.ZoneDefences[i];
				}
			}
			return 0;
		}

		private static void CaptureWorks(Simulation.City.KingdomCityBook Book, string Ground, KingdomSealRecord Record)
		{
			if (Book == null || string.IsNullOrEmpty(Ground))
			{
				return;
			}
			int rows = Book.WorkIds.Count;
			for (int i = 0; i < rows && Record.WorkKeys.Count < KingdomSealRecord.MaxWorks; i++)
			{
				if (i >= Book.WorkZoneIds.Count || Book.WorkZoneIds[i] != Ground)
				{
					continue;
				}
				if (i >= Book.WorkDesignKeys.Count || i >= Book.WorkAnchorsX.Count || i >= Book.WorkAnchorsY.Count || i >= Book.WorkConditions.Count)
				{
					continue;
				}
				string design = Book.WorkDesignKeys[i];
				string key;
				if (!KingdomInheritRules.TrySemanticKeyForBlueprint(design, out key))
				{
					// Compatibility for early/dev books which wrote the semantic key itself.
					// A blueprint-shaped or malformed unknown is dropped fail-closed.
					key = SanitizeToken(design, KingdomSealRecord.MaxIdChars);
					if (!KingdomInheritRules.IsStableSemanticKey(key))
					{
						continue;
					}
				}
				if (key.Length == 0)
				{
					continue;
				}
				int x = Book.WorkAnchorsX[i];
				int y = Book.WorkAnchorsY[i];
				// Out-of-zone coordinates are dropped rather than clamped. A clamped anchor would
				// pile works on an edge cell in the next world; a dropped one is one work the ruin
				// does not have, which is a smaller lie.
				if (x < 0 || x > 255 || y < 0 || y > 255)
				{
					continue;
				}
				Record.WorkKeys.Add(key);
				Record.WorkX.Add(x);
				Record.WorkY.Add(y);
				Record.WorkConditions.Add(Clamp(Book.WorkConditions[i], 0, 100));
			}
		}

		private static void CaptureRoll(KingdomSettlement Seat, KingdomSealRecord Record)
		{
			Simulation.City.KingdomCityState state;
			Simulation.City.KingdomCityFault fault;
			Simulation.City.KingdomResidentRollProjection roll;
			if (Seat?.City == null || !Seat.City.TryRead(out state, out fault)
				|| !Simulation.City.KingdomResidentRules.TryProject(state, out roll)) return;
			int rows = roll.Names.Count;
			for (int i = 0; i < rows && Record.RollNames.Count < KingdomSealRecord.MaxRoll; i++)
			{
				string name = SanitizeText(roll.Names[i], KingdomSealRecord.MaxNameChars);
				if (name.Length == 0)
				{
					continue;
				}
				Record.RollNames.Add(name);
				Record.RollOrigins.Add(SanitizeText((i < roll.Origins.Count) ? roll.Origins[i] : "", KingdomSealRecord.MaxNameChars));
				Record.RollArrived.Add(SanitizeText((i < roll.Arrived.Count) ? roll.Arrived[i] : "", KingdomSealRecord.MaxNameChars));
			}
		}

		private static void CaptureDead(KingdomSettlement Seat, KingdomSealRecord Record)
		{
			int rows = Seat.DeadNames.Count;
			for (int i = 0; i < rows && Record.DeadNames.Count < KingdomSealRecord.MaxDead; i++)
			{
				string name = SanitizeText(Seat.DeadNames[i], KingdomSealRecord.MaxNameChars);
				if (name.Length == 0)
				{
					continue;
				}
				Record.DeadNames.Add(name);
				Record.DeadCauses.Add(SanitizeText((i < Seat.DeadCauses.Count) ? Seat.DeadCauses[i] : "", KingdomSealRecord.MaxLineChars));
			}
		}

		/// <summary>
		/// Tallies as a seal keeps them: sorted by key so the file is canonical, since a
		/// dictionary's enumeration order is not a fact about a settlement.
		/// </summary>
		private static void CaptureTallies(Dictionary<string, int> Source, int MaxRows, List<string> Keys, List<int> Counts)
		{
			if (Source == null)
			{
				return;
			}
			Dictionary<string, int> folded = new Dictionary<string, int>();
			foreach (KeyValuePair<string, int> pair in Source)
			{
				if (pair.Value <= 0)
				{
					continue;
				}
				string key = SanitizeToken(pair.Key, KingdomSealRecord.MaxIdChars);
				if (key.Length == 0)
				{
					continue;
				}
				// Two source keys can sanitize to one token, and the tally they share is their sum
				// rather than whichever the enumeration reached last.
				int running;
				folded[key] = (folded.TryGetValue(key, out running) ? running : 0) + pair.Value;
			}
			List<string> ordered = new List<string>(folded.Keys);
			ordered.Sort(StringComparer.Ordinal);
			for (int i = 0; i < ordered.Count && Keys.Count < MaxRows; i++)
			{
				Keys.Add(ordered[i]);
				Counts.Add(Clamp(folded[ordered[i]], 0, 100000));
			}
		}

		private static ulong Fold(ulong Hash, string Value)
		{
			string value = Value ?? "";
			for (int i = 0; i < value.Length; i++)
			{
				Hash = FoldByte(Hash, (byte)(value[i] & 0xFF));
				Hash = FoldByte(Hash, (byte)(value[i] >> 8));
			}
			return Hash;
		}

		private static ulong FoldInt(ulong Hash, int Value)
		{
			uint value = unchecked((uint)Value);
			Hash = FoldByte(Hash, (byte)(value >> 24));
			Hash = FoldByte(Hash, (byte)(value >> 16));
			Hash = FoldByte(Hash, (byte)(value >> 8));
			return FoldByte(Hash, (byte)value);
		}

		private static ulong FoldByte(ulong Hash, byte Value)
		{
			Hash ^= Value;
			return unchecked(Hash * 1099511628211UL);
		}

		private static int Clamp(int Value, int Low, int High)
		{
			return (Value < Low) ? Low : ((Value > High) ? High : Value);
		}

		private static long ClampLong(long Value, long Low, long High)
		{
			return (Value < Low) ? Low : ((Value > High) ? High : Value);
		}
	}
}
