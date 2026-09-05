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
			GameObject player = The.Player;
			long now = Game.TimeTicks;
			try
			{
				Check(KingdomSubsidenceNativeFixture.TryCreate(Zone, out fixture, out string failure), failure);
				KingdomSystem system = fixture.System;
				long anchor = now - KingdomSubsidenceRules.StepDays * KingdomRules.TicksPerDay;
				// Only the supplied elapsed checkpoint is seeded. The global world clock is never advanced.
				KingdomSubsidence.Reckon(system, Zone, fixture.Survey, anchor);
				KingdomSubsidence.Reckon(system, Zone, fixture.Survey, anchor + 1L);
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
				passed++; results.Append("\nphysical-city-fixture=PASS");

				current = "five-departures-summary-interruption";
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
				Check(system.ChronicleEntries.Count == officialPrefix.Length + 5
					&& system.OutsiderEntries.Count == outsiderPrefix.Length + 4
					&& system.ChronicleEntries[system.ChronicleEntries.Count - 1] == official,
					"interrupted summary must retain its official append without an outsider append");
				Prefix(system.ChronicleEntries, officialPrefix);
				Prefix(system.OutsiderEntries, outsiderPrefix);
				VerifyDepartures(fixture, now, priorDepartures + 5);
				passed++; results.Append("\nfive-departures-summary-interruption=PASS");

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
						passed = Math.Min(passed, 2);
						results.Append("\nhandler-cleanup=FAIL handler custody or removal unproved; unknown state retained");
					}
				}
			}
			Ok = passed == KingdomSubsidenceNativeProvider.ExpectedCases;
			return "native-subsidence cases=3 passed=" + passed + " failed=" + (Ok ? 0 : 1)
				+ "; synthetic=true; elapsed=seeded-checkpoint; world-turns=untested; world-clock="
				+ (Game.TimeTicks == now ? "unchanged" : "changed")
				+ "; ordinary-acceptance=false; save-load=untested; breakpoint=not-exercised"
				+ "; partial-departure=untested; profile-and-effects-retained=true" + results;
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
