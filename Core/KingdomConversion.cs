using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// A standing source of conversion pressure on a settlement's people &mdash; a shrine
	/// consecrated over a quarter, or anything a later wave builds that works the same way.
	/// <para>
	/// A <c>Protocol</c>-shaped contract rather than a call the source makes once, and
	/// deliberately: pressure is a FACT re-derived on every resolve, not an event that
	/// happened. A source that fired once and left a counter running would keep pushing a settler
	/// toward the road after the founder had already deconsecrated the shrine, and the founder
	/// would have no way to tell. Asked fresh every pass, taking the pressure off takes it off.
	/// </para>
	/// <para>
	/// Register with <see cref="KingdomConversion.AddPressureSource"/>. Implementations are
	/// untrusted (STANDARDS.md &sect;9): one that throws is logged and skipped, and never takes
	/// the settlement pass down with it.
	/// </para>
	/// </summary>
	public interface IConversionPressure
	{
		/// <summary>
		/// The creed this source is imposing on <paramref name="Settler"/> where they stand, or
		/// null when it is imposing nothing on them.
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="Z">The claimed zone the settler is standing in.</param>
		/// <param name="Settler">The settler being asked about.</param>
		/// <returns>A faction name, or null. Whether the settler resents it is not this source's
		/// question &mdash; <see cref="KingdomConversionRules.Resents"/> decides that, from the
		/// engine's own faction feelings.</returns>
		string PressingCreed(KingdomSystem System, Zone Z, GameObject Settler);
	}

	/// <summary>
	/// Conversion, and the exit from it. The engine-coupled shell for Addendum 5's two passive
	/// channels and for the guard every channel shares.
	/// <para>
	/// <b>Osmosis.</b> Each housed settler whose household holds a creed by strict majority is
	/// pulled a little toward it for every DAY they actually live under that roof, scaled by the
	/// closeness ladder: nothing at all in one open room (the people in it already agree by
	/// construction), fastest in a hut, slowest in quarters of one's own, and nothing across a
	/// feeling the quarters would refuse. The stone house is the only architecture that holds an
	/// ambient grudge under one roof, so the stone house is where a real difference actually gets
	/// crossed &mdash; which is the whole of the healing arc the fault-line ceiling (Addendum 4d)
	/// needs. Partition, then build better, then the quarters dissolve.
	/// </para>
	/// <para>
	/// <b>Culture.</b> Each witnessed shared meal nudges its attendees toward the table's own
	/// majority: small, and capped at <see cref="KingdomConversionRules.MealCeilingPercent"/> of
	/// the road so a settlement can never eat its way to a conversion. A free rider on a ceremony
	/// the founder was already holding for other reasons.
	/// </para>
	/// <para>
	/// <b>The exit.</b> A settler may always emigrate rather than convert. Living beside somebody
	/// generates no pressure &mdash; that is the arc working &mdash; but a creed IMPOSED on a
	/// settler who resents it (a realm declaration against theirs, a rival shrine consecrated in
	/// their quarter) starts them toward the road instead: warned once wherever the founder is,
	/// given <see cref="KingdomConversionRules.ResentedWindowDays"/> of world time for them to take
	/// it off, then gone through the settlement's ordinary emigration, chronicled by name and
	/// cause in both registers. <see cref="NotePressure"/> and
	/// <see cref="AddPressureSource"/> are the surface every other channel uses; nothing may build
	/// its own.
	/// </para>
	/// <para>
	/// <b>Counted in cohabitation time, warned at the crossing, spent by the world.</b> People go
	/// on living together whether or not anyone is watching (Addendum 8 clause 1, which names
	/// osmosis), so shared living accrues for every day two settlers actually spent under one roof
	/// and the founder's presence has nothing to do with it. The road ends in a brink rather than
	/// in a conversion, and under Addendum 10(a) the founder's presence does not govern the window
	/// either: the word is pushed to them the moment the road ends, and
	/// <see cref="KingdomBrinkRules.CreedBrinkWindowDays"/> of world time later the creed changes
	/// hands whether they came back or not. What their presence still governs is everything that
	/// can STOP it &mdash; and nothing changes hands unwarned.
	/// </para>
	/// <para>
	/// <b>What the absence is allowed to assume.</b> Who sleeps where is written only by
	/// <c>KingdomLodging.OnSettlementPass</c>, which is attended, so the household standing at the
	/// last pass is the household that stood through the whole stretch &mdash; nobody moved house
	/// while nobody was there. The cohabitation clock is therefore honest by construction, and it
	/// is restarted (<see cref="ForgetCohabitation"/>) the moment lodging does move somebody, so a
	/// settler never inherits days spent under a roof they have left.
	/// </para>
	/// </summary>
	public static class KingdomConversion
	{
		/// <summary>
		/// Whether the passive channels run at all. Conversion is creed machinery working through
		/// the lodging assignment, so it is off whenever either of those is off rather than
		/// carrying a toggle of its own for a thing that cannot happen without both.
		/// </summary>
		public static bool Enabled
		{
			get { return KingdomCreed.Enabled && KingdomLodging.Enabled; }
		}

		// Standing pressure sources, asked fresh on every resolve. A list rather than a
		// single hook because two shrines in two quarters are two sources, and the first one that
		// names a creed this settler resents is the one they leave over -- a second grievance does
		// not make anybody leave twice.
		private static readonly List<IConversionPressure> Sources = new List<IConversionPressure>();

		/// <summary>
		/// Tick this settler's cohabitation was last credited at. Stamped on the settler rather
		/// than kept in a map, because how long somebody has lived under their roof is a fact
		/// about them: it survives a seat swap, a secession and a save without any per-city map
		/// having to remember to carry it.
		/// </summary>
		public const string CohabitTickProperty = "KingdomCohabitTick";

		/// <summary>
		/// Roads of shared living this settler has walked all the way to the end, converted or
		/// refused. The draw's ordinal (<c>KingdomConversionRules.RoadEnd</c>): progress holds at
		/// the road's end and can no longer be divided to find out which road they are on, so it
		/// is counted instead.
		/// </summary>
		public const string RoadsWalkedProperty = "KingdomConversionRoads";

		/// <summary>
		/// Registers a standing source of conversion pressure. Idempotent: registering the same
		/// instance twice registers it once, so a loader that re-runs cannot make one shrine press
		/// twice as hard.
		/// </summary>
		/// <param name="Source">The source. Null is ignored.</param>
		public static void AddPressureSource(IConversionPressure Source)
		{
			if (Source == null || Sources.Contains(Source))
			{
				return;
			}
			Sources.Add(Source);
		}

		/// <summary>Forgets every registered pressure source. For a registry loader re-reading its
		/// streams, and for tests; a save carries none of this.</summary>
		public static void ClearPressureSources()
		{
			Sources.Clear();
		}

		/// <summary>
		/// The kingdom's one attended pass over what its people believe: credits every household's
		/// minority the days they actually spent under its roof, records a brink for anyone who
		/// has reached the end of that road, spends one pass of every standing window, and turns
		/// anyone whose window has run out and whose draw agrees.
		/// <para>
		/// Preconditions: called from the settlement pass, on claimed ground, AFTER
		/// <c>KingdomLodging.OnSettlementPass</c> &mdash; who sleeps where is the input, so a pass
		/// that ran first would read yesterday's households. Side effects: shared living accrues,
		/// a conversion may be recorded in both registers, a settler may leave through
		/// <c>KingdomGrowth.Emigrate</c>. Failure mode: returns having done nothing.
		/// </para>
		/// </summary>
		public static void OnSettlementPass(KingdomSystem System, Zone Z)
		{
			if (!Enabled || System == null || !System.Founded || Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			List<GameObject> residents = ResidentsIn(Z);
			if (residents.Count == 0)
			{
				return;
			}
			long now = (The.Game != null) ? The.Game.TimeTicks : 0L;
			Dictionary<string, List<GameObject>> households = Households(residents);
			// The window first, and osmosis after it. The order no longer decides anything -- the
			// window is anchored at the warning tick, so a brink recorded and warned in this
			// resolve has zero days spent whichever loop looks at it next -- but it is kept,
			// because reading the standing brinks before creating new ones is the order the
			// re-derive-every-pass contract is easiest to check in.
			for (int i = 0; i < residents.Count; i++)
			{
				CreedWindow(System, Z, residents[i], now);
			}
			for (int i = 0; i < residents.Count; i++)
			{
				Osmosis(System, Z, residents[i], households, now);
			}
			for (int i = 0; i < residents.Count; i++)
			{
				Pressure(System, Z, residents[i], now);
			}
			ForgetDeparted(System);
		}

		/// <summary>
		/// Addendum 5's culture channel: the meal the founder just held nudges everyone who sat
		/// down at it toward the table's own majority creed.
		/// <para>
		/// Preconditions: called from <c>KingdomLarder.HoldSharedMeal</c> once the meal has
		/// actually been spent and recorded, so a meal that failed for want of food nudges
		/// nobody. Side effects: shared living accrues for the attendees, and a conversion may be
		/// recorded. Failure mode: returns having done nothing.
		/// </para>
		/// <para>
		/// The table's majority is read with <c>KingdomCreedRules.DominantCreed</c> over the
		/// people actually standing there &mdash; the same rule that decides what a city believes,
		/// asked of one evening &mdash; so a small or evenly split table nudges nobody at all, and
		/// the meal never invents a majority the settlement does not have.
		/// </para>
		/// </summary>
		public static void OnSharedMeal(KingdomSystem System, Zone Z)
		{
			if (!Enabled || System == null || !System.Founded || Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			List<GameObject> attendees = ResidentsIn(Z);
			if (attendees.Count == 0)
			{
				return;
			}
			Dictionary<string, int> counts = CreedCounts(attendees);
			string majority = KingdomCreedRules.DominantCreed(counts, attendees.Count);
			if (string.IsNullOrEmpty(majority))
			{
				return;
			}
			for (int i = 0; i < attendees.Count; i++)
			{
				GameObject attendee = attendees[i];
				string roll = RollNameOf(attendee);
				if (roll == null || attendee.GetStringProperty(KingdomCreed.CreedProperty) == majority)
				{
					continue;
				}
				ConversionProgress progress = ProgressOf(System, roll);
				// The ceiling applies to progress TOWARD the table's creed. A settler being pulled
				// somewhere else is not accumulating here at all -- the meal is taking points off
				// that other pull -- so the full nudge crosses over uncapped.
				int points = (progress.Creed == null || progress.Creed == majority)
					? KingdomConversionRules.MealSharedFor(progress.Shared)
					: KingdomConversionRules.MealShared;
				SetProgress(System, roll, KingdomConversionRules.Advance(progress, majority, points));
				// A meal can never carry anybody to the road's end -- the ceiling is half of it --
				// so this is here for the settler the meal took points OFF: if a counter-pull has
				// dropped them back below the end of a road they were standing at, their brink is
				// lifted and unsaid on the spot.
				LiftIfArrested(System, Z, attendee, roll);
			}
		}

		/// <summary>
		/// Records that a channel is imposing a creed on one settler right now. The immediate form
		/// of <see cref="IConversionPressure"/>, for the moment of the act itself &mdash; the day
		/// a shrine is consecrated &mdash; so the founder is told on that day rather than on their
		/// next pass.
		/// <para>
		/// Side effects: announces the pressure once by name in both registers and starts the
		/// grace. Failure mode: returns false and changes nothing.
		/// </para>
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="Z">The claimed zone the settler stands in.</param>
		/// <param name="Settler">The settler. Must be on the roll; a settler the registers cannot
		/// name is never walked out of the settlement.</param>
		/// <param name="Channel">The channel imposing it. Refused for any channel
		/// <see cref="KingdomConversionRules.IsImposed"/> rejects &mdash; osmosis and the shared
		/// table are chosen proximity, and a household that could push somebody out for living in
		/// it would make the healing arc into the thing it was written against.</param>
		/// <param name="PressingCreed">The creed being imposed.</param>
		/// <returns>True when the settler resents it and the grace has begun; false when they do
		/// not resent it, which is most people.</returns>
		public static bool NotePressure(KingdomSystem System, Zone Z, GameObject Settler, ConversionChannel Channel, string PressingCreed)
		{
			if (!Enabled || System == null || !System.Founded || Settler == null || string.IsNullOrEmpty(PressingCreed))
			{
				return false;
			}
			if (!KingdomConversionRules.IsImposed(Channel))
			{
				return false;
			}
			string roll = RollNameOf(Settler);
			if (roll == null)
			{
				return false;
			}
			int hostility = KingdomCreed.HostilityBetween(Settler.GetStringProperty(KingdomCreed.CreedProperty), PressingCreed);
			if (!KingdomConversionRules.Resents(hostility))
			{
				return false;
			}
			BeginResentment(System, Z, roll, PressingCreed);
			return true;
		}

		/// <summary>
		/// One settler changes creed. The one path a conversion may take, whichever channel turned
		/// them: the tally moves through <c>KingdomCreed.Forget</c> and <c>KingdomCreed.Record</c>
		/// and never through a second route of its own, both registers are written (disagreeing
		/// with each other where the day is contested), and whatever was pulling at them is
		/// cleared.
		/// <para>
		/// Side effects: the settler's creed property and the city's <c>CreedCounts</c> change,
		/// the creed they are leaving is written into their own history and into the city's
		/// <c>CreedPastCounts</c> (Addendum 16),
		/// two chronicle entries and one ledger note are written, and any standing grace or brink
		/// this settler was spending is forgotten &mdash; a person who has taken the creed is no
		/// longer under pressure from it, and no longer one window away from it. Failure mode:
		/// returns false and changes nothing.
		/// </para>
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="Z">The claimed zone it happened in.</param>
		/// <param name="Settler">The settler.</param>
		/// <param name="Creed">The faction name they now hold with.</param>
		/// <param name="Channel">Which channel turned them, which picks the words in both
		/// registers.</param>
		public static bool Convert(KingdomSystem System, Zone Z, GameObject Settler, string Creed, ConversionChannel Channel)
		{
			if (!Enabled || System == null || !System.Founded || Settler == null || string.IsNullOrEmpty(Creed))
			{
				return false;
			}
			string was = Settler.GetStringProperty(KingdomCreed.CreedProperty);
			if (was == Creed)
			{
				return false;
			}
			string roll = RollNameOf(Settler);
			string named = string.IsNullOrEmpty(roll) ? Settler.ShortDisplayName : roll;
			int hostility = KingdomCreed.HostilityBetween(was, Creed);
			// The existing surfaces, in the only order that keeps the tally honest: the old creed
			// is read off the settler by Forget, so it must go before Record overwrites it.
			KingdomCreed.Forget(System, Settler);
			KingdomCreed.Record(System, Settler, Creed);
			// And the history. THIS is the one place a creed is ever LEFT, which is why Addendum
			// 16's recorded fact is written here and nowhere else: every other path either gives a
			// settler their first creed (nothing left behind) or takes the whole person out of the
			// city (nothing to remember them by). Forget, a line above, took this settler's whole
			// history out of the city's tally along with their present creed, because its other two
			// callers are a death and a departure; RememberPast puts it back with one more name in
			// it. The record is bounded at KingdomCreedRules.MaxKeptCreeds and never rewrites
			// itself, so a design this city could see yesterday cannot vanish today.
			KingdomCreed.RememberPast(System, Settler, was);
			if (roll != null)
			{
				System.ConversionShared.Remove(roll);
				System.ConversionToward.Remove(roll);
				System.ConversionResented.Remove(roll);
			}
			// And the brink they were standing at, if any. Cleared HERE rather than at each call
			// site because this is the one path a conversion may take: a person who has taken the
			// creed is not one window away from taking it, and a record left standing would be
			// unsaid on the next pass -- telling the founder that somebody who converted last
			// night "holds what they held".
			KingdomBrink.Lift(Settler, BrinkKind.Creed);
			string creedName = KingdomCreed.CreedName(Creed);
			string telling = KingdomConversionRules.ConversionTelling(Channel, named, creedName);
			if (KingdomConversionRules.Contested(hostility))
			{
				KingdomChronicle.RecordDisputed(System, telling, KingdomConversionRules.ConversionRumour(Channel, named, creedName));
			}
			else
			{
				KingdomChronicle.Record(System, telling);
			}
			System.Ledger.Note("{{G|" + KingdomConversionRules.ConversionNote(named, creedName) + "}}");
			KingdomLog.Log("conversion: " + named + " " + (string.IsNullOrEmpty(was) ? "(none)" : was) + " -> " + Creed + " via " + Channel + " hostility=" + hostility);
			return true;
		}

		/// <summary>The conversion line <c>kingdom:dump</c> appends for the zone the founder is
		/// standing in: who is being pulled where, how far along they are, who is standing at the
		/// end of a road with a window running, and who is spending a grace under a creed they
		/// resent.</summary>
		public static string DumpLine(KingdomSystem System, Zone Z)
		{
			if (System == null || Z == null)
			{
				return "";
			}
			long now = (The.Game != null) ? The.Game.TimeTicks : 0L;
			List<string> pulled = new List<string>();
			foreach (KeyValuePair<string, int> entry in System.ConversionShared)
			{
				string toward;
				System.ConversionToward.TryGetValue(entry.Key, out toward);
				pulled.Add(entry.Key + "->" + (toward ?? "-") + " " + entry.Value + "/" + KingdomConversionRules.SharedLivingForConversion);
			}
			List<string> atTheEnd = new List<string>();
			List<GameObject> residents = ResidentsIn(Z);
			for (int i = 0; i < residents.Count; i++)
			{
				BrinkRecord brink = KingdomBrink.Of(residents[i], BrinkKind.Creed);
				if (!brink.Stands)
				{
					continue;
				}
				atTheEnd.Add(RollNameOf(residents[i]) + "->" + (brink.Cause ?? "-")
					+ " (" + (ConversionChannel)brink.Channel
					+ " " + KingdomBrinkRules.DaysLeft(BrinkKind.Creed, brink.WarnedTick, now)
					+ "/" + KingdomBrinkRules.CreedBrinkWindowDays + "d left"
					+ (brink.Warned ? "" : ", unwarned")
					+ ", stood " + KingdomBrinkRules.DaysStood(brink.ReachedTick, now) + "d)");
			}
			List<string> leaving = new List<string>();
			int today = KingdomBrinkRules.DayNumber(now);
			foreach (KeyValuePair<string, int> entry in System.ConversionResented)
			{
				leaving.Add(entry.Key + " (" + KingdomConversionRules.ResentmentDaysLeft(entry.Value, today)
					+ "/" + KingdomConversionRules.ResentedWindowDays + "d left"
					+ ((entry.Value > KingdomConversionRules.NotWarned) ? "" : ", unwarned") + ")");
			}
			if (pulled.Count == 0 && leaving.Count == 0 && atTheEnd.Count == 0)
			{
				return "";
			}
			string line = "\nConversion: " + ((pulled.Count == 0) ? "nobody being pulled" : string.Join(", ", pulled));
			if (atTheEnd.Count > 0)
			{
				line += "  at the road's end: " + string.Join(", ", atTheEnd);
			}
			if (leaving.Count > 0)
			{
				line += "  resenting a creed: " + string.Join(", ", leaving);
			}
			return line;
		}

		// --- Osmosis ----------------------------------------------------------------------

		/// <summary>
		/// Restarts a settler's cohabitation clock, because the roof over them has changed.
		/// Called by <c>KingdomLodging</c> the moment it houses somebody, moves them, or finds
		/// their home gone &mdash; nowhere else.
		/// <para>
		/// Their PROGRESS is untouched: a settler carries what they have come to hold across a
		/// move, and the counter-pull of a new household is what takes it off them. Only the days
		/// restart, so nobody is ever credited for living somewhere they had already left.
		/// </para>
		/// </summary>
		public static void ForgetCohabitation(GameObject Resident)
		{
			if (Resident != null)
			{
				Resident.SetLongProperty(CohabitTickProperty, 0L);
			}
		}

		private static void Osmosis(KingdomSystem System, Zone Z, GameObject Resident, Dictionary<string, List<GameObject>> Roofs, long Now)
		{
			string roll = RollNameOf(Resident);
			if (roll == null)
			{
				// Somebody the roll does not carry: a founding citizen, or a person the settlement
				// never named. Progress is keyed to the roll, so an unnamed resident never enters
				// it, and nothing happens to them. Exactly the rule Addendum 4b's window uses, and
				// for the same reason: staying as they were is the safe answer to a question the
				// registers cannot record.
				return;
			}
			// The clock advances for everybody standing here, whatever their household turns out
			// to buy them. A settler in a bunk row banks nothing, and a settler under a roof that
			// refuses them banks nothing, and neither of them may keep those days in hand against
			// the day they move somewhere that would have counted them.
			int days = CohabitedDays(Resident, Now);
			if (days <= 0)
			{
				return;
			}
			if (KingdomBrink.Stands(Resident, BrinkKind.Creed))
			{
				// At the end of the road already. Rule 1: nothing accrues past a brink, so the
				// stretch is spent and buys nothing at all.
				return;
			}
			string plotId = Resident.GetStringProperty(KingdomLodging.HomePlotIdProperty);
			List<GameObject> household;
			if (string.IsNullOrEmpty(plotId) || !Roofs.TryGetValue(plotId, out household) || household.Count < 2)
			{
				// Nobody to be pulled by. A settler sleeping in the open, and a settler alone under
				// their own roof, both simply hold what they held.
				return;
			}
			string creed = Resident.GetStringProperty(KingdomCreed.CreedProperty);
			string majority = KingdomConversionRules.HouseholdMajority(CreedCounts(household), household.Count);
			if (string.IsNullOrEmpty(majority) || majority == creed)
			{
				return;
			}
			int perDay = KingdomConversionRules.SharedLivingPerDay(
				KingdomLodging.QuartersOf(Z, Resident),
				KingdomCreed.HostilityBetween(creed, majority));
			if (perDay <= 0)
			{
				return;
			}
			ConversionProgress before = ProgressOf(System, roll);
			ConversionProgress after = KingdomConversionRules.AdvanceOverDays(before, majority, perDay, days);
			SetProgress(System, roll, after);
			if (after.Creed != majority || !KingdomConversionRules.AtMilestone(after.Shared))
			{
				return;
			}
			// The road ended somewhere inside the stretch, and the founder is owed the day it
			// actually ended on rather than the day they happened to walk back in.
			long reached = KingdomBrinkRules.CrossingTick(
				Now - (long)days * KingdomRules.TicksPerDay, Now,
				before.Creed == majority ? before.Shared : 0,
				KingdomConversionRules.SharedLivingForConversion, perDay);
			NoteRoadsEnd(System, Z, Resident, roll, majority, ConversionChannel.Osmosis, reached, Now);
		}

		/// <summary>
		/// Records that a settler has reached the end of a creed's road, and pushes the word once.
		/// The immediate form, for the channel that noticed it &mdash; <c>KingdomFaith</c>'s shrine
		/// calls it too &mdash; so the founder hears about it on the resolve it is seen rather than
		/// one later.
		/// <para>
		/// Side effects: a brink is recorded against the settler with the tick the road actually
		/// ended on; the warning is stamped, which is what STARTS the window (Addendum 10(a) &mdash;
		/// the founder's time runs from being told, never from the crossing); and the word goes out
		/// through <c>KingdomWord</c> to wherever they are, into the ledger and into the chronicle.
		/// Failure mode: returns false and changes nothing, which is what a settler already at a
		/// brink gets &mdash; nobody is told twice.
		/// </para>
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="Z">The ground this is happening on, for whether the founder is standing
		/// on it. Null reads as elsewhere, which only changes the framing.</param>
		/// <param name="Resident">The settler.</param>
		/// <param name="Roll">The name the roll carries them under.</param>
		/// <param name="TowardCreed">The creed at the end of the road.</param>
		/// <param name="Channel">Which channel walked them down it.</param>
		/// <param name="ReachedTick">The tick the road ended, from
		/// <c>KingdomBrinkRules.CrossingTick</c> or from the pass that found it.</param>
		/// <param name="NowTick">Now, for the honest elapsed and for the window's anchor.</param>
		public static bool NoteRoadsEnd(KingdomSystem System, Zone Z, GameObject Resident, string Roll, string TowardCreed, ConversionChannel Channel, long ReachedTick, long NowTick)
		{
			if (!Enabled || System == null || !System.Founded || Resident == null || string.IsNullOrEmpty(Roll) || string.IsNullOrEmpty(TowardCreed))
			{
				return false;
			}
			if (!KingdomBrink.Record(Resident, BrinkKind.Creed, ReachedTick, TowardCreed, (int)Channel))
			{
				return false;
			}
			KingdomBrink.MarkWarned(Resident, BrinkKind.Creed, NowTick);
			KingdomBrink.Announce(System, BrinkKind.Creed, Roll, KingdomCreed.CreedName(TowardCreed),
				KingdomBrink.Of(Resident, BrinkKind.Creed), NowTick, KingdomWord.StandsIn(Z), System.SeatName, null);
			return true;
		}

		// Every standing creed brink, judged against the world's clock. The arrest is asked FIRST
		// and every time, which is Rule 2 -- a founder who broke the household up has ended it,
		// whenever they did it -- and only then is the window's expiry read. The shrine's own
		// brinks are skipped here and judged in KingdomFaith's pass, because what would arrest one
		// of those is a fact about a building rather than about a household, and only that file
		// can see it.
		//
		// Absence no longer holds the window open. What it cannot do is start one: a brink the
		// founder has never been warned of has no deadline, so this can only ever warn on the
		// resolve that discovers one, and the whole window runs from there.
		private static void CreedWindow(KingdomSystem System, Zone Z, GameObject Resident, long Now)
		{
			string roll = RollNameOf(Resident);
			if (roll == null)
			{
				return;
			}
			BrinkRecord brink = KingdomBrink.Of(Resident, BrinkKind.Creed);
			if (!brink.Stands || brink.Channel == (int)ConversionChannel.Shrine)
			{
				return;
			}
			if (LiftIfArrested(System, Z, Resident, roll))
			{
				return;
			}
			if (KingdomBrink.MarkWarned(Resident, BrinkKind.Creed, Now))
			{
				// Recorded by some path that could not speak, or carried across a save from
				// before there was a warning to give. Told now, and the window starts now.
				KingdomBrink.Announce(System, BrinkKind.Creed, roll, KingdomCreed.CreedName(brink.Cause),
					KingdomBrink.Of(Resident, BrinkKind.Creed), Now, KingdomWord.StandsIn(Z), System.SeatName, null);
				return;
			}
			if (!KingdomBrinkRules.WindowSpent(BrinkKind.Creed, brink.WarnedTick, Now))
			{
				return;
			}
			EndOfTheRoad(System, Z, Resident, roll, brink, Now);
		}

		/// <summary>
		/// Rule 2 for the creed brink: the pressure is a fact, so a settler whose progress has
		/// fallen back off the road's end &mdash; a counter-pull at a rival table, a rehousing that
		/// broke the household up, a creed they have already taken &mdash; is no longer at a brink
		/// and is said to be no longer at one.
		/// </summary>
		/// <returns>True when a brink was lifted, which is the caller's signal to stop.</returns>
		private static bool LiftIfArrested(KingdomSystem System, Zone Z, GameObject Resident, string Roll)
		{
			BrinkRecord brink = KingdomBrink.Of(Resident, BrinkKind.Creed);
			if (!brink.Stands || brink.Channel == (int)ConversionChannel.Shrine)
			{
				return false;
			}
			ConversionProgress progress = ProgressOf(System, Roll);
			bool holds = progress.Creed == brink.Cause
				&& KingdomConversionRules.AtMilestone(progress.Shared)
				&& Resident.GetStringProperty(KingdomCreed.CreedProperty) != brink.Cause;
			if (holds)
			{
				return false;
			}
			bool wasWarned = brink.Warned;
			KingdomBrink.Lift(Resident, BrinkKind.Creed);
			if (wasWarned)
			{
				// Only what was actually said is unsaid.
				KingdomBrink.Unsay(System, BrinkKind.Creed, Roll, KingdomWord.StandsIn(Z), System.SeatName);
			}
			return true;
		}

		/// <summary>
		/// The window has run out with the household still pulling. NOW the draw is asked, and it
		/// is the same draw that shipped: <c>KingdomConversionRules.ConversionChancePercent</c>, on
		/// a key that names the settlement, the channel, the person and which road this is. A road
		/// that answers no is walked from nothing again, and the next one is a new question rather
		/// than the same one re-asked.
		/// <para>
		/// The window is spent by the world, so this may be the first resolve after a long absence
		/// and the turning may have happened days ago. It is dated to the day it happened: the
		/// draw is the same draw whenever it is asked (<c>CounterRandom</c> on a key with no clock
		/// in it), so the founder who was away is told what the settlement already knows rather
		/// than watching it decided in front of them.
		/// </para>
		/// </summary>
		private static void EndOfTheRoad(KingdomSystem System, Zone Z, GameObject Resident, string Roll, BrinkRecord Brink, long Now)
		{
			ConversionChannel channel = (ConversionChannel)Brink.Channel;
			int roads = Resident.GetIntProperty(RoadsWalkedProperty);
			bool turns = KingdomConversionRules.Converts(
				KingdomChronicle.SettlementId(System.KingdomFactionName), channel, Roll, KingdomConversionRules.RoadEnd(roads));
			Resident.SetIntProperty(RoadsWalkedProperty, roads + 1);
			bool here = KingdomWord.StandsIn(Z);
			int ago = KingdomBrinkRules.DaysStood(KingdomBrinkRules.ExpiryTick(BrinkKind.Creed, Brink.WarnedTick), Now);
			if (turns && Convert(System, Z, Resident, Brink.Cause, channel))
			{
				// Convert clears the brink and both maps and writes its own two registers. All
				// that is owed here is the date: the founder was told this was coming, and this is
				// the day it came.
				KingdomWord.Aftermath(System, System.SeatName, here, KingdomBrinkRules.FiredNote(BrinkKind.Creed, Roll, ago));
				return;
			}
			// It did not take. The brink is lifted rather than left standing, and the road starts
			// again from nothing -- a soul that walked a whole season of shared living and did not
			// turn is not one point away from turning tomorrow.
			KingdomBrink.Lift(Resident, BrinkKind.Creed);
			SetProgress(System, Roll, ConversionProgress.None);
			KingdomBrink.Unsay(System, BrinkKind.Creed, Roll, here, System.SeatName);
		}

		// Whole days this settler has lived under their present roof since the last time anything
		// counted them, advancing the stamp by exactly the days credited so a part-day is never
		// lost and never double-counted. A settler nobody has counted yet plants their stamp here
		// and is credited nothing: an unplanted stamp read as elapsed would charge them the age of
		// the world.
		private static int CohabitedDays(GameObject Resident, long Now)
		{
			long last = Resident.GetLongProperty(CohabitTickProperty);
			if (last <= 0L || Now <= 0L)
			{
				Resident.SetLongProperty(CohabitTickProperty, Now);
				return 0;
			}
			int days = KingdomRules.ElapsedDays(Now - last);
			if (days <= 0)
			{
				return 0;
			}
			Resident.SetLongProperty(CohabitTickProperty, KingdomRules.AdvanceCheckpoint(last, Now));
			return days;
		}

		// --- The exit ---------------------------------------------------------------------

		// The resented-creed instance of the brink, judged against the world's clock. Pressure is
		// re-derived here every pass rather than remembered, so taking the pressure off takes it
		// off; the map entry's PRESENCE is the pressure and its VALUE is the world day the founder
		// was warned, which is what the window runs from.
		//
		// It shares the creed brink's window (KingdomConversionRules.ResentedWindowDays) and its
		// doctrine: warned once, pushed to the founder wherever they are, and then spent by the
		// world whether or not they came back -- but never spent by a clock nobody started, so the
		// resolve that discovers a pressure can only ever warn about it.
		private static void Pressure(KingdomSystem System, Zone Z, GameObject Resident, long Now)
		{
			string roll = RollNameOf(Resident);
			if (roll == null)
			{
				return;
			}
			string pressing = ResentedPressure(System, Z, Resident);
			if (pressing == null)
			{
				// Nothing is being imposed on them, or nothing they mind. Forgetting the entry
				// rather than banking it is the same ruling housing makes: the founder is being
				// asked to act on THIS pressure, and if it comes back they get the whole window
				// again. And the arrest is SAID, wherever the founder is -- a warning that is
				// never withdrawn is a warning they stop believing -- but only when there was one:
				// an entry that never carried a warning day has nothing to unsay.
				int wasWarnedOn;
				bool had = System.ConversionResented.TryGetValue(roll, out wasWarnedOn);
				System.ConversionResented.Remove(roll);
				if (had && wasWarnedOn > KingdomConversionRules.NotWarned)
				{
					KingdomWord.Unsay(System, System.SeatName, KingdomWord.StandsIn(Z),
						KingdomBrinkRules.LiftedNote(BrinkKind.Creed, roll));
				}
				return;
			}
			int today = KingdomBrinkRules.DayNumber(Now);
			int warned;
			if (!System.ConversionResented.TryGetValue(roll, out warned) || warned <= KingdomConversionRules.NotWarned)
			{
				System.ConversionResented[roll] = today;
				Announce(System, Z, roll, pressing, KingdomConversionRules.ResentedWindowDays);
				// The day the word goes out is never the day they go.
				return;
			}
			if (!KingdomConversionRules.ResentmentRunOut(warned, today))
			{
				return;
			}
			long went = (long)(warned + KingdomConversionRules.ResentedWindowDays) * KingdomRules.TicksPerDay;
			string leaving = KingdomConversionRules.LeavingLine(roll)
				+ KingdomBrinkRules.FiredClause(KingdomBrinkRules.DaysStood(went, Now));
			if (KingdomGrowth.Emigrate(System, Z, null, Resident, KingdomConversionRules.DepartureCause))
			{
				KingdomWord.Aftermath(System, System.SeatName, KingdomWord.StandsIn(Z), leaving);
				System.ConversionResented.Remove(roll);
				return;
			}
			// The settlement would not let them go -- they are the last of the loyal core, or the
			// emigration machinery could not take them. The window stays spent and is tried again
			// on the next resolve rather than being reset, so nothing is lost and nobody is told
			// they are going by a settlement that then kept them.
		}

		// The first source naming a creed this settler resents, or null. First rather than worst
		// on purpose: a second grievance does not make anybody leave twice, and the founder is
		// owed one name to act on rather than a list.
		private static string ResentedPressure(KingdomSystem System, Zone Z, GameObject Resident)
		{
			string creed = Resident.GetStringProperty(KingdomCreed.CreedProperty);
			if (Resents(creed, System.DeclaredCreed))
			{
				return System.DeclaredCreed;
			}
			for (int i = 0; i < Sources.Count; i++)
			{
				string pressing = null;
				// Third-party sources are untrusted (STANDARDS 9): one that throws disables itself
				// for the pass and is logged, and never takes the settlement pass down with it.
				KingdomSystem.Guard("conversion pressure source", delegate
				{
					pressing = Sources[i].PressingCreed(System, Z, Resident);
				});
				if (Resents(creed, pressing))
				{
					return pressing;
				}
			}
			return null;
		}

		private static bool Resents(string Creed, string Pressing)
		{
			return !string.IsNullOrEmpty(Pressing)
				&& KingdomConversionRules.Resents(KingdomCreed.HostilityBetween(Creed, Pressing));
		}

		private static void BeginResentment(KingdomSystem System, Zone Z, string Roll, string Pressing)
		{
			if (System.ConversionResented.ContainsKey(Roll))
			{
				return;
			}
			System.ConversionResented[Roll] = KingdomBrinkRules.DayNumber((The.Game != null) ? The.Game.TimeTicks : 0L);
			Announce(System, Z, Roll, Pressing, KingdomConversionRules.ResentedWindowDays);
		}

		// STANDARDS 7b and Addendum 10(a): said once, and PUSHED to wherever the founder is
		// standing rather than left in a report they read at the seat. The map entry IS the
		// announce flag, so a settler whose window is already running cannot be warned about a
		// second time, and one whose pressure lifted and returned is warned afresh.
		private static void Announce(KingdomSystem System, Zone Z, string Roll, string Pressing, int DaysLeft)
		{
			string creedName = KingdomCreed.CreedName(Pressing);
			KingdomWord.Warn(System, System.SeatName, KingdomWord.StandsIn(Z),
				KingdomConversionRules.PressureNote(Roll, creedName) + " " + KingdomBrinkRules.WindowPhrase(DaysLeft),
				KingdomConversionRules.PressureTelling(Roll, creedName),
				null);
		}

		// Names that have left the roll are names nothing will ever pull at again. Pruned so a
		// departed settler's progress cannot be inherited by a later settler of the same name, and
		// so both maps stay the size of the city rather than of its history.
		private static void ForgetDeparted(KingdomSystem System)
		{
			Prune(System.ConversionShared, System.RosterNames);
			Prune(System.ConversionResented, System.RosterNames);
			List<string> stale = null;
			foreach (KeyValuePair<string, string> entry in System.ConversionToward)
			{
				if (!System.ConversionShared.ContainsKey(entry.Key))
				{
					if (stale == null)
					{
						stale = new List<string>();
					}
					stale.Add(entry.Key);
				}
			}
			if (stale == null)
			{
				return;
			}
			for (int i = 0; i < stale.Count; i++)
			{
				System.ConversionToward.Remove(stale[i]);
			}
		}

		private static void Prune(Dictionary<string, int> Map, List<string> Roll)
		{
			if (Map.Count == 0)
			{
				return;
			}
			List<string> gone = null;
			foreach (KeyValuePair<string, int> entry in Map)
			{
				if (!Roll.Contains(entry.Key))
				{
					if (gone == null)
					{
						gone = new List<string>();
					}
					gone.Add(entry.Key);
				}
			}
			if (gone == null)
			{
				return;
			}
			for (int i = 0; i < gone.Count; i++)
			{
				Map.Remove(gone[i]);
			}
		}

		// --- Facts about people, and the two maps that remember them ----------------------

		private static ConversionProgress ProgressOf(KingdomSystem System, string Roll)
		{
			string toward;
			int shared;
			if (!System.ConversionToward.TryGetValue(Roll, out toward) || !System.ConversionShared.TryGetValue(Roll, out shared))
			{
				return ConversionProgress.None;
			}
			return new ConversionProgress(toward, shared);
		}

		private static void SetProgress(KingdomSystem System, string Roll, ConversionProgress Progress)
		{
			if (!Progress.Any)
			{
				System.ConversionShared.Remove(Roll);
				System.ConversionToward.Remove(Roll);
				return;
			}
			System.ConversionShared[Roll] = Progress.Shared;
			System.ConversionToward[Roll] = Progress.Creed;
		}

		private static Dictionary<string, List<GameObject>> Households(List<GameObject> Residents)
		{
			Dictionary<string, List<GameObject>> households = new Dictionary<string, List<GameObject>>();
			for (int i = 0; i < Residents.Count; i++)
			{
				string plotId = Residents[i].GetStringProperty(KingdomLodging.HomePlotIdProperty);
				if (string.IsNullOrEmpty(plotId))
				{
					continue;
				}
				List<GameObject> under;
				if (!households.TryGetValue(plotId, out under))
				{
					under = new List<GameObject>();
					households[plotId] = under;
				}
				under.Add(Residents[i]);
			}
			return households;
		}

		private static Dictionary<string, int> CreedCounts(List<GameObject> People)
		{
			Dictionary<string, int> counts = new Dictionary<string, int>();
			for (int i = 0; i < People.Count; i++)
			{
				string creed = People[i].GetStringProperty(KingdomCreed.CreedProperty);
				if (string.IsNullOrEmpty(creed))
				{
					continue;
				}
				int held;
				counts.TryGetValue(creed, out held);
				counts[creed] = held + 1;
			}
			return counts;
		}

		private static List<GameObject> ResidentsIn(Zone Z)
		{
			List<GameObject> list = new List<GameObject>();
			foreach (GameObject item in Z.GetObjects())
			{
				if (item.GetIntProperty("KingdomCitizen") == 1)
				{
					list.Add(item);
				}
			}
			return list;
		}

		// The name the roll carries this person under, which is the key both maps are filed by and
		// the name the registers will write. Null for anybody the roll does not carry.
		private static string RollNameOf(GameObject Resident)
		{
			string name = (Resident == null) ? null : Resident.GetStringProperty("KingdomName");
			return string.IsNullOrEmpty(name) ? null : name;
		}
	}
}
