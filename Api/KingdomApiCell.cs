using System;

namespace ThousandAndFirst.Api
{
	/// <summary>
	/// One exact in-zone cell exactly as an extension reports it. This is the only cell shape that
	/// crosses the <c>ThousandAndFirst.Api</c> seam: the host bounds-checks every cell against the
	/// active zone and translates it to its own geometry, so an internal layout change can never
	/// silently break a provider compiled against this contract.
	/// </summary>
	public readonly struct KingdomApiCell : IEquatable<KingdomApiCell>
	{
		public readonly int X;
		public readonly int Y;

		public KingdomApiCell(int X, int Y)
		{
			this.X = X;
			this.Y = Y;
		}

		public bool Equals(KingdomApiCell Other)
		{
			return X == Other.X && Y == Other.Y;
		}

		public override bool Equals(object Other)
		{
			return Other is KingdomApiCell cell && Equals(cell);
		}

		public override int GetHashCode()
		{
			return unchecked((X * 397) ^ Y);
		}
	}
}
