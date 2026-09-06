using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using XRL;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	internal static class KingdomResidentDeathNativeChecks
	{
		private sealed class Frame
		{
			internal KingdomSubsidenceRungNativeFixture Fixture;
			internal KingdomSystem System;
			internal KingdomCityBook City;
			internal KingdomBindingRegistry Bindings;
			internal KingdomBindingTable BeforeBindings;
			internal KingdomLedger Ledger;
			internal GameObject Player;
			internal KingdomSubsidenceRungPlan Plan;
			internal string Wire, Realm, Settlement, Key;
			internal long Now, Checkpoint;
			internal int Departures, Dead;
			internal object[] Tables;
			internal IList[] RoofCarriers;
			internal object[][] RoofValues;
			internal KingdomResidentRow[] Rows;
			internal readonly List<int> Killed = new List<int>();
		}

		// Synthetic fixture ownership is prerequisite; only real engine death creates death authority.
		internal static void Run(KingdomSubsidenceRungNativeFixture fixture, KingdomSubsidenceRungPlan plan,
			StringBuilder results, ref int passed)
		{
			var s = fixture.Base.System;
			var f = new Frame { Fixture = fixture, System = s, City = s.City, Bindings = s.Bindings,
				Ledger = s.Ledger, Player = The.Player, Plan = plan, Wire = s.City.SubsidenceModel, Realm = s.CurrentRealmId,
				Settlement = s.CurrentSettlementId, Now = fixture.Now, Checkpoint = s.LastSubsidenceTick,
				Departures = s.Ledger.Departures, Dead = s.Dead, Tables = Tables(fixture.Base.Game) };
			f.Key = "r_TAF_ResidentDeaths_v1:" + f.Realm + ":" + f.Settlement;
			f.RoofCarriers = RoofCarriers(f.City);
			f.RoofValues = new object[f.RoofCarriers.Length][];
			for (int i = 0; i < f.RoofCarriers.Length; i++)
			{
				f.RoofValues[i] = new object[f.RoofCarriers[i].Count];
				f.RoofCarriers[i].CopyTo(f.RoofValues[i], 0);
			}
			Check(f.City.TryReadExact(out var state, out _) && state.ResidentCount == 35, "exact 35-row fixture missing");
			f.Rows = new KingdomResidentRow[state.ResidentCount];
			for (int i = 0; i < f.Rows.Length; i++) Check(state.TryResident(i, out f.Rows[i]), "row capture failed");
			Check(f.Bindings.TryReadExact(out f.BeforeBindings, out _) && f.BeforeBindings.Count == 35, "exact 35-binding fixture missing");
			Exact(f);
			Check(Journal(f) == null && KingdomResidentDeathRuntime.CanProceed(s, out _),
				"fresh fixture already holds death authority");
			GameObject unrelated = null;
			foreach (GameObject body in fixture.Base.Bodies)
				if (GameObject.Validate(body) && !ReferenceEquals(body, fixture.HomeResident)) { unrelated = body; break; }
			Check(unrelated != null, "no exact unrelated survivor");
			Kill(f, unrelated, false);
			passed++; results.Append("\nunrelated-engine-death=PASS; population=34; roof=Prepared; accounting=settled");
			Exact(f);
			Check(fixture.TryReleaseHeld(out string failure), failure);
			Exact(f);
			Kill(f, fixture.HomeResident, true);
			passed++; results.Append("\nselected-engine-death=PASS; population=33; roof=Prepared; missing-body-is-not-roof-proof=true");

			string journal = f.Fixture.Base.Game.GetStringGameState(f.Key);
			string[] accounts = Accounts(s), official = s.ChronicleEntries.ToArray(), outsider = s.OutsiderEntries.ToArray();
			string[] notes = s.Ledger.Notes.ToArray();
			for (int i = 0; i < 2; i++)
			{
				Exact(f);
				Check(KingdomResidentDeathRuntime.TryRecoverPending(s, out failure), failure);
				Verify(f);
				Check(f.Fixture.Base.Game.GetStringGameState(f.Key) == journal, "settled journal changed on recovery");
				Same(Accounts(s), accounts); Same(s.ChronicleEntries.ToArray(), official);
				Same(s.OutsiderEntries.ToArray(), outsider); Same(s.Ledger.Notes.ToArray(), notes);
			}
			passed++; results.Append("\nprepared-death-recovery-no-replay=PASS; recoveries=2; death-journal=2; roof-and-parent=unchanged; telling-delivery=not-claimed");
		}

		private static void Kill(Frame f, GameObject body, bool selected)
		{
			Exact(f);
			int id = KingdomResidents.IdOf(body); string objectId = body.IDIfAssigned;
			bool owned = false;
			foreach (GameObject candidate in f.Fixture.Base.Bodies) owned |= ReferenceEquals(candidate, body);
			Check(GameObject.Validate(body) && body.IsAlive && !body.IsPlayer() && !body.IsPlayerLed()
				&& body.CurrentZone == f.Fixture.Base.Zone && body.InInventory == null && body.Equipped == null
				&& body.GetIntProperty("NoLoot") == 1 && KingdomCitizenship.BelongsTo(f.System, body)
				&& owned, "death target is not an exact fresh owned survivor");
			KingdomBinding bound = default(KingdomBinding);
			Check(f.Bindings.TryReadExact(out var bindings, out _) && bindings.TryGet(id, KingdomBindingKind.Resident, out bound)
				&& bound.ObjectId == objectId && bound.ZoneId == f.Plan.ZoneId
				&& ReferenceEquals(f.Fixture.Survey.FindBoundBody(objectId, KingdomBindingKind.Resident), body),
				"death target binding changed");
			KingdomResidentRow before = default(KingdomResidentRow);
			Check(f.City.TryReadExact(out var state, out _) && state.TryResidentIndex(id, out int index)
				&& state.TryResident(index, out before) && before.Standing == KingdomResidentStanding.Resident,
				"death target row missing");
			string[] accounts = Accounts(f.System);
			Cell cell = body.CurrentCell;
			Exact(f);
			body.Die(Force: true);
			Check(!GameObject.Validate(body), "engine Die returned without invalidating the exact body");
			foreach (GameObject item in cell.GetObjects()) Check(!ReferenceEquals(item, body), "dead body remains live in its cell");
			Check(KingdomPlots.TryCaptureGlobalLiveIds(new HashSet<string>(StringComparer.Ordinal) { objectId },
				out var live) && !live.ContainsKey(objectId), "dead identity still has global live custody");
			var journal = Journal(f);
			Check(journal != null && journal.Entries.Count == f.Killed.Count + 1, "death callback did not append exactly one witness");
			var receipt = journal.Entries[journal.Entries.Count - 1];
			Check(receipt.Before.ResidentId == id && receipt.Body == objectId && receipt.Tick == f.Now
				&& receipt.MintedTick == bound.MintedTick && receipt.Zone == bound.ZoneId
				&& KingdomResidentDeathCodec.Row(receipt.Before) == KingdomResidentDeathCodec.Row(before)
				&& receipt.StepWire == f.Wire && receipt.Memory && receipt.RoleFault == ""
				&& receipt.Phase == KingdomResidentDeathPhase.Settled
				&& receipt.Cause == KingdomStandingCause.Unwitnessed
				&& receipt.RoofBlocked == selected && receipt.RoofIndex == (selected ? 0 : -1)
				&& receipt.RoofWork == (selected ? f.Plan.Works[0].ObjectId : ""), "wrong or incomplete native death witness");
			var oracle = receipt.Copy();
			oracle.Phase = KingdomResidentDeathPhase.RolesSettled; oracle.Telling = KingdomResidentDeathTelling.Pending;
			oracle.AccountProof = "";
			Check(KingdomResidentDeathRules.TryPrepareAccounts(oracle, accounts, out var expected)
				&& expected.AccountProof == receipt.AccountProof, "native accounting receipt does not match its exact prior");
			Same(Accounts(f.System), expected.AfterAccounts);
			f.Killed.Add(id);
			Verify(f);
		}

		private static void Verify(Frame f)
		{
			Exact(f);
			var journal = Journal(f);
			KingdomBindingTable bindings = null; KingdomCityState state = null;
			Check(journal != null && journal.Entries.Count == f.Killed.Count
				&& f.System.Population == 35 - f.Killed.Count && KingdomResidents.OnRollCount(f.System) == 35 - f.Killed.Count
				&& f.System.Dead == f.Dead + f.Killed.Count
				&& f.Bindings.TryReadExact(out bindings, out _) && bindings.Count == 35 - f.Killed.Count
				&& f.City.TryReadExact(out state, out _) && state.ResidentCount == 35,
				"native death population, history, binding or retained-row count disagrees");
			for (int i = 0; i < f.Rows.Length; i++)
			{
				KingdomResidentRow before = f.Rows[i];
				KingdomResidentRow row = default(KingdomResidentRow);
				Check(state.TryResidentIndex(before.ResidentId, out int at) && state.TryResident(at, out row), "resident row disappeared");
				if (f.Killed.Contains(before.ResidentId))
				{
					Check(!bindings.TryGet(before.ResidentId, KingdomBindingKind.Resident, out _)
						&& KingdomResidentDeathCodec.Row(row) == KingdomResidentDeathCodec.Row(
							before.WithStanding(KingdomResidentStanding.Dead, KingdomStandingCause.Unwitnessed)), "death changed more than standing/cause");
				}
				else Check(KingdomResidentDeathCodec.Row(row) == KingdomResidentDeathCodec.Row(before), "unrelated resident row changed");
			}
			int living = 0;
			for (int i = 0; i < f.Fixture.Base.Bodies.Count; i++)
			{
				GameObject body = f.Fixture.Base.Bodies[i];
				if (!GameObject.Validate(body)) continue;
				living++;
				Check(body.IsAlive && body.CurrentZone == f.Fixture.Base.Zone
					&& bindings.TryGet(f.Fixture.Base.ResidentIds[i], KingdomBindingKind.Resident, out var binding)
					&& f.BeforeBindings.TryGet(f.Fixture.Base.ResidentIds[i], KingdomBindingKind.Resident, out var priorBinding)
					&& binding.ObjectId == body.IDIfAssigned && binding.ZoneId == f.Plan.ZoneId
					&& binding.MintedTick == priorBinding.MintedTick && binding.ObjectId == priorBinding.ObjectId,
					"survivor no longer has its exact resident binding");
			}
			Check(living == 35 - f.Killed.Count && KingdomResidentDeathRuntime.CanProceed(f.System, out _),
				"live body census or settled journal barrier disagrees");
		}

		private static void Exact(Frame f)
		{
			var game = f.Fixture.Base.Game; var s = f.System; var work = f.Fixture.Work;
			Check(ReferenceEquals(KingdomSubsidenceRungNativeFixture.LastAttempt, f.Fixture)
				&& ReferenceEquals(KingdomSubsidenceNativeFixture.LastAttempt, f.Fixture.Base)
				&& ReferenceEquals(The.Game, game) && ReferenceEquals(game.GetSystem<KingdomSystem>(), s)
				&& ReferenceEquals(The.Player, f.Player) && s.Founded && !s.LoadFailed && s.OwnedZone(f.Plan.ZoneId)
				&& ReferenceEquals(The.Player?.CurrentZone, f.Fixture.Base.Zone)
				&& ReferenceEquals(The.ZoneManager?.ActiveZone, f.Fixture.Base.Zone)
				&& ReferenceEquals(KingdomSurvey.ActiveFor(f.Fixture.Base.Zone), f.Fixture.Survey)
				&& ReferenceEquals(s.City, f.City) && ReferenceEquals(s.Bindings, f.Bindings)
				&& ReferenceEquals(s.Ledger, f.Ledger) && s.CurrentRealmId == f.Realm && s.CurrentSettlementId == f.Settlement
				&& game.TimeTicks == f.Now && s.LastSubsidenceTick == f.Checkpoint && s.Stage == GrowthStage.Town
				&& s.Ledger.Departures == f.Departures && KingdomResidentDepartureRules.IsEmpty(s.ResidentDeparture)
				&& f.City.HasValidSubsidenceStorage() && f.City.SubsidenceModel == f.Wire && KingdomOffices.Enabled,
				"native death owner, clock, departure or parent changed");
			Check(KingdomScenarioRealizer.TryBindStampedPlan(out var scenario, out _, out _)
				&& scenario.Key == "founding-first-city" && scenario.AuthorityClass == KingdomScenarioFoundingStep.FoundingAuthority
				&& KingdomScenarioTransactionMarker.Observe(out _) == KingdomScenarioTransactionShape.Committed
				&& KingdomScenarioScript.TryRead(out var script, out _) && script.Count == 3
				&& script[0] == "stagedigest" && script[1] == KingdomSubsidenceRungNativeProvider.DeathVerb
				&& script[2] == "stagedigest" && KingdomScenarioDurableState.ProvesExactText(KingdomSubsidenceRungNativeProvider.Receipt, "intent"),
				"exact developer death admission absent");
			Check(f.Plan.Works.Count == 1 && f.Plan.Works[0].WearPhase == KingdomSubsidenceEffectPhase.Prepared
				&& f.Plan.Works[0].Roofs.Count == 1 && f.Plan.Works[0].Roofs[0].Phase == KingdomSubsidenceEffectPhase.Prepared
				&& GameObject.Validate(work) && work.IDIfAssigned == f.Plan.Works[0].ObjectId && work.Blueprint == f.Plan.Works[0].Blueprint
				&& work.CurrentCell == f.Fixture.Base.Zone.GetCell(f.Plan.Works[0].X, f.Plan.Works[0].Y)
				&& ReferenceEquals(work.GetPart<r_KingdomWear>(), f.Fixture.Wear) && ReferenceEquals(f.Fixture.Wear.ParentObject, work)
				&& f.Fixture.Wear.Wear == f.Fixture.BeforeWear && f.Fixture.Wear.IncidentPhase == (int)KingdomWearIncidentPhase.None
				&& !f.Fixture.Wear.LifecycleQuarantined, "Prepared work or wear changed");
			var tables = Tables(game); var carriers = RoofCarriers(f.City);
			for (int i = 0; i < tables.Length; i++) Check(tables[i] != null && ReferenceEquals(tables[i], f.Tables[i]), "game-state table changed");
			for (int i = 0; i < carriers.Length; i++)
			{
				Check(ReferenceEquals(carriers[i], f.RoofCarriers[i]) && carriers[i].Count == f.RoofValues[i].Length, "roof carrier replaced");
				for (int j = 0; j < carriers[i].Count; j++) Check(Equals(carriers[i][j], f.RoofValues[i][j]), "roof tuple changed");
			}
		}

		private static KingdomResidentDeathJournal Journal(Frame f)
		{
			var g = f.Fixture.Base.Game;
			Check(!g.HasIntGameState(f.Key) && !g.HasInt64GameState(f.Key)
				&& !g.HasObjectGameState(f.Key) && !g.HasBooleanGameState(f.Key), "death key has foreign typed authority");
			if (!g.HasStringGameState(f.Key)) return null;
			string wire = g.GetStringGameState(f.Key);
			Check(KingdomResidentDeathCodec.TryDecode(wire, out var journal)
				&& KingdomResidentDeathCodec.TryEncode(journal, out string encoded) && encoded == wire
				&& journal.Realm == f.Realm && journal.Settlement == f.Settlement, "death journal is not exact canonical owned authority");
			return journal;
		}

		private static string[] Accounts(KingdomSystem s)
		{
			Check(s.DeadNames != null && s.DeadOrigins?.Count == s.DeadNames.Count
				&& s.DeadArrived?.Count == s.DeadNames.Count && s.DeadCauses?.Count == s.DeadNames.Count, "death history is torn");
			var history = new List<string> { s.Dead.ToString(CultureInfo.InvariantCulture) };
			for (int i = 0; i < s.DeadNames.Count; i++)
			{ history.Add(s.DeadNames[i]); history.Add(s.DeadOrigins[i]); history.Add(s.DeadArrived[i]); history.Add(s.DeadCauses[i]); }
			return new[] { KingdomResidentDeathCodec.Map(s.CultureCounts), KingdomResidentDeathCodec.Map(s.SpeciesCounts),
				KingdomResidentDeathCodec.Map(s.IdentityCounts), KingdomResidentDeathCodec.Map(s.CreedCounts),
				KingdomResidentDeathCodec.Map(s.CreedPastCounts), KingdomResidentDeathCodec.Fields(history.ToArray()) };
		}
		private static object[] Tables(XRLGame g) { return new object[] { g.StringGameState, g.IntGameState,
			g.Int64GameState, g.ObjectGameState, g.BooleanGameState }; }
		private static IList[] RoofCarriers(KingdomCityBook c) { return new IList[] { c.ResidentIds, c.ResidentHomeWorkIds,
			c.ResidentBoundZoneIds, c.ResidentRoofStanding, c.ResidentRoofTicks, c.ResidentRoofWarnedTicks }; }
		private static void Same(string[] actual, string[] expected)
		{
			Check(actual.Length == expected.Length, "accounting or telling count changed");
			for (int i = 0; i < actual.Length; i++) Check(actual[i] == expected[i], "accounting or telling value changed");
		}
		private static void Check(bool condition, string failure)
		{ if (!condition) throw new InvalidOperationException(failure ?? "native death check refused"); }
	}
}
