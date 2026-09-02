using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Live adapter from founded realm facts to bounded polity authority.</summary>
	public static class KingdomPolityRuntime
	{
		public static bool TryEnsureFoundation(KingdomSystem System, Faction CurrentFaction,
			long Tick, out string Failure)
		{
			Failure = null;
			if (System == null || !System.Founded || CurrentFaction == null || Tick < 0L ||
				System.PolityLedger == null || CurrentFaction.Name != System.KingdomFactionName ||
				CurrentFaction.Name != System.RealmId)
			{
				Failure = "live realm facts do not prove a current polity foundation"; return false;
			}
			KingdomPolityRules.Normalize(System.PolityLedger);
			if (System.PolityLedger.SchemaState != KingdomPolitySchemaState.Compatible)
			{
				Failure = System.PolityLedger.SchemaFault ?? "polity authority is quarantined"; return false;
			}
			if (KingdomPolityRules.TryObserveCurrentFoundation(System.PolityLedger,
				System.RealmId, CurrentFaction.Name, out string observedFailure))
			{
				if (!KingdomPolityFactionRuntime.TryReconcile(System, Tick, out Failure)) return false;
				return KingdomPolityRealmTransitionRuntime.TryCommitRefound(System, out Failure);
			}
			if (!EmptySemantic(System.PolityLedger))
			{
				Failure = observedFailure;
				KingdomPolityRules.Quarantine(System.PolityLedger, Failure); return false;
			}
			KingdomPolityLegacySnapshot legacy = null; bool transitionLegacy;
			if (!KingdomPolityRealmTransitionRuntime.TryFoundationLegacy(System, out legacy,
				out transitionLegacy, out Failure))
			{
				KingdomPolityRules.Quarantine(System.PolityLedger, Failure); return false;
			}
			KingdomInheritanceState inheritance = KingdomInheritanceState.Instance;
			if (!transitionLegacy && inheritance != null &&
				inheritance.Phase == KingdomInheritancePhase.Committed &&
				!inheritance.TryPolityLegacySnapshot(out legacy, out Failure))
			{
				KingdomPolityRules.Quarantine(System.PolityLedger, Failure); return false;
			}
			KingdomPolityImportPolicy foundationPolicy = legacy == null ?
				KingdomPolityImportPolicy.Off : KingdomPolityImportPolicy.LatestEligible;
			if (!KingdomPolityRules.TryRebindEmptyIdentity(System.PolityLedger,
				System.RealmId, foundationPolicy, out Failure))
			{
				KingdomPolityRules.Quarantine(System.PolityLedger, Failure); return false;
			}
			if (legacy != null && System.PolityLedger.Options.ImportPolicy ==
				KingdomPolityImportPolicy.Off && EmptySemantic(System.PolityLedger))
			{
				if (!KingdomPolityRules.TrySetEmptyImportPolicy(System.PolityLedger,
					System.PolityLedger.Revision, KingdomPolityImportPolicy.LatestEligible,
					out KingdomPolityPublicationResult _, out Failure)) return false;
			}
			KingdomPolityFoundationFacts facts = Facts(System, CurrentFaction);
			if (!KingdomPolityRules.TryPublishFoundation(System.PolityLedger,
				System.PolityLedger.Revision, facts, legacy,
				out KingdomPolityPublicationResult _, out Failure))
			{
				KingdomPolityRules.Quarantine(System.PolityLedger, Failure); return false;
			}
			if (!KingdomPolityFactionRuntime.TryReconcile(System, Tick, out Failure)) return false;
			return KingdomPolityRealmTransitionRuntime.TryCommitRefound(System, out Failure);
		}

		public static bool FoundationObserved(KingdomSystem System, Faction CurrentFaction)
		{
			if (System == null || CurrentFaction == null || System.PolityLedger == null ||
				!KingdomPolityRules.TryObserveCurrentFoundation(System.PolityLedger,
					System.RealmId, CurrentFaction.Name, out string _)) return false;
			KingdomPolityRecord imported = Imported(System.PolityLedger);
			return (imported == null || KingdomPolityFactionRuntime.ProjectionObserved(
				System, imported)) && KingdomPolityRealmTransitionRuntime.RefoundObserved(System);
		}

		private static KingdomPolityFoundationFacts Facts(KingdomSystem S, Faction F)
		{
			return new KingdomPolityFoundationFacts
			{
				RealmId = S.RealmId, FactionId = F.Name,
				DisplayName = string.IsNullOrEmpty(S.KingdomDisplayName) ? F.DisplayName : S.KingdomDisplayName,
				FounderName = The.Player?.BaseDisplayNameStripped, SettlementId = S.City?.SettlementId,
				Vocation = S.Vocation, Style = S.Style, Creed = S.DeclaredCreed,
				Stage = (int)S.Stage, TechnologyBand = (int)KingdomZoning.Tech(S) * 2,
				Population = Math.Max(0, S.Population),
				FoundedTick = Math.Max(0L, S.FoundedTick), OriginKeys = TopKeys(S.OriginCounts),
				CultureKeys = TopKeys(S.CultureCounts),
				SpeciesKeys = FounderFirst(TopKeys(S.SpeciesCounts), The.Player?.GetSpecies()),
				IdentityKeys = TopKeys(S.IdentityCounts)
			};
		}

		/// <summary>
		/// The founder is the realm's first body. Resident censuses count only residents, so a
		/// realm founded moments ago would otherwise present no species at all and its profile
		/// phenotype could never resolve; the founding seal fails closed on an unresolved body
		/// pool by law. The founder's species leads the list; it is a fact, not an inference.
		/// </summary>
		private static List<string> FounderFirst(List<string> Species, string FounderSpecies)
		{
			string founder = (FounderSpecies ?? "").Trim().ToLowerInvariant();
			if (founder.Length == 0 || !KingdomPolityRules.Text(founder, true)) return Species;
			List<string> result = new List<string> { founder };
			for (int i = 0; Species != null && i < Species.Count; i++)
				if (!string.Equals(Species[i], founder, StringComparison.Ordinal)
					&& result.Count < KingdomPolityRules.MaxRefs) result.Add(Species[i]);
			return result;
		}

		private static List<string> TopKeys(Dictionary<string, int> Source)
		{
			List<KeyValuePair<string, int>> rows = new List<KeyValuePair<string, int>>();
			foreach (KeyValuePair<string, int> row in Source ??
				new Dictionary<string, int>()) if (row.Value > 0 &&
				KingdomPolityRules.Text(row.Key, true)) rows.Add(row);
			rows.Sort(delegate(KeyValuePair<string, int> a, KeyValuePair<string, int> b)
			{
				int count = b.Value.CompareTo(a.Value);
				return count != 0 ? count : string.CompareOrdinal(a.Key, b.Key);
			});
			List<string> result = new List<string>();
			for (int i = 0; i < rows.Count && i < 16; i++) result.Add(rows[i].Key);
			result.Sort(StringComparer.Ordinal); return result;
		}

		private static bool EmptySemantic(KingdomPolityLedger L)
		{
			return L.Polities.Count == 0 && L.Relations.Count == 0 && L.Profiles.Count == 0 &&
				L.Routes.Count == 0 && L.Grievances.Count == 0 && L.Fronts.Count == 0 &&
				L.Cohorts.Count == 0 && L.NamedFigures.Count == 0 && L.Incidents.Count == 0 &&
				L.Projections.Count == 0;
		}

		private static KingdomPolityRecord Imported(KingdomPolityLedger L)
		{
			for (int i = 0; i < L.Polities.Count; i++)
				if (L.Polities[i].Source == KingdomPolitySource.ImportedLegacy) return L.Polities[i];
			return null;
		}
	}
}
