using System;
using System.Globalization;

namespace ThousandAndFirst
{
	public static partial class KingdomPetitionRules
	{
		private static KingdomLifecycleAction ActionOf(PetitionLifecycle State)
		{
			switch (State)
			{
			case PetitionLifecycle.Offered: return KingdomLifecycleAction.PetitionOffer;
			case PetitionLifecycle.Accepted: return KingdomLifecycleAction.PetitionAccept;
			case PetitionLifecycle.Declined: return KingdomLifecycleAction.PetitionDecline;
			case PetitionLifecycle.Resolved: return KingdomLifecycleAction.PetitionResolve;
			case PetitionLifecycle.Expired: return KingdomLifecycleAction.PetitionExpire;
			default: return KingdomLifecycleAction.None;
			}
		}
	}
}
