using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Genkit;
using Hearthpyre;
using Hearthpyre.Realm;
using ThousandAndFirst.Api;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Integrations.Hearthpyre223
{
	/// <summary>Exact 2.2.3 Home translator. Reads only the active zone's existing registries;
	/// never creates, removes, flushes, loads, or otherwise changes Hearthpyre state.</summary>
	[KingdomForeignFootprintProvider]
	public sealed class KingdomHearthpyreFootprintProvider
		: IKingdomForeignFootprintProvider
	{
		public string ProviderId => "Hearthpyre";
		public string ProviderVersion => "2.2.3";

		public bool TryObserve(Zone ActiveZone, out KingdomForeignFootprint[] Footprints,
			out string Failure)
		{
			Footprints = null; Failure = null;
			if (ActiveZone == null || !ReferenceEquals(The.ZoneManager.ActiveZone, ActiveZone))
				return Fail("footprints require the exact loaded active zone", out Failure);
			try
			{
				KingdomHearthpyreFootprintScanBudget budget =
					new KingdomHearthpyreFootprintScanBudget();
				if (!KingdomHearthpyreFootprintCustody.TryResolveSector(ActiveZone, budget,
					out Sector sector, out bool absent, out Failure)) return false;
				if (absent) return false;
				if (!KingdomHearthpyreFootprintCustody.TrySnapshotHomes(
					sector, budget, out Home[] homes, out Failure)) return false;
				List<KingdomForeignFootprint> rows = new List<KingdomForeignFootprint>();
				for (int i = 0; i < homes.Length; i++)
				{
					if (!TryObserveHome(ActiveZone, sector, homes[i], budget,
						out KingdomForeignFootprint row, out Failure)) return false;
					rows.Add(row);
				}
				if (!KingdomHearthpyreFootprintCustody.TryResolveSector(ActiveZone, budget,
					out Sector reproved, out absent, out Failure) || absent
					|| !ReferenceEquals(reproved, sector)
					|| !KingdomHearthpyreFootprintCustody.TrySnapshotHomes(
						reproved, budget, out Home[] checkHomes, out Failure)
					|| !SameRoster(homes, checkHomes))
					return Failure != null ? false
						: Fail("sector custody changed while observed", out Failure);
				for (int i = 0; i < homes.Length; i++)
				{
					if (!TryObserveHome(ActiveZone, sector, homes[i], budget,
						out KingdomForeignFootprint check, out Failure)) return false;
					if (!SameRow(rows[i], check))
						return Fail("Home membership changed while observed", out Failure);
				}
				rows.Sort((a, b) => string.CompareOrdinal(a.Identity, b.Identity));
				Footprints = rows.ToArray(); return true;
			}
			catch (Exception exception)
			{
				Footprints = null;
				return Fail("Home observation threw " + exception.GetType().Name, out Failure);
			}
		}

		/// <summary>One damaged Home is row-local. Exact cells become refusal evidence; when
		/// membership itself cannot be proved, a no-cell row lets the host retain a bounded
		/// diagnostic without inventing ground. Sector/roster churn is still handled by the
		/// provider-wide proof surrounding these calls.</summary>
		private static bool TryObserveHome(Zone ActiveZone, Sector Sector, Home Home,
			KingdomHearthpyreFootprintScanBudget Budget,
			out KingdomForeignFootprint Row, out string Failure)
		{
			Row = null; Failure = null;
			try
			{
				bool owned = KingdomHearthpyreFootprintCustody.TryProveHome(
					Sector, Home, Budget, out string custodyFailure);
				if (!owned && Budget.Exhausted)
					return Fail(custodyFailure
						?? KingdomHearthpyreFootprintScanBudget.LimitFailure, out Failure);
				if (!TryCells(ActiveZone, Home, Budget,
					out List<ArchitecturePoint> cells, out string cellFailure))
				{
					if (Budget.Exhausted) return Fail(cellFailure
						?? KingdomHearthpyreFootprintScanBudget.LimitFailure, out Failure);
					Row = DiagnosticRow(ActiveZone, Sector, Home, cellFailure); return true;
				}
				KingdomForeignFootprint row = BuildRow(ActiveZone, Sector, Home, cells);
				if (!owned) row.Refusal = BoundFault(custodyFailure);
				Row = row; return true;
			}
			catch (Exception exception)
			{
				if (Budget?.Exhausted == true)
					return Fail(KingdomHearthpyreFootprintScanBudget.LimitFailure, out Failure);
				Row = DiagnosticRow(ActiveZone, Sector, Home,
					"Home observation threw " + exception.GetType().Name); return true;
			}
		}

		private static KingdomForeignFootprint DiagnosticRow(Zone ActiveZone, Sector Sector,
			Home Home, string Failure)
		{
			string identity = "";
			try { if (Home != null && Home.ID != Guid.Empty) identity = Home.ID.ToString("D"); }
			catch { }
			return new KingdomForeignFootprint {
				ProviderId = "Hearthpyre", ProviderVersion = "2.2.3", Identity = identity,
				Revision = "fault", Refusal = BoundFault(Failure),
				ZoneId = ActiveZone?.ZoneID ?? "", SectorId = SectorId(Sector),
				DeclaredCount = 0, Cells = null
			};
		}

		private static bool TryCells(Zone ActiveZone, Home Home,
			KingdomHearthpyreFootprintScanBudget Budget,
			out List<ArchitecturePoint> Cells, out string Failure)
		{
			Cells = new List<ArchitecturePoint>(); Failure = null;
			if (Home == null || Home.Origin == null || Home.Count < 1
				|| Home.Count > KingdomDesignationRules.MaxCellsPerDesignation)
				return Fail("Home origin or count is incomplete or over-bound", out Failure);
			if (Budget == null || !Budget.TryCharge(Home.Count))
				return Fail(KingdomHearthpyreFootprintScanBudget.LimitFailure, out Failure);
			HashSet<long> seen = new HashSet<long>(); int enumerated = 0;
			foreach (Location2D location in Home)
			{
				if (location == null || enumerated >= KingdomDesignationRules.MaxCellsPerDesignation
					|| location.X < 0 || location.Y < 0
					|| location.X >= ActiveZone.Width || location.Y >= ActiveZone.Height)
					return Fail("Home enumeration is over-bound or off-zone", out Failure);
				enumerated++;
				if (!seen.Add(Pack(location.X, location.Y)))
					return Fail("Home enumeration contains duplicate ground", out Failure);
				Cells.Add(new ArchitecturePoint(location.X, location.Y));
			}
			Cells.Sort(Compare);
			if (Cells.Count != Home.Count || enumerated != Home.Count
				|| !seen.Contains(Pack(Home.Origin.X, Home.Origin.Y)))
				return Fail("Home count, enumeration, and origin do not agree", out Failure);
			return true;
		}

		private static KingdomForeignFootprint BuildRow(Zone ActiveZone, Sector Sector,
			Home Home, List<ArchitecturePoint> Cells)
		{
			string sectorId = SectorId(Sector); string identity = Home.ID.ToString("D");
			return new KingdomForeignFootprint {
				ProviderId = "Hearthpyre", ProviderVersion = "2.2.3",
				Identity = identity, ZoneId = ActiveZone.ZoneID, SectorId = sectorId,
				Refusal = "", DeclaredCount = Cells.Count,
				OriginX = Home.Origin.X, OriginY = Home.Origin.Y, Cells = ApiCells(Cells),
				Revision = Digest(ActiveZone.ZoneID, sectorId, identity,
					Home.Origin.X, Home.Origin.Y, Cells)
			};
		}

		private static bool SameRoster(Home[] Source, Home[] Snapshot)
		{
			if (Source == null || Snapshot == null || Source.Length != Snapshot.Length) return false;
			for (int i = 0; i < Snapshot.Length; i++)
				if (!ReferenceEquals(Source[i], Snapshot[i])) return false;
			return true;
		}

		private static bool SameRow(KingdomForeignFootprint A, KingdomForeignFootprint B)
		{
			if (A == null || B == null || A.ProviderId != B.ProviderId
				|| A.ProviderVersion != B.ProviderVersion || A.Identity != B.Identity
				|| A.Revision != B.Revision || A.ZoneId != B.ZoneId || A.SectorId != B.SectorId
				|| A.Refusal != B.Refusal
				|| A.OriginX != B.OriginX || A.OriginY != B.OriginY
				|| A.DeclaredCount != B.DeclaredCount || (A.Cells == null) != (B.Cells == null)
				|| (A.Cells != null && A.Cells.Length != B.Cells.Length)) return false;
			if (A.Cells == null) return true;
			for (int i = 0; i < A.Cells.Length; i++)
				if (!A.Cells[i].Equals(B.Cells[i])) return false;
			return true;
		}

		private static string SectorId(Sector Sector)
		{
			try { return Sector == null || Sector.ID == Guid.Empty ? "" : Sector.ID.ToString("D"); }
			catch { return ""; }
		}

		private static string BoundFault(string Value)
		{
			if (string.IsNullOrWhiteSpace(Value)) return "Home evidence is malformed";
			StringBuilder result = new StringBuilder(); bool gap = false;
			for (int i = 0; i < Value.Length && i < 2048 && result.Length < 512; i++)
			{
				char ch = Value[i];
				if (char.IsControl(ch) || char.IsWhiteSpace(ch)) { gap = result.Length > 0; continue; }
				if (gap && result.Length < 511) result.Append(' ');
				gap = false; if (result.Length < 512) result.Append(ch);
			}
			return result.Length == 0 ? "Home evidence is malformed" : result.ToString();
		}

		private static string Digest(string ZoneId, string SectorId, string Identity,
			int OriginX, int OriginY, List<ArchitecturePoint> Cells)
		{
			StringBuilder body = new StringBuilder("hp223-home-v1|");
			Frame(body, "Hearthpyre"); Frame(body, "2.2.3");
			Frame(body, ZoneId); Frame(body, SectorId); Frame(body, Identity);
			body.Append(OriginX.ToString(CultureInfo.InvariantCulture)).Append(',')
				.Append(OriginY.ToString(CultureInfo.InvariantCulture)).Append('|')
				.Append(Cells.Count.ToString(CultureInfo.InvariantCulture)).Append('|');
			for (int i = 0; i < Cells.Count; i++)
				body.Append(Cells[i].X.ToString(CultureInfo.InvariantCulture)).Append(',')
					.Append(Cells[i].Y.ToString(CultureInfo.InvariantCulture)).Append(';');
			using (SHA256 sha = SHA256.Create())
			{
				byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(body.ToString()));
				StringBuilder result = new StringBuilder(64);
				for (int i = 0; i < hash.Length; i++) result.Append(hash[i].ToString("x2"));
				return result.ToString();
			}
		}

		private static void Frame(StringBuilder Body, string Value)
		{
			string value = Value ?? "";
			Body.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':')
				.Append(value).Append('|');
		}

		private static long Pack(int X, int Y) => ((long)X << 32) | (uint)Y;
		/// <summary>Only Api cells cross the provider seam; the host translates them back.</summary>
		private static KingdomApiCell[] ApiCells(List<ArchitecturePoint> Cells)
		{
			KingdomApiCell[] cells = new KingdomApiCell[Cells.Count];
			for (int i = 0; i < cells.Length; i++)
				cells[i] = new KingdomApiCell(Cells[i].X, Cells[i].Y);
			return cells;
		}

		private static int Compare(ArchitecturePoint A, ArchitecturePoint B)
		{
			int y = A.Y.CompareTo(B.Y); return y != 0 ? y : A.X.CompareTo(B.X);
		}
		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
