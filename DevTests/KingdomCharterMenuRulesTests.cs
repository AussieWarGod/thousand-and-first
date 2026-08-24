#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomCharterMenuRulesTests
	{
		private static string Source(string Relative)
		{
			return TestMain.ReadRepositoryText(Relative);
		}

		private static string Between(string SourceText, string Start, string End)
		{
			int start = SourceText.IndexOf(Start, StringComparison.Ordinal);
			if (start < 0) throw new InvalidOperationException("Missing source marker: " + Start);
			int end = SourceText.IndexOf(End, start + Start.Length, StringComparison.Ordinal);
			if (end < 0) throw new InvalidOperationException("Missing source marker: " + End);
			return SourceText.Substring(start, end - start);
		}

		private static string Normalize(string Text)
		{
			return Regex.Replace(Text ?? "", @"\s+", " ").Trim();
		}

		private static int Occurrences(string Text, string Needle)
		{
			int count = 0;
			int at = 0;
			while ((at = Text.IndexOf(Needle, at, StringComparison.Ordinal)) >= 0)
			{
				count++;
				at += Needle.Length;
			}
			return count;
		}

		private static string Sha256(string Text)
		{
			using (SHA256 sha = SHA256.Create())
			{
				byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(Text ?? ""));
				return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
			}
		}

		private static string BraceBlockAt(string Text, int OpenBrace)
		{
			Assert.GreaterOrEqual(OpenBrace, 0, "opening brace absent");
			Assert.AreEqual('{', Text[OpenBrace], "block must start at an opening brace");
			int depth = 0;
			for (int i = OpenBrace; i < Text.Length; i++)
			{
				if (Text[i] == '{') depth++;
				else if (Text[i] == '}' && --depth == 0)
					return Text.Substring(OpenBrace, i - OpenBrace + 1);
			}
			throw new InvalidOperationException("Unclosed source block");
		}

		private static string Method(string SourceText, string Signature)
		{
			int start = SourceText.IndexOf(Signature, StringComparison.Ordinal);
			if (start < 0) throw new InvalidOperationException("Missing method: " + Signature);
			int open = SourceText.IndexOf('{', start + Signature.Length);
			if (open < 0) throw new InvalidOperationException("Missing method body: " + Signature);
			return SourceText.Substring(start, open - start) + BraceBlockAt(SourceText, open);
		}

		private static int BraceDepthAt(string Text, int Index)
		{
			int depth = 0;
			for (int i = 0; i < Index; i++)
			{
				if (Text[i] == '{') depth++;
				else if (Text[i] == '}') depth--;
			}
			return depth;
		}

		[Test]
		public void RootKeepsStatusFirstAndOffersSevenClearChapters()
		{
			KingdomCharterMenuRoute[] root = KingdomCharterMenuRules.RootEntries();
			Assert.AreEqual(KingdomCharterRouteKind.Action, root[0].Kind);
			Assert.AreEqual(KingdomCharterAction.Status, root[0].Action);

			int chapters = 0;
			HashSet<KingdomCharterChapter> unique = new HashSet<KingdomCharterChapter>();
			for (int i = 0; i < root.Length; i++)
			{
				if (root[i].Kind != KingdomCharterRouteKind.Chapter) continue;
				chapters++;
				Assert.IsTrue(unique.Add(root[i].Chapter), "chapter repeated: " + root[i].Chapter);
			}
			Assert.AreEqual(7, chapters);
			Assert.AreEqual(Enum.GetValues(typeof(KingdomCharterChapter)).Length, unique.Count);
		}

		[Test]
		public void EveryFormerFlatMenuActionAppearsExactlyOnce()
		{
			Dictionary<KingdomCharterAction, int> counts = new Dictionary<KingdomCharterAction, int>();
			CountActions(KingdomCharterMenuRules.RootEntries(), counts);
			foreach (KingdomCharterChapter chapter in Enum.GetValues(typeof(KingdomCharterChapter)))
			{
				CountActions(KingdomCharterMenuRules.ChapterEntries(chapter), counts);
			}

			Array actions = Enum.GetValues(typeof(KingdomCharterAction));
			Assert.AreEqual(35, actions.Length, "the routing contract must account for all original verbs");
			Assert.AreEqual(actions.Length, counts.Count);
			foreach (KingdomCharterAction action in actions)
			{
				Assert.IsTrue(counts.ContainsKey(action), "missing route: " + action);
				Assert.AreEqual(1, counts[action], "route must be unique: " + action);
			}
		}

		[Test]
		public void EveryPopupHasUniqueHotkeysAndNonblankRows()
		{
			AssertMenu(KingdomCharterMenuRules.RootEntries(), ExpectBack: false);
			foreach (KingdomCharterChapter chapter in Enum.GetValues(typeof(KingdomCharterChapter)))
			{
				AssertMenu(KingdomCharterMenuRules.ChapterEntries(chapter), ExpectBack: true);
			}
		}

		[Test]
		public void ChapterRowsAreActionsThenOneExplicitBack()
		{
			foreach (KingdomCharterChapter chapter in Enum.GetValues(typeof(KingdomCharterChapter)))
			{
				KingdomCharterMenuRoute[] routes = KingdomCharterMenuRules.ChapterEntries(chapter);
				Assert.Greater(routes.Length, 1, chapter.ToString());
				for (int i = 0; i < routes.Length - 1; i++)
				{
					Assert.AreEqual(KingdomCharterRouteKind.Action, routes[i].Kind,
						chapter + " row " + i);
				}
				Assert.AreEqual(KingdomCharterRouteKind.Back, routes[routes.Length - 1].Kind);
			}
		}

		[Test]
		public void ReturnedArraysCannotRewriteRoutingTable()
		{
			KingdomCharterMenuRoute[] root = KingdomCharterMenuRules.RootEntries();
			root[0] = KingdomCharterMenuRoute.Back();
			Assert.AreEqual(KingdomCharterAction.Status,
				KingdomCharterMenuRules.RootEntries()[0].Action);

			KingdomCharterMenuRoute[] chapter = KingdomCharterMenuRules.ChapterEntries(
				KingdomCharterChapter.PeopleAndBelief);
			chapter[0] = KingdomCharterMenuRoute.Back();
			Assert.AreEqual(KingdomCharterAction.HearPetition,
				KingdomCharterMenuRules.ChapterEntries(
					KingdomCharterChapter.PeopleAndBelief)[0].Action);
		}

		[TestCase(10L, 10L, 100L, "founded today")]
		[TestCase(10L, 109L, 100L, "founded today")]
		[TestCase(10L, 110L, 100L, "founded yesterday")]
		[TestCase(10L, 310L, 100L, "founded 3 days ago")]
		[TestCase(20L, 10L, 100L, "founding date needs inspection")]
		public void FoundedWhenUsesDaysNotEngineTicks(long founded, long now, long ticksPerDay,
			string expected)
		{
			Assert.AreEqual(expected, KingdomCharterMenuRules.FoundedWhen(founded, now, ticksPerDay));
		}

		[TestCase(0L, 10L, 100L, "not yet scheduled")]
		[TestCase(10L, 10L, 100L, "due now")]
		[TestCase(9L, 10L, 100L, "overdue by less than a day")]
		[TestCase(10L, 110L, 100L, "overdue by 1 day")]
		[TestCase(10L, 211L, 100L, "overdue by more than 2 days")]
		[TestCase(11L, 10L, 100L, "due within a day")]
		[TestCase(110L, 10L, 100L, "due in 1 day")]
		[TestCase(111L, 10L, 100L, "due in less than 2 days")]
		public void DueWhenKeepsScheduleTruthWithoutEngineTicks(long due, long now,
			long ticksPerDay, string expected)
		{
			Assert.AreEqual(expected, KingdomCharterMenuRules.DueWhen(due, now, ticksPerDay));
		}

		[Test]
		public void RunActionPinsEveryPreChapterHandlerAndArgumentExactlyOnce()
		{
			// Hardcoded from HEAD's 35-way flat switch. Do not derive this oracle from the
			// new enum or routing table: it exists to catch a self-consistent wrong rewire.
			Dictionary<string, string> expected = new Dictionary<string, string>
			{
				{ "HearPetition", "HearPetition(System);" },
				{ "Status", "Popup.Show(KingdomReports.Status(System, ParentObject?.CurrentZone));" },
				{ "Homecoming", "ShowHomecoming(System);" },
				{ "ChronicleAndDynasty", "OpenChronicleAndDynasty(System);" },
				{ "OutsiderChronicle", "Popup.Show(KingdomReports.Chronicle(System, Outsider: true));" },
				{ "Standings", "Popup.Show(KingdomReports.Standings(System));" },
				{ "SettlerRoll", "Popup.Show(KingdomReports.Roll(System));" },
				{ "StandingPolicy", "SetPolicy(System);" },
				{ "DesignateDistrict", "DesignateDistrict(System);" },
				{ "CommissionBuilding", "CommissionBuilding(System);" },
				{ "AnswerThreat", "AnswerThreat(System);" },
				{ "DedicateStores", "DedicateVessel(System);" },
				{ "StrikeTradeCharter", "StrikeTradeCharter(System);" },
				{ "SendManifest", "LoadManifest(System);" },
				{ "ShareMeal", "HoldSharedMeal(System);" },
				{ "CertifyMachine", "CertifyMachine(System);" },
				{ "SetWaterDetail", "SetWaterDetail(System);" },
				{ "ManagePlans", "ManagePlans(System);" },
				{ "AdoptBuilding", "AdoptBuilding(System);" },
				{ "ReleaseAdoption", "ReleaseBuilding(System);" },
				{ "ManageCreed", "ManageCreed(System);" },
				{ "KeepersKnowledge", "KingdomZoning.ShowKeepers(System);" },
				{ "WorksAndTrades", "KingdomYards.ShowWorksAndTrades(System);" },
				{ "NameBuilding", "KingdomDesign.RenameBuilding(System, ParentObject);" },
				{ "GroundWork", "GroundWork(System);" },
				{ "StrikeBuilding", "StrikeBuilding(System);" },
				{ "PostPrice", "KingdomBounty.OpenNotices(System, ParentObject);" },
				{ "ConvertPlot", "KingdomSocket.OpenConvert(System, ParentObject);" },
				{ "RedressBuilding", "KingdomSocket.OpenRedress(System, ParentObject);" },
				{ "ConsecrateShrine", "KingdomFaith.OpenConsecration(System, ParentObject);" },
				{ "ShareWater", "KingdomWaterRite.OpenRite(System, ParentObject);" },
				{ "ClaimGround", "ClaimGround(System);" },
				{ "CityBook", "Simulation.City.KingdomBookReport.Open(System);" },
				{ "TechMap", "Popup.Show(KingdomTechMap.Draw(System));" },
				{ "CityAsks", "Popup.Show(KingdomAsks.Board(System));" }
			};

			string source = Source(Path.Combine("Core", "KingdomCharterPart.cs"));
			string run = Between(source,
				"private bool RunAction(KingdomSystem System, KingdomCharterAction Action)",
				"private static void OpenChronicleAndDynasty");
			MatchCollection cases = Regex.Matches(run,
				@"case\s+KingdomCharterAction\.(\w+)\s*:\s*(.*?)\s*break\s*;",
				RegexOptions.Singleline);
			Assert.AreEqual(35, expected.Count);
			Assert.AreEqual(expected.Count, cases.Count, "RunAction case count");

			HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < cases.Count; i++)
			{
				string action = cases[i].Groups[1].Value;
				Assert.IsTrue(seen.Add(action), "duplicate RunAction case: " + action);
				Assert.IsTrue(expected.ContainsKey(action), "unexpected RunAction case: " + action);
				Assert.AreEqual(Normalize(expected[action]),
					Normalize(cases[i].Groups[2].Value), "wrong handler: " + action);
			}
			foreach (string action in expected.Keys)
				Assert.IsTrue(seen.Contains(action), "missing RunAction case: " + action);

			// Each full handler token occurs once in the whole method, not merely once among
			// regex captures. An extra call before/after the switch therefore cannot hide.
			string normalizedRun = Normalize(run);
			foreach (KeyValuePair<string, string> row in expected)
			{
				Assert.AreEqual(1, Occurrences(normalizedRun, Normalize(row.Value)),
					"whole-method handler total: " + row.Key);
			}

			int switchAt = run.IndexOf("switch (Action)", StringComparison.Ordinal);
			int switchOpen = run.IndexOf('{', switchAt);
			string switchBlock = BraceBlockAt(run, switchOpen);
			string switchInner = switchBlock.Substring(1, switchBlock.Length - 2);
			MatchCollection innerCases = Regex.Matches(switchInner,
				@"case\s+KingdomCharterAction\.(\w+)\s*:\s*(.*?)\s*break\s*;",
				RegexOptions.Singleline);
			for (int i = innerCases.Count - 1; i >= 0; i--)
				switchInner = switchInner.Remove(innerCases[i].Index, innerCases[i].Length);
			Assert.AreEqual("", Normalize(switchInner),
				"switch may contain only the 35 pinned case bodies");

			string skeleton = run.Remove(switchOpen, switchBlock.Length)
				.Insert(switchOpen, "{ CASES }");
			Assert.AreEqual(
				"private bool RunAction(KingdomSystem System, KingdomCharterAction Action) { KingdomGovernanceScope action = KingdomGovernanceScope.Begin(ParentObject); try { switch (Action) { CASES } } finally { action.Dispose(); } return action.Committed; }",
				Normalize(skeleton),
				"no executable dispatch may sit outside the pinned switch");
		}

		[Test]
		public void NavigationReturnsBeforeTheOnlyGovernanceAndEnergyPath()
		{
			string part = Source(Path.Combine("Core", "KingdomCharterPart.cs"));
			string governance = Source(Path.Combine("Core", "KingdomGovernance.cs"));
			string open = Method(part, "public void OpenMenu()");
			string chapter = Method(part,
				"private bool OpenChapter(KingdomSystem System, KingdomCharterChapter Chapter)");
			string run = Method(part,
				"private bool RunAction(KingdomSystem System, KingdomCharterAction Action)");

			// Complete, accepted routing methods. These are intentionally independent pins:
			// a pre-loop root return or pre-cancel energy call must change this contract.
			Assert.AreEqual(
				"66b166b909a55fabb139cfa0c043a6b0998d2e27528a672eff0c608ae0c195c9",
				Sha256(Normalize(open)), "complete OpenMenu routing contract");
			Assert.AreEqual(
				"e3cf2d328f86b0d9612667f54d9f136f6a6ed7bdeef8afa70abc3eaf3f512310",
				Sha256(Normalize(chapter)), "complete OpenChapter routing contract");

			Assert.AreEqual(1, Occurrences(part, "KingdomGovernanceScope.Begin("),
				"only RunAction may open governance/energy accounting in CharterPart");
			Assert.AreEqual(1, Occurrences(run, "KingdomGovernanceScope.Begin(ParentObject)"));
			Assert.AreEqual(0, Regex.Matches(part, @"\.\s*UseEnergy\s*\(").Count,
				"CharterPart must never directly charge energy; RunAction's scope owns charging");
			StringAssert.DoesNotContain("KingdomGovernanceScope.Begin(", open);
			StringAssert.DoesNotContain("KingdomGovernanceScope.Begin(", chapter);
			StringAssert.DoesNotContain("KingdomGovernanceScope.Commit(", open);
			StringAssert.DoesNotContain("KingdomGovernanceScope.Commit(", chapter);
			StringAssert.Contains("KingdomGovernanceScope.Begin(ParentObject)", run);
			StringAssert.Contains("action.Dispose();", run);

			Assert.Greater(Occurrences(part, "KingdomGovernanceScope.Commit("), 0,
				"Charter action handlers must retain their governed durable commits");
			Assert.AreEqual(0, Occurrences(run, "KingdomGovernanceScope.Commit("),
				"RunAction opens scope then dispatches; only handlers may mark it committed");
			string dispose = BraceBlockAt(governance,
				governance.IndexOf('{', governance.IndexOf("public void Dispose()",
					StringComparison.Ordinal)));
			MatchCollection committedGuards = Regex.Matches(dispose,
				@"if\s*\(\s*!Committed\s*\)\s*(\{[^{}]*\})",
				RegexOptions.Singleline);
			Assert.AreEqual(1, committedGuards.Count,
				"Dispose needs one exact top-level not-committed guard");
			Assert.AreEqual("if (!Committed) { return; }",
				Normalize(committedGuards[0].Value));
			int uncommitted = committedGuards[0].Index;
			int energy = dispose.IndexOf("Actor.UseEnergy(", StringComparison.Ordinal);
			Assert.AreEqual(1, BraceDepthAt(dispose, uncommitted),
				"not-committed return must dominate at Dispose top level");
			Assert.AreEqual(1, BraceDepthAt(dispose, energy),
				"energy call must remain at Dispose top level behind the guard");
			Assert.Greater(energy, uncommitted + committedGuards[0].Length,
				"the sole energy call remains behind a committed action scope");
			Assert.AreEqual(1, Occurrences(dispose, "Actor.UseEnergy("));
			Assert.AreEqual(1, Occurrences(governance, "Actor.UseEnergy("));

			MatchCollection finalies = Regex.Matches(run,
				@"finally\s*(\{[^{}]*\})", RegexOptions.Singleline);
			Assert.AreEqual(1, finalies.Count);
			Assert.AreEqual("{ action.Dispose(); }",
				Normalize(finalies[0].Groups[1].Value),
				"RunAction must dispose its scope in its exact finally block");
			Assert.AreEqual(1, Occurrences(run, "action.Dispose();"));

			string normalizedOpen = Normalize(open);
			StringAssert.Contains(
				"if (pick < 0 || pick >= routes.Length) { return; }",
				normalizedOpen, "root cancel returns without opening an action");
			StringAssert.Contains(
				"if (route.Kind == KingdomCharterRouteKind.Chapter) { if (OpenChapter(system, route.Chapter)) { return; } } else if (route.Kind == KingdomCharterRouteKind.Action && RunAction(system, route.Action))",
				normalizedOpen, "chapter navigation must not fall through to RunAction");
			Assert.AreEqual(1, Occurrences(open, "RunAction("));

			string normalizedChapter = Normalize(chapter);
			StringAssert.Contains(
				"if (pick < 0 || pick >= routes.Length || routes[pick].Kind == KingdomCharterRouteKind.Back) { return false; }",
				normalizedChapter, "cancel and explicit Back return before action dispatch");
			StringAssert.Contains(
				"if (routes[pick].Kind == KingdomCharterRouteKind.Action && RunAction(System, routes[pick].Action))",
				normalizedChapter, "only Action routes may reach RunAction");
			Assert.Less(chapter.IndexOf("KingdomCharterRouteKind.Back", StringComparison.Ordinal),
				chapter.IndexOf("RunAction(", StringComparison.Ordinal));
			Assert.AreEqual(1, Occurrences(chapter, "RunAction("));
		}

		[Test]
		public void DetailedTradeStatusPreservesWishOnlyDiagnosticBranch()
		{
			string reports = Source(Path.Combine("Core", "KingdomReports.cs"));
			string player = Between(reports,
				"public static string TradeStatus(KingdomSystem System, bool Detailed = false)",
				"private static string ManifestStatus");
			string diagnostic = Between(reports,
				"private static string TradeDiagnosticStatus(KingdomSystem System)",
				"private static void AppendBoundedTradeText");

			int branch = player.IndexOf("if (Detailed) return TradeDiagnosticStatus(System);",
				StringComparison.Ordinal);
			int humanBook = player.IndexOf("KingdomTradeBook book = System?.TradeBook;",
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(branch, 0);
			Assert.Greater(humanBook, branch, "Detailed must branch before player wording");
			Assert.AreEqual(1, Occurrences(player, "TradeDiagnosticStatus(System)"));

			// SHA is over complete whitespace-normalized diagnostic method copied from
			// HEAD's old Detailed branch. Deleting or changing any diagnostic row must fail.
			Assert.AreEqual(
				"8459fc2dcea3e4763dba5ceb2b8d1f475de2bc6f4a48cc15ef3980c85753906a",
				Sha256(Normalize(diagnostic)), "complete old Detailed diagnostic body");

			Regex detailedCaller = new Regex(
				@"KingdomReports\s*\.\s*TradeStatus\s*\(\s*[^,]+,\s*(?:Detailed\s*:\s*)?true\s*\)",
				RegexOptions.Singleline);
			int callers = 0;
			string callerPath = null;
			string root = TestMain.RepositoryRoot;
			foreach (string path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
			{
				if (path.IndexOf(Path.DirectorySeparatorChar + "DevTests"
					+ Path.DirectorySeparatorChar, StringComparison.Ordinal) >= 0) continue;
				int found = detailedCaller.Matches(File.ReadAllText(path)).Count;
				if (found > 0) callerPath = path;
				callers += found;
			}
			Assert.AreEqual(1, callers,
				"production must have exactly one true Detailed TradeStatus caller");
			Assert.AreEqual(Path.Combine(root, "Debug", "KingdomWishes.cs"), callerPath);
		}

		private static void CountActions(KingdomCharterMenuRoute[] Routes,
			Dictionary<KingdomCharterAction, int> Counts)
		{
			for (int i = 0; i < Routes.Length; i++)
			{
				if (Routes[i].Kind != KingdomCharterRouteKind.Action) continue;
				int count;
				Counts.TryGetValue(Routes[i].Action, out count);
				Counts[Routes[i].Action] = count + 1;
			}
		}

		private static void AssertMenu(KingdomCharterMenuRoute[] Routes, bool ExpectBack)
		{
			HashSet<char> hotkeys = new HashSet<char>();
			int backs = 0;
			for (int i = 0; i < Routes.Length; i++)
			{
				Assert.IsFalse(string.IsNullOrWhiteSpace(Routes[i].Label), "blank label at " + i);
				Assert.IsTrue(hotkeys.Add(char.ToLowerInvariant(Routes[i].Hotkey)),
					"duplicate hotkey " + Routes[i].Hotkey);
				if (Routes[i].Kind == KingdomCharterRouteKind.Back) backs++;
			}
			Assert.AreEqual(ExpectBack ? 1 : 0, backs);
		}
	}
}
#endif
