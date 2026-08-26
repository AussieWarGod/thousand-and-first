using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleWireCodec
	{
		private static void WriteGrowthArrivalCandidate(BinaryWriter w,
			KingdomGrowthArrivalCandidate x, int wireVersion)
		{
			w.Write(x != null); if (x == null) return;
			w.Write(x.Sequence); S(w, x.Id, true); S(w, x.PlanHash, true);
			S(w, x.SettlementId, true); w.Write(x.CreatedTick); w.Write(x.UpdatedTick);
			w.Write((byte)x.Phase); w.Write((byte)x.EvidencePhase);
			if (wireVersion != KingdomLifecycleRules.LegacyGrowthFormatVersion)
				w.Write(x.LegacyGrowthV1UnboundZone);
			if (wireVersion >= KingdomLifecycleRules.CurrentGrowthFormatVersion)
			{
				w.Write(x.LegacySemanticPlan); w.Write(x.SemanticPlanVersion);
				S(w, x.SemanticStreamId, true); w.Write(x.SemanticEventKind);
				S(w, x.PlannedOrigin, false); S(w, x.PlannedCreed, false);
				S(w, x.PlannedName, false); S(w, x.PlannedArrived, false);
				w.Write(x.ArrivalX); w.Write(x.ArrivalY);
			}
			w.Write((byte)x.Disposition); w.Write((byte)x.RefusalReason); S(w, x.ObjectId, true);
			S(w, x.Marker, true); S(w, x.Blueprint, false); S(w, x.EscrowKey, true);
			WriteLease(w, x.CandidateLease); WriteLease(w, x.LodgingLease);
			WriteLease(w, x.EscrowLease);
			WriteGrowthOptionalObjectCallback(w, x.CreateStep);
			WriteGrowthOptionalObjectCallback(w, x.DispositionStep); S(w, x.LodgingZoneId, false);
			w.Write(x.LodgingX); w.Write(x.LodgingY); S(w, x.LodgingBeforeGraphHash, true);
			S(w, x.LodgingDeclaredGraphHash, true); S(w, x.LodgingReceiptGraphHash, true);
			S(w, x.LodgingCallbackReferenceHash, true); w.Write(x.LodgingSameReference);
			S(w, x.LodgingReceiptId, true); w.Write((byte)x.LodgingState);
			S(w, x.ConsumingOperationId, true); w.Write(x.ConsumingOperationSequence);
			S(w, x.Fault, false, true);
		}

		private static KingdomGrowthArrivalCandidate ReadGrowthArrivalCandidate(BinaryReader r,
			int wireVersion)
		{
			if (!ReadExactBoolean(r)) return null;
			bool currentSemantic = wireVersion >=
				KingdomLifecycleRules.CurrentGrowthFormatVersion;
			KingdomGrowthArrivalCandidate result = new KingdomGrowthArrivalCandidate
			{
				Sequence = r.ReadInt64(), Id = S(r, true), PlanHash = S(r, true),
				SettlementId = S(r, true), CreatedTick = r.ReadInt64(), UpdatedTick = r.ReadInt64(),
				Phase = (KingdomGrowthArrivalCandidatePhase)r.ReadByte(),
				EvidencePhase = (KingdomGrowthArrivalCandidatePhase)r.ReadByte(),
				LegacyGrowthV1UnboundZone = wireVersion ==
					KingdomLifecycleRules.LegacyGrowthFormatVersion ? false : ReadExactBoolean(r),
				LegacySemanticPlan = !currentSemantic || ReadExactBoolean(r),
				SemanticPlanVersion = currentSemantic ? r.ReadInt32() : 0,
				SemanticStreamId = currentSemantic ? S(r, true) : null,
				SemanticEventKind = currentSemantic ? r.ReadUInt32() : 0U,
				PlannedOrigin = currentSemantic ? S(r, false) : null,
				PlannedCreed = currentSemantic ? S(r, false) : null,
				PlannedName = currentSemantic ? S(r, false) : null,
				PlannedArrived = currentSemantic ? S(r, false) : null,
				ArrivalX = currentSemantic ? r.ReadInt32() : -1,
				ArrivalY = currentSemantic ? r.ReadInt32() : -1,
				Disposition = (KingdomGrowthArrivalDisposition)r.ReadByte(),
				RefusalReason = (KingdomGrowthArrivalRefusalReason)r.ReadByte(),
				ObjectId = S(r, true),
				Marker = S(r, true), Blueprint = S(r, false), EscrowKey = S(r, true),
				CandidateLease = ReadLease(r), LodgingLease = ReadLease(r),
				EscrowLease = ReadLease(r),
				CreateStep = ReadGrowthOptionalObjectCallback(r),
				DispositionStep = ReadGrowthOptionalObjectCallback(r), LodgingZoneId = S(r, false),
				LodgingX = r.ReadInt32(), LodgingY = r.ReadInt32(),
				LodgingBeforeGraphHash = S(r, true), LodgingDeclaredGraphHash = S(r, true),
				LodgingReceiptGraphHash = S(r, true), LodgingCallbackReferenceHash = S(r, true),
				LodgingSameReference = ReadExactBoolean(r), LodgingReceiptId = S(r, true),
				LodgingState = (KingdomLifecyclePhysicalState)r.ReadByte(),
				ConsumingOperationId = S(r, true), ConsumingOperationSequence = r.ReadInt64(),
				Fault = S(r, false, true)
			};
			KingdomGrowthArrivalCandidatePhase phase = result.Phase ==
				KingdomGrowthArrivalCandidatePhase.Quarantined
					? result.EvidencePhase : result.Phase;
			if (wireVersion == KingdomLifecycleRules.LegacyGrowthFormatVersion
				&& result.LodgingZoneId == null
				&& (byte)phase <= (byte)KingdomGrowthArrivalCandidatePhase.Escrowed)
				result.LegacyGrowthV1UnboundZone = true;
			return result;
		}

		private static void WriteGrowthOptionalObjectCallback(BinaryWriter w,
			KingdomGrowthObjectCallbackStep x)
		{
			w.Write(x != null); if (x != null) WriteGrowthObjectCallback(w, x);
		}

		private static KingdomGrowthObjectCallbackStep ReadGrowthOptionalObjectCallback(BinaryReader r)
		{
			return ReadExactBoolean(r) ? ReadGrowthObjectCallback(r) : null;
		}

		private static KingdomGrowthBook PoisonGrowth(string Fault)
		{
			return new KingdomGrowthBook
			{
				FormatVersion = KingdomLifecycleRules.CurrentGrowthFormatVersion,
				Quarantined = true,
				Fault = BoundFault(Fault)
			};
		}

		private static KingdomGrowthBook OpaqueGrowth(byte[] Payload, int WireVersion,
			string Fault)
		{
			return new KingdomGrowthBook
			{
				FormatVersion = KingdomLifecycleRules.CurrentGrowthFormatVersion,
				Quarantined = true,
				Fault = BoundFault(Fault),
				OpaqueWireVersion = WireVersion,
				OpaquePayload = Payload == null ? null : (byte[])Payload.Clone()
			};
		}

		private static string BoundFault(string Fault)
		{
			if (string.IsNullOrEmpty(Fault)) return "growth payload was rejected";
			return Fault.Length <= KingdomLifecycleRules.MaxTextChars ? Fault :
				Fault.Substring(0, KingdomLifecycleRules.MaxTextChars);
		}

	}
}
