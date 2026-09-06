using System;
using System.Collections.Generic;

namespace ThousandAndFirst.Harness
{
	internal sealed class KingdomQuickstartBootRequest
	{
		internal const string Verb = "quickstart-boot";
		internal const string SaveVerb = "quickstart-save";
		internal readonly string ProfileKey;
		internal readonly bool Advisor;
		internal readonly bool Save;
		internal readonly string Command;

		private KingdomQuickstartBootRequest(string ProfileKey, bool Advisor, string Command)
		{
			this.ProfileKey = ProfileKey;
			this.Advisor = Advisor;
			this.Command = Command;
			Save = Command.StartsWith(SaveVerb + " ", StringComparison.Ordinal);
		}

		internal static bool TryParse(IList<string> Script, out KingdomQuickstartBootRequest Request)
		{
			Request = null;
			if (Script == null || Script.Count != 1 || Script[0] == null || Script[0].Length > 96)
				return false;
			string[] fields = Script[0].Split(new[] { ' ' }, StringSplitOptions.None);
			if (fields.Length != 3 || fields[0] != Verb && fields[0] != SaveVerb
				|| !KingdomQuickstartRules.TryProfile(fields[1], out _)
				|| fields[2] != "yes" && fields[2] != "no") return false;
			Request = new KingdomQuickstartBootRequest(fields[1], fields[2] == "yes", Script[0]);
			return true;
		}
	}
}
