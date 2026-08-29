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
			RefreshPhysicalFoodCount();
			FoodAbundance = KingdomRules.ClassifyPantry(FoodStored);
			SynchronizeLarders();
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
			KingdomConstructionInputLeaseSnapshot leases;
			string authorityFailure;
			if (Remaining <= 0 || !KingdomOrdinaryFoodAuthority.TryCapture(
				out leases, out authorityFailure)) return 0;
			for (int i = 0; i < Larders.Count && Remaining > 0; i++)
			{
				GameObject container = Larders[i];
				if (!GameObject.Validate(container) || container.Inventory == null)
				{
					continue;
				}
				// Snapshot first: destroying a food item below removes it from this same
				// Inventory list, and mutating a collection mid-foreach throws.
				List<GameObject> held = new List<GameObject>(container.Inventory.Objects);
				for (int j = 0; j < held.Count && Remaining > 0; j++)
				{
					GameObject food = held[j];
					if (!ReferenceEquals(food == null ? null : food.InInventory, container)
						|| !KingdomOrdinaryFoodAuthority.CanSpend(leases, food))
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
						int before = food.Count;
						string beforeBlueprint = food.Blueprint;
						Inventory beforeInventory = container.Inventory;
						Zone beforeZone = container.CurrentZone;
						Cell beforeCell = container.CurrentCell;
						string failure;
						if (before <= 0 || beforeInventory == null || beforeZone != Ground
							|| beforeCell == null || food.InInventory != container
							|| (Blueprint != null && beforeBlueprint != Blueprint)
							|| !KingdomOrdinaryFoodAuthority.TrySpendNow(food, out failure)) return took;
						try { food.Destroy(null, Silent: true); }
						catch { }
						bool ownerExact = GameObject.Validate(container)
							&& container.Inventory == beforeInventory
							&& container.CurrentZone == beforeZone && container.CurrentCell == beforeCell;
						bool exact = ownerExact && (before == 1 ? !GameObject.Validate(food)
							: GameObject.Validate(food) && food.InInventory == container
								&& food.CurrentCell == null && food.Count == before - 1
								&& food.Blueprint == beforeBlueprint
								&& KingdomOrdinaryFoodAuthority.IsEdible(food));
						if (!exact) return took;
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
			RefreshPhysicalFoodCount();
			FoodAbundance = KingdomRules.ClassifyPantry(FoodStored);
			SynchronizeLarders();
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

		/// <summary>Current edible units of one exact blueprint across this survey's dedicated
		/// larders. Used only for inspection; the ration draw remains authoritative.</summary>
		public int CountFood(string Blueprint)
		{
			if (string.IsNullOrEmpty(Blueprint))
			{
				return 0;
			}
			KingdomConstructionInputLeaseSnapshot leases;
			string failure;
			if (!KingdomOrdinaryFoodAuthority.TryCapture(out leases, out failure)) return 0;
			int held = 0;
			for (int i = 0; i < Larders.Count; i++)
			{
				GameObject container = Larders[i];
				if (container == null || container.Inventory == null)
				{
					continue;
				}
				foreach (GameObject item in container.Inventory.Objects)
				{
					if (!GameObject.Validate(item) || item.Blueprint != Blueprint
						|| item.InInventory != container
						|| !KingdomOrdinaryFoodAuthority.CanSpend(leases, item))
					{
						continue;
					}
					if (held > int.MaxValue - item.Count)
					{
						return int.MaxValue;
					}
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

	}
}
