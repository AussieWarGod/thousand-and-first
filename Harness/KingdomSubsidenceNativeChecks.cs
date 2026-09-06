using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	internal static class KingdomSubsidenceNativeChecks
	{
		internal static string Run(XRLGame Game, Zone Zone, out bool Ok)
		{
			int passed = 0;
			string current = "physical-city-fixture";
			StringBuilder results = new StringBuilder();
			KingdomSubsidenceNativeFixture fixture = null;
			KingdomSubsidenceNativeSummaryFault fault = null;
			KingdomSurvey.PassScope scope = null;
			List<GameObject> held = new List<GameObject>();
			GameObject player = The.Player;
			long now = Game.TimeTicks;
			try
			{
				Check(KingdomSubsidenceNativeFixture.TryCreate(Zone, out fixture, out string failure), failure);
				KingdomSystem system = fixture.System;
				scope = fixture.Survey.BindPass();
				long anchor = now - KingdomSubsidenceRules.StepDays * KingdomRules.TicksPerDay;
				// Historical setup is explicitly synthetic; every production call uses the real world clock.
				Check(KingdomSubsidenceNativeClockSeed.TrySeed(system, Zone, fixture.Survey, anchor, out failure), failure);
				KingdomSubsidenceNativeClockChecks.Verify(fixture, Zone, fixture.Survey, now);
				Check(system.LastSubsidenceTick == anchor && system.Population == 50
					&& system.Stage == GrowthStage.City && system.SupportedLevel == KingdomCatalogueRules.FloorLevel
					&& !string.IsNullOrEmpty(system.SubsidenceBinding)
					&& !system.SubsidenceAnnounced && Game.TimeTicks == now,
					"fixture must expose one full unsupported City step without physical departures");
				int priorDepartures = system.Ledger.Departures;
				string[] officialPrefix = system.ChronicleEntries.ToArray();
				string[] outsiderPrefix = system.OutsiderEntries.ToArray();
				Check(officialPrefix.Length < KingdomChronicle.MaxEntries - 5,
					"native summary fixture must not trim its register prefix");
				string summary = KingdomSubsidenceRules.SlideDepartureSummary(
					KingdomPresentation.Rich(system.KingdomDisplayName), 5, 3,
					KingdomSubsidenceRules.DepartureCause(system.SubsidenceBinding));
				string official = "On the " + XRL.World.Calendar.GetDay() + " of "
					+ XRL.World.Calendar.GetMonth() + ", " + XRL.World.Calendar.GetYear()
					+ " AR, " + summary + ".";
				Check(!string.IsNullOrEmpty(summary), "five departures must require an aggregate summary");
				passed++; results.Append("\nphysical-city-fixture=PASS; elapsed-checkpoint=seeded; clock-refusals=4");

				current = "partial-one-of-five";
				for (int i = 1; i < fixture.Bodies.Count; i++)
				{
					GameObject body = fixture.Bodies[i];
					Check(GameObject.Validate(body) && body.Brain != null && body.Brain.PartyLeader == null,
						"owned fixture body has unexpected party custody");
					held.Add(body); body.Brain.PartyLeader = player;
					Check(body.IsPlayerLed(), "owned fixture resident was not made temporarily ineligible");
				}
				KingdomSubsidence.Reckon(system, Zone, fixture.Survey, now);
				Check(KingdomSubsidenceStepCodec.TryDecode(system.City.SubsidenceModel, out KingdomSubsidenceStepBook partial)
					&& partial.Active != null && partial.Active.Completed == 1 && partial.Active.Quota == 5
					&& partial.Active.AnchorTick == anchor && partial.Active.DueTick == now
					&& partial.Active.PendingDepartureId == "" && system.Population == 49
					&& KingdomResidents.OnRollCount(system) == 49 && system.LastSubsidenceTick == anchor
					&& system.Ledger.Departures == priorDepartures + 1,
					"partial departure must retain one credit and the original unpaid five-person step");
				string stepId = partial.Active.Id;
				long sequence = partial.Sequence;
				ReleaseHeld(held, player, Zone);
				passed++; results.Append("\npartial-one-of-five=PASS");

				current = "remaining-four-summary-interruption";
				Check(player.GetPart<KingdomSubsidenceNativeSummaryFault>() == null,
					"the player already holds a summary-fault handler");
				fault = new KingdomSubsidenceNativeSummaryFault
				{
					System = system, ExpectedOfficial = official, ExpectedCount = officialPrefix.Length + 5,
					ExpectedCheckpoint = now, ExpectedDepartures = priorDepartures + 5
				};
				player.AddPart(fault);
				Check(ReferenceEquals(player.GetPart<KingdomSubsidenceNativeSummaryFault>(), fault)
					&& ReferenceEquals(fault.ParentObject, player), "exact summary-fault attachment was not proved");
				KingdomSubsidence.Reckon(system, Zone, fixture.Survey, now);
				Check(fault.Throws == 1 && fault.CommittedBeforeFault,
					"exact native summary callback did not observe committed five-departure state before throwing");
				Check(system.ChronicleEntries.Count == officialPrefix.Length + 4
					&& system.OutsiderEntries.Count == outsiderPrefix.Length + 4
					&& KingdomSubsidenceStepCodec.TryDecode(system.City.SubsidenceModel, out KingdomSubsidenceStepBook settled)
					&& settled.Active == null && settled.Sequence == sequence && settled.LastRetiredTick == now
					&& KingdomSubsidenceBatchCodec.TryDecode(settled.BatchModel, out KingdomSubsidenceBatch batch)
					&& batch.Closing && batch.Departed == 5 && batch.FirstSequence == sequence,
					"same step must retire five credits; interrupted declaration retains both unappended summary lanes");
				Prefix(system.ChronicleEntries, officialPrefix);
				Prefix(system.OutsiderEntries, outsiderPrefix);
				VerifyDepartures(fixture, now, priorDepartures + 5);
				Check(!string.IsNullOrEmpty(stepId), "partial step identity was never captured");
				passed++; results.Append("\nremaining-four-summary-interruption=PASS");

				current = "same-tick-summary-recovery";
				KingdomSubsidence.Reckon(system, Zone, fixture.Survey, now);
				VerifyDepartures(fixture, now, priorDepartures + 5);
				Check(system.ChronicleEntries.Count == officialPrefix.Length + 5
					&& system.OutsiderEntries.Count == outsiderPrefix.Length + 5
					&& system.ChronicleEntries[system.ChronicleEntries.Count - 1] == official
					&& KingdomSubsidenceStepCodec.TryDecode(system.City.SubsidenceModel, out KingdomSubsidenceStepBook recovered)
					&& recovered.Active == null && recovered.Sequence == sequence
					&& recovered.BatchModel == KingdomSubsidenceBatchRules.None,
					"same-tick recovery must deliver exactly one summary and retire its batch without another departure");
				passed++; results.Append("\nsame-tick-summary-recovery=PASS");

				current = "same-tick-no-replay";
				string[] afterOfficial = system.ChronicleEntries.ToArray();
				string[] afterOutsider = system.OutsiderEntries.ToArray();
				KingdomSubsidence.Reckon(system, Zone, KingdomSurvey.Take(Zone, system), now);
				VerifyDepartures(fixture, now, priorDepartures + 5);
				Check(system.ChronicleEntries.Count == afterOfficial.Length
					&& system.OutsiderEntries.Count == afterOutsider.Length && fault.Throws == 1
					&& ReferenceEquals(The.Game, Game) && ReferenceEquals(The.Player, player)
					&& Game.TimeTicks == now, "same-tick retry changed the world clock or replayed summary effects");
				Prefix(system.ChronicleEntries, afterOfficial);
				Prefix(system.OutsiderEntries, afterOutsider);
				passed++; results.Append("\nsame-tick-no-replay=PASS");

				current = "synthetic-report-cut-recovery";
				passed += KingdomSubsidenceNativeLossChecks.Run(system, Zone, fixture.Survey, now, results);
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
						if (ReferenceEquals(fault.ParentObject, player)
							&& ReferenceEquals(player.GetPart<KingdomSubsidenceNativeSummaryFault>(), fault))
							player.RemovePart(fault);
						clean = fault.ParentObject == null
							&& player.GetPart<KingdomSubsidenceNativeSummaryFault>() == null;
					}
					catch (Exception) { clean = false; }
					if (!clean)
					{
						passed = Math.Min(passed, KingdomSubsidenceNativeProvider.ExpectedCases - 1);
						results.Append("\nhandler-cleanup=FAIL handler custody or removal unproved; unknown state retained");
					}
				}
				try { ReleaseHeld(held, player, Zone); }
				catch (Exception) { passed = Math.Min(passed, 4); results.Append("\nparty-cleanup=FAIL unknown custody retained"); }
				scope?.Dispose();
			}
			Ok = passed == KingdomSubsidenceNativeProvider.ExpectedCases;
			return "native-subsidence cases=8 passed=" + passed + " failed=" + (Ok ? 0 : 1)
				+ "; synthetic=true; elapsed=seeded-checkpoint; world-turns=untested; world-clock="
				+ (Game.TimeTicks == now ? "unchanged" : "changed")
				+ "; ordinary-acceptance=false; save-load=untested; breakpoint=not-exercised"
				+ "; partial-departure=one-plus-four; report-cuts=synthetic-persisted-state; profile-and-effects-retained=true" + results;
		}

		private static void ReleaseHeld(List<GameObject> Held, GameObject Player, Zone Zone)
		{
			foreach (GameObject body in Held)
			{
				Check(GameObject.Validate(body) && body.CurrentZone == Zone && body.Brain != null
					&& ReferenceEquals(body.Brain.PartyLeader, Player), "temporary party custody changed; retained");
				body.Brain.PartyLeader = null;
			}
			Held.Clear();
		}

		private static void VerifyDepartures(KingdomSubsidenceNativeFixture Fixture, long Now, int Departures)
		{
			KingdomSystem system = Fixture.System;
			Check(system.Population == 45 && KingdomResidents.OnRollCount(system) == 45
				&& system.LastSubsidenceTick == Now && system.Stage == GrowthStage.City
				&& system.Ledger.Departures == Departures
				&& KingdomResidentDepartureRules.IsEmpty(system.ResidentDeparture),
				"native departure accounting, checkpoint, stage or terminal journal disagrees");
			int absent = 0;
			for (int i = 0; i < Fixture.Bodies.Count; i++)
			{
				GameObject body = Fixture.Bodies[i];
				if (!GameObject.Validate(body))
				{
					Check(KingdomResidents.DepartureCarriersAbsent(system, system.City, Fixture.ResidentIds[i]),
						"destroyed native resident retains a row or binding");
					absent++;
				}
				else Check(body.CurrentZone == Fixture.Zone && body.IsAlive
					&& KingdomResidents.IdOf(body) == Fixture.ResidentIds[i],
					"a surviving fixture resident lost exact physical identity or ground");
			}
			Check(absent == 5 && KingdomSurvey.Take(Fixture.Zone, system).Settlers.Count == 45,
				"exactly five fixture bodies must be destroyed and 45 physically surveyed");
		}

		private static void Prefix(List<string> Current, string[] Expected)
		{
			Check(Current.Count >= Expected.Length, "register prefix was truncated");
			for (int i = 0; i < Expected.Length; i++)
				Check(string.Equals(Current[i], Expected[i], StringComparison.Ordinal), "register prefix was overwritten");
		}

		private static void Check(bool Condition, string Detail)
		{
			if (!Condition) throw new InvalidOperationException(Detail);
		}
	}
}
