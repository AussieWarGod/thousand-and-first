using System;
using System.Diagnostics;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>What one submission produced, and the receipt for it.</summary>
	internal readonly struct KingdomComputeResult<TOut>
	{
		internal readonly KingdomComputeStatus Status;

		/// <summary>The new frozen value. Default unless <see cref="Status"/> is
		/// <see cref="KingdomComputeStatus.Ok"/> — nothing is published on a fault or over budget,
		/// so the caller's state stays byte-identical.</summary>
		internal readonly TOut Value;

		internal readonly KingdomCityFault Fault;

		internal readonly KingdomComputeRefusal Refusal;

		internal readonly KingdomPerfReceipt Receipt;

		internal KingdomComputeResult(
			KingdomComputeStatus status,
			TOut value,
			KingdomCityFault fault,
			KingdomComputeRefusal refusal,
			KingdomPerfReceipt receipt)
		{
			Status = status;
			Value = value;
			Fault = fault;
			Refusal = refusal;
			Receipt = receipt;
		}

		/// <summary>Whether the caller may publish. The one question a call site asks.</summary>
		internal bool Published
		{
			get { return Status == KingdomComputeStatus.Ok; }
		}
	}
}
