using System.Collections.Generic;
using ThousandAndFirst.Simulation.Kernel;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomCreed
	{

		/// <summary>
		/// The kingdom's one attended pass over the two cities' tempers.
		/// <para>
		/// Preconditions: called from <c>ZoneActivatedEvent</c> on the realm's own ground, inside a
		/// <c>KingdomSystem.Guard</c>. Side effects: accrues dissent for the attended days since
		/// the last pass, may speak one line and write one chronicle entry, and may carry out a
		/// secession. Failure mode: returns having done nothing.
		/// </para>
		/// <para>
		/// Dissent runs the full elapsed (<see cref="KingdomRules.ElapsedDays"/>,
		/// <see cref="KingdomRules.AdvanceCheckpoint"/>), which is Addendum 8 clause 1: a quarrel
		/// between two cities is not something the founder's attention causes. The absence
		/// guarantee moved rather than went away, and it is now clause 3's: dissent CLAMPS at
		/// <see cref="KingdomCreedRules.DissentBreaking"/> and records a brink there, so a founder
		/// away a season and a founder away a thousand days come home to a realm standing in
		/// exactly the same place, told about it once with the real number of days, and holding
		/// <see cref="KingdomCreedRules.SecessionWindowDays"/> world-days in which pouring the
		/// rite or settling what the two cities believe still stops the split. Addendum 10(a): the
		/// window is the world's, so a founder who hears the word and stays away loses the city on
		/// the day it said they would &mdash; but nobody ever loses one unwarned.
		/// </para>
		/// </summary>
		public static void OnZoneActivated(KingdomSystem System, Zone Z)
		{
			if (!Enabled || System == null || !System.Founded || Z == null || System.SettlementCount < KingdomSettlement.MaxSettlements || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			long timeTicks = The.Game.TimeTicks;
			Reconcile(System);
			// A realm that has only just acquired its second city has no checkpoint yet. Starting
			// one costs nothing rather than charging the whole absence cap on the first pass.
			if (System.LastDissentTick <= 0)
			{
				System.LastDissentTick = timeTicks;
				return;
			}
			string here = SeatCreed(System);
			string there = AwayCreed(System);
			int hostility = HostilityBetween(here, there);
			if (KingdomBrink.CityStands())
			{
				// Nothing accrues past a brink, and the checkpoint is deliberately NOT advanced:
				// while the brink stands, LastDissentTick is the day the realm reached it, which
				// is what the announcement quotes and what the arrest resets.
				RunSecessionWindow(System, Z, hostility, timeTicks);
				return;
			}
			int days = KingdomRules.ElapsedDays(timeTicks - System.LastDissentTick);
			System.LastDissentTick = KingdomRules.AdvanceCheckpoint(System.LastDissentTick, timeTicks);
			if (days <= 0)
			{
				return;
			}
			int before = System.Dissent;
			System.Dissent = KingdomCreedRules.AccrueDissent(before, hostility, days);
			Announce(System, here, there);
			if (KingdomCreedRules.ClassifyTemper(System.Dissent) != CityTemper.Secession)
			{
				return;
			}
			// The breaking point was crossed somewhere inside the stretch, and the founder is owed
			// the day it was actually crossed on rather than the day they walked back in.
			long reached = KingdomBrinkRules.CrossingTick(
				timeTicks - (long)days * KingdomRules.TicksPerDay, timeTicks, before,
				KingdomCreedRules.DissentBreaking, KingdomCreedRules.DissentPerDay(hostility));
			RecordSecessionBrink(System, Z, here, there, reached, timeTicks);
		}

		// The realm reaches the breaking point: recorded, warned once by name with the honest
		// elapsed, and NOT acted on. This is the whole of what the clock rework owed secession --
		// the four-tier warning ladder used to end in a tier that had nothing to say, because by
		// the time it was reached the city was already gone.
		//
		// The warning is PUSHED (Addendum 10(a)). It goes to the founder wherever they stand, and
		// it names the arrest, because from here the window is the world's: nine days of it, and
		// then the city goes whether anybody came back or not.
		private static void RecordSecessionBrink(KingdomSystem System, Zone Z, string HereCreed, string ThereCreed, long ReachedTick, long NowTick)
		{
			if (!KingdomBrink.RecordCity(System, ReachedTick))
			{
				return;
			}
			KingdomBrink.MarkCityWarned(NowTick);
			SayTheCityBrink(System, Z, HereCreed, ThereCreed, NowTick);
		}

		// The realm's window, judged against the world's clock, and the arrest that ends it. Rule
		// 2: the quarrel is a fact re-derived every pass, so a realm whose creeds stopped clashing
		// -- or whose founder poured enough water to ease dissent back off the breaking point --
		// steps back from the edge and is told so, whenever they did it.
		//
		// What absence cannot do is start the clock. A realm carrying a brink nobody was ever
		// warned of (a save from before the word went out, a record made by a path that could not
		// speak) is warned here and gets the whole window from here.
		private static void RunSecessionWindow(KingdomSystem System, Zone Z, int Hostility, long NowTick)
		{
			string here = SeatCreed(System);
			string there = AwayCreed(System);
			string leaver;
			string kept;
			NameTheLeaver(System, here, there, out leaver, out kept);
			if (Hostility <= 0 || System.Dissent < KingdomCreedRules.DissentBreaking)
			{
				bool wasWarned = KingdomBrink.OfCity(System).Warned;
				if (KingdomBrink.LiftCity(System, NowTick))
				{
					if (wasWarned)
					{
						// Only what was actually said is unsaid.
						KingdomBrink.Unsay(System, BrinkKind.City, leaver, StandsInLeaver(System, Z, leaver), leaver);
					}
					Rearm(System);
				}
				return;
			}
			if (KingdomBrink.MarkCityWarned(NowTick))
			{
				SayTheCityBrink(System, Z, here, there, NowTick);
				return;
			}
			if (!KingdomBrink.CityWindowSpent(NowTick))
			{
				return;
			}
			long went = KingdomBrinkRules.ExpiryTick(BrinkKind.City, KingdomBrink.OfCity(System).WarnedTick);
			int ago = KingdomBrinkRules.DaysStood(went, NowTick);
			if (Secede(System, Forced: false, out var _))
			{
				KingdomWord.Aftermath(System, leaver, StandsInLeaver(System, Z, leaver),
					KingdomBrinkRules.FiredNote(BrinkKind.City,
						KingdomPresentation.Rich(leaver), ago));
				KingdomBrink.LiftCity(System, NowTick);
				return;
			}
			// The realm would not let it go -- the second city was already lost some other way, or
			// the verdict refused. The window stays spent and is tried again on the next resolve
			// rather than being reset, so nothing is lost and nobody is warned twice.
		}

		// The city brink's own voice. KingdomWord PUSHES the loud four-tier speech this ladder has
		// always ended in -- to wherever the founder is standing, framed as word out of the city
		// it is about when that is not the one they are in -- and FILES the shared brink note in
		// the report and the chronicle. Two registers of one warning, said once each, exactly as
		// this tier has always been told; what changed is that the speech now travels.
		private static void SayTheCityBrink(KingdomSystem System, Zone Z, string HereCreed, string ThereCreed, long NowTick)
		{
			string leaver;
			string kept;
			NameTheLeaver(System, HereCreed, ThereCreed, out leaver, out kept);
			BrinkRecord brink = KingdomBrink.OfCity(System);
			KingdomBrink.Announce(System, BrinkKind.City, leaver, kept, brink, NowTick,
				StandsInLeaver(System, Z, leaver), leaver,
				KingdomCreedRules.SecessionBrinkSpeech(leaver, kept,
					KingdomBrinkRules.DaysStood(brink.ReachedTick, NowTick),
					KingdomBrinkRules.DaysLeft(BrinkKind.City, brink.WarnedTick, NowTick)));
		}

		// Whether the founder is standing in the city the news is about. A realm's brink is news
		// about ONE of its two cities, and when that city is the one they are not in, the word
		// reaches them from it rather than around them.
		private static bool StandsInLeaver(KingdomSystem System, Zone Z, string Leaver)
		{
			return KingdomWord.StandsIn(Z) && !string.IsNullOrEmpty(Leaver) && Leaver == System.SeatName;
		}

		// Which city the prose should name as the one that walks. A prediction rather than a
		// decision -- KingdomCreedRules.AwayIsTheLeaver decides it again, from the same facts, on
		// the day itself -- but it is deterministic in exactly those facts, so a founder told
		// which city is drawing up its own charter is told the truth.
		private static void NameTheLeaver(KingdomSystem System, string HereCreed, string ThereCreed, out string Leaver, out string Kept)
		{
			string awayName = (System.Away != null) ? System.Away.SettlementName : null;
			bool awayLeaves = KingdomCreedRules.AwayIsTheLeaver(
				Feeling(HereCreed, ThereCreed), Feeling(ThereCreed, HereCreed),
				System.Population, (System.Away != null) ? System.Away.Population : 0);
			Leaver = awayLeaves ? awayName : System.SeatName;
			Kept = awayLeaves ? System.SeatName : awayName;
		}
	}
}
