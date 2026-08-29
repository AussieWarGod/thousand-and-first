using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Frozen source, destination, labour, and physical rows for one whole-lot move.</summary>
	public sealed class KingdomRelocationMove
	{
		public string RootId;
		public string PlotId;
		public string BuildKey;
		public string DisplayName;
		public KingdomRelocationRect Source;
		public KingdomRelocationRect Destination;
		public KingdomRelocationRect Footprint;
		public int Roof;
		public long StartedTick;
		public long LastTick;
		public long RequiredTicks;
		public long RemainingTicks;
		public long CompletionTick;
		public KingdomRelocationMovePhase Phase;
		public string FrameId;
		public string[] StakeIds = new string[0];
		public KingdomRelocationArchitecture Architecture;
		public List<KingdomRelocationRow> Rows = new List<KingdomRelocationRow>();
		public List<KingdomRelocationClearRow> Clearance =
			new List<KingdomRelocationClearRow>();
	}
}
