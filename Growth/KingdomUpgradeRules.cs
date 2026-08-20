namespace ThousandAndFirst
{
	/// <summary>
	/// Pure arithmetic and eligibility for improving a work the settlement already has into a
	/// better one: the cask rack that becomes a great cistern, the thorn palisade that becomes a
	/// stone rampart. Engine-free by design, so every refusal &mdash; and every refusal's
	/// sentence &mdash; is provable without a running game. The engine-coupled half, which reads
	/// real objects and raises real scaffolding, lives in <c>KingdomUpgrade</c> in this folder.
	/// <para>
	/// Nothing here decides that an improvement is a good idea. It decides whether one is
	/// <i>earned</i>, and when it is not, which single sentence the founder is owed.
	/// </para>
	/// </summary>
	public static class KingdomUpgradeRules
	{
		/// <summary>
		/// What a work's improvement is waiting on, if anything. Ordered the way
		/// <see cref="Assess"/> checks them: what the data says comes first, then what the
		/// founder said, then what the settlement has earned, and the pacing gate last so a work
		/// that is otherwise ready reports the honest "the settlement is already busy" rather
		/// than a condition it actually meets.
		/// <para>
		/// Every member is either a <i>not applicable</i> case, which says nothing (STANDARDS 7b's
		/// first kind), or an <i>applicable but blocked</i> case, which owes the founder a line
		/// &mdash; see <see cref="ReasonLine"/>, which returns null for exactly the former set.
		/// </para>
		/// </summary>
		public enum UpgradeVerdict
		{
			/// <summary>Earned, affordable, and free to begin.</summary>
			Ready = 0,

			/// <summary>The design names nothing to grow into. Silent: most designs never
			/// change, and that is not a stall.</summary>
			NoSuccessor = 1,

			/// <summary>The design names a successor no registry entry defines. Malformed data,
			/// not a settlement condition; announced so a third-party chain that half-loaded
			/// says so instead of doing nothing forever.</summary>
			SuccessorUnknown = 2,

			/// <summary>The successor is not built in this style of city. Silent: it never
			/// appeared in the commission list either, so nothing has stalled.</summary>
			StyleForbids = 3,

			/// <summary>Not a work the settlement raised &mdash; the founder built or placed it
			/// and the settlement merely adopted it. Silent as a message and stated in the list:
			/// the protection law means the settlement never rebuilds what the player made.
			/// </summary>
			NotOurWork = 4,

			/// <summary>Already improving. The scaffold is standing; nothing is wrong.</summary>
			AlreadyWorking = 5,

			/// <summary>The founder told the settlement to leave this ground as it is.</summary>
			HeldOnThisGround = 6,

			/// <summary>The founder told the settlement to leave this one work as it is.</summary>
			HeldByFounder = 7,

			/// <summary>The settlement is not yet large enough for the successor.</summary>
			StageTooLow = 8,

			/// <summary>Every settler is already spoken for; nobody is free to do the work.
			/// </summary>
			NotEnoughHands = 9,

			/// <summary>The successor could not hold what the predecessor is carrying, so the
			/// improvement is refused rather than risking the founder's water or goods on a
			/// badly authored chain.</summary>
			WouldSpill = 10,

			/// <summary>The stores cannot pay for it without dropping below the reserve the
			/// settlement lives on.</summary>
			NotEnoughWater = 11,

			/// <summary>Another improvement is already under way on this ground. The settlement
			/// betters one work at a time.</summary>
			WorksElsewhere = 12
		}

		/// <summary>An upgrade chain read off one <c>&lt;building&gt;</c> entry's optional
		/// attributes. A design with no <c>UpgradesTo</c> yields one of these with
		/// <see cref="Defined"/> false, which is the state every design that ships today is in.
		/// </summary>
		public class UpgradeChain
		{
			/// <summary>Registry key of the design this one grows into, or null.</summary>
			public string SuccessorKey;

			/// <summary>Authored water cost, or <see cref="Unset"/> to compute one.</summary>
			public int CostDramsOverride = Unset;

			/// <summary>Authored build time, or <see cref="UnsetTicks"/> to compute one.</summary>
			public long BuildTicksOverride = UnsetTicks;

			/// <summary>Authored free-hand requirement, or <see cref="Unset"/> to compute one.
			/// </summary>
			public int CrewOverride = Unset;

			/// <summary>Whether <see cref="MinStageOverride"/> was authored at all. False means
			/// the successor's own <c>MinStage</c> is the gate.</summary>
			public bool HasMinStageOverride;

			public GrowthStage MinStageOverride;

			/// <summary>False for every design that never changes.</summary>
			public bool Defined => !string.IsNullOrEmpty(SuccessorKey);
		}

		/// <summary>Sentinel for an integer attribute the author did not write. Negative so it
		/// can never be confused with a real cost or crew, both of which may legitimately be
		/// zero.</summary>
		public const int Unset = -1;

		/// <summary>Sentinel for an unwritten tick count. Zero, because a build time of zero is
		/// already rejected as malformed.</summary>
		public const long UnsetTicks = 0L;

		/// <summary>Water an improvement costs at the very least, however cheap the arithmetic
		/// makes it. Something is always carried, mixed, and poured, so an improvement is never
		/// free even when the successor's own design is no dearer than the predecessor's.
		/// </summary>
		public const int MinimumCostDrams = 2;

		/// <summary>What fraction of a fresh build an improvement takes when the author did not
		/// say. Under a hundred because the predecessor's own footing, frame, and materials are
		/// standing there already; not far under, because the founder should see the work
		/// happen.</summary>
		public const int BuildTicksPercent = 75;

		/// <summary>Settlers who must be free even for an improvement the successor needs no
		/// permanent crew for. Somebody does the work; nobody does it for nothing.</summary>
		public const int MinimumCrew = 1;

		/// <summary>
		/// Water an improvement costs. The default is the difference between the two designs
		/// &mdash; the founder already paid for the predecessor and its materials do not vanish
		/// &mdash; floored at <see cref="MinimumCostDrams"/> so an improvement is never free, and
		/// capped at the successor's own price so improving is never dearer than razing and
		/// building fresh would have been.
		/// </summary>
		/// <param name="SuccessorCost">The successor design's own <c>Cost</c>.</param>
		/// <param name="PredecessorCost">The predecessor design's own <c>Cost</c>.</param>
		/// <param name="Override">Authored <c>UpgradeCost</c>, or <see cref="Unset"/>.</param>
		/// <returns>Drams, never negative.</returns>
		public static int CostDrams(int SuccessorCost, int PredecessorCost, int Override)
		{
			if (Override >= 0)
			{
				return Override;
			}
			int successor = (SuccessorCost > 0) ? SuccessorCost : 0;
			int predecessor = (PredecessorCost > 0) ? PredecessorCost : 0;
			int cost = successor - predecessor;
			if (cost < MinimumCostDrams)
			{
				cost = MinimumCostDrams;
			}
			if (cost > successor)
			{
				cost = successor;
			}
			return cost;
		}

		/// <summary>
		/// Ticks an improvement takes. Defaults to <see cref="BuildTicksPercent"/> of building
		/// the successor from nothing, floored at one tick so an improvement can never complete
		/// in the same instant it began, or in the past.
		/// </summary>
		/// <param name="SuccessorTicks">The successor design's own <c>Ticks</c>.</param>
		/// <param name="Override">Authored <c>UpgradeTicks</c>, or <see cref="UnsetTicks"/>.
		/// </param>
		public static long BuildTicks(long SuccessorTicks, long Override)
		{
			if (Override > 0L)
			{
				return Override;
			}
			long ticks = ((SuccessorTicks > 0L) ? SuccessorTicks : 1L) * BuildTicksPercent / 100L;
			return (ticks < 1L) ? 1L : ticks;
		}

		/// <summary>
		/// Settlers who must be free of every other duty for the work to start. Defaults to the
		/// crew the successor will need to run once it stands &mdash; a settlement that cannot
		/// man a thing has no business raising it &mdash; and never fewer than
		/// <see cref="MinimumCrew"/>.
		/// </summary>
		/// <param name="SuccessorStaff">The successor design's own <c>Staff</c>.</param>
		/// <param name="Override">Authored <c>UpgradeCrew</c>, or <see cref="Unset"/>.</param>
		public static int CrewRequired(int SuccessorStaff, int Override)
		{
			int crew = (Override >= 0) ? Override : SuccessorStaff;
			return (crew < MinimumCrew) ? MinimumCrew : crew;
		}

		/// <summary>
		/// The growth stage an improvement waits for. Defaults to the successor's own
		/// <c>MinStage</c>, so a chain that names nothing extra inherits exactly the gate the
		/// commission list already uses and can never let a work sneak past it.
		/// </summary>
		public static GrowthStage StageRequired(GrowthStage SuccessorMinStage, bool HasOverride, GrowthStage Override)
		{
			if (!HasOverride)
			{
				return SuccessorMinStage;
			}
			return (Override > SuccessorMinStage) ? Override : SuccessorMinStage;
		}

		/// <summary>
		/// Water an improvement must leave standing in the stores: what the settlement drinks
		/// over the whole absence it is ever charged for. Improving is a luxury paid out of
		/// surplus, so it can never be the reason a settlement goes thirsty.
		/// </summary>
		/// <param name="Population">Settlers the stores must keep.</param>
		/// <param name="Stage">Growth stage, which sets the per-head rate.</param>
		public static int ReserveDrams(int Population, GrowthStage Stage)
		{
			return KingdomRules.UpkeepDrams(Population, Stage) * KingdomRules.MaxUpkeepDaysCharged;
		}

		/// <summary>
		/// Whether the stores can pay for an improvement and still hold the reserve.
		/// </summary>
		/// <param name="StoredWater">Drams currently in the dedicated stores.</param>
		/// <param name="Cost">Drams the improvement asks for.</param>
		/// <param name="Reserve">Drams that must remain, from <see cref="ReserveDrams"/>.</param>
		public static bool CanAfford(int StoredWater, int Cost, int Reserve)
		{
			return StoredWater - Cost >= Reserve;
		}

		/// <summary>
		/// Drams the stores are short of affording an improvement, for the sentence the founder
		/// reads. Zero when it is affordable.
		/// </summary>
		public static int Shortfall(int StoredWater, int Cost, int Reserve)
		{
			int missing = Cost + Reserve - StoredWater;
			return (missing > 0) ? missing : 0;
		}

		/// <summary>
		/// Whether the successor can receive everything the predecessor is holding. A liquid
		/// capacity of <see cref="UnknownCapacity"/> means the successor's blueprint did not
		/// declare one, which is not evidence of a problem and never blocks; a real capacity
		/// smaller than what is stored is, and does.
		/// </summary>
		/// <param name="StoredLiquid">Drams of anything in the predecessor.</param>
		/// <param name="SuccessorCapacity">The successor's declared liquid capacity, or
		/// <see cref="UnknownCapacity"/>.</param>
		/// <param name="HeldItems">Objects inside the predecessor.</param>
		/// <param name="SuccessorHoldsItems">Whether the successor has an inventory at all.
		/// </param>
		public static bool ContentsWouldFit(int StoredLiquid, int SuccessorCapacity, int HeldItems, bool SuccessorHoldsItems)
		{
			if (HeldItems > 0 && !SuccessorHoldsItems)
			{
				return false;
			}
			if (StoredLiquid > 0 && SuccessorCapacity != UnknownCapacity && SuccessorCapacity < StoredLiquid)
			{
				return false;
			}
			return true;
		}

		/// <summary>Reported when a blueprint declares no liquid capacity, which is different
		/// from declaring a capacity of nothing. Negative because Qud's own open pools use a
		/// negative MaxVolume for "unbounded", and neither case is a reason to refuse.</summary>
		public const int UnknownCapacity = -1;

		/// <summary>
		/// The settlement's verdict on improving one standing work. Checked in the order that
		/// respects intent before arithmetic: malformed or inapplicable data first, then work
		/// already under way, then what the founder said to leave alone &mdash; a founder who
		/// held a work is never lectured about its water &mdash; then the earned conditions in
		/// the order they can be acted on, and the one-at-a-time pacing gate last.
		/// </summary>
		/// <param name="HasSuccessor">Whether the design names anything to grow into.</param>
		/// <param name="SuccessorKnown">Whether that successor resolves to a registry entry.
		/// </param>
		/// <param name="StyleAllowed">Whether the city's style permits the successor.</param>
		/// <param name="OurWork">True only for a work the settlement itself raised; an adopted
		/// structure is the founder's and is never rebuilt.</param>
		/// <param name="AlreadyWorking">Whether this work's own improvement is under way.</param>
		/// <param name="HeldOnThisGround">Whether the founder held this whole settlement.</param>
		/// <param name="HeldByFounder">Whether the founder held this one work.</param>
		/// <param name="Stage">The settlement's growth stage.</param>
		/// <param name="StageNeeded">Stage the improvement waits for, from
		/// <see cref="StageRequired"/>.</param>
		/// <param name="FreeHands">Settlers not already spoken for.</param>
		/// <param name="CrewNeeded">Hands the work needs, from <see cref="CrewRequired"/>.</param>
		/// <param name="ContentsFit">From <see cref="ContentsWouldFit"/>.</param>
		/// <param name="StoredWater">Drams in the dedicated stores.</param>
		/// <param name="Cost">Drams the improvement asks for, from <see cref="CostDrams"/>.
		/// </param>
		/// <param name="Reserve">Drams that must remain, from <see cref="ReserveDrams"/>.</param>
		/// <param name="OtherWorkUnderway">Whether another improvement is already under way on
		/// this ground.</param>
		public static UpgradeVerdict Assess(bool HasSuccessor, bool SuccessorKnown, bool StyleAllowed, bool OurWork, bool AlreadyWorking, bool HeldOnThisGround, bool HeldByFounder, GrowthStage Stage, GrowthStage StageNeeded, int FreeHands, int CrewNeeded, bool ContentsFit, int StoredWater, int Cost, int Reserve, bool OtherWorkUnderway)
		{
			if (!HasSuccessor)
			{
				return UpgradeVerdict.NoSuccessor;
			}
			if (!SuccessorKnown)
			{
				return UpgradeVerdict.SuccessorUnknown;
			}
			if (!OurWork)
			{
				return UpgradeVerdict.NotOurWork;
			}
			if (!StyleAllowed)
			{
				return UpgradeVerdict.StyleForbids;
			}
			if (AlreadyWorking)
			{
				return UpgradeVerdict.AlreadyWorking;
			}
			if (HeldOnThisGround)
			{
				return UpgradeVerdict.HeldOnThisGround;
			}
			if (HeldByFounder)
			{
				return UpgradeVerdict.HeldByFounder;
			}
			if (Stage < StageNeeded)
			{
				return UpgradeVerdict.StageTooLow;
			}
			if (FreeHands < CrewNeeded)
			{
				return UpgradeVerdict.NotEnoughHands;
			}
			if (!ContentsFit)
			{
				return UpgradeVerdict.WouldSpill;
			}
			if (!CanAfford(StoredWater, Cost, Reserve))
			{
				return UpgradeVerdict.NotEnoughWater;
			}
			if (OtherWorkUnderway)
			{
				return UpgradeVerdict.WorksElsewhere;
			}
			return UpgradeVerdict.Ready;
		}

		/// <summary>Whether a verdict means the settlement should actually raise scaffolding.
		/// </summary>
		public static bool IsReady(UpgradeVerdict Verdict)
		{
			return Verdict == UpgradeVerdict.Ready;
		}

		/// <summary>
		/// Whether a verdict is the STANDARDS 7b <i>applicable but blocked</i> kind &mdash; a
		/// work that could grow and is not growing, which owes the founder one sentence. The
		/// silent kinds are the ones where nothing has stalled: no chain at all, a design this
		/// city's style never builds, a structure the founder made themselves, work already
		/// under way, and the ready case.
		/// </summary>
		public static bool IsBlocked(UpgradeVerdict Verdict)
		{
			switch (Verdict)
			{
			case UpgradeVerdict.Ready:
			case UpgradeVerdict.NoSuccessor:
			case UpgradeVerdict.StyleForbids:
			case UpgradeVerdict.NotOurWork:
			case UpgradeVerdict.AlreadyWorking:
				return false;
			default:
				return true;
			}
		}

		/// <summary>The settlement's growth stages in the register the founder reads them in.
		/// </summary>
		public static string StageWord(GrowthStage Stage)
		{
			switch (Stage)
			{
			case GrowthStage.Camp:
				return "camp";
			case GrowthStage.Steading:
				return "steading";
			case GrowthStage.Village:
				return "village";
			case GrowthStage.Town:
				return "town";
			default:
				return "city";
			}
		}

		/// <summary>
		/// The one sentence a blocked improvement owes the founder, or null for a verdict that
		/// correctly says nothing. Every reason names the thing that would lift it, because a
		/// stall the player cannot act on is the failure this whole file exists to prevent.
		/// </summary>
		/// <param name="Verdict">What <see cref="Assess"/> found.</param>
		/// <param name="PredecessorName">What is standing there now.</param>
		/// <param name="SuccessorName">What it would become. May be null when the successor
		/// could not be resolved at all.</param>
		/// <param name="StageNeeded">Stage the improvement waits for.</param>
		/// <param name="CrewNeeded">Hands the improvement needs free.</param>
		/// <param name="Shortfall">Drams the stores are short, from
		/// <see cref="Shortfall(int, int, int)"/>.</param>
		/// <returns>A player-facing line, or null when the verdict is not the blocked kind.
		/// </returns>
		public static string ReasonLine(UpgradeVerdict Verdict, string PredecessorName, string SuccessorName, GrowthStage StageNeeded, int CrewNeeded, int Shortfall)
		{
			string predecessor = string.IsNullOrEmpty(PredecessorName) ? "work" : PredecessorName;
			string successor = string.IsNullOrEmpty(SuccessorName) ? "something better" : SuccessorName;
			switch (Verdict)
			{
			case UpgradeVerdict.SuccessorUnknown:
				return "The " + predecessor + " was meant to grow into something this settlement has no design for.";
			case UpgradeVerdict.HeldOnThisGround:
				return "The " + predecessor + " could be raised into " + Article(successor) + ", but this ground is to be left as it is.";
			case UpgradeVerdict.HeldByFounder:
				return "The " + predecessor + " could be raised into " + Article(successor) + ", but it is to be left as it is.";
			case UpgradeVerdict.StageTooLow:
				return "The " + predecessor + " could be raised into " + Article(successor) + " once this is a " + StageWord(StageNeeded) + ".";
			case UpgradeVerdict.NotEnoughHands:
				return "The " + predecessor + " could be raised into " + Article(successor) + ", but no "
					+ ((CrewNeeded == 1) ? "one is" : (CrewNeeded + " settlers are")) + " free for the work.";
			case UpgradeVerdict.WouldSpill:
				return "The " + predecessor + " could be raised into " + Article(successor) + ", but what it holds would have nowhere to go.";
			case UpgradeVerdict.NotEnoughWater:
				return "The " + predecessor + " could be raised into " + Article(successor) + " for "
					+ Shortfall + " more " + ((Shortfall == 1) ? "dram" : "drams") + " than the stores can spare.";
			case UpgradeVerdict.WorksElsewhere:
				return "The " + predecessor + " could be raised into " + Article(successor) + " once the settlement is done with the work it has in hand.";
			default:
				return null;
			}
		}

		/// <summary>
		/// The line the founder reads when the settlement begins bettering one of its own works.
		/// </summary>
		public static string BegunLine(string PredecessorName, string SuccessorName, int Cost)
		{
			string predecessor = string.IsNullOrEmpty(PredecessorName) ? "work" : PredecessorName;
			string successor = string.IsNullOrEmpty(SuccessorName) ? "something better" : SuccessorName;
			return "The " + predecessor + " is being raised into " + Article(successor) + ". It costs "
				+ Cost + " " + ((Cost == 1) ? "dram" : "drams") + " from the stores.";
		}

		/// <summary>
		/// The notice a settlement gives the first time any of its works could be bettered, so
		/// no founder ever finds a thing changed without having been told the settlement does
		/// this and where to stop it.
		/// </summary>
		/// <param name="SeatName">What the settlement is called.</param>
		public static string FirstNoticeLine(string SeatName)
		{
			string seat = string.IsNullOrEmpty(SeatName) ? "the settlement" : SeatName;
			return seat + " has grown enough to better what it already built, and will do so out of "
				+ "what the stores can spare. (Charter: your works, and what they become.)";
		}

		/// <summary>
		/// English indefinite article for a display name, matching how the mod's prose reads
		/// object names. Kept here rather than borrowed from the engine so the rules file stays
		/// engine-free and this sentence is testable.
		/// </summary>
		public static string Article(string Name)
		{
			if (string.IsNullOrEmpty(Name))
			{
				return "something";
			}
			char first = Name[0];
			if (first == 'a' || first == 'e' || first == 'i' || first == 'o' || first == 'u'
				|| first == 'A' || first == 'E' || first == 'I' || first == 'O' || first == 'U')
			{
				return "an " + Name;
			}
			return "a " + Name;
		}

		/// <summary>
		/// Which of several designs sharing one blueprint a standing work counts as, when the work
		/// carries no stamped key and the only evidence left is the blueprint it was built from.
		/// <para>
		/// A design that declares a chain wins, because it is the only candidate that can answer
		/// the question being asked; among several that do, the first registered wins, which is the
		/// same load-order rule the registry resolves every other collision by. With no chain
		/// anywhere the first candidate is returned regardless: the key is still wanted for prose
		/// and for the cost arithmetic, and none of the candidates could grow into anything.
		/// </para>
		/// </summary>
		/// <param name="Chained">For each candidate design, in registry order, whether it declares
		/// an upgrade chain.</param>
		/// <returns>An index into that array, or -1 when no design matches the blueprint at all.
		/// </returns>
		public static int ChooseDesignIndex(bool[] Chained)
		{
			if (Chained == null || Chained.Length == 0)
			{
				return -1;
			}
			for (int i = 0; i < Chained.Length; i++)
			{
				if (Chained[i])
				{
					return i;
				}
			}
			return 0;
		}

		/// <summary>
		/// Reads one <c>&lt;building&gt;</c> entry's optional upgrade attributes. Every attribute
		/// is optional and an entry with none of them yields an undefined chain, which is what
		/// every design that shipped before this system existed has and why none of them changed
		/// behaviour.
		/// <para>
		/// Hostile input disables itself rather than half-registering: an attribute that names a
		/// cost, a time, a crew, or a stage without naming anything to grow into is an error, not
		/// a chain with a default successor, because guessing at the successor would improve a
		/// building into something its author never wrote.
		/// </para>
		/// </summary>
		/// <param name="Key">The design's own key, for the error text and the self-reference
		/// check.</param>
		/// <param name="UpgradesTo">Registry key of the successor design, or null.</param>
		/// <param name="UpgradeCost">Drams to charge, or null to compute.</param>
		/// <param name="UpgradeTicks">Ticks to take, or null to compute.</param>
		/// <param name="UpgradeCrew">Free hands to require, or null to compute.</param>
		/// <param name="UpgradeMinStage">Stage to wait for, or null to inherit the successor's.
		/// </param>
		/// <param name="Chain">The parsed chain. Undefined, never null, when this returns true
		/// for an entry that named no successor.</param>
		/// <param name="Error">Set to a log-facing reason when this returns false. The chain is
		/// null then, and the caller must register nothing.</param>
		/// <returns>False only for malformed input.</returns>
		public static bool TryParseUpgradeAttributes(string Key, string UpgradesTo, string UpgradeCost, string UpgradeTicks, string UpgradeCrew, string UpgradeMinStage, out UpgradeChain Chain, out string Error)
		{
			Chain = null;
			Error = null;
			string key = string.IsNullOrEmpty(Key) ? "(unnamed)" : Key;
			bool namesSomething = !string.IsNullOrEmpty(UpgradeCost) || !string.IsNullOrEmpty(UpgradeTicks)
				|| !string.IsNullOrEmpty(UpgradeCrew) || !string.IsNullOrEmpty(UpgradeMinStage);
			if (string.IsNullOrEmpty(UpgradesTo))
			{
				if (namesSomething)
				{
					Error = "building " + key + " has upgrade attributes but no UpgradesTo";
					return false;
				}
				Chain = new UpgradeChain();
				return true;
			}
			if (UpgradesTo == Key)
			{
				Error = "building " + key + " upgrades into itself";
				return false;
			}
			int cost = Unset;
			if (!string.IsNullOrEmpty(UpgradeCost) && (!int.TryParse(UpgradeCost, out cost) || cost < 0))
			{
				Error = "building " + key + " has a bad UpgradeCost";
				return false;
			}
			long ticks = UnsetTicks;
			if (!string.IsNullOrEmpty(UpgradeTicks) && (!long.TryParse(UpgradeTicks, out ticks) || ticks <= 0L))
			{
				Error = "building " + key + " has a bad UpgradeTicks";
				return false;
			}
			int crew = Unset;
			if (!string.IsNullOrEmpty(UpgradeCrew) && (!int.TryParse(UpgradeCrew, out crew) || crew < 0))
			{
				Error = "building " + key + " has a bad UpgradeCrew";
				return false;
			}
			GrowthStage stage = GrowthStage.Camp;
			bool hasStage = !string.IsNullOrEmpty(UpgradeMinStage);
			if (hasStage && (!System.Enum.TryParse<GrowthStage>(UpgradeMinStage, ignoreCase: true, out stage) || !KingdomRules.IsKnownStage(stage)))
			{
				// Enum.TryParse takes any number the underlying type can hold, so "7" parses into
				// a stage no settlement reaches; StageRequired would then gate the improvement
				// above every real stage and the founder would be told to wait for a city forever.
				Error = "building " + key + " has a bad UpgradeMinStage";
				return false;
			}
			Chain = new UpgradeChain
			{
				SuccessorKey = UpgradesTo,
				CostDramsOverride = (string.IsNullOrEmpty(UpgradeCost) ? Unset : cost),
				BuildTicksOverride = (string.IsNullOrEmpty(UpgradeTicks) ? UnsetTicks : ticks),
				CrewOverride = (string.IsNullOrEmpty(UpgradeCrew) ? Unset : crew),
				HasMinStageOverride = hasStage,
				MinStageOverride = stage
			};
			return true;
		}
	}
}
