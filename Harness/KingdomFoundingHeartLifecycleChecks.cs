using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	internal static class KingdomFoundingHeartLifecycleChecks
	{
		internal static string Run(XRLGame game, Zone zone, out bool ok)
		{
			int passed = 0;
			bool failed = false;
			string current = "first-mark-typed-callback-refused";
			StringBuilder rows = new StringBuilder();
			try
			{
				KingdomFoundingHeartProbeBlueprints probes = KingdomFoundingHeartProbeBlueprints.Install(true);
				var world = new KingdomFoundingHeartLifecycleWorld(game, zone);
				FoundingFault(world, "r_KingdomFirstBasin", true, 0);
				Pass(rows, current, ref passed);
				current = "first-mark-foreign-callback-refused";
				FoundingFault(world, "r_KingdomFirstBasin", false, 0);
				Pass(rows, current, ref passed);
				current = "works-typed-callback-refused";
				FoundingFault(world, "r_KingdomPlotWorks", true, KingdomFoundingHeartRules.WorksSlot);
				Pass(rows, current, ref passed);
				current = "works-foreign-callback-refused";
				FoundingFault(world, "r_KingdomPlotWorks", false, KingdomFoundingHeartRules.WorksSlot);
				Pass(rows, current, ref passed);
				current = "same-transaction-clean-founding-recovery";
				Check(KingdomScenarioFoundingStep.TryFound(zone, world.Name, out _, out string failure), failure);
				r_KingdomPlotWorks works = world.Founded();
				Check(works != null && works.StageApplied == (int)KingdomPlotRules.PlotStage.Staked,
					"clean founding did not produce its staked work");
				Check(KingdomScenarioTransactionMarker.TryCommit(out failure), failure);
				Pass(rows, current, ref passed);
				GameObject predecessor = works.ParentObject;
				KingdomFoundingHeartPlan plan = world.Plan();
				world.ClearPlayerFromHeart(plan);
				// Explicit synthetic frontier: future calendar argument, not a world-clock edit or elapsed play.
				long finishTick = checked(plan.StartedTick + plan.TotalTicks);
				current = "terminal-typed-callback-refused";
				TerminalFault(world, works, finishTick, true);
				Pass(rows, current, ref passed);
				current = "terminal-foreign-callback-refused";
				TerminalFault(world, works, finishTick, false);
				Pass(rows, current, ref passed);
				current = "real-terminal-completion-and-tombstone";
				GameObject observed = null;
				int targeted = 0;
				r_TAF_FoundingHeartMintProbe.Callback = (body, e) => {
					if (body.Blueprint != "r_KingdomRiteGround") return;
					Check(observed == null && !(body.IDIfAssigned ?? "").StartsWith("taf-heart-v1-", StringComparison.Ordinal),
						"final identity preceded its factory callback or allocation repeated");
					observed = body; targeted++;
				};
				try { KingdomPlots.Advance(works, world.System, finishTick); }
				finally { r_TAF_FoundingHeartMintProbe.Callback = null; }
				GameObject final = world.Completed(predecessor);
				Check(targeted == 1 && ReferenceEquals(final, observed) && r_TAF_FoundingHeartMintProbe.Error == null,
					"completion did not retain its one real factory reference");
				Pass(rows, current, ref passed);
				current = "completed-recovery-does-not-replay";
				NoReplay(world, predecessor, final);
				probes.Check();
				Pass(rows, current, ref passed);
				KingdomFoundingHeartRetirementChecks.Run(world, predecessor, final, rows, ref passed, ref current);
				probes.Check();
				rows.Append("\nCalendar: suppliedTick=").Append(finishTick).Append(" unchangedWorldTick=").Append(world.Tick);
			}
			catch (Exception error)
			{
				failed = true;
				rows.Append('\n').Append(current).Append("=FAIL ").Append(KingdomScenarioRules.Bounded(error.ToString()));
			}
			finally { r_TAF_FoundingHeartMintProbe.Callback = null; }
			ok = !failed && passed == KingdomFoundingHeartLifecycleProvider.ExpectedCases;
			return "Founding-heart lifecycle: cases=" + KingdomFoundingHeartLifecycleProvider.ExpectedCases
				+ " passed=" + passed + " failed=" + (ok ? 0 : 1) + rows
				+ "\nScope: real direct founding, native factory callbacks, actual Advance stages and terminal effects."
				+ " Founder walks west out of the footprint through bounded native Move; position is not restored."
				+ " Synthetic faults and future calendar argument; no world-clock edit, ordinary progression, save/load"
				+ " or root-after-write refusal claim. Six observational native retirement negatives;"
				+ " null collection is not throwing-reader coverage. Original/foreign evidence retained.";
		}

		private static void FoundingFault(KingdomFoundingHeartLifecycleWorld world, string blueprint, bool typed, int slot)
		{
			world.Current();
			var fault = new KingdomFoundingHeartLifecycleFault(world.Game, world.Zone, blueprint, typed);
			fault.Arm();
			bool founded;
			string failure;
			try { founded = KingdomScenarioFoundingStep.TryFound(world.Zone, world.Name, out _, out failure); }
			finally { r_TAF_FoundingHeartMintProbe.Callback = null; }
			Check(!founded && !string.IsNullOrEmpty(failure), "injected callback did not refuse full founding");
			fault.VerifyRefusal();
			world.Pending(slot);
			if (typed) fault.RetireInjection();
		}

		private static void TerminalFault(KingdomFoundingHeartLifecycleWorld world, r_KingdomPlotWorks works,
			long tick, bool typed)
		{
			var fault = new KingdomFoundingHeartLifecycleFault(world.Game, world.Zone, "r_KingdomRiteGround", typed);
			fault.Arm();
			try { KingdomPlots.Advance(works, world.System, tick); }
			finally { r_TAF_FoundingHeartMintProbe.Callback = null; }
			Check(fault.Observed != null, "final factory not reached; stage=" + works.StageApplied);
			fault.VerifyRefusal();
			world.NoTerminal(works);
			if (typed) fault.RetireInjection();
		}

		private static void NoReplay(KingdomFoundingHeartLifecycleWorld world, GameObject predecessor, GameObject final)
		{
			string wire = world.Zone.GetZoneProperty(KingdomPlots.FoundingHeartTerminalProperty, null);
			string[] notes = world.System.Ledger.Notes.ToArray();
			GameObject[] bodies = new List<GameObject>(world.Zone.GetObjects()).ToArray();
			var strings = new Dictionary<string, string>(final.Property);
			var ints = new Dictionary<string, int>(final.IntProperty);
			int count = r_TAF_FoundingHeartMintProbe.Count;
			r_TAF_FoundingHeartMintProbe.Callback = (body, e) => { };
			try
			{
				Check(KingdomPlots.RecoverFoundingHeart(world.System, world.Zone), "settled recovery refused");
				Check(KingdomPlots.AuditFoundingHeartReservations(world.System, world.Zone), "settled reservation audit refused");
			}
			finally { r_TAF_FoundingHeartMintProbe.Callback = null; }
			Check(ReferenceEquals(world.Completed(predecessor), final)
				&& world.Zone.GetZoneProperty(KingdomPlots.FoundingHeartTerminalProperty, null) == wire
				&& r_TAF_FoundingHeartMintProbe.Count == count && r_TAF_FoundingHeartMintProbe.Error == null,
				"settled recovery allocated or changed terminal authority");
			Check(notes.Length == world.System.Ledger.Notes.Count, "settled recovery replayed ledger notes");
			for (int i = 0; i < notes.Length; i++) Check(notes[i] == world.System.Ledger.Notes[i], "settled ledger changed");
			GameObject[] after = new List<GameObject>(world.Zone.GetObjects()).ToArray();
			Check(after.Length == bodies.Length, "settled recovery changed physical roster");
			for (int i = 0; i < bodies.Length; i++) Check(ReferenceEquals(after[i], bodies[i]), "settled physical roster changed");
			Check(strings.Count == final.Property.Count && ints.Count == final.IntProperty.Count, "settled final properties changed");
			foreach (var row in strings) Check(final.Property.TryGetValue(row.Key, out string value) && row.Value == value, "final text changed");
			foreach (var row in ints) Check(final.IntProperty.TryGetValue(row.Key, out int value) && row.Value == value, "final integer changed");
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
