using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomConversion
	{
		public static void OnSharedMeal(KingdomSystem System, Zone Z)
		{
			if (!Enabled || System == null || !System.Founded || Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			List<GameObject> attendees = ResidentsIn(System, Z);
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
		public static bool Convert(KingdomSystem System, Zone Z, GameObject Settler, string Creed,
			ConversionChannel Channel, string GovernanceVerb = null)
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
			string named = string.IsNullOrEmpty(roll) ? Settler.BaseDisplayNameStripped : roll;
			int hostility = KingdomCreed.HostilityBetween(was, Creed);
			// The existing surfaces, in the only order that keeps the tally honest: the old creed
			// is read off the settler by Forget, so it must go before Record overwrites it.
			KingdomCreed.Forget(System, Settler);
			if (!string.IsNullOrEmpty(GovernanceVerb) && !KingdomGovernanceScope.HasCommitted)
			{
				// Forget is the first durable tally/person mutation. Mark before Record or any
				// telling callback can fail, so a partially completed conversion is never free.
				KingdomGovernanceScope.Commit(GovernanceVerb);
			}
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
			string shownName = KingdomPresentation.Rich(named);
			string telling = KingdomConversionRules.ConversionTelling(Channel, shownName, creedName);
			if (KingdomConversionRules.Contested(hostility))
			{
				KingdomChronicle.RecordDisputed(System, telling, KingdomConversionRules.ConversionRumour(Channel, shownName, creedName));
			}
			else
			{
				KingdomChronicle.Record(System, telling);
			}
			System.Ledger.Note("{{G|" + KingdomConversionRules.ConversionNote(shownName, creedName) + "}}");
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
			List<GameObject> residents = ResidentsIn(System, Z);
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
	}
}
