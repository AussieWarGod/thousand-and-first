using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomWearRules
	{
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

		/// <summary>
		/// What a leaking store is losing. THREE kinds because three things in this settlement
		/// are physically stored: drams in a vessel, charge in a bed of salt, and servings in a
		/// larder. Food was deliberately absent here until food became a flow, which is exactly
		/// the condition Addendum 10(b) deferred it on ("the pattern extends per kind as kinds
		/// become physical (food spoilage waits until food is a flow)"); Wave B made it one, so
		/// the deferral is spent. Values are frozen: never zero, never renumbered.
		/// </summary>
		public enum LeakKind
		{
			Water = 1,
			Charge = 2,
			Food = 3,
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

	}
}
