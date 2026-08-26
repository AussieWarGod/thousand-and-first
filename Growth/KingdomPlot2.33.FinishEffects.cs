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
			// Physical verticality settles after every authored piece and exact final root exist,
			// but before the construction job can become terminal. Retry reads only the frozen root.
			if (currentAuthored && KingdomDelveRules.IsDelve(entry.Key)
				&& !KingdomDelveLink.TrySettle(building, Z, out string delveLinkFailure))
			{
				KingdomLog.Log("delve link: finalization waits: " + delveLinkFailure);
				if (construction != null && !string.IsNullOrEmpty(
					building.GetStringProperty(KingdomDelveLink.FaultProperty)))
					KingdomConstruction.Quarantine(ref construction, delveLinkFailure);
				return false;
			}
			if (construction != null && !KingdomConstruction.Complete(ref construction))
			{
				return false;
			}
			KingdomLog.Log("plot complete: " + displayName + " (" + entry.Blueprint
				+ ") over " + Rect.X1 + "," + Rect.Y1 + " to " + Rect.X2 + "," + Rect.Y2);
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (construction != null)
			{
				if (!FinishPlotEffects(system, Z, building, ref construction)) return false;
			}
			else if (system.Founded)
			{
				// The same close a single-cell scaffold has always had (r_KingdomScaffold.Complete):
				// attended, the crew gathers and a measure of water is shared; unattended, the
				// homecoming tells it. A house is not a lesser thing to raise than a palisade.
				KingdomCeremony.OnBuildingRaised(system, cell, displayName, completeTick, planQuote);
				// And the heart's own rung gets the chronicle's own voice on top of it: the same
				// crew, the same shared water, one more sentence about what the ground has become.
				KingdomCeremonyHeart.OnRungRaised(system, Z, entry.Key, heart);
				if (KingdomDelveRules.IsDelve(entry.Key))
				{
					// A work whose whole point is that the settlement can now do something it
					// could not do yesterday has to say so (STANDARDS 7b). Nothing else about a
					// finished shaft looks different from any other roof on the skyline.
					KingdomDelve.RecordShaft(Z.ZoneID);
					string opened = KingdomDelveRules.ShaftOpens(KingdomPresentation.Rich(system.SeatName));
					system.Ledger.Note("{{G|" + opened + "}}");
					MessageQueue.AddPlayerMessage("{{G|" + opened + "}}");
				}
			}
			else
			{
				MessageQueue.AddPlayerMessage("{{G|The " + displayName + " is complete.}}");
			}
			return true;
		}
	}
}
