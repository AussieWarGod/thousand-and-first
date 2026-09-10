using System;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public partial class KingdomSurvey
	{
		// UI calls have no reconciliation pass. Bind one survey for the complete local
		// operation; nested calls reuse it and never replace another zone's authority.
		internal static bool TryBindLocalOperation(Zone zone, KingdomSystem system,
			out PassScope scope, out string failure)
		{
			scope = null;
			failure = "Local stock can only be used on the current active ground.";
			if (zone == null || !ReferenceEquals(The.ZoneManager?.ActiveZone, zone))
				return false;
			KingdomSurvey survey = ActiveFor(zone);
			if (survey == null && HasBoundPass)
			{
				failure = "Another settlement pass already owns the local stock survey.";
				return false;
			}
			if (survey == null) survey = Take(zone, system);
			if (!ReferenceEquals(The.ZoneManager?.ActiveZone, zone)
				|| (HasBoundPass && !ReferenceEquals(ActiveFor(zone), survey))) return false;
			scope = survey.BindPass();
			failure = null;
			return true;
		}
	}
}
