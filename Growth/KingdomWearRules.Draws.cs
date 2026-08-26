using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomWearRules
	{

		// ==================================================================================
		// The draw. Counter-based, on a key that names the settlement, the work, the cause and
		// the event's own ordinal, so the answer is a pure function of those four and of nothing
		// that happened in between &mdash; the same shape KingdomConversionRules.Converts uses,
		// for the same reason: an ordinary pseudorandom call's cursor depends on every unrelated
		// roll made since the game started, and a reload must not reroll a question already
		// answered.
		// ==================================================================================

		private const int WearRulesVersion = 1;

		private const uint WearDrawIndex = 0u;

		/// <summary>Fixed, all-zero seed, for the reason <c>KingdomChronicle</c> gives at length:
		/// domain separation comes entirely from the settlement id, stream, kind and ordinal baked
		/// into the key, and whether one particular work wears does not need to be unguessable.
		/// </summary>
		private static readonly KernelSeed128 WearSeed = default(KernelSeed128);

		private const string StreamPrefix = "taf:wear:";

		private const string StreamSuffix = ":v1";

		/// <summary>The byte budget <c>KernelSemanticId</c> allows an id. Stated here rather than
		/// read from the kernel because that constant is the kernel's own and this file must fold
		/// to fit it, not reach into it.</summary>
		private const int KernelSemanticIdBudget = 128;

		/// <summary>Which channel a draw is asked on. Each is its own kernel kind code sharing one
		/// work's stream, so a hard-running roll and a temperamental roll on the same work in the
		/// same pass draw independently. Values are frozen the same way
		/// <see cref="ConversionChannel"/>'s are: never zero, never renumbered.</summary>
		public enum WearChannel
		{
			HardRunning = 1,
			Temperamental = 2,
			Raid = 3,
		}

		/// <summary>
		/// Folds one work's own id into the frozen <c>taf:</c> semantic-id grammar, exactly as
		/// <see cref="KingdomConversionRules.ResidentStream"/> folds a settler's name. The work
		/// belongs in the stream rather than the ordinal because two different works asked about
		/// in the same pass must not be forced to share one answer.
		/// </summary>
		/// <param name="WorkId">The work's own persistent <c>GameObject.ID</c>. Null and blank
		/// yield the lane an unidentified work would draw on, which nothing in production ever
		/// asks for.</param>
		internal static string WorkStream(string WorkId)
		{
			StringBuilder builder = new StringBuilder(StreamPrefix);
			int room = KernelSemanticIdBudget - StreamPrefix.Length - StreamSuffix.Length;
			if (!string.IsNullOrEmpty(WorkId))
			{
				foreach (char c in WorkId)
				{
					if (builder.Length - StreamPrefix.Length >= room)
					{
						break;
					}
					if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-')
					{
						builder.Append(c);
					}
					else if (c >= 'A' && c <= 'Z')
					{
						builder.Append((char)(c + 32));
					}
					else
					{
						builder.Append('-');
					}
				}
			}
			if (builder.Length == StreamPrefix.Length)
			{
				builder.Append("unidentified");
			}
			builder.Append(StreamSuffix);
			return builder.ToString();
		}

		private static bool Draw(string SettlementId, string WorkId, WearChannel Channel, ulong Ordinal, int ChancePercent)
		{
			if (!SemanticEventKey.TryCreate(WearRulesVersion, SettlementId, WorkStream(WorkId), (uint)Channel, Ordinal, out var key, out var fault))
			{
				return false;
			}
			if (!CounterRandom.TryDrawBelow(WearSeed, key, WearDrawIndex, 100uL, out var value, out fault))
			{
				return false;
			}
			return (int)value < ChancePercent;
		}

		/// <summary>Whether a hard-running work wears at this streak. False below the first
		/// milestone, and false (never faulting) for a malformed settlement id.</summary>
		public static bool RollHardRun(string SettlementId, string WorkId, int Streak)
		{
			if (!AtHardRunMilestone(Streak))
			{
				return false;
			}
			return Draw(SettlementId, WorkId, WearChannel.HardRunning, HardRunMilestone(Streak), HardRunChancePercent);
		}

		/// <summary>Whether a certified machine acts up this pass. <paramref name="Tick"/> is the
		/// engine tick of the pass it ran on, so every pass it runs is an independent question
		/// &mdash; unlike hard running, there is no milestone to wait out.</summary>
		public static bool RollTemperamental(string SettlementId, string WorkId, long Tick)
		{
			return Draw(SettlementId, WorkId, WearChannel.Temperamental, (ulong)((Tick > 0L) ? Tick : 0L), TemperamentalChancePercent);
		}

		/// <summary>Whether one candidate work is among a raid's targets. <paramref name="RaidTick"/>
		/// is the raid's own due tick, so every raid asks the question fresh.</summary>
		public static bool RollRaidDamage(string SettlementId, string WorkId, long RaidTick)
		{
			return Draw(SettlementId, WorkId, WearChannel.Raid, (ulong)((RaidTick > 0L) ? RaidTick : 0L), RaidDamageChancePercent);
		}

	}
}
