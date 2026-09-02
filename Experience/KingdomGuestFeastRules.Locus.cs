using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;

namespace ThousandAndFirst
{
	public static partial class KingdomGuestFeastRules
	{
		public static string TerminalDigest(KingdomGuestFeastReceipt row)
		{
			if (row == null || row.GuestResult != KingdomGrowthArrivalDisposition.Joined
				|| row.GrowthTerminalReceiptId == null || row.GuestTerminalTick < 0L) return null;
			try
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, new UTF8Encoding(false, true)))
				{
					writer.Write("TAF-FIRST-GUEST-TERMINAL-V1");
					writer.Write(row.SettlementId); writer.Write(row.GrowthTerminalReceiptId);
					writer.Write(row.GuestCandidateId); writer.Write(row.GuestObjectId);
					writer.Write(row.GuestArrivalOperationId);
					writer.Write(row.GuestArrivalOutboxEventId); writer.Write(row.GuestName);
					writer.Write(row.GuestOrigin); writer.Write(row.GuestCreed);
					writer.Write(row.GuestResidentId); writer.Write((byte)row.GuestResult);
					writer.Write(row.GuestTerminalTick); writer.Flush();
					using (SHA256 sha = SHA256.Create())
					{
						byte[] hash = sha.ComputeHash(stream.ToArray());
						StringBuilder text = new StringBuilder(64);
						for (int i = 0; i < hash.Length; i++) text.Append(hash[i].ToString("x2"));
						return text.ToString();
					}
				}
			}
			catch { return null; }
		}

		public static bool TryBuildLocusReceipt(string realmId, string settlementId,
			int workId, string objectId, string zoneId, string blueprint, long observedTick,
			out KingdomGuestFeastLocusReceipt receipt)
		{
			receipt = null;
			if (!KingdomIdentityRules.IsRealmId(realmId)
				|| !KingdomIdentityRules.IsSettlementId(settlementId) || workId <= 0
				|| !Text(objectId) || !Text(zoneId) || !Text(blueprint) || observedTick < 0L)
				return false;
			string digest;
			try
			{
				using (SHA256 sha = SHA256.Create())
				{
					byte[] bytes = Encoding.UTF8.GetBytes("TAF-GUEST-LOCUS-V1\0" + realmId
						+ "\0" + settlementId + "\0" + workId.ToString(CultureInfo.InvariantCulture)
						+ "\0" + objectId + "\0" + zoneId + "\0" + blueprint + "\0"
						+ observedTick.ToString(CultureInfo.InvariantCulture));
					byte[] hash = sha.ComputeHash(bytes); StringBuilder value = new StringBuilder(64);
					for (int i = 0; i < hash.Length; i++) value.Append(hash[i].ToString("x2"));
					digest = value.ToString();
				}
			}
			catch { return false; }
			receipt = new KingdomGuestFeastLocusReceipt
			{
				ProjectionId = "taf:guest-feast-locus:" + digest,
				RealmId = realmId, SettlementId = settlementId, WorkId = workId,
				ObjectId = objectId, ZoneId = zoneId, Blueprint = blueprint,
				ObservedTick = observedTick
			};
			return ValidLocus(receipt);
		}

		internal static bool ValidLocus(KingdomGuestFeastLocusReceipt receipt)
		{
			if (receipt == null || !KingdomIdentityRules.IsRealmId(receipt.RealmId)
				|| !KingdomIdentityRules.IsSettlementId(receipt.SettlementId)
				|| receipt.WorkId <= 0 || !Text(receipt.ObjectId) || !Text(receipt.ZoneId)
				|| !Text(receipt.Blueprint) || receipt.ObservedTick < 0L) return false;
			return TryBuildLocusReceiptCore(receipt, out string expected)
				&& receipt.ProjectionId == expected;
		}

		private static bool TryBuildLocusReceiptCore(KingdomGuestFeastLocusReceipt r,
			out string id)
		{
			id = null;
			// Avoid recursion through ValidLocus: reproduce the public builder with a sentinel id.
			if (!KingdomIdentityRules.IsRealmId(r.RealmId) || r.WorkId <= 0) return false;
			try
			{
				using (SHA256 sha = SHA256.Create())
				{
					byte[] bytes = Encoding.UTF8.GetBytes("TAF-GUEST-LOCUS-V1\0" + r.RealmId
						+ "\0" + r.SettlementId + "\0" + r.WorkId.ToString(CultureInfo.InvariantCulture)
						+ "\0" + r.ObjectId + "\0" + r.ZoneId + "\0" + r.Blueprint + "\0"
						+ r.ObservedTick.ToString(CultureInfo.InvariantCulture));
					byte[] hash = sha.ComputeHash(bytes); StringBuilder value = new StringBuilder(64);
					for (int i = 0; i < hash.Length; i++) value.Append(hash[i].ToString("x2"));
					id = "taf:guest-feast-locus:" + value; return true;
				}
			}
			catch { return false; }
		}

		internal static bool ExactLocus(KingdomGuestFeastReceipt row,
			KingdomGuestFeastLocusReceipt locus)
		{
			return row != null && ValidLocus(locus)
				&& row.LocusProjectionId == locus.ProjectionId
				&& row.LocusRealmId == locus.RealmId
				&& row.LocusSettlementId == locus.SettlementId
				&& row.LocusWorkId == locus.WorkId && row.LocusObjectId == locus.ObjectId
				&& row.LocusZoneId == locus.ZoneId && row.LocusBlueprint == locus.Blueprint
				&& row.LocusObservedTick == locus.ObservedTick;
		}

		private static void SetLocus(KingdomGuestFeastReceipt row,
			KingdomGuestFeastLocusReceipt locus)
		{
			row.LocusProjectionId = locus.ProjectionId; row.LocusRealmId = locus.RealmId;
			row.LocusSettlementId = locus.SettlementId; row.LocusWorkId = locus.WorkId;
			row.LocusObjectId = locus.ObjectId; row.LocusZoneId = locus.ZoneId;
			row.LocusBlueprint = locus.Blueprint; row.LocusObservedTick = locus.ObservedTick;
		}

		private static void ClearLocus(KingdomGuestFeastReceipt row)
		{
			row.LocusProjectionId = null; row.LocusRealmId = null;
			row.LocusSettlementId = null; row.LocusWorkId = 0; row.LocusObjectId = null;
			row.LocusZoneId = null; row.LocusBlueprint = null; row.LocusObservedTick = -1L;
		}

		private static bool LocusShape(KingdomGuestFeastReceipt r)
		{
			return ValidLocus(new KingdomGuestFeastLocusReceipt
			{
				ProjectionId = r.LocusProjectionId, RealmId = r.LocusRealmId,
				SettlementId = r.LocusSettlementId, WorkId = r.LocusWorkId,
				ObjectId = r.LocusObjectId, ZoneId = r.LocusZoneId,
				Blueprint = r.LocusBlueprint, ObservedTick = r.LocusObservedTick
			}) && r.LocusSettlementId == r.SettlementId
				&& r.LocusObservedTick > r.PracticeDecisionTick;
		}

		private static bool NoLocus(KingdomGuestFeastReceipt r) => r.LocusProjectionId == null
			&& r.LocusRealmId == null && r.LocusSettlementId == null && r.LocusWorkId == 0
			&& r.LocusObjectId == null && r.LocusZoneId == null && r.LocusBlueprint == null
			&& r.LocusObservedTick == -1L;
	}
}
