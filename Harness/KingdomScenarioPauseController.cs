using System;
using System.Collections.Generic;
using XRL;
using XRL.UI;

namespace ThousandAndFirst.Harness
{
	/// <summary>Drives real option changes only. Production wakes own all clock publication.</summary>
	[KingdomScenarioVerbProvider]
	public sealed class KingdomScenarioPauseController : IKingdomScenarioVerbProvider
	{
		private const string GrowthOption = "r_TAF_OptionGrowth";
		private static string OriginalGrowth, OriginalMaster, OwnedGrowth, OwnedMaster;
		private static bool Started, Disabled, Resuming, Finished;
		public int ScenarioVerbApiVersion => KingdomScenarioVerbApi.Version;
		public IEnumerable<string> ScenarioVerbs => new[] { "beta-local-pause", "beta-master-pause" };
		internal static bool Active => Started && !Finished;

		internal static bool Recipe(IList<string> script, string direction)
		{
			string[] exact = { "stagedigest", "realize", "advance 1200", "beta-local-pause", "advance 1200",
				"beta-master-pause", "advance 1", "beta-stress", direction, "advance 1200", "beta-return",
				"advance 39", "yield-frames 1", "beta-check", "status" };
			if (script == null || script.Count != exact.Length) return false;
			for (int i = 0; i < exact.Length; i++) if (script[i] != exact[i]) return false;
			return direction == "beta-away" || direction == "beta-present";
		}

		public string RunScenarioVerb(string Verb, string Argument, out bool Ok)
		{
			Ok = false;
			try
			{
				Require(string.IsNullOrEmpty(Argument), "pause verbs take no arguments");
				Require(KingdomScenarioScript.TryRead(out IList<string> script, out string why), why);
				Require(Recipe(script, "beta-away") || Recipe(script, "beta-present"), "requires exact pause travel recipe");
				if (Verb == "beta-local-pause")
				{
					Require(!Started && !Finished && KingdomScenarioRealizer.TryBindStampedPlan(out var plan,
						out _, out why) && plan.Key == "founding-first-city", why ?? "requires fresh first-city plan");
					OriginalGrowth = Options.GetOption(GrowthOption, "Yes");
					OriginalMaster = Options.GetOption(KingdomMaster.OptionId, "Yes");
					Require(OriginalGrowth == "Yes" && OriginalMaster == "Yes", "both options must start enabled");
					var witness = new KingdomScenarioPauseWitness(The.Game?.GetSystem<KingdomSystem>());
					Require(!witness.Growth.WorkPaused, "local growth was already paused");
					Started = true; OwnedGrowth = OriginalGrowth; OwnedMaster = OriginalMaster;
					Set(GrowthOption, "No", ref OwnedGrowth);
				}
				else if (Verb == "beta-master-pause")
				{
					var witness = KingdomScenarioPauseWitness.Current;
					Require(Active && !Disabled && witness != null && witness.Fault == null
						&& ReferenceEquals(witness.Game, The.Game) && witness.Growth.WorkPaused
						&& witness.Growth.WorkPauseStartedTick <= The.Game.TimeTicks,
						"ordinary growth wake did not establish the local pause");
					Set(KingdomMaster.OptionId, "No", ref OwnedMaster); Disabled = true;
				}
				else throw new InvalidOperationException("unknown pause verb");
				Ok = true; return "taf-pause-option-set verb=" + Verb + "; tick=" + The.Game.TimeTicks;
			}
			catch (Exception error) { Stop(); return KingdomScenarioRefusal.Message("taf-pause-refused", error.Message); }
		}

		internal static void BeforeTravel()
		{
			if (!Active) return;
			var witness = KingdomScenarioPauseWitness.Current;
			Require(Disabled && witness.Fault == null && witness.DisabledTick >= 0
				&& witness.System.MasterOption == KingdomMasterLatchValue.Disabled, "master disable was not observed");
			// Restore local configuration while master is latched off; no local clock is advanced here.
			Set(GrowthOption, OriginalGrowth, ref OwnedGrowth);
		}

		internal static void AtHome()
		{
			if (!Active || Resuming) return;
			Require(Disabled && KingdomScenarioPauseWitness.Current.Fault == null, "pause witness refused before return");
			KingdomScenarioContainerStress.BeforeResume();
			Set(KingdomMaster.OptionId, OriginalMaster, ref OwnedMaster); Resuming = true;
		}

		internal static string Check()
		{
			if (!Active) return "; pause-effects-proved=false";
			var witness = KingdomScenarioPauseWitness.Current;
			Require(Resuming, "return never requested resume"); witness.Check();
			Require(Options.GetOption(GrowthOption, "Yes") == OriginalGrowth
				&& Options.GetOption(KingdomMaster.OptionId, "Yes") == OriginalMaster, "options changed during pause fixture");
			string result = "; pause-effects-proved=true; pause-disabled=" + witness.DisabledTick
				+ "; pause-resumed=" + witness.ResumeTick + "; paused-ticks=" + witness.ObservedPaused
				+ "; resume-arrival=" + witness.ObservedArrival + "; resume-applications=" + witness.ResumeApplications;
			result += "; pause-local-start=" + witness.LocalStart + "; pause-prior=" + witness.PriorPaused
				+ "; arrival-interval=" + witness.ArrivalInterval;
			witness.Armed = false; Finished = true; return result;
		}

		private static void Set(string key, string value, ref string owned)
		{
			Require(Options.GetOption(key, "Yes") == owned, "option custody changed: " + key);
			Options.SetOption(key, value); owned = value;
			Require(Options.GetOption(key, "Yes") == value, "option write did not persist: " + key);
		}

		internal static void Stop()
		{
			var witness = KingdomScenarioPauseWitness.Current;
			if (witness != null) witness.Armed = false;
			if (!Active || witness == null || !ReferenceEquals(The.Game, witness.Game)) return;
			// Restore only values still owned by this fixture. Never overwrite an external edit.
			try
			{
				if (Options.GetOption(GrowthOption, "Yes") == OwnedGrowth) Options.SetOption(GrowthOption, OriginalGrowth);
				if (Options.GetOption(KingdomMaster.OptionId, "Yes") == OwnedMaster) Options.SetOption(KingdomMaster.OptionId, OriginalMaster);
			}
			finally { Finished = true; }
		}

		private static void Require(bool value, string why) { KingdomScenarioTravel.Require(value, why); }
	}
}
