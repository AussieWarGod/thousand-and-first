using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	public sealed partial class KingdomRealmArchive
	{
		private static string Bound(string Value, int Maximum)
		{
			if (string.IsNullOrEmpty(Value)) return "realm archive requires inspection";
			string bounded = Value.Length <= Maximum ? Value : Value.Substring(0, Maximum);
			try
			{
				if (StrictUtf8.GetByteCount(bounded) <= MaxTextBytes) return bounded;
				bounded = bounded.Substring(0, Math.Min(2048, bounded.Length));
				return StrictUtf8.GetByteCount(bounded) <= MaxTextBytes
					? bounded : "realm archive requires inspection";
			}
			catch (EncoderFallbackException)
			{
				return "realm archive requires inspection";
			}
		}

		internal static List<string> CloneStrings(List<string> Value)
		{
			return Value == null ? null : new List<string>(Value);
		}

		internal static Dictionary<string, int> CloneStandings(Dictionary<string, int> Value)
		{
			return Value == null ? null : new Dictionary<string, int>(Value,
				StringComparer.Ordinal);
		}

		internal static Simulation.City.KingdomBindingRegistry CloneBindings(
			Simulation.City.KingdomBindingRegistry Value)
		{
			if (Value == null) return null;
			return new Simulation.City.KingdomBindingRegistry
			{
				Keys = new List<int>(Value.Keys), Kinds = new List<int>(Value.Kinds),
				ZoneIds = new List<string>(Value.ZoneIds),
				ObjectIds = new List<string>(Value.ObjectIds),
				MintedTicks = new List<long>(Value.MintedTicks)
			};
		}

		internal static Simulation.City.KingdomJobRegistry CloneJobs(
			Simulation.City.KingdomJobRegistry Value)
		{
			if (Value == null) return null;
			return new Simulation.City.KingdomJobRegistry
			{
				JobCounter = Value.JobCounter,
				JobIds = new List<int>(Value.JobIds), Kinds = new List<int>(Value.Kinds),
				Cargos = new List<int>(Value.Cargos),
				CargoAmounts = new List<int>(Value.CargoAmounts),
				SourceZoneIds = new List<string>(Value.SourceZoneIds),
				DestZoneIds = new List<string>(Value.DestZoneIds),
				StartTicks = new List<long>(Value.StartTicks),
				WalkTicksPerCell = new List<int>(Value.WalkTicksPerCell),
				Statuses = new List<int>(Value.Statuses),
				OriginCodes = new List<int>(Value.OriginCodes),
				DepositLegIndexes = new List<int>(Value.DepositLegIndexes),
				SubjectIds = new List<int>(Value.SubjectIds),
				SubjectNames = new List<string>(Value.SubjectNames),
				TargetNames = new List<string>(Value.TargetNames),
				DueTicks = new List<long>(Value.DueTicks),
				WaterCosts = new List<int>(Value.WaterCosts),
				ProvisionCosts = new List<int>(Value.ProvisionCosts),
				OutcomeCodes = new List<int>(Value.OutcomeCodes),
				ExpeditionDeedDispositions = new List<int>(Value.ExpeditionDeedDispositions),
				ExpeditionDeedPolityIds = new List<string>(Value.ExpeditionDeedPolityIds),
				ExpeditionDeedCauseRefs = new List<string>(Value.ExpeditionDeedCauseRefs),
				ExpeditionDeedFigureRefs = new List<string>(Value.ExpeditionDeedFigureRefs),
				DeliverySourceEndpointIds = new List<int>(Value.DeliverySourceEndpointIds),
				DeliverySourceObjectIds = new List<string>(Value.DeliverySourceObjectIds),
				DeliverySourceXs = new List<int>(Value.DeliverySourceXs),
				DeliverySourceYs = new List<int>(Value.DeliverySourceYs),
				DeliveryTargetEndpointIds = new List<int>(Value.DeliveryTargetEndpointIds),
				DeliveryTargetObjectIds = new List<string>(Value.DeliveryTargetObjectIds),
				DeliveryTargetXs = new List<int>(Value.DeliveryTargetXs),
				DeliveryTargetYs = new List<int>(Value.DeliveryTargetYs),
				DeliverySourceBeforeAmounts = new List<long>(Value.DeliverySourceBeforeAmounts),
				DeliveryTripIds = new List<int>(Value.DeliveryTripIds),
				DeliveryStopOrdinals = new List<int>(Value.DeliveryStopOrdinals),
				DeliveryPhases = new List<int>(Value.DeliveryPhases),
				DeliveryCargoAuthorityKinds = new List<int>(Value.DeliveryCargoAuthorityKinds),
				DeliveryOwnerOperationIds = new List<string>(Value.DeliveryOwnerOperationIds),
				DeliveryOwnerManifestVersions = new List<int>(Value.DeliveryOwnerManifestVersions),
				DeliveryOwnerManifestDigests = new List<string>(Value.DeliveryOwnerManifestDigests),
				DeliveryOwnerManifestRevisions = new List<long>(Value.DeliveryOwnerManifestRevisions),
				DeliveryManifestSourceStarts = new List<int>(Value.DeliveryManifestSourceStarts),
				DeliveryManifestSourceCounts = new List<int>(Value.DeliveryManifestSourceCounts),
				DeliveryTargetBeforeAmounts = new List<long>(Value.DeliveryTargetBeforeAmounts),
				DeliveryTargetReceiptStates = new List<int>(Value.DeliveryTargetReceiptStates),
				LegCounts = new List<int>(Value.LegCounts),
				LegZoneIds = new List<string>(Value.LegZoneIds),
				LegEnterX = new List<int>(Value.LegEnterX),
				LegEnterY = new List<int>(Value.LegEnterY),
				LegExitX = new List<int>(Value.LegExitX),
				LegExitY = new List<int>(Value.LegExitY),
				LegLengths = new List<int>(Value.LegLengths),
				LegDepartTicks = new List<long>(Value.LegDepartTicks),
				LegArriveTicks = new List<long>(Value.LegArriveTicks)
			};
		}

		internal static KingdomCarryHaul CloneHaul(KingdomCarryHaul Value)
		{
			if (Value == null) return null;
			return new KingdomCarryHaul
			{
				OriginZoneID = Value.OriginZoneID, OriginX = Value.OriginX, OriginY = Value.OriginY,
				DestinationSettlementId = Value.DestinationSettlementId,
				DestinationSettlementName = Value.DestinationSettlementName,
				PlantedTick = Value.PlantedTick, DueTick = Value.DueTick,
				Mud = Value.Mud, Brush = Value.Brush, Timber = Value.Timber,
				Stone = Value.Stone, Marble = Value.Marble, Scrap = Value.Scrap
			};
		}

		internal static bool TryCloneCarry(KingdomCarryBook Value,
			out KingdomCarryBook Clone, out string Failure)
		{
			Clone = null;
			Failure = null;
			if (Value == null) return true;
			try
			{
				using (MemoryStream stream = new MemoryStream())
				{
					using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8, true))
						KingdomLifecycleWireCodec.WriteCarry(writer, Value);
					if (stream.Length > KingdomArchivedSettlementCodec.MaxPayloadBytes)
						throw new InvalidDataException("Archived carry book exceeds cap.");
					stream.Position = 0L;
					Clone = new KingdomCarryBook();
					using (BinaryReader reader = new BinaryReader(stream, StrictUtf8, true))
						KingdomLifecycleWireCodec.ReadCarry(reader, Clone);
					if (stream.Position != stream.Length)
						throw new InvalidDataException("Archived carry book has trailing bytes.");
					return true;
				}
			}
			catch (Exception ex)
			{
				Failure = Bound(ex.Message, 512);
				Clone = null;
				return false;
			}
		}

	}
}
