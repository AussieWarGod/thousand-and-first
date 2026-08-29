using System;
using System.Collections.Generic;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	/// <summary>Copies bounded institutional facts from exact exile archive; never body/object ids.</summary>
	internal static class KingdomPolityRealmLegacyFacts
	{
		internal static bool TryCreate(KingdomRealmArchive Archive, KingdomPolityLedger Ledger,
			string FounderName, out KingdomPolityRealmExileFacts Facts, out string Failure)
		{
			Facts = null; Failure = null;
			if (Archive == null || Archive.Quarantined || !Archive.Validate(out Failure) ||
				!KingdomPolityRules.TryValidate(Ledger, out Failure) ||
				Ledger.RealmId != Archive.RealmId || Archive.ClosedTick <= 0L)
			{
				Failure = Failure ?? "exile archive cannot supply polity legacy facts"; return false;
			}
			KingdomPolityRecord current = Current(Ledger);
			KingdomPolityProfileRevision profile = current == null ? null : Profile(Ledger, current);
			if (current == null || profile == null || current.ProjectedFactionId != Archive.FactionName)
			{
				Failure = "exile archive differs from current polity foundation"; return false;
			}
			List<KingdomSettlement> settlements = new List<KingdomSettlement> { Archive.Seat };
			for (int i = 0; i < Archive.SettlementTopology.Count; i++)
				settlements.Add(Archive.SettlementTopology.Get(i));
			List<string> names = new List<string>();
			Dictionary<string, int> origins = new Dictionary<string, int>(StringComparer.Ordinal);
			Dictionary<string, int> creeds = new Dictionary<string, int>(StringComparer.Ordinal);
			long population = 0L, defence = 0L, water = 0L; int stage = 0;
			for (int i = 0; i < settlements.Count; i++)
			{
				KingdomSettlement settlement = settlements[i];
				if (settlement == null) continue;
				population += Math.Max(0, settlement.Population);
				stage = Math.Max(stage, Math.Max(0, Math.Min(5, (int)settlement.Stage)));
				AddCounts(creeds, settlement.CreedCounts);
				CaptureRoll(settlement.City, names, origins);
				for (int j = 0; settlement.City != null && j < settlement.City.ZoneDefences.Count; j++)
					defence += Math.Max(0, settlement.City.ZoneDefences[j]);
				for (int j = 0; settlement.City != null && j < settlement.City.ZoneWaterLevels.Count; j++)
					water += Math.Max(0L, settlement.City.ZoneWaterLevels[j]);
			}
			if (!string.IsNullOrEmpty(Archive.DeclaredCreed) && creeds.Count == 0)
				creeds[Archive.DeclaredCreed] = Math.Max(1, (int)Math.Min(10000L, population));
			KingdomPolityLegacySnapshot legacy = new KingdomPolityLegacySnapshot
			{
				LegacyToken = "exile-" + KingdomPolityRules.ActivationDigest(
					"polity-exile-legacy-token-v1", Archive.RealmId, profile.FactsDigest,
					Archive.ClosedTick.ToString(System.Globalization.CultureInfo.InvariantCulture)),
				LineageToken = "lineage-" + KingdomPolityRules.ActivationDigest(
					"polity-exile-lineage-token-v1", profile.FactsDigest),
				FounderName = Bounded(FounderName), RealmName = Archive.DisplayName,
				SettlementName = Archive.Seat?.SettlementName, Vocation = Archive.Seat?.Vocation,
				Style = Archive.Seat?.Style, Stage = stage,
				Population = (int)Math.Min(10000L, population),
				Defence = (int)Math.Min(100000L, defence),
				StoredWater = (int)Math.Min(10000000L, water), InheritedState = 0,
				RollNames = CanonicalNames(names)
			};
			CopyCounts(origins, legacy.OriginKeys, legacy.OriginCounts);
			CopyCounts(creeds, legacy.CreedKeys, legacy.CreedCounts);
			if (!KingdomPolityProfileRules.ValidLegacy(legacy, out Failure)) return false;
			Facts = new KingdomPolityRealmExileFacts
			{
				RealmId = Archive.RealmId, FactionId = Archive.FactionName,
				ClosedTick = Archive.ClosedTick, Legacy = legacy
			};
			return true;
		}

		private static void CaptureRoll(KingdomCityBook Book, List<string> Names,
			Dictionary<string, int> Origins)
		{
			KingdomCityState state; KingdomCityFault fault; KingdomResidentRollProjection roll;
			if (Book == null || !Book.TryRead(out state, out fault) ||
				!KingdomResidentRules.TryProject(state, out roll)) return;
			for (int i = 0; i < roll.Names.Count && Names.Count < 64; i++)
				if (KingdomPolityRules.Text(roll.Names[i], true)) Names.Add(roll.Names[i]);
			for (int i = 0; i < roll.Origins.Count; i++)
				AddCount(Origins, roll.Origins[i], 1);
		}

		private static void AddCounts(Dictionary<string, int> Target,
			Dictionary<string, int> Source)
		{
			foreach (KeyValuePair<string, int> row in Source ?? new Dictionary<string, int>())
				AddCount(Target, row.Key, row.Value);
		}

		private static void AddCount(Dictionary<string, int> Target, string Key, int Count)
		{
			if (!KingdomPolityRules.Text(Key, true) || Count <= 0) return;
			Target.TryGetValue(Key, out int old);
			Target[Key] = (int)Math.Min(10000000L, (long)old + Count);
		}

		private static void CopyCounts(Dictionary<string, int> Source, List<string> Keys,
			List<int> Counts)
		{
			List<string> sorted = new List<string>(Source.Keys); sorted.Sort(StringComparer.Ordinal);
			for (int i = 0; i < sorted.Count && i < 64; i++)
			{
				Keys.Add(sorted[i]); Counts.Add(Source[sorted[i]]);
			}
		}

		private static List<string> CanonicalNames(List<string> Source)
		{
			Source.Sort(StringComparer.Ordinal); List<string> result = new List<string>();
			for (int i = 0; i < Source.Count && result.Count < 64; i++)
				if (i == 0 || Source[i] != Source[i - 1]) result.Add(Source[i]);
			return result;
		}

		private static string Bounded(string Value)
		{
			return KingdomPolityRules.Text(Value, false) ? Value : null;
		}

		private static KingdomPolityRecord Current(KingdomPolityLedger L)
		{
			for (int i = 0; i < L.Polities.Count; i++)
				if (L.Polities[i].Source == KingdomPolitySource.CurrentRealm) return L.Polities[i];
			return null;
		}

		private static KingdomPolityProfileRevision Profile(KingdomPolityLedger L,
			KingdomPolityRecord P)
		{
			for (int i = 0; i < L.Profiles.Count; i++)
				if (L.Profiles[i].ProfileId == P.ProfileId &&
					L.Profiles[i].Revision == P.ProfileRevision) return L.Profiles[i];
			return null;
		}
	}
}
