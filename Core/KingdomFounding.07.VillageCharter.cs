using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using XRL;
using XRL.Language;
using XRL.Rules;
using XRL.World;
using XRL.World.ZoneBuilders;

namespace ThousandAndFirst
{
	public static partial class KingdomFounding
	{
		/// <summary>
		/// Seals a charter with a living village: standing changes, nothing else does. The
		/// village's own faction keeps every zone, every villager, and every vanilla behaviour it
		/// already had &mdash; this never calls <see cref="ClaimZone"/>, never writes a zone
		/// property, and never touches a villager's allegiance. Only the realm's ledger and the
		/// village's feeling toward it move, through the same <see cref="KingdomSystem.SetStanding"/>
		/// every other faction's standing already moves through, and only upward: a charter the
		/// founder earned cannot make the village think worse of the realm than it already did.
		/// <para>
		/// This is deliberately not a second city. A full charter that lets a chartered village
		/// grow the way a founded one does is a larger claim than this rite makes; see the
		/// founding-paths summary for why that is out of scope this pass rather than shipped
		/// half-safe.
		/// </para>
		/// </summary>
		/// <param name="System">The kingdom system. Must already be founded; callers judge this
		/// via <see cref="KingdomRules.JudgeVillageCharter"/> before reaching here.</param>
		/// <param name="VillageFactionName">The village's own faction name (not display name).
		/// Never reassigned to any creature or zone.</param>
		/// <param name="VillageDisplayName">The village faction's display name, for the
		/// chronicle.</param>
		public static void CharterVillage(KingdomSystem System, string VillageFactionName, string VillageDisplayName)
		{
			if (System == null || !System.Founded || string.IsNullOrEmpty(VillageFactionName))
			{
				return;
			}
			Faction village = Factions.GetIfExists(VillageFactionName);
			if (KingdomFoundingTransaction.HasGlobalReservation() ||
				!KingdomFoundingTransaction.FactionRegistryCoherent(
					VillageFactionName, village) ||
				village.GetIntProperty("Village") != 1 ||
				village.DisplayName != VillageDisplayName)
			{
				return;
			}
			if (System.GetStanding(VillageFactionName) < KingdomRules.VillageCharterSealedStanding)
			{
				System.SetStanding(VillageFactionName, KingdomRules.VillageCharterSealedStanding);
			}
			KingdomFoundingTransaction.RecordChronicleAtomically(System,
				"you asked, and " + KingdomPresentation.Rich(VillageDisplayName) +
				" agreed: their ground stays theirs, and a covenant now stands between them and " +
				KingdomPresentation.Rich(System.KingdomDisplayName), Accomplishment: true);
			string sealFailure;
			KingdomSeal.TryStageSemanticSnapshot("village charter", out sealFailure);
		}
	}
}
