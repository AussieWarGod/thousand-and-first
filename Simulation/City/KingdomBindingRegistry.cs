using System;
using System.Collections.Generic;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// The binding registry as the save file holds it: realm-scope, written as columns.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;3.8 puts this on <c>KingdomSystem</c> beside the realm seed
	/// &mdash; <b>not</b> on a settlement, because a bound body can be in another city's zone or
	/// walked off the map entirely. It is therefore realm state and must never appear among
	/// <c>KingdomSettlement</c>'s carried fields; <c>SettlementSeatTests</c> asserts that directly,
	/// and a seat swap consequently leaves it exactly as it found it.
	/// </para>
	/// <para>
	/// Columns rather than a list of row composites, for the reason the city book gives: &sect;0.0(c)
	/// budgets the model with no per-row object header.
	/// </para>
	/// </summary>
	[Serializable]
	public class KingdomBindingRegistry
#if !TAF_TESTS
		: IComposite
#endif
	{
		public List<int> Keys = new List<int>();

		public List<int> Kinds = new List<int>();

		public List<string> ZoneIds = new List<string>();

		/// <summary>The engine's own persistent object <c>ID</c>, as a string. Never a live
		/// reference: the case &sect;3.8 exists for is a body whose zone is on disk.</summary>
		public List<string> ObjectIds = new List<string>();

		public List<long> MintedTicks = new List<long>();

#if !TAF_TESTS
		public bool WantFieldReflection => false;

		public void Write(SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(KingdomBindingRegistry));
		}

		public void Read(SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(KingdomBindingRegistry));
			Normalize();
		}
#endif

		public int Count => Keys.Count;

		/// <summary>
		/// Repairs a registry read from a save written by an older build. Null columns become
		/// empty; <b>ragged columns are truncated to the shortest</b>, because a binding half of
		/// whose fields are missing is not a binding and a reader that trusted the longest column
		/// would invent one out of a default key.
		/// <para>
		/// A duplicate key is dropped rather than carried: a save that came back holding one key
		/// twice is a save that can put a settler in two places, and the first row wins because it
		/// is the one every earlier session was already answering with.
		/// </para>
		/// </summary>
		public void Normalize()
		{
			Keys = Repair(Keys);
			Kinds = Repair(Kinds);
			ZoneIds = Repair(ZoneIds);
			ObjectIds = Repair(ObjectIds);
			MintedTicks = Repair(MintedTicks);
			int count = Shortest(Keys.Count, Kinds.Count, ZoneIds.Count, ObjectIds.Count, MintedTicks.Count);
			Trim(Keys, count);
			Trim(Kinds, count);
			Trim(ZoneIds, count);
			Trim(ObjectIds, count);
			Trim(MintedTicks, count);
			for (int i = Keys.Count - 1; i >= 0; i--)
			{
				if (ZoneIds[i] == null)
				{
					ZoneIds[i] = "";
				}
				if (ObjectIds[i] == null)
				{
					ObjectIds[i] = "";
				}
				if (MintedTicks[i] < 0L)
				{
					MintedTicks[i] = 0L;
				}
				if (Keys[i] == 0 || Duplicated(i))
				{
					RemoveAt(i);
				}
			}
			DropOverCap(KingdomBindingKind.Resident, KingdomBindingTable.MaxResidentBindings);
			DropOverCap(KingdomBindingKind.Transient, KingdomBindingTable.MaxTransientBindings);
		}

		/// <summary>The registry as the frozen table the rules layer works on. Refuses and
		/// publishes nothing rather than handing back a half-built one.</summary>
		internal bool TryRead(out KingdomBindingTable table, out KingdomCityFault fault)
		{
			Normalize();
			KingdomBinding[] rows = new KingdomBinding[Keys.Count];
			for (int i = 0; i < rows.Length; i++)
			{
				rows[i] = new KingdomBinding(Keys[i], KindOf(Kinds[i]), ZoneIds[i], ObjectIds[i], MintedTicks[i]);
			}
			return KingdomBindingTable.TryCreate(rows, out table, out fault);
		}

		/// <summary>Writes one frozen table into the columns, in one call and after the rules have
		/// succeeded. The single publisher &sect;1.3 requires, applied to the registry &mdash; which
		/// is what makes "the mint and the binding are published together or not at all" a fact
		/// about the code and not an intention.</summary>
		internal bool TryPublish(KingdomBindingTable table, out KingdomCityFault fault)
		{
			if (table == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			Keys.Clear();
			Kinds.Clear();
			ZoneIds.Clear();
			ObjectIds.Clear();
			MintedTicks.Clear();
			for (int i = 0; i < table.Count; i++)
			{
				KingdomBinding binding;
				if (!table.TryAt(i, out binding))
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
				Keys.Add(binding.BindingKey);
				Kinds.Add((int)binding.Kind);
				ZoneIds.Add(binding.ZoneId ?? "");
				ObjectIds.Add(binding.ObjectId ?? "");
				MintedTicks.Add(binding.MintedTick);
			}
			fault = KingdomCityFault.None;
			return true;
		}

		/// <summary>Anything this build has no word for reads as a transient, which is the side
		/// that can be swept and refused rather than the side that is a person.</summary>
		private static KingdomBindingKind KindOf(int stored)
		{
			return (stored == (int)KingdomBindingKind.Resident)
				? KingdomBindingKind.Resident
				: KingdomBindingKind.Transient;
		}

		private bool Duplicated(int index)
		{
			for (int i = 0; i < index; i++)
			{
				if (Keys[i] == Keys[index] && KindOf(Kinds[i]) == KindOf(Kinds[index]))
				{
					return true;
				}
			}
			return false;
		}

		private void DropOverCap(KingdomBindingKind kind, int cap)
		{
			int seen = 0;
			for (int i = 0; i < Keys.Count; i++)
			{
				if (KindOf(Kinds[i]) != kind)
				{
					continue;
				}
				seen++;
				if (seen > cap)
				{
					RemoveAt(i);
					i--;
				}
			}
		}

		private void RemoveAt(int index)
		{
			Keys.RemoveAt(index);
			Kinds.RemoveAt(index);
			ZoneIds.RemoveAt(index);
			ObjectIds.RemoveAt(index);
			MintedTicks.RemoveAt(index);
		}

		private static List<T> Repair<T>(List<T> column)
		{
			return column ?? new List<T>();
		}

		private static int Shortest(int a, int b, int c, int d, int e)
		{
			int shortest = a;
			if (b < shortest) { shortest = b; }
			if (c < shortest) { shortest = c; }
			if (d < shortest) { shortest = d; }
			if (e < shortest) { shortest = e; }
			return shortest;
		}

		private static void Trim<T>(List<T> column, int count)
		{
			if (column.Count > count)
			{
				column.RemoveRange(count, column.Count - count);
			}
		}
	}
}
