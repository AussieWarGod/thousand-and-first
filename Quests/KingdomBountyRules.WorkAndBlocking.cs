using System.Collections.Generic;
using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomBountyRules
	{
		// ==================================================================================
		// How long the work takes
		// ==================================================================================

		/// <summary>Days a fetch takes before the first unit is set down, however small.</summary>
		public const int HaulBaseDays = 1;

		/// <summary>Units one porter shifts in a day beyond the first.</summary>
		public const int HaulUnitsPerDay = 8;

		/// <summary>The most days one fetch can take, however big the pile.</summary>
		public const int HaulMaxDays = 5;

		/// <summary>How long carrying a marked pile in takes.</summary>
		/// <param name="Units">Material units in the pile. Zero or less still takes the base day,
		/// because the porter still walks there.</param>
		public static int HaulDays(int Units)
		{
			int units = (Units > 0) ? Units : 0;
			int days = HaulBaseDays + (units / HaulUnitsPerDay);
			return (days > HaulMaxDays) ? HaulMaxDays : days;
		}

		/// <summary>Days a settler stands a work for, once they take a manning notice. A season,
		/// in the settlement's own reckoning.</summary>
		public const int ManningSeasonDays = 30;

		/// <summary>Days walking the frontier edge and coming back takes.</summary>
		public const int ScoutDays = 4;

		/// <summary>How long a taken task runs before the price falls due.</summary>
		/// <param name="Task">The task taken.</param>
		/// <param name="Magnitude">Units for a fetch; ignored otherwise.</param>
		/// <returns>Days, or 0 for a clearance &mdash; whose clock is the clearing gang's own
		/// effort, not a countdown.</returns>
		public static int WorkDays(BountyTask Task, int Magnitude)
		{
			switch (Task)
			{
			case BountyTask.Fetch:
				return HaulDays(Magnitude);
			case BountyTask.Manning:
				return ManningSeasonDays;
			case BountyTask.Scouting:
				return ScoutDays;
			default:
				return 0;
			}
		}

		/// <summary>Absolute completion tick, saturated rather than wrapped into the past.</summary>
		public static long WorkDueTick(long TakenTick, int Days)
		{
			long taken = (TakenTick > 0L) ? TakenTick : 0L;
			if (Days <= 0)
			{
				return 0L;
			}
			long duration = (long)Days * KingdomRules.TicksPerDay;
			return (taken > long.MaxValue - duration) ? long.MaxValue : taken + duration;
		}

		// ==================================================================================
		// Saying why, once
		// ==================================================================================

		/// <summary>
		/// Whether a reason means the notice can never be attempted, as opposed to merely not
		/// today. A permanent reason is announced once and then left alone; a block is announced
		/// once per stall and re-announced if it lifts and returns.
		/// </summary>
		public static bool IsPermanent(BountyBlock Block)
		{
			return Block == BountyBlock.NothingStanding
				|| Block == BountyBlock.PileEmpty
				|| Block == BountyBlock.NoWorks
				|| Block == BountyBlock.NoFrontier
				|| Block == BountyBlock.ManningTargetLost;
		}

		/// <summary>
		/// The founder-facing sentence for a reason a notice is not moving, ready for the ledger.
		/// Names the task, because a founder with three notices standing needs to know which one
		/// went quiet.
		/// </summary>
		/// <param name="Block">The reason. <see cref="BountyBlock.None"/> yields null.</param>
		/// <param name="Task">The task the notice posted.</param>
		/// <param name="SeatName">The settlement's seat name.</param>
		/// <returns>A complete sentence, or null when there is nothing to say.</returns>
		public static string BlockReason(BountyBlock Block, BountyTask Task, string SeatName)
		{
			string seat = string.IsNullOrEmpty(SeatName) ? "the settlement" : SeatName;
			switch (Block)
			{
			case BountyBlock.NobodyToTry:
				return "A notice stands at the heart of " + seat + " offering water to whoever will " + TaskName(Task) + ", and there is nobody living here to read it.";
			case BountyBlock.NothingStanding:
				return "The notice posted over the staked ground names nothing that has to come down. No one will ever claim it; take it down when you like.";
			case BountyBlock.PileEmpty:
				return "The pile the notice was posted over holds nothing the settlement counts as material. No one will ever claim it; take it down when you like.";
			case BountyBlock.NowhereToCarry:
				return "A porter would carry the marked pile in, and " + seat + " has no stockpile dedicated to put it in. Dedicate a container.";
			case BountyBlock.NoWorks:
				return seat + " has no works at all, so the notice offering a season's manning names nothing. No one will ever claim it; take it down when you like.";
			case BountyBlock.NoIdleWork:
				return "The notice offering a season's manning stands unclaimed: every work in " + seat + " already has its hands.";
			case BountyBlock.NoFrontier:
				return "There is no unclaimed ground left along the edge of " + seat + " for a scout to walk to. No one will ever claim it; take it down when you like.";
			case BountyBlock.StoresCannotPay:
				return "The notice at " + seat + " is claimed and the work is done, and the stores cannot cover the price. It stays owed until they can.";
			case BountyBlock.ManningTargetLost:
				return "The exact work named by the manning notice no longer stands at " + seat + ". The service clock is stopped; take the notice down when you like.";
			case BountyBlock.ManningWorkerAbsent:
				return "The resident who took the manning notice is not grounded at " + seat + ". The service clock is stopped until they return.";
			case BountyBlock.NoFreeHands:
				return "The manning notice has no hand left in " + seat + "'s ordinary work pool. The service clock is stopped; reduce other duties or add labour.";
			default:
				return null;
			}
		}

	}
}
