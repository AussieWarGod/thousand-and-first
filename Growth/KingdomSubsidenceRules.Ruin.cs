using System.Collections.Generic;
using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomSubsidenceRules
	{
		// ==================================================================================
		// 5. Ruin. Damage, never deletion (STANDARDS 7).
		// ==================================================================================

		/// <summary>
		/// Chance one standing work is among those the WIDEST rung there is ruins &mdash; a City
		/// ceasing to be a city. Every shallower rung reaches proportionally less of the
		/// settlement (<see cref="RuinChanceFor"/>).
		/// <para>
		/// This replaced a flat allowance of two works a rung, which was the whole of Addendum
		/// 10(c)'s first complaint: a City falling all the way to Camp left eight works scuffed
		/// out of however many dozen were standing, and the rest pristine. "A place that has gone
		/// from city back to a few tents should have ruins on the plots that were previously
		/// buildings" is a statement about MOST of the plots, so the allowance had to stop being
		/// a count at all.
		/// </para>
		/// </summary>
		public const int RuinChancePercent = 50;

		/// <summary>
		/// Chance one standing work is among those THIS lost rung ruins: the rung's own scale,
		/// out of the scales there are. A City rung reaches half the settlement, a Steading rung
		/// a fifth of it, and the ladder in between is the same "the rung's ordinal plus one"
		/// shape <see cref="SettlersPerStep"/> already sheds people by.
		/// <para>
		/// <b>This is the reach rule</b> (Addendum 10(c)): each lost rung reaches the works that
		/// rung's scale supported, so a one-rung slide scuffs a corner of the settlement and a
		/// City falling all the way asks every standing work four separate times, at four
		/// narrowing chances. Nothing here bounds how MANY works a rung takes &mdash; the reach
		/// is a chance asked of every work independently, not a quota filled in survey order, so
		/// the field of ruins is a pure function of the draws and does not depend on which work
		/// happened to be raised first.
		/// </para>
		/// </summary>
		/// <param name="From">The rung being lost &mdash; what the settlement WAS. Out-of-range
		/// values clamp to the ladder rather than faulting.</param>
		public static int RuinChanceFor(GrowthStage From)
		{
			int index = (int)From;
			if (index < 0)
			{
				index = 0;
			}
			if (index > (int)GrowthStage.City)
			{
				index = (int)GrowthStage.City;
			}
			return RuinChancePercent * (index + 1) / ((int)GrowthStage.City + 1);
		}

		/// <summary>
		/// Wear one lost rung adds to a work it takes: the complement of what
		/// <c>KingdomRules.StandingPercent</c> says survives a ruined interregnum, halved and
		/// scaled to the wear ceiling.
		/// <para>
		/// Halved because a subsidence is not an interregnum. <c>StandingPercent</c> answers "how
		/// much of a settlement nobody has lived in for a generation is still up"; this is one
		/// rung of a place that is still lived in, still mends itself, and still has people in it
		/// arguing about which cistern to dig. Two or three rungs bring a work to
		/// <c>KingdomMaterialRules.MaxWearPercent</c>, which it never passes, so a city that falls
		/// all the way to Camp is derelict and legible rather than gone.
		/// </para>
		/// </summary>
		/// <param name="Roll">Adversity, 0 to 99. A high draw is a hard fall, exactly as
		/// <c>StandingPercent</c> reads it.</param>
		/// <returns>At least one, so a ruin is never a no-op that reads like one.</returns>
		public static int RuinIncrement(int Roll)
		{
			int standing = KingdomRules.StandingPercent(KingdomRules.InheritedState.Ruins, Roll);
			int increment = KingdomMaterialRules.MaxWearPercent * (100 - standing) / 200;
			return (increment < 1) ? 1 : increment;
		}

		// ----------------------------------------------------------------------------------
		// The draws. Counter-based on a key naming the settlement, the work, the channel and the
		// breakpoint's own ordinal, exactly as KingdomWearRules' three causes are: an ordinary
		// pseudorandom cursor depends on every unrelated roll since the game started, and a reload
		// must not re-roll a collapse the chronicle has already described.
		//
		// The stream grammar is folded here rather than borrowed from KingdomWearRules, whose own
		// folder is private to the "taf:wear:" prefix. Two files, two prefixes, so a log can tell
		// a work that wore out from a work that was let go.
		// ----------------------------------------------------------------------------------

		private const int SubsidenceRulesVersion = 1;

		private const uint SubsidenceDrawIndex = 0u;

		/// <summary>Fixed, all-zero seed, for the reason <c>KingdomChronicle</c> gives at length:
		/// domain separation comes entirely from the settlement id, stream, kind and ordinal baked
		/// into the key, and which shed sags is not a question that needs to be unguessable.
		/// </summary>
		private static readonly KernelSeed128 SubsidenceSeed = default(KernelSeed128);

		private const string StreamPrefix = "taf:subsidence:";

		private const string StreamSuffix = ":v1";

		/// <summary>The byte budget <c>KernelSemanticId</c> allows an id. Stated here rather than
		/// read from the kernel because that constant is the kernel's own and this file must fold
		/// to fit it, not reach into it.</summary>
		private const int KernelSemanticIdBudget = 128;

		/// <summary>Which question a draw answers. Frozen: never zero, never renumbered.</summary>
		public enum SubsidenceChannel
		{
			/// <summary>Whether one standing work is among those a lost rung ruins.</summary>
			Ruin = 1,

			/// <summary>How hard that fall was for it &mdash; the adversity
			/// <see cref="RuinIncrement"/> reads.</summary>
			Severity = 2,
		}

		/// <summary>Folds one work's own id into the frozen <c>taf:</c> semantic-id grammar. The
		/// work belongs in the stream rather than the ordinal because two works asked about at the
		/// same breakpoint must not be forced to share one answer.</summary>
		/// <param name="WorkId">The work's persistent <c>GameObject.id</c>. Null and blank yield
		/// the lane an unidentified work would draw on.</param>
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

		private static bool TryDraw(string SettlementId, string WorkId, SubsidenceChannel Channel, ulong Ordinal, out int Value)
		{
			Value = 0;
			if (!SemanticEventKey.TryCreate(SubsidenceRulesVersion, SettlementId, WorkStream(WorkId), (uint)Channel, Ordinal, out var key, out var _))
			{
				return false;
			}
			if (!CounterRandom.TryDrawBelow(SubsidenceSeed, key, SubsidenceDrawIndex, 100uL, out var value, out var _))
			{
				return false;
			}
			Value = (int)value;
			return true;
		}

		/// <summary>
		/// Whether one standing work is among those this lost rung ruins. False (never faulting)
		/// for a malformed settlement id, which ruins nothing and is the safe answer.
		/// <para>
		/// <b>The draw did not move when the reach did.</b> The key is the same settlement, work,
		/// channel and breakpoint ordinal it always was &mdash; what changed in Addendum 10(c) is
		/// only how much of the ladder answers yes (<see cref="RuinChanceFor"/>). So a wider rung
		/// ruins a strict SUPERSET of what a narrower rung would have ruined out of the same
		/// works, and a save whose collapse has already been chronicled draws exactly the numbers
		/// it drew before.
		/// </para>
		/// </summary>
		/// <param name="SettlementId">The settlement's kernel id.</param>
		/// <param name="WorkId">The work's persistent object id.</param>
		/// <param name="Ordinal">The breakpoint's own ordinal, so every rung asks fresh.</param>
		/// <param name="From">The rung being lost, which sets how far it reaches.</param>
		public static bool RollRuin(string SettlementId, string WorkId, ulong Ordinal, GrowthStage From)
		{
			int value;
			return TryDraw(SettlementId, WorkId, SubsidenceChannel.Ruin, Ordinal, out value) && value < RuinChanceFor(From);
		}

		/// <summary>How hard this rung's fall was for one work, as the wear it adds. Zero when the
		/// draw could not be made, which adds nothing rather than guessing.</summary>
		public static int RolledRuinIncrement(string SettlementId, string WorkId, ulong Ordinal)
		{
			int value;
			return TryDraw(SettlementId, WorkId, SubsidenceChannel.Severity, Ordinal, out value) ? RuinIncrement(value) : 0;
		}

	}
}
