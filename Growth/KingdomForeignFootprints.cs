using System;
using System.Collections.Generic;
using System.Text;
using ThousandAndFirst.Api;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Read-only, bounded registry for foreign exact-cell evidence. Foreign rows never
	/// create civic roles; an explicit TAF adoption may bind one exact row into its own receipt.</summary>
	[HasModSensitiveStaticCache]
	internal static partial class KingdomForeignFootprints
	{
		private sealed class ProviderRow
		{
			internal string Id;
			internal string Version;
			internal IKingdomForeignFootprintProvider Provider;
		}

		private sealed class RawSnapshot
		{
			internal ProviderRow Provider;
			internal KingdomForeignProviderStatus Status;
			internal string Fault;
			internal KingdomForeignFootprint[] Rows;
		}

		[ModSensitiveStaticCache]
		private static List<ProviderRow> Providers;
		[ModSensitiveStaticCache]
		private static List<string> RegistrationFaults;
		[ModSensitiveStaticCache]
		private static bool RegistrationFaultsReported;

		private static bool TryObserveAll(Zone Z,
			out List<KingdomForeignProviderSnapshot> Snapshots, out string Failure)
		{
			Snapshots = new List<KingdomForeignProviderSnapshot>(); Failure = null;
			if (Z == null) return Fail("active zone is absent", out Failure);
			List<ProviderRow> providers = Registry();
			ReportRegistrationFaults();
			Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
			for (int i = 0; i < providers.Count; i++)
				counts[providers[i].Id] = counts.TryGetValue(providers[i].Id, out int count)
					? count + 1 : 1;
			HashSet<string> emitted = new HashSet<string>(StringComparer.Ordinal);
			List<RawSnapshot> raw = new List<RawSnapshot>();
			for (int i = 0; i < providers.Count; i++)
			{
				ProviderRow provider = providers[i];
				if (!emitted.Add(provider.Id)) continue;
				if (counts[provider.Id] != 1)
				{
					raw.Add(Faulted(provider, "duplicate provider registration")); continue;
				}
				RawSnapshot observed = Invoke(provider, Z);
				raw.Add(observed);
			}
			for (int i = 0; i < raw.Count; i++)
			{
				RawSnapshot source = raw[i]; ProviderRow provider = source.Provider;
				if (source.Status == KingdomForeignProviderStatus.Faulted)
				{
					Snapshots.Add(Snapshot(provider, source.Status, source.Fault)); continue;
				}
				if (source.Status == KingdomForeignProviderStatus.Absent)
				{
					Snapshots.Add(Snapshot(provider, source.Status, null)); continue;
				}
				if (!TryNormalizeProvider(provider, source.Rows, Z,
					out List<KingdomForeignFootprintEvidence> rows,
					out List<string> rowFaults, out string failure))
				{
					Snapshots.Add(Snapshot(provider, KingdomForeignProviderStatus.Faulted,
						Bound(failure))); continue;
				}
				KingdomForeignProviderSnapshot snapshot = Snapshot(provider,
					KingdomForeignProviderStatus.Observed, null);
				snapshot.Rows.AddRange(rows); snapshot.RowFaults.AddRange(rowFaults);
				Snapshots.Add(snapshot);
			}
			RefuseCrossProviderOverlaps(Snapshots);
			KingdomForeignFootprintBudgetRules.Apply(Snapshots);
			return KingdomForeignFootprintSnapshotRules.TryValidate(Snapshots, out Failure);
		}

		private static RawSnapshot Invoke(ProviderRow Provider, Zone Z)
		{
			KingdomForeignFootprint[] rows = null; string failure = null; bool found;
			try { found = Provider.Provider.TryObserve(Z, out rows, out failure); }
			catch (Exception exception)
			{
				return Faulted(Provider, "threw " + exception.GetType().Name);
			}
			KingdomForeignProviderStatus status =
				KingdomForeignFootprintSnapshotRules.ClassifyCall(found,
					rows != null, failure != null);
			if (status == KingdomForeignProviderStatus.Absent)
				return new RawSnapshot { Provider = Provider,
					Status = KingdomForeignProviderStatus.Absent };
			if (status == KingdomForeignProviderStatus.Faulted)
				return new RawSnapshot { Provider = Provider,
					Status = KingdomForeignProviderStatus.Faulted,
					Fault = Bound(failure ?? "provider returned an inconsistent observation"),
					Rows = rows };
			return new RawSnapshot { Provider = Provider,
				Status = KingdomForeignProviderStatus.Observed, Rows = rows };
		}

		private static RawSnapshot Faulted(ProviderRow Provider, string Failure)
		{
			return new RawSnapshot { Provider = Provider,
				Status = KingdomForeignProviderStatus.Faulted, Fault = Bound(Failure) };
		}

		private static KingdomForeignProviderSnapshot Snapshot(ProviderRow Provider,
			KingdomForeignProviderStatus Status, string Fault)
		{
			return new KingdomForeignProviderSnapshot { ProviderId = Provider.Id,
				ProviderVersion = Provider.Version, Status = Status, Fault = Fault };
		}

		private static List<ProviderRow> Registry()
		{
			if (Providers != null) return Providers;
			Providers = new List<ProviderRow>(); RegistrationFaults = new List<string>();
			RegistrationFaultsReported = false;
			List<Type> types;
			try { types = ModManager.GetTypesWithAttribute(
				typeof(KingdomForeignFootprintProviderAttribute)); }
			catch (Exception exception)
			{
				RegistrationFaults.Add("provider discovery threw " + exception.GetType().Name);
				return Providers;
			}
			if (types == null)
			{
				RegistrationFaults.Add("provider discovery returned no roster"); return Providers;
			}
			types.Sort((a, b) => string.CompareOrdinal(a?.FullName, b?.FullName));
			int count = Math.Min(types.Count, KingdomForeignFootprintSnapshotRules.MaxProviders);
			for (int i = 0; i < count; i++) Collect(types[i]);
			if (types.Count > KingdomForeignFootprintSnapshotRules.MaxProviders)
				RegistrationFaults.Add("foreign footprint provider bound exceeded");
			Providers.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id)); return Providers;
		}

		private static void Collect(Type Type)
		{
			try
			{
				if (Type == null || !typeof(IKingdomForeignFootprintProvider).IsAssignableFrom(Type))
					throw new InvalidOperationException("marked type has no footprint contract");
				IKingdomForeignFootprintProvider provider =
					Activator.CreateInstance(Type) as IKingdomForeignFootprintProvider;
				string id = provider?.ProviderId; string version = provider?.ProviderVersion;
				if (!KingdomForeignFootprintSnapshotRules.SafeToken(id, 64)
					|| !KingdomForeignFootprintSnapshotRules.SafeToken(version, 32))
					throw new InvalidOperationException("provider identity is malformed");
				Providers.Add(new ProviderRow { Id = id, Version = version, Provider = provider });
			}
			catch (Exception exception)
			{
				if (RegistrationFaults.Count < KingdomForeignFootprintSnapshotRules.MaxProviders)
					RegistrationFaults.Add(Bound((Type?.FullName ?? "<unknown>") + ": "
						+ exception.GetType().Name));
			}
		}

		private static string Bound(string Value)
		{
			if (string.IsNullOrEmpty(Value)) return "unspecified provider fault";
			int maximum = KingdomForeignFootprintSnapshotRules.MaxFaultChars;
			StringBuilder result = new StringBuilder(Math.Min(maximum, Value.Length));
			int scan = Math.Min(Value.Length, maximum * 4); bool gap = false;
			for (int i = 0; i < scan && result.Length < maximum; i++)
			{
				char ch = Value[i];
				if (char.IsControl(ch) || char.IsWhiteSpace(ch)) { gap = result.Length > 0; continue; }
				if (gap && result.Length + 1 < maximum) result.Append(' ');
				gap = false;
				if (result.Length < maximum) result.Append(ch);
			}
			return result.Length == 0 ? "unspecified provider fault" : result.ToString();
		}

		private static void ReportRegistrationFaults()
		{
			if (RegistrationFaultsReported || RegistrationFaults == null) return;
			RegistrationFaultsReported = true;
			for (int i = 0; i < RegistrationFaults.Count; i++)
				KingdomLog.Log("foreign footprint registration quarantined: "
					+ Bound(RegistrationFaults[i]));
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
