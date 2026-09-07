#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Grammar and eligibility rules for the scenario stamp. Every eligibility test must fail if
	/// its governing clause is deleted, so the ruling cannot be weakened silently.
	/// </summary>
	[TestFixture]
	public sealed class KingdomScenarioProvenanceRulesTests
	{
		private const string DigestA =
			"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
		private const string DigestB =
			"fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210";
		private const string DigestC =
			"00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff";
		private const string Mod = "0.2.0";
		private const string Core = "2.0.211.51";

		private static KingdomScenarioProvenance Sound()
		{
			return new KingdomScenarioProvenance
			{
				ScenarioKey = "arch-gallery-slice",
				AuthorityClass = "architecture-stamper",
				Verbs = "foundfirst+armcheckpoint",
				AnchorId = "anchor-arch-01",
				KeySetDigest = DigestB,
				Seed = "#4242",
				ModVersion = Mod,
				QudCoreVersion = Core,
				DefinitionDigest = DigestA,
				PlanDigest = DigestC,
				Synthetic = false
			};
		}

		[Test]
		public void SoundRecordRoundTripsEveryField()
		{
			string wire = KingdomScenarioProvenanceRules.Encode(Sound());
			ClassicAssert.IsNotNull(wire);
			StringAssert.StartsWith("sc1|", wire);
			KingdomScenarioProvenance back;
			string failure;
			ClassicAssert.IsTrue(KingdomScenarioProvenanceRules.TryDecode(wire, out back, out failure),
				failure);
			ClassicAssert.AreEqual("arch-gallery-slice", back.ScenarioKey);
			ClassicAssert.AreEqual("architecture-stamper", back.AuthorityClass);
			ClassicAssert.AreEqual("foundfirst+armcheckpoint", back.Verbs);
			ClassicAssert.AreEqual(DigestC, back.PlanDigest, "the plan digest must survive the round trip");
			ClassicAssert.AreEqual("anchor-arch-01", back.AnchorId);
			ClassicAssert.AreEqual(DigestB, back.KeySetDigest);
			ClassicAssert.AreEqual("#4242", back.Seed);
			ClassicAssert.AreEqual(DigestA, back.DefinitionDigest);
			ClassicAssert.IsFalse(back.Synthetic);
		}

		[Test]
		public void AbsentAnchorAndKeySetRoundTripAsNullRatherThanTheAbsentMarker()
		{
			KingdomScenarioProvenance record = Sound();
			record.AnchorId = null;
			record.KeySetDigest = null;
			string wire = KingdomScenarioProvenanceRules.Encode(record);
			ClassicAssert.IsNotNull(wire);
			KingdomScenarioProvenance back;
			string failure;
			ClassicAssert.IsTrue(KingdomScenarioProvenanceRules.TryDecode(wire, out back, out failure),
				failure);
			ClassicAssert.IsNull(back.AnchorId);
			ClassicAssert.IsNull(back.KeySetDigest);
		}

		[Test]
		public void SyntheticFlagSurvivesTheRoundTrip()
		{
			KingdomScenarioProvenance record = Sound();
			record.Synthetic = true;
			KingdomScenarioProvenance back;
			string failure;
			ClassicAssert.IsTrue(KingdomScenarioProvenanceRules.TryDecode(
				KingdomScenarioProvenanceRules.Encode(record), out back, out failure), failure);
			ClassicAssert.IsTrue(back.Synthetic);
		}

		[TestCase(null, TestName = "EmptyStampIsRefused")]
		[TestCase("", TestName = "BlankStampIsRefused")]
		[TestCase("sc2|a|b|c|-|-|1|0.2.0|2.0.211.51|" +
			"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef|0",
			TestName = "ForeignTagIsRefused")]
		[TestCase("sc1|a|b|c|-|-|1|0.2.0|2.0.211.51|deadbeef|0",
			TestName = "ShortDefinitionDigestIsRefused")]
		[TestCase("sc1|a|b|c|-|-|1|0.2.0|2.0.211.51|" +
			"0123456789ABCDEF0123456789abcdef0123456789abcdef0123456789abcdef|0",
			TestName = "UppercaseDigestIsRefused")]
		[TestCase("sc1|a|b|c|-|-|1|0.2.0|2.0.211.51|" +
			"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef|2",
			TestName = "UnknownSyntheticFlagIsRefused")]
		[TestCase("sc1|a|b|c|-|-|1|0.2.0|2.0.211.51|" +
			"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
			TestName = "MissingFieldIsRefused")]
		[TestCase("sc1|a|b|BAD VERB|-|-|1|0.2.0|2.0.211.51|" +
			"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef|0",
			TestName = "MalformedVerbTokenIsRefused")]
		[TestCase("sc1|a|b|c|not a token|-|1|0.2.0|2.0.211.51|" +
			"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef|0",
			TestName = "MalformedAnchorIdIsRefused")]
		public void MalformedStampsAreRefusedWithoutARecord(string raw)
		{
			KingdomScenarioProvenance record;
			string failure;
			ClassicAssert.IsFalse(KingdomScenarioProvenanceRules.TryDecode(raw, out record, out failure));
			ClassicAssert.IsNull(record);
			ClassicAssert.IsNotEmpty(failure);
		}

		[Test]
		public void OversizeStampIsRefusedBeforeParsing()
		{
			string raw = "sc1|" + new string('a', KingdomScenarioProvenanceRules.MaxWire);
			KingdomScenarioProvenance record;
			string failure;
			ClassicAssert.IsFalse(KingdomScenarioProvenanceRules.TryDecode(raw, out record, out failure));
			StringAssert.Contains("bounded wire size", failure);
		}

		[Test]
		public void VerbSequencePreservesDeclaredOrder()
		{
			IList<string> verbs =
				KingdomScenarioProvenanceRules.VerbSequence("foundfirst+seat+armcheckpoint");
			ClassicAssert.AreEqual(3, verbs.Count);
			ClassicAssert.AreEqual("foundfirst", verbs[0]);
			ClassicAssert.AreEqual("seat", verbs[1]);
			ClassicAssert.AreEqual("armcheckpoint", verbs[2]);
		}

		[Test]
		public void VerbSequenceBeyondTheCapIsRejectedRatherThanTruncated()
		{
			string[] many = new string[KingdomScenarioProvenanceRules.MaxVerbs + 1];
			for (int i = 0; i < many.Length; i++) many[i] = "v" + i;
			KingdomScenarioProvenance record = Sound();
			record.Verbs = string.Join("+", many);
			ClassicAssert.IsNull(KingdomScenarioProvenanceRules.Encode(record));
			ClassicAssert.IsEmpty(KingdomScenarioProvenanceRules.VerbSequence(record.Verbs));
		}

		[Test]
		public void SoundStampPassesTheShapeAndStalenessCheck()
		{
			string failure;
			ClassicAssert.IsTrue(KingdomScenarioProvenanceRules.TryValidateStampShape(
				Sound(), DigestA, Mod, Core, out failure), failure);
		}

		/// <summary>
		/// The shape check is deliberately not an acceptance decision. A stamp naming an anchor is
		/// well formed; proving that anchor needs the independently held evidence record.
		/// </summary>
		[Test]
		public void ShapeCheckIsNotAcceptanceAndSaysSoForAWellFormedStamp()
		{
			string failure;
			ClassicAssert.IsTrue(KingdomScenarioProvenanceRules.TryValidateStampShape(
				Sound(), DigestA, Mod, Core, out failure), failure);
			StringAssert.Contains("independently held anchor-evidence",
				KingdomScenarioProvenanceRules.AcceptanceRequiresIndependentAnchorEvidence(Sound()));
		}

		[Test]
		public void MalformedDirectRecordFailsTheShapeCheckRatherThanPassing()
		{
			KingdomScenarioProvenance record = Sound();
			record.Verbs = "BAD VERB";
			string failure;
			ClassicAssert.IsFalse(KingdomScenarioProvenanceRules.TryValidateStampShape(
				record, DigestA, Mod, Core, out failure));
			StringAssert.Contains("malformed", failure);
		}

		[Test]
		public void SyntheticStateIsNamedAsADiagnosticByTheAcceptanceExplanation()
		{
			KingdomScenarioProvenance record = Sound();
			record.Synthetic = true;
			StringAssert.Contains("recovery diagnostics only",
				KingdomScenarioProvenanceRules.AcceptanceRequiresIndependentAnchorEvidence(record));
		}

		[Test]
		public void StateWithoutADifferentialAnchorIsIneligibleRatherThanGreen()
		{
			KingdomScenarioProvenance record = Sound();
			record.AnchorId = null;
			StringAssert.Contains("ineligible, not green",
				KingdomScenarioProvenanceRules.AcceptanceRequiresIndependentAnchorEvidence(record));
		}

		[Test]
		public void StateWithoutAComparedKeySetIsIneligibleRatherThanGreen()
		{
			KingdomScenarioProvenance record = Sound();
			record.KeySetDigest = null;
			StringAssert.Contains("ineligible, not green",
				KingdomScenarioProvenanceRules.AcceptanceRequiresIndependentAnchorEvidence(record));
		}

		[Test]
		public void ChangedScenarioDefinitionMakesTheStampStale()
		{
			string failure;
			ClassicAssert.IsFalse(KingdomScenarioProvenanceRules.TryValidateStampShape(
				Sound(), DigestB, Mod, Core, out failure));
			StringAssert.Contains("stale", failure);
		}

		[TestCase("0.3.0", Core, TestName = "ChangedModVersionIsStale")]
		[TestCase(Mod, "2.0.212.0", TestName = "ChangedCoreVersionIsStale")]
		public void ChangedBuildAuthorityMakesTheStampStale(string mod, string core)
		{
			string failure;
			ClassicAssert.IsFalse(KingdomScenarioProvenanceRules.TryValidateStampShape(
				Sound(), DigestA, mod, core, out failure));
			StringAssert.Contains("stale", failure);
		}

		[Test]
		public void DescribeNamesTheMissingAnchorInsteadOfImplyingEligibility()
		{
			KingdomScenarioProvenance record = Sound();
			record.AnchorId = null;
			string text = KingdomScenarioProvenanceRules.Describe(record);
			StringAssert.Contains("ineligible", text);
		}

		[Test]
		public void DescribeMarksASyntheticStateAsADiagnostic()
		{
			KingdomScenarioProvenance record = Sound();
			record.Synthetic = true;
			StringAssert.Contains("SYNTHETIC",
				KingdomScenarioProvenanceRules.Describe(record));
		}

		[Test]
		public void AbsentStampReadsAsAnOrdinaryGameRatherThanAFault()
		{
			StringAssert.Contains("ordinary game", KingdomScenarioProvenanceRules.Describe(null));
		}

		[Test]
		public void UnreadablePresentStampIsNeverReportedAsOrdinaryPlay()
		{
			string text = KingdomScenarioProvenanceRules.DescribeUnreadable("malformed field");
			StringAssert.Contains("not produced by ordinary play", text);
		}

		/// <summary>The state key is serialized; renaming it orphans every existing stamp.</summary>
		[Test]
		public void ProvenanceStateKeyIsTheVersionedName()
		{
			ClassicAssert.AreEqual("r_TAF_ScenarioProvenance_v1",
				KingdomScenarioProvenanceRules.ProvenanceState);
		}
	}
}
#endif
