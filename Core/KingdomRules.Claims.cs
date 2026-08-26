namespace ThousandAndFirst
{
	public static partial class KingdomRules
	{
		/// <summary>
		/// What the founding rite should do about ground another faction already answers to,
		/// before any water is measured. <see cref="Unclaimed"/> is the overwhelming common case
		/// (empty wilderness, or the kingdom's own ground) and changes nothing about how founding
		/// already worked. The other two verdicts exist because <c>ClaimZone</c> used to write the
		/// kingdom's faction over whatever a zone already had unconditionally &mdash; a live
		/// hazard the ecosystem-compat audit found &mdash; and a village is not a lair: it is
		/// someone's home, asked into a covenant rather than annexed.
		/// </summary>
		public enum GroundClaimVerdict
		{
			Unclaimed,
			ForeignVillage,
			ForeignOther
		}

		/// <param name="ZoneFaction">The zone's <c>faction</c> property.</param>
		/// <param name="KingdomFactionName">The realm's own faction name, or null/empty if
		/// unfounded.</param>
		/// <param name="ZoneFactionIsVillage">Whether <paramref name="ZoneFaction"/> names a
		/// vanilla village faction (<c>Faction.GetIntProperty("Village") == 1</c>), read by the
		/// caller so this stays engine-free.</param>
		public static GroundClaimVerdict JudgeGroundFaction(string ZoneFaction, string KingdomFactionName, bool ZoneFactionIsVillage)
		{
			if (!GroundIsForeignFaction(ZoneFaction, KingdomFactionName))
			{
				return GroundClaimVerdict.Unclaimed;
			}
			return ZoneFactionIsVillage ? GroundClaimVerdict.ForeignVillage : GroundClaimVerdict.ForeignOther;
		}

		/// <summary>
		/// The reputation floor a village must already hold the founder at before the charter
		/// rite will ask it anything. 250 mirrors <c>XRL.Rules.RuleSettings.REPUTATION_LIKED</c>
		/// &mdash; liked, not yet loved: a real bar, reachable through ordinary play, restated here
		/// as a plain number so this file stays free of engine references.
		/// </summary>
		public const int VillageCharterReputationThreshold = 250;

		/// <summary>
		/// The standing a sealed charter raises a village to (see
		/// <c>KingdomFounding.CharterVillage</c>), and the floor <see cref="JudgeVillageCharter"/>
		/// reads as "already chartered" so asking again cannot spend water for nothing. 600
		/// mirrors <c>XRL.Rules.RuleSettings.REPUTATION_LOVED</c>.
		/// </summary>
		public const int VillageCharterSealedStanding = 600;

		/// <summary>Why the village charter rite may not proceed, or
		/// <see cref="Allowed"/>.</summary>
		public enum VillageCharterVerdict
		{
			Allowed,
			RealmNotFounded,
			AlreadyChartered,
			OpinionTooLow
		}

		/// <summary>
		/// Judges whether a village will hear the charter rite: it asks, so a realm has to exist
		/// to ask in the name of; a village already standing at
		/// <see cref="VillageCharterSealedStanding"/> has nothing left to ask for; and otherwise
		/// the village has to already trust the founder personally
		/// (<see cref="VillageCharterReputationThreshold"/>) &mdash; opinion of the founder, not of
		/// the realm, because the founder is who is standing there with the basin.
		/// </summary>
		/// <param name="AlreadyChartered">Whether the realm's standing with this village is
		/// already at or above <see cref="VillageCharterSealedStanding"/>.</param>
		public static VillageCharterVerdict JudgeVillageCharter(bool Founded, bool AlreadyChartered, int PlayerReputation)
		{
			if (!Founded)
			{
				return VillageCharterVerdict.RealmNotFounded;
			}
			if (AlreadyChartered)
			{
				return VillageCharterVerdict.AlreadyChartered;
			}
			if (PlayerReputation < VillageCharterReputationThreshold)
			{
				return VillageCharterVerdict.OpinionTooLow;
			}
			return VillageCharterVerdict.Allowed;
		}

		/// <summary>What the founder is told when the charter rite will not proceed. Written as
		/// the water-keepers would say it, not as a rule; empty for <see cref="VillageCharterVerdict.Allowed"/>.</summary>
		/// <param name="Verdict">The refusal.</param>
		/// <param name="VillageDisplayName">The village faction's display name.</param>
		public static string VillageCharterRefusal(VillageCharterVerdict Verdict, string VillageDisplayName)
		{
			string village = string.IsNullOrEmpty(VillageDisplayName) ? "this village" : ("{{C|" + VillageDisplayName + "}}");
			switch (Verdict)
			{
			case VillageCharterVerdict.RealmNotFounded:
				return "There is no realm yet to speak this covenant in the name of. Found your own ground first, then come back and ask.";
			case VillageCharterVerdict.AlreadyChartered:
				return "This covenant is already sealed between " + village + " and the realm. Nothing more is asked, or owed.";
			case VillageCharterVerdict.OpinionTooLow:
				return "The water-right with " + village + " is not yet strong enough for this to be asked. Earn it, and ask again.";
			default:
				return "";
			}
		}

		public static bool TryParseFactionAmount(string Parameter, out string FactionName, out int Amount)
		{
			FactionName = null;
			Amount = 0;
			if (string.IsNullOrEmpty(Parameter))
			{
				return false;
			}
			int num = Parameter.LastIndexOf(':');
			if (num <= 0 || num >= Parameter.Length - 1)
			{
				return false;
			}
			if (!int.TryParse(Parameter.Substring(num + 1).Trim(), out Amount))
			{
				return false;
			}
			FactionName = Parameter.Substring(0, num).Trim();
			return FactionName.Length > 0;
		}
	}
}
