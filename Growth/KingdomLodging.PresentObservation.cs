using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomLodging
	{
		// Preserve the exact v1 hash for unchanged local households: a saved lodging intent
		// must not fault solely because the executable gained absent-owner reservations.
		private static string PresentOccupantObservation(GameObject Body, int Hostility,
			KingdomLodgingRules.Closeness Quarters, bool Conflict)
		{
			QolProfile profile = KingdomQol.ProfileOf(Body);
			var needs = new List<string>(profile.Needs);
			var prefers = new List<string>(profile.Prefers);
			var refuses = new List<string>(profile.Refuses);
			List<string> selfTags = SelfTagsOf(profile);
			needs.Sort(StringComparer.Ordinal); prefers.Sort(StringComparer.Ordinal);
			refuses.Sort(StringComparer.Ordinal); selfTags.Sort(StringComparer.Ordinal);
			return ArrivalObservationHash(writer =>
			{
				WriteObservationString(writer, Body.IDIfAssigned);
				WriteObservationString(writer, Body.Blueprint);
				WriteObservationString(writer, Body.GetStringProperty(KingdomCreed.CreedProperty));
				WriteObservationList(writer, needs); WriteObservationList(writer, prefers);
				WriteObservationList(writer, refuses); WriteObservationList(writer, selfTags);
				writer.Write(Hostility); writer.Write((int)Quarters); writer.Write(Conflict);
			});
		}
	}
}
