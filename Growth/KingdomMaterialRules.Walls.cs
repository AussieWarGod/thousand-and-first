using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomMaterialRules
	{
		// --- Wall material: the theme, chosen by what the settlement has quarried -------------

		/// <summary>
		/// Units of a material a settlement must hold before its walls can be said to be made of
		/// it. Indexed by <see cref="KingdomMaterial"/>; mud is zero, because mud is the ground
		/// and the ground is always there. The refined three sit LOWER than the raw stock they
		/// came from, on purpose: a yard turns two loads into one, so holding six shaped timbers
		/// is holding twelve trees' worth of work, and a settlement that has done that much
		/// dressing is a settlement whose walls look it.
		/// </summary>
		public static readonly int[] WallMaterialThreshold = new int[MaterialCount] { 0, 4, 8, 10, 14, 10, 6, 8, 8 };

		/// <summary>Materials in the order a settlement would rather build in, richest first.
		/// Mud is last and is the floor nothing ever falls through.</summary>
		public static readonly KingdomMaterial[] WallMaterialPreference = new KingdomMaterial[MaterialCount]
		{
			KingdomMaterial.Marble,
			KingdomMaterial.ShapedStone,
			KingdomMaterial.WorkedMetal,
			KingdomMaterial.ShapedTimber,
			KingdomMaterial.Stone,
			KingdomMaterial.Scrap,
			KingdomMaterial.Timber,
			KingdomMaterial.Brush,
			KingdomMaterial.Mud
		};

		/// <summary>
		/// The material a settlement of the given style would choose first if it could afford to,
		/// whatever its stock says. A style is a taste, not a supply: an unmet taste changes
		/// nothing and costs nothing.
		/// </summary>
		/// <param name="Style">A city style key. Unknown and null styles have no preference.</param>
		/// <param name="Material">Set on success.</param>
		/// <returns>False when the style expresses no preference at all.</returns>
		public static bool TryStylePreference(string Style, out KingdomMaterial Material)
		{
			Material = KingdomMaterial.Mud;
			if (string.IsNullOrEmpty(Style))
			{
				return false;
			}
			switch (Style)
			{
			case "verdant":
			case "fungal":
				Material = KingdomMaterial.Timber;
				return true;
			case "gyre":
				Material = KingdomMaterial.Marble;
				return true;
			case "eater":
				Material = KingdomMaterial.Scrap;
				return true;
			default:
				return false;
			}
		}

		/// <summary>
		/// The material a settlement's walls are made of: its style's taste if its own quarrying
		/// has met the threshold for that material, else the richest material it holds enough of,
		/// else mud. Never fails and never returns something the settlement does not have &mdash;
		/// a settlement that has quarried nothing builds in mud, which is what a camp looks like.
		/// </summary>
		/// <param name="Stock">What the stockpiles hold. Null reads as empty.</param>
		/// <param name="Style">The city's style key, or null.</param>
		public static KingdomMaterial WallMaterialFor(KingdomMaterialTally Stock, string Style)
		{
			KingdomMaterial preferred;
			bool hasPreference = TryStylePreference(Style, out preferred);
			return WallMaterialFor(Stock, hasPreference, preferred);
		}

		/// <summary>Open-registry form of wall selection. The caller supplies a validated style
		/// preference; stock still remains authoritative, so data can express taste but cannot
		/// conjure a material the settlement has not earned.</summary>
		public static KingdomMaterial WallMaterialFor(KingdomMaterialTally Stock,
			bool HasPreference, KingdomMaterial Preferred)
		{
			if (HasPreference && HasWallMaterial(Stock, Preferred))
			{
				return Preferred;
			}
			for (int i = 0; i < WallMaterialPreference.Length; i++)
			{
				if (HasWallMaterial(Stock, WallMaterialPreference[i]))
				{
					return WallMaterialPreference[i];
				}
			}
			return KingdomMaterial.Mud;
		}

		/// <summary>Whether the stock has reached the threshold for building walls of a
		/// material. Mud's threshold is zero, so this is always true of mud.</summary>
		public static bool HasWallMaterial(KingdomMaterialTally Stock, KingdomMaterial Material)
		{
			int index = (int)Material;
			if (index < 0 || index >= WallMaterialThreshold.Length)
			{
				return false;
			}
			int held = (Stock == null) ? 0 : Stock.Get(Material);
			return held >= WallMaterialThreshold[index];
		}

	}
}
