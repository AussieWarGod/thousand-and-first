#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// #283 stuck-save heal route, engine-free halves, run by both suites: the exact session-one
	/// script the host grammar seals, the two retired failure texts the unfixed build writes, and
	/// the snapshot codec session two binds its verdict to. Native acceptance is the
	/// renovate-heal-reload persona's, not these.
	/// </summary>
	[TestFixture]
	public sealed class KingdomRenovateHealTests
	{
		private static string[] HostScript()
		{
			string matrix = TestMain.ReadRepositoryText("Tools/personas/persona_matrix.py");
			int start = matrix.IndexOf("RENOVATE_HEAL_RELOAD_SCRIPT = (", StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(start, 0, "the host grammar names no heal script");
			string tuple = matrix.Substring(start, matrix.IndexOf(")\n", start, StringComparison.Ordinal) - start);
			return Regex.Matches(tuple, "\"([^\"]+)\"").Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
		}

		[Test]
		public void TheHarnessAndTheHostSealTheSameHealScript()
		{
			string[] sealed_ = HostScript();
			ClassicAssert.AreEqual(9, sealed_.Length);
			ClassicAssert.IsTrue(KingdomRenovateHealScript.Matches(sealed_));
			ClassicAssert.IsFalse(KingdomRenovateHealScript.Matches(null));
			ClassicAssert.IsFalse(KingdomRenovateHealScript.Matches(sealed_.Concat(new[] { "stagedigest" }).ToArray()));
			for (int i = 0; i < sealed_.Length; i++)
			{
				var changed = (string[])sealed_.Clone();
				changed[i] += " ";
				ClassicAssert.IsFalse(KingdomRenovateHealScript.Matches(changed), "edit at " + i);
				ClassicAssert.IsFalse(KingdomRenovateHealScript.Matches(
					sealed_.Where((_, at) => at != i).ToArray()), "cut at " + i);
			}
			ClassicAssert.AreEqual(KingdomRenovateHealScript.SaveVerb, sealed_[sealed_.Length - 1]);
			StringAssert.Contains("SCRIPT=reload-descendant renovate-heal 8.22@40,12",
				TestMain.ReadRepositoryText("Tools/personas/renovate-heal-reload.persona"));
		}

		/// <summary>Session one's build predates the constants, so the harness spells the texts;
		/// they must be exactly the ones that build writes and the readmission keys on. The
		/// endpoints text is retired: only builds before the fix write it.</summary>
		[Test]
		public void TheSpelledFailureTextsAreTheProductionLiterals()
		{
			ClassicAssert.AreEqual(KingdomConstructionRules.HandoverMarksFailure, KingdomRenovateHealScript.MarksFailure);
			ClassicAssert.AreEqual(KingdomConstructionRules.HandoverEndpointsFailure,
				KingdomRenovateHealScript.EndpointsFailure);
			ClassicAssert.AreEqual("A", KingdomRenovateHealScript.DefectOf(KingdomConstructionRules.HandoverMarksFailure));
			ClassicAssert.AreEqual("B", KingdomRenovateHealScript.DefectOf(KingdomConstructionRules.HandoverEndpointsFailure));
			ClassicAssert.IsNull(KingdomRenovateHealScript.DefectOf(null));
			ClassicAssert.IsNull(KingdomRenovateHealScript.DefectOf("readmitted after retired handover defect #283 (A): "
				+ KingdomConstructionRules.HandoverMarksFailure));
		}

		private static KingdomRenovateHealSnapshot Stall(string GameId = "01234567-89ab-cdef-0123-456789abcdef",
			string Job = "0123456789abcdef0123456789abcdef", string Predecessor = "3001", string Successor = "3002",
			string Defect = "A", int Phase = 5, long Ticks = 414000L)
		{
			return new KingdomRenovateHealSnapshot(GameId, "JoppaWorld.8.22.1.1.10", Job, Predecessor, Successor,
				Defect, Phase, "3003", Ticks);
		}

		[Test]
		public void TheSnapshotRoundTripsExactly()
		{
			string wire;
			ClassicAssert.IsTrue(KingdomRenovateHealSnapshot.TryEncode(Stall(), out wire));
			ClassicAssert.AreEqual("taf-renovate-heal-v1:01234567-89ab-cdef-0123-456789abcdef;"
				+ "JoppaWorld.8.22.1.1.10;0123456789abcdef0123456789abcdef;3001;3002;A;5;3003;414000", wire);
			KingdomRenovateHealSnapshot back;
			ClassicAssert.IsTrue(KingdomRenovateHealSnapshot.TryDecode(wire, out back));
			ClassicAssert.AreEqual(("3001", "3002", "A", 5, 414000L, "3003"),
				(back.PredecessorId, back.SuccessorId, back.Defect, back.UpgradePhase, back.TimeTicks, back.ScaffoldIntentId));
		}

		[Test]
		public void AMalformedSnapshotNeverEncodes()
		{
			var refused = new List<KingdomRenovateHealSnapshot>
			{
				Stall(GameId: "01234567-89AB-cdef-0123-456789abcdef"), Stall(Job: "0123"), Stall(Defect: "C"),
				Stall(Phase: 6), Stall(Phase: -1), Stall(Ticks: -1L), Stall(Successor: "3001"),
				Stall(Predecessor: "30;01"), Stall(Successor: ""), null
			};
			foreach (KingdomRenovateHealSnapshot candidate in refused)
			{
				string wire;
				ClassicAssert.IsFalse(KingdomRenovateHealSnapshot.TryEncode(candidate, out wire));
				ClassicAssert.IsNull(wire);
			}
		}

		[TestCase("taf-renovate-heal-v2:01234567-89ab-cdef-0123-456789abcdef;Z;0123456789abcdef0123456789abcdef;1;2;A;5;3;4")]
		[TestCase("taf-renovate-heal-v1:01234567-89ab-cdef-0123-456789abcdef;Z;0123456789abcdef0123456789abcdef;1;2;A;5;3")]
		[TestCase("taf-renovate-heal-v1:01234567-89ab-cdef-0123-456789abcdef;Z;0123456789abcdef0123456789abcdef;1;2;A;05;3;4")]
		[TestCase("taf-renovate-heal-v1:01234567-89ab-cdef-0123-456789abcdef;Z;0123456789abcdef0123456789abcdef;1;2;A;5;3;4;5")]
		[TestCase("taf-renovate-heal-v1:01234567-89ab-cdef-0123-456789abcdef;Z;0123456789abcdef0123456789abcdef;1;2;A;5;3;+4")]
		[TestCase(" taf-renovate-heal-v1:01234567-89ab-cdef-0123-456789abcdef;Z;0123456789abcdef0123456789abcdef;1;2;A;5;3;4")]
		[TestCase("")]
		public void AMalformedWireNeverDecodes(string Wire)
		{
			KingdomRenovateHealSnapshot snapshot;
			ClassicAssert.IsFalse(KingdomRenovateHealSnapshot.TryDecode(Wire, out snapshot));
			ClassicAssert.IsNull(snapshot);
		}

		[Test]
		public void AnOversizedWireNeverDecodes()
		{
			KingdomRenovateHealSnapshot snapshot;
			ClassicAssert.IsFalse(KingdomRenovateHealSnapshot.TryDecode(new string('x', 1025), out snapshot));
			ClassicAssert.IsFalse(KingdomRenovateHealSnapshot.TryDecode(null, out snapshot));
		}
	}
}
#endif
