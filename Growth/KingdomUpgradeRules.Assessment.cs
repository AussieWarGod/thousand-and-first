using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomUpgradeRules
	{
		/// <summary>
		/// The settlement's verdict on improving one standing work. Checked in the order that
		/// respects intent before arithmetic: malformed or inapplicable data first, then work
		/// already under way, then what the founder said to leave alone &mdash; a founder who
		/// held a work is never lectured about its water &mdash; then the earned conditions in
		/// the order they can be acted on, and the one-at-a-time pacing gate last.
		/// </summary>
		/// <param name="HasSuccessor">Whether the design names anything to grow into.</param>
		/// <param name="SuccessorKnown">Whether that successor resolves to a registry entry.
		/// </param>
		/// <param name="StyleAllowed">Whether the city's style permits the successor.</param>
		/// <param name="OurWork">True only for a work the settlement itself raised; an adopted
		/// structure is the founder's and is never rebuilt.</param>
		/// <param name="AlreadyWorking">Whether this work's own improvement is under way.</param>
		/// <param name="HeldOnThisGround">Whether the founder held this whole settlement.</param>
		/// <param name="HeldByFounder">Whether the founder held this one work.</param>
		/// <param name="Stage">The settlement's growth stage.</param>
		/// <param name="StageNeeded">Stage the improvement waits for, from
		/// <see cref="StageRequired"/>.</param>
		/// <param name="FreeHands">Settlers not already spoken for.</param>
		/// <param name="CrewNeeded">Hands the work needs, from <see cref="CrewRequired"/>.</param>
		/// <param name="ContentsFit">From <see cref="ContentsWouldFit"/>.</param>
		/// <param name="StoredWater">Drams in the dedicated stores.</param>
		/// <param name="Cost">Drams the improvement asks for, from <see cref="CostDrams"/>.
		/// </param>
		/// <param name="Reserve">Drams that must remain, from <see cref="ReserveDrams"/>.</param>
		/// <param name="OtherWorkUnderway">Whether another improvement is already under way on
		/// this ground.</param>
		/// <param name="Absorption">What the improvement would cost the city while it happens
		/// (brief, Addendum 3). Null means nothing was measured and grants every absorption check,
		/// which is exactly the behaviour that shipped before the law existed.</param>
		public static UpgradeVerdict Assess(bool HasSuccessor, bool SuccessorKnown, bool StyleAllowed, bool OurWork, bool AlreadyWorking, bool HeldOnThisGround, bool HeldByFounder, GrowthStage Stage, GrowthStage StageNeeded, int FreeHands, int CrewNeeded, bool ContentsFit, int StoredWater, int Cost, int Reserve, bool OtherWorkUnderway, AbsorptionDemand? Absorption = null)
		{
			AbsorptionDemand demand = Absorption ?? AbsorptionDemand.None;
			if (!HasSuccessor)
			{
				return UpgradeVerdict.NoSuccessor;
			}
			if (!SuccessorKnown)
			{
				return UpgradeVerdict.SuccessorUnknown;
			}
			if (!OurWork)
			{
				return UpgradeVerdict.NotOurWork;
			}
			if (!StyleAllowed)
			{
				return UpgradeVerdict.StyleForbids;
			}
			if (AlreadyWorking)
			{
				return UpgradeVerdict.AlreadyWorking;
			}
			if (HeldOnThisGround)
			{
				return UpgradeVerdict.HeldOnThisGround;
			}
			if (HeldByFounder)
			{
				return UpgradeVerdict.HeldByFounder;
			}
			if (Stage < StageNeeded)
			{
				return UpgradeVerdict.StageTooLow;
			}
			// Craft and material gate everything (Addendum 3), and they are asked here rather than
			// discovered halfway through paying, so a work that cannot be improved says so instead
			// of starting and stopping.
			if (!demand.CraftMet)
			{
				return UpgradeVerdict.CraftNotMet;
			}
			if (FreeHands < CrewNeeded)
			{
				return UpgradeVerdict.NotEnoughHands;
			}
			if (!ContentsFit)
			{
				return UpgradeVerdict.WouldSpill;
			}
			if (!CanAfford(StoredWater, Cost, Reserve))
			{
				return UpgradeVerdict.NotEnoughWater;
			}
			if (!demand.MaterialsInHand)
			{
				return UpgradeVerdict.NotEnoughMaterial;
			}
			// Housing is judged by displacement: the roof it carries IS its output, and the
			// question the law asks about a roof is who sleeps under it meanwhile.
			// Tolerance is two questions, not one (Addendum 4): is the lodging good enough -- the
			// rank ladder -- and is it somewhere these particular residents will live at all -- the
			// vocabulary's own Needs check. A tent is tolerable for a settler and is nothing at all
			// for the robot who needs a cradle.
			if (demand.IsHousing
				&& (!CanDisplace(demand.Residents, demand.SpareLodging, demand.OfferedShelter, StandardFor(demand.LuxuryCarried), demand.CurrentShelter)
					|| demand.QuartersRefused))
			{
				return UpgradeVerdict.NoTolerableLodging;
			}
			if (OtherWorkUnderway)
			{
				return UpgradeVerdict.WorksElsewhere;
			}
			// Last, and only for a working building: everything is in hand and the settlement still
			// will not take offline something the city leans on without being told to. Every real
			// refusal above outranks the offer, so the founder is never asked to force a work that
			// something else was going to stop anyway.
			if (!demand.IsHousing
				&& !CoversOutage(StoredWater, Cost, Reserve, OutputLost(demand.SupportPerDay, demand.BuildTicks)))
			{
				return UpgradeVerdict.HeldOffer;
			}
			return UpgradeVerdict.Ready;
		}

		/// <summary>Whether a verdict means the settlement should actually raise scaffolding.
		/// </summary>
		public static bool IsReady(UpgradeVerdict Verdict)
		{
			return Verdict == UpgradeVerdict.Ready;
		}

		/// <summary>
		/// Whether a verdict is the STANDARDS 7b <i>applicable but blocked</i> kind &mdash; a
		/// work that could grow and is not growing, which owes the founder one sentence. The
		/// silent kinds are the ones where nothing has stalled: no chain at all, a design this
		/// city's style never builds, a structure the founder made themselves, work already
		/// under way, and the ready case.
		/// </summary>
		public static bool IsBlocked(UpgradeVerdict Verdict)
		{
			switch (Verdict)
			{
			case UpgradeVerdict.Ready:
			case UpgradeVerdict.NoSuccessor:
			case UpgradeVerdict.StyleForbids:
			case UpgradeVerdict.NotOurWork:
			case UpgradeVerdict.AlreadyWorking:
				return false;
			default:
				return true;
			}
		}

		/// <summary>The settlement's growth stages in the register the founder reads them in.
		/// </summary>
		public static string StageWord(GrowthStage Stage)
		{
			switch (Stage)
			{
			case GrowthStage.Camp:
				return "camp";
			case GrowthStage.Steading:
				return "steading";
			case GrowthStage.Village:
				return "village";
			case GrowthStage.Town:
				return "town";
			default:
				return "city";
			}
		}

	}
}
