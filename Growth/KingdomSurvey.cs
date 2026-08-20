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

		/// <summary>
		/// Kingdom-wide defence bonus from garrison districts, folded in by
		/// <see cref="Take(Zone, KingdomSystem)"/>. Zero on a survey taken with the plain
		/// <see cref="Take(Zone)"/> overload, which knows nothing outside its own zone.
		/// </summary>
		public int DistrictDefenceBonus;

		/// <summary>
		/// Servings of food seen in the settlement's dedicated containers this pass: items
		/// carrying vanilla <c>Food</c> or <c>PreparedCookingIngredient</c>. Consuming food
		/// through <see cref="ConsumeFood"/> keeps this correct; a food item placed after the
		/// survey was taken does not retroactively appear here.
		/// </summary>
		public int FoodStored;

		/// <summary>Coarse abundance read on <see cref="FoodStored"/>. See
		/// <see cref="KingdomRules.ClassifyPantry"/>.</summary>
		public KingdomRules.PantryTier FoodAbundance;

		/// <summary>
		/// Containers marked as larders this pass. <see cref="ConsumeFood"/> walks these, in the
		/// order found, so a shared meal only ever draws from what the founder actually
		/// dedicated.
		/// </summary>
		public readonly List<GameObject> Larders = new List<GameObject>();

		public readonly List<LiquidVolume> Stores = new List<LiquidVolume>();

		public readonly List<LiquidVolume> Pools = new List<LiquidVolume>();

		public readonly List<GameObject> Settlers = new List<GameObject>();

		/// <summary>Beds the settlement built. Population cannot exceed these.</summary>
		public int Beds;

		/// <summary>Works the settlement built that require crew, in placement order.</summary>
		public readonly List<GameObject> Works = new List<GameObject>();

		/// <summary>Defensive works built here, crewed or not.</summary>
		public readonly List<GameObject> Defences = new List<GameObject>();

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
					if (item.GetIntProperty("KingdomDefence") > 0)
					{
						survey.Defences.Add(item);
					}
				}
				// Larders, not vessels. "KingdomStores" only ever marks liquid containers — the
				// dedication flow filters on LiquidVolume — so counting food there would read
				// zero forever. Food lives in what the founder dedicated as a larder.
				if (item.GetIntProperty("KingdomLarder") == 1 && item.Inventory != null)
				{
					survey.Larders.Add(item);
					foreach (GameObject held in item.Inventory.Objects)
					{
						if (held.HasPart("Food") || held.HasPart("PreparedCookingIngredient"))
						{
							survey.FoodStored += held.Count;
						}
					}
				}
				LiquidVolume part = item.GetPart<LiquidVolume>();
				if (part == null || part.Volume < 0)
				{
					continue;
				}
				bool isFreshWater = KingdomLiquids.HasFreshWater(part);
				if (part.MaxVolume < 0)
				{
					if (isFreshWater)
					{
						survey.Pools.Add(part);
						survey.OpenWater += part.Volume;
					}
				}
				else if (item.GetIntProperty("KingdomStores") == 1)
				{
					survey.Stores.Add(part);
					survey.StorageCapacity += part.MaxVolume;
					if (isFreshWater)
					{
						survey.StoredWater += part.Volume;
					}
					if (part.Volume < part.MaxVolume && KingdomLiquids.CanReceiveFreshWater(part))
					{
						survey.StorageSpace += part.MaxVolume - part.Volume;
					}
				}
			}
			survey.FoodAbundance = KingdomRules.ClassifyPantry(survey.FoodStored);
			return survey;
		}

		/// <summary>
		/// As <see cref="Take(Zone)"/>, but also folds in the settlement-wide defence bonus its
		/// districts earn. A garrison trains the whole watch, not just the tower standing on it,
		/// so the bonus is read from every claimed zone's district, not only this one.
		/// </summary>
		/// <param name="Z">Zone to survey. Null yields an empty survey.</param>
		/// <param name="System">Kingdom whose claimed-zone districts contribute the bonus.</param>
		public static KingdomSurvey Take(Zone Z, KingdomSystem System)
		{
			KingdomSurvey survey = Take(Z);
			if (System != null)
			{
				survey.DistrictDefenceBonus = KingdomRules.DistrictsDefenceBonus(System.ZoneDistricts.Values);
			}
			return survey;
		}

		/// <summary>
		/// The settlement's defence: the sum of its defensive works, counting only those with
		/// the crew to man them, plus any kingdom-wide bonus from garrison districts. A
		/// watchtower with nobody in it defends nothing; a garrison district defends everywhere.
		/// </summary>
		public int Defence()
		{
			int total = 0;
			for (int i = 0; i < Defences.Count; i++)
			{
				GameObject work = Defences[i];
				int need = work.GetIntProperty("KingdomStaffNeeded");
				int effectiveness = (need > 0) ? work.GetIntProperty("KingdomEffectiveness") : 100;
				total += work.GetIntProperty("KingdomDefence") * effectiveness / 100;
			}
			return total + DistrictDefenceBonus;
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
				if (!KingdomLiquids.HasFreshWater(store))
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

		/// <summary>
		/// Spends food from the dedicated larders, updating <see cref="FoodStored"/> and
		/// <see cref="FoodAbundance"/> to match. Draws whole food items, and partial stacks, from
		/// <see cref="Larders"/> in the order found, until the amount is met or the larders run
		/// out &mdash; never more than what is actually there, and never from anything the founder
		/// has not dedicated.
		/// </summary>
		/// <param name="Amount">Food units requested.</param>
		/// <returns>Amount actually spent, which may be less than requested.</returns>
		public int ConsumeFood(int Amount)
		{
			int remaining = Amount;
			for (int i = 0; i < Larders.Count && remaining > 0; i++)
			{
				GameObject container = Larders[i];
				if (container.Inventory == null)
				{
					continue;
				}
				// Snapshot first: destroying a food item below removes it from this same
				// Inventory list, and mutating a collection mid-foreach throws.
				List<GameObject> held = new List<GameObject>(container.Inventory.Objects);
				for (int j = 0; j < held.Count && remaining > 0; j++)
				{
					GameObject food = held[j];
					if (!food.HasPart("Food") && !food.HasPart("PreparedCookingIngredient"))
					{
						continue;
					}
					// Destroy() on a stack of more than one decrements it by exactly one and
					// leaves the object in place (see Stacker.HandleEvent(BeforeDestroyObjectEvent));
					// only the last unit actually removes it. Validate stops the loop the moment
					// that happens, rather than trusting a return value for it.
					while (remaining > 0 && GameObject.Validate(food))
					{
						food.Destroy(null, Silent: true);
						remaining--;
					}
				}
			}
			int spent = Amount - remaining;
			FoodStored -= spent;
			if (FoodStored < 0)
			{
				FoodStored = 0;
			}
			FoodAbundance = KingdomRules.ClassifyPantry(FoodStored);
			return spent;
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
