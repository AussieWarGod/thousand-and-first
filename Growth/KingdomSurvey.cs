using System.Collections.Generic;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>
	/// A single-pass accounting of everything in a zone the kingdom cares about: dedicated
	/// water stores, open water, citizens, and the trade post. Take one per zone activation
	/// and pass it down; the alternative is a full-zone scan per question, and there are
	/// twenty questions.
	/// </summary>
	/// <remarks>A survey is a snapshot. Consuming or adding water through the survey keeps
	/// its counters correct; spawning or destroying objects invalidates its lists.</remarks>
	public class KingdomSurvey
	{
		public int StoredWater;

		public int OpenWater;

		public int StorageSpace;

		public int StorageCapacity;

		public int Citizens;

		public bool HasTradePost;

		public readonly List<LiquidVolume> Stores = new List<LiquidVolume>();

		public readonly List<LiquidVolume> Pools = new List<LiquidVolume>();

		public readonly List<GameObject> Settlers = new List<GameObject>();

		/// <summary>Beds the settlement built. Population cannot exceed these.</summary>
		public int Beds;

		/// <summary>Works the settlement built that require crew, in placement order.</summary>
		public readonly List<GameObject> Works = new List<GameObject>();

		/// <summary>Walks the zone once and classifies every object of interest.</summary>
		/// <param name="Z">Zone to survey. Null yields an empty survey.</param>
		public static KingdomSurvey Take(Zone Z)
		{
			KingdomSurvey survey = new KingdomSurvey();
			if (Z == null)
			{
				return survey;
			}
			foreach (GameObject item in Z.GetObjects())
			{
				if (item.GetIntProperty("KingdomCitizen") == 1)
				{
					survey.Citizens++;
					if (item.GetIntProperty("VillageMerchant") == 1)
					{
						survey.HasTradePost = true;
					}
					else if (item.GetIntProperty("KingdomBorn") == 1 && !item.IsPlayer() && !item.IsPlayerLed())
					{
						survey.Settlers.Add(item);
					}
				}
				if (item.GetIntProperty("KingdomBuilt") == 1)
				{
					if (item.HasPart("Bed"))
					{
						survey.Beds++;
					}
					if (item.GetIntProperty("KingdomStaffNeeded") > 0)
					{
						survey.Works.Add(item);
					}
				}
				LiquidVolume part = item.GetPart<LiquidVolume>();
				if (part == null || part.Volume < 0)
				{
					continue;
				}
				bool isWater = part.Volume > 0 && part.GetPrimaryLiquidID() == "water";
				if (part.MaxVolume < 0)
				{
					if (isWater)
					{
						survey.Pools.Add(part);
						survey.OpenWater += part.Volume;
					}
				}
				else if (item.GetIntProperty("KingdomStores") == 1)
				{
					survey.Stores.Add(part);
					survey.StorageCapacity += part.MaxVolume;
					if (isWater)
					{
						survey.StoredWater += part.Volume;
					}
					if (part.Volume < part.MaxVolume && (part.Volume == 0 || isWater))
					{
						survey.StorageSpace += part.MaxVolume - part.Volume;
					}
				}
			}
			return survey;
		}

		/// <summary>Draws water from the dedicated stores, updating the survey's counters.</summary>
		/// <param name="Drams">Amount requested.</param>
		/// <returns>Amount actually drawn, which may be less than requested.</returns>
		public int Consume(int Drams)
		{
			int remaining = Drams;
			for (int i = 0; i < Stores.Count && remaining > 0; i++)
			{
				LiquidVolume store = Stores[i];
				if (store.Volume <= 0 || store.GetPrimaryLiquidID() != "water")
				{
					continue;
				}
				int removed = KingdomLiquids.Drain(store, remaining);
				if (removed > 0)
				{
					remaining -= removed;
					StoredWater -= removed;
					StorageSpace += removed;
				}
			}
			return Drams - remaining;
		}

		/// <summary>Pours water into the dedicated stores, updating the survey's counters.</summary>
		/// <param name="Drams">Amount offered.</param>
		/// <returns>Amount actually stored; the remainder had nowhere to go.</returns>
		public int Store(int Drams)
		{
			int remaining = Drams;
			for (int i = 0; i < Stores.Count && remaining > 0; i++)
			{
				LiquidVolume store = Stores[i];
				if (store.Volume >= store.MaxVolume || (store.Volume > 0 && store.GetPrimaryLiquidID() != "water"))
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
				}
			}
			return Drams - remaining;
		}

		/// <summary>Drains open water sources, updating the survey's counters.</summary>
		/// <param name="Drams">Amount requested.</param>
		/// <returns>Amount actually drawn.</returns>
		public int DrawFromPools(int Drams)
		{
			int remaining = Drams;
			for (int i = 0; i < Pools.Count && remaining > 0; i++)
			{
				LiquidVolume pool = Pools[i];
				if (pool.Volume <= 0)
				{
					continue;
				}
				int removed = KingdomLiquids.Drain(pool, remaining);
				if (removed > 0)
				{
					remaining -= removed;
					OpenWater -= removed;
				}
			}
			return Drams - remaining;
		}
	}
}
