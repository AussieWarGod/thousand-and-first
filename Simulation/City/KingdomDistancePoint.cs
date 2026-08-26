using System;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>One live-ground endpoint offered to the sparse distance cache. The stable id is
	/// the key retained by the matrix; coordinates are used only while this zone is rendered.</summary>
	internal readonly struct KingdomDistancePoint
	{
		internal readonly int Id;

		internal readonly short X;

		internal readonly short Y;

		internal KingdomDistancePoint(int id, int x, int y)
		{
			Id = id;
			X = (short)x;
			Y = (short)y;
		}
	}
}
