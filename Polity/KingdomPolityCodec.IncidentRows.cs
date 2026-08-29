using System.IO;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityCodec
	{
		private static void WriteFigure(BinaryWriter W, KingdomPolityNamedFigureRecord V)
		{
			WriteFigureLegacy(W, V); W.Write(V.ResidentId);
			WriteString(W, V.ResidentSettlementId);
		}

		private static void WriteFigureLegacy(BinaryWriter W, KingdomPolityNamedFigureRecord V)
		{
			WriteString(W, V.FigureId); WriteString(W, V.PolityId); WriteString(W, V.DisplayName);
			WriteString(W, V.RoleKey); W.Write((byte)V.Origin); W.Write((byte)V.Phase);
			WriteString(W, V.CauseRef); WriteString(W, V.ChronicleRef); WriteString(W, V.ConclusionRef);
		}

		private static KingdomPolityNamedFigureRecord ReadFigure(BinaryReader R)
		{
			KingdomPolityNamedFigureRecord value = ReadFigureLegacy(R);
			value.ResidentId = R.ReadInt32(); value.ResidentSettlementId = ReadString(R);
			return value;
		}

		private static KingdomPolityNamedFigureRecord ReadFigureLegacy(BinaryReader R)
		{
			return new KingdomPolityNamedFigureRecord
			{
				FigureId = ReadString(R), PolityId = ReadString(R), DisplayName = ReadString(R),
				RoleKey = ReadString(R), Origin = (KingdomPolityFigureOrigin)R.ReadByte(),
				Phase = (KingdomPolityFigurePhase)R.ReadByte(), CauseRef = ReadString(R),
				ChronicleRef = ReadString(R), ConclusionRef = ReadString(R)
			};
		}

		private static void WriteIncident(BinaryWriter W, KingdomPolityIncidentRecord V)
		{
			WriteIncidentLegacy(W, V);
			WriteNullable(W, V.Hospitality, WriteHospitality);
			WriteNullable(W, V.Intervention, WriteIntervention);
			WriteNullable(W, V.Aftermath, WriteAftermath);
		}

		private static void WriteIncidentLegacy(BinaryWriter W, KingdomPolityIncidentRecord V)
		{
			WriteString(W, V.IncidentPlanId); WriteString(W, V.IncidentId);
			WriteStrings(W, V.GrievanceRefs, KingdomPolityRules.MaxRefs);
			WriteStrings(W, V.ParticipantCohortRefs, KingdomPolityRules.MaxRefs);
			WriteStrings(W, V.DisclosedStakeRefs, KingdomPolityRules.MaxRefs);
			W.Write(V.MaxSystemicWound); W.Write((byte)V.Purpose); WriteString(W, V.EventStreamId);
			W.Write(V.RulesVersion); W.Write(V.EventOrdinal);
			WriteStrings(W, V.EligibleSurfaceRefs, KingdomPolityRules.MaxRefs);
			WriteStrings(W, V.InterventionOptionKeys, KingdomPolityRules.MaxRefs);
			WriteNullable(W, V.Conclusion, WriteConclusion);
		}

		private static KingdomPolityIncidentRecord ReadIncident(BinaryReader R)
		{
			KingdomPolityIncidentRecord value = ReadIncidentLegacy(R);
			value.Hospitality = ReadNullable(R, ReadHospitality);
			value.Intervention = ReadNullable(R, ReadIntervention);
			value.Aftermath = ReadNullable(R, ReadAftermath);
			return value;
		}

		private static KingdomPolityIncidentRecord ReadIncidentLegacy(BinaryReader R)
		{
			return new KingdomPolityIncidentRecord
			{
				IncidentPlanId = ReadString(R), IncidentId = ReadString(R),
				GrievanceRefs = ReadStrings(R, KingdomPolityRules.MaxRefs),
				ParticipantCohortRefs = ReadStrings(R, KingdomPolityRules.MaxRefs),
				DisclosedStakeRefs = ReadStrings(R, KingdomPolityRules.MaxRefs),
				MaxSystemicWound = R.ReadInt32(), Purpose = (KingdomPolityCohortPurpose)R.ReadByte(),
				EventStreamId = ReadString(R), RulesVersion = R.ReadInt32(), EventOrdinal = R.ReadUInt64(),
				EligibleSurfaceRefs = ReadStrings(R, KingdomPolityRules.MaxRefs),
				InterventionOptionKeys = ReadStrings(R, KingdomPolityRules.MaxRefs),
				Conclusion = ReadNullable(R, ReadConclusion)
			};
		}

		private static void WriteHospitality(BinaryWriter W,
			KingdomPolityHospitalityTransaction V)
		{
			WriteString(W, V.TransactionId); WriteString(W, V.TermsPlanId);
			WriteString(W, V.SurfaceRef); WriteString(W, V.ZoneId); W.Write((byte)V.Phase);
			W.Write(V.PlannedTick); W.Write(V.DebitedTick);
			WriteList(W, V.Lines, KingdomPolityHospitalityRules.RequiredDebitLines,
				WriteHospitalityLine);
			WriteString(W, V.PlanDigest); WriteNullable(W, V.Proof, WriteHospitalityProof);
			WriteString(W, V.Fault);
		}

		private static KingdomPolityHospitalityTransaction ReadHospitality(BinaryReader R)
		{
			return new KingdomPolityHospitalityTransaction
			{
				TransactionId = ReadString(R), TermsPlanId = ReadString(R),
				SurfaceRef = ReadString(R), ZoneId = ReadString(R),
				Phase = (KingdomPolityHospitalityPhase)R.ReadByte(),
				PlannedTick = R.ReadInt64(), DebitedTick = R.ReadInt64(),
				Lines = ReadList(R, KingdomPolityHospitalityRules.RequiredDebitLines,
					ReadHospitalityLine),
				PlanDigest = ReadString(R), Proof = ReadNullable(R, ReadHospitalityProof),
				Fault = ReadString(R)
			};
		}

		private static void WriteHospitalityLine(BinaryWriter W,
			KingdomPolityHospitalityDebitLine V)
		{
			W.Write((byte)V.Kind); WriteString(W, V.ContainerId); WriteString(W, V.ObjectId);
			WriteString(W, V.Blueprint); W.Write(V.Before); W.Write(V.After); W.Write(V.Capacity);
		}

		private static KingdomPolityHospitalityDebitLine ReadHospitalityLine(BinaryReader R)
		{
			return new KingdomPolityHospitalityDebitLine
			{
				Kind = (KingdomPolityHospitalityDebitKind)R.ReadByte(),
				ContainerId = ReadString(R), ObjectId = ReadString(R), Blueprint = ReadString(R),
				Before = R.ReadInt32(), After = R.ReadInt32(), Capacity = R.ReadInt32()
			};
		}

		private static void WriteHospitalityProof(BinaryWriter W,
			KingdomPolityHospitalityProof V)
		{
			WriteString(W, V.ProofId); WriteString(W, V.SourceAuthorityId);
			WriteString(W, V.ItemOrServingId); W.Write(V.BeforeQuantity);
			W.Write(V.AfterQuantity); W.Write(V.ConsumedQuantity); WriteString(W, V.ReceiptId);
			WriteString(W, V.ObservedFactId); W.Write(V.CommitTick); WriteString(W, V.ProofDigest);
		}

		private static KingdomPolityHospitalityProof ReadHospitalityProof(BinaryReader R)
		{
			return new KingdomPolityHospitalityProof
			{
				ProofId = ReadString(R), SourceAuthorityId = ReadString(R),
				ItemOrServingId = ReadString(R), BeforeQuantity = R.ReadInt64(),
				AfterQuantity = R.ReadInt64(), ConsumedQuantity = R.ReadInt64(),
				ReceiptId = ReadString(R), ObservedFactId = ReadString(R),
				CommitTick = R.ReadInt64(), ProofDigest = ReadString(R)
			};
		}

		private static void WriteConclusion(BinaryWriter W, KingdomPolityIncidentConclusion V)
		{
			WriteString(W, V.ConclusionId); W.Write((byte)V.ResolutionKind); W.Write(V.CommitTick);
			WriteStrings(W, V.ObservedFactIds, KingdomPolityRules.MaxObservedFacts);
			WriteList(W, V.SystemicDeltas, KingdomPolityRules.MaxDeltas, WriteSystemicDelta);
			WriteList(W, V.RelationDeltas, KingdomPolityRules.MaxDeltas, WriteRelationDelta);
			WriteStrings(W, V.ReceiptRefs, KingdomPolityRules.MaxRefs);
			WriteString(W, V.ConsentReceiptId); WriteString(W, V.EscrowReceiptId);
			WriteString(W, V.SnapshotReceiptId);
		}

		private static KingdomPolityIncidentConclusion ReadConclusion(BinaryReader R)
		{
			return new KingdomPolityIncidentConclusion
			{
				ConclusionId = ReadString(R), ResolutionKind = (KingdomPolityResolutionKind)R.ReadByte(),
				CommitTick = R.ReadInt64(),
				ObservedFactIds = ReadStrings(R, KingdomPolityRules.MaxObservedFacts),
				SystemicDeltas = ReadList(R, KingdomPolityRules.MaxDeltas, ReadSystemicDelta),
				RelationDeltas = ReadList(R, KingdomPolityRules.MaxDeltas, ReadRelationDelta),
				ReceiptRefs = ReadStrings(R, KingdomPolityRules.MaxRefs),
				ConsentReceiptId = ReadString(R), EscrowReceiptId = ReadString(R),
				SnapshotReceiptId = ReadString(R)
			};
		}

		private static void WriteSystemicDelta(BinaryWriter W, KingdomPolitySystemicDelta V)
		{
			W.Write((byte)V.Kind); WriteString(W, V.TargetId); W.Write(V.Amount);
			WriteString(W, V.ReceiptId);
		}

		private static KingdomPolitySystemicDelta ReadSystemicDelta(BinaryReader R)
		{
			return new KingdomPolitySystemicDelta
			{
				Kind = (KingdomPolitySystemicDeltaKind)R.ReadByte(), TargetId = ReadString(R),
				Amount = R.ReadInt32(), ReceiptId = ReadString(R)
			};
		}

		private static void WriteRelationDelta(BinaryWriter W, KingdomPolityRelationDelta V)
		{
			WriteString(W, V.RelationId); W.Write((byte)V.Before); W.Write((byte)V.After);
			WriteString(W, V.ReceiptId);
		}

		private static KingdomPolityRelationDelta ReadRelationDelta(BinaryReader R)
		{
			return new KingdomPolityRelationDelta
			{
				RelationId = ReadString(R), Before = (KingdomPolityRelationBand)R.ReadByte(),
				After = (KingdomPolityRelationBand)R.ReadByte(), ReceiptId = ReadString(R)
			};
		}
	}
}
