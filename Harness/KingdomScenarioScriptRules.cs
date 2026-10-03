using System.Collections.Generic;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Pure line-to-verb parsing for the sealed scenario script. <c>KingdomScenarioScript</c> finds
	/// and reads the file; this class alone decides what its lines mean and how many there may be.
	/// Engine-free, so the bound executes in both public test projects instead of being vouched for
	/// by a string.
	/// <para>
	/// ONE VERB PER LINE. Each line is trimmed. A blank line, or one whose first character is
	/// <see cref="Comment"/>, is not a verb; every other line is exactly one verb, passed through
	/// verbatim, so <c>advance 1200</c> is ONE verb. <c>Tools/scenario_profile.py</c> folds those two
	/// shell words into that single line before it seals the file.
	/// </para>
	/// <para>
	/// MIRRORED OFFLINE. A sealed profile cannot be retried, so the tools that build one count
	/// exactly as <see cref="TryParse"/> counts and refuse past <see cref="MaxVerbs"/>:
	/// <c>Tools/scenario_profile.py</c> over the text it seals, <c>Tools/personas/persona_matrix.py</c>
	/// over a persona's steps. Both restate the bound as <c>MAX_SCRIPT_VERBS</c>, and
	/// KingdomScenarioScriptRulesTests pins all three to one value. Before that mirror existed the
	/// heart's rung-5 persona passed every offline check and was refused here, before its first
	/// verb ran (#264).
	/// </para>
	/// </summary>
	internal static class KingdomScenarioScriptRules
	{
		/// <summary>Comment marker, so a sealed script can explain itself to the next reader.</summary>
		internal const char Comment = '#';

		/// <summary>
		/// A script is a short list of harness verbs, not a program: refusing an over-long one
		/// outright is cheaper than discovering mid-run that a profile was sealed around something
		/// nobody meant to run. Raised from 32 to 48 for the heart's fifth rung (#264), whose
		/// persona climbs the four rungs below it inside the same script: 35 verbs. Still far inside
		/// the file bound (<c>KingdomScenarioScript.MaxFileBytes</c>, 65536 bytes): 48 lines of
		/// <see cref="MaxVerbChars"/> are under 44 KB even at three UTF-8 bytes a character.
		/// </summary>
		internal const int MaxVerbs = 48;

		internal const int MaxVerbChars = KingdomScenarioRules.MaxTextChars;

		/// <summary>
		/// Blank lines and comments are dropped; everything else is a verb, passed through verbatim
		/// so the script and the wish always mean the same thing. This never decides whether a verb
		/// EXISTS - the shared entry owns the closed verb set, and duplicating it here would give a
		/// script two places to disagree with the wish. Fail-closed: an over-long line, a verb past
		/// <see cref="MaxVerbs"/>, or no verb at all refuses by name rather than running a partial
		/// script.
		/// </summary>
		internal static bool TryParse(IList<string> Lines, out IList<string> Verbs,
			out string Failure)
		{
			Verbs = null;
			Failure = null;
			List<string> found = new List<string>();
			for (int i = 0; Lines != null && i < Lines.Count; i++)
			{
				string line = (Lines[i] ?? "").Trim();
				if (line.Length == 0 || line[0] == Comment) continue;
				if (line.Length > MaxVerbChars)
					return Refuse("script line " + (i + 1) + " is " + line.Length
						+ " characters, over the " + MaxVerbChars + "-character bound",
						out Failure);
				if (found.Count == MaxVerbs)
					return Refuse("the script declares more than " + MaxVerbs + " verbs",
						out Failure);
				found.Add(line);
			}
			if (found.Count == 0)
				return Refuse("the script file declares no verbs", out Failure);
			Verbs = found;
			return true;
		}

		private static bool Refuse(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}
	}
}
