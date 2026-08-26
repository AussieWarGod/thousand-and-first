using System.Collections.Generic;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomCeremonyRules
	{
		// ==================================================================================
		// Notable tastes
		// ==================================================================================

		private const string TasteEventStreamId = "taf:ceremony:taste:v1";
		private const uint TasteEventKind = 1u;
		private const uint TasteCountDrawIndex = 0u;
		private const uint TasteFirstDrawIndex = 1u;
		private const uint TasteSecondDrawIndex = 2u;

		/// <summary>The ten families a notable's taste can fall into, the same vocabulary
		/// <see cref="SurveyorsPlanText"/> templates against (<c>BuildEntry.Category</c>'s own
		/// ten names), so one settlement-scanning check answers both "does this notable's taste
		/// exist here" and "what would satisfy it."</summary>
		public static readonly string[] TasteCategories = new string[10]
		{
			"food", "storage", "civic", "craft", "power", "faith", "memorial", "housing", "defense", "knowledge"
		};

		private static readonly string[] TasteStatements = new string[10]
		{
			"wants to see a table that is never bare",
			"wants to see the stores kept ahead of need",
			"wants a place built for more than one person's business",
			"wants hands busy making something worth keeping",
			"wants a settlement that can carry its own weight",
			"wants a quiet room, away from the noise of the day",
			"wants the dead kept in a roll, not forgotten",
			"judges a place by its roofs before its walls",
			"wants peace backed by something that would cost a raider dear",
			"wants what is known written down before it is lost"
		};

		/// <summary>Equilibrium points a single met taste is worth. Small on purpose: texture,
		/// not optimization.</summary>
		public const int TasteShadeAmount = 1;

		/// <summary>The most tastes one notable ever states, and so the width of the draw in
		/// <see cref="ChooseTastes"/>. Named because <see cref="MaxNotableShade"/> has to know it
		/// too, and a ceiling that disagreed with the draw would be a ceiling nothing reached.
		/// </summary>
		public const int MaxTastesStated = 2;

		// --- Re-based onto the quality-of-life vocabulary (brief, Addendum 4) ------------------
		//
		// A notable's taste WAS a private system: a category string compared against
		// BuildEntry.Category. Addendum 4 replaces the three private systems with one open
		// vocabulary, so a taste is now a Prefers tag in that vocabulary's own namespace and a
		// building of that category offers the same tag. The comparison is
		// KingdomQolRules.Has -- the shared match engine -- rather than a string equality of this
		// file's own. Prose and shading are untouched: TasteLine, TasteChronicle and TasteShade
		// read exactly as they did, and a taste met is still worth TasteShadeAmount and no more.

		/// <summary>
		/// The tag a taste index states, in the shared vocabulary's own namespace: taste
		/// <c>food</c> is <c>taf:food</c>. Out of range falls back to index zero, matching every
		/// other taste accessor's fallback.
		/// </summary>
		public static string TasteTag(int TasteIndex)
		{
			string category = (TasteIndex >= 0 && TasteIndex < TasteCategories.Length) ? TasteCategories[TasteIndex] : TasteCategories[0];
			return KingdomQolRules.Fold(KingdomQolRules.Namespace + category);
		}

		/// <summary>
		/// The same tag from the building's side: what a design of this <c>Category</c> offers a
		/// notable who states a taste for it. Null for a design with no category at all, which
		/// offers nothing and is never a match.
		/// </summary>
		public static string CategoryTag(string Category)
		{
			string category = KingdomQolRules.Fold(Category);
			return (category.Length == 0) ? null : (KingdomQolRules.Namespace + category);
		}

		/// <summary>
		/// Which of a notable's stated tastes the settlement already meets, as the met flags
		/// <see cref="TasteShade"/> and <see cref="TasteChronicle"/> already take. One membership
		/// test in the shared vocabulary per taste, so a taste and a building meet by exactly the
		/// rule a Need and a <c>Provides</c> meet by.
		/// </summary>
		/// <param name="TasteIndices">From <see cref="ChooseTastes"/>. Null is no tastes.</param>
		/// <param name="Offer">The category tags of everything standing, from
		/// <see cref="CategoryTag"/>. Null or empty means nothing is met, which is correct for a
		/// settlement with nothing built.</param>
		/// <returns>Never null; one flag per stated taste, in the order stated.</returns>
		public static List<bool> TastesMet(IList<int> TasteIndices, string[] Offer)
		{
			List<bool> met = new List<bool>();
			if (TasteIndices == null)
			{
				return met;
			}
			for (int i = 0; i < TasteIndices.Count; i++)
			{
				met.Add(KingdomQolRules.Has(Offer, TasteTag(TasteIndices[i])));
			}
			return met;
		}

		/// <summary>
		/// Draws which one or two of the ten families a settling notable states a taste for.
		/// Deterministic in <paramref name="SettlementId"/> and <paramref name="Ordinal"/>
		/// together: the same settling event always states the same tastes, on any reload.
		/// </summary>
		/// <param name="SettlementId">The settlement's kernel id
		/// (<c>KingdomChronicle.SettlementId</c>).</param>
		/// <param name="Ordinal">The tick the notable settled at.</param>
		/// <returns>One or two distinct indices into <see cref="TasteCategories"/>. Never empty;
		/// falls back to a single index zero if the kernel refuses.</returns>
		public static List<int> ChooseTastes(string SettlementId, ulong Ordinal)
		{
			List<int> chosen = new List<int>();
			SemanticEventKey key;
			KernelFaultCode fault;
			if (!SemanticEventKey.TryCreate(CeremonyRulesVersion, SettlementId, TasteEventStreamId, TasteEventKind, Ordinal, out key, out fault))
			{
				chosen.Add(0);
				return chosen;
			}
			ulong value;
			int count = 1;
			if (CounterRandom.TryDrawBelow(CeremonySeed, key, TasteCountDrawIndex, (ulong)MaxTastesStated, out value, out fault))
			{
				count = (int)value + 1;
			}
			int first = 0;
			if (CounterRandom.TryDrawBelow(CeremonySeed, key, TasteFirstDrawIndex, (ulong)TasteCategories.Length, out value, out fault))
			{
				first = (int)value;
			}
			chosen.Add(first);
			if (count < 2 || TasteCategories.Length < 2)
			{
				return chosen;
			}
			int second = first;
			if (CounterRandom.TryDrawBelow(CeremonySeed, key, TasteSecondDrawIndex, (ulong)(TasteCategories.Length - 1), out value, out fault))
			{
				second = (int)value;
				if (second >= first)
				{
					second++;
				}
			}
			if (second != first)
			{
				chosen.Add(second);
			}
			return chosen;
		}

		/// <summary>One taste, stated in prose, with its met/default clause folded in. Never a
		/// complaint: an unmet taste simply says the notable has not found it yet.</summary>
		public static string TasteLine(int TasteIndex, bool Met)
		{
			string statement = (TasteIndex >= 0 && TasteIndex < TasteStatements.Length) ? TasteStatements[TasteIndex] : TasteStatements[0];
			return statement + (Met ? ", and finds it here already" : ", and has not found it here yet");
		}

		/// <summary>The chronicle's line for a settling notable's stated tastes. Lower-case
		/// clause, no trailing period.</summary>
		public static string TasteChronicle(string HolderName, IList<int> TasteIndices, IList<bool> Met)
		{
			string who = string.IsNullOrEmpty(HolderName) ? "the newcomer" : HolderName;
			if (TasteIndices == null || TasteIndices.Count == 0)
			{
				return who + " settles in and says nothing of what they want from the place";
			}
			if (TasteIndices.Count == 1)
			{
				return who + " states a taste on settling in: " + TasteLine(TasteIndices[0], Met != null && Met.Count > 0 && Met[0]);
			}
			bool met0 = Met != null && Met.Count > 0 && Met[0];
			bool met1 = Met != null && Met.Count > 1 && Met[1];
			return who + " states two tastes on settling in: " + TasteLine(TasteIndices[0], met0) + "; and " + TasteLine(TasteIndices[1], met1);
		}

		/// <summary>Equilibrium points every met taste in the set is worth together.</summary>
		public static int TasteShade(IList<bool> Met)
		{
			if (Met == null)
			{
				return 0;
			}
			int shade = 0;
			for (int i = 0; i < Met.Count; i++)
			{
				if (Met[i])
				{
					shade += TasteShadeAmount;
				}
			}
			return shade;
		}

	}
}
