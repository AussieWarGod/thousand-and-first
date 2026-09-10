using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Native-check seam for the brush-forage duty (T-forage-1, issue #44). The setup founds a
	/// real camp (the #107 heart stockpile is the store the cut brush lands in), enrolls four
	/// real residents, and plants one object per exclusion category plus three eligible wild
	/// plants. Every following check reads outcomes the REAL settlement-pass cadence produced on
	/// real turns the persona's <c>advance</c> spends -- this seam never calls the private
	/// <c>KingdomMaterials.WorkForage</c> directly, and never realizes a scenario.
	/// <para>
	/// SYNTHETIC SETUP, DISCLOSED. Every planted object, enrolled resident, and stocked/marked
	/// unit is fixture state the harness created through real production APIs (GameObject.Create,
	/// Cell.AddObject, KingdomCitizenship.TryEnroll, KingdomResidents.TryEnsureRow), not ordinary
	/// play. This does not sign a rendered Charter, ordinary play, or general camp acceptance.
	/// </para>
	/// </summary>
	[KingdomScenarioVerbProvider]
	public sealed class KingdomForageNativeProvider : IKingdomScenarioVerbProvider
	{
		internal const string SetupVerb = "forage-setup";
		internal const string CheckVerb = "forage-check";
		internal const string Receipt = "r_TAF_ScenarioForageNative_v1";
		private static readonly string[] Script = { "stagedigest", SetupVerb, "advance 2400",
			CheckVerb, "advance 2400", CheckVerb, "advance 2400", CheckVerb, "advance 2400",
			CheckVerb, "advance 2400", CheckVerb, "advance 2400", CheckVerb, "advance 2400",
			CheckVerb, "stagedigest" };

		public int ScenarioVerbApiVersion { get { return KingdomScenarioVerbApi.Version; } }

		public IEnumerable<string> ScenarioVerbs { get { return new[] { SetupVerb, CheckVerb }; } }

		public string RunScenarioVerb(string Verb, string Argument, out bool Ok)
		{
			Ok = false;
			try
			{
				Require(string.IsNullOrEmpty(Argument) && (Verb == SetupVerb || Verb == CheckVerb),
					"forage verbs take no arguments");
				IList<string> script;
				Require(KingdomScenarioScript.TryRead(out script, out _)
					&& script.Count == Script.Length, "the exact sealed forage script is absent");
				for (int i = 0; i < Script.Length; i++)
					Require(script[i] == Script[i], "the sealed forage script differs");
				XRLGame game = The.Game;
				Zone zone = The.Player?.CurrentZone;
				if (Verb == SetupVerb)
				{
					Require(Eligible(game, zone), "requires a fresh stamped camp with the "
						+ "master, growth, civic story and native messages enabled");
					game.SetStringGameState(Receipt, "intent");
					Require(KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"),
						"the forage intent failed its exact readback");
				}
				Require(game != null && KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"),
					"the forage owner intent is absent or torn");
				bool complete;
				string result = KingdomForageNativeChecks.Run(Verb, game, zone, out complete);
				if (complete)
				{
					Require(ReferenceEquals(The.Game, game), "the forage report owner changed");
					game.SetStringGameState(Receipt, result);
					Require(KingdomScenarioDurableState.ProvesExactText(Receipt, result),
						"the forage report failed its exact readback");
				}
				Ok = true;
				return result;
			}
			catch (Exception error) { return KingdomForageNativeChecks.Fail(error); }
		}

		private static bool Eligible(XRLGame Game, Zone Zone)
		{
			KingdomScenarioPlan plan;
			return Game != null && Zone != null
				&& ReferenceEquals(The.ZoneManager?.ActiveZone, Zone)
				&& XRL.Messages.MessageQueue.Enabled
				&& KingdomMaster.ConfiguredEnabled && KingdomGrowth.Enabled
				&& XRL.UI.Options.GetOption(KingdomExperienceOptions.StoryOptionId, "Yes") != "No"
				&& !KingdomSurvey.HasBoundPass && !KingdomScenarioAdvance.Pending
				&& !(Game.GetSystem<KingdomSystem>()?.Founded ?? false)
				&& !KingdomNativeRegressionContext.HasQuickstartState(Game)
				&& !KingdomNativeRegressionContext.HasAnyState(Game, Receipt)
				&& KingdomScenarioRealizer.TryBindStampedPlan(out plan, out _, out _)
				&& plan.Key == "founding-first-city"
				&& plan.AuthorityClass == KingdomScenarioFoundingStep.FoundingAuthority
				&& KingdomScenarioTransactionMarker.Observe(out _)
					== KingdomScenarioTransactionShape.None;
		}

		internal static void Require(bool Value, string Failure)
		{
			if (!Value) throw new InvalidOperationException(
				Failure ?? "native forage evidence refused");
		}
	}
}
