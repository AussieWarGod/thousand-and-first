using System;
using System.Collections.Generic;
using System.Globalization;

namespace ThousandAndFirst
{
	/// <summary>Reads persisted realm/city decisions only; never surveys bodies or remote zones.</summary>
	public static partial class KingdomPolityProfileRuntime
	{
		public static bool TryReconcile(KingdomSystem System, long Tick, out string Failure)
		{
			Failure = null;
			if (System == null || !System.Founded || System.PolityLedger == null || Tick < 0L)
				return true;
			KingdomPolityLedger ledger = System.PolityLedger;
			if (!KingdomPolityRules.TryValidate(ledger, out Failure)) return false;
			for (int i = 0; i < ledger.Polities.Count; i++)
			{
				if (!TryCompactForCapacity(ledger, Tick,
					ledger.Profiles.Count >= KingdomPolityRules.MaxProfiles - 1, out Failure))
					return false;
				KingdomPolityRecord polity = ledger.Polities[i];
				if (polity.Lifecycle != KingdomPolityLifecycle.Active) continue;
				KingdomPolityProfileFactSet facts = polity.Source ==
					KingdomPolitySource.CurrentRealm ? CurrentFacts(System, polity, Tick) :
					ExternalFacts(ledger, polity, Tick);
				if (facts == null || !KingdomPolityProfileRules.TryRevise(ledger,
					ledger.Revision, facts, out KingdomPolityPublicationResult _, out Failure))
					return false;
			}
			return TryCompactForCapacity(ledger, Tick, ledger.Profiles.Count >= 12,
				out Failure);
		}

		private static KingdomPolityProfileFactSet CurrentFacts(KingdomSystem S,
			KingdomPolityRecord Polity, long Tick)
		{
			KingdomPolityProfileFactSet result = Begin(Polity, Tick);
			int technology = 0;
			AddCityFacts(result, S.City?.SettlementId, S.Vocation, S.Style, S.Gate,
				S.Stores, KingdomZoning.Roster(S), S.Population, S.SpeciesCounts,
				S.IdentityCounts, ref technology);
			List<KingdomSettlement> rows = S.NonSeatSettlements();
			for (int i = 0; i < rows.Count; i++)
			{
				KingdomSettlement row = rows[i];
				AddCityFacts(result, row?.City?.SettlementId, row?.Vocation, row?.Style,
					row == null ? KingdomRules.GatePolicy.Open : row.Gate,
					row == null ? KingdomRules.StoresPolicy.Plenty : row.Stores,
					KingdomZoning.RosterOf(row), row == null ? 0 : row.Population,
					row?.SpeciesCounts, row?.IdentityCounts, ref technology);
			}
			if (!string.IsNullOrEmpty(S.DeclaredCreed)) Add(result,
				KingdomPolityProfileFactKind.Creed, S.RealmId,
				"declared=" + S.DeclaredCreed);
			AddRelations(result, S.PolityLedger, Polity.PolityId);
			result.TechnologyBand = Math.Min(10, technology);
			result.Facts.Sort((a, b) => string.CompareOrdinal(a.FactId, b.FactId)); return result;
		}

		private static KingdomPolityProfileFactSet ExternalFacts(KingdomPolityLedger L,
			KingdomPolityRecord Polity, long Tick)
		{
			KingdomPolityProfileFactSet result = Begin(Polity, Tick);
			KingdomPolityProfileRevision root = KingdomPolityAuthority.Profile(L,
				Polity.ProfileId, 1);
			if (root == null) return null;
			result.TechnologyBand = root.TechnologyBand;
			Add(result, KingdomPolityProfileFactKind.Technology, Polity.PolityId,
				"legacy-band=" + root.TechnologyBand.ToString(CultureInfo.InvariantCulture));
			for (int i = 0; i < root.DerivedFromFactIds.Count; i++)
				Add(result, KingdomPolityProfileFactKind.Legacy, root.DerivedFromFactIds[i],
					"committed-legacy=" + root.DerivedFromFactIds[i]);
			AddRelations(result, L, Polity.PolityId);
			result.Facts.Sort((a, b) => string.CompareOrdinal(a.FactId, b.FactId)); return result;
		}

		private static KingdomPolityProfileFactSet Begin(KingdomPolityRecord P, long Tick)
		{
			return new KingdomPolityProfileFactSet
			{
				PolityId = P.PolityId, ProfileId = P.ProfileId,
				PreviousRevision = P.ProfileRevision, EffectiveTick = Tick
			};
		}

		private static void AddCityFacts(KingdomPolityProfileFactSet F, string SettlementId,
			string Vocation, string Style, KingdomRules.GatePolicy Gate,
			KingdomRules.StoresPolicy Stores, IList<string> Roster, int Population,
			IDictionary<string, int> Species, IDictionary<string, int> Identity,
			ref int Technology)
		{
			if (!KingdomPolityRules.TypedId(SettlementId, "taf:settlement:v1:")) return;
			string decision = "gate=" + ((int)Gate).ToString(CultureInfo.InvariantCulture) +
				";stores=" + ((int)Stores).ToString(CultureInfo.InvariantCulture) +
				";vocation=" + (Vocation ?? "");
			Add(F, KingdomPolityProfileFactKind.Decision, SettlementId, decision);
			Add(F, KingdomPolityProfileFactKind.Style, SettlementId,
				"style=" + (Style ?? "common"));
			List<string> roster = Roster == null ? new List<string>() : new List<string>(Roster);
			roster.Sort(StringComparer.Ordinal);
			int localTechnology = (int)KingdomZoningRules.LevelForPoints(
				KingdomZoningRules.TechPoints(roster)) * 2;
			string rosterDigest = KingdomPolityRules.ActivationDigest(
				"polity-technology-roster-v2", roster);
			Add(F, KingdomPolityProfileFactKind.Technology, SettlementId,
				"band=" + localTechnology.ToString(CultureInfo.InvariantCulture) +
				";roster=" + rosterDigest);
			Technology = Math.Max(Technology, localTechnology);
			List<string> bodies = KingdomPolityProfileRules.CurrentBodyKeys(
				PositiveKeys(Species), PositiveKeys(Identity), false);
			if (Population <= 0) Add(F, KingdomPolityProfileFactKind.Population,
				SettlementId, "population=none");
			else if (bodies.Count == 0) Add(F, KingdomPolityProfileFactKind.Population,
				SettlementId, "population=unresolved");
			else for (int i = 0; i < bodies.Count; i++) Add(F,
				KingdomPolityProfileFactKind.Population, SettlementId, "body=" + bodies[i]);
		}

		private static List<string> PositiveKeys(IDictionary<string, int> Values)
		{
			List<string> result = new List<string>();
			if (Values != null) foreach (KeyValuePair<string, int> row in Values)
				if (row.Value > 0) result.Add(row.Key);
			result.Sort(StringComparer.Ordinal); return result;
		}

		private static void AddRelations(KingdomPolityProfileFactSet F,
			KingdomPolityLedger L, string PolityId)
		{
			for (int i = 0; i < L.Relations.Count && F.Facts.Count < KingdomPolityRules.MaxRefs; i++)
			{
				KingdomPolityRelation relation = L.Relations[i];
				if (relation.FromPolityId != PolityId && relation.ToPolityId != PolityId) continue;
				List<string> evidence = new List<string>(relation.SourceRefs);
				string digest = KingdomPolityRules.ActivationDigest(
					"polity-relation-evidence-v1", evidence);
				Add(F, relation.Band == KingdomPolityRelationBand.Pact ?
					KingdomPolityProfileFactKind.Alliance :
					KingdomPolityProfileFactKind.Relationship, relation.RelationId,
					"band=" + ((byte)relation.Band).ToString(CultureInfo.InvariantCulture) +
					";evidence=" + digest);
			}
		}

		private static void Add(KingdomPolityProfileFactSet F,
			KingdomPolityProfileFactKind Kind, string Source, string Value)
		{
			if (F.Facts.Count >= KingdomPolityRules.MaxRefs ||
				!KingdomPolityRules.SemanticId(Source) || !KingdomPolityRules.Text(Value, true)) return;
			string id = KingdomPolityRules.ActivationId("taf:fact:profile:v1:",
				"polity-concrete-profile-fact-v1", F.PolityId,
				((byte)Kind).ToString(CultureInfo.InvariantCulture), Source, Value);
			for (int i = 0; i < F.Facts.Count; i++) if (F.Facts[i].FactId == id) return;
			F.Facts.Add(new KingdomPolityProfileFact
			{
				FactId = id, Kind = Kind, ValueKey = Value, SourceRef = Source
			});
		}
	}
}
