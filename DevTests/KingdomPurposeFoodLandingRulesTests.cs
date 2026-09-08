#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomPurposeFoodLandingRulesTests
	{
		private const int Carried = 6;

		/// <summary>One lawful canonical landing receipt, reused so witness assertions can pin the
		/// exact encoded form rather than a shape.</summary>
		private static readonly string Receipt = Encoded();

		private static string Encoded()
		{
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryLandingReceipt("p16", 1L, "zMKi",
				out string receipt));
			return receipt;
		}

		/// <summary>The delimiter join the canonical encoding replaced. Reproduced here, not in
		/// production, because it is the defect under test rather than a behaviour to preserve.</summary>
		private static string NaiveJoin(string pairId, long epoch, string operationId)
		{
			return "purpose-landing:" + pairId + ":" + epoch + ":" + operationId;
		}

		[Test]
		public void RecoveryRefusesEveryClaimOutsideItsFrozenBounds()
		{
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryRecoverCarriedFood(0, 4, 4, true, 0,
				out var zero));
			ClassicAssert.AreEqual(KingdomPurposeFoodLandingAction.Refuse, zero);
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryRecoverCarriedFood(-1, 4, 4, true, 0,
				out _));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryRecoverCarriedFood(
				KingdomPurposePortfolioRules.MaxCarriedFood + 1, 4, 4, true, 0, out _));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryRecoverCarriedFood(Carried, -1, 4, true,
				0, out _));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryRecoverCarriedFood(Carried, 4, -1, true,
				0, out _));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryRecoverCarriedFood(Carried, 4, 4, true,
				-1, out _));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryRecoverCarriedFood(Carried, 4,
				4 + Carried + 1, true, Carried + 1, out _));
		}

		[Test]
		public void UnmarkedLardersTakeTheWholeCarriedAmountOnce()
		{
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryRecoverCarriedFood(Carried, 9, 9, true, 0,
				out var action));
			ClassicAssert.AreEqual(KingdomPurposeFoodLandingAction.Apply, action);
		}

		[Test]
		public void FullyMarkedLardersAreAlreadyAppliedAndNeverCreditedTwice()
		{
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryRecoverCarriedFood(Carried, 9,
				9 + Carried, true, Carried, out var action));
			ClassicAssert.AreEqual(KingdomPurposeFoodLandingAction.AlreadyApplied, action);
		}

		[Test]
		public void PartiallyMarkedLardersContinueForTheRemainderOnly()
		{
			for (int marked = 1; marked < Carried; marked++)
			{
				ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryRecoverCarriedFood(Carried, 9,
					9 + marked, true, marked, out var action), marked.ToString());
				ClassicAssert.AreEqual(KingdomPurposeFoodLandingAction.Continue, action,
					marked.ToString());
			}
		}

		[Test]
		public void ABrokenMarkerIsInterferenceEvenWhenTheServingsAgree()
		{
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryRecoverCarriedFood(Carried, 9,
				9 + Carried, false, Carried, out var full));
			ClassicAssert.AreEqual(KingdomPurposeFoodLandingAction.Interference, full);
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryRecoverCarriedFood(Carried, 9, 9, false,
				0, out var empty));
			ClassicAssert.AreEqual(KingdomPurposeFoodLandingAction.Interference, empty);
		}

		[Test]
		public void ADisagreementBetweenTheTwoPartitionsIsInterferenceRatherThanAReceipt()
		{
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryRecoverCarriedFood(Carried, 9, 10, true,
				0, out var gained));
			ClassicAssert.AreEqual(KingdomPurposeFoodLandingAction.Interference, gained);
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryRecoverCarriedFood(Carried, 9,
				8 + Carried, true, Carried, out var lost));
			ClassicAssert.AreEqual(KingdomPurposeFoodLandingAction.Interference, lost);
		}

		[Test]
		public void EveryCutPointConvergesOnExactlyOneLandingOfTheCarriedAmount()
		{
			const int before = 11;
			// `stride` is how many servings a retry manages before the next crash: 6 models one
			// clean pass, 1 models the worst case where every single serving is its own attempt,
			// so Continue is reached from Continue rather than only ever from Apply.
			for (int stride = 1; stride <= Carried; stride++)
				for (int cut = 0; cut <= Carried; cut++)
				{
					int marked = cut;
					int created = 0;
					int continues = 0;
					for (int retry = 0; retry <= Carried; retry++)
					{
						ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryRecoverCarriedFood(Carried,
							before, before + marked, true, marked, out var action),
							stride + "/" + cut);
						if (action == KingdomPurposeFoodLandingAction.AlreadyApplied) break;
						ClassicAssert.AreEqual(marked == 0 ? KingdomPurposeFoodLandingAction.Apply
							: KingdomPurposeFoodLandingAction.Continue, action,
							stride + "/" + cut + "/" + retry);
						if (action == KingdomPurposeFoodLandingAction.Continue) continues++;
						int step = Math.Min(stride, Carried - marked);
						created += step;
						marked += step;
					}
					ClassicAssert.AreEqual(Carried, marked, stride + "/" + cut);
					ClassicAssert.AreEqual(Carried - cut, created, stride + "/" + cut);
					if (stride == 1 && cut == 0)
						ClassicAssert.AreEqual(Carried - 1, continues,
							"a one-serving-per-attempt landing must reach Continue from Continue");
				}
		}

		[Test]
		public void ALandingReceiptIsCanonicalAndNamesPairEpochAndOperationInInvariantDigits()
		{
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryLandingReceipt("p16", 1L, "zMKi",
				out string receipt));
			ClassicAssert.AreEqual("pv1;15:purpose-landing;3:p16;1:1;4:zMKi", receipt);
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryLandingReceipt("p16", 1234567L, "zMKi",
				out string otherEpoch));
			ClassicAssert.AreNotEqual(receipt, otherEpoch, "the pair epoch is part of the identity");
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryLandingReceipt("p17", 1L, "zMKi",
				out string otherPair));
			ClassicAssert.AreNotEqual(receipt, otherPair, "the pair id is part of the identity");
		}

		[Test]
		public void DistinctTuplesThatCollideUnderNaiveJoiningAreSeparatedByTheCanonicalForm()
		{
			// Id() admits ':', so these two distinct tuples produce one string under a plain join.
			ClassicAssert.AreEqual(NaiveJoin("a", 1L, "2:b"), NaiveJoin("a:1", 2L, "b"),
				"this is the defect the canonical encoding exists to remove");
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryLandingReceipt("a", 1L, "2:b",
				out string first));
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryLandingReceipt("a:1", 2L, "b",
				out string second));
			ClassicAssert.AreNotEqual(first, second,
				"two distinct operations must never share a landing identity, or one reads the"
				+ " other's provision as its own");
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryCargoRootBody("a", 1L, "2:b",
				out string firstRoot));
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryCargoRootBody("a:1", 2L, "b",
				out string secondRoot));
			ClassicAssert.AreNotEqual(firstRoot, secondRoot,
				"the rooted cargo key carries the same collision and the same fix");
			ClassicAssert.AreNotEqual(first, firstRoot, "landing and cargo-root keys are separate spaces");
		}

		[Test]
		public void AZeroCheapIndexIsNormalisedRatherThanRefused()
		{
			// A 31-bit FNV can return zero for a lawful receipt: under the old delimiter form the
			// tuple (p16, 1, zMKi) did exactly that, and the operation was refused outright.
			ClassicAssert.AreNotEqual(0, KingdomPurposePortfolioRules.LandingIndex(0),
				"a lawful receipt whose index hashes to zero must still be able to own a mark");
			ClassicAssert.AreEqual(1, KingdomPurposePortfolioRules.LandingIndex(0),
				"the sentinel is fixed, so one receipt normalises the same way on every pass");
			ClassicAssert.AreEqual(7, KingdomPurposePortfolioRules.LandingIndex(7),
				"a nonzero index is passed through untouched");
			ClassicAssert.AreEqual(int.MaxValue,
				KingdomPurposePortfolioRules.LandingIndex(int.MaxValue));
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.LandingMarkerIsOurs(Receipt,
				KingdomPurposePortfolioRules.LandingIndex(0), true,
				KingdomPurposePortfolioRules.LandingIndex(0), true, Receipt),
				"the normalised index must be usable as a real mark");
		}

		[Test]
		public void AMalformedIdentityIsRefusedAndNeverPartiallyReturned()
		{
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryLandingReceipt(null, 1L, "op",
				out string none));
			ClassicAssert.IsNull(none);
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryLandingReceipt("pair", 0L, "op", out _));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryLandingReceipt("pair", -1L, "op",
				out _));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryLandingReceipt("pair", 1L, "", out _));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryLandingReceipt("pair", 1L, null, out _));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryLandingReceipt(" pair", 1L, "op", out _),
				"an untrimmed id is not an exact id");
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryCargoRootBody("pair", 1L, null, out _));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryCargoRootBody("pair", 0L, "op", out _));
		}

		[Test]
		public void TheCanonicalIdentityStaysInsideTheSharedReceiptBound()
		{
			string widest = new string('p', 256);
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryLandingReceipt(widest, long.MaxValue,
				widest, out string receipt), "the lawful worst case must still encode");
			ClassicAssert.LessOrEqual(receipt.Length, KingdomPurposePortfolioRules.MaxReceiptChars);
			ClassicAssert.AreEqual(19, long.MaxValue.ToString(
				System.Globalization.CultureInfo.InvariantCulture).Length);
		}

		[Test]
		public void RetirementMatchesOneWholeMarkAndNeverAPrefixOfThePairNamespace()
		{
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryLandingReceipt("a", 1L, "op-1",
				out string retired));
			ClassicAssert.IsTrue(Retirable(retired, 7, true, 7, true, retired));
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryLandingReceipt("a", 1L, "op-2",
				out string sibling));
			ClassicAssert.IsTrue(sibling.StartsWith("pv1;15:purpose-landing;1:a;1:1",
				StringComparison.Ordinal), "the sibling really is inside the old prefix");
			foreach (string other in new string[] { sibling, retired + "x",
				"pv1;15:purpose-landing", "not-a-receipt", "", null })
				ClassicAssert.IsFalse(Retirable(retired, 7, true, 7, true, other),
					"a prefix match would erase evidence this operation cannot name: "
					+ (other ?? "null"));
			ClassicAssert.IsFalse(Retirable(null, 7, true, 7, true, retired),
				"nothing is retired against an absent receipt");
			ClassicAssert.IsFalse(Retirable("", 7, true, 7, true, retired));
			// RetirementPreservesWrongPrefilterSameReceipt: the receipt text alone is not the mark.
			ClassicAssert.IsFalse(Retirable(retired, 7, true, 8, true, retired),
				"a mark whose index disagrees is half-bound evidence and must survive");
			// RetirementPreservesMissingPrefilterSameReceipt.
			ClassicAssert.IsFalse(Retirable(retired, 7, false, 0, true, retired),
				"a mark with no index at all names nobody and must survive");
			ClassicAssert.IsFalse(Retirable(retired, 7, true, 7, false, null),
				"a mark with no receipt property is not this receipt");
			ClassicAssert.IsFalse(Retirable(retired, 0, true, 0, true, retired),
				"an unnormalised index proves nothing and retires nothing");
		}

		[Test]
		public void PresenceIsThePropertyExistingAndNeverItsValue()
		{
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryLandingReceipt("pair-1", 3L, "op-a",
				out string ours));
			// EmptyReceiptPropertyIsInterference: an emptied stamp is a torn mark, not clean food.
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.LandingMarkerIsPresent(false, true),
				"a present but empty stamp is still a mark");
			ClassicAssert.IsFalse(Ours(ours, 7, false, 0, true, ""),
				"an emptied stamp can never be claimed as ours either");
			// ZeroPrefilterPropertyIsInterference: a zeroed index is a torn mark, not clean food.
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.LandingMarkerIsPresent(true, false),
				"a present but zero index is still a mark");
			ClassicAssert.IsFalse(Ours(ours, 7, true, 0, false, null));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.LandingMarkerIsPresent(false, false),
				"only the absence of both properties is unmarked food");
			// A mark is ours only with both halves present and agreeing.
			ClassicAssert.IsTrue(Ours(ours, 7, true, 7, true, ours));
			ClassicAssert.IsFalse(Ours(ours, 7, false, 7, true, ours),
				"a value without its property is not a mark this lane ever wrote");
			ClassicAssert.IsFalse(Ours(ours, 7, true, 7, false, ours));
		}

		[Test]
		public void AMarkWhoseIndexCollidesButWhoseIdentityDiffersIsNeverOurs()
		{
			const int shared = 987654321;
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryLandingReceipt("pair-1", 3L, "op-a",
				out string ours));
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryLandingReceipt("pair-1", 3L, "op-b",
				out string theirs));
			ClassicAssert.AreNotEqual(ours, theirs);
			ClassicAssert.IsFalse(Ours(ours, shared, true, shared, true, theirs),
				"an identical index must not make another operation's mark ours");
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.LandingMarkerIsPresent(true, true));
			ClassicAssert.IsTrue(Ours(ours, shared, true, shared, true, ours));
		}

		[Test]
		public void AForeignOrUnattributableMarkIsNeverRetiredAndAlwaysForcesInterference()
		{
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryLandingReceipt("pair-1", 3L, "op-a",
				out string ours));
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryLandingReceipt("pair-9", 3L, "op-a",
				out string otherPair));
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryLandingReceipt("pair-1", 4L, "op-a",
				out string otherEpoch));
			foreach (string foreign in new string[] { otherPair, otherEpoch, "not-a-receipt" })
			{
				ClassicAssert.IsFalse(Retirable(ours, 5, true, 5, true, foreign),
					"erasing a mark of unknown ownership would normalise it into availability: "
					+ foreign);
				ClassicAssert.IsFalse(Ours(ours, 5, true, 5, true, foreign));
				ClassicAssert.IsTrue(KingdomPurposePortfolioRules.LandingMarkerIsPresent(true, true),
					"a surviving foreign mark must read as present so the verdict cuts on it");
			}
			ClassicAssert.IsFalse(Retirable(ours, 5, true, 5, false, null),
				"an index with no receipt names nobody and must never be quietly erased");
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.LandingMarkerIsPresent(true, false));
		}

		[Test]
		public void AMarkWithTheRightIdentityButTheWrongIndexIsNotOursAndStaysPresent()
		{
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryLandingReceipt("pair-1", 3L, "op-a",
				out string ours));
			ClassicAssert.IsFalse(Ours(ours, 11, true, 12, true, ours));
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.LandingMarkerIsPresent(true, true));
		}

		[Test]
		public void AZeroIndexIsNeverAnIdentityAndAnUnmarkedServingIsNeverPresent()
		{
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryLandingReceipt("pair-1", 3L, "op-a",
				out string ours));
			ClassicAssert.IsFalse(Ours(ours, 0, true, 0, true, ours),
				"a zero index is indistinguishable from unmarked and cannot prove ownership");
			ClassicAssert.IsFalse(Ours(null, 7, true, 7, true, null));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.LandingMarkerIsPresent(false, false));
		}

		[Test]
		public void WrongTypeAttemptPropertyRemainsAmbiguous()
		{
			// The presence law, applied to the witness: a value standing on the attempt name under
			// the wrong type table, or under both at once, is a torn witness and never no offer.
			ClassicAssert.AreEqual(KingdomPurposeLandingAttemptState.Ambiguous,
				KingdomPurposePortfolioRules.ClassifyLandingAttempt(true, false, 0, 0, true));
			ClassicAssert.AreEqual(KingdomPurposeLandingAttemptState.Ambiguous,
				KingdomPurposePortfolioRules.ClassifyLandingWitnesses(false, true, false, 0, 3,
					true));
		}

		[Test]
		public void AStandingFaultOutranksEveryOtherReadingOfTheGround()
		{
			// The composition the separate tables miss: a callback that throws after placing the
			// exact unit leaves a ground the attempt alone calls Settled. Composed, it is a fault.
			ClassicAssert.AreEqual(KingdomPurposeLandingAttemptState.Settled,
				KingdomPurposePortfolioRules.ClassifyLandingAttempt(true, true, 3, 3, true),
				"this is the reading the fault witness exists to override");
			// ThrownAfterExactPlacementStaysFaultedAfterRefusedCas.
			ClassicAssert.AreEqual(KingdomPurposeLandingAttemptState.Ambiguous,
				KingdomPurposePortfolioRules.ClassifyLandingWitnesses(true, true, true, 3, 3, true));
			// FinalPartitionFaultStaysFaultedAfterRefusedCas: the partition failed, the fault was
			// stamped, and no later ground reading can retire it.
			for (int observed = 0; observed <= Carried; observed++)
				foreach (bool exact in new bool[] { true, false })
					ClassicAssert.AreEqual(KingdomPurposeLandingAttemptState.Ambiguous,
						KingdomPurposePortfolioRules.ClassifyLandingWitnesses(true, false, false, 0,
							observed, exact), observed + "/" + exact);
			// CallbackStripsAttemptWitnessBecomesPersistentFault: with the attempt gone the
			// composed verdict is still ambiguous, because the fault is what carries the evidence.
			ClassicAssert.AreEqual(KingdomPurposeLandingAttemptState.Ambiguous,
				KingdomPurposePortfolioRules.ClassifyLandingWitnesses(true, false, false, 0, 3,
					true));
			// TerminalFaultCannotAutoReconcile: no combination of inputs clears a standing fault.
			for (int mask = 0; mask < 8; mask++)
				ClassicAssert.AreEqual(KingdomPurposeLandingAttemptState.Ambiguous,
					KingdomPurposePortfolioRules.ClassifyLandingWitnesses(true, (mask & 1) != 0,
						(mask & 2) != 0, 1, 1, (mask & 4) != 0), mask.ToString());
			// Only with no fault does the attempt decide.
			ClassicAssert.AreEqual(KingdomPurposeLandingAttemptState.Clear,
				KingdomPurposePortfolioRules.ClassifyLandingWitnesses(false, false, false, 0, 0,
					true));
		}

		[Test]
		public void AFaultIsStampableForEveryFigureIncludingForgedAndExcessEvidence()
		{
			// ExcessMarksFaultPersistsAfterMarksRemovedAndCasRefused: the ambiguity that most needs
			// a durable fault is forged or excess evidence, so an out-of-bounds diagnostic figure
			// folds onto an explicit sentinel rather than refusing the stamp.
			ClassicAssert.AreEqual(KingdomPurposePortfolioRules.MaxCarriedFood + 1,
				KingdomPurposePortfolioRules.OverBoundLandingFigure);
			ClassicAssert.AreEqual(KingdomPurposePortfolioRules.OverBoundLandingFigure,
				KingdomPurposePortfolioRules.LandingFaultFigure(Carried + 1));
			ClassicAssert.AreEqual(KingdomPurposePortfolioRules.OverBoundLandingFigure,
				KingdomPurposePortfolioRules.LandingFaultFigure(int.MaxValue));
			ClassicAssert.AreEqual(KingdomPurposePortfolioRules.OverBoundLandingFigure,
				KingdomPurposePortfolioRules.LandingFaultFigure(-1));
			ClassicAssert.AreEqual(4, KingdomPurposePortfolioRules.LandingFaultFigure(4));
			ClassicAssert.AreEqual(0, KingdomPurposePortfolioRules.LandingFaultFigure(0));
			for (int expected = 0; expected <= KingdomPurposePortfolioRules.OverBoundLandingFigure;
				expected++)
				for (int observed = 0;
					observed <= KingdomPurposePortfolioRules.OverBoundLandingFigure; observed++)
					ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryLandingFault(Receipt,
						KingdomPurposePortfolioRules.LandingFaultFigure(expected),
						KingdomPurposePortfolioRules.LandingFaultFigure(observed), out _),
						expected + "/" + observed);
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryLandingFault(null, 0, 0, out string none));
			ClassicAssert.IsNull(none);
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryLandingFault(Receipt, 0, 0,
				out string clean));
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryLandingFault(Receipt, 1, 0,
				out string other));
			ClassicAssert.AreNotEqual(clean, other, "the step a fault was judging is part of it");
		}

		private static bool Ours(string receipt, int prefilter, bool markPresent, int markPrefilter,
			bool stampPresent, string markReceipt)
		{
			return KingdomPurposePortfolioRules.LandingMarkerIsOurs(receipt, prefilter, markPresent,
				markPrefilter, stampPresent, markReceipt);
		}

		private static bool Retirable(string retired, int prefilter, bool markPresent,
			int markPrefilter, bool stampPresent, string markReceipt)
		{
			return KingdomPurposePortfolioRules.LandingMarkerIsRetiredReceipt(retired, prefilter,
				markPresent, markPrefilter, stampPresent, markReceipt);
		}

		[Test]
		public void DurableProgressStopsAConsumedLandingFromMintingReplacements()
		{
			// Six landed and recorded; a settler then eats two. The physical count is 4, but the
			// operation owes nothing: re-creating two would be minting food out of an appetite.
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryLandingOutstanding(Carried, 4, Carried,
				out int outstanding, out int progress));
			ClassicAssert.AreEqual(0, outstanding);
			ClassicAssert.AreEqual(Carried, progress);
			// A save cut between creating servings and recording them leaves the record low; the
			// physical marks lead there, and the operation finishes the remainder exactly once.
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryLandingOutstanding(Carried, 3, 0,
				out int afterCut, out int cutProgress));
			ClassicAssert.AreEqual(3, afterCut);
			ClassicAssert.AreEqual(3, cutProgress);
			// Partial landing and partial consumption together still owe only the true remainder.
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryLandingOutstanding(Carried, 2, 4,
				out int mixed, out int mixedProgress));
			ClassicAssert.AreEqual(2, mixed);
			ClassicAssert.AreEqual(4, mixedProgress);
		}

		[Test]
		public void ProgressNeverDecreasesAndForgedMarksAreRefused()
		{
			int record = 0;
			int[] physical = { 2, 5, 1, 4, 0, 6 };
			for (int i = 0; i < physical.Length; i++)
			{
				ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryLandingOutstanding(Carried,
					physical[i], record, out int outstanding, out int progress), i.ToString());
				ClassicAssert.GreaterOrEqual(progress, record, "progress must never decrease");
				ClassicAssert.AreEqual(Carried - progress, outstanding, i.ToString());
				record = progress;
			}
			ClassicAssert.AreEqual(Carried, record);
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryLandingOutstanding(Carried, Carried + 1,
				0, out _, out _), "more marks than the row ever carried is forgery, not progress");
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryLandingOutstanding(Carried, 0,
				Carried + 1, out _, out _));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryLandingOutstanding(Carried, -1, 0,
				out _, out _));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryLandingOutstanding(0, 0, 0, out _,
				out _));
		}

		[Test]
		public void AServingNeverOfferedIsAShortfallAndEveryOtherDivergenceIsStranded()
		{
			// A serving the engine never saw cannot be stranded, whatever else is false.
			for (int mask = 0; mask < 128; mask++)
				ClassicAssert.AreEqual(KingdomPurposeServingAftermath.Unavailable,
					KingdomPurposePortfolioRules.ClassifyServingAftermath(false,
						(mask & 1) != 0, (mask & 2) != 0, (mask & 4) != 0, (mask & 8) != 0,
						(mask & 16) != 0, (mask & 32) != 0, (mask & 64) != 0), mask.ToString());
			ClassicAssert.AreEqual(KingdomPurposeServingAftermath.Settled,
				KingdomPurposePortfolioRules.ClassifyServingAftermath(true, false, true, true,
					true, true, true, true));
			// Every single observation is load-bearing: drop any one and the aftermath is stranded.
			string[] names = { "threw", "different object", "invalid", "not in target larder",
				"not whole", "wrong content", "marker gone" };
			for (int i = 0; i < names.Length; i++)
			{
				// Index 0 is `Threw`, whose bad value is true; the rest are proofs whose bad
				// value is false. Exactly one observation is spoiled per pass.
				bool[] f = { false, true, true, true, true, true, true };
				f[i] = i == 0;
				ClassicAssert.AreEqual(KingdomPurposeServingAftermath.Stranded,
					KingdomPurposePortfolioRules.ClassifyServingAftermath(true, f[0], f[1], f[2],
						f[3], f[4], f[5], f[6]), names[i]);
			}
		}

		[Test]
		public void EverySettledOfferOwesAnExactIncrementInBothHalvesOfThePartition()
		{
			// The envelope [before, before + attempted] is not the law any more. Every precondition
			// the engine could refuse on is proved before a serving is offered, so a settled offer
			// owes exactly one marked serving and leaves the unmarked half alone.
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.LandingPartitionIsExact(4, 9, 0, 4, 9, true));
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.LandingPartitionIsExact(0, 0, 6, 6, 0, true));
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.LandingPartitionIsExact(2, 5, 3, 5, 5, true));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.LandingPartitionIsExact(2, 5, 3, 4, 5, true),
				"a short delta is a callback moving an earlier mark while the latest settled, not a"
				+ " shortfall to retry: retrying over it mints around servings that exist");
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.LandingPartitionIsExact(2, 5, 3, 6, 5, true),
				"more marked servings than were offered cannot have come from this landing");
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.LandingPartitionIsExact(2, 5, 3, 5, 4, true),
				"the unmarked half must be untouched; food that vanished from it is interference");
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.LandingPartitionIsExact(2, 5, 3, 5, 6, true),
				"a marked serving that lost its stamp reappears as unmarked and is not a landing");
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.LandingPartitionIsExact(2, 5, 3, 5, 5, false),
				"an inexact mark anywhere is ownership this operation cannot account for");
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.LandingPartitionIsExact(-1, 0, 0, -1, 0,
				true));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.LandingPartitionIsExact(0, -1, 0, 0, -1,
				true));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.LandingPartitionIsExact(0, 0, -1, 0, 0,
				true));
		}

		[Test]
		public void ALandingRecordIsAReceiptAndACountTogetherOrItIsNobodysRecord()
		{
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryLandingRecord(false, false, false, 0,
				Carried, out int clean), "neither property present is simply a clean cargo");
			ClassicAssert.AreEqual(0, clean);
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryLandingRecord(true, true, true, 4,
				Carried, out int ours));
			ClassicAssert.AreEqual(4, ours);
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryLandingRecord(true, false, true, 4,
				Carried, out int foreign),
				"a record another operation stamped must never read as zero, or the next write"
				+ " erases it and takes its progress");
			ClassicAssert.AreEqual(0, foreign, "a refused reading returns no figure at all");
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryLandingRecord(false, false, true, 3,
				Carried, out _), "a count under no receipt names nobody");
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryLandingRecord(true, true, false, 0,
				Carried, out _), "a stamp with no count is half a record");
			// ZeroCountPropertyIsTornRecord: the count property exists and reads zero. Presence is
			// the property, so this is a torn record, never a clean cargo at zero progress.
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryLandingRecord(true, true, true, 0,
				Carried, out _), "a present count of zero is torn, not clean");
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryLandingRecord(false, false, true, 0,
				Carried, out _), "a present count under no stamp is torn, not clean");
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryLandingRecord(true, true, true,
				Carried + 1, Carried, out _), "more than the row carries is forgery, not progress");
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryLandingRecord(true, true, true, -1,
				Carried, out _));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryLandingRecord(true, true, true, 1, 0,
				out _));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryLandingRecord(true, true, true, 1,
				KingdomPurposePortfolioRules.MaxCarriedFood + 1, out _));
		}

		[Test]
		public void AFallenMarkedCountIsConsumptionOnlyWhenNothingStillWearsTheReceipt()
		{
			// The two ways a record can outrun its marks are different events. A settler ate the
			// provision: nothing wears the receipt, the operation owes nothing, and no serving is
			// minted. A callback carried a marked serving off: the mark survives somewhere else,
			// and calling that consumption would publish Delivered over provision never kept.
			ClassicAssert.AreEqual(KingdomPurposeLandingRecordState.Consumed,
				KingdomPurposePortfolioRules.ClassifyLandingRecord(4, Carried, 0));
			ClassicAssert.AreEqual(KingdomPurposeLandingRecordState.Consumed,
				KingdomPurposePortfolioRules.ClassifyLandingRecord(0, Carried, 0));
			ClassicAssert.AreEqual(KingdomPurposeLandingRecordState.Stranded,
				KingdomPurposePortfolioRules.ClassifyLandingRecord(4, Carried, 1),
				"a surviving moved mark is never consumption");
			ClassicAssert.AreEqual(KingdomPurposeLandingRecordState.Intact,
				KingdomPurposePortfolioRules.ClassifyLandingRecord(Carried, Carried, 0));
			ClassicAssert.AreEqual(KingdomPurposeLandingRecordState.Intact,
				KingdomPurposePortfolioRules.ClassifyLandingRecord(3, 0, 0),
				"marks ahead of the record is the save cut between creating and recording");
			ClassicAssert.AreEqual(KingdomPurposeLandingRecordState.Stranded,
				KingdomPurposePortfolioRules.ClassifyLandingRecord(Carried, Carried, 1),
				"a stray mark cuts even when the surviving count still covers the record");
			ClassicAssert.AreEqual(KingdomPurposeLandingRecordState.Stranded,
				KingdomPurposePortfolioRules.ClassifyLandingRecord(3, 0, 2),
				"a nested copy is stranded evidence however the counts happen to sit");
			ClassicAssert.AreEqual(KingdomPurposeLandingRecordState.Invalid,
				KingdomPurposePortfolioRules.ClassifyLandingRecord(-1, 0, 0));
			ClassicAssert.AreEqual(KingdomPurposeLandingRecordState.Invalid,
				KingdomPurposePortfolioRules.ClassifyLandingRecord(0, -1, 0));
			ClassicAssert.AreEqual(KingdomPurposeLandingRecordState.Invalid,
				KingdomPurposePortfolioRules.ClassifyLandingRecord(0, 0, -1),
				"a scan that could not be completed is not a clean scan");
		}

		[Test]
		public void ARootEntryIsRemovedOnlyWhenItsValueReprovesThisExactCargo()
		{
			// Root keys share one namespace: the legacy delimiter form can be named by a different
			// pair/epoch/operation tuple, so a blind delete drops another operation's live root.
			for (int mask = 0; mask < 16; mask++)
			{
				bool rooted = (mask & 1) != 0;
				bool same = (mask & 2) != 0;
				bool valid = (mask & 4) != 0;
				bool reproves = (mask & 8) != 0;
				ClassicAssert.AreEqual(rooted && same && (!valid || reproves),
					KingdomPurposePortfolioRules.RootEntryIsRetirable(rooted, same, valid, reproves),
					mask.ToString());
			}
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.RootEntryIsRetirable(true, true, true, true),
				"the live cargo this receipt names leaves with its receipt");
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.RootEntryIsRetirable(true, true, false,
				false), "an obliterated cargo leaves nothing behind but a stale key");
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.RootEntryIsRetirable(true, false, true,
				true), "a foreign object under a colliding legacy key survives untouched");
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.RootEntryIsRetirable(true, true, true,
				false), "a live object that no longer reproves its receipt is not this cargo");
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.RootEntryIsRetirable(false, true, false,
				true), "a value that is not an object at all is somebody else's entry");
		}

		[Test]
		public void EveryPhaseTheDriveCanReachIsCommittedRecoveryAndProceedsUnderPause()
		{
			// Doctrine: refuse new work while paused, allow exact committed recovery. Each of these
			// is a phase DrivePortfolioOperation dispatches on, so a pause must not strand it.
			KingdomPurposeOperationPhase[] committed =
			{
				KingdomPurposeOperationPhase.Prepared,
				KingdomPurposeOperationPhase.InputDebitPending,
				KingdomPurposeOperationPhase.InputDebited,
				KingdomPurposeOperationPhase.LocalDebitPending,
				KingdomPurposeOperationPhase.LocalDebited,
				KingdomPurposeOperationPhase.EffectPending,
				KingdomPurposeOperationPhase.EffectApplied,
				KingdomPurposeOperationPhase.OutputPending,
				KingdomPurposeOperationPhase.Dispatching,
				KingdomPurposeOperationPhase.PickupComplete,
				KingdomPurposeOperationPhase.LandingPending,
				KingdomPurposeOperationPhase.Delivered
			};
			for (int i = 0; i < committed.Length; i++)
				ClassicAssert.IsTrue(KingdomPurposePortfolioRules.OperationPhaseIsCommitted(committed[i]),
					committed[i] + " is a committed hop and must finish while paused");
			KingdomPurposeOperationPhase[] uncommitted =
			{
				KingdomPurposeOperationPhase.Invalid,
				KingdomPurposeOperationPhase.Acknowledged,
				KingdomPurposeOperationPhase.Quarantined
			};
			for (int i = 0; i < uncommitted.Length; i++)
				ClassicAssert.IsFalse(
					KingdomPurposePortfolioRules.OperationPhaseIsCommitted(uncommitted[i]),
					uncommitted[i] + " is not committed work and still consults the master gate");
			ClassicAssert.AreEqual(committed.Length + uncommitted.Length,
				Enum.GetValues(typeof(KingdomPurposeOperationPhase)).Length,
				"a new phase must be classified as committed or not before it can be driven");
		}

		[Test]
		public void LandingIsProvedOnlyWithAnExactRecordAndNeverInferred()
		{
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.LandingIsProved(true, Carried, Carried));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.LandingIsProved(false, Carried, Carried),
				"a legacy delivery carries no record and must be reported unknown, not landed");
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.LandingIsProved(true, Carried - 1, Carried),
				"a short record does not prove the carriage landed");
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.LandingIsProved(true, 0, 0),
				"a row that carries nothing is not applicable rather than proved");
		}

		[Test]
		public void NoTwoDistinctOperationsCanShareALandingReceipt()
		{
			string[] pairs = { "pair-1", "pair-2" };
			long[] epochs = { 1L, 2L };
			string[] operations = { "op-1", "op-2" };
			HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
			for (int p = 0; p < pairs.Length; p++)
				for (int e = 0; e < epochs.Length; e++)
					for (int o = 0; o < operations.Length; o++)
					{
						ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryLandingReceipt(pairs[p],
							epochs[e], operations[o], out string receipt));
						ClassicAssert.IsTrue(seen.Add(receipt),
							"dropping any component of the identity lets a later operation read a"
							+ " retired provision as its own receipt, answer AlreadyApplied, land"
							+ " nothing, and still publish Delivered: " + receipt);
					}
			ClassicAssert.AreEqual(pairs.Length * epochs.Length * operations.Length, seen.Count);
		}

		[Test]
		public void TheArrivalFigureIsTheRowAmountAndCannotAccumulateAcrossRetries()
		{
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryCarriedFood(KingdomPurposeKind.Harvest,
				KingdomPurposeKind.Forge, out _, out int first, out _));
			ClassicAssert.AreEqual(Carried, first);
			for (int retry = 0; retry < 4; retry++)
			{
				ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryCarriedFood(
					KingdomPurposeKind.Harvest, KingdomPurposeKind.Forge, out _, out int again,
					out _));
				ClassicAssert.AreEqual(first, again,
					"the arrival figure is a property of the frozen row, so no sequence of retries"
					+ " can make the founder's total grow past one operation's carriage");
			}
		}

		[Test]
		public void CarriageAccountingDisclosesTheTwoServingLossOnBothHarvestRows()
		{
			AssertCarriage(KingdomPurposeKind.Harvest, KingdomPurposeKind.Forge, 8, 6, 2);
			AssertCarriage(KingdomPurposeKind.Harvest, KingdomPurposeKind.Flesh, 8, 6, 2);
		}

		[Test]
		public void NoRowLandsMoreThanItDebitedOrMoreThanTheOperationBound()
		{
			ClassicAssert.AreEqual(6, KingdomPurposePortfolioRules.MaxCarriedFood);
			IList<KingdomPurposePortfolioRecipe> rows =
				KingdomPurposePortfolioRules.AllRecipes();
			int carrying = 0;
			for (int i = 0; i < rows.Count; i++)
			{
				KingdomPurposePortfolioRecipe row = rows[i];
				ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryCarriedFood(row.Source,
					row.Destination, out int debited, out int landed, out int lost), row.CargoKey);
				ClassicAssert.AreEqual(row.FoodServings, debited, row.CargoKey);
				ClassicAssert.AreEqual(row.CarriedFood, landed, row.CargoKey);
				ClassicAssert.AreEqual(debited, landed + lost, row.CargoKey);
				ClassicAssert.GreaterOrEqual(landed, 0, row.CargoKey);
				ClassicAssert.LessOrEqual(landed, KingdomPurposePortfolioRules.MaxCarriedFood,
					row.CargoKey);
				ClassicAssert.LessOrEqual(landed, debited, row.CargoKey);
				if (landed <= 0) continue;
				carrying++;
				ClassicAssert.AreEqual(KingdomPurposeKind.Harvest, row.Source, row.CargoKey);
			}
			ClassicAssert.AreEqual(2, carrying);
		}

		[Test]
		public void RowsThatCarryNothingLandNothingAndAreRefusedByRecovery()
		{
			AssertCarriage(KingdomPurposeKind.Deep, KingdomPurposeKind.Forge, 0, 0, 0);
			AssertCarriage(KingdomPurposeKind.Flesh, KingdomPurposeKind.Harvest, 4, 0, 4);
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryRecoverCarriedFood(0, 5, 5, true, 0,
				out var action));
			ClassicAssert.AreEqual(KingdomPurposeFoodLandingAction.Refuse, action);
		}

		[Test]
		public void EveryCallbackAftermathClassQuarantinesAndOnlyRoomShortageWaits()
		{
			// One row per class the engine's post-placement callbacks can produce. Each is the
			// observation tuple the placement measures, so the classification is provable without
			// an engine. Only a serving never offered is a clean shortfall; everything else strands.
			AssertAftermath("rejected outright", KingdomPurposeServingAftermath.Stranded,
				true, false, false, true, false, true, true, true);
			AssertAftermath("moved out of the target larder",
				KingdomPurposeServingAftermath.Stranded,
				true, false, true, true, false, true, true, true);
			AssertAftermath("replaced by another object",
				KingdomPurposeServingAftermath.Stranded,
				true, false, false, true, true, true, true, true);
			AssertAftermath("mutated to a different count",
				KingdomPurposeServingAftermath.Stranded,
				true, false, true, true, true, false, true, true);
			AssertAftermath("mutated to different content",
				KingdomPurposeServingAftermath.Stranded,
				true, false, true, true, true, true, false, true);
			AssertAftermath("stripped of its stock mark", KingdomPurposeServingAftermath.Stranded,
				true, false, true, true, true, true, false, true);
			AssertAftermath("stripped of its landing mark", KingdomPurposeServingAftermath.Stranded,
				true, false, true, true, true, true, true, false);
			AssertAftermath("obliterated after placement", KingdomPurposeServingAftermath.Stranded,
				true, false, true, false, false, false, false, false);
			AssertAftermath("threw after placement", KingdomPurposeServingAftermath.Stranded,
				true, true, true, true, true, true, true, true);
			AssertAftermath("never offered at all", KingdomPurposeServingAftermath.Unavailable,
				false, false, false, false, false, false, false, false);
			AssertAftermath("settled whole and in place", KingdomPurposeServingAftermath.Settled,
				true, false, true, true, true, true, true, true);
		}

		[Test]
		public void AnUnreconciledOfferStaysAmbiguousHoweverManyPassesTheQuarantineCosts()
		{
			// The witness is written on the durable cargo BEFORE a serving is handed to any
			// inventory, so it outlives the serving. A callback that obliterates the object, a
			// save cut, and a refused quarantine publication all leave it standing, and until it
			// reconciles exactly, the transaction offers nothing further.
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryLandingAttempt(Receipt, 3,
				out string witness));
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryReadLandingAttempt(witness, Receipt,
				out int expected));
			ClassicAssert.AreEqual(3, expected);
			// refused CAS + obliteration: the serving is gone, so the count never reached the
			// promised increment. It may never be read as a clean ground and landed again.
			ClassicAssert.AreEqual(KingdomPurposeLandingAttemptState.Ambiguous,
				KingdomPurposePortfolioRules.ClassifyLandingAttempt(true, true, 3, 2, true),
				"an obliterated serving is exactly what the witness exists to remember");
			// save cut before the offer: identical reading, and the same conservative refusal.
			ClassicAssert.AreEqual(KingdomPurposeLandingAttemptState.Ambiguous,
				KingdomPurposePortfolioRules.ClassifyLandingAttempt(true, true, 3, 2, true));
			// moved, nested or replaced: the count may even reach the promise, but the partition
			// comes back inexact, and an inexact partition never reconciles a witness.
			ClassicAssert.AreEqual(KingdomPurposeLandingAttemptState.Ambiguous,
				KingdomPurposePortfolioRules.ClassifyLandingAttempt(true, true, 3, 3, false));
			// mutated or overshooting: more than the promised one-step increment is not recovery.
			ClassicAssert.AreEqual(KingdomPurposeLandingAttemptState.Ambiguous,
				KingdomPurposePortfolioRules.ClassifyLandingAttempt(true, true, 3, 4, true));
			// a settled callback whose witness was not yet cleared is the one recoverable reading.
			ClassicAssert.AreEqual(KingdomPurposeLandingAttemptState.Settled,
				KingdomPurposePortfolioRules.ClassifyLandingAttempt(true, true, 3, 3, true));
			// no outstanding offer at all.
			ClassicAssert.AreEqual(KingdomPurposeLandingAttemptState.Clear,
				KingdomPurposePortfolioRules.ClassifyLandingAttempt(false, false, 0, 3, true));
			ClassicAssert.AreEqual(KingdomPurposeLandingAttemptState.Ambiguous,
				KingdomPurposePortfolioRules.ClassifyLandingAttempt(true, false, 3, 3, true),
				"a witness this operation cannot read is ambiguity, never absence");
			ClassicAssert.AreEqual(KingdomPurposeLandingAttemptState.Ambiguous,
				KingdomPurposePortfolioRules.ClassifyLandingAttempt(true, true, 0, 0, true));
			ClassicAssert.AreEqual(KingdomPurposeLandingAttemptState.Ambiguous,
				KingdomPurposePortfolioRules.ClassifyLandingAttempt(true, true, 3, -1, true));
		}

		[Test]
		public void AWitnessIsReadBackOnlyWhenItIsThisOperationsOwnWholeAndLawful()
		{
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryLandingAttempt(Receipt, 1,
				out string witness));
			ClassicAssert.AreEqual("pv1;23:purpose-landing-attempt;39:" + Receipt + ";1:1", witness);
			// wrong receipt: another operation's witness must not read back as ours.
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryLandingReceipt("p16", 2L, "zMKi",
				out string other));
			ClassicAssert.AreNotEqual(Receipt, other);
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryReadLandingAttempt(witness, other,
				out _), "a witness bound to another receipt is not this operation's offer");
			// wrong expected progress: the figure is part of the witness, not a hint beside it.
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryLandingAttempt(Receipt, 5,
				out string five));
			ClassicAssert.AreNotEqual(witness, five);
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryReadLandingAttempt(five, Receipt,
				out int read));
			ClassicAssert.AreEqual(5, read);
			// torn: any string that is not the whole canonical witness reads back as nothing, and
			// nothing is ambiguity rather than absence at the caller.
			foreach (string torn in new string[] { null, "", witness.Substring(0, witness.Length - 1),
				witness + "x", "pv1;23:purpose-landing-attempt", "r_TAF_PurposeLandedAttempt",
				"pv1;23:purpose-landing-attempt;39:" + Receipt + ";1:0" })
				ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryReadLandingAttempt(torn, Receipt,
					out _), "a torn witness proves an offer happened, never that none did: "
					+ (torn ?? "null"));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryLandingAttempt(Receipt, 0, out string none));
			ClassicAssert.IsNull(none);
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryLandingAttempt(Receipt,
				KingdomPurposePortfolioRules.MaxCarriedFood + 1, out _));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryLandingAttempt(null, 1, out _));
			// The two witnesses share a grammar and must never read as one another: a fault is not
			// an offer, and reading one as the other would reconcile away the wrong evidence.
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryLandingFault(Receipt, 1, 1,
				out string faultWitness));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryReadLandingAttempt(faultWitness, Receipt,
				out _), "a fault witness is not an attempt witness");
			// A canonically well-formed three-field witness under any other tag is still not an
			// attempt: the tag is part of the identity, not decoration.
			ClassicAssert.AreEqual(39, Receipt.Length);
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryReadLandingAttempt(
				"pv1;21:purpose-landing-other;39:" + Receipt + ";1:1", Receipt, out _),
				"only this lane's own attempt tag names an offer");
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryReadLandingAttempt(
				"pv1;23:purpose-landing-attempt;39:" + Receipt + ";1:1", Receipt, out int one)
				&& one == 1, "the same shape under the right tag is the witness");
			// The two witnesses share a grammar and must never be read as one another: a fault is
			// not an offer, and reading one as the other would reconcile the wrong thing away.
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryLandingFault(Receipt, 1, 1,
				out string fault));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryReadLandingAttempt(fault, Receipt,
				out _), "a fault witness is not an attempt witness");
		}

		[Test]
		public void EmptyAttemptPropertyRemainsAmbiguous()
		{
			// The escape this closes: presence read as "value is non-empty" lets a witness torn
			// down to an empty string classify Clear, and a Clear reading hands the very case the
			// witness exists for a fresh serving. Presence is the property existing; any present
			// value the decoder refuses is ambiguous.
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryReadLandingAttempt("", Receipt, out _),
				"an empty value is not a readable witness");
			ClassicAssert.AreEqual(KingdomPurposeLandingAttemptState.Ambiguous,
				KingdomPurposePortfolioRules.ClassifyLandingAttempt(true, false, 0, 0, true),
				"a present but unreadable witness is ambiguous, never clear");
			// Present-and-unreadable stays ambiguous whatever the ground happens to look like,
			// including the readings that would otherwise be Settled or Consumed.
			for (int observed = 0; observed <= Carried; observed++)
				foreach (bool exact in new bool[] { true, false })
					ClassicAssert.AreEqual(KingdomPurposeLandingAttemptState.Ambiguous,
						KingdomPurposePortfolioRules.ClassifyLandingAttempt(true, false, 0,
							observed, exact), observed + "/" + exact);
			ClassicAssert.AreEqual(KingdomPurposeLandingAttemptState.Clear,
				KingdomPurposePortfolioRules.ClassifyLandingAttempt(false, false, 0, 0, true),
				"only the absence of the property itself is a clear ground");
		}

		[Test]
		public void ProgressIsNeverLiftedByAnAftermathTheWitnessStillHolds()
		{
			// The forbidden shape: a lifted record turns the same physical ground into consumption
			// that owes nothing, so a Delivered publish follows provision the destination never
			// kept. The record only ever rises past a reproved partition, and the witness stands
			// in front of it, so this reading is unreachable while an offer is outstanding.
			const int recorded = 2;
			ClassicAssert.AreEqual(KingdomPurposeLandingRecordState.Consumed,
				KingdomPurposePortfolioRules.ClassifyLandingRecord(recorded, Carried, 0));
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryLandingOutstanding(Carried, recorded,
				Carried, out int lifted, out _));
			ClassicAssert.AreEqual(0, lifted, "this is what raising progress early would buy");
			// With the record left where the last reproved pass put it, the retry owes the whole
			// remainder; and the outstanding witness refuses to offer any of it until the earlier
			// aftermath is reconciled or the pair is quarantined.
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryLandingOutstanding(Carried, recorded,
				recorded, out int owed, out int progress));
			ClassicAssert.AreEqual(Carried - recorded, owed);
			ClassicAssert.AreEqual(recorded, progress);
			ClassicAssert.AreEqual(KingdomPurposeLandingAttemptState.Ambiguous,
				KingdomPurposePortfolioRules.ClassifyLandingAttempt(true, true, recorded + 1,
					recorded, true),
				"the outstanding offer is what stops the remainder being re-offered blindly");
		}

		private static void AssertAftermath(string label,
			KingdomPurposeServingAftermath expected, bool offered, bool threw, bool sameObject,
			bool valid, bool inTargetLarder, bool whole, bool exactContent, bool markerIntact)
		{
			ClassicAssert.AreEqual(expected, KingdomPurposePortfolioRules.ClassifyServingAftermath(offered,
				threw, sameObject, valid, inTargetLarder, whole, exactContent, markerIntact), label);
		}

		private static void AssertCarriage(KingdomPurposeKind source,
			KingdomPurposeKind destination, int debited, int landed, int lost)
		{
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryCarriedFood(source, destination,
				out int actualDebited, out int actualLanded, out int actualLost));
			ClassicAssert.AreEqual(debited, actualDebited);
			ClassicAssert.AreEqual(landed, actualLanded);
			ClassicAssert.AreEqual(lost, actualLost);
			ClassicAssert.AreEqual(actualDebited, actualLanded + actualLost);
		}
	}
}
#endif
