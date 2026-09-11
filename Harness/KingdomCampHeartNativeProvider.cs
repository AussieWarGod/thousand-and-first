using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Native-check seam for the camp heart's dedicated store across a REAL PAID heart-rung
	/// upgrade (PR #107 native case 2, issue #41). The setup founds a real camp, completes its
	/// rung-1 rite ground, fills the authored camp stockpile to its declared capacity, and makes
	/// the settlement genuinely able to afford the rung-2 bill. Nothing after that is driven by
	/// this seam: the real settlement pass assesses the heart, begins the improvement, debits the
	/// water and the materials, and burns the labour on the turns the persona's <c>advance</c>
	/// spends.
	/// <para>
	/// SYNTHETIC SETUP, DISCLOSED. The camp, the six residents, the dedicated reservoir, the
	/// drams in it, and every unit inside the camp stockpile are fixture state created through
	/// real production APIs, not ordinary play. The founder's one-time improvement notice is
	/// pre-marked as read so a headless run raises no modal. This seam NEVER calls
	/// <c>KingdomUpgrade.Begin</c>, <c>KingdomPlots.Advance</c>,
	/// <c>KingdomArchitectureStamper.TryApplyUpgrade</c> or any gallery staging path.
	/// </para>
	/// </summary>
	[KingdomScenarioVerbProvider]
	public sealed class KingdomCampHeartNativeProvider : IKingdomScenarioVerbProvider
	{
		internal const string SetupVerb = "camp-heart-setup";
		internal const string CheckVerb = "camp-heart-check";
		internal const string Receipt = "r_TAF_ScenarioCampHeartNative_v1";
		/// <summary>The sealed rung 1 -> 2 script (issue #41 / PR #107 case 2). The last pair is
		/// the acceptance gate for issue #162: one more ordinary day AFTER the rung was raised, and
		/// then a check that the settlement pass still runs on this ground.</summary>
		private static readonly string[] Rung2Script = { "stagedigest", SetupVerb, "advance 1200",
			CheckVerb, "advance 3600", CheckVerb, "advance 1200", CheckVerb, "stagedigest" };

		/// <summary>The sealed rung 1 -> 2 -> 3 script (issue #159). The rung-2 climb is run
		/// FIRST, in this same persona, so the rung-3 start state is reached by the ordinary
		/// improvement route rather than seeded; the third window carries the moot yard's own
		/// 6000-tick labour.</summary>
		private static readonly string[] Rung3Script = { "stagedigest", SetupVerb, "advance 1200",
			CheckVerb, "advance 3600", CheckVerb, "advance 7200", CheckVerb, "stagedigest" };

		public int ScenarioVerbApiVersion { get { return KingdomScenarioVerbApi.Version; } }

		public IEnumerable<string> ScenarioVerbs { get { return new[] { SetupVerb, CheckVerb }; } }

		public string RunScenarioVerb(string Verb, string Argument, out bool Ok)
		{
			Ok = false;
			try
			{
				Require(string.IsNullOrEmpty(Argument) && (Verb == SetupVerb || Verb == CheckVerb),
					"camp heart verbs take no arguments");
				int targetRung = SealedTargetRung();
				XRLGame game = The.Game;
				Zone zone = The.Player?.CurrentZone;
				if (Verb == SetupVerb)
				{
					Require(Eligible(game, zone), "requires a fresh stamped unfounded camp with the "
						+ "master, growth, lodging, subsidence and native messages enabled");
					game.SetStringGameState(Receipt, "intent");
					Require(KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"),
						"the camp heart intent failed its exact readback");
				}
				Require(game != null && KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"),
					"the camp heart owner intent is absent or torn");
				bool complete;
				string result = KingdomCampHeartNativeChecks.Run(Verb, game, zone, targetRung,
					out complete);
				if (complete)
				{
					Require(ReferenceEquals(The.Game, game), "the camp heart report owner changed");
					game.SetStringGameState(Receipt, result);
					Require(KingdomScenarioDurableState.ProvesExactText(Receipt, result),
						"the camp heart report failed its exact readback");
				}
				Ok = true;
				return result;
			}
			catch (Exception error) { return KingdomCampHeartNativeChecks.Fail(error); }
		}

		/// <summary>Which rung ladder this run is sealed for, read off the sealed script itself.
		/// A script that is neither sealed form is refused exactly as before: no other script may
		/// drive these verbs, and the target rung is never an argument a caller chooses.</summary>
		private static int SealedTargetRung()
		{
			IList<string> script;
			Require(KingdomScenarioScript.TryRead(out script, out _) && script != null,
				"the exact sealed camp heart script is absent");
			if (SameScript(script, Rung2Script)) return 2;
			if (SameScript(script, Rung3Script)) return 3;
			Require(false, "the sealed camp heart script differs");
			return 0;
		}

		private static bool SameScript(IList<string> Script, string[] Sealed)
		{
			if (Script.Count != Sealed.Length) return false;
			for (int i = 0; i < Sealed.Length; i++)
				if (Script[i] != Sealed[i]) return false;
			return true;
		}

		private static bool Eligible(XRLGame Game, Zone Zone)
		{
			KingdomScenarioPlan plan;
			return Game != null && Zone != null
				&& ReferenceEquals(The.ZoneManager?.ActiveZone, Zone)
				&& MessageQueue.Enabled
				&& KingdomMaster.ConfiguredEnabled && KingdomGrowth.Enabled
				&& KingdomLodging.Enabled && KingdomSubsidence.Enabled
				&& !KingdomSurvey.HasBoundPass && !KingdomScenarioAdvance.Pending
				&& !(Game.GetSystem<KingdomSystem>()?.Founded ?? false)
				&& !KingdomNativeRegressionContext.HasQuickstartState(Game)
				&& !KingdomNativeRegressionContext.HasAnyState(Game, Receipt)
				&& KingdomSubsidenceNativeFixture.LastAttempt == null
				&& KingdomScenarioRealizer.TryBindStampedPlan(out plan, out _, out _)
				&& plan.Key == "founding-first-city"
				&& plan.AuthorityClass == KingdomScenarioFoundingStep.FoundingAuthority
				&& KingdomScenarioTransactionMarker.Observe(out _)
					== KingdomScenarioTransactionShape.None;
		}

		internal static void Require(bool Value, string Failure)
		{
			if (!Value) throw new InvalidOperationException(
				Failure ?? "native camp heart evidence refused");
		}
	}
}
