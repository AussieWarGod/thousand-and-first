using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Projects exact standing yard-work evidence from the pass's one shared survey.</summary>
	public static class KingdomYardGoods
	{
		public static int ExactStandingHouseholds(KingdomSurvey Survey)
		{
			if (Survey == null || Survey.Ground == null) return 0;
			List<KingdomYardGoodsRules.HouseholdEvidence> houses =
				new List<KingdomYardGoodsRules.HouseholdEvidence>();
			List<GameObject> houseObjects = new List<GameObject>();
			for (int i = 0; i < Survey.Built.Count; i++)
			{
				GameObject house = Survey.Built[i];
				if (!GameObject.Validate(house) || house.CurrentZone != Survey.Ground) continue;
				KingdomRules.BuildEntry entry;
				KingdomPlotRules.PlotSpec plot;
				KingdomPlotRules.PlotRect rect;
				bool readable = KingdomYards.TryReadHouse(house, out entry, out plot, out rect);
				string key = house.GetStringProperty(KingdomYards.YardKeyProperty);
				KingdomYardRules.YardWorkSpec spec = null;
				bool known = !string.IsNullOrEmpty(key) && KingdomYards.TryGetSpec(key, out spec);
				houses.Add(new KingdomYardGoodsRules.HouseholdEvidence
				{
					PlotId = house.GetStringProperty(KingdomPlots.PlotIdProperty),
					YardKey = key,
					Built = house.GetIntProperty(KingdomUpgrade.BuiltProperty) == 1,
					Eligible = readable && KingdomYardRules.IsEligibleDesign(
						plot.Size, plot.Open, entry.Category),
					FeedsGoods = known && spec.FeedsGoods,
					Working = KingdomWear.EffectivenessOf(house) > 0
				});
				houseObjects.Add(house);
			}

			List<KingdomYardGoodsRules.FixtureEvidence> fixtures =
				new List<KingdomYardGoodsRules.FixtureEvidence>();
			for (int i = 0; i < Survey.Objects.Count; i++)
			{
				GameObject fixture = Survey.Objects[i];
				if (!GameObject.Validate(fixture)
					|| fixture.GetIntProperty(KingdomYards.YardWorkProperty) != 1) continue;
				string plotId = fixture.GetStringProperty(KingdomPlots.PlotIdProperty);
				string key = fixture.GetStringProperty(KingdomYards.YardKeyProperty);
				KingdomYardRules.YardWorkSpec spec = null;
				bool known = !string.IsNullOrEmpty(key) && KingdomYards.TryGetSpec(key, out spec);
				fixtures.Add(new KingdomYardGoodsRules.FixtureEvidence
				{
					PlotId = plotId,
					YardKey = key,
					Standing = fixture.CurrentZone == Survey.Ground && fixture.CurrentCell != null,
					InYard = StandsInMatchingYard(fixture, plotId, key, houseObjects),
					FeedsGoods = known && spec.FeedsGoods
				});
			}
			return KingdomYardGoodsRules.ExactStandingHouseholds(houses, fixtures);
		}

		private static bool StandsInMatchingYard(GameObject Fixture, string PlotId,
			string YardKey, List<GameObject> Houses)
		{
			if (Fixture?.CurrentCell == null || string.IsNullOrEmpty(PlotId)
				|| string.IsNullOrEmpty(YardKey) || Houses == null) return false;
			int matches = 0;
			for (int i = 0; i < Houses.Count; i++)
			{
				GameObject house = Houses[i];
				if (!GameObject.Validate(house)
					|| !string.Equals(house.GetStringProperty(KingdomPlots.PlotIdProperty),
						PlotId, System.StringComparison.Ordinal)
					|| !string.Equals(house.GetStringProperty(KingdomYards.YardKeyProperty),
						YardKey, System.StringComparison.Ordinal)) continue;
				List<KingdomPlotRules.PlotRect> yards = KingdomPlots.YardRects(house);
				for (int j = 0; j < yards.Count; j++)
				{
					if (!yards[j].Contains(Fixture.CurrentCell.X, Fixture.CurrentCell.Y)) continue;
					matches++;
					break;
				}
			}
			return matches == 1;
		}
	}
}
