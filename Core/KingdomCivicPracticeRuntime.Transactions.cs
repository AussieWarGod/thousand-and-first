using System;

namespace ThousandAndFirst
{
	/// <summary>Pure D1 C18 append transaction. It never opens or rewrites D12 services.</summary>
	internal static class KingdomCivicPracticeTransactions
	{
		internal static bool TryChoose(IKingdomCivicPracticeSectionPort port,
			string exactRealmId, KingdomSitePracticeChoiceView view, int reading, long tick,
			out KingdomCivicPracticeCommitResult result, out string failure)
		{
			result = null;
			failure = null;
			if (port == null) return Fail("civic practice memory port is absent", out failure);
			if (view == null) return Fail("site practice choice is absent", out failure);
			if (reading < 1 || reading > 2)
				return Fail("site practice reading is outside its two choices", out failure);
			if (!view.TrySnapshotFor(exactRealmId,
				out KingdomSiteEvidenceSnapshot snapshot, out failure)) return false;
			if (tick < snapshot.ObservedTick)
				return Fail("site practice choice predates its exact evidence", out failure);

			if (!port.TryReadSection(KingdomCivicMemoryLimits.SectionCivicPractice,
				out KingdomCivicMemorySectionLease lease, out failure)) return false;
			if (lease == null || lease.SectionId != KingdomCivicMemoryLimits.SectionCivicPractice)
				return Fail("civic practice memory returned the wrong section lease", out failure);

			KingdomCivicPracticeEnvelope envelope = KingdomCivicPracticeStore.ReadForRealm(
				lease.Payload(), exactRealmId, out string readFailure);
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

			long nestedRevision = envelope.SitePractices.Revision;
			if (!KingdomSitePracticeRules.TryRead(envelope.SitePractices, nestedRevision,
				snapshot, reading, tick, out KingdomSitePracticeReceipt receipt,
				out failure)) return false;
			if (receipt == null)
				return Fail("site practice append produced no receipt", out failure);

			if (envelope.SitePractices.Revision == nestedRevision)
			{
				result = new KingdomCivicPracticeCommitResult(false, receipt);
				return true;
			}
			if (nestedRevision == long.MaxValue ||
				envelope.SitePractices.Revision != nestedRevision + 1L)
				return Fail("site practice nested revision did not advance exactly once", out failure);
			if (!KingdomCivicPracticeStore.TryWrite(envelope, out byte[] encoded,
				out failure)) return false;
			if (!port.TryCommitSection(lease, encoded, out failure)) return false;
			result = new KingdomCivicPracticeCommitResult(true, receipt);
			return true;
		}

		private static bool Fail(string text, out string failure)
		{
			failure = text;
			return false;
		}
	}
}
