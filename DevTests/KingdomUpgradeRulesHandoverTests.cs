#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// #283 engine-free handover rules, run by both suites: the founder marks shared by CarryMarks
	/// and ExactCarriedMarks, the landed scaffold's durable identity, and the classifier of the two
	/// retired handover defects the once-only readmission admits.
	/// </summary>
	[TestFixture]
	public sealed class KingdomUpgradeRulesHandoverTests
	{
		private static KingdomUpgradeRules.FounderMarks Marks(int Bits, string Given)
		{
			return new KingdomUpgradeRules.FounderMarks((Bits & 1) != 0, (Bits & 2) != 0,
				(Bits & 4) != 0, (Bits & 8) != 0, (Bits & 16) != 0, Given);
		}

		/// <summary>Every predecessor mark set, with and without a given name, against every
		/// successor shape: a carried successor settles exactly when its larder and stores
		/// dedications had somewhere to go.</summary>
		[Test]
		public void CarriedMarksSettleExactlyWhenTheDedicationsFit()
		{
			int cases = 0;
			foreach (string given in new[] { null, "x" })
				for (int bits = 0; bits < 32; bits++)
					foreach (bool inventory in new[] { false, true })
						foreach (bool liquid in new[] { false, true })
						{
							var predecessor = Marks(bits, given);
							var carried = KingdomUpgradeRules.CarryFounderMarks(predecessor, inventory, liquid);
							bool expected = (!predecessor.Larder || inventory)
								&& (!predecessor.Stores || liquid);
							ClassicAssert.AreEqual(expected, KingdomUpgradeRules.FounderMarksSettled(
								predecessor, carried, inventory, liquid),
								"bits=" + bits + " given=" + given + " inv=" + inventory + " liq=" + liquid);
							cases++;
						}
			ClassicAssert.AreEqual(256, cases);
		}

		[Test]
		public void yielding_predecessor_settles_after_carry()
		{
			KingdomUpgradeRules.FounderMarks tent = Marks(16, null);
			var carried = KingdomUpgradeRules.CarryFounderMarks(tent, false, false);
			ClassicAssert.IsTrue(carried.Yielding, "the yielding promise is carried");
			ClassicAssert.IsTrue(KingdomUpgradeRules.FounderMarksSettled(tent, carried, false, false));
			ClassicAssert.IsFalse(KingdomUpgradeRules.FounderMarksSettled(tent, Marks(0, null),
				false, false), "a successor that dropped yielding does not settle");
		}

		[Test]
		public void non_yielding_predecessor_accepts_yielding_successor()
		{
			ClassicAssert.IsTrue(KingdomUpgradeRules.FounderMarksSettled(Marks(0, null),
				Marks(16, null), false, false), "yielding stays one-directional");
		}

		/// <summary>The truth table of every clause but yielding, against an independent oracle,
		/// over every predecessor and successor mark set and both container shapes.</summary>
		[Test]
		public void SettledExceptYieldingIgnoresOnlyTheYieldingClause()
		{
			int cases = 0;
			foreach (string pGiven in new[] { null, "x" })
				foreach (string sGiven in new[] { null, "x", "y" })
					for (int p = 0; p < 32; p++)
						for (int s = 0; s < 32; s++)
							foreach (bool inventory in new[] { false, true })
								foreach (bool liquid in new[] { false, true })
								{
									var pm = Marks(p, pGiven);
									var sm = Marks(s, sGiven);
									bool oracle = (!pm.Larder || inventory && sm.Larder)
										&& (!pm.Stores || liquid && sm.Stores)
										&& (!pm.Certified || sm.Certified)
										&& (pm.GivenName == null || sm.GivenName == pm.GivenName)
										&& (!pm.Adopted || sm.Adopted);
									ClassicAssert.AreEqual(oracle,
										KingdomUpgradeRules.FounderMarksSettledExceptYielding(pm, sm,
											inventory, liquid), p + "/" + s);
									ClassicAssert.AreEqual(oracle && (!pm.Yielding || sm.Yielding),
										KingdomUpgradeRules.FounderMarksSettled(pm, sm, inventory, liquid),
										p + "/" + s);
									cases++;
								}
			ClassicAssert.AreEqual(2 * 3 * 32 * 32 * 4, cases);
		}

		[TestCase(true, true, "s1", true, "s1", true, null, TestName = "LandedScaffold_LiveReferenceRefuses")]
		[TestCase(false, false, null, false, "s1", true, null, TestName = "LandedScaffold_MalformedIntentRefuses")]
		[TestCase(false, false, null, true, "", true, null, TestName = "LandedScaffold_EmptyIntentRefuses")]
		[TestCase(false, true, "s2", true, "s1", true, null, TestName = "LandedScaffold_StaleReferenceMustAgree")]
		[TestCase(false, true, null, true, "s1", true, null, TestName = "LandedScaffold_PooledStaleReferenceRefuses")]
		[TestCase(false, false, null, true, "s1", false, null, TestName = "LandedScaffold_LiveIdElsewhereRefuses")]
		[TestCase(false, false, null, true, "s1", true, "s1", TestName = "LandedScaffold_NulledReferenceProvesFromIntent")]
		[TestCase(false, true, "s1", true, "s1", true, "s1", TestName = "LandedScaffold_AgreeingStaleReferenceProves")]
		public void LandedScaffoldIdentityGuards(bool Live, bool Present, string ReferenceId,
			bool Exact, string IntentId, bool Absent, string Expected)
		{
			ClassicAssert.AreEqual(Expected, KingdomUpgradeRules.LandedScaffoldIdentity(Live, Present,
				ReferenceId, Exact, IntentId, Absent));
		}

		private const string Predecessor = "pred-1", Successor = "succ-1", Key = "tentrow";

		private static KingdomConstructionJob StuckJob(string Failure)
		{
			return new KingdomConstructionJob
			{
				Id = "00000000000000000000000000000283", Route = KingdomConstructionRoute.Improvement,
				Phase = KingdomConstructionPhase.InspectionRequired,
				PhysicalPhase = KingdomPhysicalPhase.None, SubjectId = Predecessor,
				SourceId = Predecessor, OutputId = Successor, TargetKey = Key, X = 29, Y = 9,
				Failure = Failure
			};
		}

		private static KingdomUpgradeRules.RetiredHandoverObservation Common()
		{
			return new KingdomUpgradeRules.RetiredHandoverObservation
			{
				Owned = true, Current = true, PredecessorReceipt = true, SuccessorReceipt = true,
				SuccessorPending = true, SuccessorExact = true, Working = true, EffectsDone = false,
				PredecessorExact = true, ScaffoldLanded = true, UpgradeQuarantined = false,
				LayoutFault = false, ContentCustody = true, AlreadyReadmitted = false
			};
		}

		/// <summary>Signature A as the #283 stall leaves it: yielding dropped, larder and name
		/// carried, phase five, a complete successor layout.</summary>
		private static KingdomUpgradeRules.RetiredHandoverObservation SignatureA()
		{
			KingdomUpgradeRules.RetiredHandoverObservation observed = Common();
			observed.PredecessorMarks = Marks(1 | 16, "Hearth");
			observed.SuccessorMarks = Marks(1, "Hearth");
			observed.HasInventory = true;
			observed.SameWear = true;
			observed.UpgradePhase = 5;
			observed.SuccessorLayoutComplete = true;
			return observed;
		}

		/// <summary>Signature B with none of A's structure: a pre-retag refusal whose next pass
		/// HandOver refused on the nulled scaffold reference.</summary>
		private static KingdomUpgradeRules.RetiredHandoverObservation SignatureB()
		{
			KingdomUpgradeRules.RetiredHandoverObservation observed = Common();
			observed.UpgradePhase = 0;
			observed.ScaffoldReferenceLive = false;
			return observed;
		}

		private static KingdomRetiredHandoverDefect Classify(KingdomConstructionJob Job,
			KingdomUpgradeRules.RetiredHandoverObservation Observed)
		{
			return KingdomUpgradeRules.ClassifyRetiredHandoverDefect(Job, Predecessor, 29, 9,
				Successor, Key, Observed);
		}

		[Test]
		public void SignatureAIsAdmittedWithEveryConjunct()
		{
			ClassicAssert.AreEqual(KingdomRetiredHandoverDefect.FounderMarks,
				Classify(StuckJob(KingdomConstructionRules.HandoverMarksFailure), SignatureA()));
		}

		[Test]
		public void SignatureBIsAdmittedWithEveryConjunct()
		{
			ClassicAssert.AreEqual(KingdomRetiredHandoverDefect.LandedScaffold,
				Classify(StuckJob(KingdomConstructionRules.HandoverEndpointsFailure), SignatureB()));
		}

		private delegate void Flip(ref KingdomUpgradeRules.RetiredHandoverObservation Observed);

		private static Dictionary<string, Flip> CommonFlips()
		{
			return new Dictionary<string, Flip>
			{
				{ "not owned", (ref KingdomUpgradeRules.RetiredHandoverObservation o) => o.Owned = false },
				{ "not current", (ref KingdomUpgradeRules.RetiredHandoverObservation o) => o.Current = false },
				{ "predecessor receipt", (ref KingdomUpgradeRules.RetiredHandoverObservation o) => o.PredecessorReceipt = false },
				{ "successor receipt", (ref KingdomUpgradeRules.RetiredHandoverObservation o) => o.SuccessorReceipt = false },
				{ "successor not pending", (ref KingdomUpgradeRules.RetiredHandoverObservation o) => o.SuccessorPending = false },
				{ "successor not exact", (ref KingdomUpgradeRules.RetiredHandoverObservation o) => o.SuccessorExact = false },
				{ "not working", (ref KingdomUpgradeRules.RetiredHandoverObservation o) => o.Working = false },
				{ "effects done", (ref KingdomUpgradeRules.RetiredHandoverObservation o) => o.EffectsDone = true },
				{ "predecessor not exact", (ref KingdomUpgradeRules.RetiredHandoverObservation o) => o.PredecessorExact = false },
				{ "scaffold not landed", (ref KingdomUpgradeRules.RetiredHandoverObservation o) => o.ScaffoldLanded = false },
				{ "stamper quarantined", (ref KingdomUpgradeRules.RetiredHandoverObservation o) => o.UpgradeQuarantined = true },
				{ "layout fault", (ref KingdomUpgradeRules.RetiredHandoverObservation o) => o.LayoutFault = true },
				{ "custody", (ref KingdomUpgradeRules.RetiredHandoverObservation o) => o.ContentCustody = false },
				{ "already readmitted", (ref KingdomUpgradeRules.RetiredHandoverObservation o) => o.AlreadyReadmitted = true },
			};
		}

		[Test]
		public void EachSignatureAConjunctFlippedRefuses()
		{
			Dictionary<string, Flip> flips = CommonFlips();
			flips.Add("successor yields", (ref KingdomUpgradeRules.RetiredHandoverObservation o) => o.SuccessorMarks = Marks(1 | 16, "Hearth"));
			flips.Add("predecessor does not yield", (ref KingdomUpgradeRules.RetiredHandoverObservation o) => o.PredecessorMarks = Marks(1, "Hearth"));
			flips.Add("larder unsettled", (ref KingdomUpgradeRules.RetiredHandoverObservation o) => o.SuccessorMarks = Marks(0, "Hearth"));
			flips.Add("name unsettled", (ref KingdomUpgradeRules.RetiredHandoverObservation o) => o.SuccessorMarks = Marks(1, "Other"));
			flips.Add("wear differs", (ref KingdomUpgradeRules.RetiredHandoverObservation o) => o.SameWear = false);
			flips.Add("upgrade phase four", (ref KingdomUpgradeRules.RetiredHandoverObservation o) => o.UpgradePhase = 4);
			flips.Add("successor layout incomplete", (ref KingdomUpgradeRules.RetiredHandoverObservation o) => o.SuccessorLayoutComplete = false);
			foreach (KeyValuePair<string, Flip> flip in flips)
			{
				KingdomUpgradeRules.RetiredHandoverObservation observed = SignatureA();
				flip.Value(ref observed);
				ClassicAssert.AreEqual(KingdomRetiredHandoverDefect.None,
					Classify(StuckJob(KingdomConstructionRules.HandoverMarksFailure), observed), flip.Key);
			}
		}

		[Test]
		public void EachSignatureBConjunctFlippedRefuses()
		{
			Dictionary<string, Flip> flips = CommonFlips();
			flips.Add("scaffold reference live", (ref KingdomUpgradeRules.RetiredHandoverObservation o) => o.ScaffoldReferenceLive = true);
			foreach (KeyValuePair<string, Flip> flip in flips)
			{
				KingdomUpgradeRules.RetiredHandoverObservation observed = SignatureB();
				flip.Value(ref observed);
				ClassicAssert.AreEqual(KingdomRetiredHandoverDefect.None,
					Classify(StuckJob(KingdomConstructionRules.HandoverEndpointsFailure), observed), flip.Key);
			}
		}

		[Test]
		public void EachJobIdentityConjunctFlippedRefuses()
		{
			var flips = new Dictionary<string, Action<KingdomConstructionJob>>
			{
				{ "route", job => job.Route = KingdomConstructionRoute.PlotCommission },
				{ "phase outstanding", job => job.Phase = KingdomConstructionPhase.Outstanding },
				{ "phase complete", job => job.Phase = KingdomConstructionPhase.Complete },
				{ "physical removal pending", job => job.PhysicalPhase = KingdomPhysicalPhase.FinalRemovalPending },
				{ "subject", job => job.SubjectId = "other" },
				{ "source", job => job.SourceId = "other" },
				{ "output", job => job.OutputId = "other" },
				{ "target key", job => job.TargetKey = "other" },
				{ "cell x", job => job.X = 30 },
				{ "cell y", job => job.Y = 10 },
			};
			foreach (var flip in flips)
			{
				KingdomConstructionJob a = StuckJob(KingdomConstructionRules.HandoverMarksFailure);
				flip.Value(a);
				ClassicAssert.AreEqual(KingdomRetiredHandoverDefect.None, Classify(a, SignatureA()), "A " + flip.Key);
				KingdomConstructionJob b = StuckJob(KingdomConstructionRules.HandoverEndpointsFailure);
				flip.Value(b);
				ClassicAssert.AreEqual(KingdomRetiredHandoverDefect.None, Classify(b, SignatureB()), "B " + flip.Key);
			}
		}

		[TestCase(KingdomConstructionRules.HandoverEndpointsUnproven)]
		[TestCase("Scaffold-removal proof is absent, malformed, or foreign.")]
		[TestCase("The improved successor could not be verified before handover.")]
		[TestCase("readmitted after retired handover defect #283 (A): Founder marks did not settle exactly on the successor.")]
		[TestCase("founder marks did not settle exactly on the successor.")]
		[TestCase("")]
		public void AnyOtherFailureTextRefuses(string Failure)
		{
			ClassicAssert.AreEqual(KingdomRetiredHandoverDefect.None, Classify(StuckJob(Failure), SignatureA()));
			ClassicAssert.AreEqual(KingdomRetiredHandoverDefect.None, Classify(StuckJob(Failure), SignatureB()));
		}

		[Test]
		public void ATextOnlyNarrowsTheCandidates()
		{
			// The endpoints text with signature A's structure but a live scaffold reference, and
			// the marks text with B's structure (no yielding drop): neither admits.
			KingdomUpgradeRules.RetiredHandoverObservation live = SignatureA();
			live.ScaffoldReferenceLive = true;
			ClassicAssert.AreEqual(KingdomRetiredHandoverDefect.None,
				Classify(StuckJob(KingdomConstructionRules.HandoverEndpointsFailure), live));
			ClassicAssert.AreEqual(KingdomRetiredHandoverDefect.None,
				Classify(StuckJob(KingdomConstructionRules.HandoverMarksFailure), SignatureB()));
			ClassicAssert.AreEqual(KingdomRetiredHandoverDefect.None,
				KingdomUpgradeRules.ClassifyRetiredHandoverDefect(null, Predecessor, 29, 9, Successor,
					Key, SignatureA()));
			ClassicAssert.AreEqual(KingdomRetiredHandoverDefect.None, Classify(StuckJob(null), SignatureA()));
			ClassicAssert.AreEqual(KingdomRetiredHandoverDefect.None, Classify(StuckJob(null), SignatureB()));
		}
	}
}
#endif
