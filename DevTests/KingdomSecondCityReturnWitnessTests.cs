#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Behavioural coverage row 12: why the second-city return is the realm's FIRST polity
	/// reconciliation, and what that reconciliation must leave behind. Founding stamps the
	/// semantic clock, so the attended settlement pass - and the daily polity reconciliation at
	/// its end - waits for the next 1200-tick boundary, which a one-turn run never reaches; the
	/// return's zone activation then opens the period's polity dispatch window with the two-city
	/// facts. Real execution of production's clock and dispatch rules, plus pins on the harness
	/// reads that hold a native run to the same facts. Not native acceptance.
	/// </summary>
	public class KingdomSecondCityReturnWitnessTests
	{
		private const string Realm = "taf:realm:v1:current";
		private const string CityOne =
			"taf:settlement:v1:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
		private const string CityTwo =
			"taf:settlement:v1:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
		private const string Checks = "Harness/KingdomSecondCityNativeChecks.cs";
		private const string Cases = "Harness/KingdomSecondCityNativeCases.cs";

		/// <summary>A new game starts 325 ticks into a day (the engine's XRLGame.CreateNewGame);
		/// the persona founds a few turns later.</summary>
		private const long Founded = 40L * KingdomRules.TicksPerDay + 328L;

		[Test]
		public void NoSettlementPassIsDueInsideTheOneTurnRunAfterFounding()
		{
			KingdomSemanticClockState stamped = KingdomSemanticClockRules.FromLastDispatchTick(Founded);
			Assert.That(KingdomSemanticClockRules.Decide(stamped, Founded + 1L,
				ForceActivation: false).ShouldDispatch, Is.False, "end of the settle turn");
			Assert.That(KingdomSemanticClockRules.Decide(stamped, Founded + 1L,
				ForceActivation: true).ShouldDispatch, Is.False, "the return's activation");
			// The clock is read, not assumed: the next day boundary would dispatch.
			long boundary = Founded - Founded % KingdomRules.TicksPerDay + KingdomRules.TicksPerDay;
			Assert.That(KingdomSemanticClockRules.Decide(stamped, boundary,
				ForceActivation: false).ShouldDispatch, Is.True);
			Assert.That(TestMain.ReadRepositoryText("Core/KingdomFounding.01.FirstPublication.cs"),
				Does.Contain("system.LastSemanticTick = The.Game.TimeTicks;"));
			Assert.That(TestMain.ReadRepositoryText("Core/KingdomSystem.z21b.Polity.cs"), Does.Contain(
				"if (!Simulation.City.KingdomSemanticDispatcher.IsStationaryDispatch) return;"));
		}

		[Test]
		public void TheFirstReconciliationOpensThePeriodWindowOverBothCities()
		{
			KingdomPolityDispatchState state = new KingdomPolityDispatchState();
			Assert.That(KingdomPolityDispatchRules.TryOpen(state, Offer(Founded + 1L, CityOne, CityTwo),
				out List<KingdomPolityDueWork> _, out string failure), Is.True, failure);
			// The predicate KingdomSecondCityNativeCases.ReturnSeat holds the native run to.
			ulong window = (ulong)((Founded + 1L) / KingdomPolityDispatchRules.PeriodTicks);
			Assert.That(state.HasWindow && state.RealmId == Realm && state.LastWindowOrdinal == window
				&& state.EndpointCount == 2, Is.True);
		}

		[Test]
		public void AWindowFrozenWithOneCityRefusesTheTwoCityFactsInTheSamePeriod()
		{
			// Why setup requires no open window: the return would then meet this refusal.
			KingdomPolityDispatchState state = new KingdomPolityDispatchState();
			Assert.That(KingdomPolityDispatchRules.TryOpen(state, Offer(Founded, CityOne),
				out List<KingdomPolityDueWork> _, out string failure), Is.True, failure);
			Assert.That(KingdomPolityDispatchRules.TryOpen(state, Offer(Founded + 1L, CityOne, CityTwo),
				out List<KingdomPolityDueWork> _, out failure), Is.False);
			Assert.That(failure, Is.EqualTo("open polity topology differs from its frozen facts"));
		}

		[Test]
		public void TheFrameAndTheReturnCaseReadThatWindowAfterProductionsOwnSeatExchange()
		{
			string checks = TestMain.ReadRepositoryText(Checks);
			int none = checks.IndexOf("Require(system.PolityDispatch == null || !system.PolityDispatch.HasWindow,");
			Assert.That(none, Is.GreaterThan(0));
			Assert.That(none, Is.LessThan(checks.IndexOf("KingdomSecondCityNativeSite.TryResolve(")));
			string cases = TestMain.ReadRepositoryText(Cases);
			Assert.That(Squash(cases), Does.Contain(Squash("ulong window = (ulong)("
				+ "KingdomSecondCityNativeChecks.Ticks / KingdomPolityDispatchRules.PeriodTicks);")));
			Assert.That(Squash(cases), Does.Contain(Squash("Require(polity != null && polity.HasWindow"
				+ " && polity.RealmId == System.RealmId && polity.LastWindowOrdinal == window"
				+ " && polity.EndpointCount == 2,")));
			// Read in the return case after the seat came back, before the time re-proof.
			int ret = cases.IndexOf("private static void ReturnSeat(");
			int seat = cases.IndexOf("returning to the first city did not bring its seat back", ret);
			int polity = cases.IndexOf("polity.EndpointCount == 2", ret);
			Assert.That(ret > 0 && seat > ret && polity > seat, Is.True);
			Assert.That(cases.IndexOf("Still(Game);", polity), Is.GreaterThan(polity));
			// Production: the activation handler reconciles polity after its own seat exchange.
			string events = TestMain.ReadRepositoryText("Core/KingdomSystem.z20.Events.cs");
			int handler = events.IndexOf("public override bool HandleEvent(ZoneActivatedEvent E)");
			int trySeat = events.IndexOf("if (TrySeat(E.Zone))", handler);
			int reconcile = events.IndexOf(
				"if (!KingdomPolityActiveRuntime.TryReconcile(this, The.Game.TimeTicks,", handler);
			Assert.That(handler > 0 && trySeat > handler && reconcile > trySeat, Is.True);
			// The offer counts the seat and every non-seat city; the window keeps that count.
			Assert.That(TestMain.ReadRepositoryText("Polity/KingdomPolityEndpointFactRuntime.cs"),
				Does.Contain("List<KingdomSettlement> rows = System.NonSeatSettlements();"));
			Assert.That(TestMain.ReadRepositoryText("Polity/KingdomPolityDispatchRules.cs"),
				Does.Contain("candidate.EndpointCount = Offer.Endpoints.Count;"));
		}

		private static KingdomPolityDispatchOffer Offer(long Tick, params string[] Settlements)
		{
			List<KingdomPolityEndpointFacts> endpoints = new List<KingdomPolityEndpointFacts>();
			for (int i = 0; i < Settlements.Length; i++)
				endpoints.Add(new KingdomPolityEndpointFacts { SettlementId = Settlements[i], IsSeat = i == 0 });
			return new KingdomPolityDispatchOffer { RealmId = Realm, Tick = Tick, Endpoints = endpoints };
		}

		private static string Squash(string Source)
		{
			System.Text.StringBuilder kept = new System.Text.StringBuilder(Source.Length);
			foreach (char c in Source)
				if (c != ' ' && c != '\t' && c != '\r' && c != '\n') kept.Append(c);
			return kept.ToString();
		}
	}
}
#endif
