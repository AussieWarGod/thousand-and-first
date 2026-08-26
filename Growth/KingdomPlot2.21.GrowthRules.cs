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
		internal static bool TryStampAuthoredGrowth(GameObject Predecessor,
			GameObject Successor, KingdomArchitectureIntent Intent, out string Failure)
		{
			Failure = null;
			ArchitectureLayoutSnapshot snapshot;
			KingdomArchitectureIntent frozen;
			string plotId = Predecessor == null ? null
				: Predecessor.GetStringProperty(PlotIdProperty);
			if (!GameObject.Validate(Predecessor) || !GameObject.Validate(Successor)
				|| Predecessor.CurrentZone == null
				|| string.IsNullOrEmpty(plotId)
				|| !KingdomArchitectureRuntime.TryDecode(Intent, out snapshot, out Failure)
				|| !KingdomArchitectureRules.IsCurrentSnapshotEncoding(Intent.EncodedSnapshot)
				|| !KingdomArchitectureRuntime.TryRead(Successor, out frozen, out Failure)
				|| frozen.SnapshotHash != Intent.SnapshotHash
				|| Successor.CurrentZone != Predecessor.CurrentZone
				|| Successor.CurrentCell != Predecessor.CurrentZone.GetCell(
					Intent.MainWorldX, Intent.MainWorldY))
			{
				if (Failure == null) Failure = "Authored plot growth lacks exact frozen endpoints.";
				return false;
			}
			Zone zone = Successor.CurrentZone;
			bool coveredOnly = false;
			for (int i = 0; i < snapshot.Cells.Count; i++)
				if (snapshot.Cells[i].Claim && snapshot.Cells[i].Cover != ArchitectureCover.Open)
				{
					coveredOnly = true;
					break;
				}
			int x1 = int.MaxValue;
			int y1 = int.MaxValue;
			int x2 = int.MinValue;
			int y2 = int.MinValue;
			KingdomPlotRules.RoofState roof = KingdomPlotRules.RoofState.Open;
			for (int i = 0; i < snapshot.Cells.Count; i++)
			{
				ArchitectureCellState cell = snapshot.Cells[i];
				if (!cell.Claim || (coveredOnly && cell.Cover == ArchitectureCover.Open)) continue;
				int x;
				int y;
				if (!KingdomArchitectureRuntime.TryWorldCell(snapshot, Intent.Rect, cell,
					out x, out y, out Failure)) return false;
				if (x < x1) x1 = x;
				if (y < y1) y1 = y;
				if (x > x2) x2 = x;
				if (y > y2) y2 = y;
				if (cell.Cover == ArchitectureCover.Natural)
					roof = KingdomPlotRules.RoofState.Carved;
				else if (cell.Cover == ArchitectureCover.Walled
					&& roof != KingdomPlotRules.RoofState.Carved)
					roof = KingdomPlotRules.RoofState.Walled;
				else if (cell.Cover == ArchitectureCover.Soft
					&& roof == KingdomPlotRules.RoofState.Open)
					roof = KingdomPlotRules.RoofState.Soft;
			}
			if (x1 == int.MaxValue)
			{
				Failure = "Authored successor has no claimed plot ground.";
				return false;
			}
			KingdomPlotRules.PlotRect footprint = new KingdomPlotRules.PlotRect(x1, y1, x2, y2);
			StampRect(Successor, Intent.Rect);
			StampFootprint(Successor, footprint, roof);
			Successor.SetStringProperty(PlotIdProperty, plotId);
			if (IsHeartPlot(Predecessor)) Successor.SetIntProperty(HeartPlotProperty, 1);
			KingdomPlotRules.PlotRect checkedRect;
			KingdomPlotRules.PlotRect checkedFootprint;
			return TryReadRect(Successor, out checkedRect) && SameRect(checkedRect, Intent.Rect)
				&& TryReadFootprint(Successor, out checkedFootprint)
				&& SameRect(checkedFootprint, footprint) && RoofOf(Successor) == roof
				&& Successor.GetStringProperty(PlotIdProperty) == plotId
				&& (!IsHeartPlot(Predecessor)
					|| Successor.GetIntProperty(HeartPlotProperty) == 1);
		}

		/// <summary>
		/// Whether the next tier has room on the ground this one was staked on, and what the
		/// founder is told when it does not. Two ways it can fail, and each names the thing that
		/// would lift it: the tier wants more ground than the plot holds, or the ground it would
		/// take is where a household's yard trade stands.
		/// <para>
		/// A yard work is never taken down to make room. The founder is told which trade is in the
		/// way and chooses &mdash; let it go, or leave the building as it is &mdash; because the
		/// trade was their decision and tidying it away silently would be the settlement making it
		/// for them.
		/// </para>
		/// </summary>
		/// <param name="Work">The standing work.</param>
		/// <param name="SuccessorKey">The design it would become.</param>
		/// <param name="Refusal">A founder-facing sentence when this returns true; null
		/// otherwise.</param>
		/// <returns>False for anything that is not a plot, for a successor that is not a plot, and
		/// for a tier that has room &mdash; all three of which leave the improvement alone.</returns>
		public static bool GrowRefused(GameObject Work, string SuccessorKey, out string Refusal)
		{
			Refusal = null;
			if (Work == null || string.IsNullOrEmpty(SuccessorKey) || !TryGetSpec(SuccessorKey, out var spec))
			{
				return false;
			}
			if (!TryReadRect(Work, out var plot) || !TryReadFootprint(Work, out var footprint))
			{
				return false;
			}
			if (!KingdomPlotRules.TryFootprint(spec, out var width, out var height))
			{
				return false;
			}
			string name = KingdomDesign.ReferenceFor(Work, Work.ShortDisplayName);
			string successorName = KingdomUpgrade.DisplayNameOf(SuccessorKey);
			Zone zone = Work.CurrentZone;
			if (zone == null)
			{
				return false;
			}
			// The heart is the one plot whose GROUND grows with its rung. Every other design
			// climbs inside the envelope the founder staked; this one was surveyed for its whole
			// extent at the founding rite and takes the next ring of it each time it rises, so the
			// question is not "does the tier fit the plot" but "is the surveyed ground clear".
			if (IsHeartPlot(Work) && KingdomPlotRules.HeartRungOf(SuccessorKey) > 0)
			{
				return HeartGrowRefused(Work, zone, SuccessorKey, successorName, out Refusal);
			}
			HeartFor(zone, plot, out var heartX, out var heartY);
			if (!KingdomPlotRules.TryFootprintWithin(plot, width, height, heartX, heartY, out var grown))
			{
				Refusal = KingdomPlotRules.RefuseFootprint(successorName, width, height,
					KingdomPlotRules.SmallestPlotFor(plot.Width, plot.Height));
				return true;
			}
			if (!KingdomPlotRules.TakesNewGround(footprint, grown))
			{
				return false;
			}
			for (int y = grown.Y1; y <= grown.Y2; y++)
			{
				for (int x = grown.X1; x <= grown.X2; x++)
				{
					if (footprint.Contains(x, y))
					{
						continue;
					}
					Cell cell = zone.GetCell(x, y);
					if (cell == null)
					{
						continue;
					}
					foreach (GameObject item in cell.GetObjects())
					{
						if (item != null && item.GetIntProperty(KingdomYards.YardWorkProperty) == 1)
						{
							Refusal = KingdomPlotRules.RefuseYardWork(name, successorName, item.ShortDisplayNameStripped);
							return true;
						}
					}
				}
			}
			return false;
		}

		/// <summary>Whether one object is the heart's own plot &mdash; the works while it is being
		/// raised, or the building once it stands.</summary>
		public static bool IsHeartPlot(GameObject Object)
		{
			return Object != null && Object.GetIntProperty(HeartPlotProperty) == 1;
		}

		/// <summary>
		/// Whether one plot was staked in ground the heart was surveyed for, and told so at the
		/// time. The mark is a stored fact: this wave informs and steers with it, and the ring
		/// call that moves a yielding plot whole reads exactly this.
		/// </summary>
		public static bool IsYielding(GameObject Object)
		{
			return Object != null && Object.GetIntProperty(YieldingProperty) == 1;
		}

		/// <summary>
		/// Every plot in a zone carrying the yielding mark, works and finished buildings alike, in
		/// the engine's own object order so two reads of an unchanged zone agree.
		/// </summary>
		public static List<GameObject> FindYielding(Zone Z)
		{
			List<GameObject> found = new List<GameObject>();
			if (Z == null)
			{
				return found;
			}
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Z) ?? KingdomSurvey.Take(Z);
			for (int i = 0; i < survey.PlotRoots.Count; i++)
			{
				GameObject item = survey.PlotRoots[i];
				if (IsYielding(item) && TryReadRect(item, out _))
				{
					found.Add(item);
				}
			}
			return found;
		}

		/// <summary>
		/// The ground one rung of the heart would stand on: that rung's tier, centred on the rite
		/// ground and slid whole until it lies inside the ground surveyed at the founding.
		/// </summary>
		/// <returns>False when this zone has no survey, no rite ground, or no room for the
		/// rung.</returns>
		public static bool TryHeartRectFor(Zone Z, int Rung, out KingdomPlotRules.PlotRect Rect)
		{
			Rect = default(KingdomPlotRules.PlotRect);
			if (!TrySurveyedHeart(Z, out var survey) || !TryRiteGround(Z, out var riteX, out var riteY))
			{
				return false;
			}
			return KingdomPlotRules.TryHeartRect(survey, riteX, riteY, KingdomPlotRules.HeartSizeForRung(Rung), out Rect);
		}

		/// <summary>
		/// Whether the heart's next rung has ground to climb into, and the sentence the founder is
		/// owed when it does not. Two things can stand in the way, and both are named: another
		/// plot laid inside the surveyed ground, and anything the settlement may not take down.
		/// <para>
		/// A plot marked YIELDING is exactly the first case, and this is where the mark's promise
		/// comes due &mdash; this wave says so by name and stops. Moving it whole is the ring call,
		/// which waits on the relocation verb.
		/// </para>
		/// </summary>
	}
}
