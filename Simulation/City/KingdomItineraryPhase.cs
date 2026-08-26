namespace ThousandAndFirst.Simulation.City
{
	/// <summary>Where a carrier is, in the only terms the model has an opinion about.</summary>
	internal enum KingdomItineraryPhase : byte
	{
		/// <summary>Before the first leg departs.</summary>
		Pending = 0,

		/// <summary>Somewhere along a leg.</summary>
		EnRoute = 1,

		/// <summary>Between two legs: the edge handoff.</summary>
		Handoff = 2,

		/// <summary>Past the last leg's arrival.</summary>
		Delivered = 3
	}
}
