using XRL.World;

namespace ThousandAndFirst.Api
{
	/// <summary>The engine-coupled adapter for the frozen identity extension boundary.</summary>
	internal static class KingdomIdentity
	{
		/// <summary>
		/// Reads Qud's exact open culture/species/genotype accessors plus the resident's current
		/// kingdom creed. Null yields an empty bounded reading. No engine object survives the call.
		/// </summary>
		internal static KingdomIdentityReading Read(GameObject Resident)
		{
			if (Resident == null)
			{
				return new KingdomIdentityReading(null, null, null, null);
			}
			return new KingdomIdentityReading(
				Resident.GetCulture(),
				Resident.GetSpecies(),
				Resident.GetStringProperty(KingdomCreed.CreedProperty),
				Resident.GetGenotype());
		}
	}
}
