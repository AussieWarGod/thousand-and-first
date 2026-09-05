using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	/// <summary>Two dev-only verbs, one disposable founded profile each: the happy-path launch
	/// (CASE A) and the second-mint different-blueprint replacement (CASE B1). Both drive the real
	/// activation entry and assert outside engine dispatch. Shape mirrors
	/// Harness/KingdomRaidOutboxNativeProvider.cs; eligibility mirrors
	/// Harness/KingdomSubsidenceNativeProvider.cs:45-78, which likewise demands an UNFOUNDED
	/// profile because the fixture does the founding itself.
	/// <para>Harness/ObjectBlueprints.xml is a DEV-ONLY overlay copied into a throwaway scenario
	/// profile by Tools/prepare-scenario.sh and sealed with it; excluded from Tools/stage.sh, the
	/// shipped manifest, and the Workshop package. It merges the native raid-mint probe onto the
	/// two shipped Snapjaws Steading raider blueprints named by
	/// RuntimeData/KingdomRaidProfiles.xml:12 - the SPAWNABLE numbered variants, because the
	/// un-numbered archetypes carry BaseObject and EligibleBlueprint refuses them.
	/// Nothing else.</para></summary>
	[KingdomScenarioVerbProvider]
	public sealed class KingdomRaidLaunchNativeProvider : IKingdomScenarioVerbProvider
	{
		internal const string VerbA = "raid-launch-native-a";
		internal const string VerbB1 = "raid-launch-native-b1";
		internal const string ReceiptA = "r_TAF_ScenarioRaidLaunchNativeA_v1";
		internal const string ReceiptB1 = "r_TAF_ScenarioRaidLaunchNativeB1_v1";
		internal const int ExpectedCases = 1;
		/// <summary>Every faction key RuntimeData/KingdomRaidProfiles.xml ships. A band naming a
		/// BaseObject archetype or an ExcludeFromDynamicEncounters body fails EligibleBlueprint
		/// (Raids/KingdomRaidProfiles.cs:241-252) and HandleProfile then drops the WHOLE profile
		/// (:182), so an unspawnable band is silent unless every key is demanded together.</summary>
		internal static readonly string[] ShippedFactions =
			new[] { "Snapjaws", "Baboons", "Goatfolk", "Cannibals", "Issachari" };
		public int ScenarioVerbApiVersion { get { return KingdomScenarioVerbApi.Version; } }
		public IEnumerable<string> ScenarioVerbs { get { return new[] { VerbA, VerbB1 }; } }

		public string RunScenarioVerb(string Verb, string Argument, out bool Ok)
		{
			Ok = false;
			bool quarantine = Verb == VerbB1;
			if ((!quarantine && Verb != VerbA) || !string.IsNullOrEmpty(Argument))
				return "raid-launch-native-a and raid-launch-native-b1 take no arguments";
			string receipt = quarantine ? ReceiptB1 : ReceiptA;
			XRLGame game = The.Game;
			Zone zone = The.Player?.CurrentZone;
			string failure;
			if (!Eligible(game, zone, Verb, out failure)) return "native raid launch refused: " + failure;
			game.SetStringGameState(receipt, "intent");
			if (!KingdomScenarioDurableState.ProvesExactText(receipt, "intent"))
				return "native raid launch refused: test intent did not persist exactly";
			string report;
			// ResetProbe is permitted HERE and nowhere else: at verb entry, and only over a probe that
			// is provably empty. Another case's retained evidence is never cleared to make room -
			// the verb refuses instead.
			if (!r_TAF_RaidMintProbe.Vacant || KingdomRaidLaunchNativeFixture.LastAttempt != null)
				return "native raid launch refused: retained native raid evidence must not be cleared";
			r_TAF_RaidMintProbe.ResetProbe();
			r_TAF_RaidMintProbe.Arm(
				quarantine ? KingdomRaidLaunchNativeChecks.SubstituteAtSequence : 0,
				quarantine ? KingdomRaidLaunchNativeChecks.SubstituteBlueprint : null);
			try
			{
				report = KingdomRaidLaunchNativeChecks.Run(game, zone, quarantine, out Ok);
			}
			finally
			{
				// The ONLY cleanup this harness performs, and it is a DISARM: the probe ledger, its
				// Book reference, every body, substitute and abandoned original stay retained and
				// strongly reachable after the report. Nothing is cleared, dropped or removed.
				r_TAF_RaidMintProbe.Armed = false;
			}
			if (!KingdomScenarioDurableState.ProvesExactText(receipt, "intent"))
			{
				Ok = false;
				return report + "\nNative intent receipt changed; existing evidence retained.";
			}
			game.SetStringGameState(receipt, report);
			if (!KingdomScenarioDurableState.ProvesExactText(receipt, report))
			{
				Ok = false;
				return report + "\nNative result receipt did not persist exactly.";
			}
			return report;
		}

		/// <summary>Journal row and native gate for the installed raid table: asks the REAL
		/// KingdomRaidProfiles.TryGet surface (Raids/KingdomRaidProfiles.cs:57-62) for every
		/// shipped key, so a refused band is installed-eligibility evidence rather than a
		/// source-text claim. Emits <c>profiles-loaded=&lt;n&gt;/5 &lt;keys&gt; missing=&lt;keys&gt;</c>.
		/// </summary>
		internal static string ProfilesLoaded(out int Loaded, out string Missing)
		{
			int loaded = 0;
			StringBuilder keys = new StringBuilder();
			StringBuilder gone = new StringBuilder();
			for (int i = 0; i < ShippedFactions.Length; i++)
			{
				KingdomRaidProfile profile;
				bool got = KingdomRaidProfiles.TryGet(ShippedFactions[i], out profile)
					&& profile != null;
				StringBuilder into = got ? keys : gone;
				if (got) loaded++;
				if (into.Length > 0) into.Append(',');
				into.Append(ShippedFactions[i]);
			}
			Loaded = loaded;
			Missing = gone.Length == 0 ? "-" : gone.ToString();
			return "profiles-loaded=" + loaded + "/" + ShippedFactions.Length + " "
				+ (keys.Length == 0 ? "-" : keys.ToString()) + " missing=" + Missing;
		}

		private static bool Eligible(XRLGame Game, Zone Zone, string Verb, out string Failure)
		{
			Failure = "requires a fresh unfounded marsh profile with raids and native messages enabled";
			if (Game == null || Zone == null || The.ZoneManager == null
				|| !ReferenceEquals(The.ZoneManager.ActiveZone, Zone) || !MessageQueue.Enabled
				|| !KingdomRaids.Enabled || !KingdomMaster.ConfiguredEnabled
				|| (Game.GetSystem<KingdomSystem>()?.Founded ?? false)
				|| KingdomNativeRegressionContext.HasQuickstartState(Game)
				|| KingdomNativeRegressionContext.HasAnyState(Game, ReceiptA)
				|| KingdomNativeRegressionContext.HasAnyState(Game, ReceiptB1)) return false;
			KingdomScenarioPlan plan;
			KingdomScenarioProvenance stamp;
			if (!KingdomScenarioRealizer.TryBindStampedPlan(out plan, out stamp, out Failure)) return false;
			IList<string> script;
			if (plan.Key != "founding-first-city"
				|| !KingdomScenarioScript.TryRead(out script, out Failure) || script.Count != 3
				|| script[0] != "stagedigest" || script[1] != Verb || script[2] != "stagedigest")
			{
				Failure = "requires the exact fresh-profile script and stamped plan";
				return false;
			}
			string transaction;
			if (KingdomScenarioTransactionMarker.Observe(out transaction)
				!= KingdomScenarioTransactionShape.None)
			{
				Failure = "requires an unspent scenario transaction";
				return false;
			}
			if (KingdomRaidLaunchNativeFixture.LastAttempt != null)
			{
				Failure = "a prior native raid-launch fixture remains retained";
				return false;
			}
			KingdomQuickstartProfile profile;
			Failure = "requires the active stamped marsh zone";
			if (!KingdomQuickstartRules.TryProfile("marsh", out profile) || Zone.ZoneID != profile.ZoneId)
				return false;
			Failure = null;
			return true;
		}
	}
}
