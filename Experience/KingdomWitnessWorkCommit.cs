using System;

namespace ThousandAndFirst
{
	/// <summary>Copy/CAS transitions for O5 inside C18. Every write encodes the whole civic-
	/// artifacts envelope, preserving D6's recognition sibling byte-for-byte.</summary>
	public static class KingdomWitnessWorkCommit
	{
		public static bool TryCaptureClosed(IKingdomCivicMemoryAuthority Authority,
			string RealmId, KingdomWitnessWorkSource Source, out bool Recorded,
			out KingdomWitnessWorkReceipt Receipt, out string Failure)
		{
			Recorded = false; Receipt = null;
			if (!KingdomWitnessWorkLease.TryReadAuthority(Authority, RealmId,
				out KingdomCivicMemorySectionLease lease,
				out KingdomCivicArtifactsEnvelope held, out Failure)) return false;
			KingdomCivicArtifactsEnvelope next = Copy(held, out Failure);
			if (next == null || !KingdomWitnessWorkRules.TryCapture(next.WitnessWorks,
				next.WitnessWorks.Revision, Source, out Receipt, out Failure)) return false;
			if (next.WitnessWorks.Revision == held.WitnessWorks.Revision) return true;
			if (!Write(next, out byte[] bytes, out Failure)
				|| !Authority.TryCommitSection(lease, bytes, out Failure))
			{
				Receipt = null; return false;
			}
			Recorded = true; return true;
		}

		public static bool TryPlan(KingdomCivicArtifactsEnvelope Held, string WorkId,
			string ObjectId, string ZoneId, string ConstructionReceiptId, int X, int Y,
			long Tick, out KingdomWitnessWorkPlan Plan, out string Failure)
		{
			Plan = null; KingdomCivicArtifactsEnvelope next = Copy(Held, out Failure);
			if (next == null || !KingdomWitnessWorkRules.TryPrepareCarrier(next.WitnessWorks,
				next.WitnessWorks.Revision, WorkId, ObjectId, ZoneId, ConstructionReceiptId,
				X, Y, Tick, out Failure)) return false;
			KingdomWitnessWorkReceipt row = KingdomWitnessWorkRules.FindExact(
				next.WitnessWorks, WorkId);
			if (row == null || !Write(next, out _, out Failure)) return false;
			Plan = new KingdomWitnessWorkPlan(row, Tick); return true;
		}

		public static bool TryPreparePlanned(IKingdomCivicMemoryAuthority Authority,
			KingdomCivicMemorySectionLease Lease, string RealmId, KingdomWitnessWorkPlan Plan,
			out KingdomWitnessWorkReceipt Receipt, out bool Recorded, out string Failure)
		{
			Receipt = null; Recorded = false; Failure = null;
			if (Authority == null || Lease == null || Plan == null || Lease.SectionId
				!= KingdomWitnessWorkLease.SectionId || !KingdomWitnessWorkLease.TryInterpret(
					Lease.Payload(), RealmId, out KingdomCivicArtifactsEnvelope held, out Failure))
				return KingdomWitnessWorkLease.Fail(Failure ?? "witness plan authority is invalid",
					out Failure);
			KingdomCivicArtifactsEnvelope next = Copy(held, out Failure);
			if (next == null || !KingdomWitnessWorkRules.TryPrepareCarrier(next.WitnessWorks,
				next.WitnessWorks.Revision, Plan.WorkId, Plan.ObjectId, Plan.ZoneId,
				Plan.ConstructionReceiptId, Plan.X, Plan.Y, Plan.Tick, out Failure)) return false;
			Receipt = KingdomWitnessWorkRules.FindExact(next.WitnessWorks, Plan.WorkId);
			if (!ExactPlan(Plan, Receipt))
				return KingdomWitnessWorkLease.Fail("prepared witness row differs from disclosure",
					out Failure);
			if (next.WitnessWorks.Revision == held.WitnessWorks.Revision) return true;
			if (!Write(next, out byte[] bytes, out Failure)
				|| !Authority.TryCommitSection(Lease, bytes, out Failure))
			{
				Receipt = null; return false;
			}
			Recorded = true; return true;
		}

		public static bool TryCommitCarrier(IKingdomCivicMemoryAuthority Authority,
			string RealmId, string WorkId, string CarrierReceiptId, long Tick,
			out string Failure)
		{
			return Mutate(Authority, RealmId, delegate(KingdomWitnessWorkBook book,
				out string inner)
			{
				return KingdomWitnessWorkRules.TryCommitCarrier(book, book.Revision,
					WorkId, CarrierReceiptId, Tick, out inner);
			}, out Failure);
		}

		public static bool TryDecline(IKingdomCivicMemoryAuthority Authority,
			string RealmId, string WorkId, long Tick, out string Failure)
		{
			return Mutate(Authority, RealmId, delegate(KingdomWitnessWorkBook book,
				out string inner)
			{
				return KingdomWitnessWorkRules.TryDecline(book, book.Revision,
					WorkId, Tick, out inner);
			}, out Failure);
		}

		public static bool TryDeclinePlanned(IKingdomCivicMemoryAuthority Authority,
			KingdomCivicMemorySectionLease Lease, string RealmId, string WorkId, long Tick,
			out bool Recorded, out string Failure)
		{
			Recorded = false; Failure = null;
			if (Authority == null || Lease == null || Lease.SectionId
				!= KingdomWitnessWorkLease.SectionId || !KingdomWitnessWorkLease.TryInterpret(
					Lease.Payload(), RealmId, out KingdomCivicArtifactsEnvelope held, out Failure))
				return KingdomWitnessWorkLease.Fail(Failure
					?? "witness decline authority is invalid", out Failure);
			KingdomCivicArtifactsEnvelope next = Copy(held, out Failure);
			if (next == null || !KingdomWitnessWorkRules.TryDecline(next.WitnessWorks,
				next.WitnessWorks.Revision, WorkId, Tick, out Failure)) return false;
			if (next.WitnessWorks.Revision == held.WitnessWorks.Revision) return true;
			if (!Write(next, out byte[] bytes, out Failure)
				|| !Authority.TryCommitSection(Lease, bytes, out Failure)) return false;
			Recorded = true; return true;
		}

		public static bool TryReconcile(IKingdomCivicMemoryAuthority Authority,
			string RealmId, string WorkId, bool Present, bool Teardown, long Tick,
			out string Failure)
		{
			return Mutate(Authority, RealmId, delegate(KingdomWitnessWorkBook book,
				out string inner)
			{
				return KingdomWitnessWorkRules.TryReconcileCarrier(book, book.Revision,
					WorkId, Present, Teardown, Tick, out inner);
			}, out Failure);
		}

		private delegate bool Change(KingdomWitnessWorkBook Book, out string Failure);

		private static bool Mutate(IKingdomCivicMemoryAuthority Authority, string RealmId,
			Change Apply, out string Failure)
		{
			Failure = null;
			if (!KingdomWitnessWorkLease.TryReadAuthority(Authority, RealmId,
				out KingdomCivicMemorySectionLease lease,
				out KingdomCivicArtifactsEnvelope held, out Failure)) return false;
			KingdomCivicArtifactsEnvelope next = Copy(held, out Failure);
			if (next == null || !Apply(next.WitnessWorks, out Failure)) return false;
			if (next.WitnessWorks.Revision == held.WitnessWorks.Revision) return true;
			return Write(next, out byte[] bytes, out Failure)
				&& Authority.TryCommitSection(lease, bytes, out Failure);
		}

		private static KingdomCivicArtifactsEnvelope Copy(KingdomCivicArtifactsEnvelope Held,
			out string Failure)
		{
			Failure = null;
			try { return KingdomCivicArtifactsStore.Copy(Held); }
			catch (Exception error)
			{
				Failure = "C18 witness authority could not be copied (" + error.Message + ")";
				return null;
			}
		}

		private static bool Write(KingdomCivicArtifactsEnvelope Value, out byte[] Bytes,
			out string Failure) => KingdomCivicArtifactsStore.TryWrite(Value, out Bytes, out Failure);

		private static bool ExactPlan(KingdomWitnessWorkPlan P, KingdomWitnessWorkReceipt R)
		{
			return R != null && R.WorkId == P.WorkId && R.Source.SnapshotDigest == P.SourceDigest
				&& R.CarrierReceiptId == P.CarrierReceiptId && R.CarrierObjectId == P.ObjectId
				&& R.CarrierZoneId == P.ZoneId
				&& R.CarrierConstructionReceiptId == P.ConstructionReceiptId
				&& R.CarrierX == P.X && R.CarrierY == P.Y && R.Description == P.Description;
		}
	}
}
