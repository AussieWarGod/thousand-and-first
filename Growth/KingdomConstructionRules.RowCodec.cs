using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomConstructionRules
	{
		private static bool TryDecodeRow(string Line, bool Legacy, bool Older, bool Prior,
			out KingdomConstructionJob Row)
		{
			if (Legacy) return TryDecodeLegacyRow(Line, out Row);
			Row = null;
			string[] f = Line.Split('|');
			if (f.Length != (Older ? 45 : Prior ? 51 : 55)) return false;
			string owner, zone, subject, source, output, physicalItem, physicalDestination;
			string physicalReceipt, target, payload, requested, spent, outstanding, lost, failure;
			string eventId, chronicle, ledger, message, deed;
			int route, phase, projection, x, y, physicalPhase, physicalIndex, physicalAmount;
			int physicalSpilled, revision, waterRequested, waterSpent, waterOutstanding, waterLost;
			int mode, chronicleState, ledgerState, messageState, deedState;
			long created, started, due, updated;
			int ledgerBeforeCount = -1, ledgerAfterCount = -1;
			int buildTruthSchema = 0, buildDefence = 0;
			bool buildHasPlot = false, buildFrontier = false;
			string ledgerBeforeHash = null, ledgerAfterHash = null, proofHash = null;
			bool compacted = false;
			if (!TryDecodeText(f[1], MaxOwnerChars, out owner)
				|| !TryDecodeText(f[2], MaxZoneChars, out zone)
				|| !TryInt(f[3], 1, (int)(Older
					? KingdomConstructionRoute.Strike : KingdomConstructionRoute.PurposeConsignment), out route)
				|| !TryInt(f[4], 1, (int)KingdomConstructionPhase.InspectionRequired, out phase)
				|| !TryInt(f[5], 1, (int)(Older
					? KingdomConstructionProjection.Repair : KingdomConstructionProjection.PurposeConsignment), out projection)
				|| !TryInt(f[6], -1, 1023, out x) || !TryInt(f[7], -1, 1023, out y)
				|| !TryDecodeText(f[8], MaxSubjectChars, out subject)
				|| !TryDecodeText(f[9], MaxSubjectChars, out source)
				|| !TryDecodeText(f[10], MaxSubjectChars, out output)
				|| !TryInt(f[11], 0, (int)(Older
					? KingdomPhysicalPhase.RoadTallySettled : KingdomPhysicalPhase.CargoDelivered), out physicalPhase)
				|| !TryInt(f[12], 0, 4096, out physicalIndex)
				|| !TryInt(f[13], 0, int.MaxValue, out physicalAmount)
				|| !TryInt(f[14], 0, int.MaxValue, out physicalSpilled)
				|| !TryDecodeText(f[15], MaxSubjectChars, out physicalItem)
				|| !TryDecodeText(f[16], MaxSubjectChars, out physicalDestination)
				|| !TryDecodeText(f[17], MaxPhysicalReceiptChars, out physicalReceipt)
				|| !TryDecodeText(f[18], MaxTargetChars, out target)
				|| !TryDecodeText(f[19], MaxPayloadChars, out payload)
				|| !TryLong(f[20], out created) || !TryLong(f[21], out started)
				|| !TryLong(f[22], out due) || !TryLong(f[23], out updated)
				|| !TryInt(f[24], 1, int.MaxValue, out revision)
				|| !TryInt(f[25], 0, int.MaxValue, out waterRequested)
				|| !TryInt(f[26], 0, int.MaxValue, out waterSpent)
				|| !TryInt(f[27], 0, int.MaxValue, out waterOutstanding)
				|| !TryInt(f[28], 0, int.MaxValue, out waterLost)
				|| (f[29] != "0" && f[29] != "1")
				|| !TryDecodeText(f[30], 4096, out requested)
				|| !TryDecodeText(f[31], 4096, out spent)
				|| !TryDecodeText(f[32], 4096, out outstanding)
				|| !TryDecodeText(f[33], 4096, out lost)
				|| !TryDecodeText(f[34], MaxFailureChars, out failure)
				|| !TryDecodeText(f[35], 256, out eventId) || !TryInt(f[36], 0, 3, out mode)
				|| !TryDecodeText(f[37], MaxOutboxTextChars, out chronicle)
				|| !TryInt(f[38], 0, (int)KingdomConstructionSinkDisposition.Lost, out chronicleState)
				|| !TryDecodeText(f[39], MaxOutboxTextChars, out ledger)
				|| !TryInt(f[40], 0, (int)KingdomConstructionSinkDisposition.Lost, out ledgerState)
				|| !TryDecodeText(f[41], MaxOutboxTextChars, out message)
				|| !TryInt(f[42], 0, (int)KingdomConstructionSinkDisposition.Lost, out messageState)
				|| !TryDecodeText(f[43], MaxOutboxTextChars, out deed)
				|| !TryInt(f[44], 0, (int)KingdomConstructionSinkDisposition.Lost, out deedState)) return false;
			if (!Older && (!TryInt(f[45], -1, MaxLedgerNotes - 1, out ledgerBeforeCount)
				|| !TryDecodeText(f[46], 64, out ledgerBeforeHash)
				|| !TryInt(f[47], -1, MaxLedgerNotes, out ledgerAfterCount)
				|| !TryDecodeText(f[48], 64, out ledgerAfterHash)
				|| (f[49] != "0" && f[49] != "1")
				|| !TryDecodeText(f[50], 64, out proofHash))) return false;
			if (!Older) compacted = f[49] == "1";
			if (!Older && !Prior && (!TryInt(f[51], 0, BuildTruthSchema, out buildTruthSchema)
				|| (f[52] != "0" && f[52] != "1")
				|| (f[53] != "0" && f[53] != "1")
				|| !TryInt(f[54], 0, int.MaxValue, out buildDefence))) return false;
			if (!Older && !Prior)
			{
				buildHasPlot = f[52] == "1";
				buildFrontier = f[53] == "1";
			}
			KingdomConstructionOutbox box = null;
			if (!string.IsNullOrEmpty(eventId) || mode != 0 || chronicleState != 0
				|| ledgerState != 0 || messageState != 0 || deedState != 0)
			{
				box = new KingdomConstructionOutbox
				{
					EventId = eventId, Mode = mode, Chronicle = chronicle,
					ChronicleState = (KingdomConstructionSinkDisposition)chronicleState,
					Ledger = ledger, LedgerState = (KingdomConstructionSinkDisposition)ledgerState,
					LedgerBeforeCount = ledgerBeforeCount, LedgerBeforeHash = ledgerBeforeHash,
					LedgerAfterCount = ledgerAfterCount, LedgerAfterHash = ledgerAfterHash,
					Message = message, MessageState = (KingdomConstructionSinkDisposition)messageState,
					Deed = deed, DeedState = (KingdomConstructionSinkDisposition)deedState
				};
				// V2 could publish an uninspectable ledger attempt. Never invoke it again.
				if (Older && box.LedgerState == KingdomConstructionSinkDisposition.Attempting)
					box.LedgerState = KingdomConstructionSinkDisposition.Lost;
			}
			Row = new KingdomConstructionJob
			{
				Id = f[0], OwnerKey = owner, ZoneId = zone,
				Route = (KingdomConstructionRoute)route,
				Phase = (KingdomConstructionPhase)phase,
				Projection = (KingdomConstructionProjection)projection,
				X = x, Y = y, SubjectId = subject, SourceId = source, OutputId = output,
				PhysicalPhase = (KingdomPhysicalPhase)physicalPhase,
				PhysicalIndex = physicalIndex, PhysicalAmount = physicalAmount,
				PhysicalSpilled = physicalSpilled, PhysicalItemId = physicalItem,
				PhysicalDestinationId = physicalDestination, PhysicalReceipt = physicalReceipt,
				TargetKey = target, Payload = payload,
				BuildTruthSchema = buildTruthSchema, BuildHasPlot = buildHasPlot,
				BuildFrontier = buildFrontier, BuildDefence = buildDefence,
				CreatedTick = created,
				StartedTick = started, DueTick = due, UpdatedTick = updated, Revision = revision,
				Claims = new KingdomConstructionClaims
				{
					WaterRequested = waterRequested, WaterSpent = waterSpent,
					WaterOutstanding = waterOutstanding, WaterLost = waterLost,
					Exact = f[29] == "1", MaterialRequested = requested,
					MaterialSpent = spent, MaterialOutstanding = outstanding, MaterialLost = lost
				},
				Failure = failure, Outbox = box, Compacted = compacted, CompactHash = proofHash
			};
			return ValidJob(Row);
		}

		private static bool TryDecodeLegacyRow(string Line, out KingdomConstructionJob Row)
		{
			Row = null;
			string[] f = Line.Split('|');
			if (f.Length != 26)
			{
				return false;
			}
			string owner;
			string zone;
			string subject;
			string target;
			string payload;
			string requested;
			string spent;
			string outstanding;
			string lost;
			string failure;
			int route;
			int phase;
			int projection;
			int x;
			int y;
			long created;
			long started;
			long due;
			long updated;
			int revision;
			int waterRequested;
			int waterSpent;
			int waterOutstanding;
			int waterLost;
			if (!TryDecodeText(f[1], MaxOwnerChars, out owner) || !TryDecodeText(f[2], MaxZoneChars, out zone)
				|| !TryInt(f[3], 1, (int)KingdomConstructionRoute.WearRepair, out route)
				|| !TryInt(f[4], 1, (int)KingdomConstructionPhase.InspectionRequired, out phase)
				|| !TryInt(f[5], 1, (int)KingdomConstructionProjection.Repair, out projection)
				|| !TryInt(f[6], -1, 1023, out x) || !TryInt(f[7], -1, 1023, out y)
				|| !TryDecodeText(f[8], MaxSubjectChars, out subject) || !TryDecodeText(f[9], MaxTargetChars, out target)
				|| !TryDecodeText(f[10], MaxPayloadChars, out payload)
				|| !TryLong(f[11], out created) || !TryLong(f[12], out started) || !TryLong(f[13], out due) || !TryLong(f[14], out updated)
				|| !TryInt(f[15], 1, int.MaxValue, out revision)
				|| !TryInt(f[16], 0, int.MaxValue, out waterRequested) || !TryInt(f[17], 0, int.MaxValue, out waterSpent)
				|| !TryInt(f[18], 0, int.MaxValue, out waterOutstanding) || !TryInt(f[19], 0, int.MaxValue, out waterLost)
				|| (f[20] != "0" && f[20] != "1")
				|| !TryDecodeText(f[21], 4096, out requested) || !TryDecodeText(f[22], 4096, out spent)
				|| !TryDecodeText(f[23], 4096, out outstanding) || !TryDecodeText(f[24], 4096, out lost)
				|| !TryDecodeText(f[25], MaxFailureChars, out failure))
			{
				return false;
			}
			Row = new KingdomConstructionJob
			{
				Id = f[0],
				OwnerKey = owner,
				ZoneId = zone,
				Route = (KingdomConstructionRoute)route,
				Phase = (KingdomConstructionPhase)phase,
				Projection = (KingdomConstructionProjection)projection,
				X = x,
				Y = y,
					SubjectId = subject,
					SourceId = subject,
				TargetKey = target,
				Payload = payload,
				CreatedTick = created,
				StartedTick = started,
				DueTick = due,
				UpdatedTick = updated,
				Revision = revision,
				Claims = new KingdomConstructionClaims
				{
					WaterRequested = waterRequested,
					WaterSpent = waterSpent,
					WaterOutstanding = waterOutstanding,
					WaterLost = waterLost,
					Exact = f[20] == "1",
					MaterialRequested = requested,
					MaterialSpent = spent,
					MaterialOutstanding = outstanding,
					MaterialLost = lost
				},
				Failure = failure
			};
			return ValidJob(Row);
		}

	}
}
