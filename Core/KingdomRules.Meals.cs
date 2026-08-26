namespace ThousandAndFirst
{
	public static partial class KingdomRules
	{
		// --- What a day's eating was, and what it was worth --------------------------------

		/// <summary>
		/// What the settlement actually ate on a day the ration bill came due. A rendering of the
		/// draw rather than a second draw: the servings are the same servings either way, and only
		/// <see cref="MealVerdict.Favored"/> is worth anything on top (<see cref="MealShadeFor"/>).
		/// </summary>
		public enum MealVerdict
		{
			/// <summary>No bill came due, so nothing was eaten and nothing is said.</summary>
			None = 0,

			/// <summary>The larders gave nothing and the settlement lived off the ridge. Said
			/// once, and only by a settlement big enough that it should not have to.</summary>
			Scraps = 1,

			/// <summary>The settlement ate what it had. The ordinary day, and worth no
			/// sentence.</summary>
			Plain = 2,

			/// <summary>A kitchen stood, the staple was on the table, and the settlement ate its
			/// own dish.</summary>
			Favored = 3
		}

		/// <summary>
		/// Share of a day's ration bill that must come off the dish's own staple before the
		/// settlement can be said to have eaten its dish. Half: a table that is mostly the
		/// settlement's own cooking is the settlement's own cooking.
		/// </summary>
		public const int FavoredMealPercent = 50;

		/// <summary>
		/// The stage at and above which a settlement drawing nothing from its own larders is
		/// worth saying out loud. Below it, living off the land is what a camp IS
		/// (<see cref="ForagedRations"/>), and 7b must not nag a founder about a system working.
		/// </summary>
		public const GrowthStage ScrapsSpokenFrom = GrowthStage.Village;

		/// <summary>
		/// What a day's eating was. Pure, and the only place the four numbers are read together.
		/// </summary>
		/// <param name="Owed">The day's ration bill, from <see cref="RationsForElapsed"/>.</param>
		/// <param name="FromDish">Servings drawn off the dish's own staple.</param>
		/// <param name="FromStores">Servings drawn out of the larders in total, staple
		/// included.</param>
		/// <param name="HasKitchen">Whether a finished work carrying vanilla's <c>Campfire</c>
		/// stands here. A settlement with nowhere to cook cannot cook, however full its larder
		/// is &mdash; and the communal fire is already a real cooking site, which is the whole of
		/// what Addendum 11(c) says about it.</param>
		/// <param name="Stage">What the settlement is, for <see cref="ScrapsSpokenFrom"/>.</param>
		public static MealVerdict JudgeMeal(int Owed, int FromDish, int FromStores, bool HasKitchen, GrowthStage Stage)
		{
			if (Owed <= 0)
			{
				return MealVerdict.None;
			}
			if (HasKitchen && FromDish > 0 && FromDish * 100 / Owed >= FavoredMealPercent)
			{
				return MealVerdict.Favored;
			}
			if (FromStores <= 0 && Stage >= ScrapsSpokenFrom)
			{
				return MealVerdict.Scraps;
			}
			return MealVerdict.Plain;
		}

		/// <summary>
		/// What a settlement eating its own dish is worth to the level, for exactly one day.
		/// <para>
		/// <b>One, and one day, and both are vanilla's numbers.</b> A cooked meal applies one
		/// <c>ProceduralCookingEffect</c> at a time (<c>D/…/Campfire.cs:740,1005,1218</c>) and a
		/// NON-PLAYER eater's meal effect expires on a real timer at <c>StartTick + 1200</c> ticks
		/// (<c>D/…/ProceduralCookingEffect.cs:212-223</c>), which is exactly
		/// <see cref="TicksPerDay"/>. So the settlement's well-fed day is one meal, worth one more
		/// settler, held until tomorrow's meal re-earns it.
		/// </para>
		/// <para>
		/// It rides the same lift term as a notable's shade and a shrine's spirit, so
		/// <c>KingdomCatalogueRules.LiftCapPercent</c> binds it again on top of this: a camp at the
		/// floor cannot dine its way past five people, and no settlement can ever eat its way past
		/// its own water.
		/// </para>
		/// </summary>
		public const int FavoredMealShade = 1;

		/// <summary>The lift a day's eating leaves behind. Only a favoured meal is worth
		/// anything; nothing here is ever a penalty.</summary>
		public static int MealShadeFor(MealVerdict Verdict)
		{
			return (Verdict == MealVerdict.Favored) ? FavoredMealShade : 0;
		}

		/// <summary>
		/// The one line a settlement that ate its own dish gets, for the ledger. Named, because a
		/// lift nobody can see is a modifier and not a meal (STANDARDS 7b's posture applied to a
		/// number that helps).
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
			return "{{G|There was " + Dish + " on the table at " + Settlement + ", and the day went better for it.}}";
		}

		/// <summary>
		/// The one line a settlement whose larders gave nothing gets, said once
		/// (STANDARDS 7b) and unsaid the moment it eats out of its own stores again.
		/// </summary>
		public static string ScrapsNote(string Settlement)
		{
			return "{{r|Nothing came out of the larders at " + Settlement + ". The settlement ate what it could find, and it noticed.}}";
		}

		/// <summary>
		/// One inspectable line joining the realm's named dish to the physical chain that earns its
		/// one-day lift. This reports a snapshot; it does not predict tomorrow's full ration draw.
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
			string kitchen = (Kitchens > 0) ? "kitchen ready" : "{{r|no kitchen}}";
			string outcome;
			switch (LastMeal)
			{
			case MealVerdict.Favored:
				outcome = "{{G|Last ration: favorite dish; carries +1 today.}}";
				break;
			case MealVerdict.Scraps:
				outcome = "{{r|Last ration: scraps; no dish bonus.}}";
				break;
			case MealVerdict.Plain:
				outcome = "Last ration: ordinary; no dish bonus.";
				break;
			default:
				outcome = "No ration day has resolved yet.";
				break;
			}
			return "Dish: " + Dish + " — " + staple + ": " + StapleStored + " stored; "
				+ kitchen + ". Its +1 day needs a kitchen and at least half the ration from that staple. "
				+ outcome;
		}

	}
}
