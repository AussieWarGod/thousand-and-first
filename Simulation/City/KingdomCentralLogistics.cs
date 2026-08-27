using System;
using System.Collections.Generic;

using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>Pull view exposed to the CarryBook adapter. The central registry owns route/body;
	/// the opaque owner owns exact manifest references and callback receipts.</summary>
	internal readonly struct KingdomManifestTripView
	{
		internal readonly int JobId;
		internal readonly int TripId;
		internal readonly KingdomDeliveryPhase Phase;
		internal readonly int SourceStart;
		internal readonly int SourceCount;
		internal readonly string CarrierObjectId;
		internal readonly string CarrierZoneId;
		internal readonly KingdomLifecycleTopology CarrierTopology;
		internal readonly int CarrierX;
		internal readonly int CarrierY;
		internal readonly bool CarrierAvailable;

		internal KingdomManifestTripView(int jobId, int tripId, KingdomDeliveryPhase phase,
			int sourceStart, int sourceCount, string carrierObjectId, string carrierZoneId,
			KingdomLifecycleTopology carrierTopology, int carrierX, int carrierY,
			bool carrierAvailable)
		{
			JobId = jobId;
			TripId = tripId;
			Phase = phase;
			SourceStart = sourceStart;
			SourceCount = sourceCount;
			CarrierObjectId = carrierObjectId;
			CarrierZoneId = carrierZoneId;
			CarrierTopology = carrierTopology;
			CarrierX = carrierX;
			CarrierY = carrierY;
			CarrierAvailable = carrierAvailable;
		}
	}

	internal readonly struct KingdomManifestReservation
	{
		internal readonly int[] JobIds;
		internal readonly int[] TripIds;
		internal readonly long ArrivalTick;

		internal KingdomManifestReservation(int[] jobIds, int[] tripIds, long arrivalTick)
		{
			JobIds = jobIds == null ? new int[0] : (int[])jobIds.Clone();
			TripIds = tripIds == null ? new int[0] : (int[])tripIds.Clone();
			ArrivalTick = arrivalTick;
		}
	}

	/// <summary>Production §3.10 coordinator. Planning is one bounded frozen snapshot; this edge
	/// brackets exact holder callbacks and persists every route before physical mutation.</summary>
	internal static partial class KingdomCentralLogistics
	{
		internal const string TargetReceiptProperty = "KingdomDeliveryReceipt";
		internal const string FoodReceiptJobProperty = "KingdomDeliveryReceiptJob";
	}
}
