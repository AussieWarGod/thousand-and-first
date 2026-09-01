using System;
using System.IO;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst
{
	public sealed partial class KingdomRealmArchive
	{
#if !TAF_TESTS
		private static void WriteBindings(SerializationWriter Writer,
			Simulation.City.KingdomBindingRegistry Value)
		{
			if (!ValidBindings(Value)) throw new InvalidDataException("Invalid archived binding columns.");
			Writer.Write(Value.Keys.Count);
			for (int i = 0; i < Value.Keys.Count; i++)
			{
				Writer.Write(Value.Keys[i]); Writer.Write(Value.Kinds[i]);
				WriteString(Writer, Value.ZoneIds[i], 512); WriteString(Writer, Value.ObjectIds[i], 512);
				Writer.Write(Value.MintedTicks[i]);
			}
		}

		private static void WriteHaul(SerializationWriter Writer, KingdomCarryHaul Value)
		{
			Writer.Write(Value == null ? (byte)0 : (byte)1);
			if (Value == null) return;
			WriteString(Writer, Value.OriginZoneID, 512);
			Writer.Write(Value.OriginX); Writer.Write(Value.OriginY);
			WriteString(Writer, Value.DestinationSettlementId, 256);
			WriteString(Writer, Value.DestinationSettlementName, 512);
			Writer.Write(Value.PlantedTick); Writer.Write(Value.DueTick);
			Writer.Write(Value.Mud); Writer.Write(Value.Brush); Writer.Write(Value.Timber);
			Writer.Write(Value.Stone); Writer.Write(Value.Marble); Writer.Write(Value.Scrap);
		}

		private static KingdomCarryHaul ReadHaul(SerializationReader Reader)
		{
			byte present = Reader.ReadByte();
			if (present > 1) throw new InvalidDataException(
				"Realm archive haul flag is noncanonical.");
			if (present == 0) return null;
			return new KingdomCarryHaul
			{
				OriginZoneID = ReadString(Reader, 512),
				OriginX = Reader.ReadInt32(),
				OriginY = Reader.ReadInt32(),
				DestinationSettlementId = ReadString(Reader, 256),
				DestinationSettlementName = ReadString(Reader, 512),
				PlantedTick = Reader.ReadInt64(),
				DueTick = Reader.ReadInt64(),
				Mud = Reader.ReadInt32(),
				Brush = Reader.ReadInt32(),
				Timber = Reader.ReadInt32(),
				Stone = Reader.ReadInt32(),
				Marble = Reader.ReadInt32(),
				Scrap = Reader.ReadInt32()
			};
		}

		private static Simulation.City.KingdomBindingRegistry ReadBindings(SerializationReader Reader)
		{
			int count = Reader.ReadInt32();
			if (count < 0 || count > MaxBindings) throw new InvalidDataException("Archived binding count exceeds cap.");
			Simulation.City.KingdomBindingRegistry value = new Simulation.City.KingdomBindingRegistry();
			for (int i = 0; i < count; i++)
			{
				value.Keys.Add(Reader.ReadInt32()); value.Kinds.Add(Reader.ReadInt32());
				value.ZoneIds.Add(ReadString(Reader, 512)); value.ObjectIds.Add(ReadString(Reader, 512));
				value.MintedTicks.Add(Reader.ReadInt64());
			}
			return value;
		}

		private static void WriteJobs(SerializationWriter Writer,
			Simulation.City.KingdomJobRegistry Value)
		{
			if (!ValidJobs(Value)) throw new InvalidDataException("Invalid archived job columns.");
			Writer.Write(Value.JobCounter); Writer.Write(Value.JobIds.Count);
			for (int i = 0; i < Value.JobIds.Count; i++)
			{
				Writer.Write(Value.JobIds[i]); Writer.Write(Value.Kinds[i]); Writer.Write(Value.Cargos[i]);
				Writer.Write(Value.CargoAmounts[i]); WriteString(Writer, Value.SourceZoneIds[i], 512);
				WriteString(Writer, Value.DestZoneIds[i], 512); Writer.Write(Value.StartTicks[i]);
				Writer.Write(Value.WalkTicksPerCell[i]); Writer.Write(Value.Statuses[i]);
				Writer.Write(Value.OriginCodes[i]); Writer.Write(Value.DepositLegIndexes[i]);
				Writer.Write(Value.SubjectIds[i]); WriteString(Writer, Value.SubjectNames[i], 512);
				WriteString(Writer, Value.TargetNames[i], 512); Writer.Write(Value.DueTicks[i]);
				Writer.Write(Value.WaterCosts[i]); Writer.Write(Value.ProvisionCosts[i]);
				Writer.Write(Value.OutcomeCodes[i]);
				Writer.Write(Value.DeliverySourceEndpointIds[i]);
				WriteString(Writer, Value.DeliverySourceObjectIds[i], 512);
				Writer.Write(Value.DeliverySourceXs[i]); Writer.Write(Value.DeliverySourceYs[i]);
				Writer.Write(Value.DeliveryTargetEndpointIds[i]);
				WriteString(Writer, Value.DeliveryTargetObjectIds[i], 512);
				Writer.Write(Value.DeliveryTargetXs[i]); Writer.Write(Value.DeliveryTargetYs[i]);
				Writer.Write(Value.DeliverySourceBeforeAmounts[i]);
				Writer.Write(Value.DeliveryTripIds[i]);
				Writer.Write(Value.DeliveryStopOrdinals[i]);
				Writer.Write(Value.DeliveryPhases[i]);
				Writer.Write(Value.DeliveryCargoAuthorityKinds[i]);
				WriteString(Writer, Value.DeliveryOwnerOperationIds[i], 512);
				Writer.Write(Value.DeliveryOwnerManifestVersions[i]);
				WriteString(Writer, Value.DeliveryOwnerManifestDigests[i], 512);
				Writer.Write(Value.DeliveryOwnerManifestRevisions[i]);
				Writer.Write(Value.DeliveryManifestSourceStarts[i]);
				Writer.Write(Value.DeliveryManifestSourceCounts[i]);
				Writer.Write(Value.DeliveryTargetBeforeAmounts[i]);
				Writer.Write(Value.DeliveryTargetReceiptStates[i]);
				Writer.Write(Value.ExpeditionDeedDispositions[i]);
				WriteString(Writer, Value.ExpeditionDeedPolityIds[i], 512);
				WriteString(Writer, Value.ExpeditionDeedCauseRefs[i], 512);
				WriteString(Writer, Value.ExpeditionDeedFigureRefs[i], 512);
				Writer.Write(Value.LegCounts[i]);
			}
			Writer.Write(Value.LegZoneIds.Count);
			for (int i = 0; i < Value.LegZoneIds.Count; i++)
			{
				WriteString(Writer, Value.LegZoneIds[i], 512); Writer.Write(Value.LegEnterX[i]);
				Writer.Write(Value.LegEnterY[i]); Writer.Write(Value.LegExitX[i]);
				Writer.Write(Value.LegExitY[i]); Writer.Write(Value.LegLengths[i]);
				Writer.Write(Value.LegDepartTicks[i]); Writer.Write(Value.LegArriveTicks[i]);
			}
		}

		private static Simulation.City.KingdomJobRegistry ReadJobs(SerializationReader Reader,
			int WireVersion)
		{
			Simulation.City.KingdomJobRegistry value = new Simulation.City.KingdomJobRegistry();
			value.JobCounter = Reader.ReadInt32();
			int jobs = Reader.ReadInt32();
			if (jobs < 0 || jobs > MaxJobs) throw new InvalidDataException("Archived job count exceeds cap.");
			for (int i = 0; i < jobs; i++)
			{
				value.JobIds.Add(Reader.ReadInt32()); value.Kinds.Add(Reader.ReadInt32());
				value.Cargos.Add(Reader.ReadInt32()); value.CargoAmounts.Add(Reader.ReadInt32());
				value.SourceZoneIds.Add(ReadString(Reader, 512)); value.DestZoneIds.Add(ReadString(Reader, 512));
				value.StartTicks.Add(Reader.ReadInt64()); value.WalkTicksPerCell.Add(Reader.ReadInt32());
				value.Statuses.Add(Reader.ReadInt32()); value.OriginCodes.Add(Reader.ReadInt32());
				value.DepositLegIndexes.Add(Reader.ReadInt32());
				if (WireVersion >= MissionJobVersion)
				{
					value.SubjectIds.Add(Reader.ReadInt32());
					value.SubjectNames.Add(ReadString(Reader, 512));
					value.TargetNames.Add(ReadString(Reader, 512));
					value.DueTicks.Add(Reader.ReadInt64());
					value.WaterCosts.Add(Reader.ReadInt32());
					value.ProvisionCosts.Add(Reader.ReadInt32());
					value.OutcomeCodes.Add(Reader.ReadInt32());
				}
				if (WireVersion >= ExactDeliveryJobVersion)
				{
					value.DeliverySourceEndpointIds.Add(Reader.ReadInt32());
					value.DeliverySourceObjectIds.Add(ReadString(Reader, 512));
					value.DeliverySourceXs.Add(Reader.ReadInt32());
					value.DeliverySourceYs.Add(Reader.ReadInt32());
					value.DeliveryTargetEndpointIds.Add(Reader.ReadInt32());
					value.DeliveryTargetObjectIds.Add(ReadString(Reader, 512));
					value.DeliveryTargetXs.Add(Reader.ReadInt32());
					value.DeliveryTargetYs.Add(Reader.ReadInt32());
					value.DeliverySourceBeforeAmounts.Add(Reader.ReadInt64());
					value.DeliveryTripIds.Add(Reader.ReadInt32());
					value.DeliveryStopOrdinals.Add(Reader.ReadInt32());
					value.DeliveryPhases.Add(Reader.ReadInt32());
					value.DeliveryCargoAuthorityKinds.Add(Reader.ReadInt32());
					value.DeliveryOwnerOperationIds.Add(ReadString(Reader, 512));
					value.DeliveryOwnerManifestVersions.Add(Reader.ReadInt32());
					value.DeliveryOwnerManifestDigests.Add(ReadString(Reader, 512));
					value.DeliveryOwnerManifestRevisions.Add(Reader.ReadInt64());
					value.DeliveryManifestSourceStarts.Add(Reader.ReadInt32());
					value.DeliveryManifestSourceCounts.Add(Reader.ReadInt32());
					value.DeliveryTargetBeforeAmounts.Add(Reader.ReadInt64());
					value.DeliveryTargetReceiptStates.Add(Reader.ReadInt32());
				}
				if (WireVersion >= ExpeditionResultJobVersion)
				{
					value.ExpeditionDeedDispositions.Add(Reader.ReadInt32());
					value.ExpeditionDeedPolityIds.Add(ReadString(Reader, 512));
					value.ExpeditionDeedCauseRefs.Add(ReadString(Reader, 512));
					value.ExpeditionDeedFigureRefs.Add(ReadString(Reader, 512));
				}
				value.LegCounts.Add(Reader.ReadInt32());
			}
			int legs = Reader.ReadInt32();
			if (legs < 0 || legs > MaxLegs) throw new InvalidDataException("Archived leg count exceeds cap.");
			for (int i = 0; i < legs; i++)
			{
				value.LegZoneIds.Add(ReadString(Reader, 512)); value.LegEnterX.Add(Reader.ReadInt32());
				value.LegEnterY.Add(Reader.ReadInt32()); value.LegExitX.Add(Reader.ReadInt32());
				value.LegExitY.Add(Reader.ReadInt32()); value.LegLengths.Add(Reader.ReadInt32());
				value.LegDepartTicks.Add(Reader.ReadInt64()); value.LegArriveTicks.Add(Reader.ReadInt64());
			}
			if (WireVersion == ExactDeliveryJobVersion)
				for (int i = 0; i < value.JobIds.Count; i++)
					if (value.DeliveryCargoAuthorityKinds[i]
							> (int)Simulation.City.KingdomDeliveryCargoAuthority.CarryBookManifest
						|| value.DeliveryPhases[i]
							> (int)Simulation.City.KingdomDeliveryPhase.Quarantined)
						throw new InvalidDataException(
							"Realm archive v4 contains a future delivery enum value.");
			if (WireVersion < ExpeditionResultJobVersion) value.Normalize();
			if (!ValidJobs(value)) throw new InvalidDataException("Archived job columns are inconsistent.");
			return value;
		}
#endif
	}
}
