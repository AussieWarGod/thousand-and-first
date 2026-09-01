using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	internal static partial class KingdomDesignationSources
	{
		internal static bool TryAuthored(Zone Z, KingdomSurvey Survey,
			List<KingdomBenefitDesignation> Rows, List<string> Faults, out string Failure)
		{
			Failure = null;
			if (Z == null || Survey == null || Rows == null || Faults == null)
				return Fail("authored designation source has no active survey", out Failure);
			for (int i = 0; i < Survey.Built.Count; i++)
			{
				GameObject root = Survey.Built[i];
				if (root.GetIntProperty(KingdomArchitectureStamper.SchemaProperty)
					!= KingdomArchitectureStamper.LayoutSchema) continue;
				if (!KingdomArchitectureStamper.TryExactLayoutOwner(root, Z,
					out KingdomArchitectureIntent intent, out ArchitectureLayoutSnapshot snapshot,
					out string lot, out string rowFailure))
				{
					SourceFault(Faults, "authored designation refused: " + rowFailure); continue;
				}
				if (!KingdomArchitectureRules.IsLatestSnapshotEncoding(intent.EncodedSnapshot))
				{
					SourceFault(Faults, "authored building has no current exact-cell architecture: " + lot);
					continue;
				}
				KingdomBenefitDesignation row = new KingdomBenefitDesignation {
					ProviderId = "taf.architecture", ProviderVersion = "a4", Identity = lot,
					Revision = intent.SnapshotHash, ZoneId = Z.ZoneID, RootId = root.IDIfAssigned,
					BuildingKey = intent.BuildKey, LotId = lot
				};
				if (!TryAuthoredCells(intent, snapshot, out row.Cells, out rowFailure)
					|| !KingdomDesignationIndex.CompleteForSource(row, Z, out rowFailure))
				{
					SourceFault(Faults, "authored designation " + lot + " refused: " + rowFailure);
					continue;
				}
				if (!SourceRow(Rows, row, Faults)) break;
			}
			return true;
		}

		private static bool TryAuthoredCells(KingdomArchitectureIntent Intent,
			ArchitectureLayoutSnapshot Snapshot, out List<KingdomBenefitCell> Cells,
			out string Failure)
		{
			Cells = new List<KingdomBenefitCell>(); Failure = null;
			Dictionary<long, KingdomBenefitCell> cells =
				new Dictionary<long, KingdomBenefitCell>();
			for (int y = Intent.Rect.Y1; y <= Intent.Rect.Y2; y++)
				for (int x = Intent.Rect.X1; x <= Intent.Rect.X2; x++)
					cells[KingdomDesignationRules.Pack(x, y)] = new KingdomBenefitCell(x, y,
						KingdomBenefitCellUse.Plot, KingdomBenefitCover.Open);
			for (int i = 0; i < Snapshot.Cells.Count; i++)
			{
				ArchitectureCellState source = Snapshot.Cells[i];
				int x; int y;
				if (!KingdomArchitectureRuntime.TryWorldCell(Snapshot, Intent.Rect, source,
					out x, out y, out Failure)) return false;
				long key = KingdomDesignationRules.Pack(x, y);
				KingdomBenefitCell existing;
				cells.TryGetValue(key, out existing);
				KingdomBenefitCellUse use = existing.Use | KingdomBenefitCellUse.Plot;
				if (source.Claim == ArchitectureClaim.Building)
				{
					use |= KingdomBenefitCellUse.Building;
				}
				else if (source.Claim == ArchitectureClaim.Yard)
					use |= KingdomBenefitCellUse.Yard;
				KingdomBenefitCover cover = CoverOf(source.Cover);
					if (cover != KingdomBenefitCover.Open)
					{
						use |= KingdomBenefitCellUse.Covered;
						if (KingdomBenefitEmbodimentRules.AuthoredInterior(
							(use & KingdomBenefitCellUse.Building) != 0, true,
							source.Passability == ArchitecturePassability.Blocked))
							use |= KingdomBenefitCellUse.Interior;
					}
				cells[key] = new KingdomBenefitCell(x, y, use, cover);
			}
			foreach (KeyValuePair<long, KingdomBenefitCell> pair in cells)
				Cells.Add(pair.Value);
			return true;
		}

		private static KingdomBenefitCover CoverOf(ArchitectureCover Cover)
		{
			switch (Cover)
			{
			case ArchitectureCover.Soft: return KingdomBenefitCover.Soft;
			case ArchitectureCover.Walled: return KingdomBenefitCover.Walled;
			case ArchitectureCover.Natural: return KingdomBenefitCover.Natural;
			default: return KingdomBenefitCover.Open;
			}
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}

		private static bool SourceRow(List<KingdomBenefitDesignation> Rows,
			KingdomBenefitDesignation Row, List<string> Faults)
		{
			long cells = Row?.Cells?.Count ?? 0;
			for (int i = 0; i < Rows.Count; i++) cells += Rows[i].Cells?.Count ?? 0;
			if (Rows.Count >= KingdomDesignationRules.MaxDesignationsPerZone
				|| Row?.Cells == null || Row.Cells.Count < 1
				|| Row.Cells.Count > KingdomDesignationRules.MaxCellsPerDesignation
				|| cells > KingdomDesignationRules.MaxCellsPerZoneIndex)
			{
				SourceFault(Faults, "trusted designation sources exceeded their aggregate bound");
				return false;
			}
			Rows.Add(Row); return true;
		}

		private static void SourceFault(List<string> Faults, string Message)
		{
			if (Faults.Count >= KingdomDesignationRules.MaxSourceFaults) return;
			Faults.Add(string.IsNullOrEmpty(Message) ? "unspecified trusted source fault"
				: Message.Length <= 512 ? Message : Message.Substring(0, 512));
		}
	}
}
