using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Engine-free custody census and identity helpers for
	/// <see cref="KingdomCampHeartNativeChecks"/>. No <c>XRL</c> type appears here, so every
	/// predicate the camp-store evidence rests on runs under both public test projects against
	/// plain records instead of a GameObject.
	/// <para>
	/// RAW means raw, and CUSTODY means custody. A unit is its exact identity, its blueprint, the
	/// exact thing holding it, and the raw physical count read off its own stack - never an
	/// eventful count, never a material tally, and never a bare "how many of these are there".
	/// </para>
	/// </summary>
	internal static class KingdomCampHeartNativeCensus
	{
		internal const string FaultMissing = "taf-camp-store-unit-missing";
		internal const string FaultBlueprint = "taf-camp-store-unit-blueprint-changed";
		internal const string FaultHolder = "taf-camp-store-unit-holder-changed";
		internal const string FaultCount = "taf-camp-store-unit-count-changed";
		internal const string FaultDuplicate = "taf-camp-store-duplicate-identity";
		internal const string FaultPresent = "taf-camp-store-unit-still-present";
		internal const string FaultBillShort = "taf-camp-bill-short";
		internal const string FaultBillOver = "taf-camp-bill-over";
		internal const string FaultBillExtra = "taf-camp-bill-extra";

		/// <summary>One physical unit as it was actually found: exact identity, blueprint, the
		/// exact holder it was found in, and its raw stack count.</summary>
		internal sealed class Unit
		{
			internal readonly string Id;
			internal readonly string Blueprint;
			internal readonly string Holder;
			internal readonly int RawCount;

			internal Unit(string Id, string Blueprint, string Holder, int RawCount)
			{
				this.Id = Id;
				this.Blueprint = Blueprint;
				this.Holder = Holder;
				this.RawCount = RawCount;
			}

			internal string Describe()
			{
				return (Id ?? "(null)") + "[" + (Blueprint ?? "(null)") + "]@"
					+ (Holder ?? "(null)") + "x"
					+ RawCount.ToString(CultureInfo.InvariantCulture);
			}
		}

		/// <summary>Raw units per blueprint, in a fixed ordinal order so two runs over the same
		/// store produce the same line. The number is summed RAW COUNT, not row count.</summary>
		internal static string Describe(IReadOnlyList<Unit> Units)
		{
			if (Units == null || Units.Count == 0) return "empty";
			SortedDictionary<string, int> counts =
				new SortedDictionary<string, int>(StringComparer.Ordinal);
			for (int i = 0; i < Units.Count; i++)
			{
				Unit unit = Units[i];
				string key = unit == null ? "(null)" : (unit.Blueprint ?? "(null)");
				int raw = unit == null ? 0 : unit.RawCount;
				counts[key] = counts.TryGetValue(key, out int held) ? held + raw : raw;
			}
			StringBuilder line = new StringBuilder();
			foreach (KeyValuePair<string, int> row in counts)
			{
				if (line.Length > 0) line.Append(',');
				line.Append(row.Key).Append('=')
					.Append(row.Value.ToString(CultureInfo.InvariantCulture));
			}
			return line.ToString();
		}

		/// <summary>Every way a retained unit can have stopped being the same unit, named with a
		/// <c>taf-</c> reason each. An empty result is the only proof that a minted unit survived
		/// a transition: a matching count says nothing about which objects it counted, and an
		/// identity that survived in the wrong holder or at a different raw count did not
		/// survive.</summary>
		internal static List<string> Faults(IReadOnlyList<Unit> Wanted, IReadOnlyList<Unit> Present)
		{
			List<string> faults = new List<string>();
			if (Wanted == null) return faults;
			Dictionary<string, Unit> held = new Dictionary<string, Unit>(StringComparer.Ordinal);
			if (Present != null)
				for (int i = 0; i < Present.Count; i++)
				{
					Unit unit = Present[i];
					if (unit == null || unit.Id == null) continue;
					if (held.ContainsKey(unit.Id))
					{
						faults.Add(FaultDuplicate + ":" + unit.Id);
						continue;
					}
					held[unit.Id] = unit;
				}
			for (int i = 0; i < Wanted.Count; i++)
			{
				Unit want = Wanted[i];
				if (want == null || want.Id == null)
				{
					faults.Add(FaultMissing + ":(null)");
					continue;
				}
				Unit found;
				if (!held.TryGetValue(want.Id, out found))
				{
					faults.Add(FaultMissing + ":" + want.Id);
					continue;
				}
				if (!string.Equals(found.Blueprint, want.Blueprint, StringComparison.Ordinal))
					faults.Add(FaultBlueprint + ":" + want.Id + ":" + (want.Blueprint ?? "(null)")
						+ "->" + (found.Blueprint ?? "(null)"));
				if (!string.Equals(found.Holder, want.Holder, StringComparison.Ordinal))
					faults.Add(FaultHolder + ":" + want.Id + ":" + (want.Holder ?? "(null)")
						+ "->" + (found.Holder ?? "(null)"));
				if (found.RawCount != want.RawCount)
					faults.Add(FaultCount + ":" + want.Id + ":"
						+ want.RawCount.ToString(CultureInfo.InvariantCulture) + "->"
						+ found.RawCount.ToString(CultureInfo.InvariantCulture));
			}
			return faults;
		}

		/// <summary>The other way round: a unit the production bill is recorded as having spent
		/// must be gone from the store, so a non-empty answer refuses.</summary>
		internal static List<string> Surviving(IReadOnlyList<string> Wanted,
			IReadOnlyList<Unit> Present)
		{
			List<string> surviving = new List<string>();
			if (Wanted == null || Present == null) return surviving;
			HashSet<string> held = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Present.Count; i++)
				if (Present[i] != null && Present[i].Id != null) held.Add(Present[i].Id);
			for (int i = 0; i < Wanted.Count; i++)
				if (Wanted[i] != null && held.Contains(Wanted[i]))
					surviving.Add(FaultPresent + ":" + Wanted[i]);
			return surviving;
		}

		/// <summary>Every identity that appears more than once in one reading of one store.
		/// </summary>
		internal static List<string> Duplicates(IReadOnlyList<Unit> Units)
		{
			List<string> duplicates = new List<string>();
			if (Units == null) return duplicates;
			HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Units.Count; i++)
			{
				Unit unit = Units[i];
				if (unit == null || unit.Id == null) continue;
				if (!seen.Add(unit.Id)) duplicates.Add(FaultDuplicate + ":" + unit.Id);
			}
			return duplicates;
		}

		/// <summary>One charged line of a bill: a material kind and the units of it.</summary>
		internal sealed class Charge
		{
			internal readonly string Kind;
			internal readonly int Units;

			internal Charge(string Kind, int Units)
			{
				this.Kind = Kind;
				this.Units = Units;
			}
		}

		/// <summary>A bill rendered kind by kind, in a fixed ordinal order.</summary>
		internal static string DescribeCharges(IReadOnlyList<Charge> Charges)
		{
			if (Charges == null || Charges.Count == 0) return "empty";
			SortedDictionary<string, int> rows =
				new SortedDictionary<string, int>(StringComparer.Ordinal);
			for (int i = 0; i < Charges.Count; i++)
				if (Charges[i] != null && Charges[i].Kind != null)
					rows[Charges[i].Kind] = Charges[i].Units;
			StringBuilder line = new StringBuilder();
			foreach (KeyValuePair<string, int> row in rows)
			{
				if (line.Length > 0) line.Append(',');
				line.Append(row.Key).Append('=')
					.Append(row.Value.ToString(CultureInfo.InvariantCulture));
			}
			return line.ToString();
		}

		/// <summary>Every way a committed bill can differ from the authored one. EXACT, in both
		/// directions: a short charge and an OVERCHARGE are both faults, and any charged kind the
		/// authored bill never names is a fault too. Nothing here forgives an overcharge.</summary>
		internal static List<string> BillFaults(IReadOnlyList<Charge> Authored,
			IReadOnlyList<Charge> Committed)
		{
			List<string> faults = new List<string>();
			Dictionary<string, int> want = new Dictionary<string, int>(StringComparer.Ordinal);
			if (Authored != null)
				for (int i = 0; i < Authored.Count; i++)
					if (Authored[i] != null && Authored[i].Kind != null)
						want[Authored[i].Kind] = Authored[i].Units;
			Dictionary<string, int> got = new Dictionary<string, int>(StringComparer.Ordinal);
			if (Committed != null)
				for (int i = 0; i < Committed.Count; i++)
					if (Committed[i] != null && Committed[i].Kind != null)
						got[Committed[i].Kind] = Committed[i].Units;
			foreach (KeyValuePair<string, int> row in want)
			{
				int held;
				if (!got.TryGetValue(row.Key, out held)) held = 0;
				if (held < row.Value)
					faults.Add(FaultBillShort + ":" + row.Key + ":"
						+ held.ToString(CultureInfo.InvariantCulture) + "<"
						+ row.Value.ToString(CultureInfo.InvariantCulture));
				else if (held > row.Value)
					faults.Add(FaultBillOver + ":" + row.Key + ":"
						+ held.ToString(CultureInfo.InvariantCulture) + ">"
						+ row.Value.ToString(CultureInfo.InvariantCulture));
			}
			foreach (KeyValuePair<string, int> row in got)
				if (row.Value != 0 && !want.ContainsKey(row.Key))
					faults.Add(FaultBillExtra + ":" + row.Key + ":"
						+ row.Value.ToString(CultureInfo.InvariantCulture));
			faults.Sort(StringComparer.Ordinal);
			return faults;
		}

		/// <summary>A bounded, comma-joined rendering of a reason list for a journal row.
		/// </summary>
		internal static string Join(IReadOnlyList<string> Rows, int Cap)
		{
			if (Rows == null || Rows.Count == 0) return "-";
			StringBuilder line = new StringBuilder();
			int shown = Cap < 0 ? 0 : Cap;
			for (int i = 0; i < Rows.Count && i < shown; i++)
			{
				if (line.Length > 0) line.Append(',');
				line.Append(Rows[i] ?? "(null)");
			}
			if (Rows.Count > shown)
			{
				if (line.Length > 0) line.Append(',');
				line.Append('+').Append((Rows.Count - shown).ToString(CultureInfo.InvariantCulture))
					.Append(" more");
			}
			return line.ToString();
		}
	}
}
