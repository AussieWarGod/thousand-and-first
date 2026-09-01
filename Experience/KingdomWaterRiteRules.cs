using System;

namespace ThousandAndFirst
{
	/// <summary>
	/// The engine-free arithmetic and prose behind the rite of shared water held with one of the
	/// founder's own settlers &mdash; Addendum 5's diplomacy channel, and the only one that works
	/// on one named person at a time. <c>KingdomWaterRite</c> is the engine-coupled shell.
	/// <para>
	/// <b>The fiction, which is the design.</b> Qud's water ritual is the setting's central act:
	/// you share your water with a stranger and are water-bonded to them and to everything they
	/// belong to. This is that act turned inward. The founder fills the basin from the settlement's
	/// own stores, sets it in front of one named settler whose allegiance or belief differs from
	/// the realm's, and pours. What follows is theirs to decide. The founder never orders a
	/// transition, never changes two people at once, and never gets the water back.
	/// </para>
	/// <para>
	/// <b>No dice, anywhere in this file.</b> The two passive channels
	/// (<c>KingdomConversionRules</c>) end a long road with a kernel DRAW, and rightly: nobody
	/// decides to be talked around by their housemates, so whether a long shared life finally
	/// tells is a matter of chance. The rite is the opposite case. It is one person, asked to
	/// their face, answering a question they were invited to answer, and the answer follows from
	/// what actually stands between them and the realm. A founder who is told "not yet" is owed
	/// the same answer next time until something real has changed &mdash; and
	/// <see cref="SomethingChanged"/> is what makes that a rule rather than a hope.
	/// </para>
	/// <para>
	/// <b>Two shared livings, and they are not the same quantity.</b>
	/// <c>KingdomConversionRules.SharedLivingForConversion</c> counts shared living TOWARD ONE
	/// CREED: household-scoped, closeness-scaled, redirected the moment somebody moves house.
	/// <see cref="WaterRiteFacts.SharedDays"/> counts shared living WITH THE SETTLEMENT: how
	/// many attended passes this person has stood on this ground, whoever they sleep beside. The
	/// rite needs the second precisely because it exists to reach the people the first cannot
	/// &mdash; the settler in a quarter of their own, whom no household majority is pulling at.
	/// Both are counted in attended passes and neither reads a clock, which is the guarantee that
	/// matters and the one they share.
	/// </para>
	/// </summary>
	public static partial class KingdomWaterRiteRules
	{
		// ==================================================================================
		// The distance, and the reach. Everything in this block is denominated in the same
		// unit as Qud's own faction feelings, so a fault line and a rival shrine can be added
		// together and the sum still means something a person could say out loud.
		// ==================================================================================

		/// <summary>
		/// What it costs to cross from any covenant, allegiance, or belief to another, before anything particular is
		/// counted: twenty-four, which is six attended passes of shared living. Nobody changes what
		/// they hold over one cup with somebody they met last week, however friendly the two creeds
		/// are.
		/// </summary>
		public const int CovenantDistance = 24;

		/// <summary>
		/// Added when the settler holds a creed of their own rather than nothing in particular.
		/// Sixteen: leaving something is further than arriving from nowhere, and a settler who
		/// holds nothing is the easiest person in the settlement to share water with, which is
		/// as it should be.
		/// </summary>
		public const int CreedHeldDistance = 16;

		/// <summary>
		/// Added when a shrine consecrated to something other than the realm's creed stands in
		/// their quarter. Thirty: more than half the ambient grudge, because a consecrated
		/// building makes its argument every day and the basin makes its argument once.
		/// </summary>
		public const int RivalShrineDistance = 30;

		/// <summary>
		/// Added when belief is a thing they think about &mdash; their profile prefers the faith
		/// tag. Twenty, and deliberately a cost rather than a benefit: there is no settler it is
		/// <em>optimal</em> to convert, and a founder hunting for the cheapest soul finds only the
		/// one who cared least.
		/// </summary>
		public const int DevotionDistance = 20;

		/// <summary>Distance one attended pass of shared living covered before the clock rework.
		/// Four. Kept as the INPUT to the recalibration rather than deleted, so
		/// <see cref="MaxCountedDays"/> shows its own working.</summary>
		public const int ReachPerSharedPass = 4;

		/// <summary>
		/// The furthest a shared life reaches, however long it runs: a hundred and forty, which is
		/// exactly the distance to a settler holding a creed the realm's own creed files at the
		/// flat &minus;100 &mdash; the fault lines Addendum 4d says no shared roof will ever hold.
		/// So the water rite, and only the water rite, crosses a fault line: at the very end of a
		/// whole shared life, at the price of a small cistern, one soul at a time. That is the
		/// healing arc the ceiling was built to require, and this constant is where it lives.
		/// </summary>
		public const int ReachCap = 140;

		/// <summary>The road in the unit it used to be walked in: thirty-five attended passes, at
		/// four of reach apiece. The input to the recalibration.</summary>
		public const int SharedPassesForFullReach = ReachCap / ReachPerSharedPass;

		/// <summary>
		/// Cohabited days at which <see cref="Reach"/> stops rising: a hundred and five, which is
		/// <see cref="SharedPassesForFullReach"/> restated at
		/// <see cref="KingdomBrinkRules.CohabitationDaysPerAttendedPass"/>. Nothing above it ever
		/// means anything, which is why the shell stops counting there.
		/// <para>
		/// The old counter was already half a day counter &mdash; it refused to count a settler
		/// twice inside one day &mdash; so what changed is not the unit but who spends it: days
		/// now pass while the founder is away, and a founder who comes home at the cadence the
		/// design assumes walks exactly the hundred and five days that used to be thirty-five
		/// visits.
		/// </para>
		/// </summary>
		public const int MaxCountedDays = SharedPassesForFullReach * KingdomBrinkRules.CohabitationDaysPerAttendedPass;

		/// <summary>Distance one dram of the settlement's water is asked to carry. Four, so the
		/// price of the basin rises with what is in the way without ever becoming the reason a
		/// founder does not ask.</summary>
		public const int DistancePerDram = 4;

		/// <summary>
		/// Everything standing between this settler and the realm's creed, in one number, in the
		/// units of Qud's own faction table.
		/// </summary>
		/// <param name="Facts">The facts as they stand tonight. Hostility outside 0-100 and
		/// negative pass counts are clamped rather than rejected &mdash; hostile-input discipline,
		/// since the hostility ultimately comes out of third-party faction data.</param>
		/// <returns>At least <see cref="CovenantDistance"/>. Never negative.</returns>
		public static int Distance(WaterRiteFacts Facts)
		{
			int distance = CovenantDistance;
			if (Facts.HoldsACreed)
			{
				distance += CreedHeldDistance;
			}
			distance += Clamp(Facts.Hostility, 0, 100);
			if (Facts.RivalShrine)
			{
				distance += RivalShrineDistance;
			}
			if (Facts.Devout)
			{
				distance += DevotionDistance;
			}
			return distance;
		}

		/// <summary>How far a shared life has carried them, capped at <see cref="ReachCap"/>.
		/// Exact at every third day, because <see cref="ReachCap"/> over
		/// <see cref="MaxCountedDays"/> is four over three: three cohabited days buy the four this
		/// used to give an attended pass, and a hundred and five buy the whole of it.</summary>
		/// <param name="SharedDays">Cohabited days lived here. Negative reads as none.</param>
		public static int Reach(int SharedDays)
		{
			if (SharedDays <= 0)
			{
				return 0;
			}
			return (SharedDays >= MaxCountedDays) ? ReachCap : (SharedDays * ReachCap / MaxCountedDays);
		}

		/// <summary>
		/// Cohabited days at which this distance would be covered, or zero when no shared life
		/// would ever cover it. Recorded on a refusal so a second asking can tell "she has lived
		/// more of it since" apart from "you asked her twice"; never shown to the founder as a
		/// number, because a number on this would be a countdown and this is not a countdown.
		/// <para>
		/// Rounded up, so the day this names actually covers the distance rather than falling one
		/// point short of it &mdash; the inverse of <see cref="Reach"/>'s integer division, which
		/// would otherwise let a promise be kept a day before it was true.
		/// </para>
		/// </summary>
		/// <param name="Distance">From <see cref="Distance"/>.</param>
		public static int NeededDays(int Distance)
		{
			if (Distance <= 0 || Distance > ReachCap)
			{
				return 0;
			}
			return (int)(((long)Distance * MaxCountedDays + ReachCap - 1L) / ReachCap);
		}

		/// <summary>
		/// Drams of fresh water the rite asks of the dedicated stores: the founding basin's own
		/// eight (<c>KingdomRules.FoundingCostDrams</c>), plus a measure for whatever is in the
		/// way. The eight is not a coincidence and is not tunable apart from the founding &mdash;
		/// this is the founding rite held again, for one person, and it is priced as one.
		/// </summary>
		/// <param name="Distance">From <see cref="Distance"/>. Non-positive reads as nothing in
		/// the way and still costs the basin.</param>
		public static int Cost(int Distance)
		{
			int distance = (Distance > 0) ? Distance : 0;
			return KingdomRules.FoundingCostDrams + (distance / DistancePerDram);
		}

		/// <summary>
		/// What this settler says, given what stands between them and the realm.
		/// <para>
		/// The refusals are ordered by what the founder can do about them rather than by severity:
		/// a shrine they could strike this afternoon outranks a devotion nobody can do anything
		/// about, which outranks a shared life that simply has not been long enough yet, which
		/// outranks a quarrel between two creeds that no shared life would cross. Naming the wrong
		/// one is worse than naming none (STANDARDS 7b), so every branch names the obstacle whose
		/// removal would by itself have changed the answer.
		/// </para>
		/// </summary>
		/// <param name="Facts">The facts as they stand tonight.</param>
		/// <returns>Never throws; every combination of facts has an answer.</returns>
		public static WaterRiteAnswer Answer(WaterRiteFacts Facts)
		{
			if (Facts.Steadfast)
			{
				return WaterRiteAnswer.Steadfast;
			}
			int distance = Distance(Facts);
			int reach = Reach(Facts.SharedDays);
			if (reach >= distance)
			{
				return WaterRiteAnswer.Accepted;
			}
			if (Facts.RivalShrine && reach >= distance - RivalShrineDistance)
			{
				return WaterRiteAnswer.RivalShrine;
			}
			if (Facts.Devout && reach >= distance - DevotionDistance)
			{
				return WaterRiteAnswer.Devout;
			}
			return (ReachCap >= distance) ? WaterRiteAnswer.TooNew : WaterRiteAnswer.TooBitter;
		}

		/// <summary>Whether an answer converted anybody. The one place the rest of the mod should
		/// ask, so nothing has to know which enum values are refusals.</summary>
		public static bool Converted(WaterRiteAnswer Answer)
		{
			return Answer == WaterRiteAnswer.Accepted;
		}
	}
}
