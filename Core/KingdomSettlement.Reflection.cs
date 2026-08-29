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
		/// <summary>
		/// The same-named, same-typed seat fields for every carried field, in
		/// <see cref="CarriedFields"/> order. Cached per seat type: the answer cannot change
		/// within a run, and the swap must stay cheap enough to sit in zone activation.
		/// </summary>
		/// <exception cref="KingdomSeatMismatchException">The seat cannot carry a settlement.</exception>
		private static FieldInfo[] Counterparts(Type SeatType)
		{
			if (CounterpartCache.TryGetValue(SeatType, out var cached))
			{
				return cached;
			}
			List<string> mismatches = SeatMismatches(SeatType);
			if (mismatches.Count > 0)
			{
				throw new KingdomSeatMismatchException(SeatType.Name + " cannot carry a settlement; " + mismatches.Count + " field(s) unaccounted for: " + string.Join("; ", mismatches.ToArray()));
			}
			FieldInfo[] carried = Carried;
			FieldInfo[] counterparts = new FieldInfo[carried.Length];
			for (int i = 0; i < carried.Length; i++)
			{
				counterparts[i] = FindCounterpart(SeatType, carried[i].Name);
			}
			CounterpartCache[SeatType] = counterparts;
			return counterparts;
		}

		/// <summary>
		/// The seat's field of this name, or null. Declared fields are searched before inherited
		/// ones so a seat that shadows a base-class field resolves to its own rather than raising
		/// an ambiguity the caller cannot act on.
		/// </summary>
		private static FieldInfo FindCounterpart(Type SeatType, string Name)
		{
			for (Type type = SeatType; type != null; type = type.BaseType)
			{
				FieldInfo field = type.GetField(Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
				if (field != null)
				{
					return field;
				}
			}
			return null;
		}

		/// <summary>Read once: the swap pairs fields by position in this array, so every part of
		/// it must be looking at the same array.</summary>
		private static readonly FieldInfo[] Carried = typeof(KingdomSettlement).GetFields(BindingFlags.Instance | BindingFlags.Public);

		private static readonly Dictionary<Type, FieldInfo[]> CounterpartCache = new Dictionary<Type, FieldInfo[]>();	}
}
