using System;

namespace ThousandAndFirst
{
	public static partial class KingdomConstructionInputRules
	{
		public static KingdomConstructionInputDecision DecidePhysicalMutation(
			string BeforeWitnessHash, string AfterWitnessHash, string ObservedWitnessHash,
			bool Paused)
		{
			if (!ValidDigest(BeforeWitnessHash) || !ValidDigest(AfterWitnessHash)
				|| !ValidDigest(ObservedWitnessHash)
				|| FixedEquals(BeforeWitnessHash, AfterWitnessHash))
				return KingdomConstructionInputDecision.Invalid;
			if (FixedEquals(ObservedWitnessHash, AfterWitnessHash))
				return KingdomConstructionInputDecision.Acknowledge;
			if (!FixedEquals(ObservedWitnessHash, BeforeWitnessHash))
				return KingdomConstructionInputDecision.Quarantine;
			return Paused ? KingdomConstructionInputDecision.WaitPaused
				: KingdomConstructionInputDecision.Apply;
		}

		public static bool TrySetPaused(KingdomConstructionInputReceipt Receipt,
			int ExpectedRevision, long Now, bool Paused,
			out KingdomConstructionInputReceipt Updated,
			out KingdomConstructionInputFault Fault)
		{
			Updated = null;
			if (!Expected(Receipt, ExpectedRevision, out Fault)) return false;
			if (Now < 0L || ExpectedRevision == int.MaxValue || Paused == Receipt.Paused)
				return Refuse(KingdomConstructionInputFault.Pause, out Fault);
			long start = Receipt.PauseStartedTick;
			long elapsed = Receipt.PausedTicks;
			if (Paused) start = Now;
			else
			{
				if (Now < start || long.MaxValue - elapsed < Now - start)
					return Refuse(KingdomConstructionInputFault.Pause, out Fault);
				elapsed += Now - start; start = -1L;
			}
			Updated = Receipt.Copy(Receipt.TxPhase, ExpectedRevision + 1,
				start, elapsed, null, null, null);
			if (!TryValidate(Updated, out Fault)) { Updated = null; return false; }
			return true;
		}

		/// <summary>Stages the master switch's already-observed disabled span in one CAS.
		/// Disabled wakes stay O(1); the resume coordinator is the only caller that walks owners.</summary>
		public static bool TryRebaseMasterPause(KingdomConstructionInputReceipt Receipt,
			int ExpectedRevision, long DisabledAt, long Now,
			out KingdomConstructionInputReceipt Updated,
			out KingdomConstructionInputFault Fault)
		{
			Updated = null;
			if (!Expected(Receipt, ExpectedRevision, out Fault)) return false;
			if (DisabledAt < 0L || Now <= DisabledAt || Receipt.Paused
				|| ExpectedRevision == int.MaxValue)
				return Refuse(KingdomConstructionInputFault.Pause, out Fault);
			long span = Now - DisabledAt;
			if (Receipt.PausedTicks > long.MaxValue - span)
				return Refuse(KingdomConstructionInputFault.Pause, out Fault);
			Updated = Receipt.Copy(Receipt.TxPhase, ExpectedRevision + 1, -1L,
				Receipt.PausedTicks + span, null, null, null);
			if (!TryValidate(Updated, out Fault)) { Updated = null; return false; }
			return true;
		}

		public static bool TryEffectiveArrivalTick(long FrozenArrivalTick,
			long PausedTicks, out long EffectiveArrivalTick)
		{
			EffectiveArrivalTick = 0L;
			if (FrozenArrivalTick < 0L || PausedTicks < 0L
				|| FrozenArrivalTick > long.MaxValue - PausedTicks) return false;
			EffectiveArrivalTick = FrozenArrivalTick + PausedTicks;
			return true;
		}

		public static bool ExactIntentBinding(KingdomConstructionInputReceipt Receipt,
			KingdomConstructionInputIntent Intent, long OwnerEpoch)
		{
			string digest;
			KingdomConstructionInputFault fault;
			KingdomConstructionInputFault validation;
			return TryValidate(Receipt, out validation) && Intent != null
				&& Receipt.OwnerEpoch == OwnerEpoch
				&& Receipt.ConstructionJobId == Intent.ConstructionJobId
				&& Receipt.OwnerKey == Intent.OwnerKey && Receipt.TargetZoneId == Intent.ZoneId
				&& Receipt.WaterRequested == Intent.WaterRequested
				&& Receipt.MaterialRequestedClaim == Intent.MaterialRequestedClaim
				&& TryIntentDigest(Intent, out digest, out fault)
				&& FixedEquals(Receipt.ConstructionIntentDigest, digest);
		}

		public static bool ExactChildBinding(KingdomConstructionInputReceipt Receipt,
			int ChildOrdinal, int JobId, int TripId, string OwnerOperationId,
			int ManifestVersion, string ManifestDigest)
		{
			KingdomConstructionInputFault validation;
			if (!TryValidate(Receipt, out validation) || ChildOrdinal < 0
				|| ChildOrdinal >= Receipt.ChildCount || ManifestVersion != Receipt.Schema
				|| OwnerOperationId != Receipt.ConstructionJobId
				|| !FixedEquals(ManifestDigest, Receipt.PlanDigest)) return false;
			KingdomConstructionInputChild child = Receipt.ChildAt(ChildOrdinal);
			return child.JobId == JobId && child.TripId == TripId;
		}

		public static bool TryDeriveConservation(KingdomConstructionInputReceipt Receipt,
			KingdomConstructionInputKind Kind, out KingdomConstructionInputConservation Conservation,
			out KingdomConstructionInputFault Fault)
		{
			Conservation = null;
			if (Receipt == null) return Refuse(KingdomConstructionInputFault.Null, out Fault);
			if (!Defined(Kind)) return Refuse(KingdomConstructionInputFault.Amount, out Fault);
			if (!TryValidate(Receipt, out Fault)) return false;
			long expected = 0, source = 0, flight = 0, landed = 0, spent = 0;
			long compensating = 0, quarantined = 0, lost = 0;
			for (int i = 0; i < Receipt.CargoCount; i++)
			{
				KingdomConstructionInputCargoLine cargo = Receipt.CargoAt(i);
				if (cargo.Kind != Kind) continue;
				KingdomConstructionInputSourceLine line = Receipt.SourceAt(cargo.SourceLineOrdinal);
				long provedLost = (long)line.ProvedLost + cargo.Lost;
				if (provedLost > cargo.Amount || cargo.Spent + provedLost > cargo.Amount)
					return Refuse(KingdomConstructionInputFault.Conservation, out Fault);
				long remainder = cargo.Amount - cargo.Spent - provedLost;
				expected += cargo.Amount; spent += cargo.Spent; lost += provedLost;
				switch (cargo.Phase)
				{
				case KingdomConstructionInputCargoPhase.InFlight: flight += remainder; break;
				case KingdomConstructionInputCargoPhase.Landed:
				case KingdomConstructionInputCargoPhase.DebitIntent: landed += remainder; break;
				case KingdomConstructionInputCargoPhase.CompensationIntent:
					compensating += remainder; break;
				case KingdomConstructionInputCargoPhase.Quarantined:
					quarantined += remainder; break;
				default: source += remainder; break;
				}
			}
			long total = source + flight + landed + spent + compensating + quarantined + lost;
			if (total != expected || expected > int.MaxValue)
				return Refuse(KingdomConstructionInputFault.Conservation, out Fault);
			Conservation = new KingdomConstructionInputConservation((int)expected, (int)source,
				(int)flight, (int)landed, (int)spent, (int)compensating,
				(int)quarantined, (int)lost);
			Fault = KingdomConstructionInputFault.None;
			return true;
		}
	}
}
