using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Detached growth proposal for the master's all-participant publication gate.
	/// No child graph is replaced; the coordinator must preflight every participant before
	/// invoking PublishPrevalidated without intervening callbacks or other mutations.</summary>
	internal sealed class KingdomMasterGrowthResumePlan
	{
		private readonly KingdomLifecycleBook Parent;
		private readonly KingdomGrowthBook Source, Proposed;
		private readonly string SettlementId;
		private readonly byte[] Before, After;
		private readonly object[] References;
		private readonly bool PreserveOnly;
		private bool Published;
		internal bool HasArrivalAuthority { get { return !PreserveOnly; } }
		internal long NextArrivalTick { get { return Proposed.NextArrivalTick; } }

		private KingdomMasterGrowthResumePlan(KingdomLifecycleBook parent,
			KingdomGrowthBook source, KingdomGrowthBook proposed, string settlementId,
			byte[] before, byte[] after, object[] references, bool preserveOnly)
		{
			Parent = parent; Source = source; SettlementId = settlementId;
			Proposed = proposed; Before = before; After = after; PreserveOnly = preserveOnly;
			References = references;
		}

		internal static bool TryCreate(KingdomLifecycleBook parent, long disabledAt, long now,
			bool growthEnabled, bool scarcityEnabled, long interval, int cohort, int rulesVersion,
			out KingdomMasterGrowthResumePlan plan, out string failure)
		{
			plan = null; failure = null;
			if (disabledAt < 0L || now < disabledAt || interval <= 0L || cohort < 0
				|| rulesVersion <= 0)
				return Refuse("master growth resume source or clock is invalid", out failure);
			try
			{
				if (!KingdomLifecycleRules.CanOwnAuthority(parent))
					return Refuse("master growth parent authority is invalid", out failure);
				KingdomGrowthBook source = parent.Growth;
				string settlementId = parent.SettlementId;
				object[] references = CaptureReferences(source);
				byte[] before = KingdomLifecycleWireCodec.GrowthPayloadForWrite(source);
				KingdomGrowthBook proposed = KingdomLifecycleWireCodec.ReadGrowthPayload(before);
				if (!Same(before, KingdomLifecycleWireCodec.GrowthPayloadForWrite(proposed)))
					return Refuse("master growth source did not round-trip exactly", out failure);
				bool preserveOnly = source.OpaquePayload != null || source.Quarantined
					|| source.MigrationPending;
				if (!preserveOnly)
				{
					if (!KingdomLifecycleRules.CanOwnGrowthAuthority(source, settlementId))
						return Refuse("master growth source lacks exact growth authority", out failure);
					if (!KingdomLifecycleRules.PrepareMasterGrowthResume(proposed, disabledAt, now,
						growthEnabled, scarcityEnabled, interval, cohort, rulesVersion, out failure)) return false;
				}
				byte[] after = KingdomLifecycleWireCodec.GrowthPayloadForWrite(proposed);
				KingdomGrowthBook decoded = KingdomLifecycleWireCodec.ReadGrowthPayload(after);
				if (!Same(after, KingdomLifecycleWireCodec.GrowthPayloadForWrite(decoded))
					|| (preserveOnly ? !Same(before, after)
						: !KingdomLifecycleRules.CanOwnGrowthAuthority(decoded, settlementId)))
					return Refuse("master growth proposal did not retain canonical authority", out failure);
				var result = new KingdomMasterGrowthResumePlan(parent, source, proposed, settlementId,
					before, after, references, preserveOnly);
				if (!result.CanPublish(parent, out failure)) return false;
				plan = result; return true;
			}
			catch (Exception error)
			{ return Refuse("master growth preparation threw " + error.GetType().Name, out failure); }
		}

		internal bool CanPublish(KingdomLifecycleBook parent, out string failure)
		{
			failure = null;
			try
			{
				if (Published || !ReferenceEquals(parent, Parent) || !ReferenceEquals(parent?.Growth, Source)
					|| parent.SettlementId != SettlementId || !KingdomLifecycleRules.CanOwnAuthority(parent)
					|| !SameReferences(References, CaptureReferences(Source))
					|| !Same(Before, KingdomLifecycleWireCodec.GrowthPayloadForWrite(Source)))
					return Refuse("master growth resume source changed", out failure);
				KingdomGrowthBook trial = KingdomLifecycleWireCodec.ReadGrowthPayload(Before);
				if (!PreserveOnly) KingdomLifecycleRules.CopyMasterGrowthResumeScalars(Proposed, trial);
				if (!Same(After, KingdomLifecycleWireCodec.GrowthPayloadForWrite(trial))
					|| (!PreserveOnly && !KingdomLifecycleRules.CanOwnGrowthAuthority(trial, SettlementId)))
					return Refuse("master growth scalar publication differs from its proposal", out failure);
				return true;
			}
			catch (Exception error)
			{ return Refuse("master growth preflight threw " + error.GetType().Name, out failure); }
		}

		internal bool TryPublish(KingdomLifecycleBook parent, out string failure)
		{
			if (!CanPublish(parent, out failure)) return false;
			PublishPrevalidated(); return true;
		}

		internal void PublishPrevalidated()
		{
			if (Published) throw new InvalidOperationException("master growth plan was already published");
			if (!PreserveOnly) KingdomLifecycleRules.CopyMasterGrowthResumeScalars(Proposed, Source);
			Published = true;
		}

		private static object[] CaptureReferences(KingdomGrowthBook book)
		{
			var refs = new List<object> { book.OpaquePayload, book.ArrivalOpportunity,
				book.ArrivalCandidate, book.FirstGuestTerminal, book.FirstGuestTerminal?.Opportunity };
			AddRows(refs, book.ArrivalDebtRanges); AddRows(refs, book.FieldOps);
			AddRows(refs, book.CropRows); AddRows(refs, book.Resources); AddRows(refs, book.RecentProofs);
			CaptureOperation(refs, book.HeartbeatOp); CaptureOperation(refs, book.ArrivalOp);
			CaptureOperation(refs, book.DepartureOp); CaptureOperation(refs, book.DeliveryOp);
			CaptureOperation(refs, book.FetchOp); CaptureOperation(refs, book.MillOp);
			foreach (KingdomGrowthFieldSlot field in book.FieldOps) CaptureOperation(refs, field?.Operation);
			KingdomGrowthArrivalCandidate candidate = book.ArrivalCandidate;
			if (candidate != null)
			{
				refs.Add(candidate.FirstGuest); refs.Add(candidate.CandidateLease);
				refs.Add(candidate.LodgingLease); refs.Add(candidate.EscrowLease);
				refs.Add(candidate.CreateStep); refs.Add(candidate.DispositionStep);
			}
			return refs.ToArray();
		}

		private static void CaptureOperation(List<object> refs, KingdomGrowthOperation op)
		{
			refs.Add(op); if (op == null) return;
			refs.Add(op.ClockLease); AddRows(refs, op.WaterLegs); AddRows(refs, op.DomainSteps);
			AddRows(refs, op.OutboxEvents); CaptureLegs(refs, op.Sources); CaptureLegs(refs, op.Outputs);
			foreach (KingdomGrowthWaterLeg leg in op.WaterLegs) refs.Add(leg?.Lease);
			foreach (KingdomGrowthOutboxEvent row in op.OutboxEvents) refs.Add(row?.Outbox);
			foreach (KingdomGrowthDomainStep step in op.DomainSteps)
			{
				if (step == null) continue;
				refs.Add(step.Lease); refs.Add(step.ScarcityBefore); refs.Add(step.ScarcityAfter);
				refs.Add(step.AccountingBefore); refs.Add(step.AccountingAfter);
				refs.Add(step.FieldBefore); refs.Add(step.FieldAfter);
				AddRows(refs, step.CropRowsBefore); AddRows(refs, step.CropRowsDeclaredAfter);
				AddRows(refs, step.CropRowsAfter);
			}
		}

		private static void CaptureLegs(List<object> refs, List<KingdomGrowthObjectLeg> legs)
		{
			AddRows(refs, legs); if (legs == null) return;
			foreach (KingdomGrowthObjectLeg leg in legs)
				if (leg != null) { refs.Add(leg.Lease); AddRows(refs, leg.Callbacks); }
		}

		private static void AddRows<T>(List<object> refs, List<T> rows) where T : class
		{
			refs.Add(rows); if (rows == null) return;
			foreach (T row in rows) refs.Add(row);
		}

		private static bool SameReferences(object[] left, object[] right)
		{
			if (left.Length != right.Length) return false;
			for (int i = 0; i < left.Length; i++) if (!ReferenceEquals(left[i], right[i])) return false;
			return true;
		}

		private static bool Same(byte[] left, byte[] right)
		{
			if (left == null || right == null || left.Length != right.Length) return false;
			for (int i = 0; i < left.Length; i++) if (left[i] != right[i]) return false;
			return true;
		}

		private static bool Refuse(string reason, out string failure)
		{ failure = reason; return false; }
	}
}
