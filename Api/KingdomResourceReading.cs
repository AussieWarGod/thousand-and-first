using System;

namespace ThousandAndFirst.Api
{
	/// <summary>Frozen durable reading of one extension-owned civic good.</summary>
	public readonly struct KingdomResourceReading
	{
		/// <summary>Owner-qualified stable key.</summary>
		public readonly string Key;
		/// <summary>Founder-facing singular unit.</summary>
		public readonly string Unit;
		/// <summary>Dedicated-container property, or empty.</summary>
		public readonly string ContainerProperty;
		/// <summary>Owner-qualified network key, or empty.</summary>
		public readonly string NetworkKey;
		/// <summary>Exact Qud liquid id, or empty.</summary>
		public readonly string LiquidId;
		/// <summary>Units held by the civic share.</summary>
		public readonly long Level;
		/// <summary>Units the civic share can hold.</summary>
		public readonly long Capacity;

		/// <summary>Builds a frozen reading. Runtime callers receive host-normalized values.</summary>
		public KingdomResourceReading(string Key, string Unit, string ContainerProperty,
			string NetworkKey, string LiquidId, long Level, long Capacity)
		{
			this.Key = Key ?? "";
			this.Unit = Unit ?? "";
			this.ContainerProperty = ContainerProperty ?? "";
			this.NetworkKey = NetworkKey ?? "";
			this.LiquidId = LiquidId ?? "";
			this.Level = Level;
			this.Capacity = Capacity;
		}

		/// <summary>Room left, floored at zero.</summary>
		public long Room { get { return Capacity > Level ? Capacity - Level : 0L; } }
	}
}
