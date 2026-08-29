using System;
using System.Text;

using ThousandAndFirst.Simulation.City;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomCropRules
	{
		/// <summary>Whether a field's optional exact crop declaration accepts the offered seed's
		/// crop. Blank means an ordinary flexible field; a declaration is ordinal because both
		/// names are blueprint identities from the same merged style registry.</summary>
		public static bool DeclaredCropAllows(string DeclaredCropBlueprint,
			string OfferedCropBlueprint)
		{
			return string.IsNullOrEmpty(DeclaredCropBlueprint)
				|| string.Equals(DeclaredCropBlueprint, OfferedCropBlueprint,
					StringComparison.Ordinal);
		}

		/// <summary>Names the exact crop a specialized field requires without implying a new
		/// stratum-wide rule or spending anything.</summary>
		public static string DeclaredCropRefusal(string RequiredCropName, string FieldName)
		{
			string field = string.IsNullOrEmpty(FieldName) ? "field" : FieldName;
			string crop = string.IsNullOrEmpty(RequiredCropName) ? "its declared crop" : RequiredCropName;
			return "The " + field + " is made for " + crop
				+ ". Bring that crop's seed; this seed would not take in its beds.";
		}

		/// <summary>
		/// Resolves the ground a settlement stands on to what it grows there. Mirrors
		/// <see cref="KingdomRules.StyleForSite"/>'s total fallback: an unknown, renamed, or
		/// empty style still grows something, because the ground under a field is never the
		/// reason a founder goes hungry.
		/// </summary>
		/// <param name="Style">The settlement's <see cref="KingdomSystem.Style"/>, already
		/// resolved once at founding from the terrain the rite read. Never re-derived here from
		/// terrain directly &mdash; that evidence was gathered once, and a second reading could
		/// only disagree with it.</param>
		/// <returns>A vanilla food item blueprint name, never null or empty.</returns>
		public static string CropBlueprintForStyle(string Style)
		{
			switch (Style)
			{
			case "verdant":
				return "Vinewafer";
			case "fungal":
				return "Plump Mushroom";
			case "gyre":
				return "Godshroom Cap";
			case "eater":
				return "Dreadroot Tuber";
			default:
				return "Starapple";
			}
		}

		/// <summary>
		/// Days this crop stands before it is ripe.
		/// <para>
		/// <b>Every crop answers <see cref="CropDays"/>, and that is a constraint rather than a
		/// coincidence.</b> A design's <c>Carries="food:N"</c> is one number, and the ground a
		/// settlement is founded on is not chosen by the founder &mdash; so a crop that took
		/// longer than another would make the same field carry differently in a marsh than on a
		/// flower field, for a reason nobody chose and nothing states. If a later build wants a
		/// slow crop, the catalogue's food figures have to become per-style with it; the test
		/// table and <c>_notes/balance-sim.py</c> both assert this function is flat, so that
		/// build finds out immediately rather than shipping a silent asymmetry.
		/// </para>
		/// </summary>
		/// <param name="Style">The settlement's style. Unknown styles get the common crop's
		/// days, for the reason <see cref="CropBlueprintForStyle"/> has a default.</param>
		public static int CropDaysForStyle(string Style)
		{
			switch (Style)
			{
			case "verdant":
			case "fungal":
			case "gyre":
			case "eater":
			default:
				return CropDays;
			}
		}

		// ==================================================================================
		// Seeds. One per crop family, and the map runs both ways: a seed knows what it grows,
		// and a crop knows what would sow it again.
		// ==================================================================================

		/// <summary>The seed item that sows <paramref name="CropBlueprint"/>, or null for a crop
		/// this build ships no seed for &mdash; which is not an error, only a crop the settlement
		/// cannot start on its own.</summary>
		public static string SeedForCrop(string CropBlueprint)
		{
			switch (CropBlueprint)
			{
			case "Starapple":
				return "r_KingdomSeedStarapple";
			case "Vinewafer":
				return "r_KingdomSeedVinewafer";
			case "Plump Mushroom":
				return "r_KingdomSeedMushroom";
			case "Godshroom Cap":
				return "r_KingdomSeedGodshroom";
			case "Dreadroot Tuber":
				return "r_KingdomSeedDreadroot";
			default:
				return null;
			}
		}

		/// <summary>What <paramref name="SeedBlueprint"/> grows, or null for anything that is not
		/// one of this build's seeds.</summary>
		public static string CropForSeed(string SeedBlueprint)
		{
			switch (SeedBlueprint)
			{
			case "r_KingdomSeedStarapple":
				return "Starapple";
			case "r_KingdomSeedVinewafer":
				return "Vinewafer";
			case "r_KingdomSeedMushroom":
				return "Plump Mushroom";
			case "r_KingdomSeedGodshroom":
				return "Godshroom Cap";
			case "r_KingdomSeedDreadroot":
				return "Dreadroot Tuber";
			default:
				return null;
			}
		}

		/// <summary>The seed the ground under a settlement of this style would offer, which is
		/// what its own wild plants drop and what its traders carry.</summary>
		public static string SeedForStyle(string Style)
		{
			return SeedForCrop(CropBlueprintForStyle(Style));
		}

		/// <summary>The standing plant one row of <paramref name="CropBlueprint"/> is. These are
		/// our own blueprints wearing vanilla's <c>Harvestable</c> and <c>PlantProperties</c>, so
		/// a sown field is a field of real plants somebody can walk into and gather by hand.
		/// Null for a crop with no row object, which sows nothing.</summary>
		public static string RowForCrop(string CropBlueprint)
		{
			switch (CropBlueprint)
			{
			case "Starapple":
				return "r_KingdomRowStarapple";
			case "Vinewafer":
				return "r_KingdomRowVinewafer";
			case "Plump Mushroom":
				return "r_KingdomRowMushroom";
			case "Godshroom Cap":
				return "r_KingdomRowGodshroom";
			case "Dreadroot Tuber":
				return "r_KingdomRowDreadroot";
			default:
				return null;
			}
		}

		/// <summary>Every seed this build ships, in style order. The extensibility law's limit is
		/// honest here: seeds are a closed family because each one names a crop the catalogue
		/// already grows, and a mod adding a style adds a crop, a row and a seed together.</summary>
		public static readonly string[] SeedBlueprints = new string[5]
		{
			"r_KingdomSeedStarapple",
			"r_KingdomSeedVinewafer",
			"r_KingdomSeedMushroom",
			"r_KingdomSeedGodshroom",
			"r_KingdomSeedDreadroot"
		};

	}
}
