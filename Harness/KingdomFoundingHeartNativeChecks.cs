using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	internal static class KingdomFoundingHeartNativeChecks
	{
		internal static string Run(XRLGame game, Zone zone, out bool ok)
		{
			int passed = 0;
			string current = "real-founding-allocation-and-seven-reservations";
			StringBuilder rows = new StringBuilder();
			try
			{
				Check(r_TAF_FoundingHeartMintProbe.Count == 0 && r_TAF_FoundingHeartMintProbe.Callback == null
					&& r_TAF_FoundingHeartMintProbe.Error == null, "prior heart probe evidence exists");
				KingdomFoundingHeartProbeBlueprints probes = KingdomFoundingHeartProbeBlueprints.Install();
				List<GameObject> created = new List<GameObject>();
				r_TAF_FoundingHeartMintProbe.Callback = (body, e) => {
					Check(!(body.IDIfAssigned ?? "").StartsWith("taf-heart-v1-", StringComparison.Ordinal),
						"heart identity published before the native creation callback");
					Check(KingdomFoundingHeartRules.TryDecode(zone.GetZoneProperty(KingdomPlots.FoundingHeartReceiptProperty, null),
						out KingdomFoundingHeartPlan atCallback), "callback receipt absent");
					Reservations(atCallback);
					created.Add(body);
				};
				KingdomSubsidenceNativeFixture fixture;
				try { Check(KingdomSubsidenceNativeFixture.TryCreate(zone, out fixture, out string failure), failure); }
				finally { r_TAF_FoundingHeartMintProbe.Callback = null; }
				Check(KingdomFoundingHeartRules.TryDecode(zone.GetZoneProperty(KingdomPlots.FoundingHeartReceiptProperty, null),
					out KingdomFoundingHeartPlan plan) && KingdomFoundingHeartRules.Complete(plan), "real heart is incomplete");
				Check(created.Count == 6 && r_TAF_FoundingHeartMintProbe.Count == 6
					&& r_TAF_FoundingHeartMintProbe.Error == null, "real founding did not expose six exact allocations");
				for (int slot = 0; slot < 6; slot++)
					Check(GameObject.Validate(created[slot]) && created[slot].IDIfAssigned == KingdomFoundingHeartRules.SlotId(plan, slot)
						&& ReferenceEquals(created[slot].CurrentZone, zone), "factory reference did not become its exact physical slot");
				Reservations(plan);
				Check(KingdomPlots.AuditFoundingHeartReservations(fixture.System, zone), "valid native heart audit refused");
				Pass(rows, current, ref passed);

				current = "typed-state-matrix-224-shapes";
				r_TAF_FoundingHeartMintProbe.Callback = (body, e) => { };
				KingdomFoundingHeartReservationNativeCases.TypedStates(game, zone, plan);
				Pass(rows, current, ref passed);
				current = "malformed-string-matrix-21-shapes";
				KingdomFoundingHeartReservationNativeCases.MalformedStrings(game, zone, plan);
				Pass(rows, current, ref passed);
				current = "all-seven-preflight-before-write";
				KingdomFoundingHeartReservationNativeCases.PreflightAllSeven(game, zone, plan);
				r_TAF_FoundingHeartMintProbe.Callback = null;
				Pass(rows, current, ref passed);
				current = "shared-guard-genuine-factory-three-blueprints";
				KingdomFoundingHeartAllocationNativeCases.GenuineFactory(game, zone, plan);
				Pass(rows, current, ref passed);
				current = "shared-guard-native-typed-callback-three-blueprints";
				KingdomFoundingHeartAllocationNativeCases.TypedCallback(game, zone, plan);
				Pass(rows, current, ref passed);
				current = "shared-guard-native-foreign-replacement-three-blueprints";
				KingdomFoundingHeartAllocationNativeCases.ForeignReplacement(game, zone, plan);
				probes.Check();
				Reservations(plan);
				Check(KingdomPlots.AuditFoundingHeartReservations(fixture.System, zone), "final native heart audit refused");
				Pass(rows, current, ref passed);
			}
			catch (Exception error)
			{
				rows.Append('\n').Append(current).Append("=FAIL ").Append(KingdomScenarioRules.Bounded(error.ToString()));
			}
			finally { r_TAF_FoundingHeartMintProbe.Callback = null; }
			ok = passed == KingdomFoundingHeartNativeProvider.ExpectedCases;
			return "Founding-heart native checks: cases=" + KingdomFoundingHeartNativeProvider.ExpectedCases
				+ " passed=" + passed + " failed=" + (ok ? 0 : 1) + rows
				+ "\nScope: real founding; synthetic state faults and direct shared-guard calls; native callbacks."
				+ " No terminal completion, ordinary-save compatibility or all-callback-cut acceptance. Evidence retained.";
		}

		private static void Reservations(KingdomFoundingHeartPlan plan)
		{
			for (int slot = 0; slot <= KingdomFoundingHeartRules.SlotCount; slot++)
			{
				string role = slot == KingdomFoundingHeartRules.SlotCount ? "final" : "slot-" + slot;
				string id = KingdomFoundingHeartRules.StableId(plan.TransactionId, plan.ZoneId, role);
				Check(KingdomScenarioDurableState.ProvesExactText(KingdomPlots.FoundingHeartReservationPrefix + id,
					KingdomFoundingHeartReservationRules.Encode(plan, id, role)), "seven exact typed reservations do not stand");
			}
		}

		private static void Pass(StringBuilder rows, string name, ref int passed)
		{
			passed++; rows.Append('\n').Append(name).Append("=PASS");
		}
		private static void Check(bool condition, string failure)
		{
			KingdomFoundingHeartAllocationNativeCases.Check(condition, failure);
		}
	}
}
