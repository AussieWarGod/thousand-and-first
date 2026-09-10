using System.Text.RegularExpressions;

namespace ThousandAndFirst.Harness
{
	/// <summary>Normalizes verb-provider refusal messages to one stable taf-* reason code.
	/// A caught exception's message may already carry a stable taf-*-refused prefix from a
	/// lower Require/witness; re-prefixing it with the catching site's own code would nest
	/// prefixes and defeat stable-substring matching. Passing an already-prefixed message
	/// through unchanged keeps exactly one taf-* code on every refusal.</summary>
	internal static class KingdomScenarioRefusal
	{
		private static readonly Regex Prefixed = new Regex(@"^taf-[a-z0-9-]+-refused:", RegexOptions.Compiled);

		internal static string Message(string SitePrefix, string Text)
			=> Text != null && Prefixed.IsMatch(Text) ? Text : SitePrefix + ": " + Text;
	}
}
