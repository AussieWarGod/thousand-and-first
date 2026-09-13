using System;
using System.Collections.Generic;
using XRL;

namespace ThousandAndFirst.Harness
{
	/// <summary>Exact guest recruitment/construction/save chain; no world repair or seeded residents.</summary>
	[KingdomScenarioVerbProvider]
	public sealed class KingdomGuestSaveNativeProvider : IKingdomScenarioVerbProvider
	{
		internal const string Verb = "guest-save-witness";
		private static readonly string[] Script = {
			"quickstart-lifecycle marsh yes", "lifecycle-open", "guest-actions-quickstart", "advance 8400",
			"guest-actions-check", "advance 8400", "resourcedigest", "lifecycle-grown", Verb,
			"lifecycle-save", "stagedigest" };
		public int ScenarioVerbApiVersion => KingdomScenarioVerbApi.Version;
		public IEnumerable<string> ScenarioVerbs => new[] { Verb };
		internal static bool ClaimsScript() => KingdomScenarioScript.TryRead(out IList<string> script, out _)
			&& script.Contains(Verb);
		internal static bool IsExact(IList<string> script)
		{
			if (script == null || script.Count != Script.Length) return false;
			for (int i = 0; i < Script.Length; i++) if (script[i] != Script[i]) return false;
			return true;
		}
		internal static void RequireScript()
		{
			KingdomGuestActionsNativeProvider.Require(KingdomScenarioScript.TryRead(out IList<string> script, out _)
				&& IsExact(script), "guest save script is absent or differs");
		}
		public string RunScenarioVerb(string verb, string argument, out bool ok)
		{
			ok = false;
			try
			{
				KingdomGuestActionsNativeProvider.Require(verb == Verb && string.IsNullOrEmpty(argument)
					&& The.Game != null && The.Player?.CurrentZone != null && !KingdomSurvey.HasBoundPass
					&& !KingdomScenarioAdvance.Pending, "guest save context differs");
				RequireScript();
				string result = KingdomGuestSaveWitness.Record(The.Game);
				ok = true; return result;
			}
			catch (Exception error)
			{
				return "native-guest-save failed; " + KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message);
			}
		}
	}
}
