using System;

namespace ThousandAndFirst
{
	public static partial class KingdomFounding
	{
		/// <summary>Compatibility stub for a pre-transaction entry point. Direct publication cannot
		/// prove paid water, exact ground, or an owned before/after standing transition, so it is
		/// deliberately inert. Use the founder-basin village-charter transaction.</summary>
		[Obsolete("Direct village charter publication is unsupported; use the founder-basin transaction.")]
		public static void CharterVillage(KingdomSystem System, string VillageFactionName, string VillageDisplayName)
		{
			KingdomLog.Log("founding: refused unsupported direct village charter publication");
		}
	}
}
