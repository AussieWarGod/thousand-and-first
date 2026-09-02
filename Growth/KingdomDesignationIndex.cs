using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
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
