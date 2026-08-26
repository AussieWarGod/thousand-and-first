using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// Addendum 17's narrow identity factor. Evidence is vanilla's own TextFragments vocabulary,
	/// not a TAF-authored species table: Activity, VillageActivity, ValuedOre, SacredThing, and
	/// ArableLand travel with creature blueprints and therefore extend naturally with other mods.
	/// A matching practice may help by at most thirty percent; silence is exactly neutral.
	/// </summary>
	public static class KingdomIdentityAffinityRules
	{
		public const int MinimumPercent = 70;
		public const int NeutralPercent = 100;
		public const int MaximumPercent = 130;

		private sealed class Vocabulary
		{
			public readonly string Kind;
			public readonly string[] Words;

			public Vocabulary(string Kind, params string[] Words)
			{
				this.Kind = Kind;
				this.Words = Words;
			}
		}

		// Words are practices or valued materials vanilla itself uses in TextFragments. This is a
		// semantic map from that open prose to the catalogue's open work-kind strings, not a list of
		// cultures/species. Unknown identities and unknown kinds remain neutral.
		private static readonly Vocabulary[] Vocabularies = new Vocabulary[]
		{
			new Vocabulary("food", "farm", "field", "tend", "till", "grow", "plant",
				"harvest", "arable", "graze", "gather", "forag", "hunt", "food", "stew",
				"fruit", "grain", "meal", "bread", "vittle", "livestock"),
			new Vocabulary("storage", "collect", "trade", "merchant", "hoard", "store",
				"treasure", "cache", "shelter", "enclosure", "home"),
			new Vocabulary("craft", "craft", "build", "reinforc", "weav", "potter", "paint",
				"artifact", "object", "chrome", "metal", "ore", "machin", "cybernetic"),
			new Vocabulary("power", "machin", "diode", "electric", "charge", "artifact",
				"chrome", "bit flip", "recomposit"),
			new Vocabulary("defense", "guard", "raid", "hunt", "ramming", "bludgeon",
				"fortif", "reinforc", "wall", "weapon", "shield", "track"),
			new Vocabulary("knowledge", "read", "book", "philosoph", "meaning", "commun",
				"artifact", "object", "kasaph", "poetry", "speech", "thought", "inscrutable"),
			new Vocabulary("faith", "sacred", "pray", "worship", "ritual", "pilgrim",
				"sermon", "hymn", "moon", "kasaph"),
			new Vocabulary("housing", "home", "hearth", "bedroll", "chair", "sleep",
				"nest", "shelter", "safety", "enclosure", "shade"),
			new Vocabulary("civic", "home", "hearth", "speech", "song", "game", "feast",
				"bread", "wine", "safety", "gather"),
			new Vocabulary("memorial", "tomb", "death", "ancestor", "remember", "sacred",
				"philosoph", "poetry", "artistry")
		};

		/// <summary>One body's open identity and the exact vanilla prose used to derive its work
		/// affinity. Kept as the single identity field inside SettlerCapability.</summary>
		public readonly struct WorkerIdentity
		{
			public readonly string Culture;
			public readonly string Species;
			public readonly string Activity;
			public readonly string VillageActivity;
			public readonly string ValuedOre;
			public readonly string SacredThing;
			public readonly string ArableLand;

			public WorkerIdentity(string Culture, string Species, string Activity,
				string VillageActivity, string ValuedOre, string SacredThing, string ArableLand)
			{
				this.Culture = Bounded(Culture, 128);
				this.Species = Bounded(Species, 128);
				this.Activity = Bounded(Activity, 2048);
				this.VillageActivity = Bounded(VillageActivity, 2048);
				this.ValuedOre = Bounded(ValuedOre, 2048);
				this.SacredThing = Bounded(SacredThing, 2048);
				this.ArableLand = Bounded(ArableLand, 2048);
			}

			public int Affinity(string WorkKind)
			{
				return Percent(WorkKind, Activity, VillageActivity, ValuedOre, SacredThing,
					ArableLand);
			}
		}

		/// <summary>Derives a bounded percent. Repeated evidence in one fragment counts once per
		/// vocabulary word; different vanilla evidence lanes compose, then the lane cap applies.</summary>
		public static int Percent(string WorkKind, string Activity, string VillageActivity,
			string ValuedOre, string SacredThing, string ArableLand)
		{
			Vocabulary vocabulary = Find(WorkKind);
			if (vocabulary == null) return NeutralPercent;
			int score = Score(Activity, vocabulary.Words, 10)
				+ Score(VillageActivity, vocabulary.Words, 10)
				+ Score(ValuedOre, vocabulary.Words, 6)
				+ Score(SacredThing, vocabulary.Words, 4)
				+ Score(ArableLand, vocabulary.Words, 8);
			return Clamp(NeutralPercent + score);
		}

		public static int Clamp(int Percent)
		{
			if (Percent < MinimumPercent) return MinimumPercent;
			return Percent > MaximumPercent ? MaximumPercent : Percent;
		}

		/// <summary>Composes independent identity opinions as bounded deltas around neutral. This is
		/// the same bounded-delta shape as the public API. The API first sums all source deltas and
		/// clamps once; this method then composes that already-final extension result exactly once
		/// with built-in evidence, without executing extension code inside pure assignment.</summary>
		public static int Compose(int Current, int Offered)
		{
			long combined = (long)Clamp(Current) + Clamp(Offered) - NeutralPercent;
			return Clamp(combined < int.MinValue ? int.MinValue
				: combined > int.MaxValue ? int.MaxValue : (int)combined);
		}

		/// <summary>Applies the factor with saturation. Zero remains zero: identity never makes an
		/// idle work run.</summary>
		public static int Apply(int Value, int Percent)
		{
			if (Value <= 0) return 0;
			long result = (long)Value * Clamp(Percent) / 100L;
			return result > int.MaxValue ? int.MaxValue : (int)result;
		}

		private static Vocabulary Find(string WorkKind)
		{
			string kind = Fold(WorkKind);
			if (kind == null) return null;
			for (int i = 0; i < Vocabularies.Length; i++)
				if (Vocabularies[i].Kind == kind) return Vocabularies[i];
			return null;
		}

		private static int Score(string Evidence, string[] Words, int Weight)
		{
			string evidence = Fold(Evidence);
			if (evidence == null || Words == null || Weight <= 0) return 0;
			int hits = 0;
			for (int i = 0; i < Words.Length; i++)
				if (evidence.IndexOf(Words[i], StringComparison.Ordinal) >= 0) hits++;
			long score = (long)hits * Weight;
			return score > MaximumPercent - NeutralPercent
				? MaximumPercent - NeutralPercent : (int)score;
		}

		private static string Fold(string Value)
		{
			if (string.IsNullOrWhiteSpace(Value)) return null;
			return Value.Trim().ToLowerInvariant();
		}

		private static string Bounded(string Value, int Maximum)
		{
			string value = string.IsNullOrWhiteSpace(Value) ? null : Value.Trim();
			if (value == null) return null;
			return value.Length <= Maximum ? value : value.Substring(0, Maximum);
		}
	}
}
