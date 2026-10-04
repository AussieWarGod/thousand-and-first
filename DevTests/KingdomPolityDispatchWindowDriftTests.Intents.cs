using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.DevTests
{
	/// <summary>#244/#257 open-intent pins over production-shaped facts. An open intent survives
	/// a drifted window only when the live facts re-prove it byte for byte at its frozen slot.
	/// Guard and patrol causes bind the zone read tick (and the work's ran-through tick), so a
	/// check-in that re-reads the zone withdraws them; courier, trader and migrant causes bind the
	/// owned topology and their source settlement's facts, so a founding or a loss anywhere, or a
	/// new deed, market tier or population at the source, withdraws them. Each withdrawal is
	/// committed, completes its slot and is reported once.</summary>
	public sealed partial class KingdomPolityDispatchWindowDriftTests
	{
		private const KingdomPolityCohortPurpose Guard = KingdomPolityCohortPurpose.Guard;
		private const KingdomPolityCohortPurpose Patrol = KingdomPolityCohortPurpose.Patrol;
		private const KingdomPolityCohortPurpose Courier = KingdomPolityCohortPurpose.Courier;
		private const KingdomPolityCohortPurpose Trader = KingdomPolityCohortPurpose.Trader;
		private const KingdomPolityCohortPurpose Migrant = KingdomPolityCohortPurpose.Migrant;
		private const string Changed = " withdrawn: its source facts changed after the window opened";

		[Test]
		public void LostCityWithdrawsItsIntentAndTopologyBoundIntentsAndKeepsAnUnreadPatrol()
		{
			Site a = CityA(), b = CityB();
			KingdomPolityDispatchState state = OpenIntents(Realm(Period * 61L, a, b, CityC()),
				out List<KingdomPolityDueWork> first, Patrol, Courier, Trader);
			long revision = state.Revision;
			// Same day, before any check-in re-reads a zone: only the owned topology changed.
			ClassicAssert.IsTrue(Open(state, Realm(Period * 61L + 600L, a, b),
				out List<KingdomPolityDueWork> work, out bool drifted, out List<string> withdrawn,
				out string failure), failure);
			ClassicAssert.IsTrue(drifted);
			ClassicAssert.AreEqual(1, work.Count, "only the unread patrol survives");
			ClassicAssert.AreEqual(first[0].CohortId, work[0].CohortId);
			CollectionAssert.AreEqual(new[] { "window 61 Courier intent for " + B + Changed,
				"window 61 Trader intent for " + C + " withdrawn: its settlement left the realm" },
				withdrawn);
			ClassicAssert.AreEqual(3, state.EndpointCount, "frozen slots are not re-keyed");
			ClassicAssert.AreEqual(6, state.CompletedMask);
			ClassicAssert.IsNotNull(KingdomPolityDispatchRules.FindIntent(state, 0));
			ClassicAssert.AreEqual(revision + 1L, state.Revision);
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.ValidState(state, out failure), failure);
		}

		[Test]
		public void MidWindowFoundingWithdrawsTopologyBoundIntentsAndKeepsAnUnreadGuard()
		{
			Site a = CityA(), c = CityC();
			KingdomPolityDispatchState state = OpenIntents(Realm(Period * 64L, a, c),
				out List<KingdomPolityDueWork> first, Migrant, Guard);
			long revision = state.Revision;
			// B sorts between A and C: C's live ordinal moves 1 -> 2, its frozen slot stays 1.
			ClassicAssert.IsTrue(Open(state, Realm(Period * 64L + 600L, a, CityB(), c),
				out List<KingdomPolityDueWork> work, out bool drifted, out List<string> withdrawn,
				out string failure), failure);
			ClassicAssert.IsTrue(drifted);
			ClassicAssert.AreEqual(1, work.Count);
			ClassicAssert.AreEqual(first[1].CohortId, work[0].CohortId);
			ClassicAssert.AreEqual(1, work[0].EndpointOrdinal);
			CollectionAssert.AreEqual(new[] { "window 64 Migrant intent for "
				+ KingdomPolityTestData.Settlement + Changed }, withdrawn);
			ClassicAssert.AreEqual(2, state.EndpointCount, "count stays frozen after a founding");
			ClassicAssert.AreEqual(1, state.CompletedMask);
			ClassicAssert.AreEqual(revision + 1L, state.Revision);
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.ValidState(state, out failure), failure);
		}

		[Test]
		public void OwnedTopologyAloneWithdrawsAMigrantIntentWhoseSourceIsUnchanged()
		{
			Site a = CityA(), b = CityB();
			KingdomPolityDispatchState state = OpenIntents(Realm(Period * 69L, a, b),
				out List<KingdomPolityDueWork> first, Migrant, Guard);
			// C sorts after B, so A's migrant source stays B: only the owned topology changed.
			ClassicAssert.IsTrue(Open(state, Realm(Period * 69L + 600L, a, b, CityC()),
				out List<KingdomPolityDueWork> work, out bool drifted, out List<string> withdrawn,
				out string failure), failure);
			ClassicAssert.IsTrue(drifted);
			ClassicAssert.AreEqual(1, work.Count);
			ClassicAssert.AreEqual(first[1].CohortId, work[0].CohortId);
			CollectionAssert.AreEqual(new[] { "window 69 Migrant intent for "
				+ KingdomPolityTestData.Settlement + Changed }, withdrawn);
			ClassicAssert.AreEqual(1, state.CompletedMask);
		}

		[Test]
		public void CourierIntentIsWithdrawnWhenOnlyItsSourceCityRaisedADeed()
		{
			Site a = CityA(), b = CityB();
			KingdomPolityDispatchState state = OpenIntents(Realm(Period * 71L, a, b),
				out List<KingdomPolityDueWork> _, Patrol, Courier);
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryComplete(state, 71UL, 0,
				out string failure), failure);
			long revision = state.Revision;
			Site[] raised = Copies(a, b);
			Apply(raised, Drift.Deed, Period * 71L + 600L); // only A raised a building
			ClassicAssert.IsTrue(Open(state, Realm(Period * 71L + 600L, raised),
				out List<KingdomPolityDueWork> work, out bool drifted, out List<string> withdrawn,
				out failure), failure);
			ClassicAssert.IsTrue(drifted); ClassicAssert.AreEqual(0, work.Count);
			CollectionAssert.AreEqual(new[] { "window 71 Courier intent for " + B + Changed },
				withdrawn);
			ClassicAssert.AreEqual(3, state.CompletedMask);
			ClassicAssert.AreEqual(revision + 1L, state.Revision);
		}

		[Test]
		public void DailyReadingWithdrawsTheOpenGuardIntentOnceAndNeverRemintsIt()
		{
			Site a = CityA();
			KingdomPolityDispatchState state = OpenIntents(Realm(Period * 70L, a),
				out List<KingdomPolityDueWork> _, Guard);
			Site[] read = Copies(a);
			Apply(read, Drift.ZoneRead, Period * 70L + Day); // the next day's check-in
			ClassicAssert.IsTrue(Open(state, Realm(Period * 70L + Day, read),
				out List<KingdomPolityDueWork> work, out bool drifted, out List<string> withdrawn,
				out string failure), failure);
			ClassicAssert.IsTrue(drifted); ClassicAssert.AreEqual(0, work.Count);
			CollectionAssert.AreEqual(new[] { "window 70 Guard intent for "
				+ KingdomPolityTestData.Settlement + Changed }, withdrawn);
			ClassicAssert.AreEqual(1, state.CompletedMask);
			KingdomPolityDispatchState after = KingdomPolityDispatchRules.CloneState(state);
			ClassicAssert.IsTrue(Open(state, Realm(Period * 70L + 2L * Day, a), out work,
				out drifted, out withdrawn, out failure), failure);
			ClassicAssert.IsFalse(drifted, "facts equal to the frozen digest are not drift");
			ClassicAssert.AreEqual(0, work.Count, "restored facts never re-mint a withdrawn slot");
			ClassicAssert.AreEqual(0, withdrawn.Count, "a withdrawal is reported once");
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.SameState(after, state));
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryOpen(state, Realm(Period * 71L, read),
				out work, out failure), failure);
			ClassicAssert.AreEqual(1, work.Count); ClassicAssert.AreEqual(71UL, work[0].WindowOrdinal);
		}

		[Test]
		public void OpenIntentIsReprovedWhenAnotherCityDriftsOutsideItsCause()
		{
			Site a = CityA(), b = CityB();
			KingdomPolityDispatchState state = OpenIntents(Realm(Period * 72L, a, b),
				out List<KingdomPolityDueWork> first, Courier, Trader);
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryComplete(state, 72UL, 1,
				out string failure), failure);
			KingdomPolityDispatchState before = KingdomPolityDispatchRules.CloneState(state);
			Site[] later = Copies(a, b);
			later[1].Storage = 40; // only B's stores moved; A's courier cause reads B's deed
			ClassicAssert.IsTrue(Open(state, Realm(Period * 72L + Day, later),
				out List<KingdomPolityDueWork> work, out bool drifted, out List<string> withdrawn,
				out failure), failure);
			ClassicAssert.IsTrue(drifted); ClassicAssert.AreEqual(0, withdrawn.Count);
			ClassicAssert.AreEqual(1, work.Count);
			ClassicAssert.AreEqual(first[0].CohortId, work[0].CohortId);
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.SameState(before, state),
				"a re-proved intent writes nothing");
		}

		[Test]
		public void PausedWindowDriftSuppressesAndReportsOpenIntents()
		{
			Site a = CityA();
			KingdomPolityDispatchState state = OpenIntents(Realm(Period * 74L, a),
				out List<KingdomPolityDueWork> _, Guard);
			Site[] drifted = Copies(a);
			Apply(drifted, Drift.Storage, Period * 74L + Day);
			ClassicAssert.IsTrue(Open(state, Realm(Period * 74L + Day, drifted), false,
				out List<KingdomPolityDueWork> work, out bool _, out List<string> withdrawn,
				out string failure), failure);
			ClassicAssert.AreEqual(0, work.Count); ClassicAssert.AreEqual(1, state.CompletedMask);
			CollectionAssert.AreEqual(new[] { "window 74 Guard intent for "
				+ KingdomPolityTestData.Settlement + " withdrawn: new polity causes are paused" },
				withdrawn);
		}

		[Test]
		public void RolloverReportsUnfrozenIntentItDrops()
		{
			Site a = CityA();
			KingdomPolityDispatchState state = OpenIntents(Realm(Period * 76L, a),
				out List<KingdomPolityDueWork> _, Guard);
			ClassicAssert.IsTrue(Open(state, Realm(Period * 77L, a),
				out List<KingdomPolityDueWork> work, out bool drifted, out List<string> withdrawn,
				out string failure), failure);
			ClassicAssert.IsFalse(drifted, "opening the next window is not drift");
			ClassicAssert.AreEqual(1, work.Count);
			CollectionAssert.AreEqual(new[] { "window 76 Guard intent for "
				+ KingdomPolityTestData.Settlement
				+ " withdrawn: its window closed before the visit was frozen" }, withdrawn);
		}

		[Test]
		public void SchedulerNotesLogEachWithdrawalOnceThenTheDriftLine()
		{
			Site a = CityA(), b = CityB();
			KingdomPolityDispatchState state = OpenIntents(Realm(Period * 61L, a, b, CityC()),
				out List<KingdomPolityDueWork> _, Patrol, Courier, Trader);
			ClassicAssert.IsTrue(Open(state, Realm(Period * 61L + 600L, a, b),
				out List<KingdomPolityDueWork> _, out bool drifted, out List<string> withdrawn,
				out string failure), failure);
			string courier = "polity: window 61 Courier intent for " + B + Changed;
			string trader = "polity: window 61 Trader intent for " + C
				+ " withdrawn: its settlement left the realm";
			string drift = "polity: dispatch window 61 continues with endpoint facts changed since "
				+ "it opened; no new dispatch until window 62";
			List<string> lines = KingdomPolityDispatchRules.DispatchNotes(61UL, withdrawn, drifted);
			CollectionAssert.AreEqual(new[] { courier, trader, drift }, lines);
			CollectionAssert.AreEqual(new[] { courier, trader },
				KingdomPolityDispatchRules.DispatchNotes(61UL, withdrawn, false));
			CollectionAssert.AreEqual(new[] { drift },
				KingdomPolityDispatchRules.DispatchNotes(61UL, new List<string>(), true));
			CollectionAssert.IsEmpty(KingdomPolityDispatchRules.DispatchNotes(61UL,
				new List<string>(), false));
			foreach (string line in lines) StringAssert.DoesNotContain("refused", line);
		}
	}
}
