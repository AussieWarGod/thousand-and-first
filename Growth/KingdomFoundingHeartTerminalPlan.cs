namespace ThousandAndFirst
{
	public enum KingdomFoundingHeartTerminalPhase : byte
	{
		None = 0,
		OutputPrepared = 1,
		OutputSettled = 2,
		RemovalAttempting = 3,
		Removed = 4,
		EffectsAttempting = 5,
		EffectsSettled = 6
	}

	public enum KingdomFoundingHeartSinkDisposition : byte
	{
		Pending = 0,
		Attempting = 1,
		Settled = 2,
		Lost = 3
	}

	/// <summary>Authenticated terminal authority mirrored on owner zone and exact final root.</summary>
	public sealed class KingdomFoundingHeartTerminalPlan
	{
		public string TransactionId;
		public string CompletionSeal;
		public string ZoneId;
		public string PredecessorId;
		public string FinalId;
		public string Blueprint;
		public string BuildKey;
		public string PlotId;
		public int X;
		public int Y;
		public KingdomFoundingHeartTerminalPhase Phase;
		public KingdomFoundingHeartSinkDisposition Raising;
		public KingdomFoundingHeartSinkDisposition Heart;

		public KingdomFoundingHeartTerminalPlan Copy()
		{
			return new KingdomFoundingHeartTerminalPlan
			{
				TransactionId = TransactionId, CompletionSeal = CompletionSeal, ZoneId = ZoneId,
				PredecessorId = PredecessorId, FinalId = FinalId, Blueprint = Blueprint,
				BuildKey = BuildKey, PlotId = PlotId, X = X, Y = Y, Phase = Phase,
				Raising = Raising, Heart = Heart
			};
		}
	}
}
