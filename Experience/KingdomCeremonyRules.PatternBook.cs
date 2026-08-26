using System.Collections.Generic;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomCeremonyRules
	{
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
			public string Label;
		}

		/// <summary>One design still reachable only through the pattern-book.</summary>
		public sealed class ForeignDesign
		{
			public string BuildingKey;
			public string LearnName;
			public string Label;
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
					found.Add(new ForeignDesign
					{
						BuildingKey = entry.Key,
						LearnName = name,
						Label = entry.Label
					});
				}
			}
			found.Sort((a, b) => string.CompareOrdinal(a.LearnName, b.LearnName));
			return found;
		}

		/// <summary>Whether this CharterDelivery opens the pattern-book. Deterministic in
		/// <paramref name="SettlementId"/> and its operation-sequence <paramref name="Ordinal"/>; fails closed (no
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
