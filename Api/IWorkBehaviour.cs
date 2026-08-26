using System;

namespace ThousandAndFirst.Api
{
	/// <summary>Adds run-state behavior to existing work rows. Results are atomic and may only
	/// change resources owned by the same extension.</summary>
	public interface IWorkBehaviour : IKingdomExtension
	{
		/// <summary>Returns advances due at the city's processed tick, or null. Side effects are
		/// forbidden; the host owns durable state and physical debts.</summary>
		KingdomWorkAdvance[] Advance(KingdomCityReading City,
			KingdomBehaviourReading Model, IKingdomDraws Draws);
	}
}
