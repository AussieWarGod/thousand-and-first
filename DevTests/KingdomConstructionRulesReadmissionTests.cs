#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// #283: the one automatic exit from InspectionRequired. Proved on the real registry gate
	/// (<see cref="KingdomConstructionRules.ValidRegistryUpdate"/>), which refused every
	/// InspectionRequired to Outstanding move before the fix, and on the pure rule itself.
	/// </summary>
	[TestFixture]
	public sealed class KingdomConstructionRulesReadmissionTests
	{
		private static KingdomConstructionJob Stuck(string Failure,
			KingdomConstructionRoute Route = KingdomConstructionRoute.Improvement)
		{
			return new KingdomConstructionJob
			{
				Id = "00000000000000000000000000000283",
				OwnerKey = KingdomConstructionRules.OwnerKey("realm", 7L, "settlement"),
				ZoneId = "JoppaWorld.8.22.1.1.10",
				Route = Route,
				Phase = KingdomConstructionPhase.InspectionRequired,
				Projection = KingdomConstructionRules.ProjectionFor(Route),
				X = 29,
				Y = 9,
				SubjectId = "pred-1",
				SourceId = "pred-1",
				OutputId = "succ-1",
				TargetKey = "tentrow",
				Payload = "payload",
				CreatedTick = 409200L,
				StartedTick = 409200L,
				DueTick = 410100L,
				UpdatedTick = 411600L,
				Revision = 9,
				Failure = Failure,
				Claims = KingdomConstructionRules.NewClaims(2, new KingdomMaterialDebitCost())
			};
		}

		private static KingdomConstructionJob Readmitted(KingdomConstructionJob Current,
			KingdomRetiredHandoverDefect Defect)
		{
			return KingdomConstructionRules.Transition(Current, KingdomConstructionPhase.Outstanding,
				412800L, KingdomConstructionRules.ReadmissionPrefix(Defect) + Current.Failure);
		}

		[TestCase(KingdomConstructionRules.HandoverMarksFailure, KingdomRetiredHandoverDefect.FounderMarks)]
		[TestCase(KingdomConstructionRules.HandoverEndpointsFailure, KingdomRetiredHandoverDefect.LandedScaffold)]
		public void TheRegistryAdmitsTheExactReadmission(string Failure, KingdomRetiredHandoverDefect Defect)
		{
			KingdomConstructionJob current = Stuck(Failure);
			KingdomConstructionJob next = Readmitted(current, Defect);
			ClassicAssert.IsTrue(KingdomConstructionRules.ValidJob(current), "fixture job is valid");
			ClassicAssert.IsTrue(KingdomConstructionRules.IsRetiredDefectReadmission(current, next));
			ClassicAssert.IsTrue(KingdomConstructionRules.ValidRegistryUpdate(current, next),
				"InspectionRequired -> Outstanding publishes only for the ruled readmission");
			ClassicAssert.AreEqual("readmitted after retired handover defect #283 ("
				+ (Defect == KingdomRetiredHandoverDefect.FounderMarks ? "A" : "B") + "): " + Failure,
				next.Failure);
			string failure;
			ClassicAssert.IsTrue(KingdomConstructionRules.TryReadmissionFailure(current, Defect,
				out failure));
			ClassicAssert.AreEqual(next.Failure, failure);
		}

		[Test]
		public void EveryOtherReadmissionShapeIsRefused()
		{
			var cases = new Dictionary<string, Func<KingdomConstructionJob, KingdomConstructionJob>>
			{
				{ "subject", next => { next.SubjectId = "other"; return next; } },
				{ "source", next => { next.SourceId = "other"; return next; } },
				{ "output", next => { next.OutputId = "other"; return next; } },
				{ "target", next => { next.TargetKey = "other"; return next; } },
				{ "payload", next => { next.Payload = "other"; return next; } },
				{ "physical phase", next => { next.PhysicalPhase = KingdomPhysicalPhase.FinalRemovalPending; return next; } },
				{ "physical receipt", next => { next.PhysicalReceipt = "improvement-handover:v2"; return next; } },
				{ "due tick", next => { next.DueTick = 999999L; return next; } },
				{ "water claim", next => { next.Claims.WaterSpent = 1; next.Claims.WaterOutstanding = 1;
					next.Claims.WaterLost = 1; return next; } },
				{ "missing prefix", next => { next.Failure = KingdomConstructionRules.HandoverMarksFailure; return next; } },
				{ "wrong letter", next => { next.Failure = KingdomConstructionRules.ReadmissionPrefix(
					KingdomRetiredHandoverDefect.LandedScaffold) + KingdomConstructionRules.HandoverMarksFailure; return next; } },
				{ "phase working", next => { next.Phase = KingdomConstructionPhase.Working; return next; } },
			};
			foreach (var item in cases)
			{
				KingdomConstructionJob current = Stuck(KingdomConstructionRules.HandoverMarksFailure);
				KingdomConstructionJob next = item.Value(Readmitted(current,
					KingdomRetiredHandoverDefect.FounderMarks));
				ClassicAssert.IsFalse(KingdomConstructionRules.IsRetiredDefectReadmission(current, next),
					item.Key);
				ClassicAssert.IsFalse(KingdomConstructionRules.ValidRegistryUpdate(current, next), item.Key);
			}
		}

		[Test]
		public void AnotherRouteOrPhysicalPhaseIsRefused()
		{
			KingdomConstructionJob plot = Stuck(KingdomConstructionRules.HandoverMarksFailure,
				KingdomConstructionRoute.PlotCommission);
			KingdomConstructionJob plotNext = Readmitted(plot, KingdomRetiredHandoverDefect.FounderMarks);
			ClassicAssert.IsFalse(KingdomConstructionRules.IsRetiredDefectReadmission(plot, plotNext));
			ClassicAssert.IsFalse(KingdomConstructionRules.ValidRegistryUpdate(plot, plotNext));
			// The pure rule refuses a route change on its own, not only through the registry gate.
			plotNext.Route = KingdomConstructionRoute.Improvement;
			ClassicAssert.IsFalse(KingdomConstructionRules.IsRetiredDefectReadmission(plot, plotNext));

			KingdomConstructionJob removing = Stuck(KingdomConstructionRules.HandoverEndpointsFailure);
			removing.PhysicalPhase = KingdomPhysicalPhase.FinalRemovalPending;
			KingdomConstructionJob removingNext = Readmitted(removing,
				KingdomRetiredHandoverDefect.LandedScaffold);
			ClassicAssert.IsFalse(KingdomConstructionRules.IsRetiredDefectReadmission(removing,
				removingNext));
			ClassicAssert.IsFalse(KingdomConstructionRules.ValidRegistryUpdate(removing, removingNext));
		}

		[TestCase("Scaffold-removal proof is absent, malformed, or foreign.")]
		[TestCase("The improved successor could not be verified before handover.")]
		public void AnyOtherQuarantineStaysUnderInspection(string Failure)
		{
			KingdomConstructionJob current = Stuck(Failure);
			KingdomConstructionJob next = KingdomConstructionRules.Transition(current,
				KingdomConstructionPhase.Outstanding, 412800L,
				"readmitted after retired handover defect #283 (A): " + Failure);
			ClassicAssert.IsFalse(KingdomConstructionRules.IsRetiredDefectReadmission(current, next));
			ClassicAssert.IsFalse(KingdomConstructionRules.ValidRegistryUpdate(current, next));
			string failure;
			ClassicAssert.IsFalse(KingdomConstructionRules.TryReadmissionFailure(current,
				KingdomRetiredHandoverDefect.FounderMarks, out failure));
			ClassicAssert.IsNull(failure);
		}

		[TestCase(KingdomConstructionPhase.Complete)]
		[TestCase(KingdomConstructionPhase.Compensated)]
		[TestCase(KingdomConstructionPhase.Working)]
		[TestCase(KingdomConstructionPhase.ProjectionPending)]
		public void NoOtherExitFromInspectionOpens(KingdomConstructionPhase Phase)
		{
			KingdomConstructionJob current = Stuck(KingdomConstructionRules.HandoverMarksFailure);
			KingdomConstructionJob next = KingdomConstructionRules.Transition(current, Phase, 412800L,
				KingdomConstructionRules.ReadmissionPrefix(KingdomRetiredHandoverDefect.FounderMarks)
					+ current.Failure);
			ClassicAssert.IsFalse(KingdomConstructionRules.ValidRegistryUpdate(current, next), Phase.ToString());
		}

		[Test]
		public void CancellationOutOfInspectionIsUnchanged()
		{
			KingdomConstructionJob current = Stuck(KingdomConstructionRules.HandoverMarksFailure);
			KingdomConstructionJob next = KingdomConstructionRules.Transition(current,
				KingdomConstructionPhase.Cancelled, 412800L, "cancelled");
			ClassicAssert.IsTrue(KingdomConstructionRules.ValidRegistryUpdate(current, next));
		}

		[Test]
		public void TheSignatureLettersAndPrefixAreExact()
		{
			ClassicAssert.AreEqual(KingdomRetiredHandoverDefect.FounderMarks,
				KingdomConstructionRules.RetiredHandoverDefectFor(
					"Founder marks did not settle exactly on the successor."));
			ClassicAssert.AreEqual(KingdomRetiredHandoverDefect.LandedScaffold,
				KingdomConstructionRules.RetiredHandoverDefectFor(
					"The paid improvement job no longer matches its exact physical endpoints."));
			ClassicAssert.AreEqual(KingdomRetiredHandoverDefect.None,
				KingdomConstructionRules.RetiredHandoverDefectFor(null));
			ClassicAssert.IsNull(KingdomConstructionRules.ReadmissionPrefix(KingdomRetiredHandoverDefect.None));
			ClassicAssert.AreEqual("readmitted after retired handover defect #283 (B): ",
				KingdomConstructionRules.ReadmissionPrefix(KingdomRetiredHandoverDefect.LandedScaffold));
		}
	}
}
#endif
