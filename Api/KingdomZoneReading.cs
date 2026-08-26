using System;

namespace ThousandAndFirst.Api
{
	/// <summary>One claimed zone as the model last read it.</summary>
	public readonly struct KingdomZoneReading
	{
		/// <summary>The engine's zone id.</summary>
		public readonly string ZoneId;

		/// <summary>Water held on this ground.</summary>
		public readonly KingdomStockReading Water;

		/// <summary>Food held on this ground.</summary>
		public readonly KingdomStockReading Food;

		/// <summary>Materials held on this ground.</summary>
		public readonly KingdomStockReading Materials;

		/// <summary>Roofs counted here.</summary>
		public readonly int Roofs;

		/// <summary>Crewed defence here.</summary>
		public readonly int Defence;

		/// <summary>Water the model owes this ground and has not been able to land on real
		/// vessels yet. Signed: negative is a draw still to take (Addendum 12(d)).</summary>
		public readonly int OwedWater;

		/// <summary>Food owed, on the same terms.</summary>
		public readonly int OwedFood;

		/// <summary>Materials owed, on the same terms.</summary>
		public readonly int OwedMaterials;

		/// <summary>When the ground was last actually looked at.</summary>
		public readonly long LastReadTick;

		/// <summary>Builds a zone reading.</summary>
		public KingdomZoneReading(string ZoneId, KingdomStockReading Water, KingdomStockReading Food,
			KingdomStockReading Materials, int Roofs, int Defence, int OwedWater, int OwedFood,
			int OwedMaterials, long LastReadTick)
		{
			this.ZoneId = ZoneId;
			this.Water = Water;
			this.Food = Food;
			this.Materials = Materials;
			this.Roofs = Roofs;
			this.Defence = Defence;
			this.OwedWater = OwedWater;
			this.OwedFood = OwedFood;
			this.OwedMaterials = OwedMaterials;
			this.LastReadTick = LastReadTick;
		}
	}
}
