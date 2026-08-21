using System.Collections.Generic;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	/// <summary>
	/// The engine-free arithmetic and prose behind the five co-opted ceremonies
	/// (<c>KingdomCeremony</c> is the engine-coupled shell): the surveyor's plan staked ahead of
	/// a building, the raising ceremony that closes it, the tastes and traits a settling notable
	/// carries, and the pattern-book a chartered caravan occasionally opens. Every kernel draw
	/// here follows the split <c>KingdomVoiceRules.ChooseSpeaker</c> already established: the
	/// caller supplies a settlement id and an ordinal (both plain data, no engine reference
	/// required to produce them), and the draw itself &mdash; kernel included &mdash; is pure and
	/// unit-testable. A key that cannot be built or a kernel that refuses always falls back to a
	/// fixed, still-correct answer (index zero, "no"), never to an exception.
	/// </summary>
	public static class KingdomCeremonyRules
	{
		private const int CeremonyRulesVersion = 1;

		/// <summary>Fixed, all-zero seed, exactly as <c>KingdomChronicle</c> and
		/// <c>KingdomVoiceRules</c> use: domain separation comes entirely from the settlement id,
		/// stream, kind, and ordinal folded into each draw's key, so a shared all-zero seed can
		/// never alias two different ceremonies onto the same roll.</summary>
		private static readonly KernelSeed128 CeremonySeed = default(KernelSeed128);

		// ==================================================================================
		// The surveyor's plan
		// ==================================================================================

		/// <summary>
		/// Composes the plan a founder reads on a freshly staked marker, framed as intention
		/// rather than as the finished building's own description. One hand-written template per
		/// <see cref="KingdomRules.BuildEntry.Category"/> family, each carrying a tier slot (the
		/// design's own <see cref="GrowthStage"/>) and a material slot (a skin key, a future
		/// material name, or any other short flavour word a caller has on hand).
		/// <para>
		/// A category this file does not recognise &mdash; a third-party mod's own invention
		/// &mdash; falls back to a plain, honest line rather than a templated sentence wearing
		/// the wrong family's clothes. The fallback names no family, no tier, and no material: it
		/// is "plain stakes," not filler dressed up as a template.
		/// </para>
		/// </summary>
		/// <param name="Category">The design's <c>Category</c> attribute. Case-insensitive;
		/// null or unrecognised both fall back to plain stakes.</param>
		/// <param name="BuildingName">The design's display name. Null or empty reads as "the
		/// work".</param>
		/// <param name="Tier">The design's minimum growth stage, spoken as an adjective
		/// ("a steading's", "a city's").</param>
		/// <param name="MaterialFlavor">A short material or skin word, or null/empty to fall back
		/// to "plain stock" within the template &mdash; still a real sentence, just an
		/// unspecified material rather than missing text.</param>
		/// <returns>A capitalised, single-sentence description ending in a period, fit for a
		/// <c>Description.Short</c>. Never null or empty.</returns>
		public static string SurveyorsPlanText(string Category, string BuildingName, GrowthStage Tier, string MaterialFlavor)
		{
			string name = string.IsNullOrEmpty(BuildingName) ? "the work" : BuildingName;
			string tier = TierWord(Tier);
			string material = string.IsNullOrEmpty(MaterialFlavor) ? "plain stock" : MaterialFlavor;
			switch (Normalize(Category))
			{
			case "food":
				return "The plan for " + name + " is staked: " + tier + " table, raised in " + material + ", meant to keep the larder honest.";
			case "storage":
				return "The plan for " + name + " is staked: " + tier + " keeping-place, walled in " + material + ", meant to hold what the settlement cannot yet spend.";
			case "civic":
				return "The plan for " + name + " is staked: " + tier + " gathering-ground, built of " + material + ", meant for the business no one settler can do alone.";
			case "craft":
				return "The plan for " + name + " is staked: " + tier + " working-floor, framed in " + material + ", meant to turn hands into goods.";
			case "power":
				return "The plan for " + name + " is staked: " + tier + " engine-house, set in " + material + ", meant to carry a load no back should.";
			case "faith":
				return "The plan for " + name + " is staked: " + tier + " quiet room, raised in " + material + ", meant for whatever the settlement still believes.";
			case "memorial":
				return "The plan for " + name + " is staked: " + tier + " remembering-place, cut in " + material + ", meant to outlast whoever asked for it.";
			case "housing":
				return "The plan for " + name + " is staked: " + tier + " roof, walled in " + material + ", meant for a household that has not moved in yet.";
			case "defense":
				return "The plan for " + name + " is staked: " + tier + " standing wall, built of " + material + ", meant to cost a raider more than it cost the settlement.";
			case "knowledge":
				return "The plan for " + name + " is staked: " + tier + " keeping of what is known, written in " + material + ", meant to outlive the keeper who writes it.";
			default:
				return "The plan for " + name + " is staked: plain stakes in the ground, and nothing more written yet.";
			}
		}

		private static string TierWord(GrowthStage Tier)
		{
			switch (Tier)
			{
			case GrowthStage.Camp:
				return "a camp's";
			case GrowthStage.Steading:
				return "a steading's";
			case GrowthStage.Village:
				return "a village's";
			case GrowthStage.Town:
				return "a town's";
			case GrowthStage.City:
				return "a city's";
			default:
				return "the settlement's";
			}
		}

		private static string Normalize(string Text)
		{
			return string.IsNullOrEmpty(Text) ? null : Text.Trim().ToLowerInvariant();
		}

		// ==================================================================================
		// The raising ceremony
		// ==================================================================================

		/// <summary>
		/// A completion is attended when it lands within one day of its own due tick. A scaffold
		/// only ever advances while its zone is active, so every completion happens with the
		/// founder somewhere in the settlement; this distinguishes a founder who was already
		/// there watching the clock run out from one who has just walked back in after real time
		/// carried the due tick past while they were elsewhere &mdash; the same tick therefore
		/// reads as a late arrival, not a live one.
		/// </summary>
		/// <param name="CompleteTick">The scaffold's own due tick.</param>
		/// <param name="NowTicks">The tick completion is actually being resolved at.</param>
		/// <returns>True for a live completion; false for one caught up on return.</returns>
		public static bool IsAttended(long CompleteTick, long NowTicks)
		{
			return NowTicks - CompleteTick < KingdomRules.TicksPerDay;
		}

		/// <summary>The chronicle's line for a completion the founder watched happen: crew
		/// gathered, water shared, named if anyone was there to be named. Lower-case clause, no
		/// trailing period (the chronicle supplies both).</summary>
		/// <param name="DisplayName">The finished building's name.</param>
		/// <param name="SeatName">The settlement's seat name.</param>
		/// <param name="Present">Names of settlers found nearby, or null/empty for none found.</param>
		/// <param name="PlanQuote">The surveyor's plan text staked for this building, or null when
		/// it was never staked as a plan (a direct commission has none).</param>
		public static string RaisingAttendedChronicle(string DisplayName, string SeatName, IList<string> Present, string PlanQuote)
		{
			string who = NamePresent(Present);
			string quote = QuoteClause(PlanQuote);
			if (who == null)
			{
				return "the " + DisplayName + " was raised at " + SeatName + ", the water shared over it" + quote;
			}
			return "the " + DisplayName + " was raised at " + SeatName + " with " + who + " standing by and the water shared" + quote;
		}

		/// <summary>The chronicle's line for a completion nobody was there to see: plain, past
		/// tense, no crew named.</summary>
		public static string RaisingUnattendedChronicle(string DisplayName, string SeatName, string PlanQuote)
		{
			return "the " + DisplayName + " stood finished at " + SeatName + " before anyone came home to see it" + QuoteClause(PlanQuote);
		}

		/// <summary>The homecoming ledger's own line for an unattended completion &mdash; the
		/// "what happened while you were away" register the chronicle line above does not reach
		/// on its own.</summary>
		public static string RaisingLedgerNote(string DisplayName)
		{
			return "{{G|The " + DisplayName + " was finished while you were away.}}";
		}

		/// <summary>The live message for an attended completion.</summary>
		public static string RaisingAttendedMessage(string DisplayName, IList<string> Present)
		{
			string who = NamePresent(Present);
			if (who == null)
			{
				return "{{G|The " + DisplayName + " is complete. The water is shared.}}";
			}
			return "{{G|The " + DisplayName + " is complete. Present: " + who + ". The water is shared.}}";
		}

		private static string QuoteClause(string PlanQuote)
		{
			return string.IsNullOrEmpty(PlanQuote) ? "" : (", true to the plan staked there: \"" + PlanQuote + "\"");
		}

		private static string NamePresent(IList<string> Present)
		{
			if (Present == null || Present.Count == 0)
			{
				return null;
			}
			if (Present.Count == 1)
			{
				return Present[0];
			}
			if (Present.Count == 2)
			{
				return Present[0] + " and " + Present[1];
			}
			return Present[0] + ", " + Present[1] + ", and others";
		}

		/// <summary>Drams shared as part of an attended raising. Small and decorative: a partial
		/// or zero draw from the stores changes nothing about whether the ceremony happens.</summary>
		public const int RaisingShareDrams = 2;

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
			if (CounterRandom.TryDrawBelow(CeremonySeed, key, TasteCountDrawIndex, 2uL, out value, out fault))
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

		// ==================================================================================
		// Leader traits
		// ==================================================================================

		private const string LeaderEventStreamId = "taf:ceremony:leader:v1";
		private const uint LeaderEventKind = 1u;
		private const uint VirtueDrawIndex = 0u;
		private const uint FlawDrawIndex = 1u;

		private static readonly string[] Virtues = new string[8]
		{
			"keeps their word like water in a cask, sealed and spent only on purpose",
			"has never once let the ledger lie, even when the truth cost standing",
			"remembers every name on the roster without needing the roll read aloud",
			"works before dawn and says nothing about it",
			"trusts strangers exactly as far as the road has proven them, and no further, and no less",
			"would rather go without than watch the settlement go without",
			"has buried enough of their own to know which griefs are real",
			"says the hard thing to the founder's face, once, and then lets it go"
		};

		private static readonly string[] Flaws = new string[8]
		{
			"cannot let a debt go unmentioned, even a settled one",
			"trusts their own judgment past the point anyone asked for it",
			"keeps a grudge the way the settlement keeps water: carefully, and too long",
			"would rather be right in front of the founder than quietly correct",
			"spends more breath on how a thing should be done than on doing it",
			"cannot stand an empty larder, and has been known to hoard against one",
			"trusts the stranger with the better story over the one with the better sense",
			"has never forgiven the settlement for the year it nearly starved"
		};

		/// <summary>Equilibrium points a notable's virtue is worth.</summary>
		public const int VirtueShadeAmount = 2;

		/// <summary>Equilibrium points a notable's flaw costs. Smaller than the virtue on
		/// purpose: net texture, not a trap.</summary>
		public const int FlawShadeAmount = 1;

		/// <summary>
		/// Draws the one virtue and one flaw a newly named or newly passed office holder carries.
		/// Deterministic in <paramref name="SettlementId"/> and <paramref name="Ordinal"/>
		/// together, which is the whole of "no reroll": the same office transition always draws
		/// the same pair, on any reload, without anything needing to be stored for it.
		/// </summary>
		/// <param name="SettlementId">The settlement's kernel id.</param>
		/// <param name="Ordinal">The tick the transition happened at.</param>
		/// <param name="VirtueIndex">Index into <see cref="Virtues"/>.</param>
		/// <param name="FlawIndex">Index into <see cref="Flaws"/>.</param>
		public static void ChooseLeaderTraits(string SettlementId, ulong Ordinal, out int VirtueIndex, out int FlawIndex)
		{
			VirtueIndex = 0;
			FlawIndex = 0;
			SemanticEventKey key;
			KernelFaultCode fault;
			if (!SemanticEventKey.TryCreate(CeremonyRulesVersion, SettlementId, LeaderEventStreamId, LeaderEventKind, Ordinal, out key, out fault))
			{
				return;
			}
			ulong value;
			if (CounterRandom.TryDrawBelow(CeremonySeed, key, VirtueDrawIndex, (ulong)Virtues.Length, out value, out fault))
			{
				VirtueIndex = (int)value;
			}
			if (CounterRandom.TryDrawBelow(CeremonySeed, key, FlawDrawIndex, (ulong)Flaws.Length, out value, out fault))
			{
				FlawIndex = (int)value;
			}
		}

		public static string VirtueText(int Index)
		{
			return (Index >= 0 && Index < Virtues.Length) ? Virtues[Index] : Virtues[0];
		}

		public static string FlawText(int Index)
		{
			return (Index >= 0 && Index < Flaws.Length) ? Flaws[Index] : Flaws[0];
		}

		/// <summary>The chronicle's line naming an office holder's virtue and flaw together, so
		/// no notable is ever chronicled flawless. Lower-case clause, no trailing period.</summary>
		public static string LeaderTraitChronicle(string Title, string HolderName, string SeatName, int VirtueIndex, int FlawIndex)
		{
			string title = string.IsNullOrEmpty(Title) ? "the office" : Title;
			string holder = string.IsNullOrEmpty(HolderName) ? "the new holder" : HolderName;
			return holder + ", " + title + " of " + SeatName + ", " + VirtueText(VirtueIndex) + " -- but " + FlawText(FlawIndex);
		}

		/// <summary>Net equilibrium points one notable's virtue and flaw carry together.</summary>
		public static int LeaderShade()
		{
			return VirtueShadeAmount - FlawShadeAmount;
		}

		// ==================================================================================
		// The pattern-book
		// ==================================================================================

		private const string PatternEventStreamId = "taf:ceremony:pattern:v1";
		private const uint PatternEventKind = 1u;
		private const uint PatternChanceDrawIndex = 0u;
		private const uint PatternPickDrawIndexBase = 1u;

		/// <summary>
		/// The <c>Knowledge</c>-token kind (<see cref="KingdomZoningRules.KindOf"/>) that marks a
		/// design as pattern-book-only: reachable through no other roster kind, so teaching it
		/// from a disk or certifying a machine can never unlock it and the base catalogue is
		/// never gated on a draw. An author writes <c>Knowledge="pattern:some-name"</c> on an
		/// ordinary <c>&lt;building&gt;</c> entry to enter it into the pool.
		/// </summary>
		public const string PatternKnowledgeKind = "pattern";

		/// <summary>Chance out of 100 that a qualifying caravan arrival opens the pattern-book at
		/// all, when at least one undiscovered design exists to offer.</summary>
		public const int PatternBookChancePercent = 20;

		/// <summary>One design's <c>Key</c> and raw <c>Knowledge</c> attribute string, exactly as
		/// authored &mdash; the plain data <see cref="ForeignDesigns"/> needs, so the pure filter
		/// never has to reach into the live registry itself.</summary>
		public sealed class BuildingKnowledge
		{
			public string Key;
			public string Knowledge;
		}

		/// <summary>One design still reachable only through the pattern-book.</summary>
		public sealed class ForeignDesign
		{
			public string BuildingKey;
			public string LearnName;
		}

		/// <summary>
		/// Every design gated behind an unsatisfied <c>pattern:</c> token, deduplicated by the
		/// token's name half and sorted ordinally so the same roster and catalogue always yield
		/// the same offer order &mdash; required for the sequential kernel picks in
		/// <see cref="PickPatternIndex"/> to land on the same designs across a reload.
		/// </summary>
		public static List<ForeignDesign> ForeignDesigns(IEnumerable<BuildingKnowledge> Entries, IEnumerable<string> Roster)
		{
			List<ForeignDesign> found = new List<ForeignDesign>();
			if (Entries == null)
			{
				return found;
			}
			List<string> seen = new List<string>();
			foreach (BuildingKnowledge entry in Entries)
			{
				if (entry == null || string.IsNullOrEmpty(entry.Knowledge))
				{
					continue;
				}
				foreach (string token in KingdomZoningRules.Tokens(entry.Knowledge))
				{
					if (KingdomZoningRules.KindOf(token) != PatternKnowledgeKind)
					{
						continue;
					}
					if (KingdomZoningRules.Knows(Roster, token))
					{
						continue;
					}
					string name = KingdomZoningRules.NameOf(token);
					if (string.IsNullOrEmpty(name) || seen.Contains(name))
					{
						continue;
					}
					seen.Add(name);
					found.Add(new ForeignDesign { BuildingKey = entry.Key, LearnName = name });
				}
			}
			found.Sort((a, b) => string.CompareOrdinal(a.LearnName, b.LearnName));
			return found;
		}

		/// <summary>Whether this caravan arrival opens the pattern-book. Deterministic in
		/// <paramref name="SettlementId"/> and <paramref name="Ordinal"/>; fails closed (no
		/// offer) if the kernel refuses, which is never a loss &mdash; the base catalogue never
		/// depended on this draw.</summary>
		public static bool ShouldOfferPattern(string SettlementId, ulong Ordinal)
		{
			SemanticEventKey key;
			KernelFaultCode fault;
			if (!SemanticEventKey.TryCreate(CeremonyRulesVersion, SettlementId, PatternEventStreamId, PatternEventKind, Ordinal, out key, out fault))
			{
				return false;
			}
			ulong value;
			if (!CounterRandom.TryDrawBelow(CeremonySeed, key, PatternChanceDrawIndex, 100uL, out value, out fault))
			{
				return false;
			}
			return value < (ulong)PatternBookChancePercent;
		}

		/// <summary>
		/// Picks one index out of a shrinking remainder, for the caller's own sequential
		/// remove-and-redraw loop (draw, take that candidate out of the working list, draw again
		/// with the new, smaller count). Deterministic per <paramref name="Step"/>, so up to three
		/// sequential picks against the same settlement and ordinal never collide with each
		/// other's draw index.
		/// </summary>
		/// <param name="SettlementId">The settlement's kernel id.</param>
		/// <param name="Ordinal">The tick the caravan arrived at.</param>
		/// <param name="Step">0 for the first pick, 1 for the second, 2 for the third.</param>
		/// <param name="RemainingCount">Candidates left to choose among before this pick.</param>
		/// <returns>An index in <c>[0, RemainingCount)</c>, or 0 when nothing is left or the
		/// kernel refuses (the caller always has at least one candidate when calling this, so
		/// index zero is still a real, valid pick, not a sentinel).</returns>
		public static int PickPatternIndex(string SettlementId, ulong Ordinal, int Step, int RemainingCount)
		{
			if (RemainingCount <= 0)
			{
				return 0;
			}
			SemanticEventKey key;
			KernelFaultCode fault;
			if (!SemanticEventKey.TryCreate(CeremonyRulesVersion, SettlementId, PatternEventStreamId, PatternEventKind, Ordinal, out key, out fault))
			{
				return 0;
			}
			ulong value;
			if (!CounterRandom.TryDrawBelow(CeremonySeed, key, PatternPickDrawIndexBase + (uint)Step, (ulong)RemainingCount, out value, out fault))
			{
				return 0;
			}
			return (int)value;
		}
	}
}
