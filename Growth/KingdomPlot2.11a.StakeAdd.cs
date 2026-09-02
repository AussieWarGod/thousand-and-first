using System;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomPlots
	{
		private static GameObject CompleteStakeAdd(KingdomSystem System, Zone Z, Cell cell,
			GameObject works, r_KingdomPlotWorks part, KingdomRules.BuildEntry Entry,
			KingdomPlotRules.PlotRect Rect, KingdomPlotRules.PlotRect footprint,
			KingdomPlotRules.RoofState roof, KingdomArchitectureIntent Architecture,
			bool LegacyArchitecture, ref KingdomConstructionJob Job,
			FoundingHeartPlacement Heart)
		{
			if (Heart != null)
			{
				if (!PreparedFoundingHeartWorksShape(works, Heart.Context))
					return HeartRefusedNull("add: prepared works shape: "
						+ PreparedFoundingHeartWorksShapeFault(works, Heart.Context));
				if (!StageFoundingHeartIdentity(works, Heart.Context.Plan, Heart.Slot))
					return HeartRefusedNull("add: identity staging");
				if (!PrepareFoundingHeartWorksAdd(Heart, works))
					return HeartRefusedNull("add: prepare");
			}
			GameObject accepted = null;
			bool callbackThrew = false;
			try
			{
				accepted = cell.AddObject(works, NoStack: Heart != null);
			}
			catch (Exception ex)
			{
				callbackThrew = true;
				if (Heart != null)
				{
					KingdomLog.Log("founding heart: plot-works AddObject callback cut: "
						+ ex.Message);
				}
				else
				{
					bool cleaned = RemoveCreatedWorks(works, Z);
					if (Job != null) KingdomConstruction.Quarantine(ref Job,
						(cleaned ? "Plot-works AddObject threw after identity publication: "
							: "Plot-works AddObject threw and exact cleanup failed: ") + ex.Message);
				}
			}
			finally { KingdomSurvey.ObserveAddResultInActive(Z, works, accepted); }
			if (Heart != null)
				return SettleFoundingHeartWorksAdd(Heart, works, accepted, callbackThrew)
					? works : HeartRefusedNull("add: settle");
			if (callbackThrew) return null;
			GameObject exactWorks;
			if (!ReferenceEquals(accepted, works)
				|| KingdomConstruction.FindExactId(Z, works.ID, out exactWorks)
					!= KingdomPhysicalLookupState.Exact
				|| !ReferenceEquals(exactWorks, works)
				|| works.CurrentCell != cell || works.CurrentZone != Z
				|| works.Blueprint != WorksBlueprint
				|| works.GetPart<r_KingdomPlotWorks>() != part || part.DesignKey != Entry.Key
				|| works.GetStringProperty(KingdomUpgrade.BuildKeyProperty) != Entry.Key
				|| !ExpectedWorks(works, cell, Entry.Key, Architecture, LegacyArchitecture, Job)
				|| (Job != null && (!KingdomConstruction.Owns(System, Z, Job)
					|| works.ID != Job.OutputId
					|| !KingdomConstruction.HasReceipt(works, Job)
					|| !KingdomConstruction.IsCurrent(Job))))
			{
				bool cleaned = RemoveCreatedWorks(works, Z);
				if (Job != null) KingdomConstruction.Quarantine(ref Job, cleaned
					? "Plot works changed during AddObject; frozen identity was retired."
					: "Plot works changed during AddObject and exact cleanup failed.");
				return null;
			}
			KingdomLog.Log("plot staked: " + Entry.Key + " " + Rect.X1 + "," + Rect.Y1
				+ " to " + Rect.X2 + "," + Rect.Y2 + " footprint " + footprint.X1 + ","
				+ footprint.Y1 + " to " + footprint.X2 + "," + footprint.Y2 + " "
				+ roof.ToString().ToLowerInvariant() + " over " + part.TotalTicks + " ticks");
			return works;
		}
	}
}
