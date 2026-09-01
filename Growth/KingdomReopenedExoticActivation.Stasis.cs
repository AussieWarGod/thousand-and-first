using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	internal static partial class KingdomReopenedExoticActivation
	{
		internal static bool StasisVaultEligible(KingdomSystem System,
			IList<string> Roster)
		{
			if (System == null || !System.Founded || Roster == null
				|| !KingdomZoningRules.Knows(Roster, "node:chimerism")
				|| KingdomStasisVaultRules.CurrentReceiptVersion <= 0) return false;
			Zone zone = The.ZoneManager?.ActiveZone;
			if (zone == null || System.ClaimedZones == null
				|| !System.ClaimedZones.Contains(zone.ZoneID)) return false;
			List<GameObject> objects = zone.GetObjects();
			for (int i = 0; i < objects.Count; i++)
			{
				GameObject work = objects[i];
				if (!KingdomUpgrade.IsFunctionallyBuilt(work))
					continue;
				if (work.GetPart<r_KingdomGraftingHall>() != null
					|| work.GetPart<r_KingdomChimericTheatre>() != null) return true;
			}
			return false;
		}
	}
}
