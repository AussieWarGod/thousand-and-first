using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPurpose
	{
		/// <summary>Servings in the destination's dedicated larders proved to belong to this exact
		/// operation, with the unmarked remainder counted alongside so the caller can prove the two
		/// partitions still sum to the figure every other food surface reads. Ownership is decided
		/// on the full canonical receipt, so a serving whose index collides with ours is never
		/// counted as ours. A serving wearing our mark is only counted when it is still one exact,
		/// whole, takeable unit of the settlement's own staple; anything else, and any landing mark
		/// this operation cannot claim at all, is reported inexact. That is ambiguity, not a
		/// receipt.</summary>
		private static int MarkedPurposeFood(List<GameObject> Larders, string Receipt, int Prefilter,
			string Blueprint, out int Unmarked, out bool Exact)
		{
			Unmarked = 0;
			Exact = true;
			int marked = 0;
			List<GameObject> larders = Larders;
			for (int i = 0; i < larders.Count; i++)
			{
				// The roster is immutable and every entry is visited: an entry that stopped being
				// a usable larder is inexactness, never a row to skip.
				if (larders[i].Inventory == null)
				{
					Exact = false;
					continue;
				}
				List<GameObject> items = larders[i].Inventory.GetObjects();
				for (int j = 0; items != null && j < items.Count; j++)
				{
					GameObject item = items[j];
					if (!GameObject.Validate(item)) continue;
					// Presence is the property existing: an emptied stamp or a zeroed index is a
					// torn mark, and reading either as absence would turn it into ordinary food.
					if (KingdomPurposePortfolioRules.LandingMarkerIsOurs(Receipt, Prefilter,
						OwnedIntField(item, PortfolioLandedFoodProperty),
						item.GetIntProperty(PortfolioLandedFoodProperty),
						OwnedStringField(item, PortfolioLandedReceiptProperty),
						item.GetStringProperty(PortfolioLandedReceiptProperty)))
					{
						if (ExactLandedServing(item, larders[i], Blueprint)) marked += 1;
						else Exact = false;
					}
					else if (KingdomPurposePortfolioRules.LandingMarkerIsPresent(
						OwnedFieldPresent(item, PortfolioLandedFoodProperty),
						OwnedFieldPresent(item, PortfolioLandedReceiptProperty))) Exact = false;
					else if (item.HasPart("Food")
						|| item.HasPart("PreparedCookingIngredient")) Unmarked += item.Count;
				}
			}
			return marked;
		}

		/// <summary>One landed serving as this lane creates them: a single whole unit of the exact
		/// staple, food, stock-marked, takeable, and inside the exact larder it was placed in.
		/// Counted as one rather than by <c>Count</c>, because a marked unit that grew is not a
		/// serving this operation landed.</summary>
		private static bool ExactLandedServing(GameObject Item, GameObject Larder, string Blueprint)
		{
			return GameObject.Validate(Item) && !Item.IsInvalid() && !Item.IsInGraveyard()
				&& Item.Count == 1 && Item.Blueprint == Blueprint
				&& (Item.HasPart("Food") || Item.HasPart("PreparedCookingIngredient"))
				&& Item.GetIntProperty(Simulation.City.KingdomPorters.StockProperty) == 1
				&& Item.Physics != null && Item.Physics.Takeable
				&& ReferenceEquals(Item.InInventory, Larder) && Item.CurrentCell == null;
		}

		/// <summary>Creates and marks the outstanding servings across the destination's dedicated
		/// larders and reports the measured delta rather than the amount asked for. No exception
		/// crosses this boundary: a throw is one more aftermath class, classified like the rest, so
		/// the caller can always tell a clean shortfall from a stamped serving loose outside the
		/// larders it measures. Every offer is witnessed on the durable cargo before the engine
		/// sees it and reconciled step by step, so a callback that destroys the serving still
		/// leaves the proof that it happened.</summary>
		private static int AddPurposeFood(KingdomSurvey Survey, List<GameObject> Larders,
			GameObject Cargo, string Receipt, int Prefilter, int Amount, string Blueprint,
			out KingdomPurposeServingAftermath Aftermath)
		{
			Aftermath = KingdomPurposeServingAftermath.Settled;
			int before = MarkedPurposeFood(Larders, Receipt, Prefilter, Blueprint,
				out int unmarked, out _);
			List<GameObject> larders = Larders;
			int settled = 0;
			int held = before;
			int remaining = Amount;
			for (int i = 0; i < larders.Count && remaining > 0
				&& Aftermath == KingdomPurposeServingAftermath.Settled; i++)
			{
				GameObject larder = larders[i];
				// Room is measured before every offer, never snapshotted: a callback can shrink a
				// larder's capacity under the servings already in it, and the next offer of a
				// stale count would be placing provision the destination cannot keep.
				while (remaining > 0 && Aftermath == KingdomPurposeServingAftermath.Settled
					&& KingdomSurvey.HeldIn(larder) < KingdomSurvey.CapacityOf(larder))
				{
					int expected = held + 1;
					if (!StampPurposeLandingAttempt(Cargo, Receipt, expected))
					{
						Aftermath = KingdomPurposeServingAftermath.Stranded;
						continue;
					}
					Aftermath = PlacePurposeServing(Survey, larder, Blueprint, Receipt, Prefilter);
					// Nothing was ever offered under this witness, so it names no aftermath and is
					// retired; the shortfall is reported as itself.
					if (Aftermath == KingdomPurposeServingAftermath.Unavailable)
					{
						if (!TryClearPurposeLandingAttempt(Cargo, Receipt, expected))
							Aftermath = KingdomPurposeServingAftermath.Stranded;
						continue;
					}
					int step = MarkedPurposeFood(Larders, Receipt, Prefilter, Blueprint, out _,
						out bool stepExact);
					// The witness is retired only where this one offer is reproved settled, exact,
					// and worth precisely the increment it promised. Anything else leaves it
					// standing, so a refused quarantine publication cannot be forgotten by the next
					// pass and answered with a fresh serving.
					if (Aftermath != KingdomPurposeServingAftermath.Settled || !stepExact
						|| step != expected || !PurposeLardersWithinCapacity(Larders)
						|| !TryClearPurposeLandingAttempt(Cargo, Receipt, expected))
					{
						Aftermath = KingdomPurposeServingAftermath.Stranded;
						continue;
					}
					held = step;
					settled++;
					remaining--;
				}
			}
			int after = MarkedPurposeFood(Larders, Receipt, Prefilter, Blueprint,
				out int unmarkedAfter, out bool exact);
			// Both halves of the partition are remeasured, not just the marked delta. Every
			// precondition the engine could refuse on was proved before the offer, so a settled
			// offer owes an exact increment: a short delta means a callback moved an earlier mark
			// while the latest settled, and retrying over it would mint around servings that exist.
			if (!KingdomPurposePortfolioRules.LandingPartitionIsExact(before, unmarked, settled,
				after, unmarkedAfter, exact))
				Aftermath = KingdomPurposeServingAftermath.Stranded;
			int added = after - before;
			if (added > 0)
			{
				Survey.FoodStored += added;
				Survey.FoodAbundance = KingdomRules.ClassifyPantry(Survey.FoodStored);
				// A larder the survey will not resynchronise is one it no longer indexes, which is
				// divergence between the ground and every food figure read from it. That is
				// ambiguity, never a result to discard.
				for (int i = 0; i < larders.Count; i++)
					if (!Survey.SynchronizeReceiptObject(larders[i]))
						Aftermath = KingdomPurposeServingAftermath.Stranded;
			}
			return added > 0 ? added : 0;
		}

		/// <summary>Offers one exact serving to one exact larder and classifies what became of it.
		/// The engine's return value alone proves nothing: <c>Inventory.AddObject</c> hands back the
		/// same reference even when it refused the object outright
		/// (<c>XRL/World/Parts/Inventory.cs:258-277</c>), and its post-placement callbacks
		/// (<c>Inventory.cs:300,305</c>) may move, merge, mutate, obliterate, or throw after the
		/// object is already in the list. Only the physical aftermath decides.</summary>
		private static KingdomPurposeServingAftermath PlacePurposeServing(KingdomSurvey Survey,
			GameObject Larder, string Blueprint, string Receipt, int Prefilter)
		{
			GameObject food = ExactPurposeServing(Blueprint, Receipt, Prefilter);
			if (food == null) return KingdomPurposePortfolioRules.ClassifyServingAftermath(false,
				false, false, false, false, false, false, false);
			GameObject accepted = null;
			bool threw = false;
			try
			{
				accepted = Larder.Inventory.AddObject(food, null, Silent: true, NoStack: true);
			}
			catch { threw = true; }
			KingdomSurvey.ObserveAddResultInActive(Survey.Ground, food, accepted);
			KingdomPurposeServingAftermath aftermath =
				KingdomPurposePortfolioRules.ClassifyServingAftermath(true, threw,
					ReferenceEquals(accepted, food), GameObject.Validate(food)
						&& !food.IsInvalid() && !food.IsInGraveyard(),
					ReferenceEquals(food.InInventory, Larder) && food.CurrentCell == null,
					food.Count == 1, food.Blueprint == Blueprint
						&& (food.HasPart("Food") || food.HasPart("PreparedCookingIngredient"))
						&& food.Physics != null && food.Physics.Takeable
						&& food.GetIntProperty(Simulation.City.KingdomPorters.StockProperty) == 1,
					KingdomPurposePortfolioRules.LandingMarkerIsOurs(Receipt, Prefilter,
						OwnedIntField(food, PortfolioLandedFoodProperty),
						food.GetIntProperty(PortfolioLandedFoodProperty),
						OwnedStringField(food, PortfolioLandedReceiptProperty),
						food.GetStringProperty(PortfolioLandedReceiptProperty)));
			// Only a serving that reached no owner at all may be withdrawn; one the engine placed
			// somewhere else is evidence, and destroying it would erase the ambiguity it proves.
			if (aftermath != KingdomPurposeServingAftermath.Settled
				&& GameObject.Validate(food) && food.InInventory == null
				&& food.CurrentCell == null) food.Obliterate();
			return aftermath;
		}

		/// <summary>One exact, unstackable, fully marked serving, or null when this staple cannot
		/// become one. Every precondition the engine would refuse on is proved here instead, so a
		/// refusal never leaves a stamped object behind: <c>Inventory.AddObject</c> returns the
		/// object un-added for an untakeable, graveyard, or invalid object
		/// (<c>XRL/World/Parts/Inventory.cs:258-277</c>). <c>Stacker</c> is removed and
		/// <c>NoStack</c> is passed at the add because a stackable serving is merged into a
		/// neighbour and then obliterated by <c>AddedToInventoryEvent</c>
		/// (<c>XRL/World/Parts/Stacker.cs:137-144</c> to <c>:312-315</c>), which would carry the
		/// count away from this operation's marker and let a retry mint replacements for servings
		/// that are physically present.</summary>
		private static GameObject ExactPurposeServing(string Blueprint, string Receipt,
			int Prefilter)
		{
			GameObject food;
			try { food = GameObject.Create(Blueprint); }
			catch { return null; }
			if (!GameObject.Validate(food)) return null;
			try { food.RemovePart("Stacker"); }
			catch
			{
				food.Obliterate();
				return null;
			}
			if (food.Count != 1 || food.IsInvalid() || food.IsInGraveyard()
				|| food.Blueprint != Blueprint || food.Physics == null || !food.Physics.Takeable
				|| (!food.HasPart("Food") && !food.HasPart("PreparedCookingIngredient")))
			{
				food.Obliterate();
				return null;
			}
			food.SetIntProperty(Simulation.City.KingdomPorters.StockProperty, 1);
			// Full identity is stamped before the cheap index, and both before the serving is ever
			// offered to an inventory, so no half-marked serving can exist inside a larder.
			food.SetStringProperty(PortfolioLandedReceiptProperty, Receipt);
			food.SetIntProperty(PortfolioLandedFoodProperty, Prefilter);
			return food;
		}

		/// <summary>Whether this staple can become an exact serving at all, proved on a disposable
		/// sample and destroyed again. Called before the operation is published so that a staple
		/// which cannot be made exact refuses the operation up front, rather than becoming a
		/// perpetual partial wait after the founder has already committed to it.</summary>
		private static bool PurposeServingIsMakeable(string Blueprint)
		{
			GameObject sample = ExactPurposeServing(Blueprint, "purpose-landing-sample", 1);
			if (sample == null) return false;
			sample.Obliterate();
			return true;
		}


	}
}
