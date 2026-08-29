using System;
using System.Collections.Generic;
using System.Globalization;

using XRL;
using XRL.World;
using XRL.World.Parts;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomConstructionPresence
	{
		private static void Reset(GameObject Root)
		{
			Root.RemoveIntProperty(ActiveProperty);
			Root.RemoveIntProperty(SelectedProperty);
			Root.RemoveIntProperty(HandsProperty);
			Root.RemoveIntProperty(EffectivenessProperty);
			r_KingdomStation station = Root.GetPart<r_KingdomStation>();
			if (station != null && station.Kind == (int)KingdomWorkKind.Construction)
			{
				station.WorkId = 0;
			}
		}

		private static bool NeedsLabour(GameObject Root)
		{
			if (!GameObject.Validate(Root)) return false;
			if (Root.GetPart<r_KingdomRelocationFrame>() != null)
				return KingdomRelocation.FrameNeedsLabour(Root);
			r_KingdomScaffold scaffold = Root.GetPart<r_KingdomScaffold>();
			if (scaffold != null)
			{
				return !string.IsNullOrEmpty(scaffold.TargetBlueprint)
					&& (scaffold.LastWorkedTick <= 0L || scaffold.RemainingTicks > 0L);
			}
			r_KingdomPlotWorks plot = Root.GetPart<r_KingdomPlotWorks>();
			if (plot == null || string.IsNullOrEmpty(plot.DesignKey)
				|| Root.GetIntProperty(KingdomPlots.PlotWorkSchemaProperty) != KingdomPlots.PlotWorkSchema)
				return false;
			return long.TryParse(Root.GetStringProperty(KingdomPlots.PlotWorkRemainingProperty),
				NumberStyles.Integer, CultureInfo.InvariantCulture, out long remaining)
				&& remaining > 0L;
		}

		private static long Started(GameObject Root, r_KingdomPlotWorks Plot,
			r_KingdomScaffold Scaffold)
		{
			string receipt = Root.GetStringProperty(KingdomConstruction.ReceiptProperty);
			if (!string.IsNullOrEmpty(receipt) && KingdomConstruction.TryFind(receipt,
				out KingdomConstructionJob job) && job.StartedTick >= 0L)
				return job.StartedTick;
			if (Plot != null && Plot.StartTick >= 0L) return Plot.StartTick;
			if (Scaffold != null && Scaffold.LastWorkedTick > 0L) return Scaffold.LastWorkedTick;
			if (long.TryParse(Root.GetStringProperty(LegacyStartProperty), NumberStyles.Integer,
				CultureInfo.InvariantCulture, out long saved) && saved >= 0L) return saved;
			long now = The.Game == null ? 0L : The.Game.TimeTicks;
			Root.SetStringProperty(LegacyStartProperty, now.ToString(CultureInfo.InvariantCulture));
			return now;
		}

		private static Zone GroundOf(KingdomSurvey Survey)
		{
			for (int i = 0; i < Survey.Settlers.Count; i++)
				if (GameObject.Validate(Survey.Settlers[i]) && Survey.Settlers[i].CurrentZone != null)
					return Survey.Settlers[i].CurrentZone;
			for (int i = 0; i < Survey.Built.Count; i++)
				if (GameObject.Validate(Survey.Built[i]) && Survey.Built[i].CurrentZone != null)
					return Survey.Built[i].CurrentZone;
			return null;
		}
	}
}
