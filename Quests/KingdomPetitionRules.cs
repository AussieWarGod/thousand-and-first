using System;
using System.Globalization;

namespace ThousandAndFirst
{
	/// <summary>Engine-free petition transition, snapshot, clock, and completion rules.</summary>
	public static partial class KingdomPetitionRules
	{
		public const string ActiveClock = "petition-active";
		public const string PausedClock = "petition-paused";
		public const string OptionClosedClock = "petition-option-closed";

		public const long TicksPerYear = 438000L;
		public const int MonthsPerYear = 13;

		private static readonly long[] MonthStarts = new long[MonthsPerYear - 1]
		{
			36001L, 72001L, 108001L, 144001L, 180001L, 216001L,
			222001L, 258001L, 294001L, 330001L, 366001L, 402001L
		};

		public static PetitionLifecycle LifecycleOf(KingdomLifecycleAction Action)
		{
			switch (Action)
			{
			case KingdomLifecycleAction.PetitionOffer: return PetitionLifecycle.Offered;
			case KingdomLifecycleAction.PetitionAccept: return PetitionLifecycle.Accepted;
			case KingdomLifecycleAction.PetitionDecline: return PetitionLifecycle.Declined;
			case KingdomLifecycleAction.PetitionResolve: return PetitionLifecycle.Resolved;
			case KingdomLifecycleAction.PetitionExpire: return PetitionLifecycle.Expired;
			default: return PetitionLifecycle.None;
			}
		}

		public static PetitionLifecycle LifecycleOf(KingdomLifecycleOperation Operation)
		{
			return Operation != null && Operation.Lane == KingdomLifecycleLane.Petition
				? LifecycleOf(Operation.Action) : PetitionLifecycle.None;
		}

		public static bool IsActive(PetitionLifecycle State)
		{
			return State == PetitionLifecycle.Offered || State == PetitionLifecycle.Accepted;
		}

		public static bool IsTerminal(PetitionLifecycle State)
		{
			return State == PetitionLifecycle.Declined || State == PetitionLifecycle.Resolved
				|| State == PetitionLifecycle.Expired;
		}

		/// <summary>Exact domain graph. Repeated Accept is reserved for pause/resume restamping.</summary>
		public static bool CanFollow(KingdomLifecycleAction Prior, KingdomLifecycleAction Next)
		{
			if (Prior == KingdomLifecycleAction.None)
				return Next == KingdomLifecycleAction.PetitionOffer;
			switch (Prior)
			{
			case KingdomLifecycleAction.PetitionOffer:
				return Next == KingdomLifecycleAction.PetitionAccept
					|| Next == KingdomLifecycleAction.PetitionDecline
					|| Next == KingdomLifecycleAction.PetitionExpire;
			case KingdomLifecycleAction.PetitionAccept:
				return Next == KingdomLifecycleAction.PetitionAccept
					|| Next == KingdomLifecycleAction.PetitionResolve
					|| Next == KingdomLifecycleAction.PetitionExpire;
			case KingdomLifecycleAction.PetitionDecline:
			case KingdomLifecycleAction.PetitionResolve:
			case KingdomLifecycleAction.PetitionExpire:
				return Next == KingdomLifecycleAction.PetitionOffer;
			default:
				return false;
			}
		}

		/// <summary>Compatibility graph for callers which still reason in projection values.</summary>
		public static bool CanTransition(PetitionLifecycle From, PetitionLifecycle To)
		{
			KingdomLifecycleAction prior = ActionOf(From);
			KingdomLifecycleAction next = ActionOf(To);
			return next != KingdomLifecycleAction.None && CanFollow(prior, next)
				&& !(From == PetitionLifecycle.Accepted && To == PetitionLifecycle.Accepted);
		}

	}
}
