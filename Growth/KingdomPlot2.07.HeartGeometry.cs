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
		/// <summary>
		/// What stands over a plot. Derived rather than defaulted when nothing was stamped: the
		/// open and carved flags a works part already carries are the same three states roofs
		/// name, so a plot staked before roofs existed reads as exactly what it was staked as.
		/// </summary>
		public static KingdomPlotRules.RoofState RoofOf(GameObject Object)
		{
			if (Object == null)
			{
				return KingdomPlotRules.RoofState.Walled;
			}
			if (Object.HasIntProperty(PlotRoofProperty))
			{
				return (KingdomPlotRules.RoofState)Object.GetIntProperty(PlotRoofProperty);
			}
			r_KingdomPlotWorks works = Object.GetPart<r_KingdomPlotWorks>();
			if (works == null)
			{
				return KingdomPlotRules.RoofState.Walled;
			}
			return KingdomPlotRules.RoofOnGround(KingdomPlotRules.DefaultRoof(works.Open), works.Carved);
		}

		/// <summary>
		/// The heart this plot faces, which decides where its door is cut and which side of the
		/// plot the building fronts. A zone with no heart yet faces its own centre, so a first
		/// building is never sited by an answer nobody gave.
		/// </summary>
		public static void HeartFor(Zone Z, KingdomPlotRules.PlotRect Plot, out int X, out int Y)
		{
			bool hasRite = TryRiteGround(Z, out var riteX, out var riteY);
			// The rite ground's own weight rises with the rung standing on it, so the settled
			// centre is drawn back onto the great work as it rises rather than walking away from
			// it (KingdomPlotRules.HeartWeightForRung).
			if (KingdomPlotRules.TryHeart(KingdomLayout.ReadMarks(Z), hasRite, riteX, riteY, out var heartX, out var heartY, RiteWeight(Z)))
			{
				X = heartX;
				Y = heartY;
				return;
			}
			X = Plot.CenterX;
			Y = Plot.CenterY;
		}

		/// <summary>
		/// The ground one tier stands on inside a staked plot: the design's own footprint, sited
		/// against the heart-facing side so the yard lies behind the building. A tier that
		/// declares none fills the plot, exactly as every design did before footprints existed.
		/// </summary>
		public static KingdomPlotRules.PlotRect FootprintFor(KingdomPlotRules.PlotRect Plot, KingdomPlotRules.PlotSpec Spec, int HeartX, int HeartY)
		{
			if (Spec != null && !Spec.FillsPlot
				&& KingdomPlotRules.TryFootprintWithin(Plot, Spec.FootprintWidth, Spec.FootprintHeight, HeartX, HeartY, out var footprint))
			{
				return footprint;
			}
			return Plot;
		}

		/// <summary>
		/// The ground one rung of the heart stands on inside its plot: the tier's own footprint,
		/// centred on the RITE GROUND rather than sited against the heart-facing side.
		/// <para>
		/// This is what makes the rungs accrete. The ordinary rule puts a building on the side of
		/// its plot nearest the settled centre, which is right for a house and wrong for the one
		/// building the settled centre is measured from: a rung sited off-centre would not enclose
		/// the rung below it, and the kerb would end up outside the hall instead of under its
		/// floor. Centred on the rite ground, every rung contains the one before it, and the basin
		/// stays in the middle of all of them.
		/// </para>
		/// </summary>
		public static KingdomPlotRules.PlotRect HeartFootprintFor(Zone Z, KingdomPlotRules.PlotRect Plot, KingdomPlotRules.PlotSpec Spec)
		{
			if (Spec != null && !Spec.FillsPlot && TryRiteGround(Z, out var riteX, out var riteY)
				&& KingdomPlotRules.TryCentred(Plot, riteX, riteY, Spec.FootprintWidth, Spec.FootprintHeight, out var footprint))
			{
				return footprint;
			}
			return FootprintFor(Plot, Spec, Plot.CenterX, Plot.CenterY);
		}

		/// <summary>
		/// The yard of a standing plot: everything inside the plot the building does not stand on,
		/// recomputed from the current tier every time it is asked rather than stored anywhere.
		/// A tier that fills its plot has no yard OUTSIDE it, so the answer falls back to the
		/// building's own interior &mdash; which is the ground yard trades have always used, and
		/// is why nothing already standing changes.
		/// </summary>
		public static List<KingdomPlotRules.PlotRect> YardRects(GameObject Building)
		{
			List<KingdomPlotRules.PlotRect> bands = new List<KingdomPlotRules.PlotRect>();
			if (Building == null || !TryReadRect(Building, out var plot) || !TryReadFootprint(Building, out var footprint))
			{
				return bands;
			}
			bands = KingdomPlotRules.YardBands(plot, footprint);
			if (bands.Count == 0 && KingdomYardRules.TryYardInterior(footprint, out var interior))
			{
				bands.Add(interior);
			}
			return bands;
		}

		/// <summary>The rite ground of this zone, if it was recorded when the rite was poured.</summary>
		public static bool TryRiteGround(Zone Z, out int X, out int Y)
		{
			X = 0;
			Y = 0;
			if (Z == null)
			{
				return false;
			}
			return int.TryParse(Z.GetZoneProperty(RiteXProperty, null), out X)
				&& int.TryParse(Z.GetZoneProperty(RiteYProperty, null), out Y);
		}

		// --- The heart's own ground -------------------------------------------------------

		/// <summary>The ground this zone's heart was surveyed for at the founding rite.</summary>
		/// <returns>False for a zone with no survey, which is every zone but the one the rite was
		/// poured in and every settlement founded before the survey shipped.</returns>
		public static bool TrySurveyedHeart(Zone Z, out KingdomPlotRules.PlotRect Survey)
		{
			Survey = default(KingdomPlotRules.PlotRect);
			if (Z == null)
			{
				return false;
			}
			if (!int.TryParse(Z.GetZoneProperty(SurveyX1Property, null), out var x1)
				|| !int.TryParse(Z.GetZoneProperty(SurveyY1Property, null), out var y1)
				|| !int.TryParse(Z.GetZoneProperty(SurveyX2Property, null), out var x2)
				|| !int.TryParse(Z.GetZoneProperty(SurveyY2Property, null), out var y2))
			{
				return false;
			}
			Survey = new KingdomPlotRules.PlotRect(x1, y1, x2, y2);
			return true;
		}

		/// <summary>Which rung of the heart stands on this zone's rite ground, one-based; zero
		/// when nothing has been raised there yet.</summary>
		public static int HeartRung(Zone Z)
		{
			if (Z == null || !int.TryParse(Z.GetZoneProperty(HeartRungProperty, null), out var rung))
			{
				return 0;
			}
			return (rung < 0) ? 0 : rung;
		}

		/// <summary>How many votes this zone's rite ground gets when the heart is reckoned: one
		/// on bare ground, and the standing rung's own weight once the great work is on it.</summary>
		public static int RiteWeight(Zone Z)
		{
			return KingdomPlotRules.HeartWeightForRung(HeartRung(Z));
		}

		/// <summary>
		/// Paces out the heart's whole future extent at the founding rite and stakes its first
		/// rung on the ground the water was poured on.
		/// <para>
		/// The survey COSTS NOTHING and CLAIMS NOTHING. It stamps four corner stakes a founder can
		/// walk up to and read, and a rect the layout grammar reads as a preference
		/// (<c>KingdomPlotRules.SurveyPenalty</c>) &mdash; the settlement will not volunteer to
		/// build in the heart's ground, and it never refuses to. A plot staked there anyway is
		/// marked yielding at placement and says so forever.
		/// </para>
		/// </summary>
		/// <param name="System">The realm, freshly founded.</param>
		/// <param name="Z">The zone the rite was poured in.</param>
		/// <param name="RiteX">Rite ground x.</param>
		/// <param name="RiteY">Rite ground y.</param>
		/// <returns>False when the zone has no interior wide enough to survey a great plot in, in
		/// which case nothing is stamped and the settlement simply has no surveyed heart.</returns>
		public static bool SurveyHeart(KingdomSystem System, Zone Z, int RiteX, int RiteY)
		{
			if (Z == null || !KingdomPlotRules.TrySurveyedHeart(RiteX, RiteY, Z.Width, Z.Height, out var survey))
			{
				return false;
			}
			Z.SetZoneProperty(SurveyX1Property, survey.X1.ToString());
			Z.SetZoneProperty(SurveyY1Property, survey.Y1.ToString());
			Z.SetZoneProperty(SurveyX2Property, survey.X2.ToString());
			Z.SetZoneProperty(SurveyY2Property, survey.Y2.ToString());
			PlaceHeartMark(Z, RiteX, RiteY, HeartRelicBlueprint, HeartRelicProperty);
			PlaceHeartMark(Z, survey.X1, survey.Y1, SurveyStakeBlueprint, HeartStakeProperty);
			PlaceHeartMark(Z, survey.X2, survey.Y1, SurveyStakeBlueprint, HeartStakeProperty);
			PlaceHeartMark(Z, survey.X1, survey.Y2, SurveyStakeBlueprint, HeartStakeProperty);
			PlaceHeartMark(Z, survey.X2, survey.Y2, SurveyStakeBlueprint, HeartStakeProperty);
			KingdomLog.Log("heart surveyed: " + survey.X1 + "," + survey.Y1 + " to " + survey.X2 + "," + survey.Y2
				+ " around rite " + RiteX + "," + RiteY);
			MessageQueue.AddPlayerMessage("{{W|" + KingdomPlotRules.SurveyLine(survey) + "}}");
			StakeHeartRung(System, Z, 1, survey, RiteX, RiteY);
			return true;
		}

		/// <summary>Sets one stake or the basin down on ground, marked so the plot machinery reads
		/// it as bare. Silently does nothing where the engine will not take the object, which is
		/// the honest answer for a mark nobody can see.</summary>
		private static void PlaceHeartMark(Zone Z, int X, int Y, string Blueprint, string Mark)
		{
			Cell cell = Z?.GetCell(X, Y);
			if (cell == null)
			{
				return;
			}
			GameObject placed = GameObject.Create(Blueprint);
			if (placed == null)
			{
				return;
			}
			placed.SetIntProperty(Mark, 1);
			GameObject accepted = null;
			try { accepted = cell.AddObject(placed); }
			finally { KingdomSurvey.ObserveAddResultInActive(Z, placed, accepted); }
		}

	}
}
