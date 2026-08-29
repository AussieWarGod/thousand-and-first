using System;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst
{
	/// <summary>
	/// The manifest's engine-free arithmetic: what it may carry, how long its window stays
	/// open, whether loading one now is allowed, and the prose for every outcome. Kept apart
	/// from <see cref="KingdomManifest"/> itself so every rule here is testable without the
	/// engine.
	/// </summary>
	public static class KingdomManifestRules
	{
		/// <summary>
		/// Days of upkeep a city keeps in reserve before any of its stored water is eligible to
		/// leave on a manifest. Matches the reserve <see cref="KingdomRules.ChoosePetition"/>
		/// already uses to decide a settlement is thirsty, so a manifest can never be the reason
		/// a city crosses into that state &mdash; interlocking with the thirst ladder rather than
		/// ignoring it.
		/// </summary>
		public const int ReserveUpkeepDays = KingdomRules.ReserveDays;

		/// <summary>Most a single manifest may carry, regardless of how much the origin has spare.</summary>
		public const int MaximumManifestDrams = 60;

		/// <summary>How many days a manifest's window stays open before it is given up as lost.
		/// Generous next to an ordinary walk between two claimed grounds, so only real neglect
		/// lets one lapse.</summary>
		public const long ManifestWindowDays = 10;

		public const long ManifestWindowTicks = KingdomRules.TicksPerDay * ManifestWindowDays;

		/// <summary>Drams a city must keep on hand before any may leave on a manifest.</summary>
		/// <param name="Population">The origin city's population.</param>
		public static int ManifestReserve(int Population)
		{
			return KingdomRules.UpkeepDrams(Population) * ReserveUpkeepDays;
		}

		/// <summary>
		/// How much a manifest loaded right now would carry: what the origin holds above its
		/// own reserve, capped at <see cref="MaximumManifestDrams"/>. Zero means the stores
		/// cannot spare any.
		/// </summary>
		/// <param name="StoredWater">Drams currently in the origin's dedicated stores.</param>
		/// <param name="Population">The origin city's population.</param>
		public static int ManifestAmount(int StoredWater, int Population)
		{
			int spare = StoredWater - ManifestReserve(Population);
			if (spare <= 0)
			{
				return 0;
			}
			return (spare > MaximumManifestDrams) ? MaximumManifestDrams : spare;
		}

		/// <summary>The absolute tick a manifest loaded now becomes void if undelivered.</summary>
		/// <param name="LoadedTick">The tick it was loaded.</param>
		public static long ManifestDeadline(long LoadedTick)
		{
			return LoadedTick + ManifestWindowTicks;
		}

		/// <summary>Whether a manifest's window has closed. Strictly past the deadline: arriving
		/// on the deadline tick itself still delivers.</summary>
		/// <param name="Now">Current tick.</param>
		/// <param name="DeadlineTick">The manifest's <see cref="ManifestDeadline"/>.</param>
		public static bool ManifestExpired(long Now, long DeadlineTick)
		{
			return Now > DeadlineTick;
		}

		/// <summary>Whole days left before a manifest's window closes, rounded up so any time
		/// remaining reads as at least one day rather than zero.</summary>
		/// <param name="Now">Current tick.</param>
		/// <param name="DeadlineTick">The manifest's <see cref="ManifestDeadline"/>.</param>
		public static long ManifestDaysLeft(long Now, long DeadlineTick)
		{
			long ticksLeft = DeadlineTick - Now;
			if (ticksLeft <= 0)
			{
				return 0;
			}
			return (ticksLeft + KingdomRules.TicksPerDay - 1) / KingdomRules.TicksPerDay;
		}

		/// <summary>Why loading a manifest right now may not proceed, or <see cref="Allowed"/>.
		/// Pure so the rule is tabled rather than discovered in the field.</summary>
		public enum ManifestVerdict
		{
			Allowed,
			NotOnClaimedGround,
			AlreadyInFlight,
			OnlyOneCity,
			StoresCannotSpare,
			DestinationHasNoRoom
		}

		/// <summary>
		/// Judges whether loading a manifest right now is allowed. Checked in the order a
		/// founder would actually hit the refusals: you must be standing where a manifest could
		/// leave from before anything else matters, an in-flight manifest blocks a second one
		/// regardless of how the realm is shaped, and only then do city count and the stores
		/// themselves come into it.
		/// </summary>
		/// <param name="OnClaimedGround">Whether the founder is standing in a claimed city.</param>
		/// <param name="HasSecondCity">Whether the realm holds a second city to address one to.</param>
		/// <param name="AlreadyInFlight">Whether the realm already holds an unresolved manifest.</param>
		/// <param name="Amount">What <see cref="ManifestAmount"/> would actually offer.</param>
		/// <summary>
		/// Caps a load by what the destination was last known to have room for.
		/// <para>
		/// A manifest is sized against BELIEF, not truth: the only figure the realm has for the
		/// other city is what it had room for when the founder last stood in it
		/// (<see cref="KingdomSettlement.LastKnownStorageSpace"/>). Loading to that figure makes
		/// arriving-with-nowhere-to-put-it rare and specific &mdash; it happens when the water
		/// level there changed while the load was on the road, which is a thing that happened
		/// rather than a rule that fired.
		/// </para>
		/// </summary>
		/// <param name="Amount">What the origin could spare.</param>
		/// <param name="DestinationSpace">Destination's last known free space. Zero refuses.</param>
		/// <returns>Drams to load; zero when there is no believed room.</returns>
		public static int CapToDestination(int Amount, int DestinationSpace)
		{
			if (Amount <= 0 || DestinationSpace <= 0)
			{
				return 0;
			}
			return (DestinationSpace < Amount) ? DestinationSpace : Amount;
		}

		public static ManifestVerdict JudgeManifest(bool OnClaimedGround, bool HasSecondCity, bool AlreadyInFlight, int Amount, int DestinationSpace = int.MaxValue)
		{
			if (!OnClaimedGround)
			{
				return ManifestVerdict.NotOnClaimedGround;
			}
			if (AlreadyInFlight)
			{
				return ManifestVerdict.AlreadyInFlight;
			}
			if (!HasSecondCity)
			{
				return ManifestVerdict.OnlyOneCity;
			}
			if (Amount <= 0)
			{
				return ManifestVerdict.StoresCannotSpare;
			}
			if (DestinationSpace <= 0)
			{
				return ManifestVerdict.DestinationHasNoRoom;
			}
			return ManifestVerdict.Allowed;
		}

		/// <summary>
		/// What the founder is told when a manifest will not load. Written as the water-keepers
		/// would say it, not as a rule. <see cref="ManifestVerdict.Allowed"/> and
		/// <see cref="ManifestVerdict.AlreadyInFlight"/> return empty; the in-flight refusal
		/// needs the standing manifest's own details and is composed by
		/// <see cref="ManifestInFlightStatus"/> instead.
		/// </summary>
		/// <param name="Verdict">The refusal.</param>
		/// <param name="DestinationName">The realm's second city's name, or null if it has none.</param>
		public static string ManifestRefusal(ManifestVerdict Verdict, string DestinationName)
		{
			switch (Verdict)
			{
			case ManifestVerdict.NotOnClaimedGround:
				return "Manifests are loaded standing on the kingdom's own ground.";
			case ManifestVerdict.OnlyOneCity:
				return "There is nowhere else of the realm's to send it. A manifest needs a second city to carry water toward.";
			case ManifestVerdict.StoresCannotSpare:
				return string.IsNullOrEmpty(DestinationName)
					? "The stores cannot spare enough to send onward and still keep this city fed."
					: ("The stores cannot spare enough to send toward " + DestinationName + " and still keep this city fed.");
			case ManifestVerdict.DestinationHasNoRoom:
				return string.IsNullOrEmpty(DestinationName)
					? "There was no room in the other city's casks when anyone here last saw them. Water sent now would arrive with nowhere to go."
					: ("There was no room in " + DestinationName + "'s casks when anyone here last stood in them. Water sent now would arrive with nowhere to go - raise storage there, or go and see for yourself.");
			default:
				return "";
			}
		}

		/// <summary>
		/// What the founder is told about the one manifest already on the road, in place of
		/// loading a second: where it left from, where it is bound, how much it carries, and how
		/// long the road still allows before the water is given up as lost.
		/// </summary>
		public static string ManifestInFlightStatus(string OriginName, string DestinationName, int Drams, long Now, long DeadlineTick)
		{
			long daysLeft = ManifestDaysLeft(Now, DeadlineTick);
			string window = (daysLeft <= 0)
				? "its road is nearly closed"
				: (daysLeft + ((daysLeft == 1) ? " day" : " days") + " left on the road");
			return "A manifest of " + Drams + " drams is already crossing from " + OriginName + " to " + DestinationName + " (" + window + "). Only one may travel at a time.";
		}

		/// <summary>The chronicle clause written when a manifest's window closes before it is delivered.</summary>
		/// <summary>
		/// The carters give up on the road and start for home. Written as a fact about the
		/// errand, not about the founder: absence is never a fault here, and the water is not
		/// lost &mdash; only the trip.
		/// </summary>
		public static string ManifestTurnedBackDeed(string OriginName, string DestinationName, int Drams)
		{
			return "the " + Drams + " drams bound for " + DestinationName + " waited on the road as long as carters will wait, and turned back toward " + OriginName;
		}

		public static string ManifestLapseDeed(string OriginName, string DestinationName, int Drams)
		{
			return Drams + " drams that " + OriginName + " sent toward " + DestinationName + " never arrived, and the water-keepers wrote it off as lost";
		}

		/// <summary>The chronicle clause written when a manifest reaches its destination.</summary>
		/// <param name="OriginName">Where the manifest left from.</param>
		/// <param name="DestinationName">Where it arrived.</param>
		/// <param name="Delivered">Drams actually poured into the destination's stores.</param>
		/// <param name="Sent">Drams the manifest carried. Less than this only when the
		/// destination's stores could not hold the whole delivery.</param>
		public static string ManifestArrivalDeed(string OriginName, string DestinationName, int Delivered, int Sent)
		{
			string core = Delivered + " drams sent from " + OriginName + " reached the stores of " + DestinationName;
			return (Delivered < Sent) ? (core + ", though not all of it could be held") : core;
		}

		/// <summary>The homecoming-ledger note for the same arrival.</summary>
		public static string ManifestArrivalNote(string OriginName, int Delivered, int Sent)
		{
			return "{{G|A manifest from " + OriginName + " arrived: " + Delivered + " drams"
				+ ((Delivered < Sent) ? ", and the stores overflowed" : "") + ".}}";
		}
	}
}
