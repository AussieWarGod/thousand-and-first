using System;
using System.IO;
using System.Text;

using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>Immutable central proof used to bind one parent child to the exact frozen
	/// itinerary. The parent persists these facts; only central logistics canonicalizes them.</summary>
	internal readonly struct KingdomConstructionInputRouteProof
	{
		internal readonly int JobId;
		internal readonly int TripId;
		internal readonly int CargoStart;
		internal readonly int CargoCount;
		internal readonly int SourceEndpointId;
		internal readonly string SourceObjectId;
		internal readonly string SourceZoneId;
		internal readonly int SourceX;
		internal readonly int SourceY;
		internal readonly int TargetEndpointId;
		internal readonly string TargetObjectId;
		internal readonly string TargetZoneId;
		internal readonly int TargetX;
		internal readonly int TargetY;
		internal readonly long ArrivalTick;
		internal readonly string RouteDigest;

		internal KingdomConstructionInputRouteProof(KingdomJobRow row, long arrivalTick,
			string routeDigest)
		{
			JobId = row.JobId;
			TripId = row.DeliveryTripId;
			CargoStart = row.DeliveryManifestSourceStart;
			CargoCount = row.DeliveryManifestSourceCount;
			SourceEndpointId = row.DeliverySourceEndpointId;
			SourceObjectId = row.DeliverySourceObjectId ?? "";
			SourceZoneId = row.SourceZoneId;
			SourceX = row.DeliverySourceX;
			SourceY = row.DeliverySourceY;
			TargetEndpointId = row.DeliveryTargetEndpointId;
			TargetObjectId = row.DeliveryTargetObjectId ?? "";
			TargetZoneId = row.DestZoneId;
			TargetX = row.DeliveryTargetX;
			TargetY = row.DeliveryTargetY;
			ArrivalTick = arrivalTick;
			RouteDigest = routeDigest;
		}
	}

	internal static partial class KingdomCentralLogistics
	{
		internal static bool TryDescribeConstructionInputReservation(KingdomSystem system,
			string ownerOperationId, int jobId, out KingdomConstructionInputRouteProof proof,
			out KingdomCityFault fault)
		{
			proof = default(KingdomConstructionInputRouteProof);
			fault = KingdomCityFault.NullArgument;
			KingdomJobTable table;
			KingdomJobRow row;
			KingdomLeg last;
			if (system == null || system.Jobs == null || string.IsNullOrEmpty(ownerOperationId)
				|| jobId <= 0 || !system.Jobs.TryRead(out table, out fault)
				|| !table.TryGet(jobId, out row)) return false;
			if (row.DeliveryCargoAuthority
					!= KingdomDeliveryCargoAuthority.ConstructionInput
				|| row.Cargo != KingdomStockKind.OpaqueManifest
				|| row.CargoAmount != row.DeliveryManifestSourceCount
				|| row.JobId != row.DeliveryTripId || row.DeliveryStopOrdinal != 1
				|| row.DeliveryManifestSourceStart < 0
				|| row.DeliveryManifestSourceCount <= 0
				|| !string.Equals(row.DeliveryOwnerOperationId, ownerOperationId,
					StringComparison.Ordinal) || !row.TryLeg(row.LegCount - 1, out last))
			{ fault = KingdomCityFault.DuplicateBinding; return false; }
			string digest;
			if (!TryConstructionInputRouteDigest(row, out digest, out fault)) return false;
			proof = new KingdomConstructionInputRouteProof(row, last.ArriveTick, digest);
			fault = KingdomCityFault.None;
			return true;
		}

		private static bool TryConstructionInputRouteDigest(KingdomJobRow row,
			out string digest, out KingdomCityFault fault)
		{
			digest = null;
			byte[] payload;
			try
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, new UTF8Encoding(false, true)))
				{
					WriteConstructionInputRouteText(writer, "TAF-CONSTRUCTION-ROUTE-1");
					writer.Write(row.JobId); writer.Write(row.DeliveryTripId);
					WriteConstructionInputRouteText(writer, row.DeliveryOwnerOperationId);
					writer.Write((int)row.DeliveryCargoAuthority); writer.Write((int)row.Cargo);
					writer.Write(row.CargoAmount); writer.Write(row.StartTick);
					writer.Write(row.WalkTicksPerCell); writer.Write(row.DepositLegIndex);
					writer.Write(row.DeliverySourceEndpointId);
					WriteConstructionInputRouteText(writer, row.DeliverySourceObjectId);
					WriteConstructionInputRouteText(writer, row.SourceZoneId);
					writer.Write(row.DeliverySourceX); writer.Write(row.DeliverySourceY);
					writer.Write(row.DeliveryTargetEndpointId);
					WriteConstructionInputRouteText(writer, row.DeliveryTargetObjectId);
					WriteConstructionInputRouteText(writer, row.DestZoneId);
					writer.Write(row.DeliveryTargetX); writer.Write(row.DeliveryTargetY);
					writer.Write(row.DeliveryManifestSourceStart);
					writer.Write(row.DeliveryManifestSourceCount); writer.Write(row.LegCount);
					for (int i = 0; i < row.LegCount; i++)
					{
						KingdomLeg leg;
						if (!row.TryLeg(i, out leg))
						{ fault = KingdomCityFault.InvalidIndex; return false; }
						WriteConstructionInputRouteText(writer, leg.ZoneId);
						writer.Write(leg.EnterX); writer.Write(leg.EnterY);
						writer.Write(leg.ExitX); writer.Write(leg.ExitY);
						writer.Write(leg.PathLength); writer.Write(leg.DepartTick);
						writer.Write(leg.ArriveTick);
					}
					writer.Flush();
					payload = stream.ToArray();
				}
			}
			catch (EncoderFallbackException)
			{ fault = KingdomCityFault.InvalidIndex; return false; }
			KernelFaultCode kernelFault;
			byte[] hash;
			if (!KernelDigest.TryComputeSha256(payload, out hash, out kernelFault))
			{ fault = KingdomCityFaults.FromKernel(kernelFault); return false; }
			digest = KernelDigest.ToLowercaseHex(hash);
			fault = KingdomCityFault.None;
			return true;
		}

		private static void WriteConstructionInputRouteText(BinaryWriter writer, string value)
		{
			byte[] bytes = new UTF8Encoding(false, true).GetBytes(value ?? "");
			writer.Write(bytes.Length);
			writer.Write(bytes);
		}
	}
}
