using System.Collections.Generic;

using XRL.World;

namespace ThousandAndFirst
{
	public partial class KingdomSurvey
	{
		/// <summary>Builds the ordinary bounded physical index without legacy migration,
		/// citizenship publication, ledger work, or economic simulation. Used only to recover
		/// a durable custody receipt on an attended former claim or to take an isolated
		/// hosted-floor physical observation.</summary>
		internal static KingdomSurvey TakeCustodyOnly(Zone zone)
		{
			KingdomSurvey survey = new KingdomSurvey { Ground = zone };
			if (zone == null) return survey;
			List<GameObject> roots = zone.GetObjects();
			if (roots == null || roots.Count > MaxIndexedObjects)
			{ survey.LoadedIndexComplete = false; return survey; }
			survey.ClassificationPasses++;
			survey.ClassifiedRoots = roots.Count;
			for (int i = 0; i < roots.Count; i++)
			{
				GameObject item = roots[i];
				if (!GameObject.Validate(item) || item.CurrentZone != zone)
				{ survey.LoadedIndexComplete = false; continue; }
				survey.AddRoot(item, null);
			}
			survey.FoodAbundance = KingdomRules.ClassifyPantry(survey.FoodStored);
			return survey;
		}
	}
}
