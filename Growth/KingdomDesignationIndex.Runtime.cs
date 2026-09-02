using System;
using System.Collections.Generic;
using ThousandAndFirst.Api;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	[HasModSensitiveStaticCache]
	public sealed partial class KingdomDesignationIndex
	{
		private sealed class ProviderRow
		{
			internal string Id;
			internal string Version;
			internal IKingdomDesignationProvider Provider;
		}

		[ModSensitiveStaticCache]
		private static List<ProviderRow> Providers;
		[ModSensitiveStaticCache]
		private static List<string> ProviderFaults;

		/// <summary>Builds one current exact index. No stale or remote zone projection is accepted.</summary>
		public static bool TryActiveZone(Zone Z, out KingdomDesignationIndex Index,
			out string Failure)
		{
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Z) ?? KingdomSurvey.Take(Z);
			return TryActiveZone(Z, survey, out Index, out Failure);
		}

		internal static bool TryActiveZone(Zone Z, KingdomSurvey Survey,
			out KingdomDesignationIndex Index, out string Failure)
		{
			Index = null; Failure = null;
			if (Z == null || The.ZoneManager?.ActiveZone != Z || Survey == null
				|| !ReferenceEquals(Survey.Ground, Z))
				return Fail("building designations require the exact active zone survey", out Failure);
			List<KingdomBenefitDesignation> rows = new List<KingdomBenefitDesignation>();
			List<string> faults = new List<string>();
			if (!KingdomDesignationSources.TryAuthored(Z, Survey, rows, faults, out Failure)
				|| !KingdomDesignationSources.TryAdopted(Z, Survey, rows, faults, out Failure))
				return false;
			int trustedCount = rows.Count;
			TryExtensions(Z, rows, faults);
			return TryCreateIsolated(rows, trustedCount, faults, Z.ZoneID, Z.Width, Z.Height,
				out Index, out Failure);
		}

		internal bool TryExactRoot(Zone Z, KingdomBenefitDesignation Designation,
			out GameObject Root)
		{
			Root = null;
			return Z != null && Designation != null
				&& KingdomConstruction.FindExactId(Z, Designation.RootId, out Root)
					== KingdomPhysicalLookupState.Exact
				&& GameObject.Validate(Root) && ReferenceEquals(Root.CurrentZone, Z);
		}

		private static void TryExtensions(Zone Z, List<KingdomBenefitDesignation> Rows,
			List<string> Faults)
		{
			List<ProviderRow> providers = Registry();
			for (int f = 0; f < ProviderFaults.Count; f++) AddFault(Faults, ProviderFaults[f]);
			long totalCells = 0L;
			for (int r = 0; r < Rows.Count; r++) totalCells += Rows[r].Cells?.Count ?? 0;
			Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
			for (int p = 0; p < providers.Count; p++)
				counts[providers[p].Id] = counts.TryGetValue(providers[p].Id, out int count)
					? count + 1 : 1;
			for (int p = 0; p < providers.Count; p++)
			{
				if (counts[providers[p].Id] != 1) continue;
				// The Api seam: an extension reports Api rows, translated below cell by cell; a
				// source this build ships itself reports the full internal row on the trusted path.
				bool trusted = providers[p].Provider is IKingdomTrustedDesignationSource;
				KingdomBenefitDesignation[] trustedRows = null;
				KingdomApiDesignation[] reported = null;
				string providerFailure = null;
				bool found = false;
				try
				{
					found = trusted
						? ((IKingdomTrustedDesignationSource)providers[p].Provider).TryObserveTrusted(
							Z, out trustedRows, out providerFailure)
						: providers[p].Provider.TryObserve(Z, out reported, out providerFailure);
				}
				catch (Exception exception) { providerFailure = "threw " + exception.GetType().Name; }
				int observed = trusted ? (trustedRows?.Length ?? -1) : (reported?.Length ?? -1);
				if (!string.IsNullOrEmpty(providerFailure))
				{
					AddFault(Faults, providers[p].Id + " designation observation failed: "
						+ providerFailure); continue;
				}
				if (!found)
				{
					if (observed > 0)
						AddFault(Faults, providers[p].Id + " returned rows with a false result");
					continue;
				}
				if (observed < 0 || observed > KingdomDesignationRules.MaxDesignationsPerZone)
				{
					AddFault(Faults, providers[p].Id + " returned an invalid row count"); continue;
				}
				for (int i = 0; i < observed; i++)
				{
					KingdomBenefitDesignation source = trusted ? trustedRows[i] : null;
					if (!trusted && !KingdomDesignationRules.TryTranslate(reported[i], Z.Width,
						Z.Height, out source, out string translation))
					{
						AddFault(Faults, providers[p].Id + " row refused: " + translation); continue;
					}
					if (source == null || source.ProviderId != providers[p].Id
						|| source.ProviderVersion != providers[p].Version || source.ZoneId != Z.ZoneID)
					{
						AddFault(Faults, providers[p].Id + " returned a mismatched designation");
						continue;
					}
					if (source.Cells == null || source.Cells.Count < 1
						|| source.Cells.Count > KingdomDesignationRules.MaxCellsPerDesignation
						|| Rows.Count >= KingdomDesignationRules.MaxDesignationsPerZone
						|| totalCells > KingdomDesignationRules.MaxCellsPerZoneIndex
							- source.Cells.Count)
					{
						AddFault(Faults, providers[p].Id
							+ " exceeded the aggregate designation budget"); continue;
					}
					KingdomBenefitDesignation row = CopySource(source);
					if (!trusted) RestrictExternalSpatialClaims(row);
					if (!CompleteCatalogueContract(row, Z, out string rowFailure))
					{
						AddFault(Faults, providers[p].Id + " row refused: " + rowFailure); continue;
					}
					GameObject exact;
					if (KingdomConstruction.FindExactId(Z, row.RootId, out exact)
						!= KingdomPhysicalLookupState.Exact || !GameObject.Validate(exact)
						|| !ReferenceEquals(exact.CurrentZone, Z))
					{
						AddFault(Faults, providers[p].Id
							+ " designation root is not exact on this ground"); continue;
					}
					row.Identity = "ext:" + providers[p].Id.ToLowerInvariant() + ":" + row.Identity;
					totalCells += row.Cells.Count; Rows.Add(row);
				}
			}
		}

		private static bool TryCreateIsolated(List<KingdomBenefitDesignation> Sources,
			int TrustedCount, List<string> Faults, string ZoneId, int Width, int Height,
			out KingdomDesignationIndex Index, out string Failure)
		{
			Index = null; Failure = null;
			List<KingdomBenefitDesignation> valid = new List<KingdomBenefitDesignation>();
			List<string> identities = new List<string>();
			List<string> roots = new List<string>();
			List<bool> trusted = new List<bool>();
			for (int i = 0; i < Sources.Count; i++)
			{
				if (!KingdomDesignationRules.TryNormalize(Sources[i], ZoneId, Width, Height,
					out KingdomBenefitDesignation row, out string fault))
				{
					AddFault(Faults, "designation row refused: " + fault); continue;
				}
				identities.Add(row.Identity); roots.Add(row.RootId);
				trusted.Add(i < TrustedCount); valid.Add(row);
			}
			if (!KingdomDesignationCollisionRules.TryRefused(identities, roots, trusted,
				out HashSet<int> refused)) return Fail(
					"designation collision arbitration exceeded its bound", out Failure);
			foreach (int duplicate in refused) AddFault(Faults,
				"designation identity or root collision refused: " + identities[duplicate]);
			List<KingdomBenefitDesignation> clean = new List<KingdomBenefitDesignation>();
			int total = 0;
			for (int i = 0; i < valid.Count; i++)
			{
				if (refused.Contains(i)) continue;
				if (clean.Count >= KingdomDesignationRules.MaxDesignationsPerZone
					|| total > KingdomDesignationRules.MaxCellsPerZoneIndex - valid[i].Cells.Count)
				{
					AddFault(Faults, "designation source exceeded its bounded active-zone index");
					continue;
				}
				total += valid[i].Cells.Count; clean.Add(valid[i]);
			}
			if (!TryCreate(clean, ZoneId, Width, Height, out Index, out Failure)) return false;
			for (int f = 0; f < Faults.Count
				&& f < KingdomDesignationRules.MaxSourceFaults; f++)
				Index.SourceFaultRows.Add(Faults[f]);
			return true;
		}

		private static string Fault(string Value)
		{
			if (string.IsNullOrEmpty(Value)) return "unspecified designation source fault";
			return Value.Length <= 512 ? Value : Value.Substring(0, 512);
		}

		private static KingdomBenefitDesignation CopySource(KingdomBenefitDesignation Source)
		{
			KingdomBenefitDesignation copy = new KingdomBenefitDesignation {
				ProviderId = Source.ProviderId, ProviderVersion = Source.ProviderVersion,
				Identity = Source.Identity, Revision = Source.Revision, ZoneId = Source.ZoneId,
				RootId = Source.RootId, BuildingKey = Source.BuildingKey, LotId = Source.LotId
			};
			if (Source.Cells != null) copy.Cells.AddRange(Source.Cells);
			return copy;
		}

		private static void RestrictExternalSpatialClaims(KingdomBenefitDesignation Row)
		{
			for (int i = 0; i < Row.Cells.Count; i++)
			{
				KingdomBenefitCell source = Row.Cells[i];
				KingdomBenefitCellUse use = source.Use
					& ~(KingdomBenefitCellUse.Covered | KingdomBenefitCellUse.Interior
						| KingdomBenefitCellUse.Ingress);
				Row.Cells[i] = new KingdomBenefitCell(source.X, source.Y, use,
					KingdomBenefitCover.Open, source.NetworkKey);
			}
		}

		private static void AddFault(List<string> Faults, string Value)
		{
			if (Faults != null && Faults.Count < KingdomDesignationRules.MaxSourceFaults)
				Faults.Add(Fault(Value));
		}

		private static List<ProviderRow> Registry()
		{
			if (Providers != null) return Providers;
			Providers = new List<ProviderRow>(); ProviderFaults = new List<string>();
			List<Type> discovered = ModManager.GetTypesWithAttribute(
				typeof(KingdomDesignationProviderAttribute));
			if (discovered == null
				|| discovered.Count > KingdomDesignationRules.MaxDesignationProviders)
			{
				ProviderFaults.Add("Designation provider registry exceeded its bound");
				return Providers;
			}
			List<Type> types = new List<Type>(discovered);
			types.Sort((a, b) => string.CompareOrdinal(a?.FullName, b?.FullName));
			for (int i = 0; i < types.Count; i++) Collect(types[i]);
			Providers.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
			for (int i = 1; i < Providers.Count; i++)
				if (Providers[i - 1].Id == Providers[i].Id)
					ProviderFaults.Add("Duplicate designation provider: " + Providers[i].Id);
			return Providers;
		}

		private static void Collect(Type Type)
		{
			if (Type == null || !typeof(IKingdomDesignationProvider).IsAssignableFrom(Type))
			{
				ProviderFaults.Add("Marked designation type has no provider contract"); return;
			}
			try
			{
				IKingdomDesignationProvider provider = Activator.CreateInstance(Type)
					as IKingdomDesignationProvider;
				if (!KingdomDesignationRules.SafeToken(provider?.ProviderId, 64)
					|| !KingdomDesignationRules.SafeToken(provider?.ProviderVersion, 32))
					throw new InvalidOperationException("provider identity is malformed");
				Providers.Add(new ProviderRow { Id = provider.ProviderId,
					Version = provider.ProviderVersion, Provider = provider });
			}
			catch (Exception exception) { ProviderFaults.Add((Type.FullName ?? Type.Name)
				+ " could not register: " + exception.Message); }
		}
	}
}
