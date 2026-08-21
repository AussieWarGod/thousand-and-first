using System.Collections.Generic;
using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	/// <summary>
	/// The five ways a settler's creed can change, as Addendum 5 names them. The value is also the
	/// kernel <c>eventKindCode</c> each channel draws on, so it must never be zero and must never
	/// be renumbered: a renumbering would re-roll every conversion that has not happened yet.
	/// </summary>
	public enum ConversionChannel
	{
		/// <summary>The household majority, counted in attended passes under one roof.</summary>
		Osmosis = 1,

		/// <summary>The witnessed shared meal, nudging attendees toward the table's majority.
		/// </summary>
		Culture = 2,

		/// <summary>A shrine consecrated to a creed, staffed, working on its own quarter.</summary>
		Shrine = 3,

		/// <summary>The water ritual with one's own settler: invited, consented, one at a time.
		/// </summary>
		Diplomacy = 4
	}

	/// <summary>
	/// How far along one settler is toward holding with somebody else's creed, and which creed
	/// that is. Immutable: every transition returns a new value (<see cref="KingdomConversionRules.Advance"/>),
	/// because a half-converted soul that can be mutated in place is a soul two callers can
	/// disagree about.
	/// </summary>
	public readonly struct ConversionProgress
	{
		/// <summary>The creed this settler is being pulled toward, or null when nothing is pulling
		/// at them &mdash; which is nearly everyone, nearly always.</summary>
		public readonly string Creed;

		/// <summary>Shared living accumulated toward <see cref="Creed"/>, in the units
		/// <see cref="KingdomConversionRules.SharedLivingForConversion"/> is denominated in. Never
		/// negative.</summary>
		public readonly int Shared;

		public ConversionProgress(string Creed, int Shared)
		{
			this.Creed = string.IsNullOrEmpty(Creed) ? null : Creed;
			this.Shared = (Shared < 0 || this.Creed == null) ? 0 : Shared;
		}

		/// <summary>Nobody is pulling at this settler. The state every settler starts in and
		/// returns to.</summary>
		public static ConversionProgress None
		{
			get { return new ConversionProgress(null, 0); }
		}

		/// <summary>Whether anything at all is pulling at this settler.</summary>
		public bool Any
		{
			get { return Creed != null && Shared > 0; }
		}
	}

	/// <summary>
	/// Conversion: the long road back from the fault-line ceiling (Addendum 4d) that partitioned
	/// the city into quarters, and the guard that keeps that road from ever becoming a corridor.
	/// <para>
	/// <b>Two passive channels live here.</b> <i>Osmosis</i> is the household majority pulling at
	/// the minority, counted in SHARED LIVING &mdash; attended passes spent under one roof &mdash;
	/// and scaled by the closeness ladder, because how much of a difference a household can cross
	/// is a property of its architecture and of nothing else. <i>Culture</i> is the witnessed
	/// shared meal nudging its attendees toward the table's majority: small, capped at
	/// <see cref="MealCeilingPercent"/> of the road, and a free rider on a ceremony the founder
	/// was already holding. Neither is a meter, neither reads a clock, and neither advances by so
	/// much as a point while the founder is away.
	/// </para>
	/// <para>
	/// <b>Time is labour, never maturation.</b> Every figure below is a count of attended passes
	/// or of meals actually held. Nothing here converts a tick into progress, so a settlement left
	/// alone for a season comes back holding exactly the beliefs it held when it was left. That is
	/// the same guarantee <c>KingdomLodgingRules</c>' grace makes, made the same way: nothing
	/// elapses on its own, because nothing here consults anything that elapses.
	/// </para>
	/// <para>
	/// <b>A settler may always emigrate rather than convert.</b> Osmosis and the shared table are
	/// chosen proximity and generate no pressure at all: living beside someone is not being made
	/// to agree with them, and Addendum 5 wants exactly the Roomed household across an ambient
	/// grudge to blend. What generates pressure is an IMPOSED creed &mdash; a realm declaration
	/// against theirs, a rival shrine consecrated in their quarter &mdash; and a settler who
	/// resents one takes the road instead of the creed: named once (STANDARDS 7b), given
	/// <see cref="ResentedPasses"/> attended passes for the founder to take the pressure off, and
	/// then gone through the settlement's ordinary emigration, chronicled by name and cause in
	/// both registers. The covenant is drunk, never administered.
	/// </para>
	/// <para>
	/// <b>Rare, and never re-rolled.</b> Reaching the end of the road buys a DRAW, not a
	/// conversion: <see cref="ConversionChancePercent"/> at each milestone, taken through
	/// <see cref="CounterRandom"/> on a key that names the settlement, the channel, the person and
	/// which milestone this is. The same milestone always answers the same way, in any process,
	/// forever &mdash; a reload never re-rolls a soul, and a founder cannot save-scum a
	/// conversion.
	/// </para>
	/// <para>
	/// Engine-free, so the whole of it is tabled. Which people share which roof, and which of them
	/// hold which creed, is <c>KingdomConversion</c>'s to gather.
	/// </para>
	/// </summary>
	public static class KingdomConversionRules
	{
		// ==================================================================================
		// Addendum 5, the osmosis channel: shared living, counted in attended passes.
		//
		// The unit is deliberately not a day, an hour or a tick. It is a pass -- one arrival of
		// the founder on the settlement's own ground -- and it is spent by the lodging pass and
		// by nothing else. That is the whole of "never absence-driven": there is no code path
		// anywhere in this file or its caller that turns elapsed time into progress, so a
		// season away and three days away buy identically nothing.
		// ==================================================================================

		/// <summary>
		/// Shared living one settler must accumulate before the road even offers them a draw.
		/// Seventy-two, which at the ladder below is twenty-four attended passes in a hut,
		/// thirty-six in a stone house, and seventy-two in a fine house &mdash; a season of coming
		/// home, never a week of it, and that is before the draw is asked for at all.
		/// </summary>
		public const int SharedLivingForConversion = 72;

		/// <summary>
		/// Shared living a <see cref="KingdomLodgingRules.Closeness.Packed"/> household buys in a
		/// pass: nothing, and by author's ruling rather than by arithmetic. One open room refuses
		/// every filed feeling (<c>KingdomLodgingRules.PackedRefusalHostility</c> is one), so the
		/// people in it already agree by construction. There is nothing there to cross, and a
		/// bunk row must never become a cheap conversion engine the founder builds on purpose.
		/// </summary>
		public const int PackedSharedPerPass = 0;

		/// <summary>
		/// Shared living a <see cref="KingdomLodgingRules.Closeness.Close"/> household buys in a
		/// pass: three, the fastest rung there is. A hut is one household's worth of walls and a
		/// door that shuts, and it holds only mild differences (it refuses the ambient grudge), so
		/// what it converts it converts quickly.
		/// </summary>
		public const int CloseSharedPerPass = 3;

		/// <summary>
		/// Shared living a <see cref="KingdomLodgingRules.Closeness.Roomed"/> household buys in a
		/// pass: two. This is the rung Addendum 5 is about &mdash; the stone house is the only
		/// architecture that will hold an ambient grudge under one roof at all, so it is the only
		/// place a real difference gets crossed. Slower than the hut because the walls that make
		/// it possible are also walls.
		/// </summary>
		public const int RoomedSharedPerPass = 2;

		/// <summary>
		/// Shared living a <see cref="KingdomLodgingRules.Closeness.Private"/> household buys in a
		/// pass: one. A household that meets at dinner because it chose to converts slowest of
		/// all, which is the honest reading of quarters of one's own and is also the point:
		/// the fine house's value is quality and notables, never a tool for processing people.
		/// </summary>
		public const int PrivateSharedPerPass = 1;

		/// <summary>
		/// Shared living one witnessed meal buys each attendee, before the ceiling: four. Small on
		/// purpose &mdash; a meal is a good evening, not a policy, exactly as
		/// <c>KingdomCreedRules.MealEase</c> is smaller than a rite.
		/// </summary>
		public const int MealShared = 4;

		/// <summary>
		/// Share of the road meals may ever carry a settler along: half. Culture nudges;
		/// architecture converts. A settler who never shares a roof with the creed pulling at them
		/// &mdash; unhoused, or in a Packed household &mdash; can be fed to the ceiling and will
		/// stop there for good, which is Addendum 5's ordering ("architecture manages difference;
		/// practice converts it") made unforgeable rather than merely intended.
		/// </summary>
		public const int MealCeilingPercent = 50;

		/// <summary>
		/// Chance, in percent, that reaching a milestone of shared living actually turns somebody.
		/// Forty: rare enough that a conversion is a chronicle entry rather than a schedule, and
		/// short of certain so that the founder who builds the stone house is buying a real
		/// possibility rather than commissioning a result.
		/// </summary>
		public const int ConversionChancePercent = 40;

		/// <summary>
		/// Shared living meals may ever supply, from <see cref="MealCeilingPercent"/> of
		/// <see cref="SharedLivingForConversion"/>.
		/// </summary>
		public static int MealCeiling
		{
			get { return SharedLivingForConversion * MealCeilingPercent / 100; }
		}

		/// <summary>
		/// Shared living one attended pass under one roof buys, against the closeness ladder and
		/// the creed feeling between the two people.
		/// <para>
		/// <b>No conversion across a refusal.</b> You do not convert somebody you will not live
		/// beside: a hostility the quarters would refuse buys nothing at all, at every rung. The
		/// lodging pass already refuses to seat such a pair, so this is the same law stated where
		/// it can be tested rather than inferred from an assignment that happens to be correct.
		/// </para>
		/// </summary>
		/// <param name="Quarters">The home's rung, from <c>KingdomLodging.QuartersOf</c>.</param>
		/// <param name="Hostility">0-100 between the settler's creed and the creed pulling at
		/// them, from <c>KingdomCreed.HostilityBetween</c>.</param>
		/// <returns>Points of shared living; zero for Packed quarters and for any pair the
		/// quarters would refuse.</returns>
		public static int SharedLivingPerPass(KingdomLodgingRules.Closeness Quarters, int Hostility)
		{
			if (Hostility >= KingdomLodgingRules.RefusalHostility(Quarters))
			{
				return 0;
			}
			switch (Quarters)
			{
			case KingdomLodgingRules.Closeness.Close:
				return CloseSharedPerPass;
			case KingdomLodgingRules.Closeness.Roomed:
				return RoomedSharedPerPass;
			case KingdomLodgingRules.Closeness.Private:
				return PrivateSharedPerPass;
			default:
				return PackedSharedPerPass;
			}
		}

		/// <summary>
		/// Shared living one witnessed meal buys a settler who is already this far along, clamped
		/// so meals can carry somebody to exactly <see cref="MealCeiling"/> and never one point
		/// past it.
		/// </summary>
		/// <param name="Shared">Shared living already accumulated toward the table's creed.
		/// Negative reads as none.</param>
		/// <returns>Zero once the ceiling is reached, so a settlement that eats every night still
		/// cannot eat its way to a conversion.</returns>
		public static int MealSharedFor(int Shared)
		{
			int held = (Shared < 0) ? 0 : Shared;
			int room = MealCeiling - held;
			if (room <= 0)
			{
				return 0;
			}
			return (room < MealShared) ? room : MealShared;
		}

		/// <summary>
		/// The creed a household holds by majority, which is the creed it pulls its minority
		/// toward.
		/// <para>
		/// A STRICT majority of everyone under the roof, believers and ordinary settlers alike:
		/// two of three, three of five. Not a plurality and not
		/// <c>KingdomCreedRules.DominantSharePercent</c>'s third &mdash; a city is known by what a
		/// third of it holds, but a household only pulls when the people in it are actually
		/// outnumbering somebody. A tie has no winner, which is also the only answer that does not
		/// depend on the order a dictionary enumerates in.
		/// </para>
		/// </summary>
		/// <param name="Counts">Creed name to occupants holding it, over one household. Null,
		/// empty and non-positive entries all read as nobody.</param>
		/// <param name="Household">Everybody under the roof, including the creedless and including
		/// the settler being asked about.</param>
		/// <returns>The majority creed's name, or null when no creed holds one.</returns>
		public static string HouseholdMajority(IDictionary<string, int> Counts, int Household)
		{
			if (Counts == null || Household <= 0)
			{
				return null;
			}
			string best = null;
			foreach (KeyValuePair<string, int> entry in Counts)
			{
				if (string.IsNullOrEmpty(entry.Key) || entry.Value <= 0)
				{
					continue;
				}
				if (entry.Value * 2 > Household)
				{
					best = entry.Key;
				}
			}
			return best;
		}

		/// <summary>
		/// One settler's progress after a pull of <paramref name="Points"/> toward
		/// <paramref name="TowardCreed"/>.
		/// <para>
		/// <b>A settler pulled two ways is pulled nowhere.</b> When the pull names a different
		/// creed than the one already working on them, it does not replace it and does not start a
		/// second tally: it takes the same number of points back off the first. A citizen who
		/// sleeps in a Barathrumite house and eats at a Templar table converts to neither, and
		/// when the two cancel out entirely the slot is simply free again &mdash; the remainder is
		/// discarded rather than flipped, because winning a tug of war does not happen in the pass
		/// you win it.
		/// </para>
		/// </summary>
		/// <param name="Current">Progress now. <c>default</c> and
		/// <see cref="ConversionProgress.None"/> both read as nothing pulling.</param>
		/// <param name="TowardCreed">The creed pulling this pass. Null or empty changes
		/// nothing.</param>
		/// <param name="Points">Points the pull is worth. Non-positive changes nothing.</param>
		public static ConversionProgress Advance(ConversionProgress Current, string TowardCreed, int Points)
		{
			if (string.IsNullOrEmpty(TowardCreed) || Points <= 0)
			{
				return Current;
			}
			if (Current.Creed == null || Current.Shared <= 0)
			{
				return new ConversionProgress(TowardCreed, Points);
			}
			if (Current.Creed == TowardCreed)
			{
				return new ConversionProgress(TowardCreed, Current.Shared + Points);
			}
			int left = Current.Shared - Points;
			return (left > 0) ? new ConversionProgress(Current.Creed, left) : ConversionProgress.None;
		}

		/// <summary>Whether this settler has reached a milestone of shared living and the draw is
		/// owed. False for anyone short of the first one.</summary>
		public static bool AtMilestone(int Shared)
		{
			return Shared >= SharedLivingForConversion;
		}

		/// <summary>
		/// Which milestone this much shared living stands at &mdash; the kernel ordinal the draw
		/// is keyed on, so every pass between one milestone and the next asks the identical
		/// question and receives the identical answer. A milestone that answered no is not asked
		/// again until the settler has earned a whole further
		/// <see cref="SharedLivingForConversion"/>.
		/// </summary>
		public static ulong Milestone(int Shared)
		{
			return (Shared < SharedLivingForConversion) ? 0uL : (ulong)(Shared / SharedLivingForConversion);
		}

		// ==================================================================================
		// The draw. Counter-based, on a key that names the settlement, the channel, the person
		// and the milestone, so the answer is a pure function of those four and of nothing that
		// happened in between. An ordinary pseudorandom call could not promise that: its cursor
		// depends on every unrelated roll made since the game started, so a reload would convert
		// somebody the last session left alone.
		// ==================================================================================

		/// <summary>
		/// Rules version pinned into every conversion draw's <see cref="SemanticEventKey"/>. The
		/// key owns its rules version forever, so this moves only if the draw itself is redefined
		/// in a way that must not compare equal to what came before &mdash; which would re-answer
		/// every milestone standing unconverted in every save.
		/// </summary>
		private const int ConversionRulesVersion = 1;

		/// <summary>Only draw made on a conversion key. Named rather than passed as a bare
		/// literal because a second purpose on this key would have to pick the next index, not
		/// reuse this one.</summary>
		private const uint ConversionDrawIndex = 0u;

		/// <summary>
		/// Fixed, all-zero seed, for the reason <c>KingdomChronicle</c> gives at length: domain
		/// separation comes entirely from the settlement id, stream, kind and ordinal baked into
		/// the key, and whether a particular settler turns does not need to be unguessable.
		/// </summary>
		private static readonly KernelSeed128 ConversionSeed = default(KernelSeed128);

		private const string StreamPrefix = "taf:conversion:";

		private const string StreamSuffix = ":v1";

		/// <summary>
		/// Folds one settler's roll name into the frozen <c>taf:</c> semantic-id grammar so it can
		/// name its own ordinal lane. The person belongs in the STREAM rather than the ordinal:
		/// the ordinal is the milestone, and two settlers at the same milestone in the same city
		/// must not be forced to share one answer.
		/// <para>
		/// Not supported API (STANDARDS.md &sect;9): the grammar it folds into is frozen, but
		/// which string this mod hands the kernel is ours to change.
		/// </para>
		/// </summary>
		/// <param name="ResidentName">The name the roll carries them under. Null and blank yield
		/// the lane an unnamed settler would draw on, which nothing in production ever asks for
		/// &mdash; conversion is keyed to the roll, so an unnamed resident never enters it.</param>
		/// <returns>An id that always satisfies the <c>taf:</c> grammar. Never null.</returns>
		internal static string ResidentStream(string ResidentName)
		{
			StringBuilder builder = new StringBuilder(StreamPrefix);
			int room = KernelSemanticIdBudget - StreamPrefix.Length - StreamSuffix.Length;
			if (!string.IsNullOrEmpty(ResidentName))
			{
				foreach (char c in ResidentName)
				{
					if (builder.Length - StreamPrefix.Length >= room)
					{
						break;
					}
					if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-')
					{
						builder.Append(c);
					}
					else if (c >= 'A' && c <= 'Z')
					{
						builder.Append((char)(c + 32));
					}
					else
					{
						builder.Append('-');
					}
				}
			}
			if (builder.Length == StreamPrefix.Length)
			{
				builder.Append("unnamed");
			}
			builder.Append(StreamSuffix);
			return builder.ToString();
		}

		/// <summary>The byte budget <c>KernelSemanticId</c> allows an id. Stated here rather than
		/// read from the kernel because that constant is the kernel's own and this file must fold
		/// to fit it, not reach into it.</summary>
		private const int KernelSemanticIdBudget = 128;

		/// <summary>
		/// Whether this milestone turns this settler.
		/// <para>
		/// Deterministic in the settlement, the channel, the person and the milestone together:
		/// the same milestone always answers the same way, in any process, forever. Asking twice
		/// in one pass, reloading the save, or converting somebody else first cannot change it.
		/// </para>
		/// </summary>
		/// <param name="SettlementId">The settlement's kernel id
		/// (<c>KingdomChronicle.SettlementId</c>).</param>
		/// <param name="Channel">Which channel is asking. Each has its own kind code, so a shrine
		/// and a household working on the same person at the same milestone draw independently.
		/// </param>
		/// <param name="ResidentName">The name the roll carries them under.</param>
		/// <param name="Shared">Shared living accumulated toward the creed in question.</param>
		/// <returns>False when the settler is short of a milestone, and false when the kernel
		/// refuses the draw &mdash; a malformed id, or a machine whose crypto provider is failing.
		/// Failing closed is the only safe direction here: a fault must never be able to change
		/// what somebody believes.</returns>
		public static bool Converts(string SettlementId, ConversionChannel Channel, string ResidentName, int Shared)
		{
			if (!AtMilestone(Shared))
			{
				return false;
			}
			SemanticEventKey key;
			KernelFaultCode fault;
			if (!SemanticEventKey.TryCreate(ConversionRulesVersion, SettlementId, ResidentStream(ResidentName), (uint)Channel, Milestone(Shared), out key, out fault))
			{
				return false;
			}
			ulong value;
			if (!CounterRandom.TryDrawBelow(ConversionSeed, key, ConversionDrawIndex, 100uL, out value, out fault))
			{
				return false;
			}
			return (int)value < ConversionChancePercent;
		}

		// ==================================================================================
		// The exit: a settler may always emigrate rather than convert.
		//
		// Shaped exactly like Addendum 4b's housing grace, and for the same reason -- announced
		// once by name, counted in ATTENDED passes and in nothing else, ended by the founder
		// acting or by the settler walking out through the emigration the settlement already
		// has. Absence cannot spend a pass of it, because nothing advances it except a pass.
		//
		// What generates pressure is deliberately narrow. Osmosis and the shared table are
		// chosen proximity: Addendum 5 wants the Roomed household across an ambient grudge to
		// blend, and a household that could push somebody out for living in it would make the
		// healing arc into the thing it was written against. An IMPOSED creed is different --
		// the founder declared it, or consecrated it in somebody's quarter -- and that is the
		// one a settler is owed an exit from.
		// ==================================================================================

		/// <summary>
		/// Whether a channel imposes its creed rather than offering it, which is the whole of
		/// which channels a settler may resent.
		/// </summary>
		/// <param name="Channel">The channel.</param>
		/// <returns>True for <see cref="ConversionChannel.Shrine"/>, which is consecrated over a
		/// quarter whether or not the quarter asked. False for
		/// <see cref="ConversionChannel.Osmosis"/> and <see cref="ConversionChannel.Culture"/>,
		/// which are people living and eating together, and false for
		/// <see cref="ConversionChannel.Diplomacy"/>, which is invited and consented to one at a
		/// time.</returns>
		public static bool IsImposed(ConversionChannel Channel)
		{
			return Channel == ConversionChannel.Shrine;
		}

		/// <summary>
		/// Hostility at which a settler resents a creed being imposed on them: fifty, the ambient
		/// grudge fifty-three faction pairs hold toward everyone they have not troubled to name,
		/// and the same feeling a hut refuses to sleep on
		/// (<c>KingdomLodgingRules.CloseRefusalHostility</c>). Below it they merely differ, and
		/// people who merely differ do not leave over a shrine.
		/// </summary>
		public const int ResentmentHostility = 50;

		/// <summary>
		/// Attended passes a settler under a creed they resent is given before they go. Six: three
		/// times <c>KingdomLodgingRules.GracePasses</c>, because a roof is tonight's problem and a
		/// creed is a life's, and the founder's answer here is a thing they must unsay or
		/// deconsecrate rather than a bunk they can raise on the spot.
		/// </summary>
		public const int ResentedPasses = 6;

		/// <summary>The count of a settler nobody has warned the founder about yet. Negative so
		/// it can never be confused with "warned, and no pass has run since", which is zero.
		/// Mirrors <c>KingdomLodgingRules.NoGrace</c> deliberately.</summary>
		public const int NoResentment = -1;

		/// <summary>Whether an imposed creed is one this settler resents.</summary>
		/// <param name="Hostility">0-100 between the settler's own creed and the creed being
		/// imposed, from <c>KingdomCreed.HostilityBetween</c>. A creedless settler and a settler
		/// who already holds the imposed creed both read zero and resent nothing.</param>
		public static bool Resents(int Hostility)
		{
			return Hostility >= ResentmentHostility;
		}

		/// <summary>
		/// The count after one more attended pass has found this settler still under a creed they
		/// resent. A settler at <see cref="NoResentment"/> becomes zero, which is the pass it is
		/// announced on; every later attended pass adds one.
		/// <para>
		/// This is the ONLY thing that advances it, and it is called only from the attended pass.
		/// An absent founder therefore cannot walk anybody out of the settlement: no clock is read
		/// here, and nothing elapses on its own.
		/// </para>
		/// </summary>
		public static int ResentmentAfterPass(int Resented)
		{
			return (Resented < 0) ? 0 : (Resented + 1);
		}

		/// <summary>Whether a settler's grace is spent and they leave now: exactly
		/// <see cref="ResentedPasses"/> attended passes after the one it was announced on.
		/// </summary>
		public static bool ResentmentRunOut(int Resented)
		{
			return Resented >= ResentedPasses;
		}

		/// <summary>The cause a conversion-pressure departure is chronicled and noted under, in
		/// both registers. Named here rather than written at the call site so the chronicle and
		/// the ledger cannot drift apart, and so a test can pin it.</summary>
		public const string DepartureCause = "rather than take a creed they never chose";

		// ==================================================================================
		// Prose. Two registers that disagree where the day is contested, per the pillar: the
		// founder's book says somebody took the water, and the roads say somebody was bought.
		// ==================================================================================

		/// <summary>
		/// Hostility between the creed left and the creed taken at or above which the world argues
		/// about the day: fifty. The same number as <see cref="ResentmentHostility"/> and a
		/// different question &mdash; that one asks whether a person will stand for it, this one
		/// asks whether anybody else will believe it was freely done.
		/// </summary>
		public const int ContestedHostility = 50;

		/// <summary>Whether the two registers should tell this conversion differently.</summary>
		/// <param name="Hostility">0-100 between the creed left behind and the creed taken.
		/// A settler who held nothing before reads zero: nobody contests a conversion from
		/// nothing.</param>
		public static bool Contested(int Hostility)
		{
			return Hostility >= ContestedHostility;
		}

		/// <summary>
		/// The day as the founder's own book records it: lower-case clause, no trailing period,
		/// because the chronicle dates it and closes it.
		/// </summary>
		/// <param name="Channel">Which channel turned them, which picks the words.</param>
		/// <param name="ResidentName">The person, by name.</param>
		/// <param name="CreedDisplayName">The creed taken, as the founder reads it
		/// (<c>KingdomCreed.CreedName</c>).</param>
		public static string ConversionTelling(ConversionChannel Channel, string ResidentName, string CreedDisplayName)
		{
			string who = string.IsNullOrEmpty(ResidentName) ? "a settler" : ResidentName;
			string creed = string.IsNullOrEmpty(CreedDisplayName) ? "the creed of the house they slept in" : CreedDisplayName;
			switch (Channel)
			{
			case ConversionChannel.Culture:
				return who + " came to hold with " + creed + ", having eaten at their table often enough to be counted at it";
			case ConversionChannel.Shrine:
				return who + " took the water at the shrine and holds with " + creed;
			case ConversionChannel.Diplomacy:
				return who + " took the water rite and holds with " + creed;
			default:
				return who + " came to hold with " + creed + ", after a long season of sleeping under their roof";
			}
		}

		/// <summary>
		/// The same day as the roads tell it. Third person already, because the rumour register is
		/// not a translation of the founder's account but a rival to it. Handed to
		/// <c>KingdomChronicle.RecordDisputed</c> only when <see cref="Contested"/>; an uncontested
		/// conversion is derived the ordinary way, because the world has no reason to argue about
		/// somebody who held nothing changing their mind.
		/// </summary>
		/// <param name="Channel">Which channel turned them.</param>
		/// <param name="ResidentName">The person, by name.</param>
		/// <param name="CreedDisplayName">The creed taken.</param>
		public static string ConversionRumour(ConversionChannel Channel, string ResidentName, string CreedDisplayName)
		{
			string who = string.IsNullOrEmpty(ResidentName) ? "a settler" : ResidentName;
			string creed = string.IsNullOrEmpty(CreedDisplayName) ? "whoever was paying" : CreedDisplayName;
			switch (Channel)
			{
			case ConversionChannel.Culture:
				return who + " was bought with a good supper, and " + creed + " did not have to ask twice";
			case ConversionChannel.Shrine:
				return who + " was bought, and the shrine of " + creed + " did the buying";
			case ConversionChannel.Diplomacy:
				return who + " was bought, and the founder poured the water themselves";
			default:
				return who + " was talked around by the people they had been made to sleep beside";
			}
		}

		/// <summary>The founder-facing ledger note for a conversion: one line, said plainly,
		/// because this is news and not an alarm.</summary>
		/// <param name="ResidentName">The person, by name.</param>
		/// <param name="CreedDisplayName">The creed taken.</param>
		public static string ConversionNote(string ResidentName, string CreedDisplayName)
		{
			string who = string.IsNullOrEmpty(ResidentName) ? "A settler" : ResidentName;
			string creed = string.IsNullOrEmpty(CreedDisplayName) ? "the creed of the house they sleep in" : CreedDisplayName;
			return who + " holds with " + creed + " now.";
		}

		/// <summary>
		/// The day a settler decided they would rather go, as the founder's book records it:
		/// lower-case clause, no trailing period. Written on the pass the pressure is first
		/// noticed and not on the pass they leave, so the founder hears about it while there is
		/// still something to do (STANDARDS 7b).
		/// </summary>
		/// <param name="ResidentName">The person, by name.</param>
		/// <param name="CreedDisplayName">The creed being imposed on them.</param>
		public static string PressureTelling(string ResidentName, string CreedDisplayName)
		{
			string who = string.IsNullOrEmpty(ResidentName) ? "a settler" : ResidentName;
			string creed = string.IsNullOrEmpty(CreedDisplayName) ? "the creed the realm had taken up" : CreedDisplayName;
			return who + " would not be made to hold with " + creed + ", and began asking after the roads";
		}

		/// <summary>
		/// The same, as the founder can act on it. Says what would stop it, because a line that
		/// only reports is a line that stalls in silence.
		/// </summary>
		/// <param name="ResidentName">The person, by name.</param>
		/// <param name="CreedDisplayName">The creed being imposed on them.</param>
		public static string PressureNote(string ResidentName, string CreedDisplayName)
		{
			string who = string.IsNullOrEmpty(ResidentName) ? "A settler" : ResidentName;
			string creed = string.IsNullOrEmpty(CreedDisplayName) ? "the creed the realm has taken up" : CreedDisplayName;
			return who + " will not be made to hold with " + creed + ". Take it back out of their quarter and they stay; leave it standing and they take the road.";
		}

		/// <summary>
		/// The one sentence the founder is owed when the grace has run out and they are going
		/// (STANDARDS 7b). Names the person and the cause and nothing else; the departure itself
		/// is chronicled by the emigration machinery under <see cref="DepartureCause"/>.
		/// </summary>
		public static string LeavingLine(string ResidentName)
		{
			string who = string.IsNullOrEmpty(ResidentName) ? "A settler" : ResidentName;
			return who + " waited out the grace under a creed they never chose, and is leaving.";
		}
	}
}
