using System;
using System.Collections.Generic;
using System.Globalization;

namespace ThousandAndFirst
{
	/// <summary>Versioned legacy-profile capture and backward-compatible unresolved import.</summary>
	public static partial class KingdomPolityProfileRules
	{
		public const int UnresolvedLegacyProfileSchema = 0;
		public const int CurrentLegacyProfileSchema = 1;

		public static bool TryCreateLegacy(string PolityId, KingdomPolityLegacySnapshot Facts,
			long EffectiveTick, out KingdomPolityProfileRevision Profile, out string Failure)
		{
			Profile = null; Failure = null;
			if (!KingdomPolityRules.SemanticId(PolityId) || EffectiveTick < 0L ||
				!ValidLegacy(Facts, out Failure))
			{
				if (Failure == null) Failure = "legacy profile input is invalid";
				return false;
			}
			List<string> origins = WeightedKeys(Facts.OriginKeys, Facts.OriginCounts);
			List<string> creeds = WeightedKeys(Facts.CreedKeys, Facts.CreedCounts);
			string creed = creeds.Count == 0 ? "" : creeds[0];
			List<string> digestFacts = LegacyDigestFacts(Facts, origins, creeds);
			bool resolved = Facts.ProfileSchema == CurrentLegacyProfileSchema;
			string digest = KingdomPolityRules.ActivationDigest(resolved ?
				"polity-profile-legacy-v2" : "polity-profile-legacy-v1", digestFacts);
			// Schema-zero snapshots remain readable and institutionally importable. Their phenotype
			// is deliberately unresolved: zero is a non-expressed placeholder, not a technology claim.
			int technology = resolved ? Facts.TechnologyBand : 0;
			IList<string> bodies = resolved ? Facts.CanonicalBodyKeys :
				new List<string> { "unresolved" };
			Profile = Build(PolityId, digest, EffectiveTick, technology,
				Facts.Vocation, Facts.Style, creed, bodies, true, !resolved);
			return true;
		}

		/// <summary>
		/// Copies only immutable profile phenotype into a newly-created legacy seal. Profiles from
		/// older rules or unresolved body pools fail closed rather than acquiring invented facts.
		/// </summary>
		internal static bool TryCaptureLegacyProfile(KingdomPolityLegacySnapshot Facts,
			KingdomPolityProfileRevision Source, out string Failure)
		{
			Failure = null;
			if (Facts == null || Source == null || Source.RulesVersion != RulesVersion ||
				Source.TechnologyBand < 0 || Source.TechnologyBand > 10 ||
				!ValidCanonicalBodies(Source.BodyKeys))
			{
				Failure = "current polity profile lacks canonical seal-safe phenotype provenance";
				return false;
			}
			string sourceDigest = KingdomPolityRules.LegacySealPhenotypeDigest(Source);
			if (!KingdomPolityRules.Digest(sourceDigest))
			{
				Failure = "current polity profile commitment is invalid"; return false;
			}
			Facts.ProfileSchema = CurrentLegacyProfileSchema;
			Facts.TechnologyBand = Source.TechnologyBand;
			Facts.CanonicalBodyKeys = new List<string>(Source.BodyKeys);
			Facts.SourceProfileDigest = sourceDigest;
			Facts.ProfileProvenanceDigest = LegacyProfileProvenanceDigest(Facts.ProfileSchema,
				Facts.TechnologyBand, Facts.CanonicalBodyKeys, Facts.SourceProfileDigest);
			return true;
		}

		internal static string LegacyProfileProvenanceDigest(int Schema, int TechnologyBand,
			IList<string> CanonicalBodyKeys, string SourceProfileDigest)
		{
			List<string> values = new List<string>
			{
				Schema.ToString(CultureInfo.InvariantCulture),
				TechnologyBand.ToString(CultureInfo.InvariantCulture),
				SourceProfileDigest ?? ""
			};
			AddLane(values, "body", CanonicalBodyKeys);
			return KingdomPolityRules.ActivationDigest("polity-legacy-profile-provenance-v1", values);
		}

		internal static bool MatchesLegacyProfileSource(KingdomPolityLegacySnapshot Facts,
			KingdomPolityProfileRevision Source)
		{
			// Old prepared transition receipts remain byte-for-byte readable. They make no exact
			// phenotype claim, so only newly stamped schemas can be matched to source authority.
			if (Facts == null || Source == null) return false;
			if (Facts.ProfileSchema == UnresolvedLegacyProfileSchema) return true;
			if (!ValidLegacyProfile(Facts) || Source.RulesVersion != RulesVersion ||
				Facts.TechnologyBand != Source.TechnologyBand ||
				Facts.SourceProfileDigest != KingdomPolityRules.LegacySealPhenotypeDigest(Source) ||
				Facts.CanonicalBodyKeys.Count != Source.BodyKeys.Count) return false;
			for (int i = 0; i < Facts.CanonicalBodyKeys.Count; i++)
				if (Facts.CanonicalBodyKeys[i] != Source.BodyKeys[i]) return false;
			return true;
		}

		internal static bool ValidLegacy(KingdomPolityLegacySnapshot F, out string Failure)
		{
			Failure = null;
			if (F == null || !KingdomPolityRules.Text(F.LegacyToken, true) ||
				!KingdomPolityRules.Text(F.LineageToken, true) ||
				!KingdomPolityRules.Text(F.FounderName, false) ||
				!KingdomPolityRules.Text(F.RealmName, true) ||
				!KingdomPolityRules.Text(F.SettlementName, false) ||
				!KingdomPolityRules.Text(F.Vocation, false) ||
				!KingdomPolityRules.Text(F.Style, false) || F.Stage < 0 || F.Stage > 5 ||
				F.Population < 0 || F.Population > 10000 || F.Defence < 0 ||
				F.Defence > 100000 || F.StoredWater < 0 || F.StoredWater > 10000000 ||
				F.InheritedState < 0 || F.InheritedState > 16 ||
				!BoundedText(F.RollNames, 64) || !PairedCounts(F.OriginKeys, F.OriginCounts) ||
				!PairedCounts(F.CreedKeys, F.CreedCounts) || !ValidLegacyProfile(F))
			{
				Failure = "committed legacy facts are invalid, unbounded, or carry torn profile provenance";
				return false;
			}
			return true;
		}

		internal static bool ValidLegacyProfile(KingdomPolityLegacySnapshot F)
		{
			if (F.CanonicalBodyKeys == null) return false;
			if (F.ProfileSchema == UnresolvedLegacyProfileSchema)
				return F.TechnologyBand == 0 && F.CanonicalBodyKeys.Count == 0 &&
					string.IsNullOrEmpty(F.SourceProfileDigest) &&
					string.IsNullOrEmpty(F.ProfileProvenanceDigest);
			if (F.ProfileSchema != CurrentLegacyProfileSchema ||
				F.TechnologyBand < 0 || F.TechnologyBand > 10 ||
				!ValidCanonicalBodies(F.CanonicalBodyKeys) ||
				!KingdomPolityRules.Digest(F.SourceProfileDigest) ||
				!KingdomPolityRules.Digest(F.ProfileProvenanceDigest)) return false;
			return F.ProfileProvenanceDigest == LegacyProfileProvenanceDigest(F.ProfileSchema,
				F.TechnologyBand, F.CanonicalBodyKeys, F.SourceProfileDigest);
		}

		private static bool ValidCanonicalBodies(IList<string> Values)
		{
			if (Values == null || Values.Count == 0 || Values.Count > 6) return false;
			string previous = null;
			for (int i = 0; i < Values.Count; i++)
			{
				string value = Values[i];
				if (value != "human" && value != "snapjaw" && value != "goatfolk" &&
					value != "dromad" && value != "hindren" && value != "mechanical" ||
					previous != null && string.CompareOrdinal(previous, value) >= 0) return false;
				previous = value;
			}
			return true;
		}

		private static List<string> LegacyDigestFacts(KingdomPolityLegacySnapshot F,
			IList<string> Origins, IList<string> Creeds)
		{
			List<string> values = Merge(Origins, Creeds, F.RollNames, null);
			values.Insert(0, F.LegacyToken); values.Insert(1, F.LineageToken);
			values.Add(F.RealmName ?? ""); values.Add(F.SettlementName ?? "");
			values.Add(F.Vocation ?? ""); values.Add(F.Style ?? "");
			values.Add(F.Stage.ToString(CultureInfo.InvariantCulture));
			values.Add(F.Population.ToString(CultureInfo.InvariantCulture));
			values.Add(F.Defence.ToString(CultureInfo.InvariantCulture));
			values.Add(F.StoredWater.ToString(CultureInfo.InvariantCulture));
			values.Add(F.InheritedState.ToString(CultureInfo.InvariantCulture));
			if (F.ProfileSchema == CurrentLegacyProfileSchema)
			{
				values.Add("profile=" + F.ProfileProvenanceDigest);
				values.Add("technology=" + F.TechnologyBand.ToString(CultureInfo.InvariantCulture));
				AddLane(values, "body", F.CanonicalBodyKeys);
			}
			return values;
		}

		private static List<string> WeightedKeys(IList<string> Keys, IList<int> Counts)
		{
			List<string> result = new List<string>();
			for (int i = 0; Keys != null && Counts != null && i < Keys.Count; i++)
				AddUnique(result, Keys[i] + "=" + Counts[i].ToString(CultureInfo.InvariantCulture));
			result.Sort(StringComparer.Ordinal); return result;
		}
	}
}
