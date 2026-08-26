using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomUpgradeRules
	{
		/// <summary>
		/// Water an improvement costs. The default is the difference between the two designs
		/// &mdash; the founder already paid for the predecessor and its materials do not vanish
		/// &mdash; floored at <see cref="MinimumCostDrams"/> so an improvement is never free, and
		/// capped at the successor's own price so improving is never dearer than razing and
		/// building fresh would have been.
		/// </summary>
		/// <param name="SuccessorCost">The successor design's own <c>Cost</c>.</param>
		/// <param name="PredecessorCost">The predecessor design's own <c>Cost</c>.</param>
		/// <param name="Override">Authored <c>UpgradeCost</c>, or <see cref="Unset"/>.</param>
		/// <returns>Drams, never negative.</returns>
		public static int CostDrams(int SuccessorCost, int PredecessorCost, int Override)
		{
			if (Override >= 0)
			{
				return Override;
			}
			int successor = (SuccessorCost > 0) ? SuccessorCost : 0;
			int predecessor = (PredecessorCost > 0) ? PredecessorCost : 0;
			int cost = successor - predecessor;
			if (cost < MinimumCostDrams)
			{
				cost = MinimumCostDrams;
			}
			if (cost > successor)
			{
				cost = successor;
			}
			return cost;
		}

		/// <summary>
		/// Ticks an improvement takes. Defaults to <see cref="BuildTicksPercent"/> of building
		/// the successor from nothing, floored at one tick so an improvement can never complete
		/// in the same instant it began, or in the past.
		/// </summary>
		/// <param name="SuccessorTicks">The successor design's own <c>Ticks</c>.</param>
		/// <param name="Override">Authored <c>UpgradeTicks</c>, or <see cref="UnsetTicks"/>.
		/// </param>
		public static long BuildTicks(long SuccessorTicks, long Override)
		{
			if (Override > 0L)
			{
				return Override;
			}
			long ticks = ((SuccessorTicks > 0L) ? SuccessorTicks : 1L) * BuildTicksPercent / 100L;
			return (ticks < 1L) ? 1L : ticks;
		}

		/// <summary>
		/// Settlers who must be free of every other duty for the work to start. Defaults to the
		/// crew the successor will need to run once it stands &mdash; a settlement that cannot
		/// man a thing has no business raising it &mdash; and never fewer than
		/// <see cref="MinimumCrew"/>.
		/// </summary>
		/// <param name="SuccessorStaff">The successor design's own <c>Staff</c>.</param>
		/// <param name="Override">Authored <c>UpgradeCrew</c>, or <see cref="Unset"/>.</param>
		public static int CrewRequired(int SuccessorStaff, int Override)
		{
			int crew = (Override >= 0) ? Override : SuccessorStaff;
			return (crew < MinimumCrew) ? MinimumCrew : crew;
		}

		/// <summary>
		/// The growth stage an improvement waits for. Defaults to the successor's own
		/// <c>MinStage</c>, so a chain that names nothing extra inherits exactly the gate the
		/// commission list already uses and can never let a work sneak past it.
		/// </summary>
		public static GrowthStage StageRequired(GrowthStage SuccessorMinStage, bool HasOverride, GrowthStage Override)
		{
			if (!HasOverride)
			{
				return SuccessorMinStage;
			}
			return (Override > SuccessorMinStage) ? Override : SuccessorMinStage;
		}

		/// <summary>
		/// Water an improvement must leave standing in the stores: <see cref="KingdomRules.ReserveDays"/>
		/// days of drinking at this settlement's own rate. Improving is a luxury paid out of
		/// surplus, so it can never be the reason a settlement goes thirsty. (The cushion used to
		/// be described as "the whole absence it is ever charged for"; there is no such thing any
		/// more - absence is charged in full - so it is now simply a named reserve depth.)
		/// </summary>
		/// <param name="Population">Settlers the stores must keep.</param>
		/// <param name="Stage">Growth stage, which sets the per-head rate.</param>
		public static int ReserveDrams(int Population, GrowthStage Stage)
		{
			return KingdomRules.UpkeepDrams(Population, Stage) * KingdomRules.ReserveDays;
		}

		/// <summary>
		/// Whether the stores can pay for an improvement and still hold the reserve.
		/// </summary>
		/// <param name="StoredWater">Drams currently in the dedicated stores.</param>
		/// <param name="Cost">Drams the improvement asks for.</param>
		/// <param name="Reserve">Drams that must remain, from <see cref="ReserveDrams"/>.</param>
		public static bool CanAfford(int StoredWater, int Cost, int Reserve)
		{
			return StoredWater - Cost >= Reserve;
		}

		/// <summary>
		/// Drams the stores are short of affording an improvement, for the sentence the founder
		/// reads. Zero when it is affordable.
		/// </summary>
		public static int Shortfall(int StoredWater, int Cost, int Reserve)
		{
			int missing = Cost + Reserve - StoredWater;
			return (missing > 0) ? missing : 0;
		}

		/// <summary>
		/// Whether the successor can receive everything the predecessor is holding. A liquid
		/// capacity of <see cref="UnknownCapacity"/> means the successor's blueprint did not
		/// declare one, which is not evidence of a problem and never blocks; a real capacity
		/// smaller than what is stored is, and does.
		/// </summary>
		/// <param name="StoredLiquid">Drams of anything in the predecessor.</param>
		/// <param name="SuccessorCapacity">The successor's declared liquid capacity, or
		/// <see cref="UnknownCapacity"/>.</param>
		/// <param name="HeldItems">Objects inside the predecessor.</param>
		/// <param name="SuccessorHoldsItems">Whether the successor has an inventory at all.
		/// </param>
		public static bool ContentsWouldFit(int StoredLiquid, int SuccessorCapacity, int HeldItems, bool SuccessorHoldsItems)
		{
			if (HeldItems > 0 && !SuccessorHoldsItems)
			{
				return false;
			}
			if (StoredLiquid > 0 && SuccessorCapacity != UnknownCapacity && SuccessorCapacity < StoredLiquid)
			{
				return false;
			}
			return true;
		}

	}
}
