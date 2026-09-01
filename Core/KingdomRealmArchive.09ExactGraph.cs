using System;
using System.Collections.Generic;
using System.IO;

namespace ThousandAndFirst
{
	public sealed partial class KingdomRealmArchive
	{
		private static bool ExactStrings(List<string> Archived, List<string> Current)
		{
			if (Archived == null || Current == null || ReferenceEquals(Archived, Current) ||
				Archived.Count != Current.Count) return false;
			for (int i = 0; i < Archived.Count; i++)
				if (!string.Equals(Archived[i], Current[i], StringComparison.Ordinal)) return false;
			return true;
		}

		internal static bool ExactDictionary(Dictionary<string, int> Archived,
			Dictionary<string, int> Current)
		{
			if (Archived == null || Current == null || Archived.Count != Current.Count) return false;
			foreach (KeyValuePair<string, int> row in Archived)
				if (!Current.TryGetValue(row.Key, out int value) || value != row.Value) return false;
			return true;
		}

		private static bool ExactBindings(Simulation.City.KingdomBindingRegistry Archived,
			Simulation.City.KingdomBindingRegistry Current)
		{
			return Archived != null && Current != null && !ReferenceEquals(Archived, Current) &&
				!ReferenceEquals(Archived.Keys, Current.Keys) &&
				ExactList(Archived.Keys, Current.Keys) && ExactList(Archived.Kinds, Current.Kinds) &&
				ExactList(Archived.ZoneIds, Current.ZoneIds) &&
				ExactList(Archived.ObjectIds, Current.ObjectIds) &&
				ExactList(Archived.MintedTicks, Current.MintedTicks);
		}

		private static bool ExactJobs(Simulation.City.KingdomJobRegistry Archived,
			Simulation.City.KingdomJobRegistry Current)
		{
			return Archived != null && Current != null && !ReferenceEquals(Archived, Current) &&
				Archived.JobCounter == Current.JobCounter &&
				ExactList(Archived.JobIds, Current.JobIds) && ExactList(Archived.Kinds, Current.Kinds) &&
				ExactList(Archived.Cargos, Current.Cargos) &&
				ExactList(Archived.CargoAmounts, Current.CargoAmounts) &&
				ExactList(Archived.SourceZoneIds, Current.SourceZoneIds) &&
				ExactList(Archived.DestZoneIds, Current.DestZoneIds) &&
				ExactList(Archived.StartTicks, Current.StartTicks) &&
				ExactList(Archived.WalkTicksPerCell, Current.WalkTicksPerCell) &&
				ExactList(Archived.Statuses, Current.Statuses) &&
				ExactList(Archived.OriginCodes, Current.OriginCodes) &&
				ExactList(Archived.DepositLegIndexes, Current.DepositLegIndexes) &&
				ExactList(Archived.SubjectIds, Current.SubjectIds) &&
				ExactList(Archived.SubjectNames, Current.SubjectNames) &&
				ExactList(Archived.TargetNames, Current.TargetNames) &&
				ExactList(Archived.DueTicks, Current.DueTicks) &&
				ExactList(Archived.WaterCosts, Current.WaterCosts) &&
				ExactList(Archived.ProvisionCosts, Current.ProvisionCosts) &&
				ExactList(Archived.OutcomeCodes, Current.OutcomeCodes) &&
				ExactList(Archived.ExpeditionDeedDispositions,
					Current.ExpeditionDeedDispositions) &&
				ExactList(Archived.ExpeditionDeedPolityIds,
					Current.ExpeditionDeedPolityIds) &&
				ExactList(Archived.ExpeditionDeedCauseRefs,
					Current.ExpeditionDeedCauseRefs) &&
				ExactList(Archived.ExpeditionDeedFigureRefs,
					Current.ExpeditionDeedFigureRefs) &&
				ExactList(Archived.DeliverySourceEndpointIds,
					Current.DeliverySourceEndpointIds) &&
				ExactList(Archived.DeliverySourceObjectIds,
					Current.DeliverySourceObjectIds) &&
				ExactList(Archived.DeliverySourceXs, Current.DeliverySourceXs) &&
				ExactList(Archived.DeliverySourceYs, Current.DeliverySourceYs) &&
				ExactList(Archived.DeliveryTargetEndpointIds,
					Current.DeliveryTargetEndpointIds) &&
				ExactList(Archived.DeliveryTargetObjectIds,
					Current.DeliveryTargetObjectIds) &&
				ExactList(Archived.DeliveryTargetXs, Current.DeliveryTargetXs) &&
				ExactList(Archived.DeliveryTargetYs, Current.DeliveryTargetYs) &&
				ExactList(Archived.DeliverySourceBeforeAmounts,
					Current.DeliverySourceBeforeAmounts) &&
				ExactList(Archived.DeliveryTripIds, Current.DeliveryTripIds) &&
				ExactList(Archived.DeliveryStopOrdinals, Current.DeliveryStopOrdinals) &&
				ExactList(Archived.DeliveryPhases, Current.DeliveryPhases) &&
				ExactList(Archived.DeliveryCargoAuthorityKinds,
					Current.DeliveryCargoAuthorityKinds) &&
				ExactList(Archived.DeliveryOwnerOperationIds,
					Current.DeliveryOwnerOperationIds) &&
				ExactList(Archived.DeliveryOwnerManifestVersions,
					Current.DeliveryOwnerManifestVersions) &&
				ExactList(Archived.DeliveryOwnerManifestDigests,
					Current.DeliveryOwnerManifestDigests) &&
				ExactList(Archived.DeliveryOwnerManifestRevisions,
					Current.DeliveryOwnerManifestRevisions) &&
				ExactList(Archived.DeliveryManifestSourceStarts,
					Current.DeliveryManifestSourceStarts) &&
				ExactList(Archived.DeliveryManifestSourceCounts,
					Current.DeliveryManifestSourceCounts) &&
				ExactList(Archived.DeliveryTargetBeforeAmounts,
					Current.DeliveryTargetBeforeAmounts) &&
				ExactList(Archived.DeliveryTargetReceiptStates,
					Current.DeliveryTargetReceiptStates) &&
				ExactList(Archived.LegCounts, Current.LegCounts) &&
				ExactList(Archived.LegZoneIds, Current.LegZoneIds) &&
				ExactList(Archived.LegEnterX, Current.LegEnterX) &&
				ExactList(Archived.LegEnterY, Current.LegEnterY) &&
				ExactList(Archived.LegExitX, Current.LegExitX) &&
				ExactList(Archived.LegExitY, Current.LegExitY) &&
				ExactList(Archived.LegLengths, Current.LegLengths) &&
				ExactList(Archived.LegDepartTicks, Current.LegDepartTicks) &&
				ExactList(Archived.LegArriveTicks, Current.LegArriveTicks);
		}

		private static bool ExactList<T>(List<T> Archived, List<T> Current)
		{
			if (Archived == null || Current == null || ReferenceEquals(Archived, Current) ||
				Archived.Count != Current.Count) return false;
			EqualityComparer<T> comparer = EqualityComparer<T>.Default;
			for (int i = 0; i < Archived.Count; i++)
				if (!comparer.Equals(Archived[i], Current[i])) return false;
			return true;
		}

		private static bool ExactHaul(KingdomCarryHaul Archived, KingdomCarryHaul Current)
		{
			if (Archived == null || Current == null) return Archived == null && Current == null;
			return !ReferenceEquals(Archived, Current) && Archived.OriginZoneID == Current.OriginZoneID &&
				Archived.OriginX == Current.OriginX && Archived.OriginY == Current.OriginY &&
				Archived.DestinationSettlementId == Current.DestinationSettlementId &&
				Archived.DestinationSettlementName == Current.DestinationSettlementName &&
				Archived.PlantedTick == Current.PlantedTick && Archived.DueTick == Current.DueTick &&
				Archived.Mud == Current.Mud && Archived.Brush == Current.Brush &&
				Archived.Timber == Current.Timber && Archived.Stone == Current.Stone &&
				Archived.Marble == Current.Marble && Archived.Scrap == Current.Scrap;
		}

		private static bool ExactCarry(KingdomCarryBook Archived, KingdomCarryBook Current)
		{
			if (Archived == null || Current == null) return Archived == null && Current == null;
			if (ReferenceEquals(Archived, Current) ||
				!TryCarryBytes(Archived, out byte[] left, out string _) ||
				!TryCarryBytes(Current, out byte[] right, out string _) || left.Length != right.Length)
				return false;
			int difference = 0;
			for (int i = 0; i < left.Length; i++) difference |= left[i] ^ right[i];
			return difference == 0;
		}

		private static bool TryCarryBytes(KingdomCarryBook Value, out byte[] Bytes,
			out string Failure)
		{
			Bytes = null;
			Failure = null;
			try
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8, true))
				{
					KingdomLifecycleWireCodec.WriteCarry(writer, Value);
					writer.Flush();
					if (stream.Length > KingdomArchivedSettlementCodec.MaxPayloadBytes)
						throw new InvalidDataException("Archived carry book exceeds cap.");
					Bytes = stream.ToArray();
					return true;
				}
			}
			catch (Exception ex)
			{
				Failure = Bound(ex.Message, 512);
				return false;
			}
		}

		private static void WriteGraphBytes(BinaryWriter Writer, byte[] Value)
		{
			if (Value == null || Value.Length > KingdomArchivedSettlementCodec.MaxPayloadBytes)
				throw new InvalidDataException("Realm graph byte block exceeds cap.");
			Writer.Write(Value.Length); Writer.Write(Value);
		}

		private static void WriteGraphString(BinaryWriter Writer, string Value)
		{
			if (Value == null) { Writer.Write(-1); return; }
			int count = StrictUtf8.GetByteCount(Value);
			if (count > MaxTextBytes) throw new InvalidDataException("Realm graph string exceeds cap.");
			Writer.Write(count); Writer.Write(StrictUtf8.GetBytes(Value));
		}

		private static void WriteGraphStrings(BinaryWriter Writer, List<string> Value)
		{
			if (Value == null || Value.Count > KingdomChronicle.MaxEntries)
				throw new InvalidDataException("Realm graph list exceeds cap.");
			Writer.Write(Value.Count);
			for (int i = 0; i < Value.Count; i++) WriteGraphString(Writer, Value[i]);
		}

		private static void WriteGraphDictionary(BinaryWriter Writer,
			Dictionary<string, int> Value)
		{
			if (!BoundedStandings(Value)) throw new InvalidDataException("Realm graph map exceeds cap.");
			List<string> keys = new List<string>(Value.Keys); keys.Sort(StringComparer.Ordinal);
			Writer.Write(keys.Count);
			for (int i = 0; i < keys.Count; i++)
			{
				WriteGraphString(Writer, keys[i]); Writer.Write(Value[keys[i]]);
			}
		}

		private static void WriteGraphBindings(BinaryWriter Writer,
			Simulation.City.KingdomBindingRegistry Value)
		{
			if (!ValidBindings(Value)) throw new InvalidDataException("Realm binding graph is invalid.");
			Writer.Write(Value.Keys.Count);
			for (int i = 0; i < Value.Keys.Count; i++)
			{
				Writer.Write(Value.Keys[i]); Writer.Write(Value.Kinds[i]);
				WriteGraphString(Writer, Value.ZoneIds[i]); WriteGraphString(Writer, Value.ObjectIds[i]);
				Writer.Write(Value.MintedTicks[i]);
			}
		}

		private static void WriteGraphJobs(BinaryWriter Writer,
			Simulation.City.KingdomJobRegistry Value)
		{
			if (!ValidJobs(Value)) throw new InvalidDataException("Realm job graph is invalid.");
			Writer.Write(Value.JobCounter); Writer.Write(Value.JobIds.Count);
			for (int i = 0; i < Value.JobIds.Count; i++)
			{
				Writer.Write(Value.JobIds[i]); Writer.Write(Value.Kinds[i]); Writer.Write(Value.Cargos[i]);
				Writer.Write(Value.CargoAmounts[i]); WriteGraphString(Writer, Value.SourceZoneIds[i]);
				WriteGraphString(Writer, Value.DestZoneIds[i]); Writer.Write(Value.StartTicks[i]);
				Writer.Write(Value.WalkTicksPerCell[i]); Writer.Write(Value.Statuses[i]);
				Writer.Write(Value.OriginCodes[i]); Writer.Write(Value.DepositLegIndexes[i]);
				Writer.Write(Value.SubjectIds[i]); WriteGraphString(Writer, Value.SubjectNames[i]);
				WriteGraphString(Writer, Value.TargetNames[i]); Writer.Write(Value.DueTicks[i]);
				Writer.Write(Value.WaterCosts[i]); Writer.Write(Value.ProvisionCosts[i]);
				Writer.Write(Value.OutcomeCodes[i]);
				WriteGraphString(Writer, Value.ExpeditionDeedPolityIds[i]);
				WriteGraphString(Writer, Value.ExpeditionDeedCauseRefs[i]);
				WriteGraphString(Writer, Value.ExpeditionDeedFigureRefs[i]);
				Writer.Write(Value.ExpeditionDeedDispositions[i]);
				Writer.Write(Value.DeliverySourceEndpointIds[i]);
				WriteGraphString(Writer, Value.DeliverySourceObjectIds[i]);
				Writer.Write(Value.DeliverySourceXs[i]); Writer.Write(Value.DeliverySourceYs[i]);
				Writer.Write(Value.DeliveryTargetEndpointIds[i]);
				WriteGraphString(Writer, Value.DeliveryTargetObjectIds[i]);
				Writer.Write(Value.DeliveryTargetXs[i]); Writer.Write(Value.DeliveryTargetYs[i]);
				Writer.Write(Value.DeliverySourceBeforeAmounts[i]);
				Writer.Write(Value.DeliveryTripIds[i]);
				Writer.Write(Value.DeliveryStopOrdinals[i]);
				Writer.Write(Value.DeliveryPhases[i]);
				Writer.Write(Value.DeliveryCargoAuthorityKinds[i]);
				WriteGraphString(Writer, Value.DeliveryOwnerOperationIds[i]);
				Writer.Write(Value.DeliveryOwnerManifestVersions[i]);
				WriteGraphString(Writer, Value.DeliveryOwnerManifestDigests[i]);
				Writer.Write(Value.DeliveryOwnerManifestRevisions[i]);
				Writer.Write(Value.DeliveryManifestSourceStarts[i]);
				Writer.Write(Value.DeliveryManifestSourceCounts[i]);
				Writer.Write(Value.DeliveryTargetBeforeAmounts[i]);
				Writer.Write(Value.DeliveryTargetReceiptStates[i]);
				Writer.Write(Value.LegCounts[i]);
			}
			Writer.Write(Value.LegZoneIds.Count);
			for (int i = 0; i < Value.LegZoneIds.Count; i++)
			{
				WriteGraphString(Writer, Value.LegZoneIds[i]); Writer.Write(Value.LegEnterX[i]);
				Writer.Write(Value.LegEnterY[i]); Writer.Write(Value.LegExitX[i]);
				Writer.Write(Value.LegExitY[i]); Writer.Write(Value.LegLengths[i]);
				Writer.Write(Value.LegDepartTicks[i]); Writer.Write(Value.LegArriveTicks[i]);
			}
		}

		private static void WriteGraphHaul(BinaryWriter Writer, KingdomCarryHaul Value)
		{
			Writer.Write(Value == null ? (byte)0 : (byte)1);
			if (Value == null) return;
			WriteGraphString(Writer, Value.OriginZoneID); Writer.Write(Value.OriginX);
			Writer.Write(Value.OriginY); WriteGraphString(Writer, Value.DestinationSettlementId);
			WriteGraphString(Writer, Value.DestinationSettlementName); Writer.Write(Value.PlantedTick);
			Writer.Write(Value.DueTick); Writer.Write(Value.Mud); Writer.Write(Value.Brush);
			Writer.Write(Value.Timber); Writer.Write(Value.Stone); Writer.Write(Value.Marble);
			Writer.Write(Value.Scrap);
		}

	}
}
