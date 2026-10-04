#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>Bound-pass contract for the camp-heart native frame. The unbound census
	/// (<c>KingdomSurvey.TryTakeUnboundRecovery</c>) refuses whenever any settlement pass is bound,
	/// and several frame helpers demand an unbound caller. Code that runs inside a bound pass - an
	/// <c>*InPass</c> method, or the statement a <c>using (scope)</c> or <c>using (... .BindPass())</c>
	/// governs - must never reach either, directly or through another frame method. This is a source
	/// scan of the frame's partial files: it cannot execute a pass, and the sealed native personas
	/// still own the behaviour. It stops the defect that refused every paid-heart completion check
	/// and every higher-heart capture with <c>taf-camp-heart-census-incomplete</c>.</summary>
	public class KingdomCampHeartBoundPassSourceTests
	{
		private static readonly string[] UnboundOnly = {
			"KingdomSurvey.TryTakeUnboundRecovery(", "!KingdomSurvey.HasBoundPass" };

		private static readonly HashSet<string> Keywords = new HashSet<string>(StringComparer.Ordinal) {
			"if", "for", "foreach", "while", "using", "switch", "catch", "lock", "return", "new",
			"typeof", "nameof", "sizeof", "default", "checked", "unchecked", "base", "this", "when" };

		private static readonly Regex Declaration = new Regex(@"^[ \t]*(?:(?:private|internal|public|"
			+ @"protected|static|override|sealed|async|new|virtual|abstract|extern|unsafe|partial)\s+)+"
			+ @"[\w\.]+(?:<[^;{}()=]*?>)?(?:\[\])?\??\s+(\w+)\s*\(", RegexOptions.Multiline);

		private static readonly Regex Call = new Regex(@"\b([A-Za-z_]\w*)\s*\(");

		private static readonly Regex Using = new Regex(@"\busing\s*\(");

		[Test]
		public void BoundPassesNeverReachTheUnboundCensusOrAnUnboundOnlyHelper()
		{
			var scan = new Scan();
			foreach (string path in Directory.GetFiles(Path.Combine(TestMain.RepositoryRoot, "Harness"), "*.cs"))
			{
				string text = File.ReadAllText(path);
				if (text.Contains("partial class KingdomCampHeartNativeChecks")) scan.Add(Path.GetFileName(path), text);
			}
			Assert.That(scan.Files, Is.GreaterThanOrEqualTo(30), "camp-heart frame partials found");
			Assert.That(scan.IsUnboundOnly("Census", true), Is.True, "Census() takes the unbound recovery");
			Assert.That(scan.IsUnboundOnly("StandingHeart", true), Is.True, "StandingHeart() reaches the census");
			Assert.That(scan.IsUnboundOnly("StandingHeart", false), Is.False, "the pass overload reads its own survey");
			foreach (string region in new[] { "CaptureChainInPass", "RequireChainSupportInPass", "AssessChainInPass" })
				Assert.That(scan.Regions.Any(found => found.Item2 == region), Is.True, region + " is a bound region");
			Assert.That(scan.Regions.Count(found => found.Item2.StartsWith("using (", StringComparison.Ordinal)),
				Is.GreaterThanOrEqualTo(6), "bound using statements are analysed");
			Assert.That(scan.Violations(), Is.Empty);
		}

		[Test]
		public void TheScanCatchesAnIndirectUnboundReadAndIgnoresTheOverloadTextAndComments()
		{
			const string source = "internal static partial class KingdomCampHeartNativeChecks\n{\n"
				+ "\tinternal KingdomSurvey Census()\n\t{\n\t\tRequire(KingdomSurvey.TryTakeUnboundRecovery(Zone, out var s), \"x\");\n"
				+ "\t\treturn s;\n\t}\n"
				+ "\tinternal GameObject StandingHeart() => HeartIn(Census());\n"
				+ "\tinternal GameObject StandingHeart(KingdomSurvey Survey) => HeartIn(Survey);\n"
				+ "\tprivate GameObject HeartIn(KingdomSurvey Survey) { return Survey.Built[0]; }\n"
				+ "\tprivate void Helper() { var heart = StandingHeart(); }\n"
				+ "\tprivate void Unbound() { Require(!KingdomSurvey.HasBoundPass, \"outside only\"); }\n"
				+ "\tprivate void ReadInPass(KingdomSurvey Survey) { Helper(); }\n"
				+ "\tprivate void Good()\n\t{\n\t\tusing (scope) Count(StandingHeart(KingdomSurvey.ActiveFor(Zone)));\n"
				+ "\t\tusing (scope) Log(\"StandingHeart() and Census()\"); // StandingHeart()\n"
				+ "\t\t/* Census() */ using (scope) Count(KingdomSurvey.ActiveFor(Zone));\n\t}\n"
				+ "\tprivate void Bad()\n\t{\n\t\tusing (scope)\n\t\t{\n\t\t\tUnbound();\n\t\t}\n"
				+ "\t\tusing (survey.BindPass()) Helper();\n\t}\n}\n";
			var scan = new Scan();
			scan.Add("synthetic.cs", source);
			var found = scan.Violations();
			Assert.That(found.Count, Is.EqualTo(3), string.Join("\n", found));
			Assert.That(found[0], Does.StartWith("synthetic.cs: ReadInPass -> Helper() -> StandingHeart() -> Census()"));
			Assert.That(found[1], Does.Contain("using (scope)").And.Contain("Unbound() -> !KingdomSurvey.HasBoundPass"));
			Assert.That(found[2], Does.Contain("using (survey.BindPass())").And.Contain("Helper() -> StandingHeart()"));
			Assert.That(scan.Regions.Count, Is.EqualTo(6), "one InPass body and five bound using statements");
		}

		private sealed class Scan
		{
			private readonly Dictionary<string, List<string>> Methods =
				new Dictionary<string, List<string>>(StringComparer.Ordinal);
			internal readonly List<Tuple<string, string, string>> Regions = new List<Tuple<string, string, string>>();
			internal int Files;

			internal void Add(string File, string Source)
			{
				Files++;
				string text = Clean(Source);
				foreach (Match match in Declaration.Matches(text))
				{
					string name = match.Groups[1].Value;
					int open = match.Index + match.Length - 1, close = Close(text, open, '(', ')');
					bool zero = text.Substring(open + 1, close - open - 1).Trim().Length == 0;
					int at = Skip(text, close + 1);
					string body;
					if (text[at] == '{') body = Statement(text, at);
					else if (string.CompareOrdinal(text, at, "=>", 0, 2) == 0) body = Statement(text, at + 2);
					else continue;
					string key = Key(name, zero);
					if (!Methods.TryGetValue(key, out var bodies)) Methods.Add(key, bodies = new List<string>());
					bodies.Add(body);
					if (name.EndsWith("InPass", StringComparison.Ordinal)) Regions.Add(Tuple.Create(File, name, body));
				}
				foreach (Match match in Using.Matches(text))
				{
					int open = match.Index + match.Length - 1, close = Close(text, open, '(', ')');
					string resource = text.Substring(open + 1, close - open - 1).Trim();
					if (Regex.IsMatch(resource, @"\A\w*[Ss]cope\z") || resource.Contains("BindPass("))
						Regions.Add(Tuple.Create(File, "using (" + resource + ") at " + open, Statement(text, close + 1)));
				}
			}

			internal bool IsUnboundOnly(string Name, bool Zero) => Reasons().ContainsKey(Key(Name, Zero));

			internal List<string> Violations()
			{
				var reasons = Reasons();
				var found = new List<string>();
				foreach (var region in Regions)
				{
					string reason = Reach(region.Item3, reasons);
					if (reason != null) found.Add(region.Item1 + ": " + region.Item2 + " -> " + reason);
				}
				return found;
			}

			// Fixpoint over the frame's own methods: a body is unbound-only when it carries a
			// marker or calls (by name and empty/non-empty arguments) another unbound-only body.
			private Dictionary<string, string> Reasons()
			{
				var reasons = new Dictionary<string, string>(StringComparer.Ordinal);
				for (bool changed = true; changed;)
				{
					changed = false;
					foreach (var method in Methods)
					{
						if (reasons.ContainsKey(method.Key)) continue;
						foreach (string body in method.Value)
						{
							string reason = Reach(body, reasons);
							if (reason == null) continue;
							reasons.Add(method.Key, reason);
							changed = true;
							break;
						}
					}
				}
				return reasons;
			}

			private static string Reach(string Body, Dictionary<string, string> Reasons)
			{
				foreach (string marker in UnboundOnly)
					if (Body.Contains(marker)) return marker.TrimEnd('(');
				foreach (Match call in Call.Matches(Body))
				{
					string name = call.Groups[1].Value;
					if (Keywords.Contains(name)) continue;
					bool zero = Body[Skip(Body, call.Index + call.Length)] == ')';
					if (Reasons.TryGetValue(Key(name, zero), out string reason))
						return name + (zero ? "()" : "(..)") + " -> " + reason;
				}
				return null;
			}

			private static string Key(string Name, bool Zero) => Name + (Zero ? "/0" : "/n");
		}

		// Comments are dropped and literal contents blanked, so neither a message naming a helper
		// nor a commented-out call counts as a call; braces and parentheses stay balanced.
		private static string Clean(string Source)
		{
			var text = new StringBuilder(Source.Length);
			int i = 0;
			while (i < Source.Length)
			{
				char c = Source[i], next = i + 1 < Source.Length ? Source[i + 1] : '\0';
				if (c == '/' && next == '/')
				{
					while (i < Source.Length && Source[i] != '\n') i++;
					continue;
				}
				if (c == '/' && next == '*')
				{
					int end = Source.IndexOf("*/", i + 2, StringComparison.Ordinal);
					i = end < 0 ? Source.Length : end + 2;
					text.Append(' ');
					continue;
				}
				if (c == '"' || c == '\'')
				{
					bool verbatim = c == '"' && ((i > 0 && Source[i - 1] == '@')
						|| (i > 1 && Source[i - 1] == '$' && Source[i - 2] == '@'));
					text.Append(c);
					for (i++; i < Source.Length; i++)
					{
						if (!verbatim && Source[i] == '\\') { i++; continue; }
						if (Source[i] != c) continue;
						if (verbatim && i + 1 < Source.Length && Source[i + 1] == '"') { i++; continue; }
						break;
					}
					text.Append(c);
					i++;
					continue;
				}
				text.Append(c);
				i++;
			}
			return text.ToString();
		}

		private static int Close(string Text, int Open, char Opening, char Closing)
		{
			int depth = 0;
			for (int i = Open; i < Text.Length; i++)
			{
				if (Text[i] == Opening) depth++;
				else if (Text[i] == Closing && --depth == 0) return i;
			}
			throw new InvalidOperationException("unbalanced " + Opening + " at " + Open);
		}

		private static int Skip(string Text, int At)
		{
			while (At < Text.Length && char.IsWhiteSpace(Text[At])) At++;
			return At;
		}

		private static string Statement(string Text, int At)
		{
			At = Skip(Text, At);
			if (Text[At] == '{') return Text.Substring(At, Close(Text, At, '{', '}') - At + 1);
			int depth = 0;
			for (int i = At; i < Text.Length; i++)
			{
				char c = Text[i];
				if (c == '(' || c == '{') depth++;
				else if (c == ')' || c == '}') depth--;
				else if (c == ';' && depth == 0) return Text.Substring(At, i - At + 1);
			}
			throw new InvalidOperationException("unterminated statement at " + At);
		}
	}
}
#endif
