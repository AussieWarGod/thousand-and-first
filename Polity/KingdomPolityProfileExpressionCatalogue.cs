using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// Pure bounded catalogue. Entries name only reviewed Qud blueprints/classes; callers may merge
	/// additive entries after collision validation, without mutable global registration order.
	/// </summary>
	public static class KingdomPolityProfileExpressionCatalogue
	{
		public const int CatalogueVersion = 2;
		public const int MaxCues = 16;
		public const int MaxWeight = 8;

		public static List<KingdomPolityExpressionCue> Resolve(
			IList<KingdomPolityProfileFact> Facts, int TechnologyBand)
		{
			List<KingdomPolityExpressionCue> result = new List<KingdomPolityExpressionCue>();
			for (int i = 0; Facts != null && i < Facts.Count && result.Count < MaxCues; i++)
			{
				KingdomPolityProfileFact fact = Facts[i];
				if (fact == null) continue;
				string value = (fact.ValueKey ?? "").ToLowerInvariant();
				switch (fact.Kind)
				{
				case KingdomPolityProfileFactKind.Decision:
					if (value.Contains("gate=1"))
					{
						Add(result, fact, KingdomPolityExpressionKind.Role, "guard", 6);
						Add(result, fact, KingdomPolityExpressionKind.Signature, "gate-watch", 4);
					}
					else
					{
						Add(result, fact, KingdomPolityExpressionKind.Role, "envoy", 4);
					}
					if (value.Contains("stores=1"))
						Add(result, fact, KingdomPolityExpressionKind.Signature,
							"thrift-mark", 3);
					break;
				case KingdomPolityProfileFactKind.Creed:
					if (value.Contains("mechan") || value.Contains("chrome"))
					{
						Add(result, fact, KingdomPolityExpressionKind.Signature,
							"mechanical-bearing", 2);
						Add(result, fact, KingdomPolityExpressionKind.Dialogue, "chrome-covenant", 5);
					}
					else
					{
						Add(result, fact, KingdomPolityExpressionKind.Dialogue, "covenant-witness", 5);
					}
					break;
				case KingdomPolityProfileFactKind.Style:
					if (value.Contains("verdant"))
					{
						Add(result, fact, KingdomPolityExpressionKind.Signature, "verdant-bearing", 5);
						Add(result, fact, KingdomPolityExpressionKind.Dialogue, "reed-and-canopy", 4);
					}
					else if (value.Contains("eater"))
					{
						Add(result, fact, KingdomPolityExpressionKind.Signature, "recovered-machine", 5);
						Add(result, fact, KingdomPolityExpressionKind.Dialogue, "ruin-keeping", 4);
					}
					else if (value.Contains("moonstair") || value.Contains("moon-stair") ||
						value.Contains("gyre"))
					{
						// "gyre" is the frozen pre-v1 style alias, not evidence of Gyre Wight
						// doctrine or recovered Eater machinery. Keep the cue environmental.
						Add(result, fact, KingdomPolityExpressionKind.Signature,
							"moon-stair-crystal", 5);
						Add(result, fact, KingdomPolityExpressionKind.Dialogue,
							"warm-static-ground", 4);
					}
					else
					{
						Add(result, fact, KingdomPolityExpressionKind.Signature, "local-ground", 3);
					}
					break;
				case KingdomPolityProfileFactKind.Alliance:
					Add(result, fact, KingdomPolityExpressionKind.Role, "envoy", 6);
					Add(result, fact, KingdomPolityExpressionKind.Dialogue, "pact-token", 5);
					break;
				case KingdomPolityProfileFactKind.Relationship:
					if (value.Contains("band=4") || value.Contains("band=5"))
					{
						Add(result, fact, KingdomPolityExpressionKind.Role, "patrol", 6);
						Add(result, fact, KingdomPolityExpressionKind.Gear, TierWeapon(TechnologyBand), 4);
						Add(result, fact, KingdomPolityExpressionKind.Dialogue, "border-grievance", 5);
					}
					else
					{
						Add(result, fact, KingdomPolityExpressionKind.Role, "courier", 3);
						Add(result, fact, KingdomPolityExpressionKind.Dialogue, "known-road", 3);
					}
					break;
				case KingdomPolityProfileFactKind.Technology:
					Add(result, fact, KingdomPolityExpressionKind.Gear, TierWeapon(TechnologyBand), 6);
					Add(result, fact, KingdomPolityExpressionKind.Signature,
						"technology-band-" + TechnologyBand, 5);
					break;
				case KingdomPolityProfileFactKind.Population:
					// Population changes the immutable body pool in profile rules. It cannot
					// grant every member one body's mutation or transform the whole cohort.
					break;
				case KingdomPolityProfileFactKind.Practice:
				case KingdomPolityProfileFactKind.Work:
					AddLearned(result, fact);
					break;
				case KingdomPolityProfileFactKind.Transformation:
					AddExact(result, fact, "body=mechanical",
						KingdomPolityExpressionKind.Body, "mechanical", 8);
					AddExact(result, fact, "mutation=PhotosyntheticSkin",
						KingdomPolityExpressionKind.Mutation, "PhotosyntheticSkin", 8);
					AddExact(result, fact, "cybernetic=mechanical-bearing",
						KingdomPolityExpressionKind.Cybernetic, "mechanical-bearing", 8);
					break;
				case KingdomPolityProfileFactKind.Cargo:
					AddExact(result, fact, "blueprint=Waterskin",
						KingdomPolityExpressionKind.Cargo, "Waterskin", 8);
					break;
				}
			}
			result.Sort(Compare); return result;
		}

		public static bool TryMerge(IList<KingdomPolityExpressionCue> Base,
			IList<KingdomPolityExpressionCue> Additions,
			out List<KingdomPolityExpressionCue> Merged, out string Failure)
		{
			Merged = new List<KingdomPolityExpressionCue>(); Failure = null;
			Copy(Merged, Base); Copy(Merged, Additions); Merged.Sort(Compare);
			if (Merged.Count > MaxCues) return Fail("expression catalogue exceeds cue bound", out Failure);
			for (int i = 0; i < Merged.Count; i++)
			{
				if (!ValidCue(Merged[i]) || !CausallyAdmitted(Merged[i]))
					return Fail("expression catalogue contains an illegal or unproved cue", out Failure);
				if (i > 0 && Compare(Merged[i - 1], Merged[i]) == 0)
					return Fail("expression catalogue contains a duplicate cue", out Failure);
			}
			return true;
		}

		public static bool ValidCue(KingdomPolityExpressionCue C)
		{
			return C != null && C.Kind > KingdomPolityExpressionKind.None &&
				C.Kind <= KingdomPolityExpressionKind.Dialogue && C.Weight >= 1 &&
				C.Weight <= MaxWeight && Legal(C.Kind, C.ExpressionKey) &&
				C.SourceKind > KingdomPolityProfileFactKind.None &&
				C.SourceKind <= KingdomPolityProfileFactKind.Cargo &&
				KingdomPolityRules.Text(C.SourceValueKey, true) &&
				KingdomPolityRules.SemanticId(C.SourceRef) &&
				KingdomPolityRules.TypedId(C.ReasonFactId, "taf:fact:profile:");
		}

		public static bool CausallyAdmitted(KingdomPolityExpressionCue C)
		{
			if (!ValidCue(C)) return false;
			switch (C.Kind)
			{
			case KingdomPolityExpressionKind.Body:
				return C.SourceKind == KingdomPolityProfileFactKind.Transformation;
			case KingdomPolityExpressionKind.Skill:
				return C.SourceKind == KingdomPolityProfileFactKind.Practice ||
					C.SourceKind == KingdomPolityProfileFactKind.Work ||
					C.SourceKind == KingdomPolityProfileFactKind.Transformation;
			case KingdomPolityExpressionKind.Mutation:
				return C.SourceKind == KingdomPolityProfileFactKind.Practice ||
					C.SourceKind == KingdomPolityProfileFactKind.Transformation;
			case KingdomPolityExpressionKind.Cybernetic:
				return C.SourceKind == KingdomPolityProfileFactKind.Transformation;
			case KingdomPolityExpressionKind.Cargo:
				return C.SourceKind == KingdomPolityProfileFactKind.Cargo;
			case KingdomPolityExpressionKind.Gear:
				return C.SourceKind == KingdomPolityProfileFactKind.Technology ||
					C.SourceKind == KingdomPolityProfileFactKind.Decision ||
					C.SourceKind == KingdomPolityProfileFactKind.Alliance ||
					C.SourceKind == KingdomPolityProfileFactKind.Relationship ||
					C.SourceKind == KingdomPolityProfileFactKind.Work;
			case KingdomPolityExpressionKind.Role:
				return C.SourceKind == KingdomPolityProfileFactKind.Decision ||
					C.SourceKind == KingdomPolityProfileFactKind.Alliance ||
					C.SourceKind == KingdomPolityProfileFactKind.Relationship ||
					C.SourceKind == KingdomPolityProfileFactKind.Work;
			case KingdomPolityExpressionKind.Signature:
			case KingdomPolityExpressionKind.Dialogue:
				return C.SourceKind != KingdomPolityProfileFactKind.Legacy;
			default: return false;
			}
		}

		private static bool Legal(KingdomPolityExpressionKind Kind, string Key)
		{
			if (!KingdomPolityRules.Text(Key, true)) return false;
			switch (Kind)
			{
			case KingdomPolityExpressionKind.Body: return Key == "mechanical";
			case KingdomPolityExpressionKind.Role:
				return Key == "guard" || Key == "envoy" || Key == "patrol" || Key == "courier";
			case KingdomPolityExpressionKind.Skill:
				return Key == "Tactics" || Key == "Customs" || Key == "Survival" || Key == "Tinkering";
			case KingdomPolityExpressionKind.Mutation: return Key == "PhotosyntheticSkin";
			case KingdomPolityExpressionKind.Cybernetic: return Key == "mechanical-bearing";
			case KingdomPolityExpressionKind.Gear:
				return Key == "Club" || Key == "Long Sword" || Key == "Long Sword2" ||
					Key == "Steel Long Sword" || Key == "Long Sword3";
			case KingdomPolityExpressionKind.Cargo: return Key == "Waterskin";
			default: return Key.Length <= 64;
			}
		}

		private static string TierWeapon(int Band)
		{
			return Band <= 0 ? "Club" : Band <= 2 ? "Long Sword" : Band <= 4 ?
				"Long Sword2" : Band <= 6 ? "Steel Long Sword" : "Long Sword3";
		}

		private static void Add(List<KingdomPolityExpressionCue> Target,
			KingdomPolityProfileFact Fact, KingdomPolityExpressionKind Kind, string Key, int Weight)
		{
			if (Target.Count >= MaxCues) return;
			KingdomPolityExpressionCue cue = new KingdomPolityExpressionCue { Kind = Kind,
				ExpressionKey = Key, Weight = Weight, SourceKind = Fact.Kind,
				SourceValueKey = Fact.ValueKey, SourceRef = Fact.SourceRef, ReasonFactId = Fact.FactId };
			if (!ValidCue(cue) || !CausallyAdmitted(cue)) return;
			for (int i = 0; i < Target.Count; i++)
				if (Compare(Target[i], cue) == 0) return;
			Target.Add(cue);
		}

		private static void AddExact(List<KingdomPolityExpressionCue> Target,
			KingdomPolityProfileFact Fact, string ExactValue,
			KingdomPolityExpressionKind Kind, string Key, int Weight)
		{
			if (string.Equals(Fact.ValueKey, ExactValue, StringComparison.Ordinal))
				Add(Target, Fact, Kind, Key, Weight);
		}

		private static void AddLearned(List<KingdomPolityExpressionCue> Target,
			KingdomPolityProfileFact Fact)
		{
			AddExact(Target, Fact, "skill=Tactics",
				KingdomPolityExpressionKind.Skill, "Tactics", 8);
			AddExact(Target, Fact, "skill=Customs",
				KingdomPolityExpressionKind.Skill, "Customs", 8);
			AddExact(Target, Fact, "skill=Survival",
				KingdomPolityExpressionKind.Skill, "Survival", 8);
			AddExact(Target, Fact, "skill=Tinkering",
				KingdomPolityExpressionKind.Skill, "Tinkering", 8);
			AddExact(Target, Fact, "mutation=PhotosyntheticSkin",
				KingdomPolityExpressionKind.Mutation, "PhotosyntheticSkin", 8);
		}

		private static void Copy(List<KingdomPolityExpressionCue> Target,
			IList<KingdomPolityExpressionCue> Source)
		{
			for (int i = 0; Source != null && i < Source.Count; i++) Target.Add(Source[i]);
		}

		internal static int Compare(KingdomPolityExpressionCue A, KingdomPolityExpressionCue B)
		{
			int c = ((byte)A.Kind).CompareTo((byte)B.Kind);
			if (c != 0) return c; c = string.CompareOrdinal(A.ExpressionKey, B.ExpressionKey);
			if (c != 0) return c; c = string.CompareOrdinal(A.ReasonFactId, B.ReasonFactId);
			if (c != 0) return c; return A.Weight.CompareTo(B.Weight);
		}

		private static bool Fail(string Reason, out string Failure)
		{
			Failure = Reason; return false;
		}
	}
}
