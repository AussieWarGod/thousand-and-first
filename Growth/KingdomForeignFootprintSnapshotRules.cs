using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Engine-free overlap and bound-reproof decisions over provider-wide snapshots.</summary>
	internal static class KingdomForeignFootprintSnapshotRules
	{
		internal const int MaxProviders = 64;
		internal const int MaxRows = 512;
		internal const int MaxCells = 65536;
		internal const int MaxRowsPerProvider = MaxRows;
		internal const int MaxCellsPerProvider = MaxCells;
		internal const int MaxFaultsPerProvider = 8;
		internal const int MaxFaultChars = 512;

		internal static KingdomForeignProviderStatus ClassifyCall(bool Returned,
			bool RowsPresent, bool FailurePresent)
		{
			if (!Returned && !RowsPresent && !FailurePresent)
				return KingdomForeignProviderStatus.Absent;
			if (Returned && RowsPresent && !FailurePresent)
				return KingdomForeignProviderStatus.Observed;
			return KingdomForeignProviderStatus.Faulted;
		}

		/// <summary>Preflights only array/count metadata. Invalid row-local cell-list shapes do
		/// not consume the provider cell budget; normalization retains their bounded diagnostic.
		/// No exact cell is inspected until this whole-provider bound has succeeded.</summary>
		internal static bool TryProviderPreflight(int RowCount,
			IReadOnlyList<int> CellCounts, out string Failure)
		{
			Failure = null;
			if (RowCount < 0 || RowCount > MaxRowsPerProvider)
				return Fail("foreign provider row budget exceeded", out Failure);
			if (CellCounts == null || CellCounts.Count != RowCount)
				return Fail("foreign provider row-count preflight is inconsistent", out Failure);
			long cells = 0L;
			for (int i = 0; i < CellCounts.Count; i++)
			{
				int count = CellCounts[i];
				if (count < 1 || count > KingdomDesignationRules.MaxCellsPerDesignation)
					continue;
				if (cells > MaxCellsPerProvider - count)
					return Fail("foreign provider cell budget exceeded", out Failure);
				cells += count;
			}
			return true;
		}

		internal static bool TryMatch(IReadOnlyList<KingdomForeignProviderSnapshot> Snapshots,
			IReadOnlyList<ArchitecturePoint> Wanted,
			out KingdomForeignFootprintEvidence Match, out string Failure)
		{
			Match = null; Failure = null;
			if (!TryValidate(Snapshots, out Failure)
				|| !TryCellSet(Wanted, out HashSet<long> wanted, out Failure)) return false;
			for (int p = 0; p < Snapshots.Count; p++)
			{
				KingdomForeignProviderSnapshot snapshot = Snapshots[p];
				if (snapshot.Status != KingdomForeignProviderStatus.Observed) continue;
				for (int r = 0; r < snapshot.Rows.Count; r++)
				{
					KingdomForeignFootprintEvidence row = snapshot.Rows[r];
					if (!Intersects(wanted, row.Cells)) continue;
					if (row.IsRefused)
						return Fail("room intersects refused foreign footprint: "
							+ row.Refusal, out Failure);
					if (!SameCells(Wanted, row.Cells))
						return Fail("room partially intersects a foreign footprint", out Failure);
					if (Match != null)
						return Fail("room matches more than one foreign footprint", out Failure);
					Match = row;
				}
			}
			return true;
		}

		internal static bool TryReprove(IReadOnlyList<KingdomForeignProviderSnapshot> Snapshots,
			string ProviderId, string ProviderVersion, string Identity, string Revision,
			IReadOnlyList<ArchitecturePoint> Cells, out string Failure)
		{
			Failure = null;
			if (!TryValidate(Snapshots, out Failure)
				|| !TryCellSet(Cells, out HashSet<long> bound, out Failure)) return false;
			KingdomForeignProviderSnapshot provider = null;
			for (int i = 0; i < Snapshots.Count; i++)
				if (Snapshots[i].ProviderId == ProviderId)
				{
					if (provider != null)
						return Fail("bound foreign provider registration is ambiguous", out Failure);
					provider = Snapshots[i];
				}
			if (provider == null)
				return Fail("bound foreign footprint provider is no longer registered", out Failure);
			if (provider.Status == KingdomForeignProviderStatus.Faulted)
				return Fail("bound foreign footprint provider is faulted: "
					+ provider.Fault, out Failure);
			if (provider.Status != KingdomForeignProviderStatus.Observed)
				return Fail("bound foreign footprint provider reports no active ground", out Failure);
			if (provider.ProviderVersion != ProviderVersion)
				return Fail("bound foreign footprint provider version changed", out Failure);

			KingdomForeignFootprintEvidence match = null;
			for (int i = 0; i < provider.Rows.Count; i++)
			{
				KingdomForeignFootprintEvidence row = provider.Rows[i];
				if (row.Identity != Identity) continue;
				if (row.IsRefused)
					return Fail("bound foreign footprint is refused: " + row.Refusal, out Failure);
				match = row;
			}
			if (match == null)
				return Fail("bound foreign footprint is no longer present", out Failure);
			if (match.Revision != Revision || !SameCells(Cells, match.Cells))
				return Fail("bound foreign footprint membership or revision changed", out Failure);
			for (int p = 0; p < Snapshots.Count; p++)
				for (int r = 0; r < Snapshots[p].Rows.Count; r++)
				{
					KingdomForeignFootprintEvidence row = Snapshots[p].Rows[r];
					if (ReferenceEquals(row, match)) continue;
					if (Intersects(bound, row.Cells))
						return Fail("bound foreign footprint now intersects other foreign ground",
							out Failure);
				}
			return true;
		}

		internal static bool TryValidate(IReadOnlyList<KingdomForeignProviderSnapshot> Snapshots,
			out string Failure)
		{
			Failure = null;
			if (Snapshots == null || Snapshots.Count > MaxProviders)
				return Fail("foreign provider snapshot roster is absent or over-bound", out Failure);
			HashSet<string> providers = new HashSet<string>(StringComparer.Ordinal);
			long rows = 0; long cells = 0;
			for (int p = 0; p < Snapshots.Count; p++)
			{
				KingdomForeignProviderSnapshot snapshot = Snapshots[p];
				if (snapshot == null || !SafeToken(snapshot.ProviderId, 64)
					|| !SafeToken(snapshot.ProviderVersion, 32)
					|| !providers.Add(snapshot.ProviderId) || snapshot.Rows == null
					|| snapshot.RowFaults == null
					|| !Enum.IsDefined(typeof(KingdomForeignProviderStatus), snapshot.Status))
					return Fail("foreign provider snapshot identity is malformed or duplicated",
						out Failure);
				bool faulted = snapshot.Status == KingdomForeignProviderStatus.Faulted;
				if ((faulted && (!ValidFault(snapshot.Fault) || snapshot.Rows.Count != 0
						|| snapshot.RowFaults.Count != 0))
					|| (!faulted && !string.IsNullOrEmpty(snapshot.Fault))
					|| (snapshot.Status == KingdomForeignProviderStatus.Absent
						&& (snapshot.Rows.Count != 0 || snapshot.RowFaults.Count != 0))
					|| snapshot.RowFaults.Count > MaxFaultsPerProvider)
					return Fail("foreign provider snapshot status is inconsistent", out Failure);
				for (int f = 0; f < snapshot.RowFaults.Count; f++)
					if (!ValidFault(snapshot.RowFaults[f]))
						return Fail("foreign provider row fault is malformed", out Failure);
				Dictionary<string, bool> identities =
					new Dictionary<string, bool>(StringComparer.Ordinal);
				if (snapshot.Rows.Count > MaxRowsPerProvider)
					return Fail("foreign provider row budget exceeded", out Failure);
				rows += snapshot.Rows.Count;
				if (rows > MaxRows) return Fail("foreign footprint row budget exceeded", out Failure);
				long providerCells = 0L;
				for (int r = 0; r < snapshot.Rows.Count; r++)
				{
					KingdomForeignFootprintEvidence row = snapshot.Rows[r];
					if (!ValidRow(snapshot, row, identities, out Failure)) return false;
					providerCells += row.Cells.Count; cells += row.Cells.Count;
					if (providerCells > MaxCellsPerProvider)
						return Fail("foreign provider cell budget exceeded", out Failure);
					if (cells > MaxCells)
						return Fail("foreign footprint cell budget exceeded", out Failure);
				}
			}
			return true;
		}

		private static bool ValidRow(KingdomForeignProviderSnapshot Snapshot,
			KingdomForeignFootprintEvidence Row, Dictionary<string, bool> Identities,
			out string Failure)
		{
			Failure = null;
			if (Row == null || Row.ProviderId != Snapshot.ProviderId
				|| Row.ProviderVersion != Snapshot.ProviderVersion
				|| !SafeToken(Row.Identity, 256)
				|| !SafeToken(Row.Revision, 256)
				|| !SafeToken(Row.ZoneId, 256)
				|| (!string.IsNullOrEmpty(Row.SectorId)
					&& !SafeToken(Row.SectorId, 256))
				|| Row.OriginX < 0 || Row.OriginY < 0 || !ValidRefusal(Row.Refusal)
				|| Row.Cells == null || Row.Cells.Count < 1
				|| Row.Cells.Count > KingdomDesignationRules.MaxCellsPerDesignation)
				return Fail("foreign footprint row is malformed or duplicated", out Failure);
			bool refused = Row.IsRefused;
			if (Identities.TryGetValue(Row.Identity, out bool priorRefused))
			{
				if (!refused || !priorRefused)
					return Fail("foreign footprint row is malformed or duplicated", out Failure);
			}
			else Identities.Add(Row.Identity, refused);
			HashSet<long> seen = new HashSet<long>(); ArchitecturePoint prior = default;
			bool origin = false;
			for (int i = 0; i < Row.Cells.Count; i++)
			{
				ArchitecturePoint cell = Row.Cells[i];
				if (cell.X < 0 || cell.Y < 0
					|| !seen.Add(KingdomDesignationRules.Pack(cell.X, cell.Y))
					|| (i > 0 && Compare(prior, cell) >= 0))
					return Fail("foreign footprint exact cells are malformed", out Failure);
				if (cell.X == Row.OriginX && cell.Y == Row.OriginY) origin = true;
				prior = cell;
			}
			return origin || Fail("foreign footprint origin is outside its exact cells", out Failure);
		}

		private static bool TryCellSet(IReadOnlyList<ArchitecturePoint> Cells,
			out HashSet<long> Result, out string Failure)
		{
			Result = new HashSet<long>(); Failure = null;
			if (Cells == null || Cells.Count < 1
				|| Cells.Count > KingdomDesignationRules.MaxCellsPerDesignation)
				return Fail("room has no bounded exact cells", out Failure);
			for (int i = 0; i < Cells.Count; i++)
				if (Cells[i].X < 0 || Cells[i].Y < 0
					|| !Result.Add(KingdomDesignationRules.Pack(Cells[i].X, Cells[i].Y)))
					return Fail("room exact cells are malformed or duplicated", out Failure);
			return true;
		}

		private static bool Intersects(HashSet<long> Wanted,
			IReadOnlyList<ArchitecturePoint> Cells)
		{
			for (int i = 0; i < Cells.Count; i++)
				if (Wanted.Contains(KingdomDesignationRules.Pack(Cells[i].X, Cells[i].Y))) return true;
			return false;
		}

		private static bool SameCells(IReadOnlyList<ArchitecturePoint> A,
			IReadOnlyList<ArchitecturePoint> B)
		{
			if (A == null || B == null || A.Count != B.Count) return false;
			HashSet<long> cells = new HashSet<long>();
			for (int i = 0; i < A.Count; i++)
				if (!cells.Add(KingdomDesignationRules.Pack(A[i].X, A[i].Y))) return false;
			for (int i = 0; i < B.Count; i++)
				if (!cells.Contains(KingdomDesignationRules.Pack(B[i].X, B[i].Y))) return false;
			return true;
		}

		private static bool ValidFault(string Value)
		{
			if (Value == null || Value.Length == 0 || Value.Length > MaxFaultChars
				|| string.IsNullOrWhiteSpace(Value)
				|| Value.Trim() != Value) return false;
			for (int i = 0; i < Value.Length; i++) if (char.IsControl(Value[i])) return false;
			return true;
		}
		private static bool ValidRefusal(string Value) => string.IsNullOrEmpty(Value)
			|| ValidFault(Value);
		internal static bool SafeToken(string Value, int Maximum)
		{
			if (Value == null || Value.Length < 1 || Value.Length > Maximum) return false;
			for (int i = 0; i < Value.Length; i++)
				if (!(Value[i] >= 'a' && Value[i] <= 'z')
					&& !(Value[i] >= 'A' && Value[i] <= 'Z')
					&& !(Value[i] >= '0' && Value[i] <= '9')
					&& "._:+-/@".IndexOf(Value[i]) < 0) return false;
			return true;
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
