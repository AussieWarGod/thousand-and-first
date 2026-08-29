#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomPurposeEffectRecoverySourceTests
	{
		private static string Source(string relative)
		{
			return TestMain.ReadRepositoryText(
				relative.Replace('/', Path.DirectorySeparatorChar));
		}

		private static string Between(string source, string from, string to)
		{
			int start = source.IndexOf(from, StringComparison.Ordinal);
			Assert.GreaterOrEqual(start, 0, from);
			int end = source.IndexOf(to, start + from.Length, StringComparison.Ordinal);
			Assert.Greater(end, start, to);
			return source.Substring(start, end - start);
		}

		private static void Ordered(string source, params string[] terms)
		{
			int cursor = -1;
			for (int i = 0; i < terms.Length; i++)
			{
				int next = source.IndexOf(terms[i], cursor + 1, StringComparison.Ordinal);
				Assert.Greater(next, cursor, terms[i]);
				cursor = next;
			}
		}

		[Test]
		public void X1ToX4RefusedPublicationCannotReofferPhysicalBoundary()
		{
			string debit = Source("Growth/KingdomPurposePortfolio.EffectDebit.cs");
			string settled = Between(debit,
				"if (state == KingdomPurposeEffectAttemptState.Settled)",
				"if (state != KingdomPurposeEffectAttemptState.Before");
			Ordered(settled, "ExactPurposeEffectOffer", "NextStep = Operation.EffectStep + 1",
				"KingdomPurposeBodyDriveState.Applied");
			StringAssert.DoesNotContain("Destroy", settled);
			Ordered(debit, "StampPurposeEffectOffer(Context.Work, encoded)",
				"item.Destroy(null, Silent: true)", "ObservePurposeEffectDebit(Context, attempt");

			string product = Source("Growth/KingdomPurposePortfolio.EffectProductRuntime.cs");
			Ordered(product, "TryPurposeEffectProductCensus(Context, Operation",
				"if (census.AttemptPresent)", "RecoverPurposeEffectProductAttempt(",
				"int count = PurposeEffectProductCount(census, Role)",
				"if (count == Target)", "OfferPurposeEffectProduct(");
			string offered = Between(product,
				"KingdomPurposeEffectCallbackAftermath aftermath =",
				"if (aftermath == KingdomPurposeEffectCallbackAftermath.Unavailable");
			Ordered(offered, "ClassifyEffectProductAftermath", "Settled",
				"RecordReleaseAndClearPurposeEffectProduct", "Settled = true");
			string fault = Source("Growth/KingdomPurposePortfolio.EffectDriveHelpers.cs");
			string durable = Between(fault, "private static KingdomPurposeBodyDriveState FaultedEffect(",
				"private static bool TryExpectedEffectCallback(");
			StringAssert.Contains("StampPurposeEffectFault", durable);
			StringAssert.DoesNotContain("ClearPurposeEffect", durable);
		}

		[Test]
		public void X5ToX10TornCustodyMarkersAndRosterMutationsAlwaysRefuse()
		{
			string ground = Source("Growth/KingdomPurposePortfolio.EffectGround.cs");
			Ordered(ground, "workAttemptPresent", "PortfolioEffectReadyProperty",
				"PortfolioEffectOfferProperty", "bool itemAttempt", "bool itemMark",
				"itemAttempt && (!workAttemptPresent", "++carriers > 1");
			string debit = Source("Growth/KingdomPurposePortfolio.EffectDebitEvidence.cs");
			StringAssert.Contains("&& !AnyPurposeLandingField(Item)", debit);
			StringAssert.Contains("&& !HasProtectedCargoEvidence(Item)", debit);
			Ordered(debit, "Attempt.BeforeRosterDigest", "Attempt.AfterRosterDigest",
				"DebitCandidateAtFrozenAfter");

			string custody = Source("Growth/KingdomPurposePortfolio.LandingGround.cs");
			StringAssert.Contains("if (roots == null) return false;", custody);
			StringAssert.Contains("if (item.Inventory.Objects == null) return false;", custody);
			StringAssert.Contains("if (!GameObject.Validate(roots[i])", custody);
			StringAssert.Contains("!Store.CurrentCell.Objects.Contains(Store)", ground);
			string census = Source("Growth/KingdomPurposePortfolio.EffectProductCensus.cs");
			Ordered(census, "Recorded = recorded", "Refined = recorded.Refined",
				"Seed = recorded.Seed", "Staple = recorded.Staple");
			StringAssert.Contains("!attemptPresent && census.EvidenceCarrier != null", census);
		}

		[Test]
		public void X11ToX13PublishedAttemptRecoveryPrecedesFreshAdmissionAndComposes()
		{
			string debit = Source("Growth/KingdomPurposePortfolio.EffectDebit.cs");
			Ordered(debit, "if (present && attempt.Step < Operation.EffectStep)",
				"TryRetirePublishedEffectAttempt", "present = false", "if (!present)",
				"TryPurposeEffectDebitCensus");
			string product = Source("Growth/KingdomPurposePortfolio.EffectProductRuntime.cs");
			Ordered(product, "census.AttemptPresent && census.Attempt.Step < Operation.EffectStep",
				"TryRetirePublishedEffectAttempt", "continue;", "if (census.AttemptPresent)",
				"RecoverPurposeEffectProductAttempt", "continue;",
				"int count = PurposeEffectProductCount", "OfferPurposeEffectProduct");

			string debitRetirement = Source(
				"Growth/KingdomPurposePortfolio.EffectDebitRetirement.cs");
			Ordered(debitRetirement, "TryCapturePurposeEffectRoster",
				"Attempt.AfterRosterDigest", "ClearPurposeEffectAttempt(item, witness)",
				"ClearPurposeEffectOffer", "ClearPurposeEffectReady",
				"ClearPurposeEffectAttempt(Context.Work");
			string recovery = Source(
				"Growth/KingdomPurposePortfolio.EffectAttemptRecovery.cs");
			Ordered(recovery, "RecordPurposeEffectProducts(Context.Work",
				"TryReleasePurposeEffectProduct(Context", "ClearPurposeEffectReady",
				"ClearPurposeEffectAttempt(Context.Work");
		}

		[Test]
		public void X14ToX15HeadroomAndOrphanAuthoritiesPrecedeEffectDispatch()
		{
			string drive = Source("Growth/KingdomPurposePortfolio.OperationDrive.cs");
			string loop = Between(drive, "private static bool DrivePortfolioOperation(",
				"private static bool BeginLocalDebit(");
			Ordered(loop, "PairRevisionHeadroomIsValid(Published)",
				"Published.Revision == int.MaxValue || operation.Revision == int.MaxValue",
				"switch (operation.Phase)", "DrivePurposeEffect(before");
			string control = Source("Growth/KingdomPurposePortfolio.OperationControl.cs");
			string publish = Between(control, "private static bool TryPublishOperation(",
				"private static bool QuarantinePortfolio(");
			Ordered(publish, "Pair.Revision == int.MaxValue",
				"Pair.Operation.Revision == int.MaxValue", "KingdomPurposePairReceipt next = Pair.Copy()",
				"Pair.Phase == KingdomPurposePairPhase.Orphaned", "next.ResumePhase = PairPhase",
				"TryPublishPortfolioPair(Pair, next");
			string transitions = Source("Growth/KingdomPurposePortfolioRules.Transitions.cs");
			Ordered(transitions, "Before.Phase == KingdomPurposePairPhase.Orphaned",
				"After.Phase == KingdomPurposePairPhase.Orphaned",
				"AdvanceCommittedWhileOrphaned(Before, After)");
		}

		[Test]
		public void X16RuntimeRetirementSequencePinsExecutableComposition()
		{
			string effect = Source("Growth/KingdomPurposePortfolio.EffectRuntime.cs");
			Ordered(effect, "DriveManualPurposeEffect(context", "nextStep != operation.EffectStep + 1",
				"stepped.EffectStep = nextStep", "stepped.Revision++",
				"TryPublishOperation(Pair, stepped");
			string output = Source("Growth/KingdomPurposePortfolio.OutputRuntime.cs");
			Ordered(output, "TryRetireCompletedPurposeEffect(Pair", "next.Phase =",
				"KingdomPurposeOperationPhase.OutputPending", "TryPublishOperation(Pair, next");
			string retirement = Source("Growth/KingdomPurposePortfolio.EffectRetirement.cs");
			Ordered(retirement, "ExactPublishedPortfolioPair(Pair)",
				"CompletedPurposeEffectProductCount(operation, record)",
				"ClearPurposeEffectProducts(context.Work, receipt)",
				"Bounded-effect evidence survived terminal retirement");
		}

		[Test]
		public void X17RuntimeFinalProofAndCustodyOrderPinsRefusalSet()
		{
			string retirement = Source("Growth/KingdomPurposePortfolio.EffectRetirement.cs");
			Ordered(retirement, "operation.Phase != KingdomPurposeOperationPhase.EffectApplied",
				"ExactPublishedPortfolioPair(Pair)", "TryPurposeEffectContext(system, operation",
				"PurposeEffectEvidenceOnlyOnWorkOrProducts(context",
				"TryReadPurposeEffectAttempt", "TryReadPurposeEffectProducts",
				"attemptPresent || OwnedFieldPresent(context.Work, PortfolioEffectReadyProperty)",
				"PortfolioEffectOfferProperty", "PurposeEffectIsFaulted(context.Work)",
				"still has a protected physical product", "CompletedPurposeEffectProductCount",
				"ClearPurposeEffectProducts", "PurposeEffectEvidenceOnlyOnWorkOrProducts");
			string ground = Source("Growth/KingdomPurposePortfolio.EffectGround.cs");
			Ordered(ground, "FindExactKnown(Zone, StoreId", "ReferenceEquals(exact, Store)",
				"Store.CurrentCell.Objects.Contains(Store)", "TryLoadedLandingCustody(Context.Zone");
		}
	}
}
#endif
