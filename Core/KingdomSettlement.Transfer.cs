using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst
{
	public partial class KingdomSettlement
	{
		internal static void TruncateParallelRows(params List<string>[] Columns)
		{
			if (Columns == null || Columns.Length == 0)
			{
				return;
			}
			int rows = Columns[0].Count;
			for (int i = 1; i < Columns.Length; i++)
			{
				rows = Math.Min(rows, Columns[i].Count);
			}
			for (int i = 0; i < Columns.Length; i++)
			{
				if (Columns[i].Count > rows)
				{
					Columns[i].RemoveRange(rows, Columns[i].Count - rows);
				}
			}
		}

		/// <summary>
		/// Reads every field of this settlement from the same-named field of <paramref name="Seat"/>.
		/// <para>
		/// Preconditions: the seat carries a field of the same name and exact type for every field
		/// declared here. Side effects: this settlement's fields are overwritten and
		/// <see cref="Normalize"/> is run; the seat is not touched. Mutable containers are handed
		/// over by reference rather than cloned, which is safe only because the caller restores
		/// another settlement over the seat in the same breath &mdash; see
		/// <c>KingdomSystem.TrySeat</c>.
		/// </para>
		/// </summary>
		/// <param name="Seat">The object holding the seated settlement's flat fields.</param>
		/// <exception cref="KingdomSeatMismatchException">A field here has no counterpart there.
		/// Nothing is written when this is thrown.</exception>
		public void ReadFrom(object Seat)
		{
			if (Seat == null)
			{
				throw new KingdomSeatMismatchException("No seat was supplied to read a settlement from.");
			}
			FieldInfo[] carried = Carried;
			FieldInfo[] counterparts = Counterparts(Seat.GetType());
			// Gathered first, assigned second: a failure mid-read must not leave a settlement
			// wearing half of one city and half of another.
			object[] values = new object[carried.Length];
			for (int i = 0; i < carried.Length; i++)
			{
				values[i] = counterparts[i].GetValue(Seat);
			}
			for (int i = 0; i < carried.Length; i++)
			{
				carried[i].SetValue(this, values[i]);
			}
			Normalize();
		}

		/// <summary>
		/// Writes every field of this settlement onto the same-named field of
		/// <paramref name="Seat"/>, making it the seated city.
		/// <para>
		/// Preconditions and hand-over semantics are those of <see cref="ReadFrom"/>, reversed.
		/// <see cref="Normalize"/> runs on this settlement first, so a seat is never given a null
		/// roster or ledger.
		/// </para>
		/// </summary>
		/// <param name="Seat">The object holding the seated settlement's flat fields.</param>
		/// <exception cref="KingdomSeatMismatchException">A field here has no counterpart there.
		/// Nothing is written when this is thrown.</exception>
		public void WriteTo(object Seat)
		{
			if (Seat == null)
			{
				throw new KingdomSeatMismatchException("No seat was supplied to write a settlement to.");
			}
			Normalize();
			FieldInfo[] carried = Carried;
			FieldInfo[] counterparts = Counterparts(Seat.GetType());
			object[] values = new object[carried.Length];
			for (int i = 0; i < carried.Length; i++)
			{
				values[i] = carried[i].GetValue(this);
			}
			for (int i = 0; i < carried.Length; i++)
			{
				counterparts[i].SetValue(Seat, values[i]);
			}
		}

		/// <summary>
		/// Every field one settlement holds. Reflected rather than listed, because a hand-written
		/// list is what rots. A copy, so a caller cannot reorder the array the swap is keyed on.
		/// </summary>
		public static FieldInfo[] CarriedFields()
		{
			return (FieldInfo[])Carried.Clone();
		}

		/// <summary>
		/// Names the fields <paramref name="SeatType"/> cannot carry, with the reason. Empty means
		/// the seat is a complete home for a settlement. This is the check a tester can run
		/// (<c>kingdom:dump</c>) to prove that adding a field here did not silently start losing
		/// a city on every swap.
		/// </summary>
		/// <param name="SeatType">The type holding the seated settlement's flat fields.</param>
		/// <returns>One line per unusable field; never null.</returns>
		public static List<string> SeatMismatches(Type SeatType)
		{
			List<string> mismatches = new List<string>();
			if (SeatType == null)
			{
				mismatches.Add("(no seat type)");
				return mismatches;
			}
			foreach (FieldInfo field in Carried)
			{
				FieldInfo counterpart = FindCounterpart(SeatType, field.Name);
				if (counterpart == null)
				{
					mismatches.Add(field.Name + " (no field of that name on " + SeatType.Name + ")");
				}
				else if (counterpart.FieldType != field.FieldType)
				{
					mismatches.Add(field.Name + " (expected " + field.FieldType.Name + ", seat holds " + counterpart.FieldType.Name + ")");
				}
			}
			return mismatches;
		}

	}
}
