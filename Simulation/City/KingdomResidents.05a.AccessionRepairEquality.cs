using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Simulation.City
{
	public static partial class KingdomResidents
	{
		private static bool SameBindings(KingdomBindingTable A, KingdomBindingTable B)
		{
			if (A == null || B == null || A.Count != B.Count) return false;
			for (int i = 0; i < A.Count; i++)
			{
				KingdomBinding a;
				KingdomBinding b;
				if (!A.TryAt(i, out a) || !B.TryAt(i, out b)
					|| a.BindingKey != b.BindingKey || a.Kind != b.Kind || a.ZoneId != b.ZoneId
					|| a.ObjectId != b.ObjectId || a.MintedTick != b.MintedTick) return false;
			}
			return true;
		}

		/// <summary>Removes one person from a per-city tally without leaving zero rows behind.</summary>
		private static void DropCount(Dictionary<string, int> Counts, string Key)
		{
			if (Counts == null || Key == null || !Counts.TryGetValue(Key, out int count))
			{
				return;
			}
			if (count > 1)
			{
				Counts[Key] = count - 1;
			}
			else
			{
				Counts.Remove(Key);
			}
		}
	}
}
