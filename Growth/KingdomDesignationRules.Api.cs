using System.Collections.Generic;
using ThousandAndFirst.Api;

namespace ThousandAndFirst
{
	/// <summary>
	/// The Api seam for designations. An extension reports <see cref="KingdomApiDesignation"/>
	/// rows; only after every cell is proved inside the zone and unique does one become the
	/// internal row the evaluator reads. Engine-free so the seam is tabled by the pure suite.
	/// </summary>
	public static partial class KingdomDesignationRules
	{
		/// <summary>Every cell an extension reports is open-yard evidence: plot and building
		/// membership only. Covered, interior, and ingress claims never cross the seam.</summary>
		public const KingdomBenefitCellUse ExternalCellUse =
			KingdomBenefitCellUse.Plot | KingdomBenefitCellUse.Building;

		/// <summary>Refuses, never repairs: one out-of-zone or duplicated cell refuses the row.</summary>
		public static bool TryTranslate(KingdomApiDesignation Source, int Width, int Height,
			out KingdomBenefitDesignation Row, out string Failure)
		{
			Row = null; Failure = null;
			if (Source == null) return Fail("designation row is null", out Failure);
			if (Width < 1 || Height < 1) return Fail("designation zone has no extent", out Failure);
			if (Source.Cells == null || Source.Cells.Length < 1
				|| Source.Cells.Length > MaxCellsPerDesignation)
				return Fail("designation row has no bounded exact cells", out Failure);
			KingdomBenefitDesignation row = new KingdomBenefitDesignation {
				ProviderId = Source.ProviderId, ProviderVersion = Source.ProviderVersion,
				Identity = Source.Identity, Revision = Source.Revision, ZoneId = Source.ZoneId,
				RootId = Source.RootId, BuildingKey = Source.BuildingKey, LotId = Source.LotId
			};
			HashSet<long> seen = new HashSet<long>();
			for (int i = 0; i < Source.Cells.Length; i++)
			{
				KingdomApiCell cell = Source.Cells[i];
				if (cell.X < 0 || cell.Y < 0 || cell.X >= Width || cell.Y >= Height)
					return Fail("designation cell " + cell.X + "," + cell.Y
						+ " lies outside the active zone", out Failure);
				if (!seen.Add(Pack(cell.X, cell.Y)))
					return Fail("designation cell " + cell.X + "," + cell.Y + " is duplicated",
						out Failure);
				row.Cells.Add(new KingdomBenefitCell(cell.X, cell.Y, ExternalCellUse));
			}
			Row = row; return true;
		}

		/// <summary>The Api face of an internal row: identity and cells, nothing the seam
		/// does not publish. Null in, null out.</summary>
		public static KingdomApiDesignation ToApi(KingdomBenefitDesignation Source)
		{
			if (Source == null) return null;
			KingdomApiDesignation row = new KingdomApiDesignation {
				ProviderId = Source.ProviderId, ProviderVersion = Source.ProviderVersion,
				Identity = Source.Identity, Revision = Source.Revision, ZoneId = Source.ZoneId,
				RootId = Source.RootId, BuildingKey = Source.BuildingKey, LotId = Source.LotId
			};
			int count = Source.Cells?.Count ?? 0;
			row.Cells = new KingdomApiCell[count];
			for (int i = 0; i < count; i++)
				row.Cells[i] = new KingdomApiCell(Source.Cells[i].X, Source.Cells[i].Y);
			return row;
		}
	}
}
