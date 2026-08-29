using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomGuestRules
	{
		/// <summary>Whether lodging a notable guest right now succeeds, and why not when it
		/// doesn't. Checked worst-first is not needed here &mdash; the two refusals are
		/// independent facts about the settlement, not a priority ladder &mdash; but tier is
		/// judged before room so the founder is told the more specific reason first when both
		/// are true.</summary>
		public enum LodgingVerdict
		{
			Lodged,
			NoTier,
			NoRoom,
			NoFineHouse,
			FineHouseOccupied,
			ShopTooCrude
		}

		public static LodgingVerdict AssessLodging(bool HasSufficientTier, bool HasRoom)
		{
			if (!HasSufficientTier)
			{
				return LodgingVerdict.NoTier;
			}
			if (!HasRoom)
			{
				return LodgingVerdict.NoRoom;
			}
			return LodgingVerdict.Lodged;
		}

		/// <summary>The luxury-lane conjunction from the catalogue brief. A legendary trader is
		/// not a generic notable with a large-area check: the building must be an exact fine-house
		/// offer, its actual lot must meet the declared tier, that one house must be wholly vacant,
		/// and the city's live staffed shop tier must warrant the wares. Checked in founder-facing
		/// order so every refusal names the next concrete repair.</summary>
		public static LodgingVerdict AssessLegendaryTraderLodging(bool HasFineHouse,
			KingdomPlotRules.PlotSize FineHouseTier, bool FineHouseVacant, int ShopTier)
		{
			if (!HasFineHouse)
			{
				return LodgingVerdict.NoFineHouse;
			}
			if (FineHouseTier < LegendaryTraderFineHouseTier)
			{
				return LodgingVerdict.NoTier;
			}
			if (!FineHouseVacant)
			{
				return LodgingVerdict.FineHouseOccupied;
			}
			if (ShopTier < LegendaryTraderMinimumShopTier)
			{
				return LodgingVerdict.ShopTooCrude;
			}
			return LodgingVerdict.Lodged;
		}

		public static string LegendaryTraderRefusal(LodgingVerdict Verdict)
		{
			switch (Verdict)
			{
			case LodgingVerdict.NoFineHouse:
				return "This trader will settle only into a fine house, not a manor or an ordinary large home. Raise one and leave it empty.";
			case LodgingVerdict.NoTier:
				return "The fine house is too small for this trader's household. It must stand on at least a medium lot.";
			case LodgingVerdict.FineHouseOccupied:
				return "Every suitable fine house is already somebody's home. This trader requires one wholly vacant.";
			case LodgingVerdict.ShopTooCrude:
				return "The stalls do not yet carry goods worthy of this trader. A staffed shop of tier "
					+ LegendaryTraderMinimumShopTier + " or better must be trading first.";
			default:
				return "";
			}
		}

		public static string SettledTradeNoun(HookKind Kind, bool LegendaryTrader)
		{
			return LegendaryTrader ? "legendary trader" : TradeNoun(Kind);
		}

		public static string ArrivalChronicleLine(string GuestName, string SettlementName)
		{
			return GuestName + " came to the gate of " + SettlementName + ", carrying word of something outside it";
		}

		public static string ArrivalGreeting(HookKind Kind)
		{
			switch (Kind)
			{
				case HookKind.Ruin:
					return "I've been walking a long while, chasing something worth the walk. I could be talked into staying, for the right bed.";
				case HookKind.Machine:
					return "I know a thing worth fixing, if I ever find somewhere worth fixing it from. Do you keep a proper house here?";
				default:
					return "I'm owed, somewhere back the way I came. It's a long walk to collect on it alone. A bed here might change my mind.";
			}
		}

		/// <summary>What the founder is told when a bed of the guest's own tier is not yet
		/// standing. Names the tier so the refusal is a target, not a wall.</summary>
		public static string NoTierRefusal(HookKind Kind)
		{
			return "There is nowhere here " + ArticleFor(TradeNoun(Kind)) + " " + TradeNoun(Kind)
				+ " would call a proper house. A " + KingdomPlotRules.SizeName(RequiredTier(Kind)) + " house, at least, and empty.";
		}

		public static string NoRoomRefusal()
		{
			return "Every bed here is spoken for. Room enough for one more, and this one stays.";
		}

		private static string ArticleFor(string Noun)
		{
			if (string.IsNullOrEmpty(Noun))
			{
				return "a";
			}
			char c = char.ToLowerInvariant(Noun[0]);
			return (c == 'a' || c == 'e' || c == 'i' || c == 'o' || c == 'u') ? "an" : "a";
		}

		public static string LodgedChronicleLine(string GuestName, string SettlementName, HookKind Kind,
			bool LegendaryTrader = false)
		{
			string trade = SettledTradeNoun(Kind, LegendaryTrader);
			return GuestName + " took a bed at " + SettlementName + " and set up as " + ArticleFor(trade) + " " + trade
				+ ", the road behind them finally worth having walked";
		}

		public static string LodgedMessage(string GuestName, HookKind Kind,
			bool LegendaryTrader = false)
		{
			string trade = SettledTradeNoun(Kind, LegendaryTrader);
			return "{{G|" + GuestName + " settles in as " + ArticleFor(trade) + " " + trade + ".}}";
		}

		/// <summary>The official chronicle line for a notable who left unmet.</summary>
		public static string DepartedChronicleLine(string GuestName, string SettlementName)
		{
			return GuestName + " waited at " + SettlementName + " and, finding no bed offered, left a letter and went on";
		}

		/// <summary>
		/// The hook's new life as a standing rumor, phrased for the outsider register that
		/// <c>KingdomChronicle.RecordDisputed</c> already carries. Never lost, only relocated:
		/// the hook that walked in on a guest's own feet now travels in what strangers say instead.
		/// </summary>
		public static string DepartedOutsiderRumor(string GuestName, HookKind Kind, string HookText)
		{
			return GuestName + " passed through and was gone before anyone could offer a bed, and what " + GuestName
				+ " was bound for — " + HookText + " — is still out there, waiting on whoever hears the rumor next";
		}

		/// <summary>One line of the guestbook: what a notable guest did, in the past tense,
		/// suitable for the roll-of-settlers appendix.</summary>
		public static string GuestbookLine(string GuestName, HookKind Kind, string HookText, bool Lodged,
			bool LegendaryTrader = false)
		{
			if (Lodged)
			{
				string trade = SettledTradeNoun(Kind, LegendaryTrader);
				return GuestName + ", " + ArticleFor(trade) + " " + trade + " who once meant to chase " + HookText + " {{K|(lodged)}}";
			}
			return GuestName + ", who left word of " + HookText + " {{K|(departed; a rumor now)}}";
		}

		/// <summary>
		/// The chronicle's telling of a run of notables who came, waited out their patience at an
		/// unanswered gate, and went on again while the founder was elsewhere. One entry for the
		/// whole run, dated against the day it is being told about &mdash; the register keeps two
		/// hundred lines and a season of notables is not what they are for.
		/// </summary>
		/// <param name="Passed">How many came and went. Zero or less answers null.</param>
		/// <param name="SettlementName">The seat they came to.</param>
		/// <param name="DaysAgo">Days since the last of them stood at the gate.</param>
		public static string PassedChronicleLine(int Passed, string SettlementName, int DaysAgo)
		{
			if (Passed <= 0)
			{
				return null;
			}
			string who = (Passed == 1) ? "a notable" : (Passed + " notables");
			return who + " came to the gate of " + SettlementName + " while you were away, waited, and went on "
				+ "leaving letters behind them, " + WhenPhrase(DaysAgo);
		}

		/// <summary>
		/// The same run in the outsider register: the hooks they were carrying did not die with
		/// the visit, they became what the road says. Never lost, only relocated &mdash; the
		/// co-opt's own promise, kept for the ones nobody was home to meet.
		/// </summary>
		public static string PassedOutsiderRumor(int Passed, string SettlementName, int DaysAgo)
		{
			if (Passed <= 0)
			{
				return null;
			}
			string who = (Passed == 1) ? "a notable" : (Passed + " notables");
			return who + " walked to " + SettlementName + " and found nobody to offer a bed, and whatever they were "
				+ "each bound for is out on the roads now as talk, " + WhenPhrase(DaysAgo);
		}

		/// <summary>The homecoming ledger's note for the same run. News to discover, never a debt
		/// to answer for: an unanswered gate costs the settlement a chance and nothing else.
		/// </summary>
		public static string PassedLedgerNote(int Passed, int DaysAgo)
		{
			if (Passed <= 0)
			{
				return null;
			}
			string who = (Passed == 1) ? "One notable" : (Passed + " notables");
			return "{{K|" + who + " came to the gate while you were away and found no bed offered — " + WhenPhrase(DaysAgo)
				+ ". What they were chasing is rumor on the road now; nothing is lost.}}";
		}

		/// <summary>One guestbook line for the whole run, so the appendix records that the gate
		/// went unanswered without spending a line per stranger nobody met.</summary>
		public static string PassedGuestbookLine(int Passed, int DaysAgo)
		{
			if (Passed <= 0)
			{
				return null;
			}
			string who = (Passed == 1) ? "One notable" : (Passed + " notables");
			return who + " who came to a gate nobody answered, " + WhenPhrase(DaysAgo) + " {{K|(departed; rumors now)}}";
		}

		/// <summary>
		/// The ledger note for one notable who waited at the gate and gave up while the founder
		/// was elsewhere, dated against the day their patience actually ran out rather than the
		/// pass that noticed it.
		/// </summary>
		/// <param name="GuestName">Who it was.</param>
		/// <param name="DaysAgo">Whole days since they gave up. Zero and below drop the clause.</param>
		public static string DepartedLedgerNote(string GuestName, int DaysAgo)
		{
			string when = (DaysAgo <= 0)
				? ""
				: ((DaysAgo == 1) ? " a day before you saw it" : (" " + DaysAgo + " days before you saw it"));
			return "{{K|" + GuestName + " waited a while at the gate, found no bed offered, and moved on" + when
				+ ". What " + GuestName + " was chasing is a rumor on the road now \u2014 nothing is lost.}}";
		}

		/// <summary>How a run of unwitnessed passages is dated: against the day the founder is
		/// being told, the same phrasing a subsidence rung and a plain traveller's passage
		/// both use.</summary>
		public static string WhenPhrase(int DaysAgo)
		{
			if (DaysAgo <= 0)
			{
				return "the last of them today";
			}
			return (DaysAgo == 1)
				? "the last of them a day before you saw it"
				: ("the last of them " + DaysAgo + " days before you saw it");
		}

	}
}
