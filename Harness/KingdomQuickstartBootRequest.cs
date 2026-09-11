using System;
using System.Collections.Generic;

namespace ThousandAndFirst.Harness
{
	internal sealed class KingdomQuickstartBootRequest
	{
		internal const string Verb = "quickstart-boot";
		internal const string SaveVerb = "quickstart-save";
		// Sibling of Verb/SaveVerb: same genuine boot, plus a separate post-boot build phase
		// (Harness/KingdomQuickstartBootTest.cs). Never changes boot-only behaviour or rows.
		internal const string BuildVerb = "quickstart-build";
		/// <summary>
		/// The lifecycle variant's own authority marker. It is a DIFFERENT verb from boot, save
		/// and build precisely so that nothing about those three changes: only a script whose
		/// first line names this verb may carry further auto-runner lines after it, and only such
		/// a run is allowed to carry a scenario auto-runner at all. Every old profile keeps the
		/// exact single-line grammar and the runner exclusion it always had.
		/// </summary>
		internal const string LifecycleVerb = "quickstart-lifecycle";
		internal readonly string ProfileKey;
		internal readonly bool Advisor;
		internal readonly bool Save;
		internal readonly bool Build;
		internal readonly bool Lifecycle;
		internal readonly string Command;

		private KingdomQuickstartBootRequest(string ProfileKey, bool Advisor, string Command)
		{
			this.ProfileKey = ProfileKey;
			this.Advisor = Advisor;
			this.Command = Command;
			Save = Command.StartsWith(SaveVerb + " ", StringComparison.Ordinal);
			Build = Command.StartsWith(BuildVerb + " ", StringComparison.Ordinal);
			Lifecycle = Command.StartsWith(LifecycleVerb + " ", StringComparison.Ordinal);
		}

		internal static bool TryParse(IList<string> Script, out KingdomQuickstartBootRequest Request)
		{
			Request = null;
			if (Script == null || Script.Count < 1 || Script[0] == null || Script[0].Length > 96)
				return false;
			bool lifecycle = Script[0].StartsWith(LifecycleVerb + " ", StringComparison.Ordinal);
			// Boot, save and build remain exactly one line, as they always were. Only the
			// lifecycle variant may be followed by further lines, and those are the auto-runner's
			// own verbs, sealed and validated by Tools/scenario_profile.py before the run starts.
			if (!lifecycle && Script.Count != 1) return false;
			string[] fields = Script[0].Split(new[] { ' ' }, StringSplitOptions.None);
			if (fields.Length != 3 || fields[0] != Verb && fields[0] != SaveVerb && fields[0] != BuildVerb
					&& fields[0] != LifecycleVerb
				|| !KingdomQuickstartRules.TryProfile(fields[1], out _)
				|| fields[2] != "yes" && fields[2] != "no") return false;
			Request = new KingdomQuickstartBootRequest(fields[1], fields[2] == "yes", Script[0]);
			return true;
		}
	}
}
