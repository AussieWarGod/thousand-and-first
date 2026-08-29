using System.IO;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityCodec
	{
		private static void WriteRoute(BinaryWriter W, KingdomPolityRouteRecord V)
		{
			WriteString(W, V.RouteId); WriteString(W, V.EventStreamId); WriteString(W, V.OriginId);
			WriteString(W, V.DestinationId); WriteStrings(W, V.OrderedPath, KingdomPolityRules.MaxPath);
			W.Write((byte)V.Mode); W.Write((byte)V.Purpose); W.Write((byte)V.Phase);
			W.Write(V.DepartureOrdinal); W.Write(V.DepartureTick); W.Write(V.SegmentIndex);
			W.Write(V.NextDueTick); WriteString(W, V.ManifestOrErrandId);
			WriteString(W, V.CounterpartyRef); WriteString(W, V.FrontId);
			WriteString(W, V.DepartureReceiptId); WriteString(W, V.DeliveryReceiptId);
			WriteString(W, V.ReturnReceiptId); WriteString(W, V.ActiveManifestationId);
		}

		private static KingdomPolityRouteRecord ReadRoute(BinaryReader R)
		{
			return new KingdomPolityRouteRecord
			{
				RouteId = ReadString(R), EventStreamId = ReadString(R), OriginId = ReadString(R),
				DestinationId = ReadString(R), OrderedPath = ReadStrings(R, KingdomPolityRules.MaxPath),
				Mode = (KingdomPolityRouteMode)R.ReadByte(),
				Purpose = (KingdomPolityRoutePurpose)R.ReadByte(),
				Phase = (KingdomPolityRoutePhase)R.ReadByte(), DepartureOrdinal = R.ReadUInt64(),
				DepartureTick = R.ReadInt64(), SegmentIndex = R.ReadInt32(), NextDueTick = R.ReadInt64(),
				ManifestOrErrandId = ReadString(R), CounterpartyRef = ReadString(R),
				FrontId = ReadString(R), DepartureReceiptId = ReadString(R),
				DeliveryReceiptId = ReadString(R), ReturnReceiptId = ReadString(R),
				ActiveManifestationId = ReadString(R)
			};
		}

		private static void WriteGrievance(BinaryWriter W, KingdomPolityGrievanceRecord V)
		{
			WriteString(W, V.GrievanceId); WriteString(W, V.IssuerPolityId);
			WriteString(W, V.TargetPolityId); W.Write((byte)V.Cause); WriteString(W, V.SourceEventId);
			W.Write(V.Severity); WriteStrings(W, V.EvidenceRefs, KingdomPolityRules.MaxRefs);
			W.Write((byte)V.Phase); WriteString(W, V.ConsumedByIncidentId);
			WriteString(W, V.ResolutionRef);
		}

		private static KingdomPolityGrievanceRecord ReadGrievance(BinaryReader R)
		{
			return new KingdomPolityGrievanceRecord
			{
				GrievanceId = ReadString(R), IssuerPolityId = ReadString(R),
				TargetPolityId = ReadString(R), Cause = (KingdomPolityGrievanceCause)R.ReadByte(),
				SourceEventId = ReadString(R), Severity = R.ReadInt32(),
				EvidenceRefs = ReadStrings(R, KingdomPolityRules.MaxRefs),
				Phase = (KingdomPolityGrievancePhase)R.ReadByte(),
				ConsumedByIncidentId = ReadString(R), ResolutionRef = ReadString(R)
			};
		}

		private static void WriteFront(BinaryWriter W, KingdomPolityFrontRecord V)
		{
			WriteString(W, V.FrontId); W.Write((byte)V.TargetKind); WriteString(W, V.TargetRef);
			W.Write(V.PressureBand); W.Write(V.NextDueEventTick);
			WriteStrings(W, V.GrievanceRefs, KingdomPolityRules.MaxRefs); W.Write((byte)V.Phase);
		}

		private static KingdomPolityFrontRecord ReadFront(BinaryReader R)
		{
			return new KingdomPolityFrontRecord
			{
				FrontId = ReadString(R), TargetKind = (KingdomPolityFrontTarget)R.ReadByte(),
				TargetRef = ReadString(R), PressureBand = R.ReadInt32(),
				NextDueEventTick = R.ReadInt64(),
				GrievanceRefs = ReadStrings(R, KingdomPolityRules.MaxRefs),
				Phase = (KingdomPolityFrontPhase)R.ReadByte()
			};
		}

		private static void WriteCohort(BinaryWriter W, KingdomPolityCohortPlan V)
		{
			WriteString(W, V.CohortId); W.Write((byte)V.Purpose); WriteString(W, V.SourceRef);
			WriteString(W, V.PolityId); WriteString(W, V.ProfileId); W.Write(V.ProfileRevision);
			W.Write(V.MinimumLevel); W.Write(V.MaximumLevel); WriteString(W, V.SurfaceRef);
			W.Write(V.ScaleBudget); WriteStrings(W, V.RoleSlots, KingdomPolityRules.MaxCohortMembers);
			WriteList(W, V.ResolvedMembers, KingdomPolityRules.MaxCohortMembers, WriteMember);
			W.Write(V.NamedRepresentativeAllowance); WriteString(W, V.EventStreamId);
			W.Write(V.RulesVersion); W.Write(V.EventOrdinal);
			W.Write((byte)V.PresentationOptionKind); W.Write(V.PresentationEnableEpoch);
			W.Write(V.PresentationReservedTick); W.Write((byte)V.Phase);
			WriteString(W, V.ManifestationReceiptId); WriteString(W, V.RewardEventId);
		}

		private static KingdomPolityCohortPlan ReadCohort(BinaryReader R)
		{
			return ReadCohortCore(R, false);
		}

		private static KingdomPolityCohortPlan ReadCohortV6(BinaryReader R)
		{
			return ReadCohortCore(R, true);
		}

		private static KingdomPolityCohortPlan ReadCohortCore(BinaryReader R, bool AdmitAbandoned)
		{
			KingdomPolityCohortPlan value = new KingdomPolityCohortPlan
			{
				CohortId = ReadString(R), Purpose = (KingdomPolityCohortPurpose)R.ReadByte(),
				SourceRef = ReadString(R), PolityId = ReadString(R), ProfileId = ReadString(R),
				ProfileRevision = R.ReadInt32(), MinimumLevel = R.ReadInt32(),
				MaximumLevel = R.ReadInt32(), SurfaceRef = ReadString(R), ScaleBudget = R.ReadInt32(),
				RoleSlots = ReadStrings(R, KingdomPolityRules.MaxCohortMembers),
				ResolvedMembers = ReadList(R, KingdomPolityRules.MaxCohortMembers, ReadMember),
				NamedRepresentativeAllowance = R.ReadInt32(), EventStreamId = ReadString(R),
				RulesVersion = R.ReadInt32(), EventOrdinal = R.ReadUInt64(),
				PresentationOptionKind = (KingdomExperienceOptionKind)R.ReadByte(),
				PresentationEnableEpoch = R.ReadInt64(),
				PresentationReservedTick = R.ReadInt64(),
				Phase = ReadCohortPhase(R, AdmitAbandoned),
				ManifestationReceiptId = ReadString(R), RewardEventId = ReadString(R)
			}; return value;
		}

		private static void WriteCohortLegacy(BinaryWriter W, KingdomPolityCohortPlan V)
		{
			WriteString(W, V.CohortId); W.Write((byte)V.Purpose); WriteString(W, V.SourceRef);
			WriteString(W, V.PolityId); WriteString(W, V.ProfileId); W.Write(V.ProfileRevision);
			W.Write(V.MinimumLevel); W.Write(V.MaximumLevel); WriteString(W, V.SurfaceRef);
			W.Write(V.ScaleBudget); WriteStrings(W, V.RoleSlots, KingdomPolityRules.MaxCohortMembers);
			WriteList(W, V.ResolvedMembers, KingdomPolityRules.MaxCohortMembers, WriteMember);
			W.Write(V.NamedRepresentativeAllowance); WriteString(W, V.EventStreamId);
			W.Write(V.RulesVersion); W.Write(V.EventOrdinal); W.Write((byte)V.Phase);
			WriteString(W, V.ManifestationReceiptId); WriteString(W, V.RewardEventId);
		}

		private static KingdomPolityCohortPlan ReadCohortLegacy(BinaryReader R)
		{
			return new KingdomPolityCohortPlan
			{
				CohortId = ReadString(R), Purpose = (KingdomPolityCohortPurpose)R.ReadByte(),
				SourceRef = ReadString(R), PolityId = ReadString(R), ProfileId = ReadString(R),
				ProfileRevision = R.ReadInt32(), MinimumLevel = R.ReadInt32(),
				MaximumLevel = R.ReadInt32(), SurfaceRef = ReadString(R), ScaleBudget = R.ReadInt32(),
				RoleSlots = ReadStrings(R, KingdomPolityRules.MaxCohortMembers),
				ResolvedMembers = ReadList(R, KingdomPolityRules.MaxCohortMembers, ReadMember),
				NamedRepresentativeAllowance = R.ReadInt32(), EventStreamId = ReadString(R),
				RulesVersion = R.ReadInt32(), EventOrdinal = R.ReadUInt64(),
				Phase = ReadCohortPhase(R, false),
				ManifestationReceiptId = ReadString(R), RewardEventId = ReadString(R)
			};
		}

		private static KingdomPolityCohortPhase ReadCohortPhase(BinaryReader R,
			bool AdmitAbandoned)
		{
			byte phase = R.ReadByte();
			if (phase > (AdmitAbandoned ? (byte)6 : (byte)5))
				throw new InvalidDataException("Polity cohort phase is not admitted by this wire version.");
			return (KingdomPolityCohortPhase)phase;
		}

		private static void WriteMember(BinaryWriter W, KingdomPolityCohortMember V)
		{
			W.Write(V.Ordinal); WriteString(W, V.MemberKey); WriteString(W, V.BlueprintKey);
			WriteString(W, V.LoadoutKey); WriteString(W, V.SignatureKey);
		}

		private static KingdomPolityCohortMember ReadMember(BinaryReader R)
		{
			return new KingdomPolityCohortMember
			{
				Ordinal = R.ReadInt32(), MemberKey = ReadString(R), BlueprintKey = ReadString(R),
				LoadoutKey = ReadString(R), SignatureKey = ReadString(R)
			};
		}
	}
}
