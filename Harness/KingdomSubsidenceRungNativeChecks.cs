using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	internal static class KingdomSubsidenceRungNativeChecks
	{
		internal static string Run(XRLGame Game, Zone Zone, out bool Ok, bool DeathPrepared = false)
		{
			int passed = 0;
			string current = "synthetic-home-and-work-fixture";
			StringBuilder results = new StringBuilder();
			KingdomSubsidenceRungNativeFixture fixture = null;
			KingdomSubsidenceRungNativeFault fault = null;
			KingdomSurvey.PassScope scope = null;
			long now = Game.TimeTicks;
			try
			{
				Check(KingdomSubsidenceRungNativeFixture.TryCreate(Zone, out fixture, out string failure), failure);
				KingdomSystem system = fixture.Base.System;
				scope = fixture.Survey.BindPass();
				long anchor = now - 3L * KingdomSubsidenceStepRules.StepTicks;
				Check(KingdomSubsidenceNativeClockSeed.TrySeed(system, Zone, fixture.Survey, anchor, out failure), failure);
				KingdomSubsidenceNativeClockChecks.Verify(fixture.Base, Zone, fixture.Survey, now);
				Check(system.LastSubsidenceTick == anchor && system.Population == 50
					&& system.Stage == GrowthStage.City && Game.TimeTicks == now && fixture.Now == now,
					"three elapsed steps must be seeded without advancing the world clock or departures");
				int priorDepartures = system.Ledger.Departures;
				passed++; results.Append("\nsynthetic-home-and-work-fixture=PASS; construction-and-assignment=seeded; elapsed-checkpoint=seeded; clock-refusals=4");

				current = "fifteen-departures-before-rung-capture";
				Check(fixture.Work.GetPart<KingdomSubsidenceRungNativeFault>() == null, "foreign rung fault retained");
				fault = new KingdomSubsidenceRungNativeFault { System = system, DueTick = now };
				fixture.Work.AddPart(fault);
				Check(ReferenceEquals(fault.ParentObject, fixture.Work)
					&& ReferenceEquals(fixture.Work.GetPart<KingdomSubsidenceRungNativeFault>(), fault), "fault attachment changed");
				bool ran = KingdomSubsidence.TryReckon(system, Zone, fixture.Survey, now, out failure);
				Check(!ran && !string.IsNullOrEmpty(failure) && fault.Throws == 1 && fault.CommittedBeforeFault,
					"actual fifteen-departure path did not stop at the native declaration callback: " + failure);
				KingdomSubsidenceStepBook before = Read(system);
				Check(before.Active != null && before.Active.RungModel == KingdomSubsidenceStepRules.UnplannedRungs
					&& fixture.Wear.Wear == fixture.BeforeWear && !Roof(fixture).RoofStanding,
					"capture interruption must leave physical wear and roof at their before-state");
				VerifyDepartures(fixture, priorDepartures + 15, now - KingdomSubsidenceStepRules.StepTicks);
				passed++; results.Append("\nfifteen-departures-before-rung-capture=PASS; cut=native-display-name-callback");

				current = "exact-rung-and-roof-capture";
				Check(KingdomSubsidenceStepRuntime.TryPrepareRung(system, Zone, fixture.Survey, now, out failure), failure);
				KingdomSubsidenceStepBook prepared = Read(system);
				Check(KingdomSubsidenceStepRules.TryReadRungPlan(prepared, out KingdomSubsidenceRungPlan plan)
					&& plan.StepId == before.Active.Id && plan.From == GrowthStage.City && plan.To == GrowthStage.Town
					&& plan.DueTick == now && plan.Works.Count == 1
					&& plan.Works[0].ObjectId == fixture.Work.IDIfAssigned
					&& plan.Works[0].BeforeWear == fixture.BeforeWear && plan.Works[0].AfterWear == fixture.AfterWear
					&& plan.Works[0].Roofs.Count == 1 && plan.Works[0].Roofs[0].ResidentId == fixture.HomeResidentId
					&& plan.Works[0].Roofs[0].BodyObjectId == fixture.HomeResident.IDIfAssigned
					&& !plan.Works[0].Roofs[0].BeforeStanding,
					"real capture did not freeze the exact work, due date and surviving housed resident");
				string stepId = plan.StepId;
				passed++; results.Append("\nexact-rung-and-roof-capture=PASS");

				if (DeathPrepared)
				{
					current = "prepared-roof-native-deaths";
					KingdomResidentDeathNativeChecks.Run(fixture, plan, results, ref passed);
				}
				else
				{
					current = "actual-wear-after-synthetic-authority-cut";
					string priorWire = system.City.SubsidenceModel;
					string intentWire = null;
					Check(KingdomSubsidenceStepRules.TryArmRungWear(prepared, 0, out KingdomSubsidenceStepBook intent)
						&& KingdomSubsidenceStepCodec.TryEncode(intent, out intentWire)
						&& system.City.SubsidenceModel == priorWire, "synthetic parent intent lost its exact prior");
					// Explicit test-only persisted parent frontier; no production field is used as a fault hook.
					system.City.SubsidenceModel = intentWire;
					Check(KingdomSubsidenceStepRules.TryReadRungPlan(Read(system), out plan), "armed plan cannot be read");
					int afterCuts = 0;
					bool applied = KingdomSubsidenceWearRuntime.TryApply(plan, 0, fixture.Work, () =>
					{
						Check(ReferenceEquals(The.Game, Game) && ReferenceEquals(The.ZoneManager.ActiveZone, Zone)
							&& ReferenceEquals(Game.GetSystem<KingdomSystem>(), system)
							&& system.City.SubsidenceModel == intentWire && GameObject.Validate(fixture.Work)
							&& fixture.Work.IDIfAssigned == plan.Works[0].ObjectId
							&& fixture.Work.CurrentCell == Zone.GetCell(plan.Works[0].X, plan.Works[0].Y)
							&& ReferenceEquals(fixture.Work.GetPart<r_KingdomWear>(), fixture.Wear), "adapter fixture authority changed");
						if (fixture.Wear.Wear == fixture.AfterWear) { afterCuts++; return false; }
						return true;
					}, out r_KingdomWear observed, out failure);
					Check(!applied && failure == KingdomSubsidenceWearRuntime.AuthorityLost && afterCuts == 1
						&& ReferenceEquals(observed, fixture.Wear) && fixture.Wear.Wear == fixture.AfterWear
						&& fixture.Wear.IncidentPhase == (int)KingdomWearIncidentPhase.MutationIntent
						&& fixture.Wear.IncidentId == stepId && !Roof(fixture).RoofStanding
						&& system.City.SubsidenceModel == intentWire,
						"real wear write did not remain under its own mutation intent at the synthetic authority cut");
					passed++; results.Append("\nactual-wear-after-synthetic-authority-cut=PASS; parent-intent=seeded; wear-write=production");

					current = "production-rung-recovery-and-retirement";
					int officialBefore = system.ChronicleEntries.Count, outsiderBefore = system.OutsiderEntries.Count;
					string[] officialPrefix = system.ChronicleEntries.ToArray(), outsiderPrefix = system.OutsiderEntries.ToArray();
					Check(KingdomSubsidenceBatchCodec.TryDecode(Read(system).BatchModel, out KingdomSubsidenceBatch batch)
						&& batch.Wanted == 15 && batch.Departed == 10, "frozen three-step batch changed");
					string summaryLine = Dated(KingdomSubsidenceRules.SlideDepartureSummary(batch.Name, 15,
						KingdomSubsidenceRules.NamedDepartures(15), KingdomSubsidenceRules.DepartureCause(batch.Binding)), now);
					string kingdom = KingdomPresentation.Rich(system.KingdomDisplayName);
					string stageLine = Dated(kingdom + " ceased to be a city and became a town again", now);
					string ruinLine = Dated(KingdomSubsidenceRules.RuinedWorkLine(plan.Works[0].Name, kingdom), now);
					Check(KingdomSubsidenceStepRuntime.TryBeforePass(system, Zone, fixture.Survey, out failure), failure);
					VerifyDepartures(fixture, priorDepartures + 15, now);
					KingdomSubsidenceStepBook retired = Read(system);
					KingdomCityBook.SubsidenceRoofRow roof = Roof(fixture);
					Check(retired.Active == null && retired.Sequence == 3 && retired.LastRetiredTick == now
						&& retired.BatchModel == KingdomSubsidenceBatchRules.None
						&& retired.FailureModel == KingdomSubsidenceReportArchive.None
						&& fixture.Wear.Wear == fixture.AfterWear && KingdomLodgingRules.IsCondemned(fixture.Wear.Wear)
						&& fixture.Wear.IncidentPhase == (int)KingdomWearIncidentPhase.None
						&& fixture.Wear.IncidentId == null && fixture.Wear.LastCompletedIncidentId == stepId
						&& roof.RoofStanding && roof.Reached == now && roof.Warned == KingdomBrinkRules.Unwarned
						&& system.ChronicleEntries.Count == officialBefore + 3
						&& system.OutsiderEntries.Count == outsiderBefore + 3
						&& Count(system.ChronicleEntries, stageLine) == 1 && Count(system.ChronicleEntries, ruinLine) == 1
						&& Count(system.ChronicleEntries, summaryLine) == 1,
						"recovery must credit existing wear once, date the roof, release receipt, tell and retire all three steps");
					Prefix(system.ChronicleEntries, officialPrefix); Prefix(system.OutsiderEntries, outsiderPrefix);
					VerifyEffects(fixture, plan);
					passed++; results.Append("\nproduction-rung-recovery-and-retirement=PASS; departed=15; stage=Town; condemned-roofs=1");

					current = "same-tick-no-rung-replay";
					string wire = system.City.SubsidenceModel;
					string[] official = system.ChronicleEntries.ToArray(), outsider = system.OutsiderEntries.ToArray();
					string[] notes = system.Ledger.Notes.ToArray();
					Check(KingdomSubsidence.TryReckon(system, Zone, fixture.Survey, now, out failure), failure);
					VerifyDepartures(fixture, priorDepartures + 15, now);
					Check(system.City.SubsidenceModel == wire && fixture.Wear.Wear == fixture.AfterWear
						&& roof.SameCarriers(Roof(fixture)) && Roof(fixture).Reached == now
						&& Roof(fixture).Warned == KingdomBrinkRules.Unwarned && fault.Throws == 1 && Game.TimeTicks == now,
						"same-tick retry changed exact rung evidence or world clock");
					Same(system.ChronicleEntries, official); Same(system.OutsiderEntries, outsider); Same(system.Ledger.Notes, notes);
					VerifyEffects(fixture, plan);
					passed++; results.Append("\nsame-tick-no-rung-replay=PASS");
				}
			}
			catch (Exception error)
			{
				results.Append('\n').Append(current).Append("=FAIL ")
					.Append(KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message));
			}
			finally
			{
				if (fault != null)
				{
					bool clean = false;
					try
					{
						if (ReferenceEquals(fault.ParentObject, fixture.Work)
							&& ReferenceEquals(fixture.Work.GetPart<KingdomSubsidenceRungNativeFault>(), fault))
							fixture.Work.RemovePart(fault);
						clean = fault.ParentObject == null && fixture.Work.GetPart<KingdomSubsidenceRungNativeFault>() == null;
					}
					catch (Exception) { }
					if (!clean) { passed = Math.Min(passed, 5); results.Append("\nhandler-cleanup=FAIL unknown state retained"); }
				}
				if (fixture != null && !fixture.TryReleaseHeld(out string cleanup))
				{ passed = Math.Min(passed, 5); results.Append("\nparty-cleanup=FAIL ").Append(cleanup); }
				scope?.Dispose();
			}
			Ok = passed == KingdomSubsidenceRungNativeProvider.ExpectedCases;
			return "native-subsidence-rung cases=6 passed=" + passed + " failed=" + (Ok ? 0 : 1)
				+ "; mode=" + (DeathPrepared ? "death-prepared" : "wear-recovery")
				+ "; synthetic=true; elapsed=seeded-checkpoint; ordinary-acceptance=false; save-load=untested"
				+ "; world-clock=" + (Game.TimeTicks == now ? "unchanged" : "changed")
				+ "; profile-and-effects-retained=true" + results;
		}

		private static KingdomSubsidenceStepBook Read(KingdomSystem System)
		{
			KingdomSubsidenceStepBook book = null;
			Check(System.City.HasValidSubsidenceStorage() && KingdomSubsidenceStepCodec.TryDecode(
				System.City.SubsidenceModel, out book), "exact step book unavailable");
			return book;
		}

		private static KingdomCityBook.SubsidenceRoofRow Roof(KingdomSubsidenceRungNativeFixture Fixture)
		{
			Check(Fixture.Base.System.City.TryCaptureSubsidenceRoof(Fixture.HomeResidentId,
				out KingdomCityBook.SubsidenceRoofRow roof) && roof.HomeWorkId == KingdomCityRules.StableId(Fixture.Work.IDIfAssigned)
				&& roof.Standing == (int)KingdomResidentStanding.Resident, "exact resident roof row unavailable");
			return roof;
		}

		private static void VerifyDepartures(KingdomSubsidenceRungNativeFixture Fixture, int Departures, long Checkpoint)
		{
			KingdomSystem system = Fixture.Base.System;
			Check(system.Population == 35 && KingdomResidents.OnRollCount(system) == 35
				&& system.Stage == GrowthStage.Town && system.LastSubsidenceTick == Checkpoint
				&& system.Ledger.Departures == Departures && KingdomResidentDepartureRules.IsEmpty(system.ResidentDeparture),
				"native departure counters or checkpoint disagree");
			int absent = 0;
			for (int i = 0; i < Fixture.Base.Bodies.Count; i++)
			{
				GameObject body = Fixture.Base.Bodies[i];
				if (!GameObject.Validate(body))
				{
					Check(KingdomResidents.DepartureCarriersAbsent(system, system.City, Fixture.Base.ResidentIds[i]),
						"destroyed resident retains row or binding");
					absent++;
				}
				else Check(body.CurrentZone == Fixture.Base.Zone && body.IsAlive
					&& KingdomResidents.IdOf(body) == Fixture.Base.ResidentIds[i], "survivor identity or ground changed");
			}
			Check(absent == 15 && GameObject.Validate(Fixture.HomeResident) && Fixture.HomeResident.IsPlayerLed(),
				"fifteen physical departures and the exact held survivor were not proved");
		}

		private static void VerifyEffects(KingdomSubsidenceRungNativeFixture Fixture, KingdomSubsidenceRungPlan Plan)
		{
			GameObject work = Fixture.Work, body = Fixture.HomeResident;
			KingdomSubsidenceRungWork row = Plan.Works[0];
			KingdomSubsidenceRungRoof recipient = row.Roofs[0];
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal) { row.ObjectId, recipient.BodyObjectId };
			Check(KingdomPlots.TryCaptureGlobalLiveIds(ids, out Dictionary<string, GameObject> live)
				&& live.TryGetValue(row.ObjectId, out GameObject exactWork) && ReferenceEquals(exactWork, work)
				&& live.TryGetValue(recipient.BodyObjectId, out GameObject exactBody) && ReferenceEquals(exactBody, body)
				&& GameObject.Validate(work) && work.IDIfAssigned == row.ObjectId && work.Blueprint == row.Blueprint
				&& work.CurrentCell == Fixture.Base.Zone.GetCell(row.X, row.Y) && work.Count == 1
				&& work.InInventory == null && work.Equipped == null
				&& ReferenceEquals(work.GetPart<r_KingdomWear>(), Fixture.Wear)
				&& ReferenceEquals(Fixture.Wear.ParentObject, work), "final work/body identity or wear attachment changed");
			int copies = 0;
			foreach (IPart part in work.PartsList) if (part is r_KingdomWear) copies++;
			KingdomCityBook.SubsidenceRoofRow roof = Roof(Fixture);
			Check(copies == 1 && Fixture.Wear.Wear == row.AfterWear && !Fixture.Wear.LifecycleQuarantined
				&& Fixture.Wear.IncidentPhase == (int)KingdomWearIncidentPhase.None
				&& Fixture.Wear.IncidentId == null && Fixture.Wear.LastCompletedIncidentId == Plan.StepId
				&& roof.RoofStanding && roof.Reached == Plan.DueTick && roof.Warned == KingdomBrinkRules.Unwarned
				&& GameObject.Validate(body) && body.IDIfAssigned == recipient.BodyObjectId
				&& body.CurrentZone == Fixture.Base.Zone && KingdomCitizenship.BelongsTo(Fixture.Base.System, body)
				&& body.GetStringProperty(KingdomLodging.HomePlotIdProperty) == row.PlotId
				&& Fixture.Base.System.Bindings.TryReadExact(out KingdomBindingTable bindings, out _)
				&& bindings.TryGet(recipient.ResidentId, KingdomBindingKind.Resident, out KingdomBinding binding)
				&& binding.ObjectId == recipient.BodyObjectId && binding.ZoneId == Plan.ZoneId,
				"final released receipt, full roof tuple or bound resident changed");
		}

		private static void Prefix(List<string> Current, string[] Before)
		{
			Check(Current.Count >= Before.Length, "register prefix truncated");
			for (int i = 0; i < Before.Length; i++) Check(Current[i] == Before[i], "register prefix overwritten");
		}

		private static string Dated(string Text, long Tick)
		{
			return "On the " + Calendar.GetDay(Tick) + " of " + Calendar.GetMonth(Tick) + ", "
				+ Calendar.GetYear(Tick) + " AR, " + Text + ".";
		}

		private static int Count(List<string> Lines, string Text)
		{
			int count = 0;
			foreach (string line in Lines) if (line == Text) count++;
			return count;
		}

		private static void Same(List<string> Current, string[] Before)
		{
			Check(Current.Count == Before.Length, "telling count changed on retry");
			for (int i = 0; i < Before.Length; i++) Check(Current[i] == Before[i], "telling text changed on retry");
		}

		private static void Check(bool Condition, string Failure)
		{
			if (!Condition) throw new InvalidOperationException(Failure ?? "native rung check refused");
		}
	}
}
