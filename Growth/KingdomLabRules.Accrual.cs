using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomLabRules
	{
		/// <summary>Advances only paid, staffed work from an absolute semantic boundary.</summary>
		internal static KingdomLabJobAccrual AccrueJob(long LastTick, long TimeTick,
			int RemainingTicks, int CrewEffectiveness, int WearEffectiveness,
			KingdomLabJobPhase Phase)
		{
			return AccrueJob(LastTick, TimeTick, RemainingTicks, CrewEffectiveness,
				WearEffectiveness, Phase, KingdomIdentityAffinityRules.NeutralPercent);
		}

		internal static KingdomLabJobAccrual AccrueJob(long LastTick, long TimeTick,
			int RemainingTicks, int CrewEffectiveness, int WearEffectiveness,
			KingdomLabJobPhase Phase, int IdentityAffinity)
		{
			int remaining = (RemainingTicks > 0) ? RemainingTicks : 0;
			if (Phase != KingdomLabJobPhase.Working)
			{
				return new KingdomLabJobAccrual(LastTick, remaining, 0, Phase);
			}
			if (remaining == 0)
			{
				return new KingdomLabJobAccrual((TimeTick > LastTick) ? TimeTick : LastTick,
					0, 0, KingdomLabJobPhase.Ready);
			}
			if (LastTick <= 0L)
			{
				return new KingdomLabJobAccrual((TimeTick > 0L) ? TimeTick : 0L,
					remaining, 0, KingdomLabJobPhase.Working);
			}
			if (TimeTick <= LastTick)
			{
				// A resumed older semantic pass may observe a boundary behind a newer job stamp.
				// It settles nothing and, critically, cannot rewind the job into pre-commission time.
				return new KingdomLabJobAccrual(LastTick, remaining, 0,
					KingdomLabJobPhase.Working);
			}
			int worked = KingdomProcedureRules.VatWorked(TimeTick - LastTick,
				CrewEffectiveness, WearEffectiveness, IdentityAffinity);
			if (worked <= 0)
			{
				return new KingdomLabJobAccrual(TimeTick, remaining, 0,
					KingdomLabJobPhase.Working);
			}
			if (worked >= remaining)
			{
				return new KingdomLabJobAccrual(TimeTick, 0, remaining,
					KingdomLabJobPhase.Ready);
			}
			return new KingdomLabJobAccrual(TimeTick, remaining - worked, worked,
				KingdomLabJobPhase.Working);
		}

		/// <summary>
		/// Merges one transient debit into the durable job receipt. Uncertain observation is sticky:
		/// once vessel identity or composition cannot be proved, no automatic retry may charge the
		/// apparent outstanding amount.
		/// </summary>
		internal static KingdomLabWaterClaim MergeWaterClaim(int Owed, int Paid, int Lost,
			bool Quarantined, int AttemptSpent, int AttemptLost, bool AttemptExact)
		{
			int owed = (Owed > 0) ? Owed : 0;
			int paid = ClampAdd(Paid, AttemptSpent, owed);
			int lost = SaturatingNonnegativeAdd(Lost, AttemptLost);
			bool quarantined = Quarantined || !AttemptExact;
			int outstanding = owed - paid;
			return new KingdomLabWaterClaim(paid, lost, outstanding, quarantined,
				!quarantined && outstanding == 0);
		}

		/// <summary>
		/// Mutation effect score: listed/native contribution outranks modifier-only contribution,
		/// so adding and removing a lab mutation remains observable without trampling equipment,
		/// tonic, cooking, or external mutation providers.
		/// </summary>
		internal static int MutationPresence(bool ListedMutation, bool LiveMutationPart)
		{
			return ListedMutation ? 2 : (LiveMutationPart ? 1 : 0);
		}

		private static int ClampAdd(int Left, int Right, int Maximum)
		{
			long left = (Left > 0) ? Left : 0;
			long right = (Right > 0) ? Right : 0;
			long sum = left + right;
			return (sum >= Maximum) ? Maximum : (int)sum;
		}

		private static int SaturatingNonnegativeAdd(int Left, int Right)
		{
			long sum = (long)((Left > 0) ? Left : 0) + ((Right > 0) ? Right : 0);
			return (sum > int.MaxValue) ? int.MaxValue : (int)sum;
		}

		/// <summary>Classifies the persisted funding receipt after one synchronous attempt.</summary>
		internal static KingdomLabJobPhase FundingPhase(bool WaterExact, bool BitsExact,
			KingdomKeptSpendPhase KeptPhase)
		{
			return WaterExact && BitsExact && KeptPhase == KingdomKeptSpendPhase.SpentExact
				? KingdomLabJobPhase.Working
				: KingdomLabJobPhase.FundingRecovery;
		}

		/// <summary>A removal can touch the body only after an exact, fully paid receipt.</summary>
		internal static KingdomLabRemovalPhase RemovalFundingPhase(int Owed, int Paid,
			bool Quarantined)
		{
			if (Quarantined)
			{
				return KingdomLabRemovalPhase.Quarantined;
			}
			int owed = (Owed > 0) ? Owed : 0;
			int paid = (Paid > 0) ? Paid : 0;
			return paid >= owed ? KingdomLabRemovalPhase.Paid
				: KingdomLabRemovalPhase.FundingRecovery;
		}

		/// <summary>Classifies the durable read after an exact removal call was started.</summary>
		internal static KingdomLabRemovalPhase RemovalObservation(
			KingdomLabOwnedTargetState Target, bool RemovingStarted)
		{
			switch (Target)
			{
			case KingdomLabOwnedTargetState.Absent:
				return KingdomLabRemovalPhase.Removed;
			case KingdomLabOwnedTargetState.Present:
				return RemovingStarted ? KingdomLabRemovalPhase.RemovalRecovery
					: KingdomLabRemovalPhase.Paid;
			default:
				return KingdomLabRemovalPhase.Quarantined;
			}
		}

		internal static bool IsLiveJob(KingdomLabJobPhase Phase)
		{
			return Phase != KingdomLabJobPhase.Complete && Phase != KingdomLabJobPhase.Cancelled;
		}

		internal static string JobProgressLine(string ProcedureName, KingdomLabJobPhase Phase,
			int RemainingTicks, int StaffDays, bool Staffed, bool WornOut)
		{
			switch (Phase)
			{
			case KingdomLabJobPhase.Funding:
				return Named(ProcedureName) + " is recording its payment.";
			case KingdomLabJobPhase.FundingRecovery:
				return "{{r|Payment was interrupted. Inspect and recover this commission before doing anything else.}}";
			case KingdomLabJobPhase.Ready:
			case KingdomLabJobPhase.Applying:
				return "{{G|" + Named(ProcedureName) + " is ready. Return to the table to finish it.}}";
			case KingdomLabJobPhase.ApplicationRecovery:
				return "{{r|The terminal procedure needs recovery. Its payment and work are preserved; inspect and retry.}}";
			case KingdomLabJobPhase.Complete:
				return "{{G|The commission is complete.}}";
			case KingdomLabJobPhase.Cancelled:
				return "{{K|The commission was cancelled.}}";
			default:
				if (!Staffed)
				{
					return "{{r|No crew is working this commission.}}";
				}
				if (WornOut)
				{
					return "{{r|The hall is too worn to continue this commission.}}";
				}
				int total = KingdomProcedureRules.StaffDayTicks(StaffDays);
				int done = (total > RemainingTicks) ? total - RemainingTicks : 0;
				return Named(ProcedureName) + ": {{C|" + done + "/" + total
					+ "}} staffed work ticks complete.";
			}
		}
	}
}
