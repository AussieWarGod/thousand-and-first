using System;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst
{
	/// <summary>
	/// The realm's one water manifest in flight between its two cities: drams already drawn
	/// from the origin's stores, waiting to be poured into the destination's the next time the
	/// founder stands on that city's ground and it activates.
	/// <para>
	/// Held by <see cref="KingdomSystem"/> directly, not by <see cref="KingdomSettlement"/> — it
	/// is realm-level state, like <c>Standings</c> and the chronicle registers, so it survives
	/// every seat swap untouched rather than travelling with whichever city happens to be seated.
	/// A manifest names its origin and destination by settlement name for exactly that reason:
	/// "seat" and "Away" are roles that swap, but the manifest is addressed to a place, not a role.
	/// </para>
	/// </summary>
	[Serializable]
	public class KingdomManifest
#if !TAF_TESTS
		: IComposite
#endif
	{
		/// <summary>The city the drams were physically drawn from.</summary>
		public string OriginName;

		/// <summary>The city the drams are addressed to. Delivery fires when this equals the
		/// currently seated city's own name.</summary>
		public string DestinationName;

		/// <summary>Drams actually drawn from the origin's stores at load time. Escrow, not a
		/// promise: this water already left the origin and belongs to no vessel until delivered.</summary>
		public int Drams;

		/// <summary>Tick the manifest was loaded.</summary>
		public long LoadedTick;

		/// <summary>Absolute tick past which the manifest is void if undelivered.</summary>
		public long DeadlineTick;

#if !TAF_TESTS
		public bool WantFieldReflection => false;

		public void Write(SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(KingdomManifest));
		}

		public void Read(SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(KingdomManifest));
		}
#endif
	}

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
		public const int ReserveUpkeepDays = 3;

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
			StoresCannotSpare
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
		public static ManifestVerdict JudgeManifest(bool OnClaimedGround, bool HasSecondCity, bool AlreadyInFlight, int Amount)
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

		/// <summary>What the founder is told directly when loading a new manifest discovers the
		/// old one lapsed unresolved. The realm's own chronicle carries the permanent record;
		/// this is the same news, told to the person standing there.</summary>
		public static string ManifestLapseNotice(string OriginName, string DestinationName, int Drams)
		{
			return "{{r|The manifest sent from " + OriginName + " toward " + DestinationName + " never reached the road's end. " + Drams + " drams are given up for lost.}} The road stands clear now; load another if you mean to.";
		}

		/// <summary>The chronicle clause written when a manifest's window closes before it is delivered.</summary>
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
