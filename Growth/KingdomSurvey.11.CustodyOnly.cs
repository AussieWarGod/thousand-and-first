using System.Collections.Generic;

using XRL.World;

namespace ThousandAndFirst
{
	public partial class KingdomSurvey
	{
		internal static bool HasBoundPass { get { return BoundSurvey != null || BoundDepth != 0; } }

		/// <summary>A separate recovery wake needs fresh, complete roots, never a cached absence.
		/// Bound callers defer; this never adds a second classification to a semantic pass.</summary>
		internal static bool TryTakeUnboundRecovery(Zone zone, out KingdomSurvey survey)
		{
			survey = null;
			if (zone == null || HasBoundPass) return false;
			try
			{
				KingdomSurvey captured = TakeCustodyOnly(zone);
				if (HasBoundPass || !captured.TryLoaded(out _)
					|| captured.ClassifiedRoots != captured.Objects.Count) return false;
				survey = captured; return true;
			}
			catch { return false; }
		}

		/// <summary>Builds the ordinary bounded physical index without legacy migration,
		/// citizenship publication, ledger work, or economic simulation. Used only to recover
		/// a durable custody receipt on an attended former claim or to take an isolated
		/// hosted-floor or unbound raid-recovery physical observation.</summary>
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
