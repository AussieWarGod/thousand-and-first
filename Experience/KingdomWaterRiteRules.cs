using System;

namespace ThousandAndFirst
{
	/// <summary>Why the founder may not put the basin down in front of this settler at all.</summary>
	public enum WaterRiteBar
	{
		/// <summary>Nothing stands in the way. The rite may be offered, and it will cost what
		/// <see cref="KingdomWaterRiteRules.Cost"/> says.</summary>
		Ready = 0,

		/// <summary>The founder is not standing on the settlement's own ground.</summary>
		NotOnOurGround = 1,

		/// <summary>The realm holds no creed of its own, so there is nothing to share water
		/// toward.</summary>
		RealmBelievesNothing = 2,

		/// <summary>They already hold what the realm holds. There is nothing between them.</summary>
		NothingBetweenYou = 3,

		/// <summary>They hold an office at the founder's word. A notable asked this by the person
		/// who named them is not being asked.</summary>
		TheirOffice = 4,

		/// <summary>They could not leave if they wanted to, so their yes would not be a yes.</summary>
		NoRoadOut = 5,

		/// <summary>They have been asked as many times as anyone should be. The question is shut
		/// for as long as the realm holds what it holds.</summary>
		AskedTooOften = 6,

		/// <summary>They answered, and nothing has changed since. See
		/// <see cref="KingdomWaterRiteRules.SomethingChanged"/>.</summary>
		AlreadyAnswered = 7,

		/// <summary>The founder poured for one of their own too recently.</summary>
		PouredTooRecently = 8,

		/// <summary>The dedicated stores cannot bear the drams.</summary>
		StoresCannotBear = 9
	}

	/// <summary>What one settler said, once the water was poured. Every value but
	/// <see cref="Accepted"/> costs the water all the same.</summary>
	public enum WaterRiteAnswer
	{
		/// <summary>They drank to it, and hold with the realm's creed from that evening on.</summary>
		Accepted = 0,

		/// <summary>They have not lived enough of this settlement's life to owe it a belief. The
		/// one refusal shared living alone will lift.</summary>
		TooNew = 1,

		/// <summary>A shrine consecrated to something else stands within sight of their own door,
		/// and it makes its argument every morning. The one refusal the founder can go and change
		/// today.</summary>
		RivalShrine = 2,

		/// <summary>They hold their own belief the way the founder holds the basin. Not a fault
		/// and not a fixable thing; the honest name for a road longer than it looks.</summary>
		Devout = 3,

		/// <summary>What stands between the two creeds is more than any shared life could cross.
		/// One of the two has to move.</summary>
		TooBitter = 4,

		/// <summary>They will not have belief put to them by anybody. An authored <c>Refuses</c>
		/// naming the faith tag, and absolute at every distance.</summary>
		Steadfast = 5
	}

	/// <summary>
	/// Everything that actually stands between one settler and the realm's creed, gathered by
	/// <c>KingdomWaterRite</c> off real people and real buildings and handed here as plain data.
	/// Nothing in this struct is a meter and nothing in it decays; each field is a fact about
	/// tonight.
	/// </summary>
	public readonly struct WaterRiteFacts
	{
		/// <summary>0-100, from <c>KingdomCreed.HostilityBetween</c> on their creed and the
		/// realm's. Zero for a settler who holds nothing, for two creeds that get on, and for a
		/// pair the engine has no opinion about.</summary>
		public readonly int Hostility;

		/// <summary>Attended passes this settler has been present for. See
		/// <see cref="KingdomWaterRiteRules.SharedDaysAfter"/> for what it is denominated in.</summary>
		public readonly int SharedDays;

		/// <summary>Whether they hold a creed of their own, as against holding nothing in
		/// particular. Crossing from a belief is further than crossing from none.</summary>
		public readonly bool HoldsACreed;

		/// <summary>Whether a shrine consecrated to something other than the realm's creed stands
		/// within <see cref="KingdomWaterRiteRules.QuarterRadiusCells"/> of their own door.</summary>
		public readonly bool RivalShrine;

		/// <summary>Whether their quality-of-life profile <em>prefers</em> the faith tag &mdash;
		/// belief is a thing they think about, so it is not a thing they trade.</summary>
		public readonly bool Devout;

		/// <summary>Whether their profile <em>refuses</em> the faith tag. Absolute at every
		/// distance, exactly as an authored <c>Refuses</c> is absolute at every closeness rung.
		/// </summary>
		public readonly bool Steadfast;

		/// <summary>The realm's own creed, as a faction name. What they are being asked to take,
		/// and the thing whose changing re-opens every closed door here.</summary>
		public readonly string RealmCreed;

		public WaterRiteFacts(int Hostility, int SharedDays, bool HoldsACreed, bool RivalShrine, bool Devout, bool Steadfast, string RealmCreed)
		{
			this.Hostility = Hostility;
			this.SharedDays = SharedDays;
			this.HoldsACreed = HoldsACreed;
			this.RivalShrine = RivalShrine;
			this.Devout = Devout;
			this.Steadfast = Steadfast;
			this.RealmCreed = RealmCreed;
		}
	}

	/// <summary>
	/// What one refusal recorded about the night it happened, so a second asking can be told apart
	/// from the same asking twice. Written onto the settler themselves and read back by
	/// <see cref="KingdomWaterRiteRules.SomethingChanged"/>; it holds no countdown and is shown to
	/// the founder as words rather than as numbers.
	/// </summary>
	public readonly struct WaterRiteStamp
	{
		/// <summary>The answer given, for the line that repeats it when the founder asks why the
		/// door is shut.</summary>
		public readonly WaterRiteAnswer Answer;

		/// <summary>Hostility as it stood that night. A fall re-opens the question.</summary>
		public readonly int Hostility;

		/// <summary>Whether a rival shrine stood in their quarter that night. Its going re-opens
		/// the question.</summary>
		public readonly bool RivalShrine;

		/// <summary>Whether nothing but a change of the realm's own creed can re-open this. Set
		/// for <see cref="WaterRiteAnswer.Steadfast"/> and for nothing else.</summary>
		public readonly bool Absolute;

		/// <summary>Shared passes at which their reach would have covered the distance, or zero
		/// when no shared life could. From <see cref="KingdomWaterRiteRules.NeededDays"/>.</summary>
		public readonly int NeededDays;

		/// <summary>The realm's creed as it stood that night. A different creed is a different
		/// question, and is always allowed to be asked.</summary>
		public readonly string RealmCreed;

		public WaterRiteStamp(WaterRiteAnswer Answer, int Hostility, bool RivalShrine, bool Absolute, int NeededDays, string RealmCreed)
		{
			this.Answer = Answer;
			this.Hostility = Hostility;
			this.RivalShrine = RivalShrine;
			this.Absolute = Absolute;
			this.NeededDays = NeededDays;
			this.RealmCreed = RealmCreed;
		}
	}

	/// <summary>
	/// The engine-free arithmetic and prose behind the rite of shared water held with one of the
	/// founder's own settlers &mdash; Addendum 5's diplomacy channel, and the only one that works
	/// on one named person at a time. <c>KingdomWaterRite</c> is the engine-coupled shell.
	/// <para>
	/// <b>The fiction, which is the design.</b> Qud's water ritual is the setting's central act:
	/// you share your water with a stranger and are water-bonded to them and to everything they
	/// belong to. This is that act turned inward. The founder fills the basin from the settlement's
	/// own stores, sets it in front of one named settler who does not believe what the realm
	/// believes, and pours. What follows is theirs to decide. The founder never orders a
	/// conversion, never converts two people at once, and never gets the water back.
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
	public static class KingdomWaterRiteRules
	{
		// ==================================================================================
		// The distance, and the reach. Everything in this block is denominated in the same
		// unit as Qud's own faction feelings, so a fault line and a rival shrine can be added
		// together and the sum still means something a person could say out loud.
		// ==================================================================================

		/// <summary>
		/// What it costs to cross from any belief to any other, before anything particular is
		/// counted: twenty-four, which is six attended passes of shared living. Nobody changes what
		/// they hold over one cup with somebody they met last week, however friendly the two creeds
		/// are.
		/// </summary>
		public const int CovenantDistance = 24;

		/// <summary>
		/// Added when the settler holds a creed of their own rather than nothing in particular.
		/// Sixteen: leaving something is further than arriving from nowhere, and a settler who
		/// believes nothing is the easiest person in the settlement to share water with, which is
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

		// ==================================================================================
		// Asked once, and not again until something is different. The rule that keeps this from
		// becoming a button the founder presses every visit until the dice fall right -- and
		// there are no dice here, so pressing it twice against an unchanged settlement would
		// produce the identical refusal at the identical price, which is the definition of a nag.
		// ==================================================================================

		/// <summary>Records what a refusal turned on, so <see cref="SomethingChanged"/> can tell a
		/// second question apart from the same question.</summary>
		/// <param name="Facts">The facts the answer was given against.</param>
		/// <param name="Answer">The answer given.</param>
		public static WaterRiteStamp StampFor(WaterRiteFacts Facts, WaterRiteAnswer Answer)
		{
			return new WaterRiteStamp(
				Answer,
				Clamp(Facts.Hostility, 0, 100),
				Facts.RivalShrine,
				Answer == WaterRiteAnswer.Steadfast,
				NeededDays(Distance(Facts)),
				Facts.RealmCreed);
		}

		/// <summary>
		/// Whether anything has changed since they answered that could honestly change the answer.
		/// False is the ordinary result, and means the Charter shows the row shut with the reason
		/// they gave rather than letting the founder buy the same refusal twice.
		/// <para>
		/// Four doors and no others. The realm believing something else is always a different
		/// question. A quarrel that has eased is a real change. A rival shrine that is gone is a
		/// real change. And a shared life grown long enough to cover the distance the refusal
		/// turned on is a real change &mdash; the only door that opens by itself, and it opens on
		/// attended passes, so it never opens while the founder is away.
		/// </para>
		/// </summary>
		/// <param name="Then">The stamp their refusal left.</param>
		/// <param name="Now">The facts as they stand tonight.</param>
		public static bool SomethingChanged(WaterRiteStamp Then, WaterRiteFacts Now)
		{
			if (!SameCreed(Then.RealmCreed, Now.RealmCreed))
			{
				return true;
			}
			if (Then.Absolute)
			{
				return false;
			}
			if (Clamp(Now.Hostility, 0, 100) < Then.Hostility)
			{
				return true;
			}
			if (Then.RivalShrine && !Now.RivalShrine)
			{
				return true;
			}
			return Then.NeededDays > 0 && Now.SharedDays >= Then.NeededDays;
		}

		/// <summary>Whether two creed faction names are the same belief. Null and empty are one
		/// another and both mean "holds nothing in particular", so a realm that recanted and a
		/// realm that never declared read alike.</summary>
		public static bool SameCreed(string A, string B)
		{
			bool aEmpty = string.IsNullOrEmpty(A);
			bool bEmpty = string.IsNullOrEmpty(B);
			if (aEmpty || bEmpty)
			{
				return aEmpty && bEmpty;
			}
			return string.Equals(A, B, StringComparison.Ordinal);
		}

		// ==================================================================================
		// Shared living with the settlement, counted in the days somebody has actually lived
		// here. Deliberately not a date: the roll records the day somebody arrived, and that day
		// is a fact about the calendar rather than about anything shared -- what this counts is
		// how much of this settlement's own life they have been part of.
		// ==================================================================================

		/// <summary>
		/// Shared living after a stretch of days lived here. Stops at
		/// <see cref="MaxCountedDays"/>, which is exactly where <see cref="Reach"/> stops rising,
		/// so the number never grows past the point of meaning anything.
		/// <para>
		/// The clock used to FORBID here and never cause: a settler could be counted at most once
		/// a day, but only an attended pass could count them at all. Addendum 8 clause 1 makes the
		/// days themselves the unit &mdash; a settler goes on living in the settlement whether or
		/// not the founder is standing in it &mdash; and the one-a-day gate survives as arithmetic
		/// rather than as a guard, because a stretch of elapsed time cannot yield more whole days
		/// than it contains.
		/// </para>
		/// <para>
		/// Nothing irreversible hangs off this counter, so it needs no brink of its own. It buys
		/// REACH, which only makes an invitation the founder must still extend and the settler
		/// must still accept more likely to be accepted; the rite's exit is its refusal counter,
		/// and its pressure surface is <c>KingdomConversion</c>'s. One exit, many feeders.
		/// </para>
		/// </summary>
		/// <param name="Held">Days so far. Negative reads as none.</param>
		/// <param name="Days">Whole days lived here since the last count, from
		/// <c>KingdomRules.ElapsedDays</c>. Non-positive changes nothing.</param>
		public static int SharedDaysAfter(int Held, int Days)
		{
			int held = (Held < 0) ? 0 : Held;
			if (Days <= 0)
			{
				return held;
			}
			long total = (long)held + Days;
			return (total >= MaxCountedDays) ? MaxCountedDays : (int)total;
		}

		// ==================================================================================
		// The exit, which is not optional: a settler may always emigrate rather than convert.
		//
		// One invitation is not pressure -- KingdomConversionRules.IsImposed says so, and says
		// it about this channel by name -- so a settler who is asked and says no is simply a
		// settler who said no. Being asked over and over IS pressure, and at the count below the
		// asking stops being a question the founder is allowed to keep putting. From that night
		// the rite is shut to them for as long as the realm holds what it holds, and the shell
		// hands them to KingdomConversion's own pressure surface, which is where every channel's
		// resented departure is named, graced and chronicled. There is one exit in this mod and
		// this file does not build a second one.
		// ==================================================================================

		/// <summary>
		/// Refusals after which the asking closes. Three: enough that a founder who was told "not
		/// yet" is not punished for asking again once something changed, few enough that
		/// persistence has a cost the person paying it can see coming.
		/// </summary>
		public const int RefusalsBeforeAskingCloses = 3;

		/// <summary>Whether a further asking would be one asking too many.</summary>
		public static bool AskedTooOften(int Refusals)
		{
			return Refusals >= RefusalsBeforeAskingCloses;
		}

		/// <summary>Refusals after one more. Clamped at the threshold, because past it the count
		/// stops meaning anything &mdash; the next asking closes the matter either way.</summary>
		public static int RefusalsAfter(int Refusals)
		{
			if (Refusals < 0)
			{
				return 1;
			}
			return (Refusals >= RefusalsBeforeAskingCloses) ? RefusalsBeforeAskingCloses : (Refusals + 1);
		}

		// ==================================================================================
		// The quarter. Addendum 4d's quarters emerge from the layout grammar with no code
		// knowing the word, so this is the only reading of "their quarter" the code can
		// honestly make: the ground within sight of their own door.
		//
		// Narrower than the shrine channel's own scope on purpose. KingdomFaith asks whether a
		// consecrated shrine stands in the SETTLEMENT, because that is a question about a
		// settlement. The rite asks whether one stands where THIS PERSON lives, because that is
		// a question about one person's evening, and a shrine across town is not in their ears
		// when the basin goes down.
		// ==================================================================================

		/// <summary>
		/// Cells from a settler's own door within which a consecrated building is in their quarter.
		/// Twelve: half a zone's twenty-five rows and a sixth of its eighty columns, which is a few
		/// streets rather than a city.
		/// </summary>
		public const int QuarterRadiusCells = 12;

		/// <summary>Whether a cell offset from a settler's door falls inside their quarter.
		/// Chebyshev, matching how the engine measures a neighbourhood on a grid.</summary>
		public static bool WithinQuarter(int DX, int DY)
		{
			int x = (DX < 0) ? -DX : DX;
			int y = (DY < 0) ? -DY : DY;
			return ((x > y) ? x : y) <= QuarterRadiusCells;
		}

		// ==================================================================================
		// Prose. Half the simulation. The founder is spoken to in the second person; a chronicle
		// line is a lower-case clause the register dates and closes; a rumour line is already in
		// the third person, because that register is not a translation of the founder's account
		// but a rival to it -- and must never contain the word "you", which
		// KingdomRules.ToThirdPerson would rewrite into the founder's own name.
		// ==================================================================================

		/// <summary>The founder-facing line for why the basin does not go down in front of this
		/// person. Never a complaint and never a countdown (STANDARDS 7b).</summary>
		/// <param name="Bar">The bar. <see cref="WaterRiteBar.Ready"/> returns empty.</param>
		/// <param name="Name">The settler's own name.</param>
		/// <param name="RealmCreedDisplay">The realm's creed, formatted, or null.</param>
		/// <param name="Drams">What the rite would have cost, for the stores bar.</param>
		/// <param name="Stored">What the dedicated stores hold, for the stores bar.</param>
		public static string BarLine(WaterRiteBar Bar, string Name, string RealmCreedDisplay, int Drams, int Stored)
		{
			string name = Named(Name);
			string creed = string.IsNullOrEmpty(RealmCreedDisplay) ? "anything in particular" : ("{{C|" + RealmCreedDisplay + "}}");
			switch (Bar)
			{
			case WaterRiteBar.NotOnOurGround:
				return "Water is shared on the settlement's own ground, in front of the people who live on it.";
			case WaterRiteBar.RealmBelievesNothing:
				return "Your realm holds with nothing in particular, and nobody can be asked to drink to that. Let one creed become the city's, or say one out loud, and then ask.";
			case WaterRiteBar.NothingBetweenYou:
				return name + " already holds with " + creed + ". You have shared water with " + name + " a hundred times over a cookfire; there is nothing here that wants a ceremony.";
			case WaterRiteBar.TheirOffice:
				return name + " holds office at your word. Put the basin down in front of them and they will drink to whatever you like, and it will mean nothing, and you will both know it.";
			case WaterRiteBar.NoRoadOut:
				return name + " has nowhere to go if the answer is no — the settlement is too small to let anybody walk. A yes from somebody you have left no room to refuse is not a yes. Ask when there are more of you.";
			case WaterRiteBar.AskedTooOften:
				return name + " has been asked this as many times as anyone should be. It is not a question any more; it is a thing being done to them. Let it alone while the city holds " + creed + ".";
			case WaterRiteBar.AlreadyAnswered:
				return name + " has answered, and nothing has changed since. Asking the same question twice is not asking twice.";
			case WaterRiteBar.PouredTooRecently:
				return "You poured for one of your own too recently. A rite held whenever it occurs to you is a round of drinks, and a round of drinks converts nobody.";
			case WaterRiteBar.StoresCannotBear:
				return "The rite would take {{C|" + Drams + " drams}} from the stores, and the stores hold {{C|" + Stored + "}}. Fill the casks first; this is not a thing to do by halves.";
			default:
				return "";
			}
		}

		/// <summary>
		/// The Charter's own row for one settler: their name, what they hold, and either the price
		/// or a shut door. Shut rows stay selectable, because a founder who picks one is owed the
		/// whole reason rather than a colour.
		/// </summary>
		/// <param name="Name">The settler's own name.</param>
		/// <param name="CreedDisplay">What they hold, formatted, or null for nothing in
		/// particular.</param>
		/// <param name="Drams">What the rite would cost. Meaningless unless <paramref name="Bar"/>
		/// is <see cref="WaterRiteBar.Ready"/>.</param>
		/// <param name="Bar">The bar standing against them, or <see cref="WaterRiteBar.Ready"/>.</param>
		/// <param name="Pressed">Whether a further asking would be the last one.</param>
		public static string RowLabel(string Name, string CreedDisplay, int Drams, WaterRiteBar Bar, bool Pressed)
		{
			string name = Named(Name);
			string holds = string.IsNullOrEmpty(CreedDisplay) ? "holds with nothing in particular" : ("holds with " + CreedDisplay);
			if (Bar != WaterRiteBar.Ready)
			{
				return "{{K|" + name + " — " + holds + "}}";
			}
			if (Pressed)
			{
				return "{{r|" + name + "}} — " + holds + " {{r|(asked, and asked, and asked)}} {{K|(" + Drams + " drams)}}";
			}
			return "{{W|" + name + "}} — " + holds + " {{K|(" + Drams + " drams)}}";
		}

		/// <summary>
		/// The consent modal: what the water costs, what is being asked, and &mdash; plainly
		/// &mdash; that the water is spent whichever way they answer. Every spend in this mod names
		/// its price before it is paid, and this one names two.
		/// </summary>
		/// <param name="Name">The settler's own name.</param>
		/// <param name="TheirCreedDisplay">What they hold, formatted, or null.</param>
		/// <param name="RealmCreedDisplay">What the realm holds, formatted.</param>
		/// <param name="Settlement">The city's name.</param>
		/// <param name="Drams">Drams the stores will give up.</param>
		public static string OfferPrompt(string Name, string TheirCreedDisplay, string RealmCreedDisplay, string Settlement, int Drams)
		{
			string name = Named(Name);
			string where = string.IsNullOrEmpty(Settlement) ? "this city" : ("{{C|" + Settlement + "}}");
			string realm = string.IsNullOrEmpty(RealmCreedDisplay) ? "what this city has come to hold" : ("{{C|" + RealmCreedDisplay + "}}");
			string theirs = string.IsNullOrEmpty(TheirCreedDisplay)
				? (name + " holds with nothing in particular.")
				: (name + " holds with {{C|" + TheirCreedDisplay + "}}.");
			return "You draw {{C|" + Drams + " drams}} from the stores of " + where + ", fill the basin, and set it down on the ground in front of " + name + ".\n\n"
				+ theirs + " " + where + " holds with " + realm + ".\n\n"
				+ "Nobody is ordered to drink. You pour, and you wait, and the water is gone either way.\n\n"
				+ "Pour?";
		}

		/// <summary>
		/// The warning appended to <see cref="OfferPrompt"/> when this asking would be the last
		/// one. States the consequence before it is bought, exactly as declaring a creed states its
		/// price.
		/// </summary>
		/// <param name="Name">The settler's own name.</param>
		/// <param name="WillTakeTheRoad">Whether they hold the realm's creed in enough dislike to
		/// leave over being made to hold it (<c>KingdomConversionRules.Resents</c>). A settler who
		/// merely differs stays, and is simply never asked again.</param>
		public static string PressedWarning(string Name, bool WillTakeTheRoad)
		{
			string name = Named(Name);
			string tail = WillTakeTheRoad
				? (name + " will start asking after the roads, and unless the city stops holding what it holds, " + name + " will take one.}}")
				: ("it will be the last time anybody puts it to " + name + ", and " + name + " will remember which of you kept asking.}}");
			return "\n\n{{r|" + name + " has answered you three times, and has begun looking at the ground while doing it. Ask a fourth time and it stops being a question: "
				+ tail;
		}

		/// <summary>The modal the founder reads when a settler drinks to it. No triumph in it: the
		/// fiction is that nobody was argued out of anything.</summary>
		/// <param name="Name">The settler's own name.</param>
		/// <param name="RealmCreedDisplay">What they hold now, formatted.</param>
		public static string AcceptNotice(string Name, string RealmCreedDisplay)
		{
			string name = Named(Name);
			string creed = string.IsNullOrEmpty(RealmCreedDisplay) ? "what this city holds" : ("{{C|" + RealmCreedDisplay + "}}");
			return name + " looks at the basin for a long moment, and then at you, and kneels, and drinks.\n\n"
				+ "Nobody was argued out of anything. The water was yours and " + name + " took it, and out here that is the whole of the thing.\n\n"
				+ name + " holds with " + creed + " from tonight. It will be in the book by morning, and it will be told wrong on the road by the end of the month.\n\n"
				+ "{{C|Live and drink.}}";
		}

		/// <summary>
		/// The modal the founder reads when a settler does not drink to it. One refusal per answer,
		/// each written to be worth reading, and each naming what would have to be different
		/// &mdash; which for two of them is nothing the founder can do, and says so rather than
		/// pretending otherwise.
		/// </summary>
		/// <param name="Answer">The answer. <see cref="WaterRiteAnswer.Accepted"/> returns empty.</param>
		/// <param name="Name">The settler's own name.</param>
		/// <param name="TheirCreedDisplay">What they hold, formatted, or null.</param>
		/// <param name="RealmCreedDisplay">What the realm holds, formatted, or null.</param>
		/// <param name="ShrineCreedDisplay">What the rival shrine is consecrated to, formatted, or
		/// null when the answer was not <see cref="WaterRiteAnswer.RivalShrine"/>.</param>
		public static string RefusalNotice(WaterRiteAnswer Answer, string Name, string TheirCreedDisplay, string RealmCreedDisplay, string ShrineCreedDisplay)
		{
			string name = Named(Name);
			string theirs = string.IsNullOrEmpty(TheirCreedDisplay) ? "what they came here holding" : ("{{C|" + TheirCreedDisplay + "}}");
			string realm = string.IsNullOrEmpty(RealmCreedDisplay) ? "what this city holds" : ("{{C|" + RealmCreedDisplay + "}}");
			string shrine = string.IsNullOrEmpty(ShrineCreedDisplay) ? "something else" : ("{{C|" + ShrineCreedDisplay + "}}");
			switch (Answer)
			{
			case WaterRiteAnswer.TooNew:
				return name + " takes the basin in both hands, drinks a good mouthful, and hands it back still half full.\n\n"
					+ "\"I have been here a season and you are asking me what I am. Ask me after I have carried water for this place in a bad year.\"\n\n"
					+ "Nothing is spoiled and nothing is owed. The water is spent, " + name + " is still yours, and " + name + " is still " + theirs + "'s. Ask again when " + name + " has lived more of this settlement's life than that.";
			case WaterRiteAnswer.RivalShrine:
				return name + " drinks, and does it looking past your shoulder the whole time.\n\n"
					+ "The shrine to " + shrine + " stands two streets from " + name + "'s own door, and it makes its argument every morning, and yours is being made once, tonight, on the ground, in a tin bowl.\n\n"
					+ "The water is spent. Take that shrine down, or consecrate it to something " + name + " could drink to, and ask again.";
			case WaterRiteAnswer.Devout:
				return name + " drinks, and thanks you for it, and then says the thing you were afraid of.\n\n"
					+ "\"You hold that basin the way I hold mine. Would you put yours down for a cup of somebody else's water?\"\n\n"
					+ "The water is spent, and " + name + " is not being difficult. Some people came here carrying something and did not come here to put it down. Nothing said tonight moves this. More years under the same roof might; the quarrel between the two creeds easing certainly would.";
			case WaterRiteAnswer.TooBitter:
				return name + " does not touch the basin. " + name + " does not have to; the pouring was the asking.\n\n"
					+ "What stands between " + theirs + " and " + realm + " was not started by either of you and will not be finished over a bowl. There is no shared life long enough to walk that, and " + name + " is not going to pretend there is.\n\n"
					+ "The water is spent. One of those two creeds has to move — this city's, or theirs — before there is anything here worth asking again.";
			case WaterRiteAnswer.Steadfast:
				return name + " lets the water sit where you put it, and does not look at it, and does not look away from you either.\n\n"
					+ "\"Ask me for anything else. Ask me to carry, ask me to stand a wall, ask me to die on it. Not this.\"\n\n"
					+ "This is not a thing " + name + " is going to be talked around, tonight or in ten years. The water is spent. Let it be the last time it is asked.";
			default:
				return "";
			}
		}

		/// <summary>The refusal as the founder's own book records it: the same dignity in the record
		/// that was in the room. Lower-case clause, no trailing period.</summary>
		/// <param name="Answer">The answer given. <see cref="WaterRiteAnswer.Accepted"/> returns
		/// empty &mdash; an acceptance is chronicled by <c>KingdomConversion.Convert</c>, which is
		/// the one path every conversion in the mod takes.</param>
		/// <param name="Name">The settler's own name.</param>
		/// <param name="Settlement">The city's name.</param>
		public static string RefusalTelling(WaterRiteAnswer Answer, string Name, string Settlement)
		{
			string name = Named(Name);
			string where = string.IsNullOrEmpty(Settlement) ? "the city" : Settlement;
			switch (Answer)
			{
			case WaterRiteAnswer.TooNew:
				return "the basin was set down in front of " + name + " at " + where + ", and " + name + " drank half of it and asked to be asked again in a harder year";
			case WaterRiteAnswer.RivalShrine:
				return "the basin was set down in front of " + name + " at " + where + ", and " + name + " drank it looking at a shrine that was not the city's";
			case WaterRiteAnswer.Devout:
				return "the basin was set down in front of " + name + " at " + where + ", and " + name + " drank, and thanked the founder, and kept what " + name + " had come with";
			case WaterRiteAnswer.TooBitter:
				return "the basin was set down in front of " + name + " at " + where + " and was not touched, and nobody in the room thought worse of " + name + " for it";
			case WaterRiteAnswer.Steadfast:
				return "the basin was set down in front of " + name + " at " + where + ", and " + name + " said that this was the one thing that would not be asked of " + name + " again";
			default:
				return "";
			}
		}

		/// <summary>
		/// The same night as the roads tell it. Third person already, and arguing: the founder's
		/// book records that somebody was asked and said no, and the roads record that saying no
		/// cost nothing, which is the half nobody in a harder country believes.
		/// </summary>
		/// <param name="Name">The settler's own name.</param>
		/// <param name="Settlement">The city's name.</param>
		/// <param name="FounderName">The founder as strangers would name them.</param>
		public static string RefusalRumour(string Name, string Settlement, string FounderName)
		{
			string name = Named(Name);
			string where = string.IsNullOrEmpty(Settlement) ? "that city" : Settlement;
			string founder = string.IsNullOrEmpty(FounderName) ? "the founder" : FounderName;
			return name + " of " + where + " told " + founder + " no, in " + founder + "'s own city, with " + founder + "'s own water going cold in the bowl — and walked out of it whole, which is the part that gets left off";
		}

		/// <summary>The modal the founder reads on the asking that closes the matter. States plainly
		/// what is still true &mdash; nothing was taken from them and nothing is being taken now
		/// &mdash; because that is what makes this a wound and not a punishment.</summary>
		/// <param name="Name">The settler's own name.</param>
		/// <param name="Settlement">The city's name.</param>
		/// <param name="WillTakeTheRoad">Whether they resent the creed enough to leave over it.</param>
		public static string ClosedNotice(string Name, string Settlement, bool WillTakeTheRoad)
		{
			string name = Named(Name);
			string where = string.IsNullOrEmpty(Settlement) ? "the city" : ("{{C|" + Settlement + "}}");
			string opening = "You set the basin down, and " + name + " does not look at it.\n\n"
				+ "\"That is four times. I gave you an answer each time and you kept the water coming.\"\n\n";
			if (!WillTakeTheRoad)
			{
				return opening + name + " drinks, because it is water and there is a drought on somewhere. Nothing is taken from " + name + " and nothing changes tonight.\n\n"
					+ "{{K|But that was the last time it will be put to " + name + " while " + where + " holds what it holds.}}";
			}
			return opening + name + " is not driven out and nothing of theirs is taken. They will start asking travellers where the good roads are, in the open, where you can see them do it.\n\n"
				+ "{{r|Take it out of their quarter and they stay. Leave it standing and they go, and the book will say why.}}";
		}

		/// <summary>The asking that closed the matter, as the founder's own book records it. Written
		/// on the night rather than on the pass anybody acts, because the founder should be able to
		/// find the night they went one asking too far.</summary>
		/// <param name="Name">The settler's own name.</param>
		/// <param name="Settlement">The city's name.</param>
		public static string ClosedTelling(string Name, string Settlement)
		{
			string name = Named(Name);
			string where = string.IsNullOrEmpty(Settlement) ? "the city" : Settlement;
			return "the basin was set down in front of " + name + " at " + where + " a fourth time, and " + name + " counted the askings out loud";
		}

		/// <summary>The same night as the roads tell it. See <see cref="RefusalRumour"/>.</summary>
		public static string ClosedRumour(string Name, string Settlement, string FounderName)
		{
			string name = Named(Name);
			string where = string.IsNullOrEmpty(Settlement) ? "that city" : Settlement;
			string founder = string.IsNullOrEmpty(FounderName) ? "the founder" : FounderName;
			return "in " + where + " they ask a settler what a settler believes, and then they ask again, and " + name + " is the one who counted the askings out loud — which " + founder + " tells as a misunderstanding over a bowl";
		}

		/// <summary>The founder-facing note for the same night, in the ledger's voice: what can
		/// still be done about it (STANDARDS 7b).</summary>
		/// <param name="Name">The settler's own name.</param>
		/// <param name="RealmCreedDisplay">The creed being put to them, formatted.</param>
		public static string ClosedNote(string Name, string RealmCreedDisplay)
		{
			string name = Named(Name);
			string creed = string.IsNullOrEmpty(RealmCreedDisplay) ? "what the city holds" : RealmCreedDisplay;
			return name + " has been asked about " + creed + " once too often, and will not be asked again while the city holds it.";
		}

		/// <summary>A settler's own name, or the honest fallback for somebody the roll does not
		/// carry. Repeated rather than pronouned throughout this file: the roll carries no gender,
		/// and a wrong pronoun in a line about somebody's belief reads worse than a repeat.</summary>
		private static string Named(string Name)
		{
			return string.IsNullOrEmpty(Name) ? "a settler" : Name;
		}

		private static int Clamp(int Value, int Low, int High)
		{
			if (Value < Low)
			{
				return Low;
			}
			return (Value > High) ? High : Value;
		}
	}
}
