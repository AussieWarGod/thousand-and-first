using System;

namespace ThousandAndFirst
{
	/// <summary>
	/// Copy-isolated player choice opened from one exact loaded-city evidence reading.
	/// Only the runtime may create it; UI code may render it but cannot replace its evidence.
	/// </summary>
	public sealed class KingdomSitePracticeChoiceView
	{
		private readonly string RealmId;
		private readonly KingdomSiteEvidenceSnapshot Evidence;

		public string SettlementId { get; }
		public string Vocation { get; }
		public string EvidenceDigest { get; }
		public string SourceSummary { get; }
		public string FirstTitle { get; }
		public string FirstReading { get; }
		public string SecondTitle { get; }
		public string SecondReading { get; }
		public string VocationNotice { get; }

		private KingdomSitePracticeChoiceView(string realmId,
			KingdomSitePracticePreview preview)
		{
			RealmId = realmId;
			Evidence = Copy(preview.Snapshot);
			SettlementId = Evidence.SettlementId;
			Vocation = Evidence.Vocation;
			EvidenceDigest = Evidence.Digest;
			SourceSummary = preview.SourceSummary;
			FirstTitle = preview.FirstTitle;
			FirstReading = preview.FirstReading;
			SecondTitle = preview.SecondTitle;
			SecondReading = preview.SecondReading;
			VocationNotice = preview.VocationNotice;
		}

		internal static bool TryCreate(string exactRealmId,
			KingdomSitePracticePreview preview, out KingdomSitePracticeChoiceView view,
			out string failure)
		{
			view = null;
			failure = null;
			KingdomCivicPracticeEnvelope identity = new KingdomCivicPracticeEnvelope();
			if (!identity.TryBindEmptyIdentity(exactRealmId, out failure)) return false;
			if (preview == null || preview.Snapshot == null ||
				!KingdomSitePracticeRules.TryPreview(preview.Snapshot,
					out string first, out string second, out failure))
				return Fail(failure ?? "site practice preview is invalid", out failure);
			if (!string.Equals(preview.FirstReading, first, StringComparison.Ordinal) ||
				!string.Equals(preview.SecondReading, second, StringComparison.Ordinal) ||
				string.IsNullOrWhiteSpace(preview.SourceSummary) ||
				string.IsNullOrWhiteSpace(preview.FirstTitle) ||
				string.IsNullOrWhiteSpace(preview.SecondTitle) ||
				string.IsNullOrWhiteSpace(preview.VocationNotice))
				return Fail("site practice preview presentation does not match its evidence",
					out failure);
			view = new KingdomSitePracticeChoiceView(exactRealmId, preview);
			return true;
		}

		internal bool TrySnapshotFor(string exactRealmId,
			out KingdomSiteEvidenceSnapshot snapshot, out string failure)
		{
			snapshot = null;
			failure = null;
			if (!string.Equals(RealmId, exactRealmId, StringComparison.Ordinal))
				return Fail("site practice view belongs to another realm", out failure);
			KingdomSiteEvidenceSnapshot copy = Copy(Evidence);
			if (!KingdomSitePracticeRules.TryPreview(copy, out string _, out string _,
				out failure)) return false;
			snapshot = copy;
			return true;
		}

		internal bool Matches(string exactRealmId, KingdomSitePracticePreview fresh,
			out string failure)
		{
			failure = null;
			if (!TryCreate(exactRealmId, fresh, out KingdomSitePracticeChoiceView current,
				out failure)) return false;
			return string.Equals(RealmId, exactRealmId, StringComparison.Ordinal) &&
				Same(Evidence, current.Evidence) ||
				Fail("Exact loaded-city evidence changed after this choice opened.", out failure);
		}

		private static bool Same(KingdomSiteEvidenceSnapshot left,
			KingdomSiteEvidenceSnapshot right)
		{
			return left != null && right != null &&
				left.SettlementId == right.SettlementId && left.Vocation == right.Vocation &&
				left.Style == right.Style && left.Terrain == right.Terrain &&
				left.Region == right.Region && left.Creed == right.Creed &&
				left.WorkReceiptId == right.WorkReceiptId &&
				left.DeedReceiptId == right.DeedReceiptId && left.WorkText == right.WorkText &&
				left.DeedText == right.DeedText && left.FoundedTick == right.FoundedTick &&
				left.ObservedTick == right.ObservedTick && left.Digest == right.Digest;
		}

		private static KingdomSiteEvidenceSnapshot Copy(KingdomSiteEvidenceSnapshot source)
		{
			return source == null ? null : new KingdomSiteEvidenceSnapshot
			{
				SettlementId = source.SettlementId,
				Vocation = source.Vocation,
				Style = source.Style,
				Terrain = source.Terrain,
				Region = source.Region,
				Creed = source.Creed,
				WorkReceiptId = source.WorkReceiptId,
				DeedReceiptId = source.DeedReceiptId,
				WorkText = source.WorkText,
				DeedText = source.DeedText,
				Digest = source.Digest,
				FoundedTick = source.FoundedTick,
				ObservedTick = source.ObservedTick
			};
		}

		private static bool Fail(string text, out string failure)
		{
			failure = text;
			return false;
		}
	}

	/// <summary>Immutable UI result from either one accepted append or an exact retry.</summary>
	public sealed class KingdomCivicPracticeCommitResult
	{
		public bool Changed { get; }
		public string PracticeId { get; }
		public string Title { get; }
		public string Description { get; }
		public long ChosenTick { get; }

		internal KingdomCivicPracticeCommitResult(bool changed,
			KingdomSitePracticeReceipt receipt)
		{
			Changed = changed;
			PracticeId = receipt.PracticeId;
			Title = receipt.Title;
			Description = receipt.Description;
			ChosenTick = receipt.ChosenTick;
		}
	}

	/// <summary>Narrow C18 section lease seam; game and pure tests provide separate adapters.</summary>
	internal interface IKingdomCivicPracticeSectionPort
	{
		bool TryReadSection(int sectionId, out KingdomCivicMemorySectionLease lease,
			out string failure);
		bool TryCommitSection(KingdomCivicMemorySectionLease lease, byte[] payload,
			out string failure);
	}
}
