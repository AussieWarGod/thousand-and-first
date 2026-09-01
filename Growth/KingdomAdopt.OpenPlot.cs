using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomAdopt
	{
		/// <summary>Designates one exact catalogue-sized outdoor plot around a chosen centre.
		/// The marker reserves ground only; live furniture or machinery supplies every benefit.</summary>
		public static bool AdoptOpenPlot(KingdomSystem System, Zone Z, Cell Center,
			string Key, out string Failure)
		{
			Failure = null;
			if (System == null || !System.Founded)
				return FailOpen("You rule nothing yet.", out Failure);
			if (Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
				return FailOpen("An open plot is designated on the kingdom's own ground.", out Failure);
			if (Center == null || Center.ParentZone != Z)
				return FailOpen("Choose the centre of the open plot.", out Failure);
			if (!KingdomData.TryGetBuilding(Key, out KingdomRules.BuildEntry entry))
				return FailOpen("No such design.", out Failure);
			if (!entry.Adoptable)
				return FailOpen("A " + entry.Name
					+ " needs authored construction and cannot be assigned to open ground.",
					out Failure);
			if (!KingdomPlots.TryGetSpec(entry.Key, out KingdomPlotRules.PlotSpec spec)
				|| !KingdomAdoptabilityRules.TryClassify(entry.Key, entry.Category,
					spec.Size, spec.Open, out KingdomAdoptionTargetKind target, out Failure)
				|| target != KingdomAdoptionTargetKind.OpenPlot)
				return Failure != null ? false
					: FailOpen("That design has no exact open-plot contract.", out Failure);
			if (!KingdomZoning.Permits(System, Z.ZoneID, entry, out Failure)) return false;
			if (System.Stage < entry.MinStage)
				return FailOpen(KingdomPresentation.Rich(System.SeatName)
					+ " hasn't grown enough yet to keep a " + entry.Name + " standing.",
					out Failure);
			if (HasAdoptionMarker(Center))
				return FailOpen("Something at that centre already serves the settlement.", out Failure);
			if (!KingdomAdoptionPlotRules.TryCenteredCells(Center.X, Center.Y, spec.Size,
				Z.Width, Z.Height, out _, out List<ArchitecturePoint> cells, out Failure)
				|| !CellsAreUnclaimed(Z, cells, out Failure)) return false;

			GameObject marker = GameObject.Create(WorkMarkerBlueprint);
			if (!GameObject.Validate(marker))
				return FailOpen("The civic marker could not be prepared.", out Failure);
			Center.AddObject(marker);
			if (marker.CurrentCell != Center)
			{
				marker.Obliterate(null, Silent: true);
				return FailOpen("The civic marker could not be set down.", out Failure);
			}
			if (!BeginPending(marker, Key, true, out Failure))
			{
				if (GameObject.Validate(marker)) marker.Obliterate(null, Silent: true);
				return false;
			}
			if (!KingdomAdoptionDesignation.TryStampOpenPlot(marker, Z, Key, cells,
				out KingdomAdoptionDesignationReceipt receipt, out Failure)
				|| !AdvancePending(marker, Key, 2, out Failure)
				|| !KingdomPlots.StampAdoptedExact(marker, entry, cells)
				|| !AdvancePending(marker, Key, 3, out Failure)
				|| !ReproveOpenPlotForCommit(System, Z, marker, entry, receipt, out Failure)
				|| !AdvancePending(marker, Key, 4, out Failure)
				|| !FinalizePending(marker, Key, "", out Failure))
			{
				RollbackPending(marker);
				if (Failure == null) Failure = "The open plot could not be recorded exactly.";
				return false;
			}
			AnnounceAdoption(System, entry, marker); return true;
		}

		private static bool ReproveOpenPlotForCommit(KingdomSystem System, Zone Z,
			GameObject Marker, KingdomRules.BuildEntry Entry,
			KingdomAdoptionDesignationReceipt Receipt, out string Failure)
		{
			Failure = null;
			if (!GameObject.Validate(Marker) || Marker.CurrentZone != Z
				|| Marker.CurrentCell == null || Receipt == null || !Receipt.OpenPlot
				|| !KingdomZoning.Permits(System, Z.ZoneID, Entry, out Failure)
				|| !KingdomAdoptionDesignation.TryReproveLocal(Marker, Receipt, out Failure))
				return false;
			return CellsAreUnclaimed(Z, Receipt.Cells, out Failure);
		}

		private static bool CellsAreUnclaimed(Zone Z,
			IReadOnlyList<ArchitecturePoint> Cells, out string Failure)
		{
			Failure = null;
			if (Z == null || Cells == null || Cells.Count < 1)
				return FailOpen("The designation has no exact ground.", out Failure);
			if (!KingdomDesignationIndex.TryActiveZone(Z, out KingdomDesignationIndex index,
				out Failure)) return false;
			for (int i = 0; i < Cells.Count; i++)
				if (index.Containing(Cells[i].X, Cells[i].Y,
					KingdomBenefitScope.Plot).Count > 0)
					return FailOpen("This ground overlaps another exact building designation.",
						out Failure);
			return true;
		}

		private static bool FailOpen(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
