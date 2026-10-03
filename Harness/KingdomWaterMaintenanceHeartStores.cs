using System;
using System.Text;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	// Since the rite ground became a camp, the heart's first basin is a civic store: the rite
	// loader stamps KingdomStores on it (Growth/KingdomPlotHeartRules.Loader.cs), so the survey
	// lawfully lists it beside the fixture's two dedicated vessels. The fixture therefore
	// partitions the survey's stores instead of assuming exactly two, admits only heart stores
	// standing on the surveyed heart ground, and accounts their fresh water separately. Read-only.
	internal static class KingdomWaterMaintenanceHeartStores
	{
		internal static int Water(KingdomSurvey survey, Zone zone, LiquidVolume[] fixture, out int count, out string description)
		{
			int water = 0; count = 0; var text = new StringBuilder();
			bool surveyed = KingdomPlots.TrySurveyedHeart(zone, out KingdomPlotRules.PlotRect heart);
			foreach (LiquidVolume store in survey.Stores)
			{
				if (Array.IndexOf(fixture, store) >= 0) continue;
				GameObject owner = store.ParentObject; Cell cell = owner?.CurrentCell;
				KingdomWaterMaintenanceNativeProvider.Require(GameObject.Validate(owner) && cell != null && surveyed
					&& owner.GetIntProperty("KingdomStores") == 1 && heart.Contains(cell.X, cell.Y),
					"survey store is neither a fixture vessel nor a heart store on the surveyed ground: " + (owner?.Blueprint ?? "null"));
				int fresh = KingdomLiquids.HasFreshWater(store) ? store.Volume : 0;
				water += fresh; count++;
				text.Append(count > 1 ? ";" : "").Append(owner.Blueprint).Append('@').Append(cell.X).Append(',').Append(cell.Y)
					.Append(" water=").Append(fresh).Append('/').Append(store.MaxVolume);
			}
			description = count == 0 ? "none" : text.ToString();
			return water;
		}
	}
}
