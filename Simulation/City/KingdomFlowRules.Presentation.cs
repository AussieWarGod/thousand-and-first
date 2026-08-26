namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomFlowRules
	{

		/// <summary>
		/// What a founder is told when the lights go down, once.
		/// <para>
		/// STANDARDS 7b: <i>applicable but blocked</i> announces, and announces <b>once</b>. The
		/// latch lives on the thing that went quiet, not on the settlement, so each work remembers
		/// its own telling and a dormant city keeps that memory with no field on the system &mdash;
		/// the idiom <c>r_KingdomPowerWork.DryAnnounced</c> already uses.
		/// </para>
		/// <para>
		/// <b>Recovery says nothing, and that is the rule rather than an omission</b>
		/// (Addendum 12(c), felt-and-announced): the latch is UNSAID when supply returns, so the
		/// next failure can be told again, and no line is written for the good news. A settlement
		/// that announced every recovery would be a settlement that talks about itself constantly,
		/// and 7b's whole complaint is about the founder being unable to find the one line that
		/// mattered.
		/// </para>
		/// </summary>
		/// <param name="workName">What went quiet, in the founder's own words for it.</param>
		internal static string BrownoutNotice(string workName)
		{
			string named = string.IsNullOrEmpty(workName) ? "a work" : workName;
			return "The " + named + " has gone quiet. There is not enough to go round, and it is the first thing this city gives up.";
		}

		/// <summary>The same moment, dated, for the chronicle &mdash; where a founder three zones
		/// away reads it at the homecoming.</summary>
		internal static string BrownoutTelling(string workName, string cityName)
		{
			string named = string.IsNullOrEmpty(workName) ? "a work" : workName;
			string city = string.IsNullOrEmpty(cityName) ? "the city" : cityName;
			return "the " + named + " of " + city + " went quiet, the lines running thin and the salt cold";
		}

		/// <summary>
		/// The ladder in one line, for a founder who wants to know what goes next. Read off
		/// <see cref="KingdomWorkTier"/> in its own order, never restated as a literal, so the
		/// enum stays the single place the order is written.
		/// </summary>
		internal static string LadderLine()
		{
			return "When the lines run thin, works stop in this order: "
				+ TierName(KingdomWorkTier.Industry) + ", then "
				+ TierName(KingdomWorkTier.Refining) + ", then "
				+ TierName(KingdomWorkTier.Amenity) + ", then "
				+ TierName(KingdomWorkTier.Food) + ", then "
				+ TierName(KingdomWorkTier.Water) + ", and last of all "
				+ TierName(KingdomWorkTier.Watch) + ". The newest built goes before the oldest.";
		}

		/// <summary>A tier as a founder would name it.</summary>
		internal static string TierName(KingdomWorkTier tier)
		{
			switch (tier)
			{
			case KingdomWorkTier.Industry:
				return "the forges and the workshops";
			case KingdomWorkTier.Refining:
				return "the yards that refine";
			case KingdomWorkTier.Amenity:
				return "comfort and lodging";
			case KingdomWorkTier.Food:
				return "the food works";
			case KingdomWorkTier.Water:
				return "the water works";
			default:
				return "the watch";
			}
		}

		/// <summary>
		/// Where a design sits on the brownout ladder, read off the catalogue's own
		/// <c>Category</c> vocabulary rather than off a second table nobody would keep in step.
		/// <para>
		/// The ten categories <c>KingdomBuildings.xml</c> actually uses map onto six rungs. An
		/// unknown category &mdash; a third party's own, arriving through the extension API
		/// (&sect;5) &mdash; lands on <see cref="KingdomWorkTier.Amenity"/>, the middle rung:
		/// a stranger's work is neither the first thing this city gives up nor the last, which is
		/// the only honest default when we do not know what it does.
		/// </para>
		/// </summary>
		internal static KingdomWorkTier TierOfCategory(string category)
		{
			if (string.IsNullOrEmpty(category))
			{
				return KingdomWorkTier.Amenity;
			}
			switch (category.Trim().ToLowerInvariant())
			{
			case "craft":
				return KingdomWorkTier.Industry;
			case "knowledge":
				return KingdomWorkTier.Refining;
			case "housing":
			case "civic":
			case "faith":
			case "memorial":
				return KingdomWorkTier.Amenity;
			case "food":
				return KingdomWorkTier.Food;
			case "storage":
			case "power":
				return KingdomWorkTier.Water;
			case "defense":
				return KingdomWorkTier.Watch;
			default:
				return KingdomWorkTier.Amenity;
			}
		}
	}
}
