#if TAF_TESTS && !TAF_CONSTRUCTION_INPUT_PORTABLE
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.DevTests
{
	[TestFixture]
	public sealed class KingdomPolityConsignmentAbsenceTests
	{
		[Test]
		public void EmptyExactTradeBookProvesAbsenceWithoutMintingReceiptOrMutation()
		{
			KingdomPolityConsignmentRequest request = Request();
			KingdomTradeBook book = EmptyBook(request); byte[] before = KingdomTradeCodec.
				EncodeEnvelope(book);
			ClassicAssert.IsTrue(KingdomTradeRules.TryProveNoPolityConsignmentCustody(book,
				request, out KingdomPolityConsignmentAbsenceProof proof,
				out bool custody, out string failure), failure);
			ClassicAssert.IsFalse(custody); ClassicAssert.NotNull(proof);
			ClassicAssert.AreEqual(request.CorrespondencePlanId, proof.CorrespondencePlanId);
			ClassicAssert.AreEqual(request.ConsignmentId, proof.ConsignmentId);
			ClassicAssert.AreEqual(KingdomPolityCorrespondenceRules.ConsignmentAbsenceDigest(proof),
				proof.ProofDigest);
			ClassicAssert.AreEqual(0, book.RecentProofs.Count);
			ClassicAssert.IsNull(book.OpenOperation); ClassicAssert.IsNull(book.PendingRetirement);
			CollectionAssert.AreEqual(before, KingdomTradeCodec.EncodeEnvelope(book));
		}

		[Test]
		public void AnyExactOperationProofOrCollisionRefusesAbsenceAndPreservesCustody()
		{
			KingdomPolityConsignmentRequest request = Request();
			KingdomTradeBook book = EmptyBook(request);
			book.OpenOperation = new KingdomTradeOperation
			{
				Kind = KingdomTradeOperationKind.PolityConsignmentDelivery,
				ManifestId = request.ConsignmentId
			};
			AssertHeld(book, request);
			book = EmptyBook(request);
			book.PendingRetirement = new KingdomTradeProof
			{
				Kind = KingdomTradeOperationKind.PolityConsignmentDelivery,
				ManifestId = request.ConsignmentId
			};
			AssertHeld(book, request);

			book = KingdomPolityConsignmentTests.TradeBookForWitness(request, 4,
				KingdomTradePhase.Terminal);
			AssertHeld(book, request);
			book.RecentProofs.Add(book.RecentProofs[0]); byte[] ambiguous =
				KingdomTradeCodec.EncodeEnvelope(book);
			ClassicAssert.IsFalse(KingdomTradeRules.TryProveNoPolityConsignmentCustody(book,
				request, out KingdomPolityConsignmentAbsenceProof proof,
				out bool _, out string failure));
			ClassicAssert.IsNull(proof); StringAssert.Contains("duplicated", failure);
			CollectionAssert.AreEqual(ambiguous, KingdomTradeCodec.EncodeEnvelope(book));
		}

		private static void AssertHeld(KingdomTradeBook Book,
			KingdomPolityConsignmentRequest Request)
		{
			ClassicAssert.IsTrue(KingdomTradeRules.TryProveNoPolityConsignmentCustody(Book,
				Request, out KingdomPolityConsignmentAbsenceProof proof,
				out bool custody, out string failure), failure);
			ClassicAssert.IsTrue(custody); ClassicAssert.IsNull(proof);
		}

		private static KingdomPolityConsignmentRequest Request()
		{
			KingdomPolityLedger ledger = KingdomPolityConsignmentTests.Scene();
			ClassicAssert.IsTrue(KingdomPolityCorrespondenceRules.TryPlanConsignment(ledger,
				ledger.Revision, KingdomPolityTestData.Plan, KingdomPolityTestData.Cohort,
				KingdomPolityTestData.Settlement, out KingdomPolityConsignmentRequest request,
				out KingdomPolityPublicationResult _, out string failure), failure);
			return request;
		}

		private static KingdomTradeBook EmptyBook(KingdomPolityConsignmentRequest Request)
		{
			KingdomTradeBook book = new KingdomTradeBook();
			ClassicAssert.IsTrue(KingdomTradeRules.BindExactIdentity(book, Request.CurrentPolityId,
				new[] { Request.SurfaceRef }, out string failure), failure);
			return book;
		}
	}
}
#endif
