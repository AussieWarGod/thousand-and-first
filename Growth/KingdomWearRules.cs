using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	/// <summary>
	/// Engine-free rules for WHETHER a work wears, what that damage costs it, and what a repair
	/// job is waiting on (BUILDING-CATALOGUE-BRIEF.md Addendum 7: "maintenance/wear translation",
	/// Addendum 10(b): "damage degrades function &mdash; for every work, in its own kind").
	/// <para>
	/// <b>Time is labour, never decay.</b> Nothing here ever DECIDES that a work wore from a
	/// clock: every function that answers "did this take damage" is a pure function of an EVENT
	/// already in hand &mdash; a raid tick, a streak of consecutive full-stretch attended passes,
	/// a tick a certified machine was running &mdash; never of how long the save has existed.
	/// What a work that is ALREADY damaged goes on losing is a different question, and that one
	/// does run on world days (<see cref="Leaked"/>): a hole in a cistern empties it whether
	/// anybody is watching. The damage is still an event; only its consequence is a clock, and
	/// mending ends the consequence outright.
	/// </para>
	/// <para>
	/// What a work's wear COSTS to mend, how it runs while worn, and the one line a founder reads
	/// when a work takes damage all belong to the chain the wear was built from, and already live
	/// on <c>KingdomMaterialRules</c> (<c>MaxWearPercent</c>, <c>AddWear</c>,
	/// <c>ConditionPercent</c>/<c>ConditionWord</c>, <c>RepairCost</c>/<c>RepairBits</c>/
	/// <c>RepairEffort</c>, <c>DamageLine</c>) &mdash; this file calls those rather than keeping a
	/// second, divergent copy of them. What is uniquely this file's own: whether an event wears a
	/// work at all (the three causes and their kernel draws), what a damaged work of any kind is
	/// worth to the settlement afterwards (<see cref="WorkEffectiveness"/>), what a damaged STORE
	/// goes on losing while it stands unmended (<see cref="Leaked"/>), and whether a repair job
	/// already under way is READY, or waiting on hands, materials, or the founder's own standing
	/// wish.
	/// </para>
	/// <para>
	/// The three causes draw on <see cref="ThousandAndFirst.Simulation.Kernel.CounterRandom"/> the
	/// same way <see cref="KingdomConversionRules"/> does: a pure function of the settlement, the
	/// work, the cause, and the event's own ordinal (a raid tick, a hard-run milestone, an engine
	/// tick a machine was running), so a reload asks every open question exactly once more and
	/// never rerolls a question already answered. The engine-coupled half that walks a real
	/// <c>Survey.Works</c>, attaches <c>XRL.World.Parts.r_KingdomWear</c>, spends real stockpiled
	/// material and bits, and writes the founder-facing lines this file only composes, is
	/// <c>KingdomWear</c>, beside it.
	/// </para>
	/// </summary>
	public static class KingdomWearRules
	{
		// ==================================================================================
		// The three causes. Named, never anonymous: every damage event says which of the three
		// it was, both in the moment (the message this file composes) and later (LastCause on
		// the part), because "the mill is worn" is a different sentence from "the mill was
		// raided" and the founder is owed the difference.
		// ==================================================================================

		public enum WearCause
		{
			/// <summary>Sentinel for "nothing has ever damaged this work". Never itself the
			/// subject of a message.</summary>
			None = 0,

			/// <summary>Raiders who got past the wall.</summary>
			Raid = 1,

			/// <summary>Crewed at full stretch for many consecutive attended passes.</summary>
			HardRunning = 2,

			/// <summary>Certified salvage, acting up on use.</summary>
			TemperamentalTech = 3,
		}

		/// <summary>The clause a damage line names the cause by.</summary>
		public static string CauseVerb(WearCause Cause)
		{
			switch (Cause)
			{
			case WearCause.Raid:
				return "broken into by raiders who got past the wall";
			case WearCause.HardRunning:
				return "run past what it was built to bear, crewed at full stretch too long";
			case WearCause.TemperamentalTech:
				return "acted up under its own certified hands";
			default:
				return "damaged";
			}
		}

		public const int RaidDamageIncrement = 15;

		public const int HardRunDamageIncrement = 10;

		public const int TemperamentalDamageIncrement = 20;

		/// <summary>The wear one instance of a cause adds, for
		/// <see cref="KingdomMaterialRules.AddWear"/> to clamp at
		/// <see cref="KingdomMaterialRules.MaxWearPercent"/>. Never called for
		/// <see cref="WearCause.None"/>; returns zero for it rather than throwing, so a defensive
		/// caller reads "no damage" instead of crashing on a sentinel.</summary>
		public static int IncrementFor(WearCause Cause)
		{
			switch (Cause)
			{
			case WearCause.Raid:
				return RaidDamageIncrement;
			case WearCause.HardRunning:
				return HardRunDamageIncrement;
			case WearCause.TemperamentalTech:
				return TemperamentalDamageIncrement;
			default:
				return 0;
			}
		}

		/// <summary>
		/// What a work actually manages this pass: this pass's crew stretch (0-100, from
		/// headcount and capability) reduced again by how worn the work itself is
		/// (<see cref="KingdomMaterialRules.ConditionPercent"/>). Two independent reasons a work
		/// can run under full, combined by multiplying their fractions rather than by picking the
		/// worse of the two: a fully-crewed but half-wrecked mill and a well-kept mill crewed at
		/// half strength are not the same settlement, and should not read as one.
		/// </summary>
		public static int CombinedEffectiveness(int CrewStretch, int Wear)
		{
			int stretch = (CrewStretch < 0) ? 0 : ((CrewStretch > 100) ? 100 : CrewStretch);
			return stretch * KingdomMaterialRules.ConditionPercent(Wear) / 100;
		}

		/// <summary>
		/// What ANY finished work is worth this pass, crewed or not (Addendum 10(b): "wear reduces
		/// every work's level contribution, staffed or not"). One rule with two arms, and the
		/// second arm is the ruling:
		/// <list type="bullet">
		/// <item>a work that asks for crew runs at <see cref="CombinedEffectiveness"/> &mdash; the
		/// staffing pass's stretch, reduced again by its own condition;</item>
		/// <item>a work that asks for nobody runs at its CONDITION alone. A cistern holds water
		/// whoever is home, and a holed cistern holds less of it.</item>
		/// </list>
		/// <para>
		/// The arm that was wrong was the second: a staffless work was handed a flat 100, so a
		/// ruined reservoir carried its full twenty-six drams and only crewed works ever felt
		/// damage. Both arms return 100 for a sound work, which is what makes this a strict
		/// refinement of the ternary it replaces rather than a new tax.
		/// </para>
		/// </summary>
		/// <param name="StaffNeeded">The design's <c>KingdomStaffNeeded</c>. Zero or less means
		/// the work asks for nobody.</param>
		/// <param name="CrewStretch">The staffing pass's own 0-100 stamp, read BEFORE any wear is
		/// folded into it. Ignored entirely for a staffless work, which never carries one.</param>
		/// <param name="Wear">The work's own wear, 0 to
		/// <see cref="KingdomMaterialRules.MaxWearPercent"/>.</param>
		public static int WorkEffectiveness(int StaffNeeded, int CrewStretch, int Wear)
		{
			return (StaffNeeded > 0)
				? CombinedEffectiveness(CrewStretch, Wear)
				: KingdomMaterialRules.ConditionPercent(Wear);
		}

		// ==================================================================================
		// Hard running: a streak of consecutive full-stretch ATTENDED passes, re-eligible for a
		// draw once per whole further streak of the same length &mdash; the same
		// milestone-and-reroll shape KingdomConversionRules.AtMilestone/Milestone use, so a
		// question the kernel already answered "no" for this stretch is not asked again until a
		// whole further stretch has been run.
		// ==================================================================================

		/// <summary>Consecutive full-stretch attended passes before a hard-running work is
		/// eligible to be asked whether it wore. Generous: an occasional day at full output is
		/// ordinary work, not the mill run mercilessly.</summary>
		public const int HardRunStreakThreshold = 8;

		/// <summary>Whether this streak has reached a milestone and the draw is owed.</summary>
		public static bool AtHardRunMilestone(int Streak)
		{
			return Streak >= HardRunStreakThreshold;
		}

		/// <summary>Which milestone this streak stands at &mdash; the kernel ordinal the draw is
		/// keyed on, exactly as <see cref="KingdomConversionRules.Milestone"/> keys conversion.
		/// Zero below the first milestone, where nothing calls it.</summary>
		public static ulong HardRunMilestone(int Streak)
		{
			return (Streak < HardRunStreakThreshold) ? 0uL : (ulong)(Streak / HardRunStreakThreshold);
		}

		public const int HardRunChancePercent = 15;

		public const int TemperamentalChancePercent = 4;

		public const int RaidDamageChancePercent = 35;

		/// <summary>Works one raid may damage, at most. Bounded: a large raid still damages at
		/// most a couple of works, never sweeps the settlement.</summary>
		public const int MaxWorksDamagedPerRaid = 2;

		/// <summary>How many works one raid may damage, before the per-work chance is even asked.
		/// Grows gently with the size of the party that got past the wall, and never past
		/// <see cref="MaxWorksDamagedPerRaid"/>.</summary>
		public static int WorksToDamage(int RaidersThrough)
		{
			if (RaidersThrough <= 0)
			{
				return 0;
			}
			int count = 1 + RaidersThrough / 4;
			return (count > MaxWorksDamagedPerRaid) ? MaxWorksDamagedPerRaid : count;
		}

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
		/// <param name="WorkId">The work's own persistent <c>GameObject.id</c>. Null and blank
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

		// ==================================================================================
		// Repair readiness. Shaped like KingdomUpgradeRules' own verdict: a small enum, an
		// assessment that never throws, and a 7b-legible reason for every blocked value. Ready is
		// zero on purpose &mdash; the same "never itself announced as a block" sentinel
		// r_KingdomImprovement.AnnouncedReason relies on. What a repair COSTS is
		// KingdomMaterialRules' own (RepairCost/RepairBits/RepairEffort); this only ever asks
		// whether this pass's hands and this ground's stockpiles cover it.
		// ==================================================================================

		public enum RepairVerdict
		{
			Ready = 0,
			NoHands = 1,
			NoMaterials = 2,
			Held = 3,
			OtherWorkUnderway = 4,
		}

		/// <summary>
		/// What one repair pass is owed, from the founder's own standing wish, the hands this
		/// pass has to spare, and whether the stockpiles cover the cost. Only ever called for a
		/// work that is actually worn; a sound work has nothing to assess.
		/// </summary>
		public static RepairVerdict AssessRepair(bool Held, int FreeHands, bool MaterialsCovered)
		{
			if (Held)
			{
				return RepairVerdict.Held;
			}
			if (FreeHands <= 0)
			{
				return RepairVerdict.NoHands;
			}
			if (!MaterialsCovered)
			{
				return RepairVerdict.NoMaterials;
			}
			return RepairVerdict.Ready;
		}

		/// <summary>True for a verdict that is a stall worth telling the founder about (STANDARDS
		/// 7b). False for <see cref="RepairVerdict.Ready"/>, for <see cref="RepairVerdict.Held"/>
		/// (the founder's own standing choice needs no reminder that it is in effect), and for
		/// <see cref="RepairVerdict.OtherWorkUnderway"/> (ordinary queueing, not a shortage).</summary>
		public static bool IsBlocked(RepairVerdict Verdict)
		{
			return Verdict == RepairVerdict.NoHands || Verdict == RepairVerdict.NoMaterials;
		}

		// ==================================================================================
		// Storage leaks (Addendum 10(b): "storage works leak their stored contents as wear
		// climbs"). The kind-appropriate consequence sitting on top of the general effectiveness
		// scale, and nothing here reads a clock either: the DAYS are handed in by the caller, the
		// same way every other day-taking rule in this mod takes them. Loss, not transfer - water
		// that runs out of a holed cistern goes into the ground and is gone, which is the honest
		// reading and deliberately NOT the manifest's pour-on-ground pools (that water was moved
		// somewhere a founder can walk up to; this water was lost).
		// ==================================================================================

		/// <summary>What a leaking store is losing. Two kinds because two things in this
		/// settlement are physically STORED: drams in a vessel and charge in a bed of salt. Food
		/// is deliberately absent &mdash; it is not a flow yet, and spoilage waits until it is
		/// (Addendum 10(b)). Values are frozen: never zero, never renumbered.</summary>
		public enum LeakKind
		{
			Water = 1,
			Charge = 2,
		}

		/// <summary>
		/// World days a store at the wear ceiling takes to lose its WHOLE capacity to the ground.
		/// The one number the leak is tuned on, and a season is what it is tuned to: the same
		/// length the doctrine's own prose keeps reaching for, and the length a whole slide from
		/// City to Camp runs in. Measured against the daily water bill of the rung each store
		/// belongs to (_notes/balance-sim.py, Q9's leak table), a store at the CEILING loses less
		/// than a day's drinking in a day, and one at the wear a single lost rung actually leaves
		/// loses about a fifth of it &mdash; so a leak thins the cushion the settlement keeps and
		/// can never outrun what the settlement makes.
		/// </summary>
		public const int LeakDaysToEmptyAtCeiling = 50;

		/// <summary>
		/// What a damaged store loses over a stretch of world days. Linear in the wear, linear in
		/// the days, denominated against the store's own CAPACITY (the size of the hole is set by
		/// the damage, not by how full the vessel happens to be), and never more than is actually
		/// in there.
		/// <para>
		/// Zero wear is zero leak, exactly: a sound store is not a slow one. The division is done
		/// last, so a small store over a long absence still loses something rather than rounding
		/// to nothing every single day &mdash; and a caller that gets zero back is expected to
		/// BANK its days rather than spend them, which is what makes the accrual honest at any
		/// granularity of visit.
		/// </para>
		/// </summary>
		/// <param name="Capacity">The store's own capacity, in whatever unit it holds.</param>
		/// <param name="Held">What it holds right now. The answer never exceeds this.</param>
		/// <param name="Wear">The store's wear, 0 to
		/// <see cref="KingdomMaterialRules.MaxWearPercent"/>; anything higher reads as the
		/// ceiling.</param>
		/// <param name="Days">World days elapsed since this store was last leaked.</param>
		public static int Leaked(int Capacity, int Held, int Wear, int Days)
		{
			if (Capacity <= 0 || Held <= 0 || Wear <= 0 || Days <= 0)
			{
				return 0;
			}
			int wear = (Wear > KingdomMaterialRules.MaxWearPercent) ? KingdomMaterialRules.MaxWearPercent : Wear;
			// Long throughout: a city's capacity times the ceiling times a hundred-thousand-day
			// absence leaves int behind long before any of the three individually would.
			long lost = (long)Capacity * wear * Days
				/ ((long)KingdomMaterialRules.MaxWearPercent * LeakDaysToEmptyAtCeiling);
			return (lost >= Held) ? Held : (int)lost;
		}

		// ==================================================================================
		// Prose. Every line unique to this file (a damage event naming its cause, a completed
		// mending, a queued-behind-another-job wait, the Status/NextNeed summaries) is composed
		// once here and asserted directly. The condition wording itself
		// (<c>ConditionWord</c>/<c>ConditionPercent</c>) is quoted from
		// <c>KingdomMaterialRules</c> rather than re-derived, so the two systems never describe
		// the same work two different ways.
		// ==================================================================================

		public static string DamagedLine(string WorkName, WearCause Cause, int Wear)
		{
			return WorkName + " was " + CauseVerb(Cause) + ": " + KingdomMaterialRules.ConditionWord(Wear)
				+ " now, and runs " + KingdomMaterialRules.ConditionPercent(Wear) + " parts in a hundred until it is mended.";
		}

		public static string ReasonLine(RepairVerdict Verdict, string WorkName)
		{
			switch (Verdict)
			{
			case RepairVerdict.NoHands:
				return WorkName + " stands damaged, and there are no hands free to mend it.";
			case RepairVerdict.NoMaterials:
				return WorkName + " stands damaged, and the stockpiles are short of what mending it wants.";
			case RepairVerdict.OtherWorkUnderway:
				return WorkName + " stands damaged, and waits its turn while another mending has this pass's hands.";
			default:
				return null;
			}
		}

		public static string RepairBegunLine(string WorkName)
		{
			return "Mending begins on " + WorkName + ".";
		}

		public static string RepairCompleteLine(string WorkName)
		{
			return WorkName + " is mended, and runs at its full measure again.";
		}

		/// <summary>
		/// The one line a store gets when the founder is first told it is losing what it holds
		/// (STANDARDS 7b), named by kind because a cistern and a bed of salt fail in different
		/// sentences. Said once per work and unsaid by <see cref="LeakStoppedLine"/> when it is
		/// mended.
		/// </summary>
		public static string LeakBegunLine(string WorkName, LeakKind Kind)
		{
			return (Kind == LeakKind.Charge)
				? (WorkName + " has gone cold at the seams, and the night's charge bleeds out of it.")
				: (WorkName + " weeps down its east face, and what it holds runs away into the ground.");
		}

		/// <summary>The unsaying: mending restores function, so the leak is over the moment the
		/// work is whole. The consequence is of damage, not of history (Addendum 10(b)).</summary>
		public static string LeakStoppedLine(string WorkName, LeakKind Kind)
		{
			return (Kind == LeakKind.Charge)
				? (WorkName + " is sealed again, and keeps its heat overnight.")
				: (WorkName + " is sealed again, and holds every dram it is given.");
		}

		/// <summary>The Status report's own line for how many works stand damaged, or empty when
		/// none do. Mirrors <c>KingdomReports</c>' own inline idle-works phrasing so a founder
		/// reads the two the same way.</summary>
		public static string StatusSuffix(int DamagedWorks)
		{
			return (DamagedWorks > 0) ? ("  {{r|" + DamagedWorks + " works stand damaged and run reduced}}") : "";
		}

		/// <summary>The NextNeed advice line for damaged works, or empty when none stand damaged.</summary>
		public static string NextNeedLine(int DamagedWorks)
		{
			return (DamagedWorks > 0) ? (DamagedWorks + " of the works stand damaged and run reduced. Mending queues on its own, out of what the stores can spare.") : "";
		}
	}
}
