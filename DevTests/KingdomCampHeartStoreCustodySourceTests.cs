#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Scanner = ThousandAndFirst.Tests.KingdomCampHeartBoundPassSourceTests;

namespace ThousandAndFirst.Tests
{
	/// <summary>Store-custody contract for the camp-heart native frame. <c>RequireSameBodies</c>
	/// proves each retained body is the same object AND that the camp store holds it, so the list it
	/// searches must be the store's own <c>ContentUnits</c> bodies, read in the same method. Residents
	/// stand on city ground and must be compared by reference without that custody proof. This is a
	/// source scan: it cannot execute the loaded continuation, and the sealed native personas still
	/// own the behaviour. It stops the defect that refused every higher-heart loaded continuation with
	/// <c>taf-camp-store-foreign-holder</c> on its first original resident.</summary>
	public class KingdomCampHeartStoreCustodySourceTests
	{
		private static readonly Regex SameBodies = new Regex(@"\bRequireSameBodies\s*\(");

		[Test]
		public void StoreCustodyComparisonsOnlySearchThisMethodsCampStoreBodies()
		{
			int calls = 0, files = 0;
			var faults = new List<string>();
			foreach (string path in Directory.GetFiles(Path.Combine(TestMain.RepositoryRoot, "Harness"), "*.cs"))
			{
				string text = File.ReadAllText(path);
				if (!text.Contains("partial class KingdomCampHeartNativeChecks")) continue;
				files++;
				faults.AddRange(Faults(Path.GetFileName(path), text, ref calls));
			}
			Assert.That(files, Is.GreaterThanOrEqualTo(30), "camp-heart frame partials found");
			Assert.That(calls, Is.GreaterThanOrEqualTo(6), "RequireSameBodies call sites analysed");
			Assert.That(faults, Is.Empty);
		}

		[Test]
		public void TheLoadedContinuationComparesOriginalResidentsByIdentityOnly()
		{
			// Source pins only; the cold-load persona owns the behaviour.
			string preserved = Body(TestMain.ReadRepositoryText("Harness/KingdomCampHeartChainLoadChecks.cs"),
				"RequireLoadedChainPreserved");
			Assert.That(preserved, Does.Contain("foreach (var original in Original.ChainResidents)"));
			Assert.That(preserved, Does.Contain("Require(ChainResidents.Contains(original),"));
			Assert.That(preserved, Does.Not.Contain("RequireSameBodies(Original.ChainResidents"));
			string residents = Body(TestMain.ReadRepositoryText("Harness/KingdomCampHeartChainSaveWitness.cs"),
				"CaptureChainResidents");
			Assert.That(residents, Does.Contain("foreach (var body in ChainResidentBodies(Survey))"));
			Assert.That(residents, Does.Contain("ExactGround(Zone, body);"),
				"every captured resident body is proved to stand on this exact ground");
		}

		[Test]
		public void TheCustodyScanRejectsGroundBodiesAndIgnoresCommentsAndText()
		{
			const string source = "internal static partial class KingdomCampHeartNativeChecks\n{\n"
				+ "\tprivate void Good()\n\t{\n\t\tvar units = ContentUnits(out var bodies);\n"
				+ "\t\tRequireSameBodies(Kept, bodies, Name(\"a, b\"));\n"
				+ "\t\t// RequireSameBodies(Original.Residents, Residents, \"ground\");\n"
				+ "\t\tLog(\"RequireSameBodies(Residents, Residents, x)\");\n\t}\n"
				+ "\tinternal static string Loaded(Frame frame)\n\t{\n\t\tList<GameObject> brush;\n"
				+ "\t\tframe.ContentUnits(out brush);\n\t\tframe.RequireSameBodies(Kept, brush, \"loaded\");\n"
				+ "\t\treturn null;\n\t}\n"
				+ "\tprivate void Ground()\n\t{\n\t\tRequireSameBodies(Original.Residents, Residents, \"residents\");\n\t}\n"
				+ "\tprivate void Elsewhere() => RequireSameBodies(Kept, bodies, \"other method\");\n}\n";
			int calls = 0;
			var faults = Faults("synthetic.cs", source, ref calls);
			Assert.That(calls, Is.EqualTo(4), "commented and quoted calls are not calls");
			Assert.That(faults.Count, Is.EqualTo(2), string.Join("\n", faults));
			Assert.That(faults[0], Does.StartWith("synthetic.cs: Ground passes 'Residents'"));
			Assert.That(faults[1], Does.StartWith("synthetic.cs: Elsewhere passes 'bodies'"));
		}

		private static List<string> Faults(string File, string Source, ref int Calls)
		{
			var faults = new List<string>();
			var seen = new HashSet<int>();
			string text = Scanner.Clean(Source);
			foreach (Match method in Scanner.Declaration.Matches(text))
			{
				int at = BodyStart(text, method);
				if (at < 0) continue;
				string body = Scanner.Statement(text, at);
				foreach (Match call in SameBodies.Matches(body))
				{
					// A nested declaration repeats its text inside the outer body; count each call once.
					if (!seen.Add(at + call.Index)) continue;
					Calls++;
					int open = call.Index + call.Length - 1;
					var args = Arguments(body.Substring(open + 1, Scanner.Close(body, open, '(', ')') - open - 1));
					string present = args.Count == 3 ? args[1] : "";
					bool store = Regex.IsMatch(present, @"\A\w+\z") && Regex.IsMatch(body,
						@"\bContentUnits\s*\(\s*out\s+(?:[\w\.]+(?:<[\w\.,\s]*>)?\s+)?" + present + @"\s*\)");
					if (!store) faults.Add(File + ": " + method.Groups[1].Value + " passes '" + present
						+ "' to RequireSameBodies; only this method's ContentUnits bodies are store-held");
				}
			}
			return faults;
		}

		// The cleaned body: comments dropped and literal contents blanked, so a pin matches code only.
		private static string Body(string Source, string Name)
		{
			string text = Scanner.Clean(Source);
			foreach (Match method in Scanner.Declaration.Matches(text))
			{
				if (method.Groups[1].Value != Name) continue;
				int at = BodyStart(text, method);
				if (at >= 0) return Scanner.Statement(text, at);
			}
			throw new InvalidOperationException(Name + " has no body");
		}

		private static int BodyStart(string Text, Match Method)
		{
			int close = Scanner.Close(Text, Method.Index + Method.Length - 1, '(', ')');
			int at = Scanner.Skip(Text, close + 1);
			if (Text[at] == '{') return at;
			return string.CompareOrdinal(Text, at, "=>", 0, 2) == 0 ? Scanner.Skip(Text, at + 2) : -1;
		}

		private static List<string> Arguments(string Inner)
		{
			var args = new List<string>();
			int depth = 0, from = 0;
			for (int i = 0; i < Inner.Length; i++)
			{
				char c = Inner[i];
				if (c == '(' || c == '[' || c == '{') depth++;
				else if (c == ')' || c == ']' || c == '}') depth--;
				else if (c == ',' && depth == 0)
				{
					args.Add(Inner.Substring(from, i - from).Trim());
					from = i + 1;
				}
			}
			args.Add(Inner.Substring(from).Trim());
			return args;
		}
	}
}
#endif
