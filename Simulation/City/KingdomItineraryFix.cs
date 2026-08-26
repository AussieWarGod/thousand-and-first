namespace ThousandAndFirst.Simulation.City
{
	/// <summary>The one answer the model gives to "where is this carrier".</summary>
	internal readonly struct KingdomItineraryFix
	{
		internal readonly KingdomItineraryPhase Phase;

		/// <summary>Which leg the tick fell in, or -1 before the first and after the last.</summary>
		internal readonly int LegIndex;

		internal readonly string ZoneId;

		internal readonly short X;

		internal readonly short Y;

		/// <summary>Cells walked along this leg: <c>floor(progress x PathLength)</c>.</summary>
		internal readonly int StepsTaken;

		internal KingdomItineraryFix(KingdomItineraryPhase phase, int legIndex, string zoneId, short x, short y, int stepsTaken)
		{
			Phase = phase;
			LegIndex = legIndex;
			ZoneId = zoneId;
			X = x;
			Y = y;
			StepsTaken = stepsTaken;
		}
	}
}
