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
			if (Rows.ContainsKey(Work))
			{
				ObserveChanged(Work);
			}
			else
			{
				Larders.Add(Work);
				FoodCapacity += CapacityOf(Work);
				FoodStored += HeldIn(Work);
				FoodAbundance = KingdomRules.ClassifyPantry(FoodStored);
			}
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
		/// from <c>KingdomData.CropForStyle</c>, so a fungal city's granary fills
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
					if (!TryStoreOneExact(container, Blueprint))
					{
						int partial = Amount - remaining;
						RefreshPhysicalFoodCount();
						FoodAbundance = KingdomRules.ClassifyPantry(FoodStored);
						SynchronizeLarders();
						return partial;
					}
					remaining--;
				}
			}
			int stored = Amount - remaining;
			RefreshPhysicalFoodCount();
			FoodAbundance = KingdomRules.ClassifyPantry(FoodStored);
			SynchronizeLarders();
			return stored;
		}

		/// <summary>
		/// Puts food into one exact dedicated larder and returns the measured inventory delta.
		/// Catch-up prices one container touch as one medium unit, so its runtime may not call the
		/// broad <see cref="StoreFood"/> loop and then pretend only one larder was touched.
		/// A callback exception is judged by the physical before/after count; deferred food is not
		/// harvest loss and this method deliberately does not write the settlement ledger.
		/// </summary>
		public int StoreFoodIn(GameObject Container, int Amount, string Blueprint)
		{
			if (!GameObject.Validate(Container) || Container.Inventory == null
				|| !Larders.Contains(Container) || Amount <= 0 || string.IsNullOrEmpty(Blueprint))
			{
				return 0;
			}
			int before = HeldIn(Container);
			int room = CapacityOf(Container) - before;
			int wanted = (Amount < room) ? Amount : room;
			if (wanted <= 0) return 0;
			int accepted = 0;
			for (int i = 0; i < wanted; i++)
			{
				int heldBefore = HeldIn(Container);
				bool exact = TryStoreOneExact(Container, Blueprint);
				int heldAfter = HeldIn(Container);
				if (heldAfter != heldBefore + 1) break;
				accepted++;
				// A callback may throw after vanilla has already inserted the item. Count that
				// measured landing once, then stop: retrying it would mint a duplicate while
				// continuing past an unproved custody transition would hide the fault.
				if (!exact) break;
			}
			RefreshPhysicalFoodCount();
			FoodAbundance = KingdomRules.ClassifyPantry(FoodStored);
			SynchronizeReceiptObject(Container);
			return accepted;
		}

		private bool TryStoreOneExact(GameObject Container, string Blueprint)
		{
			if (!GameObject.Validate(Container) || Container.Inventory == null) return false;
			GameObject food = null;
			try
			{
				food = GameObject.Create(Blueprint);
				if (!KingdomOrdinaryFoodAuthority.IsEdible(food) || food.Count != 1)
				{
					string cleanupFailure;
					if (GameObject.Validate(food)
						&& KingdomOrdinaryFoodAuthority.TryCustodyAvailable(food,
							out cleanupFailure)) food.Obliterate();
					return false;
				}
				string identity = food.ID;
				if (string.IsNullOrEmpty(identity)) return false;
				Container.Inventory.AddObject(food, null, Silent: true, NoStack: true);
			}
			catch { }
			string failure;
			return GameObject.Validate(food) && food.Count == 1 && food.Blueprint == Blueprint
				&& food.InInventory == Container && food.CurrentCell == null
				&& KingdomOrdinaryFoodAuthority.IsEdible(food)
				&& KingdomOrdinaryFoodAuthority.TryCustodyAvailable(food, out failure);
		}

		private void RefreshPhysicalFoodCount()
		{
			long physical = 0L;
			for (int i = 0; i < Larders.Count; i++) physical += HeldIn(Larders[i]);
			FoodStored = physical > int.MaxValue ? int.MaxValue : (int)physical;
		}

		/// <summary>
		/// Frozen source-compatibility projection for the retired passive spoilage mechanic.
		/// Always returns zero and never inspects or mutates the container.
		/// </summary>
		[Obsolete("Passive food spoilage is retired; use an explicit food transaction instead.", false)]
		public int SpoilFrom(GameObject Container, int Amount)
		{
			return 0;
		}

	}
}
