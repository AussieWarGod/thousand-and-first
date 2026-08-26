using System;

namespace ThousandAndFirst.Api
{
	/// <summary>Adds civic resource kinds. Called through the executor over frozen city and sidecar
	/// readings; at most <c>KingdomApiRules.MaxResourceKindsPerOwner</c> valid rows are retained.</summary>
	public interface IResourceKind : IKingdomExtension
	{
		/// <summary>Returns proposed kinds, or null. Side effects are forbidden.</summary>
		KingdomResourceDefinition[] Resources(KingdomCityReading City,
			KingdomBehaviourReading Model, IKingdomDraws Draws);
	}
}
