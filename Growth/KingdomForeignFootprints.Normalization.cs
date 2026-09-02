using System;
using System.Collections.Generic;
using System.Globalization;
using ThousandAndFirst.Api;
using XRL.World;

namespace ThousandAndFirst
{
	internal static partial class KingdomForeignFootprints
	{
		private static bool TryNormalizeProvider(ProviderRow Provider,
			KingdomForeignFootprint[] Sources, Zone Z,
			out List<KingdomForeignFootprintEvidence> Results,
			out List<string> Faults, out string Failure)
		{
			Results = new List<KingdomForeignFootprintEvidence>();
			Faults = new List<string>(); Failure = null;
			if (Provider == null || Sources == null || Z == null)
				return Fail("provider observation cannot be normalized", out Failure);
			if (Sources.Length > KingdomForeignFootprintSnapshotRules.MaxRowsPerProvider)
				return Fail("foreign provider row budget exceeded", out Failure);
			int[] cellCounts = new int[Sources.Length];
			for (int i = 0; i < Sources.Length; i++)
				cellCounts[i] = Sources[i]?.Cells?.Length ?? -1;
			if (!KingdomForeignFootprintSnapshotRules.TryProviderPreflight(
				Sources.Length, cellCounts, out Failure)) return false;
			int count = Sources.Length;
			for (int i = 0; i < count; i++)
			{
				if (!TryNormalize(Provider, Sources[i], Z,
					out KingdomForeignFootprintEvidence row, out string rowFault))
				{
					AddRowFault(Faults, DescribeRowFault(Sources[i], rowFault)); continue;
				}
				if (!string.IsNullOrEmpty(rowFault)) AddRowFault(Faults, rowFault);
				Results.Add(row);
			}
			Results.Sort(CompareEvidence);
			RefuseProviderContradictions(Results, Faults);
			return true;
		}

		/// <summary>False means no bounded exact cells exist to quarantine. Once exact cells are
		/// known, malformed metadata becomes a refused row instead of erasing healthy siblings.</summary>
		private static bool TryNormalize(ProviderRow Provider, KingdomForeignFootprint Source,
			Zone Z, out KingdomForeignFootprintEvidence Result, out string Failure)
		{
			Result = null; Failure = null;
			if (Source == null || !TryNormalizeCells(Source.Cells, Z,
				out List<ArchitecturePoint> cells, out Failure)) return false;
			string malformed = MetadataFailure(Provider, Source, Z, cells);
			if (malformed != null)
			{
				string digest = FaultDigest(Provider.Id, Source.Identity, cells);
				Result = new KingdomForeignFootprintEvidence {
					ProviderId = Provider.Id, ProviderVersion = Provider.Version,
					Identity = KingdomForeignFootprintSnapshotRules.SafeToken(Source.Identity, 256)
						? Source.Identity : "fault:" + digest,
					Revision = "fault:" + digest, Refusal = Bound(malformed), ZoneId = Z.ZoneID,
					SectorId = "", OriginX = cells[0].X, OriginY = cells[0].Y, Cells = cells };
				Failure = malformed; return true;
			}
			Result = new KingdomForeignFootprintEvidence { ProviderId = Provider.Id,
				ProviderVersion = Provider.Version, Identity = Source.Identity,
				Revision = Source.Revision, Refusal = Source.Refusal ?? "", ZoneId = Source.ZoneId,
				SectorId = Source.SectorId ?? "", OriginX = Source.OriginX,
				OriginY = Source.OriginY, Cells = cells };
			return true;
		}

		private static string MetadataFailure(ProviderRow Provider,
			KingdomForeignFootprint Source, Zone Z, List<ArchitecturePoint> Cells)
		{
			if (Source.ProviderId != Provider.Id || Source.ProviderVersion != Provider.Version
				|| Source.ZoneId != Z.ZoneID
				|| !KingdomForeignFootprintSnapshotRules.SafeToken(Source.Identity, 256)
				|| !KingdomForeignFootprintSnapshotRules.SafeToken(Source.Revision, 256)
				|| (!string.IsNullOrEmpty(Source.SectorId)
					&& !KingdomForeignFootprintSnapshotRules.SafeToken(Source.SectorId, 256)))
				return "footprint identity addresses malformed or different ground";
			if (!ValidRefusal(Source.Refusal)) return "footprint refusal is malformed";
			if (Source.DeclaredCount != Cells.Count)
				return "footprint declared count disagrees with its exact cells";
			for (int i = 0; i < Cells.Count; i++)
				if (Cells[i].X == Source.OriginX && Cells[i].Y == Source.OriginY) return null;
			return "footprint origin is outside its exact membership";
		}

		/// <summary>The Api seam: provider cells arrive as <see cref="KingdomApiCell"/> and become
		/// internal geometry only after every one is proved inside the zone and unique.</summary>
		private static bool TryNormalizeCells(IReadOnlyList<KingdomApiCell> Source, Zone Z,
			out List<ArchitecturePoint> Result, out string Failure)
		{
			Result = new List<ArchitecturePoint>(); Failure = null;
			if (Z == null || Source == null || Source.Count < 1
				|| Source.Count > KingdomDesignationRules.MaxCellsPerDesignation)
				return Fail("foreign footprint has no bounded exact cells", out Failure);
			HashSet<long> seen = new HashSet<long>();
			for (int i = 0; i < Source.Count; i++)
			{
				KingdomApiCell cell = Source[i];
				if (cell.X < 0 || cell.Y < 0 || cell.X >= Z.Width || cell.Y >= Z.Height
					|| !seen.Add(KingdomDesignationRules.Pack(cell.X, cell.Y)))
					return Fail("foreign footprint cell is out of bounds or duplicated", out Failure);
				Result.Add(new ArchitecturePoint(cell.X, cell.Y));
			}
			Result.Sort(Compare); return true;
		}

		private static void RefuseProviderContradictions(
			List<KingdomForeignFootprintEvidence> Rows, List<string> Faults)
		{
			Dictionary<string, KingdomForeignFootprintEvidence> identities =
				new Dictionary<string, KingdomForeignFootprintEvidence>(StringComparer.Ordinal);
			Dictionary<long, KingdomForeignFootprintEvidence> owners =
				new Dictionary<long, KingdomForeignFootprintEvidence>();
			for (int i = 0; i < Rows.Count; i++)
			{
				KingdomForeignFootprintEvidence row = Rows[i];
				if (identities.TryGetValue(row.Identity, out KingdomForeignFootprintEvidence prior))
				{
					Refuse(prior, "duplicate footprint identity from one provider");
					Refuse(row, "duplicate footprint identity from one provider");
					AddRowFault(Faults, "duplicate footprint identity from one provider");
				}
				else identities.Add(row.Identity, row);
				for (int c = 0; c < row.Cells.Count; c++)
				{
					long key = KingdomDesignationRules.Pack(row.Cells[c].X, row.Cells[c].Y);
					if (!owners.TryGetValue(key, out prior)) owners.Add(key, row);
					else if (!ReferenceEquals(prior, row))
					{
						Refuse(prior, "footprint overlaps another row from the same provider");
						Refuse(row, "footprint overlaps another row from the same provider");
						AddRowFault(Faults, "provider footprint rows overlap");
					}
				}
			}
		}

		private static void Refuse(KingdomForeignFootprintEvidence Row, string Reason)
		{
			if (Row != null) Row.Refusal = Bound(Reason);
		}

		private static void AddRowFault(List<string> Faults, string Failure)
		{
			if (Faults == null || string.IsNullOrWhiteSpace(Failure)) return;
			int maximum = KingdomForeignFootprintSnapshotRules.MaxFaultsPerProvider;
			if (Faults.Count < maximum - 1) Faults.Add(Bound(Failure));
			else if (Faults.Count == maximum - 1)
				Faults.Add("additional foreign footprint row faults omitted");
		}

		private static string DescribeRowFault(KingdomForeignFootprint Source, string Failure)
		{
			string identity = KingdomForeignFootprintSnapshotRules.SafeToken(
				Source?.Identity, 256) ? Source.Identity : "unidentified";
			return Bound("footprint " + identity + ": "
				+ (Failure ?? "row evidence is malformed"));
		}

		private static string FaultDigest(string Provider, string Identity,
			IReadOnlyList<ArchitecturePoint> Cells)
		{
			ulong hash = 14695981039346656037UL;
			HashText(ref hash, Provider); HashText(ref hash, Identity);
			for (int i = 0; i < Cells.Count; i++)
			{
				HashInt(ref hash, Cells[i].X); HashInt(ref hash, Cells[i].Y);
			}
			return hash.ToString("x16", CultureInfo.InvariantCulture);
		}

		private static void HashText(ref ulong Hash, string Value)
		{
			string value = Value ?? "";
			int count = Math.Min(value.Length, 256);
			for (int i = 0; i < count; i++) HashInt(ref Hash, value[i]);
			HashInt(ref Hash, count);
		}

		private static void HashInt(ref ulong Hash, int Value)
		{
			unchecked
			{
				for (int shift = 0; shift < 32; shift += 8)
				{ Hash ^= (byte)(Value >> shift); Hash *= 1099511628211UL; }
			}
		}

		private static bool ValidRefusal(string Value)
		{
			if (string.IsNullOrEmpty(Value)) return true;
			if (Value.Length > KingdomForeignFootprintSnapshotRules.MaxFaultChars
				|| Value.Trim() != Value) return false;
			for (int i = 0; i < Value.Length; i++) if (char.IsControl(Value[i])) return false;
			return true;
		}

		private static int CompareEvidence(KingdomForeignFootprintEvidence A,
			KingdomForeignFootprintEvidence B)
		{
			int identity = string.CompareOrdinal(A.Identity, B.Identity);
			if (identity != 0) return identity;
			int count = A.Cells.Count.CompareTo(B.Cells.Count);
			for (int i = 0; count == 0 && i < A.Cells.Count; i++) count = Compare(A.Cells[i], B.Cells[i]);
			return count != 0 ? count : string.CompareOrdinal(A.Revision, B.Revision);
		}

		private static int Compare(ArchitecturePoint A, ArchitecturePoint B)
		{
			int y = A.Y.CompareTo(B.Y); return y != 0 ? y : A.X.CompareTo(B.X);
		}
	}
}
