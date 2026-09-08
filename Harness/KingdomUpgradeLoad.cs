using System;
using System.Collections.Generic;
using System.IO;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	internal static class KingdomUpgradeLoad
	{
		private static KingdomInheritanceState Reading, ReadOwner;
		private static SerializationReader Reader;
		private static int ReadErrors, Reads, ShapeEntries, ShapeSuccesses, Repairs, NamedReads, Preactivations;
		private static string Fault;
		private static KingdomUpgradeState PreactivationState;
		private static readonly List<object> RawTransitions = new List<object>(), RawLegacies = new List<object>();
		internal static bool Active => KingdomScenarioLoadEntry.Armed && KingdomScenarioLoadEntry.UpgradeSnapshot != null;
		private static KingdomUpgradeSnapshot Expected => KingdomScenarioLoadEntry.UpgradeSnapshot;

		internal static void Prepare()
		{
			KingdomUpgradeState.Engine();
			Check(Reads == 0 && NamedReads == 0 && PreactivationState == null && Fault == null,
				"upgrade load observation was reused");
		}

		internal static void BeginRead(KingdomInheritanceState value, SerializationReader reader)
		{
			if (!Active) return;
			Observe(delegate
			{
				Check(Reading == null && ++Reads == 1, "upgrade inheritance reader repeated");
				Reading = value; Reader = reader; ReadErrors = reader.Errors;
			});
		}

		internal static void BeforeShape(KingdomInheritanceSavedShape shape, string target)
		{
			if (!Active || Reading == null) return;
			Observe(delegate
			{
				Check(++ShapeEntries == 1 && target == Expected.GameId && shape != null
					&& shape.LegacyText == KingdomUpgradeState.Field<string>(Reading, "LegacyText")
					&& KingdomUpgradeGraph.Capture(Reading) == Expected.Inheritance,
					"inheritance raw read differs before saved-shape validation or repair");
			});
		}

		internal static void AfterShape(bool success)
		{
			if (!Active || Reading == null) return;
			Observe(delegate { Check(success && ++ShapeSuccesses == 1, "actual inherited saved-shape validation refused"); });
		}

		internal static void EndRead(KingdomInheritanceState value)
		{
			if (!Active) return;
			Observe(delegate
			{
				Check(ReferenceEquals(Reading, value) && Reader != null && Reader.Errors == ReadErrors
					&& ShapeEntries == 1 && ShapeSuccesses == 1 && Repairs == 0
					&& KingdomUpgradeGraph.Capture(value) == Expected.Inheritance,
					"inheritance read did not preserve exact pre-repair bytes");
				ReadOwner = value;
			});
		}

		internal static void FinalizeRead(Exception error)
		{
			if (!Active) return;
			if (error != null) Fail(error);
			Reading = null; Reader = null;
		}

		internal static void Repair(KingdomInheritanceState value)
		{
			if (!Active) return;
			// Any inherited-state repair during this selected load prevents a no-repair result.
			Repairs++; Fail(new InvalidOperationException("production inheritance repair/disable invoked"));
		}

		internal static void NamedRead(object value, Type type)
		{
			if (!Active || type != typeof(KingdomPolityRealmTransition) && type != typeof(KingdomPolityLegacySnapshot)) return;
			Observe(delegate
			{
				Check(value != null && value.GetType() == type && ++NamedReads <= 64, "upgrade named-reader witness is ambiguous or excessive");
				string raw = KingdomUpgradeGraph.Capture(value);
				// Save the actual object reference before its own Read calls Normalize. Later root identity selects it.
				if (type == typeof(KingdomPolityRealmTransition) && raw == Expected.Transition) RawTransitions.Add(value);
				if (type == typeof(KingdomPolityLegacySnapshot) && raw == Expected.Legacy) RawLegacies.Add(value);
			});
		}

		internal static void BeforeActivation()
		{
			if (!Active) return;
			Observe(delegate
			{
				Check(Fault == null && ++Preactivations == 1 && KingdomScenarioLoadReaderWitness.Releases == 1
					&& !KingdomScenarioLoadReaderWitness.HadErrors, "upgrade primary reader did not finish exactly without errors: " + Fault);
				KingdomUpgradeState state = new KingdomUpgradeState(The.Game, Expected.Case); state.Matches(Expected);
				Check(state.Inheritance == null ? Reads == 0 : Reads == 1 && ReferenceEquals(ReadOwner, state.Inheritance)
					&& ShapeEntries == 1 && ShapeSuccesses == 1 && Repairs == 0,
					"selected inheritance lacks its exact pre-repair reader witness");
				Check(state.Transition == null || Count(RawTransitions, state.Transition) == 1,
					"selected transition lacks an exact pre-normalization named-reader witness");
				Check(state.Transition?.Legacy == null || Count(RawLegacies, state.Transition.Legacy) == 1,
					"selected nested Legacy lacks its exact pre-normalization named-reader witness");
				Check(KingdomUpgradeFiles.Same(Path.GetFullPath(state.Game._CacheDirectory),
					KingdomUpgradeFiles.SaveDirectory(KingdomUpgradeFiles.Root(), Expected.GameId)), "loaded upgrade cache was not rebased exactly");
				PreactivationState = state;
				Check(KingdomScenarioJournal.Append("UPGRADE-PREACTIVATION", true,
					"case=" + Expected.Case + "; raw-source-exact=true; pre-normalization=true; pre-repair=true; repair-calls=0"
					+ state.Stage()) == null,
					"upgrade preactivation journal failed");
			});
		}

		internal static void VerifyLoaded(XRLGame game)
		{
			Check(Fault == null && Preactivations == 1 && PreactivationState != null
				&& ReferenceEquals(PreactivationState.Game, game) && Repairs == 0
				&& !KingdomScenarioLoadReaderWitness.HadErrors, "upgrade load lacks complete observed barriers: " + Fault);
			PreactivationState.Matches(Expected);
			// Re-run only read-only validators, then prove the original loaded graph remained untouched.
			KingdomUpgradeState after = new KingdomUpgradeState(game, Expected.Case); after.Matches(Expected);
			PreactivationState.Exact(); PreactivationState.Stage();
			Check(KingdomScenarioJournal.Append("SCRIPT-COMPLETE", true,
				"native-upgrade cases=1 passed=1 failed=0; case=" + Expected.Case
				+ "; actual-source-save=true; old-pin=" + Expected.OldPin
				+ "; pre-normalization-and-pre-repair=true; legacy-canonical=true; repair-calls=0"
				+ "; source-graph-preserved=true; ordinary-ui-acceptance=false; no-world-state-written-by-witness=true") == null,
				"upgrade completion journal failed");
		}

		private static int Count(List<object> values, object selected)
		{ int count = 0; foreach (object value in values) if (ReferenceEquals(value, selected)) count++; return count; }

		private static void Observe(Action observation)
		{ try { observation(); } catch (Exception error) { Fail(error); } }

		private static void Fail(Exception error)
		{
			if (Fault != null) return;
			Fault = error.GetType().Name + ": " + error.Message;
			if (Fault.Length > 1000) Fault = Fault.Substring(0, 1000);
			KingdomScenarioJournal.Append("UPGRADE-OBSERVATION", false, Fault);
		}

		private static void Check(bool condition, string failure) { KingdomUpgradeFiles.Check(condition, failure); }
	}
}
