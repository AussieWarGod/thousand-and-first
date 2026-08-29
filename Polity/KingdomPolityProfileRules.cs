using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Builds immutable, deterministic profile revisions from bounded semantic facts.</summary>
	public static partial class KingdomPolityProfileRules
	{
		public const int RulesVersion = 1;

		public static bool TryCreateCurrent(KingdomPolityFoundationFacts Facts,
			out KingdomPolityProfileRevision Profile, out string Failure)
		{
			Profile = null; Failure = null;
			if (!ValidFoundation(Facts, out Failure)) return false;
			List<string> identity = Merge(Facts.OriginKeys, Facts.CultureKeys,
				Facts.SpeciesKeys, Facts.IdentityKeys);
			string digest = FoundationDigest(Facts, identity);
			Profile = Build(Facts.RealmId, digest, Facts.FoundedTick,
				Math.Min(10, Facts.Stage * 2), Facts.Vocation, Facts.Style, Facts.Creed,
				identity, false);
			return true;
		}

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
			List<string> identity = WeightedKeys(Facts.OriginKeys, Facts.OriginCounts);
			List<string> creeds = WeightedKeys(Facts.CreedKeys, Facts.CreedCounts);
			string creed = creeds.Count == 0 ? "" : creeds[0];
			List<string> digestFacts = Merge(identity, creeds, Facts.RollNames, null);
			digestFacts.Insert(0, Facts.LegacyToken); digestFacts.Insert(1, Facts.LineageToken);
			digestFacts.Add(Facts.RealmName ?? ""); digestFacts.Add(Facts.SettlementName ?? "");
			digestFacts.Add(Facts.Vocation ?? ""); digestFacts.Add(Facts.Style ?? "");
			digestFacts.Add(Facts.Stage.ToString(System.Globalization.CultureInfo.InvariantCulture));
			digestFacts.Add(Facts.Population.ToString(System.Globalization.CultureInfo.InvariantCulture));
			digestFacts.Add(Facts.Defence.ToString(System.Globalization.CultureInfo.InvariantCulture));
			digestFacts.Add(Facts.StoredWater.ToString(System.Globalization.CultureInfo.InvariantCulture));
			digestFacts.Add(Facts.InheritedState.ToString(System.Globalization.CultureInfo.InvariantCulture));
			string digest = KingdomPolityRules.ActivationDigest("polity-profile-legacy-v1", digestFacts);
			int technology = Math.Min(10, Math.Max(0, Facts.Stage * 2 +
				(Facts.Defence >= 8 ? 1 : 0)));
			Profile = Build(PolityId, digest, EffectiveTick, technology,
				Facts.Vocation, Facts.Style, creed, identity, true);
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
				F.Population < 0 || F.Population > 10000 || F.FoundedTick < 0L ||
				!BoundedText(F.OriginKeys, 16) || !BoundedText(F.CultureKeys, 16) ||
				!BoundedText(F.SpeciesKeys, 16) || !BoundedText(F.IdentityKeys, 16))
			{
				Failure = "foundation profile facts are invalid or unbounded"; return false;
			}
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
				!PairedCounts(F.CreedKeys, F.CreedCounts))
			{
				Failure = "committed legacy facts are invalid or unbounded"; return false;
			}
			return true;
		}

		private static KingdomPolityProfileRevision Build(string PolityId, string Digest,
			long Tick, int Technology, string Vocation, string Style, string Creed,
			IList<string> Identity, bool External)
		{
			string profileId = KingdomPolityRules.ActivationId("taf:polity-profile:v1:",
				"polity-profile-id-v1", PolityId, Digest);
			List<string> practices = new List<string>();
			AddUnique(practices, "style-" + Token(Style));
			AddUnique(practices, "vocation-" + Token(Vocation));
			if (!string.IsNullOrEmpty(Creed)) AddUnique(practices, "creed-" + Token(Creed));
			practices.Sort(StringComparer.Ordinal);
			List<string> roles = External
				? new List<string> { "claimant", "courier", "envoy", "guard", "migrant",
					"namesake", "patrol", "successor", "trader", "warband" }
				: new List<string> { "cook", "courier", "guard", "migrant", "patrol",
					"successor", "trader" };
			List<string> gear = GearKeys(Technology);
			KingdomPolityLoadoutPolicy loadout = new KingdomPolityLoadoutPolicy
			{
				Kind = KingdomPolityLoadoutPolicyKind.OwnedReplace,
				ExpectedValueBudget = Math.Min(KingdomPolityRules.MaxValueBudget,
					50 + Technology * 125)
			};
			loadout.ExcludedKeys.AddRange(new[] { "natural-gear", "quest", "relic",
				"trader-stock", "unique" });
			loadout.ExcludedKeys.Sort(StringComparer.Ordinal);
			loadout.SelectedKeys.AddRange(gear);
			return new KingdomPolityProfileRevision
			{
				ProfileId = profileId, Revision = 1, PolityId = PolityId, EffectiveTick = Tick,
				RulesVersion = RulesVersion,
				DerivedFromFactIds = new List<string> { KingdomPolityRules.ActivationId(
					"taf:fact:polity:v1:", "polity-profile-fact-v1", PolityId, Digest) },
				FactsDigest = Digest, TechnologyBand = Technology, PracticeTags = practices,
				BodyKeys = BodyKeys(Identity), RoleKeys = roles, GearKeys = gear, Loadout = loadout
			};
		}

		private static string FoundationDigest(KingdomPolityFoundationFacts F, IList<string> Identity)
		{
			List<string> values = new List<string> { F.RealmId, F.FactionId, F.DisplayName,
				F.FounderName ?? "", F.SettlementId, F.Vocation ?? "", F.Style ?? "",
				F.Creed ?? "", F.Stage.ToString(System.Globalization.CultureInfo.InvariantCulture),
				F.Population.ToString(System.Globalization.CultureInfo.InvariantCulture),
				F.FoundedTick.ToString(System.Globalization.CultureInfo.InvariantCulture) };
			values.AddRange(Identity); return KingdomPolityRules.ActivationDigest(
				"polity-profile-current-v1", values);
		}

		private static List<string> BodyKeys(IList<string> Values)
		{
			List<string> result = new List<string>();
			for (int i = 0; Values != null && i < Values.Count; i++)
			{
				string v = (Values[i] ?? "").ToLowerInvariant();
				if (v.Contains("snapjaw")) AddUnique(result, "snapjaw");
				else if (v.Contains("goat")) AddUnique(result, "goatfolk");
				else if (v.Contains("dromad")) AddUnique(result, "dromad");
				else if (v.Contains("robot") || v.Contains("mechan")) AddUnique(result, "mechanical");
				else if (v.Contains("hindren")) AddUnique(result, "hindren");
				else if (v.Contains("human") || v.Contains("true kin")) AddUnique(result, "human");
			}
			if (result.Count == 0) result.Add("human"); result.Sort(StringComparer.Ordinal); return result;
		}

		private static List<string> GearKeys(int Technology)
		{
			if (Technology <= 0) return new List<string> { "club", "leather-armor", "wooden-buckler" };
			if (Technology <= 2) return new List<string> { "bronze-sword", "leather-armor", "wooden-buckler" };
			if (Technology <= 4) return new List<string> { "chain-mail", "iron-sword", "wooden-buckler" };
			if (Technology <= 6) return new List<string> { "chain-mail", "steel-sword", "wooden-buckler" };
			return new List<string> { "carbide-plate", "carbide-sword", "wooden-buckler" };
		}

		private static List<string> Merge(IList<string> A, IList<string> B,
			IList<string> C, IList<string> D)
		{
			List<string> result = new List<string>(); AddAll(result, A); AddAll(result, B);
			AddAll(result, C); AddAll(result, D); result.Sort(StringComparer.Ordinal); return result;
		}

		private static List<string> WeightedKeys(IList<string> Keys, IList<int> Counts)
		{
			List<string> result = new List<string>();
			for (int i = 0; Keys != null && Counts != null && i < Keys.Count; i++)
				AddUnique(result, Keys[i] + "=" + Counts[i].ToString(
					System.Globalization.CultureInfo.InvariantCulture));
			result.Sort(StringComparer.Ordinal); return result;
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
