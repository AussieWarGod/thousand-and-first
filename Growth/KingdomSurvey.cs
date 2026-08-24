using System;
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
	/// <remarks>A survey is a snapshot. Consuming or adding water and food through the survey
	/// keeps its counters correct; spawning or destroying objects invalidates its lists.</remarks>
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

		/// <summary>
		/// Servings the settlement's dedicated larders could hold between them, from each
		/// container's own declared capacity (<see cref="KingdomRules.LarderCapacityTag"/>,
		/// defaulting to <see cref="KingdomRules.DefaultLarderCapacity"/>). The food side of
		/// <see cref="StorageCapacity"/>, and physical in exactly the same way: a vessel says how
		/// much it holds, and the catalogue never does.
		/// </summary>
		public int FoodCapacity;

		/// <summary>
		/// Room left in the larders. DERIVED rather than counted, unlike its water counterpart
		/// <see cref="StorageSpace"/>, so that a caller which puts food in by another road
		/// &mdash; the kitchen garden's own harvest, which spawns crops straight into
		/// <see cref="Larders"/> and adjusts <see cref="FoodStored"/> &mdash; cannot leave this
		/// figure stale behind it.
		/// </summary>
		public int FoodSpace => (FoodCapacity > FoodStored) ? (FoodCapacity - FoodStored) : 0;

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

		/// <summary>
		/// Finished works here that carry vanilla's <c>Campfire</c> part &mdash; the communal
		/// fire, and the oven above it. A settlement with none of these cannot cook, however full
		/// its larders are, which is the gate on the favoured meal
		/// (<c>KingdomRules.JudgeMeal</c>).
		/// </summary>
		public int Kitchens;

		/// <summary>Works the settlement built that require crew, in placement order.</summary>
		public readonly List<GameObject> Works = new List<GameObject>();

		/// <summary>
		/// Everything the settlement built and finished, crewed or not, in placement order.
		/// <para>
		/// A superset of <see cref="Works"/> and of <see cref="Defences"/>: a cistern asks for
		/// nobody and a palisade defends without a crew, and both stand. This is the list
		/// <c>KingdomSubsidence</c> sums <c>Carries</c> over, so a settlement is measured by
		/// everything holding it up rather than only by the parts of it that want hands.
		/// </para>
		/// <para>
		/// Gated on <c>KingdomBuilt</c> like the rest, which is what makes "a scaffold carries
		/// nothing until it is raised" true here as well &mdash; a half-built cistern feeds
		/// nobody, and a settlement cannot outrun its own level by staking plans.
		/// </para>
		/// </summary>
		public readonly List<GameObject> Built = new List<GameObject>();

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
					survey.Built.Add(item);
					if (item.HasPart("Bed"))
					{
						survey.Beds++;
					}
					// Somewhere to cook, counted off vanilla's own cooking part rather than
					// off a catalogue key: Campfire IS the entire cooking system in Qud
					// (D/XRL/World/Parts/Campfire.cs), so the communal fire has always been a
					// real cooking site and nothing in this mod said so until now. An oven is
					// the same part with the settlement's own dish set on its PresetMeals.
					// Addendum 11(c)'s first clause, read literally: extend the machine that
					// already does the thing.
					if (item.HasPart("Campfire"))
					{
						survey.Kitchens++;
					}
					// A field with no seed in it asks for nobody (Addendum 11(b)). It stays in
					// Built, so it is still measured, still worn, still mended and still struck -
					// what it stops being is somewhere the staffing pass sends people. Without
					// this, bare ground took the four hands the home farm's crew wants and turned
					// them into nothing, and those are exactly the hands that would otherwise be
					// out foraging: the seed gate would have quietly cost a settlement its meal
					// as well as its harvest.
					if (item.GetIntProperty("KingdomStaffNeeded") > 0 && (KingdomCrops.FieldOf(item) == null || KingdomCrops.IsSown(item)))
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
					survey.FoodCapacity += CapacityOf(item);
					survey.FoodStored += HeldIn(item);
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
		/// Reserves an all-or-nothing water debit against the exact dedicated vessels in this
		/// snapshot. Reservation does not remove water. The returned receipt must be
		/// <see cref="KingdomWaterDebit.Commit"/>ted after the caller's other preconditions pass,
		/// and may be <see cref="KingdomWaterDebit.Rollback"/>ed into those same vessels if the
		/// enclosing operation later fails. Use <see cref="Consume"/> instead where a deliberately
		/// partial simulation loss is the rule.
		/// </summary>
		/// <param name="Drams">Exact amount required. Non-positive amounts reserve a total no-op.</param>
		public KingdomWaterDebit ReserveExactWater(int Drams)
		{
			return KingdomWaterDebit.Reserve(this, Drams);
		}

		/// <summary>Try-pattern facade for callers that must not proceed without an exact receipt.</summary>
		public bool TryReserveExactWater(int Drams, out KingdomWaterDebit Debit)
		{
			Debit = ReserveExactWater(Drams);
			return Debit.State == KingdomWaterDebitState.Reserved;
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
			int _;
			return ConsumeFood(Amount, null, out _);
		}

		/// <summary>
		/// The meal-shaped draw (Addendum 11(b)): spends the day's food out of the dedicated
		/// larders, <b>reaching for the settlement's own dish first</b>.
		/// <para>
		/// <b>The order, stated once and deterministic.</b> Pass one takes
		/// <paramref name="Preferred"/> &mdash; the preserved staple the settlement's favourite
		/// dish is made of, which is also what its mill produces &mdash; walking
		/// <see cref="Larders"/> in the order found and each larder's inventory in the order
		/// held. Pass two takes everything else that is food, in the same order. Nothing is
		/// random, so the same larders drained in the same sequence give the same answer on every
		/// reload, which is what Addendum 12(d) asks of any draw that lands on real containers.
		/// </para>
		/// <para>
		/// Why the staple goes first and not last: a settlement eats what it cooks. The staple is
		/// the thing the fields grew and the mill bound to keep, and a granary that hoards its own
		/// dish while the settlement chews raw tubers is not a settlement anybody would write
		/// down. It also makes the favoured meal reachable in exactly the case it should be
		/// &mdash; when the chain from field to mill to table is actually running.
		/// </para>
		/// </summary>
		/// <param name="Amount">Food units requested.</param>
		/// <param name="Preferred">The dish's staple blueprint, or null to draw in plain order.</param>
		/// <param name="FromPreferred">Set to how much of the draw came off that staple, which is
		/// what <c>KingdomRules.JudgeMeal</c> reads to decide whether the settlement ate its own
		/// dish or merely ate.</param>
		/// <returns>Amount actually spent, which may be less than requested.</returns>
		public int ConsumeFood(int Amount, string Preferred, out int FromPreferred)
		{
			FromPreferred = 0;
			int remaining = Amount;
			if (!string.IsNullOrEmpty(Preferred))
			{
				FromPreferred = Draw(ref remaining, Preferred);
			}
			Draw(ref remaining, null);
			int spent = Amount - remaining;
			FoodStored -= spent;
			if (FoodStored < 0)
			{
				FoodStored = 0;
			}
			FoodAbundance = KingdomRules.ClassifyPantry(FoodStored);
			return spent;
		}

		/// <summary>
		/// One pass of the draw. Counters are left to the caller so a two-pass draw adjusts
		/// <see cref="FoodStored"/> exactly once.
		/// </summary>
		/// <param name="Remaining">Servings still wanted; decremented in place.</param>
		/// <param name="Blueprint">Restrict to this blueprint, or null for anything edible.</param>
		/// <returns>Servings this pass took.</returns>
		private int Draw(ref int Remaining, string Blueprint)
		{
			int took = 0;
			for (int i = 0; i < Larders.Count && Remaining > 0; i++)
			{
				GameObject container = Larders[i];
				if (container == null || container.Inventory == null)
				{
					continue;
				}
				// Snapshot first: destroying a food item below removes it from this same
				// Inventory list, and mutating a collection mid-foreach throws.
				List<GameObject> held = new List<GameObject>(container.Inventory.Objects);
				for (int j = 0; j < held.Count && Remaining > 0; j++)
				{
					GameObject food = held[j];
					if (!food.HasPart("Food") && !food.HasPart("PreparedCookingIngredient"))
					{
						continue;
					}
					if (Blueprint != null && food.Blueprint != Blueprint)
					{
						continue;
					}
					// Destroy() on a stack of more than one decrements it by exactly one and
					// leaves the object in place (see Stacker.HandleEvent(BeforeDestroyObjectEvent));
					// only the last unit actually removes it. Validate stops the loop the moment
					// that happens, rather than trusting a return value for it.
					while (Remaining > 0 && GameObject.Validate(food))
					{
						food.Destroy(null, Silent: true);
						Remaining--;
						took++;
					}
				}
			}
			return took;
		}

		/// <summary>
		/// Takes raw crops of one named blueprint out of the larders, for industry rather than
		/// for a mouth (Addendum 11(b): food "used by industry to produce things"). The grinding
		/// mill's input half; <see cref="StoreFood"/> puts the preserved staple back.
		/// <para>
		/// Named rather than general on purpose: a mill grinds the settlement's own harvest, and
		/// a draw that took anything edible would grind the staple it had just made back into
		/// itself. Same order and the same determinism as <see cref="ConsumeFood"/>.
		/// </para>
		/// </summary>
		/// <param name="Blueprint">The crop to grind. Null or empty takes nothing.</param>
		/// <param name="Amount">Crops wanted.</param>
		/// <returns>Crops actually taken, which may be fewer than asked for.</returns>
		public int ConsumeCrop(string Blueprint, int Amount)
		{
			if (string.IsNullOrEmpty(Blueprint) || Amount <= 0)
			{
				return 0;
			}
			int remaining = Amount;
			int took = Draw(ref remaining, Blueprint);
			FoodStored -= took;
			if (FoodStored < 0)
			{
				FoodStored = 0;
			}
			FoodAbundance = KingdomRules.ClassifyPantry(FoodStored);
			return took;
		}


		/// <summary>
		/// Servings one container holds right now: vanilla <c>Food</c> or
		/// <c>PreparedCookingIngredient</c> items, counted by stack rather than by object so a
		/// stack of twenty apples reads as twenty.
		/// </summary>
		/// <param name="Container">Any object. Null, or one with no inventory, holds nothing.</param>
		public static int HeldIn(GameObject Container)
		{
			if (Container == null || Container.Inventory == null)
			{
				return 0;
			}
			int held = 0;
			foreach (GameObject item in Container.Inventory.Objects)
			{
				if (item.HasPart("Food") || item.HasPart("PreparedCookingIngredient"))
				{
					held += item.Count;
				}
			}
			return held;
		}

		/// <summary>
		/// Servings one container was built to hold, off its blueprint's own
		/// <see cref="KingdomRules.LarderCapacityTag"/> &mdash; the food side of a vessel's
		/// <c>MaxVolume</c>. A container that declares nothing gets
		/// <see cref="KingdomRules.DefaultLarderCapacity"/>, which is the chest a founder walked
		/// up to and dedicated by hand.
		/// </summary>
		public static int CapacityOf(GameObject Container)
		{
			if (Container == null)
			{
				return 0;
			}
			int declared;
			// GetTag reads the blueprint's own dictionary, so a modded pantry declares its size
			// in XML exactly the way a modded cistern declares MaxVolume.
			if (!int.TryParse(Container.GetTag(KingdomRules.LarderCapacityTag, ""), out declared))
			{
				declared = 0;
			}
			return KingdomRules.LarderCapacity(declared);
		}

		/// <summary>
		/// Takes a finished work into the settlement's food stores and keeps this survey's
		/// counters in step, so a granary raised before this pass is a pantry from the moment the
		/// pass notices it rather than from the pass after.
		/// <para>
		/// STANDARDS 7 &mdash; "commissioned storage auto-flags" &mdash; is the whole warrant.
		/// Nothing the founder placed is swept up: the caller is expected to have checked
		/// <c>KingdomBuilt</c> and <see cref="KingdomRules.IsCivicLarderBlueprint"/>, which
		/// between them mean the settlement paid for this and the catalogue calls it a pantry.
		/// </para>
		/// </summary>
		/// <param name="Work">The finished container. Null, one with no inventory, or one already
		/// dedicated is left alone.</param>
		/// <returns>True when this call is what dedicated it.</returns>
		public bool AdoptLarder(GameObject Work)
		{
			if (Work == null || Work.Inventory == null || Work.GetIntProperty("KingdomLarder") == 1)
			{
				return false;
			}
			Work.SetIntProperty("KingdomLarder", 1);
			Larders.Add(Work);
			FoodCapacity += CapacityOf(Work);
			FoodStored += HeldIn(Work);
			FoodAbundance = KingdomRules.ClassifyPantry(FoodStored);
			return true;
		}

		/// <summary>
		/// Puts food into the dedicated larders, updating the survey's counters. The food mirror
		/// of <see cref="Store"/>, and bounded the same way: each container takes what it has room
		/// for and no more, and whatever did not fit is handed back to the caller to be honest
		/// about.
		/// <para>
		/// Room is measured off each container as it is reached rather than read from a figure
		/// cached at <see cref="Take(Zone)"/> time, because the harvest path spawns crops into
		/// these same containers by another road within the same pass.
		/// </para>
		/// </summary>
		/// <param name="Amount">Servings offered.</param>
		/// <param name="Blueprint">What the food physically is &mdash; the settlement's own crop,
		/// from <c>KingdomCropRules.CropBlueprintForStyle</c>, so a fungal city's granary fills
		/// with mushrooms. An unknown blueprint stores nothing rather than minting a null.</param>
		/// <returns>Servings actually stored; the remainder had nowhere to go.</returns>
		public int StoreFood(int Amount, string Blueprint)
		{
			if (Amount <= 0 || string.IsNullOrEmpty(Blueprint))
			{
				return 0;
			}
			int remaining = Amount;
			for (int i = 0; i < Larders.Count && remaining > 0; i++)
			{
				GameObject container = Larders[i];
				if (container == null || container.Inventory == null)
				{
					continue;
				}
				int room = CapacityOf(container) - HeldIn(container);
				if (room <= 0)
				{
					continue;
				}
				int put = (room < remaining) ? room : remaining;
				for (int j = 0; j < put; j++)
				{
					GameObject food = GameObject.Create(Blueprint);
					if (food == null)
					{
						// The blueprint does not resolve. Nothing further is stored and nothing is
						// lost: the caller gets the rest of its offer back and can say so.
						return Amount - remaining;
					}
					// A crop this settlement's own style names but that is not actually food would
					// otherwise be an unbounded spawn: HeldIn would never count it, so the room
					// would never fill and every pass would put more of it in the chest forever.
					// Refuse the whole errand instead, and take the one object back out with us.
					if (!food.HasPart("Food") && !food.HasPart("PreparedCookingIngredient"))
					{
						food.Obliterate();
						return Amount - remaining;
					}
					container.Inventory.AddObject(food, Silent: true);
					remaining--;
				}
			}
			int stored = Amount - remaining;
			FoodStored += stored;
			FoodAbundance = KingdomRules.ClassifyPantry(FoodStored);
			return stored;
		}

		/// <summary>
		/// Food lost out of one damaged larder, keeping the survey's counters correct the same way
		/// <see cref="ConsumeFood"/> does. The food mirror of <see cref="LeakFrom"/>, and loss
		/// rather than transfer for the same reason: this is a harvest gone bad in a holed
		/// granary, not a harvest moved somewhere the founder can walk up to (Addendum 10(b)).
		/// </summary>
		/// <param name="Container">The damaged pantry. Must be one of <see cref="Larders"/>.</param>
		/// <param name="Amount">Servings the spoilage is owed.</param>
		/// <returns>Servings actually lost, measured from the container rather than assumed.</returns>
		public int SpoilFrom(GameObject Container, int Amount)
		{
			int lost;
			TrySpoilFromExact(Container, Amount, out lost);
			return lost;
		}

		private sealed class SpoilFrame
		{
			internal GameObject Container;
			internal string ContainerId;
			internal Zone Zone;
			internal string ZoneId;
			internal Cell Cell;
			internal Inventory Inventory;
			internal List<GameObject> List;
			internal GameObject[] Items;
			internal string[] ItemIds;
			internal int[] Counts;
			internal bool[] Edible;
			internal List<GameObject> LarderList;
			internal GameObject[] LarderRows;
			internal int FoodStored;
			internal int FoodCapacity;
			internal KingdomRules.PantryTier FoodAbundance;
		}

		/// <summary>
		/// Invokes each destructive food callback once. Every unit is counted only after the exact
		/// same container, Inventory part/list, item ordering, identities, ownership, and counts prove
		/// the one expected transition. A veto with no delta never counts.
		/// </summary>
		public bool TrySpoilFromExact(GameObject Container, int Amount, out int Lost)
		{
			Lost = 0;
			SpoilFrame frame;
			if (!TryCaptureSpoilFrame(Container, Amount, out frame)) return false;
			int[] expected = (int[])frame.Counts.Clone();
			int remaining = Amount;
			for (int i = 0; i < frame.Items.Length && remaining > 0; i++)
			{
				if (!frame.Edible[i]) continue;
				GameObject food = frame.Items[i];
				while (remaining > 0 && expected[i] > 0)
				{
					if (!SpoilTopologyExact(frame, expected)) return false;
					int before = expected[i];
					try
					{
						food.Destroy(null, Silent: true);
					}
					catch
					{
						// The exact post-callback topology below, never the exception or return value,
						// decides whether one physical unit was lost.
					}
					expected[i] = before - 1;
					if (!SpoilTopologyExact(frame, expected))
					{
						expected[i] = before;
						if (SpoilTopologyExact(frame, expected))
						{
							if (!PublishSpoilCounters(frame, Lost)) Lost = 0;
						}
						else Lost = 0;
						return false;
					}
					Lost++;
					remaining--;
				}
			}
			if (!PublishSpoilCounters(frame, Lost))
			{
				Lost = 0;
				return false;
			}
			return Lost == Amount;
		}

		private bool PublishSpoilCounters(SpoilFrame Frame, int Lost)
		{
			if (Frame == null || Lost < 0 || Lost > Frame.FoodStored
				|| FoodStored != Frame.FoodStored || FoodCapacity != Frame.FoodCapacity
				|| FoodAbundance != Frame.FoodAbundance) return false;
			if (Lost > 0)
			{
				FoodStored = Frame.FoodStored - Lost;
				FoodAbundance = KingdomRules.ClassifyPantry(FoodStored);
			}
			return true;
		}

		private bool TryCaptureSpoilFrame(GameObject Container, int Amount,
			out SpoilFrame Frame)
		{
			Frame = null;
			Inventory inventory = GameObject.Validate(Container) ? Container.Inventory : null;
			if (inventory == null || inventory.Objects == null || inventory.ParentObject != Container
				|| Container.CurrentZone == null || Container.CurrentCell == null
				|| Container.CurrentCell.ParentZone != Container.CurrentZone || Amount <= 0
				|| FoodStored < Amount || !Larders.Contains(Container)) return false;
			GameObject[] items = inventory.Objects.ToArray();
			int[] counts = new int[items.Length];
			string[] ids = new string[items.Length];
			bool[] edible = new bool[items.Length];
			int available = 0;
			for (int i = 0; i < items.Length; i++)
			{
				GameObject item = items[i];
				if (!GameObject.Validate(item) || item.Physics == null || item.InInventory != Container
					|| item.CurrentCell != null || item.Count <= 0 || string.IsNullOrEmpty(item.ID)) return false;
				for (int j = 0; j < i; j++) if (ReferenceEquals(items[j], item)) return false;
				counts[i] = item.Count;
				ids[i] = item.ID;
				edible[i] = item.HasPart("Food") || item.HasPart("PreparedCookingIngredient");
				if (edible[i])
				{
					long next = (long)available + item.Count;
					available = (next > int.MaxValue) ? int.MaxValue : (int)next;
				}
			}
			if (available < Amount) return false;
			Frame = new SpoilFrame
			{
				Container = Container,
				ContainerId = Container.ID,
				Zone = Container.CurrentZone,
				ZoneId = Container.CurrentZone.ZoneID,
				Cell = Container.CurrentCell,
				Inventory = inventory,
				List = inventory.Objects,
				Items = items,
				ItemIds = ids,
				Counts = counts,
				Edible = edible,
				LarderList = Larders,
				LarderRows = Larders.ToArray(),
				FoodStored = FoodStored,
				FoodCapacity = FoodCapacity,
				FoodAbundance = FoodAbundance
			};
			return true;
		}

		private bool SpoilTopologyExact(SpoilFrame Frame, int[] Expected)
		{
			if (Frame == null || Expected == null || Expected.Length != Frame.Items.Length
				|| !GameObject.Validate(Frame.Container) || Frame.Container.ID != Frame.ContainerId
				|| Frame.Container.CurrentZone != Frame.Zone || Frame.Zone.ZoneID != Frame.ZoneId
				|| Frame.Container.CurrentCell != Frame.Cell
				|| Frame.Cell == null || Frame.Cell.ParentZone != Frame.Zone
				|| !ReferenceEquals(Frame.Container.Inventory, Frame.Inventory)
				|| Frame.Inventory.ParentObject != Frame.Container
				|| !ReferenceEquals(Frame.Inventory.Objects, Frame.List)
				|| !ReferenceEquals(Larders, Frame.LarderList)
				|| Larders.Count != Frame.LarderRows.Length
				|| FoodStored != Frame.FoodStored || FoodCapacity != Frame.FoodCapacity
				|| FoodAbundance != Frame.FoodAbundance) return false;
			for (int i = 0; i < Frame.LarderRows.Length; i++)
				if (!ReferenceEquals(Larders[i], Frame.LarderRows[i])) return false;
			int live = 0;
			for (int i = 0; i < Frame.Items.Length; i++) if (Expected[i] > 0) live++;
			if (Frame.List.Count != live) return false;
			int row = 0;
			for (int i = 0; i < Frame.Items.Length; i++)
			{
				GameObject item = Frame.Items[i];
				if (Expected[i] <= 0)
				{
					if (Frame.List.Contains(item) || GameObject.Validate(item)) return false;
					continue;
				}
				if (!ReferenceEquals(Frame.List[row++], item) || !GameObject.Validate(item)
					|| item.ID != Frame.ItemIds[i] || item.Count != Expected[i]
					|| item.InInventory != Frame.Container || item.CurrentCell != null
					|| (item.HasPart("Food") || item.HasPart("PreparedCookingIngredient")) != Frame.Edible[i])
					return false;
			}
			return true;
		}

		/// <summary>
		/// Water lost out of one damaged store, keeping the survey's counters correct the same way
		/// <see cref="Consume"/> does. Loss and not transfer: this water runs into the ground and
		/// is gone (Addendum 10(b)), which is why it does not go through
		/// <c>KingdomLiquids.PourOnGround</c> the way a manifest's surplus does &mdash; that
		/// surplus is water a founder can still walk up to, and this is not.
		/// </summary>
		/// <param name="Store">The damaged vessel. Must be one of <see cref="Stores"/>.</param>
		/// <param name="Drams">Amount the leak is owed.</param>
		/// <returns>Drams actually lost, measured from the vessel rather than assumed.</returns>
		public int LeakFrom(LiquidVolume Store, int Drams)
		{
			int lost;
			return TryLeakFromExact(Store, Drams, out lost) ? lost : 0;
		}

		/// <summary>Exact callback-safe leak from one dedicated pure-water vessel.</summary>
		public bool TryLeakFromExact(LiquidVolume Store, int Drams, out int Lost)
		{
			Lost = 0;
			GameObject owner = (Store == null) ? null : Store.ParentObject;
			Zone zone = GameObject.Validate(owner) ? owner.CurrentZone : null;
			Cell cell = GameObject.Validate(owner) ? owner.CurrentCell : null;
			string ownerId = GameObject.Validate(owner) ? owner.ID : null;
			string zoneId = (zone == null) ? null : zone.ZoneID;
			Dictionary<string, int> dictionary = (Store == null) ? null : Store.ComponentLiquids;
			Dictionary<string, int> components = (dictionary == null)
				? null : new Dictionary<string, int>(dictionary);
			LiquidVolume[] rows = Stores.ToArray();
			int occurrences = 0;
			for (int i = 0; i < rows.Length; i++) if (ReferenceEquals(rows[i], Store)) occurrences++;
			if (Store == null || Drams <= 0 || occurrences != 1 || !GameObject.Validate(owner)
				|| zone == null || cell == null || cell.ParentZone != zone
				|| owner.GetIntProperty("KingdomStores") != 1 || Store.ParentObject != owner
				|| !ReferenceEquals(owner.GetPart<LiquidVolume>(), Store)
				|| dictionary == null || components == null || Store.MaxVolume < 0
				|| Store.Volume < Drams || !KingdomLiquids.HasFreshWater(Store)
				|| StoredWater < Drams || StorageSpace < 0) return false;
			int before = Store.Volume;
			int max = Store.MaxVolume;
			int oldStored = StoredWater;
			int oldSpace = StorageSpace;
			try
			{
				KingdomLiquids.Drain(Store, Drams);
			}
			catch
			{
				// Exact post-state below is authoritative even when a refresh callback throws.
			}
			if (!GameObject.Validate(owner) || owner.ID != ownerId || owner.CurrentZone != zone
				|| zone.ZoneID != zoneId || owner.CurrentCell != cell || cell.ParentZone != zone
				|| owner.GetIntProperty("KingdomStores") != 1
				|| Store.ParentObject != owner || !ReferenceEquals(owner.GetPart<LiquidVolume>(), Store)
				|| Store.MaxVolume != max || Store.Volume != before - Drams
				|| !ReferenceEquals(Store.ComponentLiquids, dictionary)
				|| !LeakComponentsExact(Store.ComponentLiquids, components, Store.Volume == 0)
				|| Stores.Count != rows.Length || StoredWater != oldStored || StorageSpace != oldSpace)
				return false;
			for (int i = 0; i < rows.Length; i++) if (!ReferenceEquals(Stores[i], rows[i])) return false;
			int newSpace;
			try { newSpace = checked(oldSpace + Drams); }
			catch (OverflowException) { return false; }
			StoredWater = oldStored - Drams;
			StorageSpace = newSpace;
			Lost = Drams;
			return true;
		}

		private static bool LeakComponentsExact(Dictionary<string, int> Current,
			Dictionary<string, int> Before, bool Empty)
		{
			if (Current == null || Before == null) return false;
			if (Empty) return Current.Count == 0;
			if (Current.Count != Before.Count) return false;
			foreach (KeyValuePair<string, int> pair in Before)
			{
				int value;
				if (!Current.TryGetValue(pair.Key, out value) || value != pair.Value) return false;
			}
			return true;
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
