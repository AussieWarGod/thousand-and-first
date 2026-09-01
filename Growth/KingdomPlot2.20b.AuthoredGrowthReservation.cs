using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Durable plot-envelope publication for paid authored renovations.</summary>
	public static partial class KingdomPlots
	{
		/// <summary>
		/// Publishes the successor's exact reserved ground before any standing scenery is removed.
		/// Every scalar is write-once for this transaction: an interrupted exact prefix may be
		/// completed, while a third value is reported as divergence and must be quarantined by the
		/// upgrade owner.
		/// </summary>
		internal static bool TryReserveAuthoredGrowthEnvelope(GameObject Predecessor,
			GameObject Successor, KingdomArchitectureIntent Intent, out bool Divergent,
			out string Failure)
		{
			Divergent = false;
			Failure = null;
			string plotId = Predecessor == null ? null
				: Predecessor.GetStringProperty(PlotIdProperty);
			Zone zone = Predecessor == null ? null : Predecessor.CurrentZone;
			KingdomPlotRules.PlotRect standing;
			if (!GameObject.Validate(Predecessor) || !GameObject.Validate(Successor)
				|| Intent == null || zone == null || Successor.CurrentZone != zone
				|| Successor.CurrentCell != zone.GetCell(Intent.MainWorldX, Intent.MainWorldY)
				|| string.IsNullOrEmpty(plotId) || !TryReadRect(Predecessor, out standing)
				|| Intent.Rect.X1 < 0 || Intent.Rect.Y1 < 0
				|| Intent.Rect.X2 >= zone.Width || Intent.Rect.Y2 >= zone.Height
				|| Intent.Rect.X1 > Intent.Rect.X2 || Intent.Rect.Y1 > Intent.Rect.Y2)
			{
				Failure = "Authored plot-envelope reservation lacks exact bounded endpoints.";
				return false;
			}
			if (!ExactOrAbsentString(Successor, PlotIdProperty, plotId)
				|| !ExactOrAbsentInt(Successor, PlotX1Property, Intent.Rect.X1)
				|| !ExactOrAbsentInt(Successor, PlotY1Property, Intent.Rect.Y1)
				|| !ExactOrAbsentInt(Successor, PlotX2Property, Intent.Rect.X2)
				|| !ExactOrAbsentInt(Successor, PlotY2Property, Intent.Rect.Y2)
				|| !ExactOrAbsentHeart(Successor, IsHeartPlot(Predecessor)))
			{
				Divergent = true;
				Failure = "Authored successor carries foreign or changed plot-envelope state.";
				return false;
			}
			try
			{
				Successor.SetStringProperty(PlotIdProperty, plotId);
				Successor.SetIntProperty(PlotX1Property, Intent.Rect.X1);
				Successor.SetIntProperty(PlotY1Property, Intent.Rect.Y1);
				Successor.SetIntProperty(PlotX2Property, Intent.Rect.X2);
				Successor.SetIntProperty(PlotY2Property, Intent.Rect.Y2);
				if (IsHeartPlot(Predecessor)) Successor.SetIntProperty(HeartPlotProperty, 1);
			}
			catch (System.Exception exception)
			{
				Failure = "Authored plot-envelope publication remains retryable: "
					+ exception.Message;
				return false;
			}
			KingdomPlotRules.PlotRect observed;
			if (!TryReadRect(Successor, out observed) || !SameRect(observed, Intent.Rect)
				|| Successor.GetStringProperty(PlotIdProperty) != plotId
				|| (IsHeartPlot(Predecessor) && !IsHeartPlot(Successor)))
			{
				Divergent = true;
				Failure = "Authored plot-envelope publication did not read back exactly.";
				return false;
			}
			return true;
		}

		private static bool ExactOrAbsentInt(GameObject Object, string Property, int Expected)
		{
			return !Object.HasStringProperty(Property)
				&& (!Object.HasIntProperty(Property)
					|| Object.GetIntProperty(Property) == Expected);
		}

		private static bool ExactOrAbsentString(GameObject Object, string Property,
			string Expected)
		{
			return !Object.HasIntProperty(Property)
				&& (!Object.HasStringProperty(Property)
					|| Object.GetStringProperty(Property) == Expected);
		}

		private static bool ExactOrAbsentHeart(GameObject Object, bool Expected)
		{
			if (Object.HasStringProperty(HeartPlotProperty)) return false;
			if (!Object.HasIntProperty(HeartPlotProperty)) return true;
			return Object.GetIntProperty(HeartPlotProperty) == (Expected ? 1 : 0);
		}
	}
}
