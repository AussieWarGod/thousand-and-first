using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.DevTests
{
	/// <summary>#244/#257: the window digest authenticates the window's open intents only.
	/// Ordinary city evolution inside a window must not refuse reconciliation, write dispatch
	/// state, mint or replay work. Open intents are re-proved at their frozen slot or withdrawn
	/// and reported; nothing is lost silently. Every offer is built by production endpoint fact
	/// rules (see the Fixtures part); the open-intent pins are in the Intents part.</summary>
	[TestFixture]
	public sealed partial class KingdomPolityDispatchWindowDriftTests
	{
		private const string B =
			"taf:settlement:v1:dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
		private const string C =
			"taf:settlement:v1:eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
		private const long Period = KingdomPolityDispatchRules.PeriodTicks;
		private const long Day = KingdomPolityDispatchRules.CalendarDayTicks;

		/// <summary>Ordinary production inputs that change during a window: stores, growth, a
		/// deed, the zone read tick and work ran-through tick a check-in writes, defence, wear,
		/// the settlement's name and the seat during travel between owned cities.</summary>
		public enum Drift { Storage, Population, Stage, Shop, Deed, ZoneRead, WorkRun, Defence,
			Wear, Name, SeatExchange }

		[TestCase(Drift.Storage)] [TestCase(Drift.Population)] [TestCase(Drift.Stage)]
		[TestCase(Drift.Shop)] [TestCase(Drift.Deed)] [TestCase(Drift.ZoneRead)]
		[TestCase(Drift.WorkRun)] [TestCase(Drift.Defence)] [TestCase(Drift.Wear)]
		[TestCase(Drift.Name)] [TestCase(Drift.SeatExchange)]
		public void CompletedWindowReconcilesOrdinaryDriftWithoutWorkOrWrite(Drift change)
		{
			Site[] sites = { CityA(), CityB() };
			KingdomPolityDispatchState state = OpenAndComplete(Realm(Period * 30L + 1L, sites));
			KingdomPolityDispatchState before = KingdomPolityDispatchRules.CloneState(state);
			long tick = Period * 30L + Day;
			Apply(sites, change, tick);
			ClassicAssert.IsTrue(Open(state, Realm(tick, sites), out List<KingdomPolityDueWork> work,
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
			ClassicAssert.IsTrue(Open(state, Camp(96001L), out List<KingdomPolityDueWork> work,
				out bool drifted, out List<string> withdrawn, out string failure), failure);
			ClassicAssert.IsFalse(drifted, "the first pass opens window 11; it is not drift");
			ClassicAssert.AreEqual(0, work.Count); ClassicAssert.AreEqual(1, state.CompletedMask);
			KingdomPolityDispatchState frozen = KingdomPolityDispatchRules.CloneState(state);
			for (long tick = 97200L; tick < 100800L; tick += Day)
			{
				ClassicAssert.IsTrue(Open(state, Camp(tick), out work, out drifted, out withdrawn,
					out failure), tick + ": " + failure);
				ClassicAssert.IsTrue(drifted, tick + ": the day's readings change the camp's facts");
				ClassicAssert.AreEqual(0, work.Count); ClassicAssert.AreEqual(0, withdrawn.Count);
				ClassicAssert.IsTrue(KingdomPolityDispatchRules.SameState(frozen, state));
			}
			ClassicAssert.IsTrue(Open(state, Camp(100800L), out work, out drifted, out withdrawn,
				out failure), failure);
			ClassicAssert.IsFalse(drifted, "opening window 12 is not drift");
			ClassicAssert.AreEqual(12UL, state.LastWindowOrdinal, "window 12 still opens");
		}

		[Test]
		public void DriftNeverRemintsTheWindowAndTheNextWindowStillDispatchesOnce()
		{
			Site[] sites = { CityA(), CityB(), CityC() };
			KingdomPolityDispatchState state = OpenAndComplete(Realm(Period * 40L, sites));
			Site[] later = Copies(sites);
			Apply(later, Drift.Storage, Period * 40L + 3L * Day);
			Apply(later, Drift.ZoneRead, Period * 40L + 3L * Day);
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryOpen(state,
				Realm(Period * 40L + 3L * Day, later), out List<KingdomPolityDueWork> work,
				out string failure), failure);
			ClassicAssert.AreEqual(0, work.Count);
			ClassicAssert.IsTrue(Open(state, Realm(Period * 40L + 4L * Day, sites), out work,
				out bool drifted, out List<string> withdrawn, out failure), failure);
			ClassicAssert.IsFalse(drifted, "facts equal to the frozen digest are not drift");
			ClassicAssert.AreEqual(0, work.Count, "restored facts must not re-mint the window");
			Site[] next = Copies(sites);
			Apply(next, Drift.Storage, Period * 41L);
			ClassicAssert.IsTrue(Open(state, Realm(Period * 41L, next), out work, out drifted,
				out withdrawn, out failure), failure);
			ClassicAssert.IsFalse(drifted, "a window opened on new facts is not drift");
			ClassicAssert.AreEqual(0, withdrawn.Count);
			ClassicAssert.AreEqual(3, work.Count, "the next window opens on the new facts");
			ClassicAssert.IsFalse(KingdomPolityDispatchRules.TryOpen(state,
				Realm(Period * 40L, sites), out work, out failure));
			ClassicAssert.AreEqual("polity dispatch clock regressed", failure);
		}

		[Test]
		public void SecondCityFoundedInsideCompleteWindowWaitsWithoutWrite()
		{
			Site a = CityA();
			KingdomPolityDispatchState state = OpenAndComplete(Realm(Period * 50L, a));
			KingdomPolityDispatchState before = KingdomPolityDispatchRules.CloneState(state);
			ClassicAssert.IsTrue(Open(state, Realm(Period * 50L + Day, a, CityB()),
				out List<KingdomPolityDueWork> work, out bool drifted, out List<string> withdrawn,
				out string failure), failure);
			ClassicAssert.IsTrue(drifted); ClassicAssert.AreEqual(0, withdrawn.Count);
			ClassicAssert.AreEqual(0, work.Count, "a city founded mid-window waits for the next");
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.SameState(before, state),
				"no re-key: count, digest and revision stay frozen");
			Site[] seated = { a.Copy(), CityB() };
			Apply(seated, Drift.SeatExchange, Period * 50L + 2L * Day);
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryOpen(state,
				Realm(Period * 50L + 2L * Day, seated), out work, out failure), failure);
			ClassicAssert.AreEqual(0, work.Count);
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.SameState(before, state));
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryOpen(state,
				Realm(Period * 51L, a, CityB()), out work, out failure), failure);
			ClassicAssert.AreEqual(2, work.Count);
			ClassicAssert.AreEqual(2, state.EndpointCount);
		}

		[Test]
		public void ForgedOpenIntentIsStillRefused()
		{
			Site a = CityA();
			KingdomPolityDispatchState state = OpenIntents(Realm(Period * 80L, a),
				out List<KingdomPolityDueWork> _, KingdomPolityCohortPurpose.Guard);
			KingdomPolityDirectRecord intent = KingdomPolityDispatchRules.FindIntent(state, 0);
			StringAssert.Contains("storage-space=10", intent.EndpointVerb);
			intent.EndpointVerb = intent.EndpointVerb.Replace("storage-space=10", "storage-space=11");
			Site[] drifted = Copies(a);
			Apply(drifted, Drift.Storage, Period * 80L + Day);
			ClassicAssert.IsFalse(KingdomPolityDispatchRules.TryOpen(state,
				Realm(Period * 80L + Day, drifted), out List<KingdomPolityDueWork> _,
				out string failure));
			ClassicAssert.AreEqual("polity dispatch state is invalid", failure);
		}

		[Test]
		public void ForeignRealmOfferIsStillRefused()
		{
			Site a = CityA();
			KingdomPolityDispatchState state = OpenAndComplete(Realm(Period * 90L, a));
			KingdomPolityDispatchOffer foreign = Realm(Period * 90L + Day, a);
			foreign.RealmId = "taf:realm:v1:" + new string('9', 64);
			ClassicAssert.IsFalse(KingdomPolityDispatchRules.TryOpen(state, foreign,
				out List<KingdomPolityDueWork> _, out string failure));
			ClassicAssert.AreEqual("polity dispatch belongs to another realm", failure);
		}
	}
}
