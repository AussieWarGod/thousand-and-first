using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	internal static partial class KingdomLabCivicRules
	{
		internal const int CurrentVersion = 1;
		internal const int MaxTextChars = 512;
		internal const string TasteOrdinalSource = "notable-lodge:resident-row-arrived-tick";

		internal static KingdomLabCivicRequest RequestForTaste(int TasteIndex)
		{
			return TasteIndex == 5 || TasteIndex == 6
				? KingdomLabCivicRequest.ShrineUnconsecrated
				: KingdomLabCivicRequest.NeighbourRehoused;
		}

		internal static KingdomLabCivicReceipt PrepareSavant(string RealmId,
			string SettlementId, string ZoneId, string OwnerId, string SubjectId,
			int ResidentId, string Name, string SubjectCreed, string CityCreed,
			string LodgeReceiptId, long TasteOrdinal, int TasteIndex, string TasteTag,
			string TargetObjectId, string TargetHomeObjectId, int TargetResidentId, string TargetName,
			string SourcePlotId, string SourceHomeName, string TargetPlotId,
			string TargetHomeName, long Tick)
		{
			KingdomLabCivicReceipt receipt = new KingdomLabCivicReceipt
			{
				Version = CurrentVersion,
				Kind = KingdomLabCivicKind.SavantPrice,
				Phase = KingdomLabCivicPhase.Prepared,
				Request = RequestForTaste(TasteIndex),
				RealmId = RealmId, SettlementId = SettlementId, ZoneId = ZoneId,
				OwnerObjectId = OwnerId, SubjectObjectId = SubjectId,
				SubjectResidentId = ResidentId, SubjectName = Name,
				SubjectCreed = SubjectCreed, CityCreed = CityCreed,
				NotableLodgeReceiptId = LodgeReceiptId,
				TasteOrdinal = TasteOrdinal, TasteSource = TasteOrdinalSource,
				TasteIndex = TasteIndex, TasteTag = TasteTag,
				TargetObjectId = TargetObjectId, TargetHomeObjectId = TargetHomeObjectId,
				TargetResidentId = TargetResidentId,
				TargetName = TargetName, SourcePlotId = SourcePlotId,
				SourceHomeName = SourceHomeName, TargetPlotId = TargetPlotId,
				TargetHomeName = TargetHomeName, CreatedTick = Math.Max(0L, Tick),
				Fault = ""
			};
			Seal(receipt);
			return Valid(receipt, out _) ? receipt : null;
		}

		internal static KingdomLabCivicReceipt PrepareDeparture(string RealmId,
			string SettlementId, string ZoneId, string OwnerId, string ResidentObjectId,
			int ResidentId, string Name, string SourcePlotId, string RefusedTag, long Tick)
		{
			KingdomLabCivicReceipt receipt = new KingdomLabCivicReceipt
			{
				Version = CurrentVersion,
				Kind = KingdomLabCivicKind.RefusalDeparture,
				Phase = KingdomLabCivicPhase.Active,
				Request = KingdomLabCivicRequest.RoofRefusal,
				RealmId = RealmId, SettlementId = SettlementId, ZoneId = ZoneId,
				OwnerObjectId = OwnerId, SubjectObjectId = ResidentObjectId,
				SubjectResidentId = ResidentId, SubjectName = Name,
				SourcePlotId = SourcePlotId, RefusedTag = RefusedTag,
				CreatedTick = Math.Max(0L, Tick), Fault = ""
			};
			Seal(receipt);
			return Valid(receipt, out _) ? receipt : null;
		}

		private static void Seal(KingdomLabCivicReceipt Receipt)
		{
			Receipt.CauseDigest = Digest(Receipt);
			Receipt.EventId = string.IsNullOrEmpty(Receipt.CauseDigest) ? null
				: "taf:lab-civic:" + ((int)Receipt.Kind) + ":"
					+ Receipt.CauseDigest.Substring(0, 24);
		}

		internal static string Digest(KingdomLabCivicReceipt R)
		{
			if (R == null) return null;
			try
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream,
					new UTF8Encoding(false, true), true))
				{
					S(writer, "TAF-LAB-CIVIC-CAUSE-V1"); writer.Write((byte)R.Kind);
					writer.Write((byte)R.Request); S(writer, R.RealmId); S(writer, R.SettlementId);
					S(writer, R.ZoneId); S(writer, R.OwnerObjectId); S(writer, R.SubjectObjectId);
					writer.Write(R.SubjectResidentId); S(writer, R.SubjectName); S(writer, R.SubjectCreed);
					S(writer, R.CityCreed); S(writer, R.NotableLodgeReceiptId);
					writer.Write(R.TasteOrdinal); S(writer, R.TasteSource); writer.Write(R.TasteIndex);
					S(writer, R.TasteTag); S(writer, R.TargetObjectId); S(writer, R.TargetHomeObjectId);
					writer.Write(R.TargetResidentId);
					S(writer, R.TargetName); S(writer, R.SourcePlotId); S(writer, R.TargetPlotId);
					S(writer, R.SourceHomeName); S(writer, R.TargetHomeName);
					S(writer, R.RefusedTag); writer.Write(R.CreatedTick); writer.Flush();
					using (SHA256 sha = SHA256.Create()) return Hex(sha.ComputeHash(stream.ToArray()));
				}
			}
			catch { return null; }
		}

		private static void S(BinaryWriter Writer, string Value)
		{
			if (Value == null) { Writer.Write(-1); return; }
			byte[] bytes = new UTF8Encoding(false, true).GetBytes(Value);
			Writer.Write(bytes.Length); Writer.Write(bytes);
		}

		private static string Hex(byte[] Bytes)
		{
			StringBuilder result = new StringBuilder(Bytes.Length * 2);
			for (int i = 0; i < Bytes.Length; i++) result.Append(Bytes[i].ToString("x2"));
			return result.ToString();
		}
	}
}
