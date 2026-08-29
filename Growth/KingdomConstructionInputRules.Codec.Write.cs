using System.IO;

namespace ThousandAndFirst
{
	public static partial class KingdomConstructionInputRules
	{
		private static byte[] WritePayload(KingdomConstructionInputReceipt receipt)
		{
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8))
			{
				writer.Write((byte)'T'); writer.Write((byte)'A'); writer.Write((byte)'F');
				writer.Write((byte)'C'); writer.Write((byte)'R'); writer.Write((byte)1);
				writer.Write(receipt.Schema);
				WriteText(writer, receipt.ReceiptId, MaxIdentityChars);
				WriteText(writer, receipt.ConstructionJobId, MaxIdentityChars);
				WriteText(writer, receipt.OwnerKey, MaxIdentityChars); writer.Write(receipt.OwnerEpoch);
				WriteText(writer, receipt.TargetZoneId, MaxIdentityChars);
				writer.Write(receipt.TargetX); writer.Write(receipt.TargetY);
				WriteText(writer, receipt.ConstructionIntentDigest, 64);
				if (receipt.Schema == LegacySchema)
					WriteOptionalText(writer, receipt.RequiredObjectId, MaxIdentityChars);
				else WriteRequiredObjectIds(writer, receipt);
				writer.Write(receipt.WaterRequested);
				WriteText(writer, receipt.MaterialRequestedClaim, MaxClaimChars);
				writer.Write(receipt.WaterReserveFloor);
				writer.Write(receipt.MaterialReservePolicyVersion);
				writer.Write(receipt.PriorWaterSpent); writer.Write(receipt.PriorWaterLost);
				WriteText(writer, receipt.PriorMaterialSpentClaim, MaxClaimChars);
				WriteText(writer, receipt.PriorMaterialLostClaim, MaxClaimChars);
				writer.Write((byte)receipt.TxPhase); writer.Write(receipt.Revision);
				WriteText(writer, receipt.PlanDigest, 64);
				writer.Write(receipt.PauseStartedTick); writer.Write(receipt.PausedTicks);
				writer.Write((byte)receipt.SourceCount);
				for (int i = 0; i < receipt.SourceCount; i++) WriteSource(writer, receipt.SourceAt(i));
				writer.Write((byte)receipt.CargoCount);
				for (int i = 0; i < receipt.CargoCount; i++) WriteCargo(writer, receipt.CargoAt(i));
				writer.Write((byte)receipt.ChildCount);
				for (int i = 0; i < receipt.ChildCount; i++) WriteChild(writer, receipt.ChildAt(i));
				writer.Flush(); return stream.ToArray();
			}
		}

		private static void WriteRequiredObjectIds(BinaryWriter writer,
			KingdomConstructionInputReceipt receipt)
		{
			writer.Write((byte)receipt.RequiredObjectCount);
			for (int i = 0; i < receipt.RequiredObjectCount; i++)
				WriteText(writer, receipt.RequiredObjectAt(i), MaxIdentityChars);
		}

		private static void WriteSource(BinaryWriter w, KingdomConstructionInputSourceLine x)
		{
			w.Write(x.Ordinal); WriteText(w, x.LineId, MaxIdentityChars); w.Write((byte)x.Kind);
			WriteText(w, x.Classification, MaxClaimChars);
			WriteText(w, x.SourceSettlementId, MaxIdentityChars);
			WriteText(w, x.SourceZoneId, MaxIdentityChars); WriteText(w, x.HolderId, MaxIdentityChars);
			WriteText(w, x.SourceObjectId, MaxIdentityChars); w.Write((byte)x.Topology);
			w.Write(x.X); w.Write(x.Y); WriteText(w, x.Blueprint, MaxBlueprintChars);
			w.Write(x.Before); w.Write(x.Take); w.Write(x.ResidualAfter);
			w.Write(x.HolderStockBefore); w.Write(x.PriorReserved); w.Write(x.ReserveFloor);
			w.Write(x.CargoOrdinal); w.Write(x.RouteCost); w.Write(x.DedicationOrdinal);
			WriteOptionalText(w, x.RemainderMarker, MaxIdentityChars);
			w.Write((byte)x.Phase); WriteOptionalText(w, x.RemainderObjectId, MaxIdentityChars);
			WriteOptionalText(w, x.BeforeWitnessHash, 64); WriteOptionalText(w, x.AfterWitnessHash, 64);
			w.Write(x.ProvedLost);
		}

		private static void WriteCargo(BinaryWriter w, KingdomConstructionInputCargoLine x)
		{
			w.Write(x.Ordinal); WriteText(w, x.CargoKey, MaxIdentityChars);
			WriteText(w, x.CreationMarker, MaxIdentityChars); w.Write((byte)x.Kind);
			WriteText(w, x.Classification, MaxClaimChars); w.Write(x.Amount);
			WriteText(w, x.Blueprint, MaxBlueprintChars); w.Write(x.Capacity);
			w.Write(x.SourceLineOrdinal); WriteOptionalText(w, x.ExpectedObjectId, MaxIdentityChars);
			w.Write(x.ChildJobId); w.Write(x.ChildTripId); WriteOptionalText(w, x.ObjectId, MaxIdentityChars);
			w.Write((byte)x.Phase); w.Write((byte)x.CustodyTopology);
			WriteOptionalText(w, x.CustodyOwnerId, MaxIdentityChars);
			WriteOptionalText(w, x.CustodyZoneId, MaxIdentityChars); w.Write(x.CustodyX); w.Write(x.CustodyY);
			WriteOptionalText(w, x.BeforeWitnessHash, 64); WriteOptionalText(w, x.AfterWitnessHash, 64);
			w.Write(x.Spent); w.Write(x.Lost);
		}

		private static void WriteChild(BinaryWriter w, KingdomConstructionInputChild x)
		{
			w.Write(x.Ordinal); w.Write(x.JobId); w.Write(x.TripId); w.Write(x.CargoStart);
			w.Write(x.CargoCount); w.Write((byte)x.CargoShape); w.Write(x.SourceEndpointId);
			WriteOptionalText(w, x.SourceObjectId, MaxIdentityChars);
			WriteText(w, x.SourceZoneId, MaxIdentityChars); w.Write(x.SourceX); w.Write(x.SourceY);
			w.Write(x.TargetEndpointId); WriteOptionalText(w, x.TargetObjectId, MaxIdentityChars);
			WriteText(w, x.TargetZoneId, MaxIdentityChars); w.Write(x.TargetX); w.Write(x.TargetY);
			w.Write(x.ArrivalTick); WriteText(w, x.RouteDigest, 64);
			w.Write(x.CentralPhase); w.Write(x.CentralRevision);
		}
	}
}
