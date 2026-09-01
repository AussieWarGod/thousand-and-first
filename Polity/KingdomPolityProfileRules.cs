using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Builds immutable, deterministic profile revisions from bounded semantic facts.</summary>
	public static partial class KingdomPolityProfileRules
	{
		public const int LegacyRulesVersion = 1;
		// Version 2 is readable so an in-flight pre-v1 cohort can finish byte-identically. It
		// predates the causal surface law and is never minted by current profile construction.
		public const int PriorExpressionRulesVersion = 2;
		public const int RulesVersion = 3;

		public static bool TryCreateCurrent(KingdomPolityFoundationFacts Facts,
			out KingdomPolityProfileRevision Profile, out string Failure)
		{
			Profile = null; Failure = null;
			if (!ValidFoundation(Facts, out Failure)) return false;
			List<string> bodies = CurrentBodyKeys(Facts.SpeciesKeys, Facts.IdentityKeys, true);
			string digest = FoundationDigest(Facts);
			Profile = Build(Facts.RealmId, digest, Facts.FoundedTick,
				Facts.TechnologyBand, Facts.Vocation, Facts.Style, Facts.Creed,
				bodies, false);
			return true;
		}

		internal static bool ValidFoundation(KingdomPolityFoundationFacts F, out string Failure)
		{
			Failure = null;
			if (F == null || !KingdomPolityRules.TypedId(F.RealmId, "taf:realm:") ||
				F.FactionId != F.RealmId ||
				!KingdomPolityRules.TypedId(F.SettlementId, "taf:settlement:v1:") ||
				!KingdomPolityRules.Text(F.DisplayName, true) ||
				!KingdomPolityRules.Text(F.FounderName, false) ||
				!KingdomPolityRules.Text(F.Vocation, false) ||
				!KingdomPolityRules.Text(F.Style, false) ||
				!KingdomPolityRules.Text(F.Creed, false) || F.Stage < 0 || F.Stage > 5 ||
				F.TechnologyBand < 0 || F.TechnologyBand > 10 ||
				F.Population < 0 || F.Population > 10000 || F.FoundedTick < 0L ||
				!BoundedText(F.OriginKeys, 16) || !BoundedText(F.CultureKeys, 16) ||
				!BoundedText(F.SpeciesKeys, 16) || !BoundedText(F.IdentityKeys, 16))
			{
				Failure = "foundation profile facts are invalid or unbounded"; return false;
			}
			return true;
		}

		private static KingdomPolityProfileRevision Build(string PolityId, string Digest,
			long Tick, int Technology, string Vocation, string Style, string Creed,
			IList<string> Bodies, bool External, bool UnresolvedLegacy = false)
		{
			string profileId = KingdomPolityRules.ActivationId("taf:polity-profile:v1:",
				"polity-profile-id-v1", PolityId, Digest);
			List<string> practices = new List<string>();
			if (UnresolvedLegacy) AddUnique(practices, "legacy-profile-unresolved");
			AddUnique(practices, "vocation-" + Token(Vocation));
			if (!string.IsNullOrEmpty(Creed)) AddUnique(practices, "creed-" + Token(Creed));
			practices.Sort(StringComparer.Ordinal);
			List<string> roles = External
				? new List<string> { "claimant", "courier", "envoy", "guard", "migrant",
					"namesake", "patrol", "successor", "trader", "warband" }
				: new List<string> { "cook", "courier", "guard", "migrant", "patrol",
					"successor", "trader" };
			List<string> gear = UnresolvedLegacy ? new List<string>() : GearKeys(Technology);
			List<KingdomPolityProfileFact> rootFacts = RootFacts(PolityId, Digest, Tick,
				Technology, Style, Creed);
			List<KingdomPolityExpressionCue> cues =
				KingdomPolityProfileExpressionCatalogue.Resolve(rootFacts, Technology);
			List<string> factIds = new List<string>();
			for (int i = 0; i < rootFacts.Count; i++) factIds.Add(rootFacts[i].FactId);
			factIds.Sort(StringComparer.Ordinal);
			KingdomPolityLoadoutPolicy loadout = new KingdomPolityLoadoutPolicy
			{
				Kind = KingdomPolityLoadoutPolicyKind.OwnedReplace,
				ExpectedValueBudget = UnresolvedLegacy ? 0 :
					Math.Min(KingdomPolityRules.MaxValueBudget, 50 + Technology * 125)
			};
			loadout.ExcludedKeys.AddRange(new[] { "natural-gear", "quest", "relic",
				"trader-stock", "unique" });
			loadout.ExcludedKeys.Sort(StringComparer.Ordinal);
			loadout.SelectedKeys.AddRange(gear);
			return new KingdomPolityProfileRevision
			{
				ProfileId = profileId, Revision = 1, PolityId = PolityId, EffectiveTick = Tick,
				RulesVersion = RulesVersion, DerivedFromFactIds = factIds,
				FactsDigest = Digest, TechnologyBand = Technology, PracticeTags = practices,
				BodyKeys = new List<string>(Bodies), RoleKeys = roles, GearKeys = gear, Loadout = loadout,
				ExpressionCues = cues
			};
		}

		private static List<KingdomPolityProfileFact> RootFacts(string PolityId, string Digest,
			long Tick, int Technology, string Style, string Creed)
		{
			List<KingdomPolityProfileFact> result = new List<KingdomPolityProfileFact>();
			AddRootFact(result, PolityId, Digest, KingdomPolityProfileFactKind.Style,
				"style=" + (Style ?? "common"));
			if (!string.IsNullOrEmpty(Creed)) AddRootFact(result, PolityId, Digest,
				KingdomPolityProfileFactKind.Creed, "declared=" + Creed);
			AddRootFact(result, PolityId, Digest, KingdomPolityProfileFactKind.Technology,
				"band=" + Technology.ToString(System.Globalization.CultureInfo.InvariantCulture));
			result.Sort((a, b) => string.CompareOrdinal(a.FactId, b.FactId)); return result;
		}

		private static void AddRootFact(List<KingdomPolityProfileFact> Target, string PolityId,
			string Digest, KingdomPolityProfileFactKind Kind, string Value)
		{
			Target.Add(new KingdomPolityProfileFact { Kind = Kind, ValueKey = Value,
				SourceRef = PolityId, FactId = KingdomPolityRules.ActivationId(
					"taf:fact:profile:v1:", "polity-root-expression-fact-v1", PolityId,
					Digest, ((byte)Kind).ToString(System.Globalization.CultureInfo.InvariantCulture), Value) });
		}

		private static string FoundationDigest(KingdomPolityFoundationFacts F)
		{
			List<string> values = new List<string> { F.RealmId, F.FactionId, F.DisplayName,
				F.FounderName ?? "", F.SettlementId, F.Vocation ?? "", F.Style ?? "",
				F.Creed ?? "", F.Stage.ToString(System.Globalization.CultureInfo.InvariantCulture),
				F.TechnologyBand.ToString(System.Globalization.CultureInfo.InvariantCulture),
				F.Population.ToString(System.Globalization.CultureInfo.InvariantCulture),
				F.FoundedTick.ToString(System.Globalization.CultureInfo.InvariantCulture) };
			AddLane(values, "origin", F.OriginKeys); AddLane(values, "culture", F.CultureKeys);
			AddLane(values, "species", F.SpeciesKeys); AddLane(values, "identity", F.IdentityKeys);
			return KingdomPolityRules.ActivationDigest("polity-profile-current-v2", values);
		}

		internal static List<string> CurrentBodyKeys(IList<string> Species,
			IList<string> Identity, bool UnresolvedIfEmpty)
		{
			List<string> result = new List<string>();
			for (int i = 0; Species != null && i < Species.Count; i++)
			{
				string value = (Species[i] ?? "").Trim().ToLowerInvariant();
				if (value == "human") AddUnique(result, "human");
				else if (value == "snapjaw") AddUnique(result, "snapjaw");
				else if (value == "goatfolk") AddUnique(result, "goatfolk");
				else if (value == "dromad") AddUnique(result, "dromad");
				else if (value == "hindren") AddUnique(result, "hindren");
				else if (value == "mechanical" || value == "robot" ||
					value.EndsWith(" robot", StringComparison.Ordinal))
					AddUnique(result, "mechanical");
			}
			for (int i = 0; Identity != null && i < Identity.Count; i++)
			{
				string value = (Identity[i] ?? "").Trim().ToLowerInvariant();
				if (value == "body:robot") AddUnique(result, "mechanical");
				else if (value == "genotype:true kin" || value == "genotype:mutated human")
					AddUnique(result, "human");
			}
			if (result.Count == 0 && UnresolvedIfEmpty) result.Add("unresolved");
			result.Sort(StringComparer.Ordinal); return result;
		}

		private static void AddLane(List<string> Target, string Lane, IList<string> Values)
		{
			for (int i = 0; Values != null && i < Values.Count; i++)
				Target.Add(Lane + "=" + Values[i]);
		}

		private static List<string> GearKeys(int Technology)
		{
			return KingdomPolityLoadoutCatalogue.KeysForTechnology(Technology);
		}

		private static List<string> Merge(IList<string> A, IList<string> B,
			IList<string> C, IList<string> D)
		{
			List<string> result = new List<string>(); AddAll(result, A); AddAll(result, B);
			AddAll(result, C); AddAll(result, D); result.Sort(StringComparer.Ordinal); return result;
		}

		private static void AddAll(List<string> Target, IList<string> Values)
		{
			for (int i = 0; Values != null && i < Values.Count; i++) AddUnique(Target, Values[i]);
		}

		private static void AddUnique(List<string> Target, string Value)
		{
			if (string.IsNullOrEmpty(Value)) return;
			for (int i = 0; i < Target.Count; i++) if (Target[i] == Value) return;
			Target.Add(Value);
		}

		private static bool BoundedText(IList<string> Values, int Max)
		{
			if (Values == null || Values.Count > Max) return false;
			for (int i = 0; i < Values.Count; i++)
				if (!KingdomPolityRules.Text(Values[i], true)) return false;
			return true;
		}

		private static bool PairedCounts(IList<string> Keys, IList<int> Counts)
		{
			if (!BoundedText(Keys, 16) || Counts == null || Counts.Count != Keys.Count) return false;
			for (int i = 0; i < Counts.Count; i++) if (Counts[i] < 1 || Counts[i] > 10000) return false;
			return true;
		}

		private static string Token(string Value)
		{
			if (string.IsNullOrEmpty(Value)) return "holding";
			char[] chars = Value.ToLowerInvariant().ToCharArray();
			for (int i = 0; i < chars.Length; i++)
				if (!((chars[i] >= 'a' && chars[i] <= 'z') ||
					(chars[i] >= '0' && chars[i] <= '9'))) chars[i] = '-';
			string token = new string(chars).Trim('-');
			return token.Length == 0 ? "holding" : (token.Length <= 64 ? token : token.Substring(0, 64));
		}
	}
}
