using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public partial class KingdomSurvey
	{
		/// <summary>Pours water into the dedicated stores, updating the survey's counters.</summary>
		/// <param name="Drams">Amount offered.</param>
		/// <returns>Amount actually stored; the remainder had nowhere to go.</returns>
		public int Store(int Drams)
		{
			int remaining = Drams;
			for (int i = 0; i < Stores.Count && remaining > 0; i++)
			{
				LiquidVolume store = Stores[i];
				if (store.Volume >= store.MaxVolume || !KingdomLiquids.CanReceiveFreshWater(store))
				{
					continue;
				}
				int drams = store.MaxVolume - store.Volume;
				if (drams > remaining)
				{
					drams = remaining;
				}
				int added = KingdomLiquids.Fill(store, "water", drams);
				if (added > 0)
				{
					remaining -= added;
					StoredWater += added;
					StorageSpace -= added;
					SynchronizeReceiptObject(store.ParentObject);
				}
			}
			return Drams - remaining;
		}

		/// <summary>
		/// Pours into one exact dedicated vessel. The physical volume delta, including a callback
		/// that completed and then threw, is the only amount published to survey counters.
		/// </summary>
		public int StoreIn(LiquidVolume Store, int Drams)
		{
			if (Store == null || Drams <= 0 || !Stores.Contains(Store)
				|| Store.MaxVolume < 0 || Store.Volume < 0 || Store.Volume >= Store.MaxVolume
				|| !KingdomLiquids.CanReceiveFreshWater(Store)) return 0;
			int before = Store.Volume;
			int wanted = Store.MaxVolume - before;
			if (wanted > Drams) wanted = Drams;
			try
			{
				KingdomLiquids.Fill(Store, "water", wanted);
			}
			catch
			{
				// Measured volume delta below decides whether callback completed.
			}
			int added = Store.Volume - before;
			if (added <= 0 || added > wanted) return 0;
			StoredWater += added;
			StorageSpace -= added;
			SynchronizeReceiptObject(Store.ParentObject);
			return added;
		}

	}
}
