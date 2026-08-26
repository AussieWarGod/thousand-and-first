using System;

namespace ThousandAndFirst.Api
{
	/// <summary>One stock and the ceiling it fills toward, as the city book holds it.</summary>
	public readonly struct KingdomStockReading
	{
		/// <summary>What the civic share holds. Player-carried and undedicated stock is not in
		/// here and never will be (LIVING-CITY-ARCHITECTURE &sect;1.2(a)): the model speaks only
		/// for what the founder designated, which is what keeps the protection law simple.</summary>
		public readonly long Level;

		/// <summary>What the dedicated vessels could hold.</summary>
		public readonly long Capacity;

		/// <summary>Builds a stock reading.</summary>
		public KingdomStockReading(long Level, long Capacity)
		{
			this.Level = Level;
			this.Capacity = Capacity;
		}

		/// <summary>Room left, floored at zero.</summary>
		public long Room
		{
			get { return (Capacity > Level) ? (Capacity - Level) : 0L; }
		}
	}
}
