using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomConstructionInputRules
	{
		internal static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

		public static bool TryIntentDigest(KingdomConstructionInputIntent Intent,
			out string Digest, out KingdomConstructionInputFault Fault)
		{
			Digest = null;
			if (!ValidIntent(Intent)) return Refuse(KingdomConstructionInputFault.CrossBinding, out Fault);
			try
			{
				byte[] payload;
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8))
				{
					writer.Write((byte)'T'); writer.Write((byte)'A'); writer.Write((byte)'F');
					writer.Write((byte)'C'); writer.Write((byte)'I'); writer.Write((byte)1);
					WriteText(writer, Intent.ConstructionJobId, MaxIdentityChars);
					WriteText(writer, Intent.OwnerKey, MaxIdentityChars);
					WriteText(writer, Intent.ZoneId, MaxIdentityChars);
					writer.Write(Intent.Route); writer.Write(Intent.Projection);
					writer.Write(Intent.X); writer.Write(Intent.Y);
					WriteOptionalText(writer, Intent.SubjectId, MaxIdentityChars);
					WriteOptionalText(writer, Intent.SourceId, MaxIdentityChars);
					WriteOptionalText(writer, Intent.TargetKey, MaxIdentityChars);
					WriteText(writer, Intent.PayloadDigest, 64);
					WriteText(writer, Intent.BuildTruthDigest, 64);
					writer.Write(Intent.WaterRequested);
					WriteText(writer, Intent.MaterialRequestedClaim, MaxClaimChars);
					writer.Write(Intent.CreatedTick); writer.Write(Intent.StartedTick);
					writer.Write(Intent.DueTick); writer.Flush(); payload = stream.ToArray();
				}
				Digest = HashBytes(payload);
				Fault = KingdomConstructionInputFault.None;
				return true;
			}
			catch { return Refuse(KingdomConstructionInputFault.Digest, out Fault); }
		}

		internal static bool TryPlanDigest(KingdomConstructionInputReceipt Receipt,
			out string Digest)
		{
			Digest = null;
			if (Receipt == null) return false;
			try
			{
				byte[] payload;
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8))
				{
					writer.Write((byte)'T'); writer.Write((byte)'A'); writer.Write((byte)'F');
					writer.Write((byte)'R'); writer.Write((byte)'P');
					writer.Write((byte)Receipt.Schema);
					WriteReceiptPlan(writer, Receipt);
					writer.Flush(); payload = stream.ToArray();
				}
				if (payload.Length > MaxPayloadBytes) return false;
				Digest = HashBytes(payload);
				return true;
			}
			catch { return false; }
		}

		private static void WriteReceiptPlan(BinaryWriter Writer,
			KingdomConstructionInputReceipt Receipt)
		{
			WriteText(Writer, Receipt.ReceiptId, MaxIdentityChars);
			WriteText(Writer, Receipt.ConstructionJobId, MaxIdentityChars);
			WriteText(Writer, Receipt.OwnerKey, MaxIdentityChars); Writer.Write(Receipt.OwnerEpoch);
			WriteText(Writer, Receipt.TargetZoneId, MaxIdentityChars);
			Writer.Write(Receipt.TargetX); Writer.Write(Receipt.TargetY);
			WriteText(Writer, Receipt.ConstructionIntentDigest, 64);
			if (Receipt.Schema == LegacySchema)
				WriteOptionalText(Writer, Receipt.RequiredObjectId, MaxIdentityChars);
			else
			{
				Writer.Write((byte)Receipt.RequiredObjectCount);
				for (int i = 0; i < Receipt.RequiredObjectCount; i++)
					WriteText(Writer, Receipt.RequiredObjectAt(i), MaxIdentityChars);
			}
			Writer.Write(Receipt.WaterRequested);
			WriteText(Writer, Receipt.MaterialRequestedClaim, MaxClaimChars);
			Writer.Write(Receipt.WaterReserveFloor);
			Writer.Write(Receipt.MaterialReservePolicyVersion);
			Writer.Write(Receipt.PriorWaterSpent); Writer.Write(Receipt.PriorWaterLost);
			WriteText(Writer, Receipt.PriorMaterialSpentClaim, MaxClaimChars);
			WriteText(Writer, Receipt.PriorMaterialLostClaim, MaxClaimChars);
			Writer.Write((byte)Receipt.SourceCount);
			for (int i = 0; i < Receipt.SourceCount; i++) WriteSourcePlan(Writer, Receipt.SourceAt(i));
			Writer.Write((byte)Receipt.CargoCount);
			for (int i = 0; i < Receipt.CargoCount; i++) WriteCargoPlan(Writer, Receipt.CargoAt(i));
			Writer.Write((byte)Receipt.ChildCount);
			for (int i = 0; i < Receipt.ChildCount; i++) WriteChildPlan(Writer, Receipt.ChildAt(i));
		}

		private static void WriteSourcePlan(BinaryWriter Writer,
			KingdomConstructionInputSourceLine Line)
		{
			Writer.Write(Line.Ordinal); WriteText(Writer, Line.LineId, MaxIdentityChars);
			Writer.Write((byte)Line.Kind); WriteText(Writer, Line.Classification, MaxClaimChars);
			WriteText(Writer, Line.SourceSettlementId, MaxIdentityChars);
			WriteText(Writer, Line.SourceZoneId, MaxIdentityChars);
			WriteText(Writer, Line.HolderId, MaxIdentityChars);
			WriteText(Writer, Line.SourceObjectId, MaxIdentityChars);
			Writer.Write((byte)Line.Topology); Writer.Write(Line.X); Writer.Write(Line.Y);
			WriteText(Writer, Line.Blueprint, MaxBlueprintChars);
			Writer.Write(Line.Before); Writer.Write(Line.Take); Writer.Write(Line.ResidualAfter);
			Writer.Write(Line.HolderStockBefore); Writer.Write(Line.PriorReserved);
			Writer.Write(Line.ReserveFloor); Writer.Write(Line.CargoOrdinal);
			Writer.Write(Line.RouteCost); Writer.Write(Line.DedicationOrdinal);
			WriteOptionalText(Writer, Line.RemainderMarker, MaxIdentityChars);
		}

		private static void WriteCargoPlan(BinaryWriter Writer,
			KingdomConstructionInputCargoLine Line)
		{
			Writer.Write(Line.Ordinal); WriteText(Writer, Line.CargoKey, MaxIdentityChars);
			WriteText(Writer, Line.CreationMarker, MaxIdentityChars);
			Writer.Write((byte)Line.Kind); WriteText(Writer, Line.Classification, MaxClaimChars);
			Writer.Write(Line.Amount); WriteText(Writer, Line.Blueprint, MaxBlueprintChars);
			Writer.Write(Line.Capacity); Writer.Write(Line.SourceLineOrdinal);
			WriteOptionalText(Writer, Line.ExpectedObjectId, MaxIdentityChars);
			Writer.Write(Line.ChildJobId); Writer.Write(Line.ChildTripId);
		}

		private static void WriteChildPlan(BinaryWriter Writer, KingdomConstructionInputChild Child)
		{
			Writer.Write(Child.Ordinal); Writer.Write(Child.JobId); Writer.Write(Child.TripId);
			Writer.Write(Child.CargoStart); Writer.Write(Child.CargoCount);
			Writer.Write((byte)Child.CargoShape); Writer.Write(Child.SourceEndpointId);
			WriteOptionalText(Writer, Child.SourceObjectId, MaxIdentityChars);
			WriteText(Writer, Child.SourceZoneId, MaxIdentityChars);
			Writer.Write(Child.SourceX); Writer.Write(Child.SourceY);
			Writer.Write(Child.TargetEndpointId);
			WriteOptionalText(Writer, Child.TargetObjectId, MaxIdentityChars);
			WriteText(Writer, Child.TargetZoneId, MaxIdentityChars);
			Writer.Write(Child.TargetX); Writer.Write(Child.TargetY);
			Writer.Write(Child.ArrivalTick); WriteText(Writer, Child.RouteDigest, 64);
		}

		private static bool ValidIntent(KingdomConstructionInputIntent Intent)
		{
			KingdomMaterialDebitCost ignored;
			return Intent != null
				&& ValidText(Intent.ConstructionJobId, MaxIdentityChars, false)
				&& ValidText(Intent.OwnerKey, MaxIdentityChars, false)
				&& ValidText(Intent.ZoneId, MaxIdentityChars, false)
				&& Intent.Route > 0 && Intent.Projection > 0
				&& Intent.X >= 0 && Intent.X <= MaxCoordinate
				&& Intent.Y >= 0 && Intent.Y <= MaxCoordinate
				&& ValidText(Intent.SubjectId, MaxIdentityChars, true)
				&& ValidText(Intent.SourceId, MaxIdentityChars, true)
				&& ValidText(Intent.TargetKey, MaxIdentityChars, true)
				&& ValidDigest(Intent.PayloadDigest) && ValidDigest(Intent.BuildTruthDigest)
				&& Intent.WaterRequested >= 0
				&& TryParseMaterialClaim(Intent.MaterialRequestedClaim, out ignored)
				&& Intent.CreatedTick >= 0L && Intent.StartedTick >= 0L && Intent.DueTick >= 0L;
		}

		internal static void WriteText(BinaryWriter Writer, string Value, int MaximumChars)
		{
			if (!ValidText(Value, MaximumChars, false)) throw new InvalidDataException("text");
			WriteUtf8(Writer, Value);
		}

		internal static void WriteOptionalText(BinaryWriter Writer, string Value, int MaximumChars)
		{
			if (!ValidText(Value, MaximumChars, true)) throw new InvalidDataException("text");
			WriteUtf8(Writer, Value ?? string.Empty);
		}

		private static void WriteUtf8(BinaryWriter Writer, string Value)
		{
			byte[] bytes = StrictUtf8.GetBytes(Value);
			if (bytes.Length > ushort.MaxValue) throw new InvalidDataException("text bytes");
			Writer.Write((ushort)bytes.Length); Writer.Write(bytes);
		}

		internal static string ReadText(BinaryReader Reader, int MaximumChars, bool Optional)
		{
			int length = Reader.ReadUInt16();
			if (length > MaximumChars * 4) throw new InvalidDataException("text bytes");
			byte[] bytes = Reader.ReadBytes(length);
			if (bytes.Length != length) throw new EndOfStreamException();
			string value = StrictUtf8.GetString(bytes);
			if (!ValidText(value, MaximumChars, Optional)) throw new InvalidDataException("text");
			return value;
		}

		internal static string HashBytes(byte[] Payload)
		{
			byte[] digest;
			using (SHA256 sha = SHA256.Create()) digest = sha.ComputeHash(Payload);
			StringBuilder result = new StringBuilder(64);
			for (int i = 0; i < digest.Length; i++)
				result.Append(digest[i].ToString("x2", CultureInfo.InvariantCulture));
			return result.ToString();
		}

		internal static bool ValidDigest(string Value)
		{
			if (Value == null || Value.Length != 64) return false;
			for (int i = 0; i < Value.Length; i++)
				if (!((Value[i] >= '0' && Value[i] <= '9')
					|| (Value[i] >= 'a' && Value[i] <= 'f'))) return false;
			return true;
		}

		internal static bool FixedEquals(string A, string B)
		{
			if (A == null || B == null || A.Length != B.Length) return false;
			int difference = 0;
			for (int i = 0; i < A.Length; i++) difference |= A[i] ^ B[i];
			return difference == 0;
		}
	}
}
