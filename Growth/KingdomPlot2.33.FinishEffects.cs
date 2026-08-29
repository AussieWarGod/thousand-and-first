using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomPlots
	{
		private static bool TryFinishEffects(Zone Z, Cell cell,
			KingdomRules.BuildEntry entry, GameObject building,
			KingdomPlotRules.PlotRect Rect, bool currentAuthored, bool heart,
			string displayName, long completeTick, string planQuote,
			ref KingdomConstructionJob construction)
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (construction == null) return FinishLegacyPlotEffects(system, Z, building);
			// Physical verticality settles after every authored piece and exact final root exist,
			// but before the construction job can become terminal. Retry reads only the frozen root.
			string delveLinkFailure = null;
			bool delveSettled = true;
			if (currentAuthored && KingdomDelveRules.IsDelve(entry.Key))
			{
				try { delveSettled = KingdomDelveLink.TrySettle(building, Z,
					out delveLinkFailure); }
				catch { delveSettled = false; }
				if (!ExactPlotFinalRootCustody(construction.OutputId, building)) return false;
				if (!delveSettled)
				{
					KingdomLog.Log("delve link: finalization waits: " + delveLinkFailure);
					if (!string.IsNullOrEmpty(building.GetStringProperty(
						KingdomDelveLink.FaultProperty)))
						KingdomConstruction.Quarantine(ref construction, delveLinkFailure);
					return false;
				}
			}
			if (!ExactPlotFinalRootCustody(construction.OutputId, building)) return false;
			if (construction != null && !KingdomConstruction.Complete(ref construction))
			{
				return false;
			}
			if (!ExactPlotFinalRootCustody(construction.OutputId, building)) return false;
			KingdomLog.Log("plot complete: " + displayName + " (" + entry.Blueprint
				+ ") over " + Rect.X1 + "," + Rect.Y1 + " to " + Rect.X2 + "," + Rect.Y2);
			return FinishPlotEffects(system, Z, building, ref construction);
		}

		private static bool TryExactSettlementName(KingdomSystem System, Zone Z,
			out string Name)
		{
			Name = null;
			string id = System?.SettlementIdForOwnedZone(Z?.ZoneID);
			if (string.IsNullOrEmpty(id) || !System.TryFindSettlement(id,
				out bool seated, out KingdomSettlement settlement)) return false;
			Name = seated ? System.SeatName : settlement?.SettlementName;
			return !string.IsNullOrEmpty(Name);
		}

		private static bool ExactPlotEffectEndpoint(KingdomSystem System, Zone Z,
			GameObject Building, KingdomConstructionJob Job)
		{
			GameObject exact;
			return KingdomConstruction.Owns(System, Z, Job)
				&& KingdomConstruction.IsCurrent(Job)
				&& KingdomConstruction.FindExactId(Z, Job.OutputId, out exact)
					== KingdomPhysicalLookupState.Exact
				&& ReferenceEquals(exact, Building) && GameObject.Validate(Building)
				&& Building.CurrentCell == Z.GetCell(Job.X, Job.Y)
				&& KingdomConstruction.HasReceipt(Building, Job);
		}
	}
}
