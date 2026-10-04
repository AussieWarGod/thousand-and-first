using System;
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.DevTests
{
	/// <summary>#244/#257: the window digest authenticates the window's open intents only.
	/// Ordinary city evolution inside a window must not refuse reconciliation, write dispatch
	/// state, mint or replay work. Open intents are re-proved at their frozen slot or withdrawn
	/// and reported; nothing is lost silently.</summary>
	[TestFixture]
	public sealed partial class KingdomPolityDispatchWindowDriftTests
	{
		private const string B =
			"taf:settlement:v1:dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
		private const string C =
			"taf:settlement:v1:eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
		private const long Period = KingdomPolityDispatchRules.PeriodTicks;
		private const long Day = KingdomPolityDispatchRules.CalendarDayTicks;

		public enum Drift { Storage, Population, Stage, Shop, Deed, GuardReading, PatrolReading,
			Wear, Name, SeatExchange }

		[TestCase(Drift.Storage)] [TestCase(Drift.Population)] [TestCase(Drift.Stage)]
		[TestCase(Drift.Shop)] [TestCase(Drift.Deed)] [TestCase(Drift.GuardReading)]
		[TestCase(Drift.PatrolReading)] [TestCase(Drift.Wear)] [TestCase(Drift.Name)]
		[TestCase(Drift.SeatExchange)]
		public void CompletedWindowReconcilesOrdinaryDriftWithoutWorkOrWrite(Drift change)
		{
			KingdomPolityDispatchState state = OpenAndComplete(Offer(2, Period * 30L + 1L));
			KingdomPolityDispatchState before = KingdomPolityDispatchRules.CloneState(state);
			KingdomPolityDispatchOffer later = Offer(2, Period * 30L + Day);
			Apply(later, change);
			ClassicAssert.IsTrue(Open(state, later, out List<KingdomPolityDueWork> work,
				out bool drifted, out List<string> withdrawn, out string failure), failure);
			ClassicAssert.AreEqual(0, work.Count, "drift inside a window must not mint work");
			ClassicAssert.IsTrue(drifted, "the drift must be visible to the caller");
			ClassicAssert.AreEqual(0, withdrawn.Count);
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.SameState(before, state),
				"ordinary drift must not rewrite frozen dispatch authority");
		}

		[Test]
		public void Issue257PopulationZeroCampReconcilesEveryDayOfItsWindow()
		{
			KingdomPolityDispatchState state = new KingdomPolityDispatchState();
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryOpen(state, Camp(96001L),
				out List<KingdomPolityDueWork> work, out string failure), failure);
			ClassicAssert.AreEqual(0, work.Count); ClassicAssert.AreEqual(1, state.CompletedMask);
			KingdomPolityDispatchState frozen = KingdomPolityDispatchRules.CloneState(state);
			for (long tick = 97200L; tick < 100800L; tick += Day)
			{
				ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryOpen(state, Camp(tick),
					out work, out failure), tick + ": " + failure);
				ClassicAssert.AreEqual(0, work.Count);
				ClassicAssert.IsTrue(KingdomPolityDispatchRules.SameState(frozen, state));
			}
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryOpen(state, Camp(100800L),
				out work, out failure), failure);
			ClassicAssert.AreEqual(12UL, state.LastWindowOrdinal, "window 12 still opens");
		}

		[Test]
		public void DriftNeverRemintsTheWindowAndTheNextWindowStillDispatchesOnce()
		{
			KingdomPolityDispatchState state = OpenAndComplete(Offer(3, Period * 40L));
			KingdomPolityDispatchOffer drifted = Offer(3, Period * 40L + 3L * Day);
			Apply(drifted, Drift.Storage); Apply(drifted, Drift.PatrolReading);
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryOpen(state, drifted,
				out List<KingdomPolityDueWork> work, out string failure), failure);
			ClassicAssert.AreEqual(0, work.Count);
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryOpen(state,
				Offer(3, Period * 40L + 4L * Day), out work, out failure), failure);
			ClassicAssert.AreEqual(0, work.Count, "restored facts must not re-mint the window");
			KingdomPolityDispatchOffer next = Offer(3, Period * 41L);
			Apply(next, Drift.Storage);
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryOpen(state, next,
				out work, out failure), failure);
			ClassicAssert.AreEqual(3, work.Count, "the next window opens on the new facts");
			ClassicAssert.IsFalse(KingdomPolityDispatchRules.TryOpen(state,
				Offer(3, Period * 40L), out work, out failure));
			ClassicAssert.AreEqual("polity dispatch clock regressed", failure);
		}

		[Test]
		public void SecondCityFoundedInsideCompleteWindowWaitsWithoutWrite()
		{
			KingdomPolityDispatchState state = OpenAndComplete(Offer(1, Period * 50L));
			KingdomPolityDispatchState before = KingdomPolityDispatchRules.CloneState(state);
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryOpen(state,
				Offer(2, Period * 50L + Day), out List<KingdomPolityDueWork> work,
				out string failure), failure);
			ClassicAssert.AreEqual(0, work.Count, "a city founded mid-window waits for the next");
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.SameState(before, state),
				"no re-key: count, digest and revision stay frozen");
			KingdomPolityDispatchOffer seatedSecond = Offer(2, Period * 50L + 2L * Day);
			Apply(seatedSecond, Drift.SeatExchange);
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryOpen(state, seatedSecond,
				out work, out failure), failure);
			ClassicAssert.AreEqual(0, work.Count);
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.SameState(before, state));
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryOpen(state,
				Offer(2, Period * 51L), out work, out failure), failure);
			ClassicAssert.AreEqual(2, work.Count);
			ClassicAssert.AreEqual(2, state.EndpointCount);
		}

		[Test]
		public void LostCityWithdrawsOnlyItsOwnIntentAndReprovesTheSurvivors()
		{
			KingdomPolityDispatchState state = new KingdomPolityDispatchState();
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryOpen(state, Offer(3, Period * 60L),
				out List<KingdomPolityDueWork> first, out string failure), failure);
			ClassicAssert.AreEqual(3, first.Count); // crash cut: three intents left open
			long revision = state.Revision;
			ClassicAssert.IsTrue(Open(state, Offer(2, Period * 60L + Day),
				out List<KingdomPolityDueWork> work, out bool drifted,
				out List<string> withdrawn, out failure), failure);
			ClassicAssert.IsTrue(drifted);
			ClassicAssert.AreEqual(2, work.Count, "surviving cities keep their frozen work");
			ClassicAssert.AreEqual(first[0].CohortId, work[0].CohortId);
			ClassicAssert.AreEqual(first[1].CohortId, work[1].CohortId);
			ClassicAssert.AreEqual(1, withdrawn.Count);
			StringAssert.Contains(C, withdrawn[0]);
			StringAssert.Contains("its settlement left the realm", withdrawn[0]);
			ClassicAssert.AreEqual(3, state.EndpointCount, "frozen slots are not re-keyed");
			ClassicAssert.AreEqual(4, state.CompletedMask);
			ClassicAssert.IsNull(KingdomPolityDispatchRules.FindIntent(state, 2));
			ClassicAssert.IsNotNull(KingdomPolityDispatchRules.FindIntent(state, 0));
			ClassicAssert.AreEqual(revision + 1L, state.Revision);
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.ValidState(state, out failure), failure);
		}

		[Test]
		public void SurvivingCityIntentIsKeptWhenAnotherCityIsFoundedMidWindow()
		{
			KingdomPolityDispatchState state = new KingdomPolityDispatchState();
			KingdomPolityDispatchOffer open = Pair(KingdomPolityTestData.Settlement, C,
				Period * 62L);
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryOpen(state, open,
				out List<KingdomPolityDueWork> first, out string failure), failure);
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryComplete(state, 62UL, 0,
				out failure), failure);
			KingdomPolityDispatchState before = KingdomPolityDispatchRules.CloneState(state);
			// B sorts between A and C: C's live ordinal moves 1 -> 2, its frozen slot stays 1.
			ClassicAssert.IsTrue(Open(state, Offer(3, Period * 62L + Day),
				out List<KingdomPolityDueWork> work, out bool _, out List<string> withdrawn,
				out failure), failure);
			ClassicAssert.AreEqual(0, withdrawn.Count);
			ClassicAssert.AreEqual(1, work.Count);
			ClassicAssert.AreEqual(first[1].CohortId, work[0].CohortId);
			ClassicAssert.AreEqual(1, work[0].EndpointOrdinal);
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.SameState(before, state));
		}

		[Test]
		public void OpenIntentWhoseOwnFactsChangedIsWithdrawnAndReportedOnce()
		{
			KingdomPolityDispatchState state = new KingdomPolityDispatchState();
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryOpen(state, Offer(1, Period * 70L),
				out List<KingdomPolityDueWork> first, out string failure), failure);
			ClassicAssert.AreEqual(1, first.Count); // partial failure: intent left open
			KingdomPolityDispatchOffer drifted = Offer(1, Period * 70L + Day);
			Apply(drifted, Drift.Storage);
			ClassicAssert.IsTrue(Open(state, drifted, out List<KingdomPolityDueWork> work,
				out bool _, out List<string> withdrawn, out failure), failure);
			ClassicAssert.AreEqual(0, work.Count);
			ClassicAssert.AreEqual(1, withdrawn.Count);
			StringAssert.Contains("window 70", withdrawn[0]);
			StringAssert.Contains("its source facts changed after the window opened", withdrawn[0]);
			ClassicAssert.AreEqual(1, state.CompletedMask);
			KingdomPolityDispatchState after = KingdomPolityDispatchRules.CloneState(state);
			ClassicAssert.IsTrue(Open(state, Offer(1, Period * 70L + 2L * Day), out work,
				out bool _, out withdrawn, out failure), failure);
			ClassicAssert.AreEqual(0, work.Count, "restored facts never re-mint a withdrawn slot");
			ClassicAssert.AreEqual(0, withdrawn.Count, "a withdrawal is reported once");
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.SameState(after, state));
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryOpen(state, Offer(1, Period * 71L),
				out work, out failure), failure);
			ClassicAssert.AreEqual(1, work.Count); ClassicAssert.AreEqual(71UL, work[0].WindowOrdinal);
		}

		[Test]
		public void OpenIntentIsReprovedWhenOnlyAnotherCityDrifted()
		{
			KingdomPolityDispatchState state = new KingdomPolityDispatchState();
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryOpen(state, Offer(2, Period * 72L),
				out List<KingdomPolityDueWork> first, out string failure), failure);
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryComplete(state, 72UL, 1,
				out failure), failure);
			KingdomPolityDispatchOffer later = Offer(2, Period * 72L + Day);
			later.Endpoints[1].KnownStorageSpace = 40; // only B moved
			ClassicAssert.IsTrue(Open(state, later, out List<KingdomPolityDueWork> work,
				out bool drifted, out List<string> withdrawn, out failure), failure);
			ClassicAssert.IsTrue(drifted); ClassicAssert.AreEqual(0, withdrawn.Count);
			ClassicAssert.AreEqual(1, work.Count);
			ClassicAssert.AreEqual(first[0].CohortId, work[0].CohortId);
		}

		[Test]
		public void PausedWindowDriftSuppressesAndReportsOpenIntents()
		{
			KingdomPolityDispatchState state = new KingdomPolityDispatchState();
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryOpen(state, Offer(1, Period * 74L),
				out List<KingdomPolityDueWork> _, out string failure), failure);
			KingdomPolityDispatchOffer drifted = Offer(1, Period * 74L + Day);
			Apply(drifted, Drift.Storage);
			ClassicAssert.IsTrue(Open(state, drifted, false, out List<KingdomPolityDueWork> work,
				out bool _, out List<string> withdrawn, out failure), failure);
			ClassicAssert.AreEqual(0, work.Count); ClassicAssert.AreEqual(1, state.CompletedMask);
			ClassicAssert.AreEqual(1, withdrawn.Count);
			StringAssert.Contains("new polity causes are paused", withdrawn[0]);
		}

		[Test]
		public void RolloverReportsUnfrozenIntentItDrops()
		{
			KingdomPolityDispatchState state = new KingdomPolityDispatchState();
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryOpen(state, Offer(1, Period * 76L),
				out List<KingdomPolityDueWork> _, out string failure), failure);
			ClassicAssert.IsTrue(Open(state, Offer(1, Period * 77L), out List<KingdomPolityDueWork> work,
				out bool _, out List<string> withdrawn, out failure), failure);
			ClassicAssert.AreEqual(1, work.Count);
			ClassicAssert.AreEqual(1, withdrawn.Count);
			StringAssert.Contains("window 76", withdrawn[0]);
			StringAssert.Contains("its window closed before the visit was frozen", withdrawn[0]);
		}

		[Test]
		public void ForgedOpenIntentIsStillRefused()
		{
			KingdomPolityDispatchState state = new KingdomPolityDispatchState();
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryOpen(state, Offer(1, Period * 80L),
				out List<KingdomPolityDueWork> _, out string failure), failure);
			KingdomPolityDirectRecord intent = KingdomPolityDispatchRules.FindIntent(state, 0);
			intent.EndpointVerb = intent.EndpointVerb.Replace("storage-space=10", "storage-space=11");
			KingdomPolityDispatchOffer drifted = Offer(1, Period * 80L + Day);
			Apply(drifted, Drift.Storage);
			ClassicAssert.IsFalse(KingdomPolityDispatchRules.TryOpen(state, drifted,
				out List<KingdomPolityDueWork> _, out failure));
			ClassicAssert.AreEqual("polity dispatch state is invalid", failure);
		}

		[Test]
		public void ForeignRealmOfferIsStillRefused()
		{
			KingdomPolityDispatchState state = OpenAndComplete(Offer(1, Period * 90L));
			KingdomPolityDispatchOffer foreign = Offer(1, Period * 90L + Day);
			foreign.RealmId = "taf:realm:v1:" + new string('9', 64);
			ClassicAssert.IsFalse(KingdomPolityDispatchRules.TryOpen(state, foreign,
				out List<KingdomPolityDueWork> _, out string failure));
			ClassicAssert.AreEqual("polity dispatch belongs to another realm", failure);
		}
	}
}
