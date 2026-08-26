using System;

namespace ThousandAndFirst.Api
{
	/// <summary>Adds durable, timed jobs. The host validates legs, reserves cargo, computes duration
	/// from the paired carrier, and commits completion exactly once.</summary>
	public interface IJobKind : IKingdomExtension
	{
		/// <summary>Returns proposals opening exactly at <paramref name="City"/>'s processed tick, or
		/// null. A key is idempotent while its bounded receipt is retained and must never identify a
		/// later logical job after that receipt retires.</summary>
		KingdomJobPlan[] Jobs(KingdomCityReading City,
			KingdomBehaviourReading Model, IKingdomDraws Draws);
	}
}
