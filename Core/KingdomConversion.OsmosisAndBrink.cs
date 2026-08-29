using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomConversion
	{
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
			string settlementId = KingdomChronicle.SettlementId(System);
			if (!KingdomIdentityRules.IsSettlementId(settlementId)) return;
			bool turns = KingdomConversionRules.Converts(
				settlementId, channel, Roll, KingdomConversionRules.RoadEnd(roads));
			Resident.SetIntProperty(RoadsWalkedProperty, roads + 1);
			bool here = KingdomWord.StandsIn(Z);
			int ago = KingdomBrinkRules.DaysStood(KingdomBrinkRules.ExpiryTick(BrinkKind.Creed, Brink.WarnedTick), Now);
			if (turns && Convert(System, Z, Resident, Brink.Cause, channel))
			{
				// Convert clears the brink and both maps and writes its own two registers. All
				// that is owed here is the date: the founder was told this was coming, and this is
				// the day it came.
				KingdomWord.Aftermath(System, System.SeatName, here,
					KingdomBrinkRules.FiredNote(BrinkKind.Creed,
						KingdomPresentation.Rich(Roll), ago));
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
	}
}
