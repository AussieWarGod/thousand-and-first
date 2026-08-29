namespace ThousandAndFirst
{
	public enum KingdomConstructionInputKind : byte
	{
		Invalid = 0,
		Water = 1,
		Material = 2,
		Bit = 3,
		Exotic = 4
	}

	public enum KingdomConstructionInputTopology : byte
	{
		Invalid = 0,
		ContainerInventory = 1,
		LooseCell = 2,
		LiquidVessel = 3,
		CarrierInventory = 4,
		LandingEscrow = 5,
		Consumed = 6,
		Released = 7,
		CompensationEscrow = 8,
		Returned = 9
	}

	public enum KingdomConstructionInputCargoShape : byte
	{
		Invalid = 0,
		OpaqueObjectManifest = 1
	}

	public enum KingdomConstructionInputTxPhase : byte
	{
		Invalid = 0,
		ReservationPrepared = 1,
		Reserved = 2,
		SourcePending = 3,
		Routing = 4,
		LandedAwaitingOwner = 5,
		DebitPending = 6,
		Closing = 7,
		Committed = 8,
		RollbackPending = 9,
		RolledBack = 10,
		CompensationPending = 11,
		Compensated = 12,
		Quarantined = 13,
		CancellationPending = 14,
		Cancelled = 15
	}

	public enum KingdomConstructionInputSourcePhase : byte
	{
		Invalid = 0,
		Reserved = 1,
		SplitIntent = 2,
		SplitProved = 3,
		TransferIntent = 4,
		Debited = 5,
		RestoreIntent = 6,
		Restored = 7,
		Spent = 8,
		CompensationIntent = 9,
		Compensated = 10,
		Quarantined = 11
	}

	public enum KingdomConstructionInputCargoPhase : byte
	{
		Invalid = 0,
		Planned = 1,
		CreateIntent = 2,
		AtSource = 3,
		PickupIntent = 4,
		InFlight = 5,
		Landed = 6,
		DebitIntent = 7,
		Spent = 8,
		ReleaseIntent = 9,
		Released = 10,
		CompensationIntent = 11,
		Compensated = 12,
		Quarantined = 13
	}

	public enum KingdomConstructionInputDecision : byte
	{
		Invalid = 0,
		Apply = 1,
		Acknowledge = 2,
		Quarantine = 3,
		WaitPaused = 4,
		NoAction = 5
	}

	public enum KingdomConstructionInputFault : byte
	{
		None = 0,
		Null = 1,
		Schema = 2,
		Bounds = 3,
		Identity = 4,
		Owner = 5,
		Phase = 6,
		Amount = 7,
		Duplicate = 8,
		Overlap = 9,
		Claim = 10,
		Digest = 11,
		Child = 12,
		Route = 13,
		Codec = 14,
		Revision = 15,
		Transition = 16,
		Conservation = 17,
		FutureSchema = 18,
		Pause = 19,
		Witness = 20,
		CrossBinding = 21
	}

	public sealed class KingdomConstructionInputConservation
	{
		public readonly int Expected;
		public readonly int AtSource;
		public readonly int InFlight;
		public readonly int Landed;
		public readonly int Spent;
		public readonly int Compensating;
		public readonly int Quarantined;
		public readonly int ProvedLost;

		internal KingdomConstructionInputConservation(int Expected, int AtSource, int InFlight,
			int Landed, int Spent, int Compensating, int Quarantined, int ProvedLost)
		{
			this.Expected = Expected;
			this.AtSource = AtSource;
			this.InFlight = InFlight;
			this.Landed = Landed;
			this.Spent = Spent;
			this.Compensating = Compensating;
			this.Quarantined = Quarantined;
			this.ProvedLost = ProvedLost;
		}
	}
}
