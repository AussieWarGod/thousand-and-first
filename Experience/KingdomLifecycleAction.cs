namespace ThousandAndFirst
{

	public enum KingdomLifecycleAction : byte
	{
		None = 0,
		Passages = 1,
		Spawn = 2,
		Depart = 3,
		OfferWater = 4,
		Lodge = 5,
		RaidWarning = 6,
		RaidRewarning = 7,
		RaidTribute = 8,
		RaidTalkDown = 9,
		RaidAttack = 10,
		RaidCancel = 11,
		PetitionOffer = 12,
		PetitionAccept = 13,
		PetitionDecline = 14,
		PetitionResolve = 15,
		PetitionExpire = 16,
		RaidFight = 17,
		RaidFortify = 18,
		RaidResolve = 19,
		RaidDeliverDemand = 20,
		RaidAcknowledgeDemand = 21,
		RaidLoseChannel = 22,
		RaidDeadline = 23,
		RaidFortifyOrder = 24,
		RaidFortifyFailure = 25,
		RaidRecoveryAccept = 26,
		RaidRecoveryReady = 27,
		RaidRecoveryResolve = 28,
		RaidRecoveryDecline = 29
	}
}
