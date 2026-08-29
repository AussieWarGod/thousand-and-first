using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst.Harness
{
	/// <summary>One candidate building, reduced to what selection is allowed to depend on.</summary>
	internal sealed class KingdomScenarioOwnerRow
	{
		internal int X;
		internal int Y;

		/// <summary>The engine's stable object identity. Never blank on a candidate.</summary>
		internal string Id;
	}

	/// <summary>
	/// Deterministic attended selection among buildings that all match the frozen case.
	/// <para>
	/// Engine-free so the ordering and every ambiguity case execute without a live zone. A real
	/// settlement holds several buildings, so "exactly one stamped owner in the zone" is not a
	/// workflow; but silently taking the first would make the curated anchor depend on enumeration
	/// order, which is the one thing a differential may never depend on. Ambiguity therefore refuses
	/// and prints a stable list the reviewer selects from by coordinate or by object id.
	/// </para>
	/// </summary>
	internal static class KingdomScenarioSelectorRules
	{
		internal const string CoordinatePrefix = "at=";
		internal const string IdentityPrefix = "id=";
		private const int MaxIdChars = 96;

		/// <summary>
		/// Total ordering by lot position, then by stable identity. Two buildings never share a
		/// cell, so the identity tiebreak only ever settles malformed input.
		/// </summary>
		internal static void Sort(List<KingdomScenarioOwnerRow> Rows)
		{
			if (Rows == null) return;
			Rows.Sort(delegate (KingdomScenarioOwnerRow a, KingdomScenarioOwnerRow b)
			{
				if (a.Y != b.Y) return a.Y < b.Y ? -1 : 1;
				if (a.X != b.X) return a.X < b.X ? -1 : 1;
				return string.CompareOrdinal(a.Id ?? "", b.Id ?? "");
			});
		}

		/// <summary>Parses the optional selector. An empty selector is lawful and selects nothing.</summary>
		internal static bool TryParse(string Raw, out bool HasCoordinate, out int X, out int Y,
			out string Id, out string Failure)
		{
			HasCoordinate = false;
			X = 0;
			Y = 0;
			Id = null;
			Failure = null;
			string raw = (Raw ?? "").Trim();
			if (raw.Length == 0) return true;
			if (raw.StartsWith(IdentityPrefix, StringComparison.Ordinal))
			{
				string id = raw.Substring(IdentityPrefix.Length);
				if (id.Length == 0 || id.Length > MaxIdChars || !Printable(id))
					return Refuse("the id selector is malformed", out Failure);
				Id = id;
				return true;
			}
			if (!raw.StartsWith(CoordinatePrefix, StringComparison.Ordinal))
				return Refuse("a selector must be 'at=<x>,<y>' or 'id=<object-id>'", out Failure);
			string[] parts = raw.Substring(CoordinatePrefix.Length).Split(',');
			if (parts.Length != 2 || !TryCoordinate(parts[0], out X) || !TryCoordinate(parts[1], out Y))
				return Refuse("the coordinate selector must be 'at=<x>,<y>' with whole numbers",
					out Failure);
			HasCoordinate = true;
			return true;
		}

		/// <summary>
		/// The index of the one row the selector names, or -1 with a refusal. An absent selector
		/// resolves only when exactly one candidate exists: never the first of several.
		/// </summary>
		internal static int Resolve(IList<KingdomScenarioOwnerRow> Rows, bool HasCoordinate,
			int X, int Y, string Id, out string Failure)
		{
			Failure = null;
			if (Rows == null || Rows.Count == 0)
				return Refused("no building in this zone matches the frozen case", out Failure);
			if (!HasCoordinate && Id == null)
			{
				if (Rows.Count == 1) return 0;
				Failure = "this zone holds " + Rows.Count.ToString(CultureInfo.InvariantCulture)
					+ " buildings matching the frozen case. Name one:\n" + Describe(Rows);
				return -1;
			}
			int found = -1;
			for (int i = 0; i < Rows.Count; i++)
			{
				KingdomScenarioOwnerRow row = Rows[i];
				bool hit = HasCoordinate
					? row.X == X && row.Y == Y
					: string.Equals(row.Id, Id, StringComparison.Ordinal);
				if (!hit) continue;
				if (found >= 0)
					return Refused("the selector names more than one building; use id=", out Failure);
				found = i;
			}
			if (found < 0)
				return Refused("the selector names no building matching the frozen case",
					out Failure);
			return found;
		}

		/// <summary>The stable candidate list an operator selects from.</summary>
		internal static string Describe(IList<KingdomScenarioOwnerRow> Rows)
		{
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < Rows.Count; i++)
				sb.Append(i == 0 ? "" : "\n").Append("  at=")
					.Append(Rows[i].X.ToString(CultureInfo.InvariantCulture)).Append(",")
					.Append(Rows[i].Y.ToString(CultureInfo.InvariantCulture))
					.Append("  id=").Append(Rows[i].Id ?? "(none)");
			return sb.ToString();
		}

		private static bool TryCoordinate(string Value, out int Result)
		{
			return int.TryParse(Value, NumberStyles.None, CultureInfo.InvariantCulture, out Result);
		}

		private static bool Printable(string Value)
		{
			for (int i = 0; i < Value.Length; i++)
				if (Value[i] < ' ' || Value[i] > '~') return false;
			return true;
		}

		private static bool Refuse(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}

		private static int Refused(string Message, out string Failure)
		{
			Failure = Message;
			return -1;
		}
	}
}
