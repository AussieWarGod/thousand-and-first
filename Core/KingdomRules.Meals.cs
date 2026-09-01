namespace ThousandAndFirst
{
	public static partial class KingdomRules
	{
		// --- What one explicit shared meal was ---------------------------------------------

		/// <summary>
		/// What an attended, player-authorized shared-meal transaction cooked. Ordinals remain
		/// stable because old saves and lifecycle receipts carry this enum.
		/// </summary>
		public enum MealVerdict
		{
			/// <summary>No complete meal transaction occurred.</summary>
			None = 0,

			/// <summary>Legacy value from passive ration builds. Never produced by new code.</summary>
			Scraps = 1,

			/// <summary>A working kitchen cooked a complete meal from physical ingredients.</summary>
			Plain = 2,

			/// <summary>A working kitchen cooked the complete meal from its named recipe staple.</summary>
			Favored = 3
		}

		/// <summary>
		/// Share of an explicit meal's disclosed ingredient cost that must come from the named
		/// staple to call it the settlement's favored dish. Exact means the entire cost.
		/// </summary>
		public const int FavoredMealPercent = 100;

		/// <summary>
		/// Legacy API constant retained for source compatibility. Empty pantries now withhold the
		/// optional meal and report availability without applying a penalty.
		/// </summary>
		public const GrowthStage ScrapsSpokenFrom = GrowthStage.Village;

		/// <summary>
		/// Judges one explicit meal debit. Incomplete payment or a missing kitchen means no act:
		/// callers must not grant civic effects for a transaction that did not complete.
		/// </summary>
		/// <param name="Owed">Exact, disclosed ingredient cost for this meal.</param>
		/// <param name="FromDish">Units drawn from the named staple.</param>
		/// <param name="FromStores">Physical ingredient units debited in total.</param>
		/// <param name="HasKitchen">Whether one exact designation currently credits a live
		/// <c>taf:cooking</c> provider. A settlement with nowhere working to cook cannot cook,
		/// however full its larder is.</param>
		/// <param name="Stage">Legacy API parameter; settlement stage does not alter a recipe.</param>
		public static MealVerdict JudgeMeal(int Owed, int FromDish, int FromStores, bool HasKitchen, GrowthStage Stage)
		{
			if (Owed <= 0 || !HasKitchen || FromStores < Owed)
			{
				return MealVerdict.None;
			}
			if (FromDish >= Owed)
			{
				return MealVerdict.Favored;
			}
			return MealVerdict.Plain;
		}

		/// <summary>
		/// Legacy capacity value. Earlier builds made a favored ration worth one resident for one
		/// day, which could turn its expiry into food-caused subsidence or departure.
		/// <para>
		/// New shared meals instead grant bounded, explicit creed-easing and cohabitation progress
		/// in <c>KingdomLarder</c>. This projection therefore remains zero for every verdict.
		/// </para>
		/// </summary>
		public const int FavoredMealShade = 0;

		/// <summary>Neutral compatibility projection. Meals never alter population capacity.</summary>
		public static int MealShadeFor(MealVerdict Verdict)
		{
			return 0;
		}

		/// <summary>
		/// The ledger line for a completed meal cooked wholly from its named staple.
		/// </summary>
		/// <param name="Settlement">The settlement's display name.</param>
		/// <param name="Dish">The dish's name, from <see cref="FavoredDish.Name"/>.</param>
		/// <returns>Null when there is no dish to name, which is a sentence not worth writing.</returns>
		public static string FavoredMealNote(string Settlement, string Dish)
		{
			if (string.IsNullOrEmpty(Dish))
			{
				return null;
			}
			return "{{G|There was " + Dish + " on the table at " + Settlement + ", cooked from its own staple.}}";
		}

		/// <summary>
		/// Legacy helper retained for API compatibility. It reports a withheld optional act and
		/// explicitly confirms that no stock or settlement state was lost.
		/// </summary>
		public static string ScrapsNote(string Settlement)
		{
			return "{{K|No shared meal was available at " + Settlement + "; nothing was spent.}}";
		}

		/// <summary>
		/// One inspectable line joining the named dish to physical ingredients and an operational
		/// cooking provider. This reports optional action availability, never passive upkeep.
		/// </summary>
		public static string DishStatusLine(string Dish, string Staple, int StapleStored,
			int Kitchens, MealVerdict LastMeal)
		{
			if (string.IsNullOrEmpty(Dish))
			{
				return null;
			}
			if (StapleStored < 0)
			{
				StapleStored = 0;
			}
			string staple = string.IsNullOrEmpty(Staple) ? "staple not yet named" : Staple;
			string kitchen = (Kitchens > 0) ? "kitchen ready" : "{{r|no capable kitchen}}";
			string outcome;
			switch (LastMeal)
			{
			case MealVerdict.Favored:
				outcome = "{{G|Last shared meal: favorite dish.}}";
				break;
			case MealVerdict.Scraps:
				outcome = "No shared meal has completed yet.";
				break;
			case MealVerdict.Plain:
				outcome = "Last shared meal: other ingredients.";
				break;
			default:
				outcome = "No shared meal has completed yet.";
				break;
			}
			return "Dish: " + Dish + " — " + staple + ": " + StapleStored + " stored; "
				+ kitchen + ". A shared meal needs real ingredients and this cooking provider; "
				+ "the favorite dish needs its full disclosed cost from that staple. "
				+ outcome;
		}

	}
}
