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
				GameObject food = null;
				try
				{
					food = GameObject.Create(Blueprint);
					if (!GameObject.Validate(food)
						|| (!food.HasPart("Food") && !food.HasPart("PreparedCookingIngredient")))
					{
						if (GameObject.Validate(food)) food.Obliterate();
						break;
					}
					Container.Inventory.AddObject(food, Silent: true);
				}
				catch
				{
					// Measured inventory delta below decides whether callback completed.
				}
				int heldAfter = HeldIn(Container);
				if (heldAfter != heldBefore + 1)
				{
					if (GameObject.Validate(food) && food.InInventory != Container) food.Obliterate();
					break;
				}
				accepted++;
			}
			if (accepted > 0)
			{
				FoodStored += accepted;
				FoodAbundance = KingdomRules.ClassifyPantry(FoodStored);
				SynchronizeReceiptObject(Container);
			}
			return accepted;
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

	}
}
