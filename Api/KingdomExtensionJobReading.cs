using System;

namespace ThousandAndFirst.Api
{
	/// <summary>Frozen reading of one durable extension job.</summary>
	public readonly struct KingdomExtensionJobReading
	{
		/// <summary>Owner-qualified receipt key.</summary>
		public readonly string Key;
		/// <summary>Owner-qualified carrier key.</summary>
		public readonly string CarrierKey;
		/// <summary>Exact retained Qud carrier blueprint.</summary>
		public readonly string CarrierBlueprint;
		/// <summary>Owner-qualified cargo resource key.</summary>
		public readonly string CargoResourceKey;
		/// <summary>Units reserved by the job.</summary>
		public readonly int CargoAmount;
		/// <summary>Opening tick.</summary>
		public readonly long StartTick;
		/// <summary>Computed end tick.</summary>
		public readonly long DueTick;
		/// <summary>Current terminal/open state.</summary>
		public readonly KingdomExtensionJobStatus Status;

		/// <summary>Builds a frozen reading.</summary>
		public KingdomExtensionJobReading(string Key, string CarrierKey, string CarrierBlueprint,
			string CargoResourceKey, int CargoAmount, long StartTick, long DueTick,
			KingdomExtensionJobStatus Status)
		{
			this.Key = Key ?? "";
			this.CarrierKey = CarrierKey ?? "";
			this.CarrierBlueprint = CarrierBlueprint ?? "";
			this.CargoResourceKey = CargoResourceKey ?? "";
			this.CargoAmount = CargoAmount;
			this.StartTick = StartTick;
			this.DueTick = DueTick;
			this.Status = Status;
		}
	}
}
