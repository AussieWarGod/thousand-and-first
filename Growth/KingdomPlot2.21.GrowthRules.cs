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
				|| !KingdomArchitectureRules.IsManagedSnapshotEncoding(Intent.EncodedSnapshot)
				|| !KingdomArchitectureRuntime.TryRead(Successor, out frozen, out Failure)
				|| frozen.SnapshotHash != Intent.SnapshotHash
				|| Successor.CurrentZone != Predecessor.CurrentZone
				|| Successor.CurrentCell != Predecessor.CurrentZone.GetCell(
					Intent.MainWorldX, Intent.MainWorldY))
			{
				if (Failure == null) Failure = "Authored plot growth lacks exact frozen endpoints.";
				return false;
			}
			Zone zone = Predecessor.CurrentZone;
			bool latest = KingdomArchitectureRules.IsLatestSnapshotEncoding(
				Intent.EncodedSnapshot);
			KingdomPlotRules.PlotRect footprint;
			KingdomPlotRules.RoofState roof;
			if (latest)
			{
				KingdomArchitectureIntent beforeIntent;
				KingdomPlotRules.PlotRect expectedBefore;
				KingdomPlotRules.PlotRect standingBefore;
				KingdomPlotRules.RoofState expectedBeforeRoof;
				if (!KingdomArchitectureRuntime.TryRead(Predecessor, out beforeIntent, out Failure)
					|| !KingdomArchitectureRules.IsLatestSnapshotEncoding(
						beforeIntent.EncodedSnapshot)
					|| !KingdomArchitectureRuntime.TryWorldFootprint(beforeIntent,
						out expectedBefore, out Failure)
					|| !KingdomArchitectureRuntime.TryRoofOnGround(beforeIntent,
						KingdomPlotRules.IsUnderground(zone.Z), out expectedBeforeRoof,
						out Failure)
					|| !TryReadFootprint(Predecessor, out standingBefore)
					|| !SameRect(expectedBefore, standingBefore)
					|| RoofOf(Predecessor) != expectedBeforeRoof)
				{
					if (Failure == null) Failure =
						"Standing authored building no longer matches its frozen footprint or roof.";
					return false;
				}
				if (!KingdomArchitectureRuntime.TryWorldFootprint(Intent, out footprint,
						out Failure)
					|| !KingdomArchitectureRuntime.TryRoofOnGround(Intent,
						KingdomPlotRules.IsUnderground(zone.Z), out roof, out Failure)) return false;
			}
			else if (!TryLegacyManagedGrowthTruth(snapshot, Intent, out footprint,
				out roof, out Failure)) return false;
			if (!TryReserveAuthoredGrowthEnvelope(Predecessor, Successor, Intent,
				out bool divergentEnvelope, out Failure))
			{
				if (divergentEnvelope)
					KingdomArchitectureStamper.TryQuarantineUpgrade(Predecessor, Failure,
						out Failure);
				return false;
			}
			if (!ExactOrAbsentInt(Successor, FootX1Property, footprint.X1)
				|| !ExactOrAbsentInt(Successor, FootY1Property, footprint.Y1)
				|| !ExactOrAbsentInt(Successor, FootX2Property, footprint.X2)
				|| !ExactOrAbsentInt(Successor, FootY2Property, footprint.Y2)
				|| !ExactOrAbsentInt(Successor, PlotRoofProperty, (int)roof))
			{
				Failure = "Authored successor carries foreign or changed footprint state.";
				KingdomArchitectureStamper.TryQuarantineUpgrade(Predecessor, Failure,
					out Failure);
				return false;
			}
			try
			{
				StampFootprint(Successor, footprint, roof);
			}
			catch (System.Exception exception)
			{
				Failure = "Authored footprint publication remains retryable: "
					+ exception.Message;
				return false;
			}
			KingdomPlotRules.PlotRect checkedRect;
			KingdomPlotRules.PlotRect checkedFootprint;
			bool exact = TryReadRect(Successor, out checkedRect)
				&& SameRect(checkedRect, Intent.Rect)
				&& TryReadFootprint(Successor, out checkedFootprint)
				&& SameRect(checkedFootprint, footprint) && RoofOf(Successor) == roof
				&& Successor.GetStringProperty(PlotIdProperty) == plotId
				&& (!IsHeartPlot(Predecessor)
					|| Successor.GetIntProperty(HeartPlotProperty) == 1);
			if (!exact)
				KingdomArchitectureStamper.TryQuarantineUpgrade(Predecessor,
					"Authored plot-growth metadata did not settle exactly.", out Failure);
			return exact;
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
				if (!r_KingdomScaffold.HasPendingImprovementSuccessorAuthority(item)
					&& IsYielding(item) && TryReadRect(item, out _))
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
