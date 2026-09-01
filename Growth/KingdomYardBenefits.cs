using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Dedicated physical food-flow proof for the one food-bearing yard work.</summary>
	public static class KingdomYardBenefits
	{
		public static int PhysicalFoodForHouse(KingdomSurvey Survey, GameObject House)
		{
			if (Survey?.Ground == null || !GameObject.Validate(House)
				|| !ReferenceEquals(House.CurrentZone, Survey.Ground)
				|| !KingdomYards.TryReadHouse(House, out KingdomRules.BuildEntry entry,
					out KingdomPlotRules.PlotSpec plot, out _)
				|| !KingdomYardRules.IsEligibleDesign(plot.Size, plot.Open, entry.Category)) return 0;
			string key = House.GetStringProperty(KingdomYards.YardKeyProperty);
			if (string.IsNullOrEmpty(key) || !KingdomYards.TryGetSpec(key,
				out KingdomYardRules.YardWorkSpec spec) || spec == null || spec.FeedsGoods) return 0;
			int food = 0;
			for (int i = 0; i < spec.Shades.Count; i++)
				if (spec.Shades[i].Kind == KingdomCatalogueRules.SupportFood)
					food = KingdomCatalogueRules.SaturatingCounterAdd(
						food, spec.Shades[i].Amount);
			if (food <= 0) return 0;
			string plotId = House.GetStringProperty(KingdomPlots.PlotIdProperty);
			KingdomYardGoodsRules.FoodHouseholdEvidence household =
				new KingdomYardGoodsRules.FoodHouseholdEvidence {
					PlotId = plotId, YardKey = key, ExpectedBlueprint = spec.Blueprint,
					FoodCap = food, Built = KingdomUpgrade.IsFunctionallyBuilt(House),
					Eligible = true, Registered = true };
			List<KingdomYardGoodsRules.FoodFixtureEvidence> fixtures =
				new List<KingdomYardGoodsRules.FoodFixtureEvidence>();
			List<GameObject> houses = new List<GameObject> { House };
			for (int i = 0; i < Survey.Objects.Count; i++)
			{
				GameObject fixture = Survey.Objects[i];
				if (!GameObject.Validate(fixture)
					|| fixture.GetIntProperty(KingdomYards.YardWorkProperty) != 1) continue;
				string fixturePlot = fixture.GetStringProperty(KingdomPlots.PlotIdProperty);
				string fixtureKey = fixture.GetStringProperty(KingdomYards.YardKeyProperty);
				fixtures.Add(new KingdomYardGoodsRules.FoodFixtureEvidence {
					PlotId = fixturePlot, YardKey = fixtureKey, Blueprint = fixture.Blueprint,
					Standing = ReferenceEquals(fixture.CurrentZone, Survey.Ground)
						&& fixture.CurrentCell != null,
					InYard = KingdomYardGoods.StandsInMatchingYard(fixture, fixturePlot,
						fixtureKey, houses), Unbroken = !fixture.IsBroken() });
			}
			return KingdomYardGoodsRules.ExactPhysicalFood(household, fixtures);
		}
	}
}
