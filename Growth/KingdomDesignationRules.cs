using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Bounded, engine-free normalization for every building designation source.</summary>
	public static class KingdomDesignationRules
	{
		public const int MaxDesignationsPerZone = 512;
		public const int MaxCellsPerDesignation = 2000;
		public const int MaxCellsPerZoneIndex = 65536;
		public const int MaxIdentityChars = 256;
		public const int MaxCapsPerDesignation = 32;
		public const int MaxTagsPerDesignation = 32;
		public const int MaxDesignationProviders = 128;
		public const int MaxSourceFaults = 512;
		private const KingdomBenefitCellUse KnownUses = KingdomBenefitCellUse.Plot
			| KingdomBenefitCellUse.Building | KingdomBenefitCellUse.Covered
			| KingdomBenefitCellUse.Interior | KingdomBenefitCellUse.Yard
			| KingdomBenefitCellUse.Network | KingdomBenefitCellUse.Ingress;

		public static bool TryNormalize(KingdomBenefitDesignation Source, string ZoneId,
			int Width, int Height, out KingdomBenefitDesignation Result, out string Failure)
		{
			Result = null;
			Failure = null;
			if (Source == null || !SafeToken(Source.ProviderId, 64)
				|| !SafeToken(Source.ProviderVersion, 32) || !SafeToken(Source.Identity, MaxIdentityChars)
				|| !SafeToken(Source.Revision, MaxIdentityChars)
				|| !SafeToken(Source.ZoneId, MaxIdentityChars)
				|| Source.ZoneId != ZoneId || !SafeToken(Source.RootId, MaxIdentityChars)
				|| !SafeToken(Source.BuildingKey, 128)
				|| (!string.IsNullOrEmpty(Source.LotId) && !SafeToken(Source.LotId, MaxIdentityChars))
				|| Width < 1 || Height < 1)
				return Fail("designation identity is malformed or addresses different ground", out Failure);
			if (Source.Cells == null || Source.Cells.Count < 1
				|| Source.Cells.Count > MaxCellsPerDesignation)
				return Fail("designation has no bounded exact-cell membership", out Failure);

			KingdomBenefitDesignation row = new KingdomBenefitDesignation {
				ProviderId = Source.ProviderId.Trim(), ProviderVersion = Source.ProviderVersion.Trim(),
				Identity = Source.Identity.Trim(), Revision = Source.Revision.Trim(),
				ZoneId = Source.ZoneId.Trim(), RootId = Source.RootId.Trim(),
				BuildingKey = Source.BuildingKey.Trim(), LotId = CleanOptional(Source.LotId)
			};
			if (Source.Caps == null || Source.Caps.Count > MaxCapsPerDesignation
				|| Source.AcceptedTags == null || Source.AcceptedTags.Count > MaxTagsPerDesignation)
				return Fail("designation benefit contract exceeds its row bound", out Failure);
			HashSet<string> kinds = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; Source.Caps != null && i < Source.Caps.Count; i++)
			{
				string kind = Fold(Source.Caps[i].Kind);
				if (!SafeToken(kind, 64) || Source.Caps[i].Amount <= 0 || !kinds.Add(kind))
					return Fail("designation benefit caps are malformed or duplicated", out Failure);
				row.Caps.Add(new KindAmount(kind, Source.Caps[i].Amount));
			}
			HashSet<string> tags = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; Source.AcceptedTags != null && i < Source.AcceptedTags.Count; i++)
			{
				string tag = Fold(Source.AcceptedTags[i]);
				if (!SafeToken(tag, 128) || !tags.Add(tag))
					return Fail("designation accepted tags are malformed or duplicated", out Failure);
				row.AcceptedTags.Add(tag);
			}

			HashSet<long> cells = new HashSet<long>();
			for (int i = 0; i < Source.Cells.Count; i++)
			{
				KingdomBenefitCell cell = Source.Cells[i];
				bool network = (cell.Use & KingdomBenefitCellUse.Network) != 0;
				bool plot = (cell.Use & KingdomBenefitCellUse.Plot) != 0;
				bool building = (cell.Use & KingdomBenefitCellUse.Building) != 0;
				bool yard = (cell.Use & KingdomBenefitCellUse.Yard) != 0;
				bool interior = (cell.Use & KingdomBenefitCellUse.Interior) != 0;
				bool covered = (cell.Use & KingdomBenefitCellUse.Covered) != 0;
				bool ingress = (cell.Use & KingdomBenefitCellUse.Ingress) != 0;
				if (cell.X < 0 || cell.Y < 0 || cell.X >= Width || cell.Y >= Height
					|| cell.Use == KingdomBenefitCellUse.None || (cell.Use & ~KnownUses) != 0
					|| ((building || yard) && !plot) || (building && yard)
					|| (ingress && !plot)
					|| (interior && (!building || !covered))
					|| !Enum.IsDefined(typeof(KingdomBenefitCover), cell.Cover)
					|| (covered != (cell.Cover != KingdomBenefitCover.Open))
					|| (network ? !SafeToken(cell.NetworkKey, 128)
						: !string.IsNullOrEmpty(cell.NetworkKey))
					|| !cells.Add(Pack(cell.X, cell.Y)))
					return Fail("designation exact-cell membership is malformed or duplicated", out Failure);
				row.Cells.Add(cell);
			}
			row.Cells.Sort(CompareCells);
			row.Caps.Sort((a, b) => string.CompareOrdinal(a.Kind, b.Kind));
			row.AcceptedTags.Sort(StringComparer.Ordinal);
			Result = row;
			return true;
		}

		public static bool ScopeAccepts(KingdomBenefitScope Scope,
			KingdomBenefitCellUse Use, bool InContainer)
		{
			return KingdomBenefitProviderRules.ScopeAccepts(Scope, Use, InContainer);
		}

		internal static long Pack(int X, int Y)
		{
			return ((long)X << 32) | (uint)Y;
		}

		private static int CompareCells(KingdomBenefitCell A, KingdomBenefitCell B)
		{
			int y = A.Y.CompareTo(B.Y);
			return y != 0 ? y : A.X.CompareTo(B.X);
		}

		private static string Fold(string Value) => (Value ?? "").Trim().ToLowerInvariant();
		private static string CleanOptional(string Value) => string.IsNullOrWhiteSpace(Value)
			? "" : Value.Trim();

		public static bool SafeToken(string Value, int Maximum)
		{
			if (string.IsNullOrWhiteSpace(Value)) return false;
			string value = Value.Trim();
			if (value.Length > Maximum || value.Length != Value.Length) return false;
			for (int i = 0; i < value.Length; i++)
				if (!(value[i] >= 'a' && value[i] <= 'z')
					&& !(value[i] >= 'A' && value[i] <= 'Z')
					&& !(value[i] >= '0' && value[i] <= '9')
					&& "._:+-/@".IndexOf(value[i]) < 0) return false;
			return true;
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}

		internal static KingdomBenefitDesignation Copy(KingdomBenefitDesignation Source)
		{
			KingdomBenefitDesignation copy = new KingdomBenefitDesignation {
				ProviderId = Source.ProviderId, ProviderVersion = Source.ProviderVersion,
				Identity = Source.Identity, Revision = Source.Revision, ZoneId = Source.ZoneId,
				RootId = Source.RootId, BuildingKey = Source.BuildingKey, LotId = Source.LotId };
			copy.Caps.AddRange(Source.Caps); copy.AcceptedTags.AddRange(Source.AcceptedTags);
			copy.Cells.AddRange(Source.Cells); return copy;
		}
	}

	/// <summary>Immutable exact-cell lookup used equally by authored and external sources.</summary>
	public sealed partial class KingdomDesignationIndex
	{
		private sealed class CellMembership
		{
			internal KingdomBenefitDesignation Designation;
			internal KingdomBenefitCellUse Use;
			internal KingdomBenefitCover Cover;
			internal string NetworkKey;
		}

		private readonly List<KingdomBenefitDesignation> Rows;
		private readonly Dictionary<long, List<CellMembership>> ByCell;
		private readonly Dictionary<string, KingdomBenefitDesignation> ById;
		private readonly List<string> SourceFaultRows = new List<string>();

		private KingdomDesignationIndex(List<KingdomBenefitDesignation> Rows,
			Dictionary<long, List<CellMembership>> ByCell,
			Dictionary<string, KingdomBenefitDesignation> ById)
		{
			this.Rows = Rows; this.ByCell = ByCell; this.ById = ById;
		}

		public IReadOnlyList<KingdomBenefitDesignation> Designations
		{
			get
			{
				List<KingdomBenefitDesignation> copy = new List<KingdomBenefitDesignation>();
				for (int i = 0; i < Rows.Count; i++) copy.Add(Clone(Rows[i]));
				return copy.AsReadOnly();
			}
		}

		/// <summary>Bounded faults quarantined while assembling this active-zone snapshot.
		/// Independent valid designations remain usable.</summary>
		public IReadOnlyList<string> SourceFaults =>
			new List<string>(SourceFaultRows).AsReadOnly();

		internal IReadOnlyList<KingdomBenefitDesignation> ExactDesignations => Rows;

		public static bool TryCreate(IReadOnlyList<KingdomBenefitDesignation> Sources,
			string ZoneId, int Width, int Height, out KingdomDesignationIndex Index,
			out string Failure)
		{
			Index = null; Failure = null;
			if (Sources == null || Sources.Count > KingdomDesignationRules.MaxDesignationsPerZone)
				return Fail("designation source exceeded its per-zone bound", out Failure);
			List<KingdomBenefitDesignation> rows = new List<KingdomBenefitDesignation>();
			Dictionary<string, KingdomBenefitDesignation> ids =
				new Dictionary<string, KingdomBenefitDesignation>(StringComparer.Ordinal);
			HashSet<string> roots = new HashSet<string>(StringComparer.Ordinal);
			Dictionary<long, List<CellMembership>> cells =
				new Dictionary<long, List<CellMembership>>();
			int total = 0;
			for (int i = 0; i < Sources.Count; i++)
			{
				KingdomBenefitDesignation row;
				if (!KingdomDesignationRules.TryNormalize(Sources[i], ZoneId, Width, Height,
					out row, out Failure)) return false;
				if (ids.ContainsKey(row.Identity))
					return Fail("designation identity is duplicated: " + row.Identity, out Failure);
				if (!roots.Add(row.RootId))
					return Fail("designation root is duplicated: " + row.RootId, out Failure);
				total += row.Cells.Count;
				if (total > KingdomDesignationRules.MaxCellsPerZoneIndex)
					return Fail("designation source exceeded its exact-cell index bound", out Failure);
				ids.Add(row.Identity, row); rows.Add(row);
				for (int c = 0; c < row.Cells.Count; c++)
				{
					long key = KingdomDesignationRules.Pack(row.Cells[c].X, row.Cells[c].Y);
					if (!cells.TryGetValue(key, out List<CellMembership> list))
						cells.Add(key, list = new List<CellMembership>());
					list.Add(new CellMembership { Designation = row, Use = row.Cells[c].Use,
						Cover = row.Cells[c].Cover, NetworkKey = row.Cells[c].NetworkKey });
				}
			}
			rows.Sort((a, b) => string.CompareOrdinal(a.Identity, b.Identity));
			Index = new KingdomDesignationIndex(rows, cells, ids);
			return true;
		}

		public KingdomBenefitDesignation Find(string Identity)
		{
			return !string.IsNullOrEmpty(Identity) && ById.TryGetValue(Identity, out var row)
				? Clone(row) : null;
		}

		internal KingdomBenefitDesignation FindExact(string Identity)
		{
			return !string.IsNullOrEmpty(Identity) && ById.TryGetValue(Identity, out var row)
				? row : null;
		}

		public List<KingdomBenefitDesignation> Containing(int X, int Y,
			KingdomBenefitScope Scope, bool InContainer = false, string NetworkKey = null)
		{
			List<KingdomBenefitDesignation> result = new List<KingdomBenefitDesignation>();
			if (!ByCell.TryGetValue(KingdomDesignationRules.Pack(X, Y), out var rows)) return result;
			for (int i = 0; i < rows.Count; i++)
				if (Accepts(rows[i], Scope, InContainer, NetworkKey))
					result.Add(Clone(rows[i].Designation));
			return result;
		}

		internal List<KingdomBenefitDesignation> ContainingExact(int X, int Y,
			KingdomBenefitScope Scope, bool InContainer, string NetworkKey)
		{
			List<KingdomBenefitDesignation> result = new List<KingdomBenefitDesignation>();
			if (!ByCell.TryGetValue(KingdomDesignationRules.Pack(X, Y), out var rows)) return result;
			for (int i = 0; i < rows.Count; i++)
				if (Accepts(rows[i], Scope, InContainer, NetworkKey)) result.Add(rows[i].Designation);
			return result;
		}

		private static bool Accepts(CellMembership Row, KingdomBenefitScope Scope,
			bool InContainer, string NetworkKey)
		{
			return KingdomDesignationRules.ScopeAccepts(Scope, Row.Use, InContainer)
				&& (Scope != KingdomBenefitScope.Network || Row.NetworkKey == NetworkKey);
		}

		private static KingdomBenefitDesignation Clone(KingdomBenefitDesignation Source)
		{
			KingdomBenefitDesignation copy = new KingdomBenefitDesignation {
				ProviderId = Source.ProviderId, ProviderVersion = Source.ProviderVersion,
				Identity = Source.Identity, Revision = Source.Revision, ZoneId = Source.ZoneId,
				RootId = Source.RootId, BuildingKey = Source.BuildingKey, LotId = Source.LotId
			};
			copy.Caps.AddRange(Source.Caps); copy.AcceptedTags.AddRange(Source.AcceptedTags);
			copy.Cells.AddRange(Source.Cells);
			return copy;
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
