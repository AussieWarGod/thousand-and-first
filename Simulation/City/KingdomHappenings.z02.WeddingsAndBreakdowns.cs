using System;

using XRL;
using XRL.UI;
using XRL.World;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	public static partial class KingdomHappenings
	{

		// ==================================================================================
		// Weddings — two rows that already share a roof
		// ==================================================================================

		private static KingdomCityState Weddings(KingdomSystem System, KingdomCityBook book,
			KingdomCityState state, string label, bool here, long nowTick, ref int pushed,
			int pushBudget)
		{
			int draws = 0;
			int found = 0;
			for (int i = 0; i < state.ResidentCount && found < MaxWeddingsPerReckon && draws < MaxWeddingDraws; i++)
			{
				KingdomResidentRow a;
				if (!state.TryResident(i, out a) || a.Standing != KingdomResidentStanding.Resident || a.HomeWorkId <= 0)
				{
					continue;
				}
				for (int j = i + 1; j < state.ResidentCount && found < MaxWeddingsPerReckon && draws < MaxWeddingDraws; j++)
				{
					KingdomResidentRow b;
					// One integer comparison rejects every pair that does not already share a
					// roof, which is nearly all of them, before anything more expensive happens.
					if (!state.TryResident(j, out b) || b.HomeWorkId != a.HomeWorkId)
					{
						continue;
					}
					int hostility = KingdomHappeningRules.CreedHostility(a.CreedCode, b.CreedCode);
					if (!KingdomHappeningRules.WeddingEligible(a, b, hostility, nowTick))
					{
						continue;
					}
					int first;
					int second;
					KingdomHappeningRules.PairOrder(a.ResidentId, b.ResidentId, out first, out second);
					if (KingdomHappeningRules.AlreadyTold(state, KingdomHappeningKind.Wedding, first, second)
						|| KingdomPhysicalHappenings.AlreadyCompleted(book,
							KingdomPhysicalHappeningKind.Wedding, first, second, nowTick))
					{
						continue;
					}
					draws++;
					// The ordinal is the WORLD-DAY, not the tick. A slice runs every fifty ticks,
					// so a per-tick ordinal would give a settled pair twenty-four rolls a day and
					// marry the whole city inside a week. One roll a day per pair is the honest
					// reading of "a chance per pair per reckoning", and it is still reload-stable:
					// the same day and the same pair always draw the same answer.
					if (!Drawn(book.SettlementId, WeddingStreamId, WeddingEventKind, WeddingDrawIndex,
						unchecked((ulong)(((long)first << 20) ^ second ^ (KingdomAmbientRules.DayOrdinal(nowTick) << 40))),
						KingdomHappeningRules.WeddingChancePercent))
					{
						continue;
					}
					found++;
					state = Marry(System, book, state, label, here, nowTick, a, b, first,
						second, ref pushed, pushBudget);
				}
			}
			return state;
		}

		private static KingdomCityState Marry(KingdomSystem System, KingdomCityBook book,
			KingdomCityState state, string label, bool here, long nowTick, KingdomResidentRow a,
			KingdomResidentRow b, int first, int second, ref int pushed, int pushBudget)
		{
			string one = Named(a.Name);
			string other = Named(b.Name);
			string telling = KingdomHappeningRules.WeddingTelling(one, other,
				KingdomPresentation.Rich(KingdomWord.CityName(System, label)));
			Zone zone = here ? The.Player?.CurrentZone : null;
			KingdomPhysicalQueueResult result = KingdomPhysicalHappenings.QueueGeneric(System,
				book, KingdomPhysicalHappeningKind.Wedding, nowTick, first, second, 0, zone,
				new[] { first, second }, telling, DatedReport(nowTick, telling), "", "",
				KingdomVoices.Say(System, VoiceOccasion.Wedding,
				KingdomHappeningRules.WeddingNotice(one, other)), "", "",
				"shared-water bench", CurrentTick(nowTick));
			KingdomCityState next = Refresh(book, state);
			if (KingdomHappeningRules.AlreadyTold(next, KingdomHappeningKind.Wedding, first,
				second)) KingdomLog.Log("happening: wedding " + one + " + " + other + " at "
				+ label + " physical=" + (result == KingdomPhysicalQueueResult.AttendedReady));
			return next;
		}

		// ==================================================================================
		// Breakdowns — announce once, and unsay when it turns again
		// ==================================================================================

		private static KingdomCityState Breakdowns(KingdomSystem System, KingdomCityState state, string label, bool here, long nowTick, ref int pushed, int pushBudget)
		{
			int found = 0;
			for (int i = 0; i < state.WorkCount && found < MaxBreakdownsPerReckon; i++)
			{
				KingdomWorkRow row;
				if (!state.TryWork(i, out row) || row.WorkId <= 0)
				{
					continue;
				}
				KingdomHappening happening = KingdomHappeningRules.Judge(row, BelievedBroken(state, row.WorkId), nowTick);
				if (!happening.Stands)
				{
					continue;
				}
				found++;
				bool broken = !KingdomHappeningRules.IsMending(happening.Outcome);
				string name = KingdomUpgrade.DisplayNameOf(row.DesignKey);
				string shownName = KingdomPresentation.Rich(name);
				state = Tell(state, happening);
				if (broken)
				{
					KingdomChronicle.Record(System,
						KingdomHappeningRules.BreakdownTelling(shownName,
							KingdomPresentation.Rich(KingdomWord.CityName(System, label)),
							row.ConditionPercent));
					if (pushed < pushBudget)
					{
						KingdomWord.Ambient(System, label, here,
							KingdomHappeningRules.BreakdownNotice(shownName,
								row.ConditionPercent));
						pushed++;
					}
				}
				else if (pushed < pushBudget)
				{
					// The unsaying, in the lane Addendum 10(a) built for it: a founder told from a
					// distance that their mill had stopped is owed the withdrawal from the same
					// distance. Not a chronicle entry - the book records what happened, and a
					// thing that stopped happening is news for the report.
					KingdomWord.Unsay(System, label, here,
						KingdomHappeningRules.MendedNotice(shownName, row.ConditionPercent));
					pushed++;
				}
				KingdomLog.Log("happening: " + (broken ? "breakdown " : "mended ") + name + " work=" + row.WorkId + " condition=" + row.ConditionPercent);
			}
			return state;
		}

		/// <summary>
		/// What the city last said about this work, read off the told-log ring rather than off a
		/// second ledger kept for the purpose.
		/// <para>
		/// The ring is bounded at thirty-two lines and overwrites its oldest, so a belief can be
		/// forgotten &mdash; and a forgotten belief reads as "not broken", which is the safe
		/// direction: the worst it can cost is one repeated announcement about a work that has
		/// been broken for thirty-two happenings, and never a mending that is never announced.
		/// </para>
		/// </summary>
		private static bool BelievedBroken(KingdomCityState state, int workId)
		{
			bool believed = false;
			for (int i = 0; i < state.ToldCount; i++)
			{
				KingdomToldRow row;
				if (!state.TryTold(i, out row) || row.Kind != KingdomToldKind.Breakdown || row.SubjectA != workId)
				{
					continue;
				}
				believed = !KingdomHappeningRules.IsMending(row.Outcome);
			}
			return believed;
		}
	}
}
