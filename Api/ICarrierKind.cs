using System;

namespace ThousandAndFirst.Api
{
	/// <summary>Adds carrier kinds used by <see cref="IJobKind"/> plans.</summary>
	public interface ICarrierKind : IKingdomExtension
	{
		/// <summary>Returns proposed carrier kinds, or null. Side effects are forbidden.</summary>
		KingdomCarrierDefinition[] Carriers(KingdomCityReading City,
			KingdomBehaviourReading Model, IKingdomDraws Draws);
	}
}
