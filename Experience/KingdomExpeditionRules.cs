using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// Pure commission law. A destination must be a real zone in the same world; the engine edge
	/// separately proves that its journal note was personally visited. Travel is bounded, quoted
	/// once, and outcome draws are counter-addressed by the durable realm job id.
	/// </summary>
	internal static class KingdomExpeditionRules
	{
		internal const int MinDurationDays = 3;
		internal const int MaxDurationDays = 30;
		internal const int WaterPerDay = 3;
		internal const int ProvisionsPerDay = 1;
		internal const int MaxSkillBonus = 20;

		internal const string StreamId = "taf:stream:salvage-expedition";
		internal const uint KindCode = 1u;
		internal const uint OutcomeDrawIndex = 0u;
		internal const uint YieldDrawIndex = 1u;

		/// <summary>Quotes an out-search-back journey. Surface distance costs one day per three
		/// cells (rounded up), each stratum costs another day, and searching costs one day. The
		/// whole journey is clamped to the explicit three-to-thirty-day gameplay bound.</summary>
		internal static bool TryQuote(string sourceZoneId, string targetZoneId, long startTick,
			out KingdomExpeditionQuote quote)
		{
			quote = default(KingdomExpeditionQuote);
			if (startTick < 0L || string.IsNullOrEmpty(sourceZoneId)
				|| string.IsNullOrEmpty(targetZoneId)
				|| string.Equals(sourceZoneId, targetZoneId, StringComparison.Ordinal)) return false;
			string sourceWorld;
			int sx, sy, sz;
			string targetWorld;
			int tx, ty, tz;
			if (!KingdomRules.TryParseZoneID(sourceZoneId, out sourceWorld, out sx, out sy, out sz)
				|| !KingdomRules.TryParseZoneID(targetZoneId, out targetWorld, out tx, out ty, out tz)
				|| !string.Equals(sourceWorld, targetWorld, StringComparison.Ordinal)) return false;
			long dx = Math.Abs((long)tx - sx);
			long dy = Math.Abs((long)ty - sy);
			long dz = Math.Abs((long)tz - sz);
			long surface = (dx > dy) ? dx : dy;
			long distance = surface + dz;
			if (distance <= 0L || distance > int.MaxValue) return false;
			long oneWayDays = 1L + ((surface + 2L) / 3L) + dz;
			long days = oneWayDays * 2L + 1L;
			if (days < MinDurationDays) days = MinDurationDays;
			if (days > MaxDurationDays) days = MaxDurationDays;
			long durationTicks = days * KingdomRules.TicksPerDay;
			if (durationTicks < 0L || startTick > long.MaxValue - durationTicks) return false;
			quote = new KingdomExpeditionQuote((int)distance, (int)days,
				startTick + durationTicks, (int)days * WaterPerDay,
				(int)days * ProvisionsPerDay);
			return true;
		}

		/// <summary>Freezes one outcome and scrap yield. Skill can improve the band but can never
		/// add a draw or exceed the named twenty-point cap. A picked-clean site returns the resident
		/// safely with no cargo; bodily death is never a random result here.</summary>
		internal static bool TryDrawOutcome(KernelSeed128 seed, string settlementId, int jobId,
			int skillBonus, out KingdomExpeditionOutcome outcome, out int scrap)
		{
			outcome = KingdomExpeditionOutcome.None;
			scrap = 0;
			if (jobId <= 0 || string.IsNullOrEmpty(settlementId)) return false;
			if (skillBonus < 0) skillBonus = 0;
			if (skillBonus > MaxSkillBonus) skillBonus = MaxSkillBonus;
			SemanticEventKey key;
			KernelFaultCode fault;
			if (!SemanticEventKey.TryCreate(KingdomCityRules.RulesVersion, settlementId,
				StreamId, KindCode, (ulong)jobId, out key, out fault)) return false;
			ulong band;
			ulong yield;
			if (!CounterRandom.TryDrawBelow(seed, key, OutcomeDrawIndex, 100uL, out band, out fault)
				|| !CounterRandom.TryDrawBelow(seed, key, YieldDrawIndex, 2uL, out yield, out fault))
				return false;
			int score = (int)band + skillBonus;
			if (score < 20)
			{
				outcome = KingdomExpeditionOutcome.PickedClean;
				scrap = 0;
			}
			else if (score < 75)
			{
				outcome = KingdomExpeditionOutcome.ModestFind;
				scrap = 1 + (int)yield;
			}
			else
			{
				outcome = KingdomExpeditionOutcome.RichFind;
				scrap = 3 + (int)yield;
			}
			return true;
		}

		internal static bool IsFrozenOutcome(int code)
		{
			return code >= (int)KingdomExpeditionOutcome.PickedClean
				&& code <= (int)KingdomExpeditionOutcome.RichFind;
		}

		internal static bool Due(long nowTick, long dueTick)
		{
			return nowTick >= 0L && dueTick > 0L && nowTick >= dueTick;
		}

		internal static bool IsPhase(int stored)
		{
			return KingdomJobRules.IsExpeditionPhase(stored);
		}

		internal static bool IsPaid(int stored)
		{
			return stored == (int)KingdomExpeditionPhase.Paid
				|| stored == (int)KingdomExpeditionPhase.Dispatched;
		}

		internal static bool IsDispatched(int stored)
		{
			return stored == (int)KingdomExpeditionPhase.Dispatched;
		}

		internal static bool IsResolutionPrepared(int stored)
		{
			return stored == (int)KingdomExpeditionPhase.ResolutionPrepared;
		}

		internal static bool IsTerminalOutcome(int stored)
		{
			return stored >= (int)KingdomExpeditionOutcome.ResidentDiedOnGround
				&& stored <= (int)KingdomExpeditionOutcome.ResidentJoinedFounder;
		}

		/// <summary>Classifies a receipt-bound physical state after any injected cut. A value still
		/// between before and after is safe forward progress; outside that closed range is a conflict
		/// and must not be charged again. Absence is exact only for a zero target.</summary>
		internal static bool TryDebitProgress(int before, int after, bool present, int current,
			out int remaining)
		{
			remaining = 0;
			if (before <= 0 || after < 0 || after >= before) return false;
			if (!present) return after == 0;
			if (current < after || current > before) return false;
			remaining = current - after;
			return true;
		}
	}
}
