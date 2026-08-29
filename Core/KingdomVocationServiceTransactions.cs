using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Immutable UI result from one D12 C18 append or exact retry.</summary>
	public sealed class KingdomVocationServiceCommitResult
	{
		public bool Changed { get; }
		public string ServiceId { get; }
		public string SourceReceiptId { get; }
		public string SinkReceiptId { get; }
		public string Verb { get; }
		public string ReceiptText { get; }
		public long CadenceOrdinal { get; }
		public long CompletedTick { get; }

		internal KingdomVocationServiceCommitResult(bool changed,
			KingdomVocationServiceReceipt receipt)
		{
			Changed = changed;
			ServiceId = receipt.ServiceId;
			SourceReceiptId = receipt.Request.SourceReceiptId;
			SinkReceiptId = receipt.Request.SinkReceiptId;
			Verb = receipt.Verb;
			ReceiptText = receipt.OutputText;
			CadenceOrdinal = receipt.Request.CadenceOrdinal;
			CompletedTick = receipt.CompletedTick;
		}
	}

	/// <summary>Pure D12 C18 copy/append/CAS transaction and read-only history ingress.</summary>
	internal static class KingdomVocationServiceTransactions
	{
		internal static bool TryRecordGoverned(IKingdomCivicPracticeSectionPort port,
			string exactRealmId, KingdomVocationServiceOffer offer, long tick,
			IKingdomVocationServicePublication publication,
			out KingdomVocationServiceCommitResult result, out string failure)
		{
			if (publication == null)
			{
				result = null; return Fail("vocation service governance boundary is absent", out failure);
			}
			return TryRecordCore(port, exactRealmId, offer, tick, publication,
				out result, out failure);
		}

		private static bool TryRecordCore(IKingdomCivicPracticeSectionPort port,
			string exactRealmId, KingdomVocationServiceOffer offer, long tick,
			IKingdomVocationServicePublication publication,
			out KingdomVocationServiceCommitResult result, out string failure)
		{
			result = null; failure = null;
			if (port == null) return Fail("civic practice memory port is absent", out failure);
			if (!KingdomVocationServiceRules.TryValidateOffer(offer, out failure) ||
				offer.State != KingdomVocationServiceOfferState.Available)
				return Fail(failure ?? "this vocation report offers no service", out failure);
			if (!TryRead(port, exactRealmId, out KingdomCivicMemorySectionLease lease,
				out KingdomCivicPracticeEnvelope envelope, out failure)) return false;
			long revision = envelope.VocationServices.Revision;
			if (!KingdomVocationServiceRules.TryPrepareRequest(envelope.VocationServices,
				offer, tick, out KingdomVocationServiceRequest request, out failure) ||
				!KingdomVocationServiceRules.TryServe(envelope.VocationServices, revision,
					request, tick, out KingdomVocationServiceReceipt receipt, out failure)) return false;
			if (receipt == null)
				return Fail("vocation service append produced no receipt", out failure);
			if (envelope.VocationServices.Revision == revision)
			{
				result = new KingdomVocationServiceCommitResult(false, receipt);
				return true;
			}
			if (revision == long.MaxValue ||
				envelope.VocationServices.Revision != revision + 1L)
				return Fail("vocation service nested revision did not advance exactly once", out failure);
			if (!KingdomCivicPracticeStore.TryWrite(envelope, out byte[] encoded,
				out failure)) return false;
			string publishFailure = null;
			if (!publication.TryPublish(() => port.TryCommitSection(lease, encoded,
				out publishFailure)))
				return Fail(publishFailure ??
					"vocation service publication boundary refused the durable CAS", out failure);
			result = new KingdomVocationServiceCommitResult(true, receipt);
			return true;
		}

		internal static bool TryReadHistory(IKingdomCivicPracticeSectionPort port,
			string exactRealmId, string settlementId, string vocation,
			out string history, out string failure)
		{
			history = null; failure = null;
			if (port == null) return Fail("civic practice memory port is absent", out failure);
			if (!TryRead(port, exactRealmId, out KingdomCivicMemorySectionLease _,
				out KingdomCivicPracticeEnvelope envelope, out failure)) return false;
			return KingdomVocationServiceRules.TryDescribeHistory(envelope.VocationServices,
				settlementId, vocation, out history, out failure);
		}

		internal static bool TryReadView(IKingdomCivicPracticeSectionPort port,
			string exactRealmId, string settlementId, string vocation,
			KingdomVocationServiceOffer offer, out string history,
			out KingdomVocationServiceStatus status, out string failure)
		{
			history = null; status = null; failure = null;
			if (port == null) return Fail("civic practice memory port is absent", out failure);
			if (!TryRead(port, exactRealmId, out KingdomCivicMemorySectionLease _,
				out KingdomCivicPracticeEnvelope envelope, out failure)) return false;
			if (!KingdomVocationServiceRules.TryDescribeHistory(envelope.VocationServices,
				settlementId, vocation, out history, out failure)) return false;
			if (offer == null || offer.State != KingdomVocationServiceOfferState.Available)
				return true;
			return KingdomVocationServiceRules.TryInspect(envelope.VocationServices,
				offer, out status, out failure);
		}

		internal static bool TryReadRealmResults(IKingdomCivicPracticeSectionPort port,
			string exactRealmId, out List<string> pages, out string failure)
		{
			pages = null; failure = null;
			if (port == null) return Fail("civic practice memory port is absent", out failure);
			if (!TryRead(port, exactRealmId, out KingdomCivicMemorySectionLease _,
				out KingdomCivicPracticeEnvelope envelope, out failure)) return false;
			List<string> result = new List<string>();
			int offset = 0;
			do
			{
				if (!KingdomVocationServiceRules.TryDescribeRealmResults(
					envelope.VocationServices, offset, out string page,
					out int next, out failure)) return false;
				result.Add(page); offset = next;
			}
			while (offset >= 0);
			pages = result; return true;
		}

		private static bool TryRead(IKingdomCivicPracticeSectionPort port,
			string exactRealmId, out KingdomCivicMemorySectionLease lease,
			out KingdomCivicPracticeEnvelope envelope, out string failure)
		{
			lease = null; envelope = null; failure = null;
			if (!port.TryReadSection(KingdomCivicMemoryLimits.SectionCivicPractice,
				out lease, out failure)) return false;
			if (lease == null || lease.SectionId != KingdomCivicMemoryLimits.SectionCivicPractice)
				return Fail("civic practice memory returned the wrong section lease", out failure);
			envelope = KingdomCivicPracticeStore.ReadForRealm(lease.Payload(),
				exactRealmId, out string readFailure);
			if (envelope == null)
				return Fail("civic practice authority is absent after its section read", out failure);
			if (envelope.IsOpaqueFuture)
				return Fail("Civic practice authority belongs to a newer build and is carried, not edited.",
					out failure);
			if (envelope.Quarantined || !string.IsNullOrEmpty(readFailure))
				return Fail(readFailure ?? envelope.Fault ??
					"civic practice authority is quarantined", out failure);
			if (!envelope.IdentityBound || !string.Equals(envelope.RealmId, exactRealmId,
				StringComparison.Ordinal) ||
				!KingdomCivicPracticeStore.TryValidateIdentity(envelope, out readFailure))
				return Fail(readFailure ?? "civic practice authority belongs to another realm",
					out failure);
			return true;
		}

		private static bool Fail(string text, out string failure)
		{
			failure = text;
			return false;
		}
	}
}
