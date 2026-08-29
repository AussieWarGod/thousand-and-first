using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;

namespace ThousandAndFirst
{
	public static partial class KingdomGuestFeastRules
	{
		internal static bool ValidOpportunity(string settlementId,
			KingdomGrowthFirstGuestOpportunity x)
		{
			if (!KingdomIdentityRules.IsSettlementId(settlementId) || x == null
				|| x.RulesVersion != 1 && x.RulesVersion != 2 || x.CohortSize != 1
				|| x.FactsState != KingdomGrowthFirstGuestFactsState.Exact
				|| x.CauseTick < 0L || x.OfferedTick < x.CauseTick || x.CadenceTicks <= 0L
				|| x.OpportunityId != KingdomGrowthFirstGuestIdentityRules.OpportunityId(settlementId, 1L)
				|| x.CauseId != KingdomGrowthFirstGuestIdentityRules.CauseId(
					settlementId, 1L, x.CauseTick, x.CadenceTicks)) return false;
			bool decided = x.ChoiceState == KingdomGrowthFirstGuestChoiceState.Admitted
				|| x.ChoiceState == KingdomGrowthFirstGuestChoiceState.Declined;
			if (!(decided ? x.DecisionTick >= x.OfferedTick
				&& GeneratedId(x.DecisionReceiptId,
					"taf:growth-first-guest-receipt:", MaxGuestDecisionIdBytes)
				: (x.ChoiceState == KingdomGrowthFirstGuestChoiceState.AwaitingChoice
					|| x.ChoiceState == KingdomGrowthFirstGuestChoiceState.Deferred)
					&& x.DecisionTick == -1L && x.DecisionReceiptId == null)) return false;
			return x.RulesVersion == 1 ? LegacyGuestAuthority(x)
				: PhysicalGuestAuthority(x);
		}

		private static bool PhysicalGuestAuthority(KingdomGrowthFirstGuestOpportunity x)
		{
			if (!Enum.IsDefined(typeof(KingdomGrowthFirstGuestGuestPhase), x.GuestPhase)
				|| !Enum.IsDefined(typeof(KingdomGrowthFirstGuestTerminalState),
					x.GuestTerminalState)) return false;
			if (x.ChoiceState != KingdomGrowthFirstGuestChoiceState.Admitted)
				return NoPhysicalGuestAuthority(x);
			if (!ValidBodyAuthority(x)) return false;
			bool action = x.GuestActionTick >= x.DecisionTick
				&& GeneratedId(x.GuestActionReceiptId,
					"taf:growth-first-guest-receipt:", MaxGuestDecisionIdBytes);
			bool noAction = x.GuestActionTick == -1L && x.GuestActionReceiptId == null;
			bool terminal = x.GuestTerminalTick >= x.DecisionTick
				&& GeneratedId(x.GuestTerminalReceiptId,
					"taf:growth-first-guest-receipt:", MaxGuestDecisionIdBytes)
				&& x.GuestTerminalState != KingdomGrowthFirstGuestTerminalState.None;
			bool noTerminal = x.GuestTerminalTick == -1L && x.GuestTerminalReceiptId == null
				&& x.GuestTerminalState == KingdomGrowthFirstGuestTerminalState.None;
			if (x.GuestPhase == KingdomGrowthFirstGuestGuestPhase.Preparing
				|| x.GuestPhase == KingdomGrowthFirstGuestGuestPhase.Hosted)
				return noAction && noTerminal && x.BodyLeaseState ==
					KingdomGrowthFirstGuestBodyLeaseState.Reserved;
			if (x.GuestPhase == KingdomGrowthFirstGuestGuestPhase.CitizenshipIntent
				|| x.GuestPhase == KingdomGrowthFirstGuestGuestPhase.CitizenshipPrepared
				|| x.GuestPhase == KingdomGrowthFirstGuestGuestPhase.DepartureIntent)
				return action && noTerminal && x.BodyLeaseState ==
					KingdomGrowthFirstGuestBodyLeaseState.Reserved;
			return x.GuestPhase == KingdomGrowthFirstGuestGuestPhase.Terminal && terminal
				&& x.BodyLeaseState == KingdomGrowthFirstGuestBodyLeaseState.Released
				&& (action || x.GuestTerminalState == KingdomGrowthFirstGuestTerminalState.Died
					&& noAction);
		}
		private static bool NoPhysicalGuestAuthority(KingdomGrowthFirstGuestOpportunity x) =>
			NoBodyAuthority(x) && NoGuestStateAuthority(x);
		private static bool LegacyGuestAuthority(KingdomGrowthFirstGuestOpportunity x)
		{
			return NoGuestStateAuthority(x) && (NoBodyAuthority(x)
				|| x.ChoiceState == KingdomGrowthFirstGuestChoiceState.Admitted
					&& ValidBodyAuthority(x));
		}
		private static bool ValidBodyAuthority(KingdomGrowthFirstGuestOpportunity x)
		{
			return GeneratedId(x.BodyReservationId, "taf:experience-body:first-guest:v1:",
				MaxBodyReservationIdBytes) && KingdomIdentityRules.IsRealmId(x.BodyRealmId)
				&& x.BodyOptionKind == KingdomExperienceOptionKind.CivicStory
				&& x.BodyEnableEpoch > 0L && x.BodyReservedTick >= x.CauseTick
				&& x.BodyReservedTick <= x.DecisionTick
				&& (x.BodyLeaseState == KingdomGrowthFirstGuestBodyLeaseState.Reserved
					|| x.BodyLeaseState == KingdomGrowthFirstGuestBodyLeaseState.Released);
		}
		private static bool NoBodyAuthority(KingdomGrowthFirstGuestOpportunity x) =>
			x.BodyReservationId == null && x.BodyRealmId == null
			&& x.BodyOptionKind == KingdomExperienceOptionKind.None && x.BodyEnableEpoch == 0L
			&& x.BodyReservedTick == -1L
			&& x.BodyLeaseState == KingdomGrowthFirstGuestBodyLeaseState.None;
		private static bool NoGuestStateAuthority(KingdomGrowthFirstGuestOpportunity x)
		{
			return x.GuestPhase == KingdomGrowthFirstGuestGuestPhase.None
				&& x.GuestTerminalState == KingdomGrowthFirstGuestTerminalState.None
				&& x.GuestActionTick == -1L && x.GuestActionReceiptId == null
				&& x.GuestTerminalTick == -1L && x.GuestTerminalReceiptId == null;
		}

		internal static KingdomGuestFeastReceipt Copy(KingdomGuestFeastReceipt row)
		{
			return new KingdomGuestFeastReceipt
			{
				Version = row.Version, Phase = row.Phase, SettlementId = row.SettlementId,
				OpportunityId = row.OpportunityId, CauseId = row.CauseId,
				GuestDecisionReceiptId = row.GuestDecisionReceiptId,
				GrowthTerminalReceiptId = row.GrowthTerminalReceiptId,
				GuestCandidateId = row.GuestCandidateId,
				GuestObjectId = row.GuestObjectId,
				GuestArrivalOperationId = row.GuestArrivalOperationId,
				GuestArrivalOutboxEventId = row.GuestArrivalOutboxEventId,
				GuestName = row.GuestName, GuestOrigin = row.GuestOrigin,
				GuestCreed = row.GuestCreed, DeedId = row.DeedId,
				PracticeId = row.PracticeId, PointerSourceId = row.PointerSourceId,
				PointerTargetId = row.PointerTargetId, CauseTick = row.CauseTick,
				GuestDecisionTick = row.GuestDecisionTick,
				GuestTerminalTick = row.GuestTerminalTick,
				PracticeDecisionTick = row.PracticeDecisionTick,
				PointerTick = row.PointerTick, HomeCycles = row.HomeCycles,
				GuestResidentId = row.GuestResidentId, GuestResult = row.GuestResult,
				PracticeOutcome = row.PracticeOutcome,
				LocusProjectionId = row.LocusProjectionId,
				LocusRealmId = row.LocusRealmId,
				LocusSettlementId = row.LocusSettlementId,
				LocusWorkId = row.LocusWorkId, LocusObjectId = row.LocusObjectId,
				LocusZoneId = row.LocusZoneId, LocusBlueprint = row.LocusBlueprint,
				LocusObservedTick = row.LocusObservedTick, AwayArmed = row.AwayArmed,
				PointerKind = row.PointerKind
			};
		}

		internal static KingdomGuestFeastBook Clone(KingdomGuestFeastBook book)
		{
			KingdomGuestFeastBook copy = new KingdomGuestFeastBook
			{
				SchemaState = book.SchemaState, SchemaFault = book.SchemaFault,
				RealmId = book.RealmId, IdentityBound = book.IdentityBound,
				Revision = book.Revision, OpaqueWireVersion = book.OpaqueWireVersion,
				OpaqueFuturePayload = book.OpaqueFuturePayload == null ? null
					: (byte[])book.OpaqueFuturePayload.Clone(),
				OpaqueEnvelope = book.OpaqueEnvelope == null ? null
					: (byte[])book.OpaqueEnvelope.Clone()
			};
			for (int i = 0; i < book.Rows.Count; i++) copy.Rows.Add(Copy(book.Rows[i]));
			return copy;
		}

		internal static void Replace(KingdomGuestFeastBook target, KingdomGuestFeastBook source)
		{
			target.SchemaState = source.SchemaState; target.SchemaFault = source.SchemaFault;
			target.RealmId = source.RealmId; target.IdentityBound = source.IdentityBound;
			target.Revision = source.Revision; target.Rows.Clear();
			for (int i = 0; i < source.Rows.Count; i++) target.Rows.Add(Copy(source.Rows[i]));
			target.OpaqueWireVersion = source.OpaqueWireVersion;
			target.OpaqueFuturePayload = source.OpaqueFuturePayload;
			target.OpaqueEnvelope = source.OpaqueEnvelope;
		}

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
