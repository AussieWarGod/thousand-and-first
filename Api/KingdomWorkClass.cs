using System;

namespace ThousandAndFirst.Api
{
	/// <summary>What a work is, for the one slot of run-state its row carries. Mirrors the model's
	/// own vocabulary; ordinals are stable API.</summary>
	public enum KingdomWorkClass : byte
	{
		/// <summary>Anything with no run-state of its own.</summary>
		Other = 0,

		/// <summary>A field or row that ripens.</summary>
		Growing = 1,

		/// <summary>A store.</summary>
		Store = 2,

		/// <summary>Something that makes a good.</summary>
		Producer = 3,

		/// <summary>Something that turns one good into a better one.</summary>
		Refiner = 4,

		/// <summary>Something that carries charge.</summary>
		Power = 5,

		/// <summary>A plot or scaffold actively being raised.</summary>
		Construction = 6
	}
}
