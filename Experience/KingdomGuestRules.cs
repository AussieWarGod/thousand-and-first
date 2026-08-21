using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// Engine-free rules for the two co-opted ideas this file pairs: notable guests who carry one
	/// outward-pointing hook and may be lodged into the settlement, and the carry-sign that marks
	/// a pile or container anywhere in the world for porters to haul home. No <c>XRL</c> usings
	/// &mdash; everything here is deterministic given the inputs its callers
	/// (<see cref="KingdomGuestbook"/>) read off the live kingdom.
	/// <para>
	/// Extends <see cref="KingdomLocusRules"/> rather than folding into it: a notable guest is a
	/// structurally different kind of arrival &mdash; one that can be lodged, that carries a hook,
	/// that leaves a rumor behind &mdash; and giving it its own rules file keeps
	/// <c>KingdomLocusRules</c>'s already-shipped plain-traveller cadence untouched.
	/// </para>
	/// </summary>
	public static class KingdomGuestRules
	{
		// ==================================================================================
		// Guests at the gate
		// ==================================================================================

		/// <summary>What a notable guest is carrying word of. Each points outward, at something
		/// the settlement has not touched and this mod does not simulate directly &mdash; the
		/// hook is the promise of a world bigger than the settlement, told in prose, the same way
		/// the outsider chronicle register already is.</summary>
		public enum HookKind
		{
			Ruin = 0,
			Machine = 1,
			Debt = 2
		}

		public const int HookKindCount = 3;

		/// <summary>A ruin worth stripping, in the guest's own words. Indexed by a flavor roll.</summary>
		public static readonly string[] RuinHooks = new string[4]
		{
			"a sunken forge nobody has gone back into since the roof fell",
			"a stair down into an unlicensed dig, sealed but not empty",
			"a wrecked transport half-swallowed by the salt, still shut tight",
			"a shrine picked over by every looter who lacked the nerve to go deeper"
		};

		/// <summary>A machine worth certifying, in the guest's own words. Indexed by a flavor roll.</summary>
		public static readonly string[] MachineHooks = new string[4]
		{
			"a loom that still turns, if anyone would trust it enough to run it",
			"a still that has not been lit in a generation, and might yet be lit again",
			"a pump seized up with verdigris, sound underneath it",
			"a cutting-engine, whole, that nobody nearby knows how to ask permission of"
		};

		/// <summary>Named vanilla villages a debt hook may point at. Verified against
		/// StreamingAssets/Base/Factions.xml; already used for exactly this kind of flavor by
		/// <c>KingdomCreed.cs</c>.</summary>
		public static readonly string[] NamedVillages = new string[3] { "Joppa", "Kyakukya", "Ezra" };

		/// <summary>What kind of debt a debt hook names. Indexed by a flavor roll, independent of
		/// which village it lands in.</summary>
		public static readonly string[] DebtReasons = new string[3]
		{
			"a debt of drams, the plain kind",
			"a debt of favor, which is worth more and harder to collect",
			"a debt nobody there will name outright, which is usually the largest kind"
		};

		/// <summary>Which of <see cref="HookKind"/> a roll names. <paramref name="Roll"/> is
		/// taken modulo <see cref="HookKindCount"/>, so any non-negative roll is accepted.</summary>
		public static HookKind PickHookKind(ulong Roll)
		{
			return (HookKind)(int)(Roll % HookKindCount);
		}

		/// <summary>
		/// Composes the hook's prose from <paramref name="Kind"/> and a flavor roll. A debt hook
		/// spends the roll twice &mdash; once for the village, once for the reason &mdash; the
		/// same split <c>KingdomRules.ComposeOutsider</c> uses for lead and tail, so the two
		/// tables vary independently instead of one being pinned to the other.
		/// </summary>
		public static string HookText(HookKind Kind, ulong FlavorRoll)
		{
			switch (Kind)
			{
				case HookKind.Ruin:
					return RuinHooks[(int)(FlavorRoll % (ulong)RuinHooks.Length)];
				case HookKind.Machine:
					return MachineHooks[(int)(FlavorRoll % (ulong)MachineHooks.Length)];
				default:
				{
					int village = (int)(FlavorRoll % (ulong)NamedVillages.Length);
					int reason = (int)((FlavorRoll / (ulong)NamedVillages.Length) % (ulong)DebtReasons.Length);
					return DebtReasons[reason] + ", left standing in " + NamedVillages[village];
				}
			}
		}

		/// <summary>
		/// The smallest housing tier a notable will settle for, by what their hook says about
		/// them: a scavenger travels light, an engineer wants a proper house, and someone owed a
		/// debt keeps some standing even on the road.
		/// </summary>
		public static KingdomPlotRules.PlotSize RequiredTier(HookKind Kind)
		{
			switch (Kind)
			{
				case HookKind.Ruin:
					return KingdomPlotRules.PlotSize.Small;
				case HookKind.Machine:
					return KingdomPlotRules.PlotSize.Large;
				default:
					return KingdomPlotRules.PlotSize.Medium;
			}
		}

		/// <summary>The trade a lodged notable takes up, named from their hook rather than
		/// rolled independently &mdash; what they were chasing on the road is what they know.</summary>
		public static string TradeNoun(HookKind Kind)
		{
			switch (Kind)
			{
				case HookKind.Ruin:
					return "scavenger";
				case HookKind.Machine:
					return "machinist";
				default:
					return "reckoner of debts";
			}
		}

		/// <summary>How rarely a notable guest &mdash; as opposed to an ordinary passing
		/// traveller &mdash; walks up the road. Rarer than <c>KingdomLocusRules.GuestIntervalTicks</c>
		/// on purpose: a notable is an event, not ambient traffic.</summary>
		public const long NotableGuestIntervalTicks = KingdomRules.TicksPerDay * 7;

		/// <summary>How long a notable guest waits to be lodged before giving up. Longer than a
		/// plain traveller's patience (<c>KingdomLocusRules.GuestPatienceTicks</c>): finding a
		/// bed of the right tier is a real ask, and a notable who came this far waits for it.</summary>
		public const long NotableGuestPatienceTicks = KingdomRules.TicksPerDay * 2;

		public static bool ShouldArrive(long TimeTicks, long NextDueTick)
		{
			return TimeTicks >= NextDueTick;
		}

		public static long NextDueTick(long TimeTicks)
		{
			return TimeTicks + NotableGuestIntervalTicks;
		}

		public static long DepartTickFor(long ArrivalTick)
		{
			return ArrivalTick + NotableGuestPatienceTicks;
		}

		public static bool ShouldDepartUnattended(long TimeTicks, long DepartTick)
		{
			return DepartTick > 0 && TimeTicks >= DepartTick;
		}

		/// <summary>Whether lodging a notable guest right now succeeds, and why not when it
		/// doesn't. Checked worst-first is not needed here &mdash; the two refusals are
		/// independent facts about the settlement, not a priority ladder &mdash; but tier is
		/// judged before room so the founder is told the more specific reason first when both
		/// are true.</summary>
		public enum LodgingVerdict
		{
			Lodged,
			NoTier,
			NoRoom
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

		public static string LodgedChronicleLine(string GuestName, string SettlementName, HookKind Kind)
		{
			return GuestName + " took a bed at " + SettlementName + " and set up as " + ArticleFor(TradeNoun(Kind)) + " " + TradeNoun(Kind)
				+ ", the road behind them finally worth having walked";
		}

		public static string LodgedMessage(string GuestName, HookKind Kind)
		{
			return "{{G|" + GuestName + " settles in as " + ArticleFor(TradeNoun(Kind)) + " " + TradeNoun(Kind) + ".}}";
		}

		public static string LodgedConversationAnswer(HookKind Kind, string HookText)
		{
			return "I was bound for " + HookText + ". I still think about it. But a bed's a bed, and I've had enough of the road for a while.";
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
		public static string GuestbookLine(string GuestName, HookKind Kind, string HookText, bool Lodged)
		{
			if (Lodged)
			{
				return GuestName + ", " + ArticleFor(TradeNoun(Kind)) + " " + TradeNoun(Kind) + " who once meant to chase " + HookText + " {{K|(lodged)}}";
			}
			return GuestName + ", who left word of " + HookText + " {{K|(departed; a rumor now)}}";
		}

		/// <summary>Guestbook entries kept per city before the oldest is trimmed. Smaller than
		/// <c>KingdomChronicle.MaxEntries</c> because the guestbook is a side reading, not the
		/// settlement's primary record &mdash; every guestbook event is also written into the
		/// chronicle proper, which keeps the full 200.</summary>
		public const int GuestbookMaxEntries = 30;

		// ==================================================================================
		// The carry-sign
		// ==================================================================================

		/// <summary>Days a haul takes even between adjacent ground &mdash; packing a pile onto
		/// porters' backs and setting out is never instant.</summary>
		public const int CarrySignBaseDays = 2;

		/// <summary>Additional days per zone-step of Chebyshev distance between the marked ground
		/// and the nearest ground the settlement holds.</summary>
		public const int CarrySignDaysPerZoneStep = 1;

		/// <summary>
		/// Chebyshev distance between two zones in the same world, three axes at once: the two
		/// grid axes <c>KingdomRules.CoordsAdjacent</c> already reads, and depth, because moving
		/// between strata is exactly as real a distance for porters carrying a load as moving
		/// across the surface.
		/// </summary>
		public static int ZoneGridDistance(int GX1, int GY1, int Z1, int GX2, int GY2, int Z2)
		{
			int dx = (GX1 > GX2) ? (GX1 - GX2) : (GX2 - GX1);
			int dy = (GY1 > GY2) ? (GY1 - GY2) : (GY2 - GY1);
			int dz = (Z1 > Z2) ? (Z1 - Z2) : (Z2 - Z1);
			int m = (dx > dy) ? dx : dy;
			return (m > dz) ? m : dz;
		}

		/// <summary>Whole days a haul over <paramref name="ZoneDistance"/> zone-steps takes.</summary>
		public static int HaulDays(int ZoneDistance)
		{
			if (ZoneDistance < 0)
			{
				ZoneDistance = 0;
			}
			return CarrySignBaseDays + ZoneDistance * CarrySignDaysPerZoneStep;
		}

		public static long HaulDueTick(long PlantedTick, int Days)
		{
			return PlantedTick + (long)Days * KingdomRules.TicksPerDay;
		}

		public static bool ShouldResolveHaul(long TimeTicks, long DueTick)
		{
			return TimeTicks >= DueTick;
		}

		/// <summary>
		/// Chance out of 100 a haul is lost to the road while a raid presently threatens its
		/// destination. Zero whenever no raid is warned: an ordinary road is safe, and threat has
		/// to cost more than the tribute that ends it (STANDARDS 8) or paying tribute would never
		/// pay for itself against an inbound haul.
		/// </summary>
		public const int RoadRiskPercent = 35;

		/// <summary>
		/// Whether a haul is lost, judged only from state live at the moment of resolution
		/// (STANDARDS 5.3, witnessed-only accounting) &mdash; never from a raid that came and
		/// went while nobody was there to see this haul through it.
		/// </summary>
		/// <param name="RaidActive">The destination settlement currently has a raid warned
		/// against it (<c>KingdomSystem.RaidState == 1</c>).</param>
		/// <param name="RiskRoll">A roll in [0, 100).</param>
		public static bool HaulAtRisk(bool RaidActive, int RiskRoll)
		{
			return RaidActive && RiskRoll >= 0 && RiskRoll < RoadRiskPercent;
		}

		public enum PlantVerdict
		{
			Planted,
			NotFounded,
			NothingToCarry,
			NoRoad,
			AlreadyInFlight
		}

		/// <summary>
		/// Judges whether planting a carry-sign right now is allowed. Checked in the order a
		/// founder would hit the refusals: you must have somewhere to carry to before anything
		/// else matters, a haul already on the road blocks a second one (mirrors
		/// <c>KingdomManifestRules.JudgeManifest</c>'s one-in-flight rule exactly), then whether
		/// there is a road there at all, then whether the ground under the sign holds anything
		/// worth the trip.
		/// </summary>
		public static PlantVerdict AssessPlant(bool Founded, bool AlreadyInFlight, bool HasRoad, int ManifestTotal)
		{
			if (!Founded)
			{
				return PlantVerdict.NotFounded;
			}
			if (AlreadyInFlight)
			{
				return PlantVerdict.AlreadyInFlight;
			}
			if (!HasRoad)
			{
				return PlantVerdict.NoRoad;
			}
			if (ManifestTotal <= 0)
			{
				return PlantVerdict.NothingToCarry;
			}
			return PlantVerdict.Planted;
		}

		public static string PlantRefusal(PlantVerdict Verdict)
		{
			switch (Verdict)
			{
				case PlantVerdict.NotFounded:
					return "There is no settlement yet for porters to carry anything home to.";
				case PlantVerdict.AlreadyInFlight:
					return "A carry-sign is already planted and porters are already on the road. Only one load travels at a time.";
				case PlantVerdict.NoRoad:
					return "This ground is too far from anything the settlement holds. Porters cannot find the road from here.";
				case PlantVerdict.NothingToCarry:
					return "There is nothing here the settlement's stockpiles would know what to do with.";
				default:
					return "";
			}
		}

		/// <summary>Builds a single-value confirmation prompt naming exactly what the sign will
		/// take. <paramref name="ManifestDescription"/> is <c>KingdomMaterialTally.Describe()</c>'s
		/// output &mdash; consent before cost: the founder sees the whole manifest before anything
		/// is swept.</summary>
		public static string PlantConfirm(string ManifestDescription, int Days)
		{
			return "Plant the carry-sign here? It will take " + ManifestDescription + " with it, and porters will need "
				+ Days + ((Days == 1) ? " day" : " days") + " to carry it home. The pile will be gone the moment the sign is planted — the sign is the designation.";
		}

		public static string PlantedChronicleLine(string SettlementName, string ManifestDescription, int Days)
		{
			return "a carry-sign was planted out on the road, marking " + ManifestDescription + " bound for " + SettlementName + ", " + Days + ((Days == 1) ? " day" : " days") + " out";
		}

		public static string PlantedMessage(int Days)
		{
			return "{{G|The carry-sign is planted.}} Porters will need " + Days + ((Days == 1) ? " day" : " days") + " to bring the load home.";
		}

		public static string DeliveredChronicleLine(string SettlementName, string ManifestDescription)
		{
			return "porters reached " + SettlementName + " carrying " + ManifestDescription + " that a carry-sign had marked out on the road";
		}

		public static string DeliveredLedgerNote(string ManifestDescription)
		{
			return "{{G|Porters arrived carrying " + ManifestDescription + " that a carry-sign had marked out on the road.}}";
		}

		public static string LostChronicleLine(string SettlementName, string RaidFactionName, string ManifestDescription)
		{
			return "porters carrying " + ManifestDescription + " toward " + SettlementName + " never made it past " + RaidFactionName + "'s riders, and the load is given up for lost";
		}

		public static string LostLedgerNote(string ManifestDescription)
		{
			return "{{r|A carry-sign's load never made it home: " + ManifestDescription + " lost to raiders on the road.}}";
		}
	}
}
