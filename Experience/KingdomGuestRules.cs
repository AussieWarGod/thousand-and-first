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
	public static partial class KingdomGuestRules
	{
		/// <summary>Earliest current market-service standing at which a legendary trader considers
		/// the city a market rather than a camp stall. A staffed capable fixture, exact office, and
		/// vacant fine house remain required; this threshold never promises a ware tier.</summary>
		public const int LegendaryTraderMinimumShopTier = 3;

		/// <summary>The smallest actual lot a fine house may occupy for the luxury invitation.
		/// Matches the shipped fine house's declared M binding.</summary>
		public const KingdomPlotRules.PlotSize LegendaryTraderFineHouseTier =
			KingdomPlotRules.PlotSize.Medium;

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
		/// bed of the right tier is a real ask, and a notable who came this far waits for it.
		/// <para>
		/// Real elapsed time, running whether or not anybody is here to watch it run (Addendum 8
		/// clause 1). Still shorter than <see cref="NotableGuestIntervalTicks"/>, which is what
		/// makes at most one of them ever still be standing when the founder walks back in
		/// (<c>KingdomRules.PassagesThrough</c>); the rest left letters, and the letters are the
		/// dated trace an absence gets.
		/// </para>
		/// </summary>
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

	}
}
