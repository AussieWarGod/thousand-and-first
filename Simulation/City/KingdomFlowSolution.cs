namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// What one span did to one network. Every figure is in the network's own unit &mdash; vanilla
	/// charge for the power families, drams for a liquid line &mdash; and the whole of it satisfies
	/// one identity, which is what makes it checkable rather than merely plausible:
	/// <code>
	/// Generated + Discharged == Delivered + Charged + Spilled
	/// </code>
	/// </summary>
	internal readonly struct KingdomFlowSolution
	{
		/// <summary>What the sources made, throttled by their own segments.</summary>
		internal readonly long Generated;

		/// <summary>What the sinks asked for, throttled by theirs.</summary>
		internal readonly long Demanded;

		/// <summary>What actually reached a sink. Below <see cref="Demanded"/> exactly when
		/// something stopped.</summary>
		internal readonly long Delivered;

		/// <summary>What went into the stores.</summary>
		internal readonly long Charged;

		/// <summary>What came back out of them.</summary>
		internal readonly long Discharged;

		/// <summary>What was made with nowhere to put it. Loss, never a queue &mdash; the same
		/// ruling <c>KingdomProductionRules</c> makes about a harvest over a full granary.</summary>
		internal readonly long Spilled;

		/// <summary>How far short the span ran once the stores had given everything they could.
		/// The brownout's size, and the figure the stop loop is asked to cover.</summary>
		internal readonly long Shortfall;

		/// <summary>How many sinks went quiet, counted off the front of the brownout order.</summary>
		internal readonly int Stopped;

		internal KingdomFlowSolution(long generated, long demanded, long delivered, long charged, long discharged, long spilled, long shortfall, int stopped)
		{
			Generated = generated;
			Demanded = demanded;
			Delivered = delivered;
			Charged = charged;
			Discharged = discharged;
			Spilled = spilled;
			Shortfall = shortfall;
			Stopped = stopped;
		}

		/// <summary>Whether anything went quiet. The predicate the happening is generated from.</summary>
		internal bool Brownout
		{
			get { return Stopped > 0; }
		}

		internal static KingdomFlowSolution None
		{
			get { return new KingdomFlowSolution(0L, 0L, 0L, 0L, 0L, 0L, 0L, 0); }
		}
	}
}
