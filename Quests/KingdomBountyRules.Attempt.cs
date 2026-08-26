using System.Collections.Generic;
using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomBountyRules
	{
		/// <summary>Everything one pass drew about one notice, so the shell can chronicle it with
		/// names without re-deriving any of it.</summary>
		public struct BountyAttempt
		{
			/// <summary>False only when the kernel refused before an outcome existed. Callers must
			/// leave the scheduled cursor on this event and retry it; no truth was burned.</summary>
			public bool Determined;

			/// <summary>What the pass came to.</summary>
			public BountyOutcome Outcome;

			/// <summary>Who read the notice, or null when nobody did.</summary>
			public string Name;

			/// <summary>Index into the roster the reader was drawn from, or -1.</summary>
			public int RosterIndex;

			/// <summary>Ceremony virtue index for the reader, for the prose. Meaningless when
			/// <see cref="Name"/> is null.</summary>
			public int VirtueIndex;

			/// <summary>Ceremony flaw index for the reader, for the prose.</summary>
			public int FlawIndex;

			/// <summary>True when the task's family was one of the reader's stated tastes.</summary>
			public bool TasteMatched;
		}

		/// <summary>
		/// Resolves one attended pass against one standing notice: whether anybody read it, who,
		/// and whether they took it.
		/// <para>
		/// Pure and total. Every draw is keyed on the settlement, the notice's posted tick, and a
		/// draw index derived from <paramref name="PassIndex"/>, so replaying the same pass always
		/// produces the same reader and the same answer. A kernel that refuses &mdash; an unnamed
		/// settlement, a machine whose crypto provider is failing &mdash; yields
		/// <see cref="BountyOutcome.NobodyTried"/>, which costs the founder nothing and loses
		/// nothing: the notice simply stands another day.
		/// </para>
		/// </summary>
		/// <param name="SettlementId">The settlement's kernel id
		/// (<c>KingdomChronicle.SettlementId</c>).</param>
		/// <param name="PostedTick">The tick the notice was staked at, its ordinal forever.</param>
		/// <param name="PassIndex">How many passes this notice has already been resolved for.
		/// Negative reads as zero; anything past <see cref="MaxPasses"/> is clamped.</param>
		/// <param name="Roster">Living settlers, longest-served first. Null or empty yields
		/// <see cref="BountyOutcome.NobodyTried"/>.</param>
		/// <param name="Task">The task posted.</param>
		/// <param name="Price">Drams promised.</param>
		public static BountyAttempt Resolve(string SettlementId, long PostedTick, int PassIndex, IList<string> Roster, BountyTask Task, int Price)
		{
			int pass = (PassIndex > 0) ? PassIndex : 0;
			if (pass > MaxPasses)
			{
				pass = MaxPasses;
			}
			SemanticEventKey key;
			KernelFaultCode fault;
			if (!SemanticEventKey.TryCreate(BountyRulesVersion, SettlementId, NoticeEventStreamId, NoticeEventKind, (ulong)((PostedTick > 0L) ? PostedTick : 0L), out key, out fault))
			{
				return EmptyAttempt();
			}
			return ResolveKey(SettlementId, key, (uint)pass * DrawsPerPass, Roster, Task, Price);
		}

		/// <summary>
		/// Resolves one absolute scheduled opportunity. Notice identity owns the event stream and
		/// the scheduled world tick owns the ordinal, so entering a zone cannot mint a new draw.
		/// </summary>
		public static BountyAttempt ResolveScheduled(string SettlementId, string EventStreamId,
			long ScheduledTick, IList<string> Roster, BountyTask Task, int Price)
		{
			if (ScheduledTick < 0L)
			{
				return EmptyAttempt();
			}
			SemanticEventKey key;
			KernelFaultCode fault;
			if (!SemanticEventKey.TryCreate(ScheduledBountyRulesVersion, SettlementId,
				EventStreamId, NoticeEventKind, (ulong)ScheduledTick, out key, out fault))
			{
				return EmptyAttempt();
			}
			return ResolveKey(SettlementId, key, 0u, Roster, Task, Price);
		}

		private static BountyAttempt EmptyAttempt()
		{
			BountyAttempt attempt = default(BountyAttempt);
			attempt.Outcome = BountyOutcome.NobodyTried;
			attempt.RosterIndex = -1;
			return attempt;
		}

		private static BountyAttempt ResolveKey(string SettlementId, SemanticEventKey Key,
			uint DrawBase, IList<string> Roster, BountyTask Task, int Price)
		{
			BountyAttempt attempt = EmptyAttempt();
			if (Roster == null || Roster.Count == 0)
			{
				attempt.Determined = true;
				return attempt;
			}
			KernelFaultCode fault;
			ulong value;
			if (!CounterRandom.TryDrawBelow(BountySeed, Key, DrawBase, 100uL, out value, out fault))
			{
				return attempt;
			}
			if (value >= (ulong)ReadChancePercent(Price))
			{
				attempt.Determined = true;
				return attempt;
			}
			if (!CounterRandom.TryDrawBelow(BountySeed, Key, DrawBase + 1u, (ulong)Roster.Count, out value, out fault))
			{
				return attempt;
			}
			int index = (int)value;
			string name = Roster[index];
			attempt.Name = name;
			attempt.RosterIndex = index;
			ulong person = PersonOrdinal(name);
			KingdomCeremonyRules.ChooseLeaderTraits(SettlementId, person, out attempt.VirtueIndex, out attempt.FlawIndex);
			int wantedTaste = TasteIndexFor(Task);
			if (wantedTaste >= 0)
			{
				List<int> tastes = KingdomCeremonyRules.ChooseTastes(SettlementId, person);
				for (int i = 0; i < tastes.Count; i++)
				{
					if (tastes[i] == wantedTaste)
					{
						attempt.TasteMatched = true;
						break;
					}
				}
			}
			attempt.Outcome = BountyOutcome.Refused;
			if (!CounterRandom.TryDrawBelow(BountySeed, Key, DrawBase + 2u, 100uL, out value, out fault))
			{
				return attempt;
			}
			int take = TakeChancePercent(Task, Price, index == 0, attempt.TasteMatched, TraitAppetite(attempt.VirtueIndex, attempt.FlawIndex));
			if (value < (ulong)take)
			{
				attempt.Outcome = BountyOutcome.Taken;
			}
			attempt.Determined = true;
			return attempt;
		}

	}
}
