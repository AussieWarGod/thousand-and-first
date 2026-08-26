using System;

namespace ThousandAndFirst.Api
{
	/// <summary>Adds bounded network topology. Host solve owns capacity arithmetic and brownout
	/// ordering; extension supplies only frozen nodes and edges.</summary>
	public interface INetworkKind : IKingdomExtension
	{
		/// <summary>Returns proposed networks, or null. Side effects are forbidden.</summary>
		KingdomNetworkPlan[] Networks(KingdomCityReading City,
			KingdomBehaviourReading Model, IKingdomDraws Draws);
	}
}
