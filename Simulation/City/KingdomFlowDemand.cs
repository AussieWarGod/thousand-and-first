namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// One sink as the solve sees it: which work, where it sits on the brownout ladder, and what it
	/// can actually draw in a day once the narrowest segment between it and a source has had its
	/// say.
	/// <para>
	/// The demand is <b>already bottleneck-limited</b> by
	/// <c>KingdomNetworkGraph.TryBottleneck</c> before it reaches here. That split is deliberate:
	/// the graph knows about pipes and this file knows about arithmetic, and neither has to know
	/// the other's shape.
	/// </para>
	/// </summary>
	internal readonly struct KingdomFlowDemand
	{
		internal readonly int WorkId;

		internal readonly KingdomWorkTier Tier;

		internal readonly int PerDay;

		internal KingdomFlowDemand(int workId, KingdomWorkTier tier, int perDay)
		{
			WorkId = workId;
			Tier = tier;
			PerDay = (perDay > 0) ? perDay : 0;
		}
	}
}
