namespace ThousandAndFirst
{
	public static partial class KingdomRules
	{
		// --- The favoured dish, and what a day's eating actually was ------------------------
		//
		// Addendum 11(b) asks that food be "consumed for favored meals or recipes by the
		// residents", not spent as an abstract ration tick, and VANILLA-PRODUCTION-TRUTH 2.3
		// found the exact vanilla home for that: a faction declares
		// <waterritual Recipe="X" RecipeText="..." RecipeGenotype="..."/>, parsed into
		// Faction.WaterRitualRecipe / ...Text / ...Genotype (D/XRL/World/Faction.cs:72-76) and
		// SERIALISED ON THE FACTION ITSELF (:286-288 write, :362 read). A runtime faction - which
		// is what Core/KingdomFounding.cs mints - therefore carries its own favourite dish across
		// save and load with no persistence of ours at all. Eight vanilla factions ship one; the
		// realm makes nine.
		//
		// Nothing in this block is invented vocabulary. The dish's FORM is borrowed from the
		// creed's own vanilla dish; the dish's BODY is the crop the founding ground grows; the
		// ritual line is vanilla's own sentence with the realm's name in it.

		/// <summary>
		/// The one type name every settlement's favoured dish resolves under.
		/// <para>
		/// Both vanilla consumers reach a recipe the same way &mdash;
		/// <c>Activator.CreateInstance(ModManager.ResolveType("XRL.World.Skills.Cooking." + name))</c>
		/// (<c>D/…/Campfire.cs:203-214</c> for <c>PresetMeals</c>,
		/// <c>D/…/WaterRitualCookingRecipe.cs:29-31</c> for the faction recipe) &mdash; so a dish
		/// of our own is a <c>CookingRecipe</c> subclass shipped in that namespace and named here.
		/// ONE class serves every realm: what differs between settlements is the dish's name and
		/// its ingredients, and those are data on the settlement rather than a class apiece.
		/// </para>
		/// </summary>
		public const string DishRecipeType = "r_KingdomFavoredDish";

		/// <summary>The form a settlement takes its dish in when its people hold with nobody in
		/// particular. Plain, and one of vanilla's own tile words.</summary>
		public const string DefaultDishForm = "stew";

		/// <summary>
		/// A settlement's favourite dish: a name, the sentence a stranger asks for it with, and
		/// the two things it is actually made of. Derived, never authored &mdash; see
		/// <see cref="DeriveDish"/>.
		/// </summary>
		public struct FavoredDish
		{
			/// <summary>What the dish is called, lower case, Qud's own register: "vinewafer
			/// matz", "starapple porridge". Empty for a realm with no ground to derive from.</summary>
			public string Name;

			/// <summary>The shape the food is made in, from <see cref="DishFormFor"/>. One of
			/// <c>CookingRecipe.ingredientTileTypes</c>, so vanilla's own recipe-tile generator
			/// finds a picture for it.</summary>
			public string Form;

			/// <summary>The line a stranger asks for the recipe with at the water ritual, in
			/// vanilla's own phrasing. Goes straight onto <c>Faction.WaterRitualRecipeText</c>.</summary>
			public string Text;

			/// <summary>The crop the settlement grows, raw. The dish's second component.</summary>
			public string Crop;

			/// <summary>The preserved staple the mill makes out of that crop, and the dish's
			/// first component. This is the link that makes the whole chain one thing: the fields
			/// grow the crop, the mill binds it into the staple, and the staple is what the
			/// settlement's own dish is made of.</summary>
			public string Staple;

			/// <summary>The creed dish this one's form was borrowed from, or empty. Kept so a
			/// later pass can tell whether the people who live here have changed their minds
			/// without re-deriving to find out.</summary>
			public string Source;
		}

		/// <summary>
		/// The FORM a dish takes: the shape the people who settled here make their food in.
		/// Derived from the creed's OWN favourite dish, of which vanilla ships eight
		/// (<c>B/Factions.xml:154,187,219,1087,1117,1179,1777,1814</c>) &mdash; so a settlement
		/// whose people mostly hold with Joppa binds its harvest into matz, and one that holds
		/// with the Barathrumites boils it into porridge.
		/// <para>
		/// Every form word returned here is one of <c>CookingRecipe.ingredientTileTypes</c>
		/// (<c>D/XRL/World/Skills/Cooking/CookingRecipe.cs:44-51</c>), which is what lets
		/// vanilla's own recipe-tile generator find a picture for a dish this mod invented. A
		/// creed with no dish of its own, or none at all, gets <see cref="DefaultDishForm"/>:
		/// people who hold with nobody still eat, and what they eat is a stew.
		/// </para>
		/// </summary>
		/// <param name="CreedRecipe">The creed faction's own <c>WaterRitualRecipe</c>, read off
		/// the engine by the caller. Null or unknown is not an error.</param>
		public static string DishFormFor(string CreedRecipe)
		{
			switch (CreedRecipe)
			{
			case "AppleMatz":
				return "matz";
			case "MushroomCider":
				return "compote";
			case "GoatAndSweetLeaf":
				return "roast";
			case "TongueAndCheek":
				return "brisket";
			case "BoneBabka":
				return "pastry";
			case "HotandSpiny":
				return "goulash";
			case "MahLahSoup":
				return "soup";
			case "ThePorridge":
				return "porridge";
			default:
				return DefaultDishForm;
			}
		}

		/// <summary>
		/// What the settlement's own crop is called when it is an ingredient rather than a plant.
		/// The dish's body: the ground a realm was founded on is what its dish is made of.
		/// </summary>
		/// <param name="Crop">A crop blueprint, from the merged <c>KingdomData</c> style row.
		/// An unknown blueprint is named as itself, lower case, so a third party's crop still
		/// makes a readable dish.</param>
		public static string CropWordFor(string Crop)
		{
			switch (Crop)
			{
			case "Vinewafer":
				return "vinewafer";
			case "Starapple":
				return "starapple";
			case "Plump Mushroom":
				return "mushroom";
			case "Godshroom Cap":
				return "godshroom";
			case "Bundle of Noisegrass":
				return "noisegrass";
			case "Dreadroot Tuber":
				return "dreadroot";
			default:
				return string.IsNullOrEmpty(Crop) ? "" : Crop.ToLowerInvariant();
			}
		}

		/// <summary>
		/// What a crop becomes when it is bound to keep: the preserved staple the grinding mill
		/// makes out of it, and the first component of the settlement's own dish.
		/// <para>
		/// Four of the five terrain crops are vanilla's own answer, read straight off the crop's
		/// <c>PreservableItem Result</c> (<c>B/ObjectBlueprints/Foods.xml:424,441,457,598</c>).
		/// <c>Dreadroot Tuber</c> and the optional <c>Godshroom Cap</c> cult crop declare no
		/// <c>PreservableItem</c> at all, so vanilla's mill can do nothing with them; those two
		/// are Addendum 11(c)'s third clause, filled in in vanilla's idiom by inheriting the
		/// nearest shipped preserve of the same family (<c>ObjectBlueprints.xml</c>).
		/// </para>
		/// <para>
		/// Null for a crop this build has no staple for. The engine-side caller falls back to the
		/// crop's own <c>PreservableItem.Result</c>, so a third party's crop that declares one
		/// still mills.
		/// </para>
		/// </summary>
		public static string PreservedStapleFor(string Crop)
		{
			switch (Crop)
			{
			case "Vinewafer":
				return "Vinewafer Sheaf";
			case "Starapple":
				return "Starapple Preserves";
			case "Plump Mushroom":
				return "Pickled Mushrooms";
			case "Godshroom Cap":
				return "r_KingdomGodshroomPickle";
			case "Bundle of Noisegrass":
				return "Wild Rice";
			case "Dreadroot Tuber":
				return "r_KingdomDreadrootMash";
			default:
				return null;
			}
		}

		/// <summary>
		/// A realm's own possessive, without the engine's <c>Grammar</c> &mdash; this file is
		/// engine-free so the rules can be tested. Ptohs' rather than Ptohs's, the way Qud writes
		/// it.
		/// </summary>
		public static string Possessive(string Name)
		{
			if (string.IsNullOrEmpty(Name))
			{
				return "";
			}
			char last = Name[Name.Length - 1];
			return (last == 's' || last == 'S') ? (Name + "'") : (Name + "'s");
		}

		/// <summary>
		/// Derives the settlement's favourite dish from the people in it and the ground under it.
		/// Deterministic and total: the same realm, creed and crop always give the same dish, and
		/// no input is an error.
		/// <para>
		/// <b>Creed picks the form, ground picks the body.</b> A realm whose people mostly hold
		/// with Joppa makes matz; a realm founded in a marsh makes it out of vinewafers; put the
		/// two together and the settlement's dish is vinewafer matz. Nobody in particular, and it
		/// is a stew &mdash; which is the honest answer, not a fallback.
		/// </para>
		/// </summary>
		/// <param name="Realm">The realm's display name, for the ritual line.</param>
		/// <param name="CreedRecipe">The dominant creed faction's own <c>WaterRitualRecipe</c>,
		/// or null for a realm of mixed people.</param>
		/// <param name="Crop">The crop the founding ground grows, from
		/// <c>KingdomData.CropForStyle</c>.</param>
		public static FavoredDish DeriveDish(string Realm, string CreedRecipe, string Crop)
		{
			FavoredDish dish = default(FavoredDish);
			dish.Form = DishFormFor(CreedRecipe);
			dish.Source = string.IsNullOrEmpty(CreedRecipe) ? "" : CreedRecipe;
			dish.Crop = Crop;
			dish.Staple = PreservedStapleFor(Crop);
			string body = CropWordFor(Crop);
			dish.Name = string.IsNullOrEmpty(body) ? dish.Form : (body + " " + dish.Form);
			// Vanilla's own sentence, with the realm where the faction's name goes: compare
			// "Would you teach me to cook the Barathrumites' favorite dish?" (B/Factions.xml:1179).
			// American spelling on purpose - it is the spelling every other one of these lines in
			// the game uses, and this one sits in the same menu as them.
			dish.Text = "Would you teach me to cook " + Possessive(string.IsNullOrEmpty(Realm) ? "this settlement" : Realm) + " favorite dish?";
			return dish;
		}

	}
}
