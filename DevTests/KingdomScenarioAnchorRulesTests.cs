#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The differential-anchor law. A stamp names an anchor; only an independently held evidence
	/// record proves one. Every negative case below must refuse, or the harness signs its own work.
	/// </summary>
	[TestFixture]
	public sealed class KingdomScenarioAnchorRulesTests
	{
		private const string DefinitionDigest =
			"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
		private const string KeySetDigest =
			"fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210";
		private const string PlanDigest =
			"abcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcd";
		private const string OtherDigest =
			"1111111111111111111111111111111111111111111111111111111111111111";
		private const string Mod = "0.2.0";
		private const string Core = "2.0.211.51";
		private const string Authority = "architecture-stamper";
		private const string Verbs = "provecatalogue+stagegallerycase";

		private static KingdomScenarioProvenance Stamp()
		{
			return new KingdomScenarioProvenance
			{
				ScenarioKey = "arch-gallery-slice",
				AuthorityClass = Authority,
				Verbs = Verbs,
				AnchorId = "anchor-arch-01",
				KeySetDigest = KeySetDigest,
				Seed = "#4242",
				ModVersion = Mod,
				QudCoreVersion = Core,
				DefinitionDigest = DefinitionDigest,
				PlanDigest = PlanDigest,
				Synthetic = false
			};
		}

		private static KingdomScenarioAnchorEvidence Evidence()
		{
			return new KingdomScenarioAnchorEvidence
			{
				AnchorId = "anchor-arch-01",
				AuthorityClass = Authority,
				Verbs = Verbs,
				KeySetDigest = KeySetDigest,
				DefinitionDigest = DefinitionDigest,
				PlanDigest = PlanDigest,
				ModVersion = Mod,
				QudCoreVersion = Core,
				Reached = KingdomScenarioAnchorRules.Provenance.OrdinaryPlay
			};
		}

		private static bool Sign(KingdomScenarioProvenance stamp,
			KingdomScenarioAnchorEvidence evidence, out string failure)
		{
			return KingdomScenarioAnchorRules.TrySignAcceptance(stamp, evidence,
				DefinitionDigest, Mod, Core, out failure);
		}

		[Test]
		public void MatchingEvidenceSignsAcceptance()
		{
			string failure;
			Assert.IsTrue(Sign(Stamp(), Evidence(), out failure), failure);
		}

		/// <summary>The trap the ruling names: a stamp alone must never be enough.</summary>
		[Test]
		public void AStampWithoutIndependentEvidenceNeverSigns()
		{
			string failure;
			Assert.IsFalse(Sign(Stamp(), null, out failure));
			StringAssert.Contains("cannot prove it", failure);
		}

		[Test]
		public void InventedAnchorIdIsRefused()
		{
			KingdomScenarioAnchorEvidence evidence = Evidence();
			evidence.AnchorId = "anchor-invented";
			string failure;
			Assert.IsFalse(Sign(Stamp(), evidence, out failure));
			StringAssert.Contains("different anchor id", failure);
		}

		[Test]
		public void EvidenceForAnotherAuthorityClassIsRefused()
		{
			KingdomScenarioProvenance stamp = Stamp();
			stamp.AuthorityClass = "polity-custody";
			KingdomScenarioAnchorEvidence evidence = Evidence();
			string failure;
			Assert.IsFalse(Sign(stamp, evidence, out failure));
			StringAssert.Contains("authority class", failure);
		}

		[Test]
		public void EvidenceReachedByADifferentVerbSequenceIsRefused()
		{
			KingdomScenarioAnchorEvidence evidence = Evidence();
			evidence.Verbs = "foundfirst";
			string failure;
			Assert.IsFalse(Sign(Stamp(), evidence, out failure));
			StringAssert.Contains("verb sequence", failure);
		}

		[Test]
		public void DivergentKeySetIsRefused()
		{
			KingdomScenarioAnchorEvidence evidence = Evidence();
			evidence.KeySetDigest = OtherDigest;
			string failure;
			Assert.IsFalse(Sign(Stamp(), evidence, out failure));
			StringAssert.Contains("diverges", failure);
		}

		[Test]
		public void AnchorFoundedUnderDifferentAuthoredTextIsStale()
		{
			KingdomScenarioAnchorEvidence evidence = Evidence();
			evidence.DefinitionDigest = OtherDigest;
			string failure;
			Assert.IsFalse(Sign(Stamp(), evidence, out failure));
			StringAssert.Contains("stale", failure);
		}

		[TestCase("0.3.0", Core)]
		[TestCase(Mod, "2.0.212.0")]
		public void AnchorFoundedUnderADifferentBuildIsStale(string mod, string core)
		{
			KingdomScenarioAnchorEvidence evidence = Evidence();
			evidence.ModVersion = mod;
			evidence.QudCoreVersion = core;
			string failure;
			Assert.IsFalse(Sign(Stamp(), evidence, out failure));
			StringAssert.Contains("stale", failure);
		}

		[TestCase(KingdomScenarioAnchorRules.Provenance.ScenarioBuilt)]
		[TestCase(KingdomScenarioAnchorRules.Provenance.Unknown)]
		public void AnchorNotReachedByOrdinaryPlayIsRefused(
			KingdomScenarioAnchorRules.Provenance reached)
		{
			KingdomScenarioAnchorEvidence evidence = Evidence();
			evidence.Reached = reached;
			string failure;
			Assert.IsFalse(Sign(Stamp(), evidence, out failure));
			StringAssert.Contains("cannot anchor itself", failure);
		}

		[Test]
		public void SyntheticStampNeverSignsEvenWithPerfectEvidence()
		{
			KingdomScenarioProvenance stamp = Stamp();
			stamp.Synthetic = true;
			string failure;
			Assert.IsFalse(Sign(stamp, Evidence(), out failure));
			StringAssert.Contains("recovery diagnostics only", failure);
		}

		[Test]
		public void StampNamingNoAnchorIsIneligibleRatherThanGreen()
		{
			KingdomScenarioProvenance stamp = Stamp();
			stamp.AnchorId = null;
			string failure;
			Assert.IsFalse(Sign(stamp, Evidence(), out failure));
			StringAssert.Contains("ineligible, not green", failure);
		}

		/// <summary>A directly constructed malformed stamp must not slip past decode checks.</summary>
		[Test]
		public void MalformedDirectStampRecordIsRefused()
		{
			KingdomScenarioProvenance stamp = Stamp();
			stamp.Verbs = "BAD VERB";
			string failure;
			Assert.IsFalse(Sign(stamp, Evidence(), out failure));
			StringAssert.Contains("malformed", failure);
		}

		[Test]
		public void MalformedDirectEvidenceRecordIsRefused()
		{
			KingdomScenarioAnchorEvidence evidence = Evidence();
			evidence.KeySetDigest = "deadbeef";
			string failure;
			Assert.IsFalse(Sign(Stamp(), evidence, out failure));
			StringAssert.Contains("malformed", failure);
		}

		[Test]
		public void EvidenceNamingAnUnknownAuthorityClassIsRefused()
		{
			KingdomScenarioProvenance stamp = Stamp();
			stamp.AuthorityClass = "no-such-authority";
			KingdomScenarioAnchorEvidence evidence = Evidence();
			evidence.AuthorityClass = "no-such-authority";
			string failure;
			Assert.IsFalse(Sign(stamp, evidence, out failure));
			StringAssert.Contains("no semantic key set", failure);
		}

		/// <summary>
		/// The plan digest binds the bindings and resolved arguments. An anchor measured against a
		/// different resolved plan is not evidence for this one.
		/// </summary>
		[Test]
		public void EvidenceMeasuredAgainstADifferentResolvedPlanIsRefused()
		{
			KingdomScenarioAnchorEvidence evidence = Evidence();
			evidence.PlanDigest = OtherDigest;
			string failure;
			Assert.IsFalse(Sign(Stamp(), evidence, out failure));
			StringAssert.Contains("different resolved plan", failure);
		}

		[Test]
		public void EvidenceWithAMalformedPlanDigestIsRefused()
		{
			KingdomScenarioAnchorEvidence evidence = Evidence();
			evidence.PlanDigest = "deadbeef";
			string failure;
			Assert.IsFalse(Sign(Stamp(), evidence, out failure));
			StringAssert.Contains("malformed", failure);
		}

		[Test]
		public void StampWithAMalformedPlanDigestCannotSign()
		{
			KingdomScenarioProvenance stamp = Stamp();
			stamp.PlanDigest = "deadbeef";
			string failure;
			Assert.IsFalse(Sign(stamp, Evidence(), out failure));
			Assert.IsNotEmpty(failure);
		}

		/// <summary>
		/// The differential must be satisfiable from ordinary play. Every declared key is read from
		/// the production architecture intent, which an ordinary commission also produces; a
		/// gallery-only receipt property here would make the oracle impossible rather than pending.
		/// </summary>
		[Test]
		public void DeclaredKeySetNamesOnlyProductionOwnedIntentFacts()
		{
			IList<string> keys = KingdomScenarioAnchorRules.KeySet(Authority);
			Assert.Greater(keys.Count, 0);
			foreach (string key in keys)
			{
				StringAssert.StartsWith("architecture.", key);
				Assert.IsFalse(key.Contains("case"), key + " is a gallery-only property");
				Assert.IsFalse(key.Contains("receipt.digest"), key + " is a gallery-only property");
				Assert.IsFalse(key.Contains("rect"),
					key + " is placement-dependent and cannot match across two lawful builds");
			}
		}
	}
}
#endif
