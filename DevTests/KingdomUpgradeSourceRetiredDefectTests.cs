#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// #283 source contracts over the production handover (design section 7.2). These pin text;
	/// the behaviour is proved by the engine-free rules in KingdomUpgradeRulesHandoverTests and
	/// KingdomConstructionRulesReadmissionTests and natively by the tier-upgrade persona and the
	/// stuck-save heal route.
	/// </summary>
	[TestFixture]
	public sealed class KingdomUpgradeSourceRetiredDefectTests
	{
		private const string CarryMarksFile = "Growth/KingdomUpgrade.22.CarryMarks.cs";
		private const string ProofsFile = "Growth/KingdomUpgrade.21.HandoverProofs.cs";
		private const string HandOverFile = "Growth/KingdomUpgrade.20.HandOver.cs";
		private const string RemovalFile = "Growth/KingdomUpgrade.25.HandoverRemoval.cs";
		private const string PollFile = "Growth/KingdomUpgrade.06.r_KingdomImprovement.Poll.cs";
		private const string ReadmissionFile = "Growth/KingdomUpgrade.27.RetiredDefectReadmission.cs";

		private static string Read(string Path) => TestMain.ReadRepositoryText(Path);

		/// <summary>The body of the first method whose declaration contains
		/// <paramref name="Signature"/>, by brace count; empty when there is none.</summary>
		private static string Body(string Source, string Signature)
		{
			int start = Source.IndexOf(Signature, StringComparison.Ordinal);
			if (start < 0) return "";
			int open = Source.IndexOf('{', start);
			int depth = 0;
			for (int i = open; i < Source.Length; i++)
			{
				if (Source[i] == '{') depth++;
				else if (Source[i] == '}' && --depth == 0) return Source.Substring(start, i + 1 - start);
			}
			return "";
		}

		private static void AssertOrdered(string Source, params string[] Terms)
		{
			int offset = 0;
			foreach (string term in Terms)
			{
				int found = Source.IndexOf(term, offset, StringComparison.Ordinal);
				ClassicAssert.GreaterOrEqual(found, 0, "missing ordered source term: " + term);
				offset = found + term.Length;
			}
		}

		[Test]
		public void CarryMarksWritesTheSharedRuleAndCarriesYieldingInsideTry()
		{
			string carry = Body(Read(CarryMarksFile), "public static bool CarryMarks(");
			StringAssert.Contains("KingdomUpgradeRules.CarryFounderMarks(", carry);
			AssertOrdered(carry, "if (carried.Yielding)",
				"Successor.SetIntProperty(KingdomPlots.YieldingProperty, 1);",
				"try { Successor.RequirePart<r_KingdomYielding>(); }",
				"catch (System.Exception) { return false; }", "return true;");
			AssertOrdered(Read("Growth/KingdomUpgrade.24.HandoverContents.cs"),
				"bool marked = CarryMarks(Predecessor, Successor, SuccessorKey);",
				"if (!marked || !ExactCarriedMarks(Predecessor, Successor, SuccessorKey)");
		}

		[Test]
		public void ExactCarriedMarksChecksTheSharedRule()
		{
			string proof = Body(Read(ProofsFile), "private static bool ExactCarriedMarks(");
			StringAssert.Contains("KingdomUpgradeRules.FounderMarksSettled(ReadFounderMarks(Predecessor),", proof);
			StringAssert.DoesNotContain("YieldingProperty", proof);
		}

		/// <summary>Every property the founder-mark check reads has a writer in the authored
		/// handover lane: CarryMarks, the authored envelope reservation or the authored growth
		/// stamp. r_TAF_Yielding had none before #283.</summary>
		[Test]
		public void EveryMarkTheCheckReadsHasAnAuthoredLaneWriter()
		{
			string marks = Read(CarryMarksFile);
			string readers = Body(Read(ProofsFile), "private static bool ExactCarriedMarks(")
				+ Body(marks, "private static KingdomUpgradeRules.FounderMarks ReadFounderMarks(");
			string writers = Body(marks, "public static bool CarryMarks(")
				+ Body(marks, "public static void CarryMarks(")
				+ Read("Growth/KingdomPlot2.20b.AuthoredGrowthReservation.cs")
				+ Read("Growth/KingdomPlot2.21.GrowthRules.cs");
			var written = new HashSet<string>(StringComparer.Ordinal);
			foreach (Match match in Regex.Matches(writers, @"Set(?:Int|String)Property\(\s*([A-Za-z_][A-Za-z0-9_.]*)"))
				written.Add(Last(match.Groups[1].Value));
			int read = 0;
			foreach (Match match in Regex.Matches(readers, @"Get(?:Int|String)Property\(\s*([A-Za-z_][A-Za-z0-9_.]*)"))
			{
				read++;
				string name = Last(match.Groups[1].Value);
				ClassicAssert.IsTrue(written.Contains(name), "no authored-lane writer for " + name);
			}
			ClassicAssert.GreaterOrEqual(read, 8, "the founder-mark readers were not found");
		}

		private static string Last(string Identifier)
		{
			int dot = Identifier.LastIndexOf('.');
			return dot < 0 ? Identifier : Identifier.Substring(dot + 1);
		}

		[Test]
		public void HandOverProvesTheLandedScaffoldFromDurableEvidence()
		{
			foreach (string path in new[] { HandOverFile, RemovalFile })
			{
				string text = Read(path);
				StringAssert.DoesNotContain("intent.Scaffold == null", text, path);
				StringAssert.DoesNotContain("intent.Scaffold.IDIfAssigned", text, path);
			}
			AssertOrdered(Read(HandOverFile), "bool landed = TryLandedScaffoldId(intent, Successor, out scaffoldId);",
				"|| !landed", "Successor, null,", "intent.SuccessorBlueprint, scaffoldId, ref job,",
				"ExactPendingRemovalProof(Successor, scaffoldId,",
				"SuccessorKey, intent, scaffoldId, ownerSystem, ref job,");
			string adapter = Read("Growth/KingdomUpgrade.20b.LandedScaffold.cs");
			StringAssert.Contains("KingdomUpgradeRules.LandedScaffoldIdentity(", adapter);
			StringAssert.Contains("r_KingdomScaffold.HasExactScaffoldRemovalIntent(Successor, id)", adapter);
			StringAssert.Contains("KingdomConstruction.FindGlobalLiveId(id, out _)", adapter);
			StringAssert.Contains("GameObject.Validate(ref Scaffold)", Read(PollFile));
		}

		[Test]
		public void PollHandoverAsksTheReadmissionBeforeHandOver()
		{
			string poll = Body(Read(PollFile), "public void PollHandover(");
			AssertOrdered(poll, "GameObject.Validate(ref Scaffold)", "KingdomConstruction.Bind(successor, job);",
				"KingdomUpgrade.TryReadmitRetiredHandoverDefect(ParentObject, successor,",
				"KingdomUpgrade.HandOver(ParentObject, successor, SuccessorKey);");
		}

		[Test]
		public void TheReadmissionJudgesFirstAndMarksBeforeItMovesTheJob()
		{
			string readmission = Read(ReadmissionFile);
			StringAssert.Contains("public const string ReadmittedProperty = \"r_TAF_ImprovementReadmitted\";",
				readmission);
			AssertOrdered(readmission, "KingdomUpgradeRules.ClassifyRetiredHandoverDefect(",
				"Predecessor.SetStringProperty(ReadmittedProperty, Job.Id);",
				"improvement.HandoverQuarantined = false;", "improvement.HandoverFailure = null;",
				"KingdomConstruction.Readmit(ref Job, defect)", "KingdomLog.Log(\"improvement readmitted: job=\"");
			Assert.That(Regex.IsMatch(readmission, "improvement readmitted:[^;]*(error|fault|exception|quarantin|inspection required)",
				RegexOptions.IgnoreCase), Is.False, "the log line must pass the strict Player.log check");
			AssertOrdered(Read("Growth/KingdomConstructionRules.Transitions.cs"),
				"private static bool ValidPhaseUpdate(", "if (IsRetiredDefectReadmission(Current, Next)) return true;",
				"switch (Current.Phase)");
		}

		/// <summary>Only the readmission calls KingdomConstruction.Readmit, and only
		/// KingdomConstruction.Readmit writes the readmission prefix.</summary>
		[Test]
		public void ReadmitIsCalledOnlyFromTheReadmission()
		{
			int callers = 0;
			foreach (KeyValuePair<string, string> source in ProductionSources())
			{
				if (source.Value.Contains("KingdomConstruction.Readmit("))
				{
					callers++;
					ClassicAssert.AreEqual(ReadmissionFile, source.Key);
				}
				if (source.Key != "Growth/KingdomConstructionRules.Readmission.cs")
					StringAssert.DoesNotContain("readmitted after retired handover defect", source.Value,
						source.Key);
			}
			ClassicAssert.AreEqual(1, callers);
		}

		private static IEnumerable<KeyValuePair<string, string>> ProductionSources()
		{
			string root = TestMain.RepositoryRoot;
			foreach (string directory in new[] { "Growth", "Core", "Simulation", "Integrations" })
			{
				string full = Path.Combine(root, directory);
				if (!Directory.Exists(full)) continue;
				foreach (string file in Directory.GetFiles(full, "*.cs", SearchOption.AllDirectories))
					yield return new KeyValuePair<string, string>(
						file.Substring(root.Length + 1).Replace('\\', '/'),
						File.ReadAllText(file).Replace("\r\n", "\n"));
			}
		}

		/// <summary>Signature B's endpoints text is retired: only builds before the #283 fix
		/// wrote it. No production file but the readmission rules names it, and those only declare
		/// it and recognize it.</summary>
		[Test]
		public void NoProductionCodeWritesSignatureBsText()
		{
			const string Rules = "Growth/KingdomConstructionRules.Readmission.cs";
			int scanned = 0;
			foreach (KeyValuePair<string, string> source in ProductionSources())
			{
				scanned++;
				if (source.Key == Rules) continue;
				StringAssert.DoesNotContain("HandoverEndpointsFailure", source.Value, source.Key);
				StringAssert.DoesNotContain(KingdomConstructionRules.HandoverEndpointsFailure,
					source.Value, source.Key);
			}
			ClassicAssert.Greater(scanned, 1000, "the production tree was not found");
			string code = Regex.Replace(Read(Rules), @"^\s*///.*$", "", RegexOptions.Multiline);
			ClassicAssert.AreEqual(2, Regex.Matches(code, @"\bHandoverEndpointsFailure\b").Count);
			StringAssert.Contains("public const string HandoverEndpointsFailure =", code);
			StringAssert.Contains(": Failure == HandoverEndpointsFailure ? KingdomRetiredHandoverDefect.LandedScaffold",
				code);
		}

		/// <summary>A quarantine the fixed HandOver writes is never readmitted. The failure its
		/// endpoint refusal publishes, read from the source, names no retired signature, so the
		/// classifier refuses it with every structural conjunct held and the registry gate refuses
		/// its readmission. Before, the refusal published signature B's own text, so a transient
		/// landed-scaffold refusal (the scaffold id still live somewhere, or past the global
		/// lookup bound) quarantined on this build was readmitted as defect B on a later pass.
		/// </summary>
		[Test]
		public void AQuarantineTheFixedHandOverWritesIsNeverReadmitted()
		{
			Match refusal = Regex.Match(Body(Read(HandOverFile), "public static void HandOver("),
				@"\|\| !landed[^{]*\{\s*FailExactHandover\(Predecessor, Successor, SuccessorKey,\s*"
				+ @"KingdomConstructionRules\.(\w+)\);");
			ClassicAssert.IsTrue(refusal.Success, "the endpoint refusal after the landed proof");
			FieldInfo field = typeof(KingdomConstructionRules).GetField(refusal.Groups[1].Value,
				BindingFlags.Public | BindingFlags.Static);
			ClassicAssert.IsNotNull(field, refusal.Groups[1].Value);
			string text = (string)field.GetRawConstantValue();
			ClassicAssert.AreEqual(KingdomRetiredHandoverDefect.None,
				KingdomConstructionRules.RetiredHandoverDefectFor(text), text);
			KingdomConstructionJob job = new KingdomConstructionJob
			{
				Id = "00000000000000000000000000000283", Route = KingdomConstructionRoute.Improvement,
				Phase = KingdomConstructionPhase.InspectionRequired, PhysicalPhase = KingdomPhysicalPhase.None,
				SubjectId = "pred-1", SourceId = "pred-1", OutputId = "succ-1", TargetKey = "tentrow",
				X = 29, Y = 9, Failure = text
			};
			KingdomUpgradeRules.RetiredHandoverObservation held = new KingdomUpgradeRules.RetiredHandoverObservation
			{
				Owned = true, Current = true, PredecessorReceipt = true, SuccessorReceipt = true,
				SuccessorPending = true, SuccessorExact = true, Working = true, PredecessorExact = true,
				ScaffoldLanded = true, ContentCustody = true
			};
			ClassicAssert.AreEqual(KingdomRetiredHandoverDefect.None, KingdomUpgradeRules
				.ClassifyRetiredHandoverDefect(job, "pred-1", 29, 9, "succ-1", "tentrow", held));
			foreach (KingdomRetiredHandoverDefect defect in new[] {
				KingdomRetiredHandoverDefect.FounderMarks, KingdomRetiredHandoverDefect.LandedScaffold })
			{
				string failure;
				ClassicAssert.IsFalse(KingdomConstructionRules.TryReadmissionFailure(job, defect, out failure));
				KingdomConstructionJob next = KingdomConstructionRules.Transition(job,
					KingdomConstructionPhase.Outstanding, 412800L,
					KingdomConstructionRules.ReadmissionPrefix(defect) + text);
				ClassicAssert.IsFalse(KingdomConstructionRules.IsRetiredDefectReadmission(job, next),
					defect.ToString());
			}
		}

		/// <summary>Signature A is retired by structure: CarryMarks writes the successor's yielding
		/// mark before anything can refuse, and no production code removes or lowers that mark, so
		/// a marks quarantine this build writes never shows A's yielding predecessor beside a
		/// successor that does not yield. The generated removal coverage (mod uninstall) is apart.
		/// </summary>
		[Test]
		public void NoProductionCodeRemovesTheYieldingMark()
		{
			int writers = 0;
			foreach (KeyValuePair<string, string> source in ProductionSources())
			{
				if (source.Key == "Core/KingdomRemovalCoverage.Generated.cs") continue;
				foreach (Match match in Regex.Matches(source.Value,
					@"\b(\w+)IntProperty\(\s*(?:KingdomPlots\.)?YieldingProperty\b\s*(,[^)]*)?\)"))
				{
					string verb = match.Groups[1].Value;
					if (verb == "Get" || verb == "Has") continue;
					ClassicAssert.AreEqual("Set", verb, source.Key + ": " + match.Value);
					ClassicAssert.AreEqual(", 1", match.Groups[2].Value, source.Key + ": " + match.Value);
					writers++;
				}
				if (source.Key != "Growth/KingdomPlot2.03.RegistryAndDeclarations.cs")
					StringAssert.DoesNotContain("\"r_TAF_Yielding\"", source.Value, source.Key);
			}
			ClassicAssert.GreaterOrEqual(writers, 4, "the yielding writers were not found");
		}
	}
}
#endif
