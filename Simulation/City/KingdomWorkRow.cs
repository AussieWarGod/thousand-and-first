using System;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>One standing work. LIVING-CITY-ARCHITECTURE &sect;1.2(c); sixty-four bytes at
	/// &sect;0.0(c).</summary>
	internal readonly struct KingdomWorkRow
	{
		internal readonly int WorkId;

		internal readonly string ZoneId;

		internal readonly short AnchorX;

		internal readonly short AnchorY;

		internal readonly string DesignKey;

		/// <summary>The wear percent KingdomWear already owns.</summary>
		internal readonly int ConditionPercent;

		internal readonly int CrewAssigned;

		internal readonly long RanThroughTick;

		internal readonly KingdomWorkRunState RunState;

		internal KingdomWorkRow(
			int workId,
			string zoneId,
			short anchorX,
			short anchorY,
			string designKey,
			int conditionPercent,
			int crewAssigned,
			long ranThroughTick,
			KingdomWorkRunState runState)
		{
			WorkId = workId;
			ZoneId = zoneId;
			AnchorX = anchorX;
			AnchorY = anchorY;
			DesignKey = designKey;
			ConditionPercent = conditionPercent;
			CrewAssigned = crewAssigned;
			RanThroughTick = ranThroughTick;
			RunState = runState;
		}

		internal KingdomWorkRow WithRunState(KingdomWorkRunState runState, long ranThroughTick)
		{
			return new KingdomWorkRow(WorkId, ZoneId, AnchorX, AnchorY, DesignKey, ConditionPercent, CrewAssigned, ranThroughTick, runState);
		}
	}
}
