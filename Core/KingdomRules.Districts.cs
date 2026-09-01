namespace ThousandAndFirst
{
	public static partial class KingdomRules
	{

		public static string ToThirdPerson(string Text, string FounderName)
		{
			if (string.IsNullOrEmpty(Text))
			{
				return Text;
			}
			string text = Text.Replace("your ", FounderName + "'s ").Replace("Your ", FounderName + "'s ");
			text = text.Replace("you poured", FounderName + " poured").Replace("You poured", FounderName + " poured");
			text = text.Replace("you ", FounderName + " ").Replace("You ", FounderName + " ");
			return text;
		}

		public static bool IsValidDistrict(string District)
		{
			for (int i = 0; i < Districts.Length; i++)
			{
				if (Districts[i] == District)
				{
					return true;
				}
			}
			return false;
		}

		public static long ArrivalIntervalTicks(int Population, string District)
		{
			long num = ArrivalIntervalTicks(Population);
			if (District == "market")
			{
				num = num * DistrictMarketArrivalPercent / 100;
			}
			return num;
		}

		/// <summary>
		/// Defence a garrison district contributes for every claimed zone that declares it.
		/// Deliberately small: a watch is a militia turning out, not a wall, and
		/// <see cref="DefenceToRepel"/> still wants real works behind it.
		/// </summary>
		public const int DistrictGarrisonDefence = 2;

		/// <summary>Upkeep under an agrarian district, as a percent of the ordinary bill.</summary>
		public const int DistrictAgrarianUpkeepPercent = 90;

		/// <summary>Market-service standing a designated market district can add, within the
		/// physical fixture, craft-knowledge, and growth ceilings.</summary>
		public const int DistrictMarketShopTier = 1;

		/// <summary>Arrival interval under a market district, as a percent of the ordinary wait.</summary>
		public const int DistrictMarketArrivalPercent = 90;

		/// <summary>Build time under a craft district, as a percent of the ordinary time.</summary>
		public const int DistrictCraftBuildPercent = 80;

		/// <summary>Wait between petitions under a shrine district, as a percent of the ordinary wait.</summary>
		public const int DistrictShrinePetitionPercent = 75;

		/// <summary>
		/// Reachable outsider drift under an academy district, as a percent of the ordinary
		/// range: the scriptorium keeps the record, so fewer retellings wander from it.
		/// </summary>
		public const int DistrictAcademyDriftPercent = 50;

		/// <summary>A district with nothing to say about a quantity leaves that quantity whole.</summary>
		public const int DistrictNeutralPercent = 100;

		/// <summary>
		/// Hard floor under every aggregated district percent. Districts are registry data that
		/// third parties may extend (STANDARDS 6), so without a floor one careless entry could
		/// drive an interval, a bill, or a drift range to zero. Nothing a district does may cut a
		/// quantity by more than half.
		/// </summary>
		public const int DistrictPercentFloor = 50;

		/// <summary>Defence the watch adds where a garrison district is declared.</summary>
		/// <param name="District">A district key, or any unknown string. Null is tolerated.</param>
		/// <returns><see cref="DistrictGarrisonDefence"/> for "garrison", otherwise 0. A district
		/// never subtracts.</returns>
		public static int DistrictDefenceBonus(string District)
		{
			if (District == "garrison")
			{
				return DistrictGarrisonDefence;
			}
			return 0;
		}

		/// <summary>Upkeep the vinelands charge, as a percent of the ordinary bill.</summary>
		/// <param name="District">A district key, or any unknown string. Null is tolerated.</param>
		/// <returns><see cref="DistrictAgrarianUpkeepPercent"/> for "agrarian", otherwise
		/// <see cref="DistrictNeutralPercent"/>.</returns>
		public static int DistrictUpkeepPercent(string District)
		{
			if (District == "agrarian")
			{
				return DistrictAgrarianUpkeepPercent;
			}
			return DistrictNeutralPercent;
		}

		/// <summary>Market-service standing a designated market district can add. It never
		/// creates wares or bypasses the physical market and growth ceilings.</summary>
		/// <param name="District">A district key, or any unknown string. Null is tolerated.</param>
		/// <returns><see cref="DistrictMarketShopTier"/> for "market", otherwise 0.</returns>
		public static int DistrictShopTierBonus(string District)
		{
			if (District == "market")
			{
				return DistrictMarketShopTier;
			}
			return 0;
		}

		/// <summary>Build time the forgeworks charge, as a percent of the ordinary time.</summary>
		/// <param name="District">A district key, or any unknown string. Null is tolerated.</param>
		/// <returns><see cref="DistrictCraftBuildPercent"/> for "craft", otherwise
		/// <see cref="DistrictNeutralPercent"/>.</returns>
		public static int DistrictBuildPercent(string District)
		{
			if (District == "craft")
			{
				return DistrictCraftBuildPercent;
			}
			return DistrictNeutralPercent;
		}

		/// <summary>Wait between petitions on sacred ground, as a percent of the ordinary wait.</summary>
		/// <param name="District">A district key, or any unknown string. Null is tolerated.</param>
		/// <returns><see cref="DistrictShrinePetitionPercent"/> for "shrine", otherwise
		/// <see cref="DistrictNeutralPercent"/>.</returns>
		public static int DistrictPetitionIntervalPercent(string District)
		{
			if (District == "shrine")
			{
				return DistrictShrinePetitionPercent;
			}
			return DistrictNeutralPercent;
		}

		/// <summary>Reachable outsider drift under the scriptorium, as a percent of the ordinary range.</summary>
		/// <param name="District">A district key, or any unknown string. Null is tolerated.</param>
		/// <returns><see cref="DistrictAcademyDriftPercent"/> for "academy", otherwise
		/// <see cref="DistrictNeutralPercent"/>.</returns>
		public static int DistrictDriftPercent(string District)
		{
			if (District == "academy")
			{
				return DistrictAcademyDriftPercent;
			}
			return DistrictNeutralPercent;
		}

		/// <summary>
		/// The aggregation law for district percent effects: <b>best wins, and nothing stacks.</b>
		/// The strongest single district of a kind sets the number for the whole settlement, so a
		/// second vinelands feeds the same city rather than feeding it twice, and the result is
		/// clamped into <see cref="DistrictPercentFloor"/>..<see cref="DistrictNeutralPercent"/>.
		/// <para>
		/// Stacking was rejected on both pillars. Multiplied percents make the sixth claimed zone
		/// worth more than the first and converge on zero, which turns a flavour choice into a
		/// mandatory one; and a settlement that declared its districts early would be punished for
		/// not declaring more of them. Additive aggregation is kept only for defence, where the
		/// fiction is bodies on a wall and more of them plainly is more.
		/// </para>
		/// </summary>
		/// <param name="Districts">District key of every claimed zone. Nulls, blanks, unknown
		/// keys, and duplicates are all tolerated and contribute nothing.</param>
		/// <param name="Effect">Percent this district charges for the quantity being aggregated.</param>
		/// <returns><see cref="DistrictNeutralPercent"/> for a null or empty sequence.</returns>
		private static int BestDistrictPercent(System.Collections.Generic.IEnumerable<string> Districts, System.Func<string, int> Effect)
		{
			int best = DistrictNeutralPercent;
			if (Districts != null)
			{
				foreach (string district in Districts)
				{
					int percent = Effect(district);
					if (percent < best)
					{
						best = percent;
					}
				}
			}
			if (best < DistrictPercentFloor)
			{
				return DistrictPercentFloor;
			}
			if (best > DistrictNeutralPercent)
			{
				return DistrictNeutralPercent;
			}
			return best;
		}

		/// <summary>
		/// Defence every garrison district in the realm musters. Additive by the law documented
		/// on <see cref="BestDistrictPercent"/>, and uncapped: claiming and declaring six zones is
		/// six zones of real investment, and the answer feeds <see cref="ResolveRaid"/> beside the
		/// works that must still be crewed.
		/// </summary>
		/// <param name="Districts">District key of every claimed zone; nulls and unknowns ignored.</param>
		/// <returns>0 for a null or empty sequence.</returns>
		public static int DistrictsDefenceBonus(System.Collections.Generic.IEnumerable<string> Districts)
		{
			int total = 0;
			if (Districts == null)
			{
				return total;
			}
			foreach (string district in Districts)
			{
				total += DistrictDefenceBonus(district);
			}
			return total;
		}

		/// <summary>Upkeep the realm's vinelands charge, by the law on <see cref="BestDistrictPercent"/>.</summary>
		/// <param name="Districts">District key of every claimed zone; nulls and unknowns ignored.</param>
		public static int DistrictsUpkeepPercent(System.Collections.Generic.IEnumerable<string> Districts)
		{
			return BestDistrictPercent(Districts, DistrictUpkeepPercent);
		}

		/// <summary>
		/// Market-service standing the realm's designated bazaars can add. Best-wins like the
		/// percent effects: a second market broadens place, not service depth or physical stock.
		/// </summary>
		/// <param name="Districts">District key of every claimed zone; nulls and unknowns ignored.</param>
		/// <returns>0 for a null or empty sequence.</returns>
		public static int DistrictsShopTierBonus(System.Collections.Generic.IEnumerable<string> Districts)
		{
			int best = 0;
			if (Districts == null)
			{
				return best;
			}
			foreach (string district in Districts)
			{
				int bonus = DistrictShopTierBonus(district);
				if (bonus > best)
				{
					best = bonus;
				}
			}
			return best;
		}

		/// <summary>Build time the realm's forgeworks charge, by the law on <see cref="BestDistrictPercent"/>.</summary>
		/// <param name="Districts">District key of every claimed zone; nulls and unknowns ignored.</param>
		public static int DistrictsBuildPercent(System.Collections.Generic.IEnumerable<string> Districts)
		{
			return BestDistrictPercent(Districts, DistrictBuildPercent);
		}

		/// <summary>Petition wait under the realm's sacred ground, by the law on <see cref="BestDistrictPercent"/>.</summary>
		/// <param name="Districts">District key of every claimed zone; nulls and unknowns ignored.</param>
		public static int DistrictsPetitionIntervalPercent(System.Collections.Generic.IEnumerable<string> Districts)
		{
			return BestDistrictPercent(Districts, DistrictPetitionIntervalPercent);
		}

		/// <summary>Outsider drift under the realm's scriptoria, by the law on <see cref="BestDistrictPercent"/>.</summary>
		/// <param name="Districts">District key of every claimed zone; nulls and unknowns ignored.</param>
		public static int DistrictsDriftPercent(System.Collections.Generic.IEnumerable<string> Districts)
		{
			return BestDistrictPercent(Districts, DistrictDriftPercent);
		}

	}
}
