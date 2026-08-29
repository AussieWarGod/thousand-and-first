using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomPlots
	{
		/// <summary>Freezes only facts witnessed now; they may price the following interval.</summary>
		private static bool TryCapturePlotLabourWindow(GameObject Root, KingdomSystem System,
			long TimeTick, int InfrastructurePercent, string InfrastructureFailure,
			string DisplayName)
		{
			int effectiveness = KingdomConstructionPresence.EffectivenessOf(Root, System,
				out int freeHands, out bool selected);
			if (Root.GetIntProperty(KingdomConstructionPresence.SchemaProperty)
				!= KingdomConstructionPresenceRules.Schema)
			{
				effectiveness = 0;
				freeHands = 0;
				selected = false;
			}
			int infrastructure = InfrastructurePercent
				== KingdomPlotLabourWindowRules.InfrastructureReady
				? KingdomPlotLabourWindowRules.InfrastructureReady
				: KingdomPlotLabourWindowRules.InfrastructureUnavailable;
			KingdomPlotLabourWindow current = new KingdomPlotLabourWindow
			{
				Tick = TimeTick,
				LabourPercent = effectiveness,
				InfrastructurePercent = infrastructure,
				Hands = freeHands,
				Selected = selected
			};
			if (!KingdomPlotLabourWindowRules.TryEncode(current, out string encoded))
			{
				current.LabourPercent = 0;
				freeHands = current.Hands = 0;
				selected = current.Selected = false;
				if (!KingdomPlotLabourWindowRules.TryEncode(current, out encoded)) return false;
				SayPlotWorkFault(System, Root,
					"The plot's loaded construction-crew witness was out of bounds; no crew was inferred.");
			}
			Root.SetStringProperty(PlotWorkWindowProperty, encoded);
			if (Root.GetStringProperty(PlotWorkWindowProperty) != encoded)
			{
				SayPlotWorkFault(System, Root,
					"The plot could not retain its loaded construction witness; no later work was inferred.");
				return false;
			}
			SayPlotInfrastructure(System, Root,
				infrastructure == KingdomPlotLabourWindowRules.InfrastructureReady
					? null : InfrastructureFailure);
			if (selected) SayPlotWorkShortfall(System, Root, DisplayName, freeHands);
			return true;
		}

		/// <summary>Unauthorized facts can seed only a canonical zero following interval.</summary>
		private static bool TryCaptureZeroPlotLabourWindow(GameObject Root, KingdomSystem System,
			long TimeTick, string Failure)
		{
			KingdomPlotLabourWindow zero = new KingdomPlotLabourWindow
			{
				Tick = TimeTick,
				LabourPercent = 0,
				InfrastructurePercent = KingdomPlotLabourWindowRules.InfrastructureUnavailable,
				Hands = 0,
				Selected = false
			};
			if (!KingdomPlotLabourWindowRules.TryEncode(zero, out string encoded)) return false;
			Root.SetStringProperty(PlotWorkWindowProperty, encoded);
			if (Root.GetStringProperty(PlotWorkWindowProperty) != encoded)
			{
				Root.RemoveStringProperty(PlotWorkWindowProperty);
				SayPlotWorkFault(System, Root,
					"The plot could not retain its zero labour witness; no later work was inferred.");
				return false;
			}
			SayPlotInfrastructure(System, Root, Failure);
			return true;
		}

		private static void SayPlotInfrastructure(KingdomSystem System, GameObject Works,
			string Failure)
		{
			if (Works == null) return;
			if (string.IsNullOrEmpty(Failure))
			{
				Works.SetIntProperty(PlotWorkShortfallSaidProperty,
					Works.GetIntProperty(PlotWorkShortfallSaidProperty)
						& ~PlotWorkInfrastructureSaid);
				return;
			}
			int said = Works.GetIntProperty(PlotWorkShortfallSaidProperty);
			if ((said & PlotWorkInfrastructureSaid) != 0) return;
			Works.SetIntProperty(PlotWorkShortfallSaidProperty,
				said | PlotWorkInfrastructureSaid);
			KingdomLog.Log("plot infrastructure: " + Failure);
			if (System != null && System.Founded) System.Ledger.Note("{{r|" + Failure + "}}");
		}
	}
}
