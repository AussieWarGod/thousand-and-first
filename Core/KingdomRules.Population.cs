namespace ThousandAndFirst
{
	public static partial class KingdomRules
	{
		/// <summary>Stock tier a settlement's shops carry at a given growth stage.</summary>
		public static int ShopTierForStage(GrowthStage Stage)
		{
			switch (Stage)
			{
			case GrowthStage.Camp:
				return 1;
			case GrowthStage.Steading:
				return 2;
			case GrowthStage.Village:
				return 3;
			case GrowthStage.Town:
				return 5;
			default:
				return 7;
			}
		}

		/// <summary>
		/// Whether the settlement has a bed free for one more settler &mdash; a raw tally, and no
		/// longer the arrival gate. Addendum 4b made joining assignment-level (a settler joins only
		/// if a home <em>they</em> would accept exists &mdash; <c>KingdomLodging</c>), so this
		/// survives for the two places a plain count is the right question: whether a guest can be
		/// lodged, and the founder-facing next-need line.
		/// </summary>
		public static bool HasRoomToHouse(int Population, int Beds)
		{
			return Population < Beds;
		}

		/// <summary>
		/// Allocates citizens to works in priority order. Works declare how they respond to
		/// short crew: a <b>scaled</b> work runs at whatever fraction it has hands for (one
		/// person on a two-person crank still turns it, half as fast), while a
		/// <b>threshold</b> work needs its full crew or nothing at all &mdash; there is no such
		/// thing as half a shopkeeper.
		/// </summary>
		/// <param name="Citizens">Citizens available for work.</param>
		/// <param name="Demands">Staff each work requires, in priority order.</param>
		/// <param name="Thresholds">True where a work is all-or-nothing. Null means all scaled.</param>
		/// <returns>Parallel array of citizens actually assigned to each work.</returns>
		public static int[] AssignCrew(int Citizens, int[] Demands, bool[] Thresholds = null)
		{
			int[] result = new int[(Demands != null) ? Demands.Length : 0];
			if (Demands == null)
			{
				return result;
			}
			int remaining = (Citizens > 0) ? Citizens : 0;
			for (int i = 0; i < Demands.Length; i++)
			{
				int need = (Demands[i] > 0) ? Demands[i] : 0;
				if (need == 0)
				{
					continue;
				}
				bool threshold = Thresholds != null && i < Thresholds.Length && Thresholds[i];
				int give = (need <= remaining) ? need : (threshold ? 0 : remaining);
				result[i] = give;
				remaining -= give;
			}
			return result;
		}

		/// <summary>Fraction of full output a work produces with the crew it has, 0 to 100.</summary>
		public static int CrewEffectiveness(int Assigned, int Needed)
		{
			if (Needed <= 0)
			{
				return 100;
			}
			if (Assigned <= 0)
			{
				return 0;
			}
			if (Assigned >= Needed)
			{
				return 100;
			}
			return Assigned * 100 / Needed;
		}

		public static bool IsThresholdManning(string Manning)
		{
			return Manning == "threshold";
		}

	}
}
