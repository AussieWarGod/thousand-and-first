#if TAF_TESTS && !TAF_CONSTRUCTION_INPUT_PORTABLE
using NUnit.Framework;

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
			Assert.IsTrue(KingdomTradeRules.TryProveNoPolityConsignmentCustody(book,
				request, out KingdomPolityConsignmentAbsenceProof proof,
				out bool custody, out string failure), failure);
			Assert.IsFalse(custody); Assert.NotNull(proof);
			Assert.AreEqual(request.CorrespondencePlanId, proof.CorrespondencePlanId);
			Assert.AreEqual(request.ConsignmentId, proof.ConsignmentId);
			Assert.AreEqual(KingdomPolityCorrespondenceRules.ConsignmentAbsenceDigest(proof),
				proof.ProofDigest);
			Assert.AreEqual(0, book.RecentProofs.Count);
			Assert.IsNull(book.OpenOperation); Assert.IsNull(book.PendingRetirement);
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
			Assert.IsFalse(KingdomTradeRules.TryProveNoPolityConsignmentCustody(book,
				request, out KingdomPolityConsignmentAbsenceProof proof,
				out bool _, out string failure));
			Assert.IsNull(proof); StringAssert.Contains("duplicated", failure);
			CollectionAssert.AreEqual(ambiguous, KingdomTradeCodec.EncodeEnvelope(book));
		}

		private static void AssertHeld(KingdomTradeBook Book,
			KingdomPolityConsignmentRequest Request)
		{
			Assert.IsTrue(KingdomTradeRules.TryProveNoPolityConsignmentCustody(Book,
				Request, out KingdomPolityConsignmentAbsenceProof proof,
				out bool custody, out string failure), failure);
			Assert.IsTrue(custody); Assert.IsNull(proof);
		}

		private static KingdomPolityConsignmentRequest Request()
		{
			KingdomPolityLedger ledger = KingdomPolityConsignmentTests.Scene();
			Assert.IsTrue(KingdomPolityCorrespondenceRules.TryPlanConsignment(ledger,
				ledger.Revision, KingdomPolityTestData.Plan, KingdomPolityTestData.Cohort,
				KingdomPolityTestData.Settlement, out KingdomPolityConsignmentRequest request,
				out KingdomPolityPublicationResult _, out string failure), failure);
			return request;
		}

		private static KingdomTradeBook EmptyBook(KingdomPolityConsignmentRequest Request)
		{
			KingdomTradeBook book = new KingdomTradeBook();
			Assert.IsTrue(KingdomTradeRules.BindExactIdentity(book, Request.CurrentPolityId,
				new[] { Request.SurfaceRef }, out string failure), failure);
			return book;
		}
	}
}
#endif
