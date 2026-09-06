namespace ThousandAndFirst
{
	/// <summary>Exact resident carriers retained across the cut before the realm journal is
	/// published. Contains no engine object and cannot itself authorize a physical departure.</summary>
	internal sealed class KingdomSubsidenceDepartureIdentity
	{
		internal readonly int ResidentId;
		internal readonly string BodyObjectId, ZoneId;
		internal readonly long PreparedTick;

		internal KingdomSubsidenceDepartureIdentity(int residentId, string bodyObjectId,
			string zoneId, long preparedTick)
		{
			ResidentId = residentId; BodyObjectId = bodyObjectId;
			ZoneId = zoneId; PreparedTick = preparedTick;
		}

		internal bool Matches(KingdomResidentDepartureOperation departure)
		{
			return departure != null && ResidentId == departure.ResidentId
				&& BodyObjectId == departure.BodyObjectId && ZoneId == departure.ZoneId
				&& PreparedTick == departure.PreparedTick;
		}
	}
}
