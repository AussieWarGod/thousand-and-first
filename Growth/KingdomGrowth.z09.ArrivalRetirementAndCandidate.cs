using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.Rules;
using XRL.UI;
using XRL.World;
using XRL.World.AI;
using XRL.World.Conversations;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomGrowth
	{

		private static bool RetireArrivalCandidate(KingdomSystem system, Zone zone,
			KingdomGrowthBook growth, KingdomGrowthArrivalCandidate candidate)
		{
			if (candidate == null || candidate.Phase != KingdomGrowthArrivalCandidatePhase.Settled
				|| growth.ArrivalOp != null) return false;
			GameObject settler;
			TryArrivalObject(candidate, zone, out settler);
			bool declined = candidate.Disposition == KingdomGrowthArrivalDisposition.Declined;
			bool physical = declined ? ExactDeclinedFirstGuest(candidate, settler, zone)
				: candidate.Disposition == KingdomGrowthArrivalDisposition.Departed
					? ExactDepartedFirstGuest(candidate, settler, zone)
				: candidate.Disposition == KingdomGrowthArrivalDisposition.Joined
				? ExactPlacedCandidate(candidate, settler, zone)
				: ExactRefusedCandidate(candidate, settler, zone);
			if (!physical)
			{
				CandidateFault(growth, candidate,
					"candidate retirement could not prove its settled disposition");
				return false;
			}
			if (The.Game.ObjectGameState.ContainsKey(candidate.EscrowKey))
			{
				object rooted = The.Game.GetObjectGameState(candidate.EscrowKey);
				if (settler != null && !ReferenceEquals(rooted, settler)) return false;
				The.Game.ObjectGameState.Remove(candidate.EscrowKey);
				if (The.Game.ObjectGameState.ContainsKey(candidate.EscrowKey)) return false;
			}
			if (!KingdomLifecycleRules.RetireGrowthArrivalCandidate(growth, candidate))
				return false;
			system.NextArrivalTick = growth.NextArrivalTick;
			return true;
		}

		private static bool ExactDepartedFirstGuest(KingdomGrowthArrivalCandidate candidate,
			GameObject body, Zone zone)
		{
			KingdomGrowthFirstGuestTerminalState terminal = candidate?.FirstGuest == null
				? KingdomGrowthFirstGuestTerminalState.None
				: candidate.FirstGuest.GuestTerminalState;
			return candidate?.Disposition == KingdomGrowthArrivalDisposition.Departed
				&& (terminal == KingdomGrowthFirstGuestTerminalState.Departed
					|| terminal == KingdomGrowthFirstGuestTerminalState.Died)
				&& (body == null || !GameObject.Validate(body))
				&& zone?.FindObjectByID(candidate.ObjectId) == null
				&& !The.Game.ObjectGameState.ContainsKey(candidate.EscrowKey)
				&& CountArrivalMarker(zone, candidate.Marker) == 0;
		}

		private static bool ExactDeclinedFirstGuest(KingdomGrowthArrivalCandidate candidate,
			GameObject settler, Zone zone)
		{
			return candidate?.FirstGuest?.ChoiceState ==
				KingdomGrowthFirstGuestChoiceState.Declined
				&& (settler == null || !GameObject.Validate(settler))
				&& candidate.ObjectId == null
				&& !The.Game.ObjectGameState.ContainsKey(candidate.EscrowKey)
				&& CountArrivalMarker(zone, candidate.Marker) == 0;
		}

		private static bool AppendArrivalOutbox(KingdomSystem system,
			KingdomGrowthOperation operation, string kind, string chronicleClause,
			string ledger)
		{
			if (system?.ChronicleEntries == null || system.OutsiderEntries == null
				|| system.Ledger?.Notes == null) return false;
			string chronicle = null;
			int chronicleBeforeCount = 0;
			int chronicleAfterCount = 0;
			string chronicleBeforeHash = null;
			string chronicleAfterHash = null;
			int outsiderBeforeCount = 0;
			int outsiderAfterCount = 0;
			string outsiderBeforeHash = null;
			string outsiderAfterHash = null;
			string chronicleOfficial = null;
			string chronicleOutsider = null;
			if (chronicleClause != null)
			{
				string receiptId = KingdomLifecycleRules.GrowthChronicleOutboxReceiptId(
					operation, operation.OutboxEvents.Count);
				KingdomChronicleDeclaration declaration;
				if (receiptId == null || !KingdomChronicle.TryDeclareOnce(system, receiptId,
					chronicleClause, false, null, out declaration)) return false;
				chronicle = chronicleClause;
				chronicleBeforeCount = system.ChronicleEntries.Count;
				chronicleAfterCount = Math.Min(KingdomChronicle.MaxEntries,
					chronicleBeforeCount + 1);
				chronicleBeforeHash = declaration.OfficialBefore;
				chronicleAfterHash = declaration.OfficialAfter;
				outsiderBeforeCount = system.OutsiderEntries.Count;
				outsiderAfterCount = Math.Min(KingdomChronicle.MaxEntries,
					outsiderBeforeCount + 1);
				outsiderBeforeHash = declaration.OutsiderBefore;
				outsiderAfterHash = declaration.OutsiderAfter;
				chronicleOfficial = declaration.Official;
				chronicleOutsider = declaration.Outsider;
			}
			int ledgerBeforeCount = 0;
			int ledgerAfterCount = 0;
			string ledgerBeforeHash = null;
			string ledgerAfterHash = null;
			if (system.Ledger.Notes.Count >= 12) ledger = null;
			if (ledger != null)
			{
				ledgerBeforeCount = system.Ledger.Notes.Count;
				ledgerAfterCount = ledgerBeforeCount + 1;
				if (!TryHashStringList(system.Ledger.Notes, out ledgerBeforeHash)
					|| !TryHashStringListAfter(system.Ledger.Notes, ledger,
						out ledgerAfterHash)) return false;
			}
			KingdomGrowthOutboxEvent e = KingdomLifecycleRules.PrepareDeclaredGrowthOutboxEvent(
				operation, operation.OutboxEvents.Count, "arrival-" + kind, chronicle,
				chronicleOfficial, chronicleOutsider, ledger, null, null, null,
				chronicleBeforeCount, chronicleBeforeHash,
				chronicleAfterCount, chronicleAfterHash, outsiderBeforeCount,
				outsiderBeforeHash, outsiderAfterCount, outsiderAfterHash, ledgerBeforeCount,
				ledgerBeforeHash, ledgerAfterCount, ledgerAfterHash);
			if (e == null) return false;
			operation.OutboxEvents.Add(e);
			return true;
		}

		private static bool RootArrivalCandidate(KingdomGrowthArrivalCandidate candidate,
			GameObject settler)
		{
			if (The.Game == null || candidate == null || !GameObject.Validate(settler)) return false;
			object rooted;
			if (The.Game.ObjectGameState.TryGetValue(candidate.EscrowKey, out rooted)
				&& !ReferenceEquals(rooted, settler)) return false;
			The.Game.SetObjectGameState(candidate.EscrowKey, settler);
			return The.Game.ObjectGameState.TryGetValue(candidate.EscrowKey, out rooted)
				&& ReferenceEquals(rooted, settler);
		}

		private static bool TryExactArrivalRoot(KingdomGrowthArrivalCandidate candidate,
			out GameObject settler)
		{
			settler = null;
			object rooted;
			if (The.Game == null || candidate == null
				|| !The.Game.ObjectGameState.TryGetValue(candidate.EscrowKey, out rooted))
				return false;
			settler = rooted as GameObject;
			return settler != null;
		}

		private static bool TryArrivalObject(KingdomGrowthArrivalCandidate candidate,
			Zone zone, out GameObject settler)
		{
			if (TryExactArrivalRoot(candidate, out settler)) return true;
			settler = zone?.FindObjectByID(candidate?.ObjectId);
			return settler != null;
		}

		private static bool ExactEscrowedCandidate(KingdomGrowthArrivalCandidate candidate,
			GameObject settler)
		{
			GameObject rooted;
			return candidate != null && GameObject.Validate(settler)
				&& TryExactArrivalRoot(candidate, out rooted) && ReferenceEquals(rooted, settler)
				&& settler.ID == candidate.ObjectId && settler.Blueprint == candidate.Blueprint
				&& settler.Count == 1 && settler.CurrentCell == null
				&& (settler.Physics == null || settler.Physics.InInventory == null)
				&& settler.GetStringProperty(ArrivalMarkerProperty) == candidate.Marker;
		}

		private static bool ExactFreshEscrowedCandidate(KingdomGrowthArrivalCandidate candidate,
			GameObject settler)
		{
			GameObject rooted;
			return candidate != null && candidate.ObjectId == null
				&& GameObject.Validate(settler)
				&& TryExactArrivalRoot(candidate, out rooted) && ReferenceEquals(rooted, settler)
				&& !string.IsNullOrEmpty(settler.ID)
				&& settler.Blueprint == candidate.Blueprint
				&& settler.Count == 1 && settler.CurrentCell == null
				&& (settler.Physics == null || settler.Physics.InInventory == null)
				&& settler.GetStringProperty(ArrivalMarkerProperty) == candidate.Marker;
		}

		private static bool ExactCreatedCandidate(KingdomGrowthArrivalCandidate candidate,
			GameObject settler, Zone zone)
		{
			KingdomGrowthObjectCallbackStep step = candidate?.CreateStep;
			return step != null && ExactEscrowedCandidate(candidate, settler)
				&& string.Equals(step.AfterOwnerGraphHash,
					ArrivalObjectHash(candidate, settler,
						KingdomGrowthLocationKind.Escrow, -1, -1), StringComparison.Ordinal)
				&& string.Equals(step.AfterObjectGraphHash, ArrivalPersonHash(settler),
					StringComparison.Ordinal)
				&& string.Equals(step.AfterTopologyHash,
					ArrivalZoneIdentityHash(zone, settler, candidate.Marker,
						candidate.EscrowKey, KingdomGrowthLocationKind.Escrow, -1, -1),
					StringComparison.Ordinal);
		}

		private static bool ArrivalCellIsStillOpen(Cell cell)
		{
			return cell != null && cell.IsEmpty() && cell.IsPassable()
				&& !cell.HasObjectWithPart("LiquidVolume");
		}

		private static bool ExactPlacedCandidate(KingdomGrowthArrivalCandidate candidate,
			GameObject settler, Zone zone)
		{
			if (candidate == null || !GameObject.Validate(settler) || zone == null
				|| settler.ID != candidate.ObjectId || settler.Blueprint != candidate.Blueprint
				|| settler.Count != 1 || settler.GetStringProperty(ArrivalMarkerProperty)
					!= candidate.Marker || settler.CurrentCell == null
				|| !ReferenceEquals(settler.CurrentZone, zone)
				|| settler.CurrentCell.X != candidate.LodgingX
				|| settler.CurrentCell.Y != candidate.LodgingY) return false;
			GameObject found = zone.FindObjectByID(candidate.ObjectId);
			return ReferenceEquals(found, settler) && CountArrivalMarker(zone,
				candidate.Marker) == 1;
		}

		private static bool ExactRefusedCandidate(KingdomGrowthArrivalCandidate candidate,
			GameObject settler, Zone zone)
		{
			return candidate != null && candidate.Disposition ==
				KingdomGrowthArrivalDisposition.NoAcceptableHome
				&& (settler == null || !GameObject.Validate(settler))
				&& zone?.FindObjectByID(candidate.ObjectId) == null
				&& CountArrivalMarker(zone, candidate.Marker) == 0;
		}

		private static bool ExactDispositionEndpoint(KingdomGrowthArrivalCandidate candidate,
			GameObject settler, Zone zone, KingdomGrowthObjectCallbackStep step, bool after)
		{
			if (candidate == null || step == null || zone == null) return false;
			bool joined = candidate.Disposition == KingdomGrowthArrivalDisposition.Joined;
			if (!after)
				return ExactEscrowedCandidate(candidate, settler)
					&& string.Equals(step.BeforeOwnerGraphHash,
						ArrivalObjectHash(candidate, settler,
							KingdomGrowthLocationKind.Escrow, -1, -1),
						StringComparison.Ordinal)
					&& string.Equals(step.BeforeObjectGraphHash, ArrivalPersonHash(settler),
						StringComparison.Ordinal)
					&& string.Equals(step.BeforeTopologyHash,
						ArrivalTopologyHash(zone, candidate.ObjectId, candidate.Marker,
							candidate.EscrowKey, KingdomGrowthLocationKind.Escrow, -1, -1),
						StringComparison.Ordinal);
			if (joined)
				return ExactPlacedCandidate(candidate, settler, zone)
					&& string.Equals(step.AfterOwnerGraphHash,
						ArrivalObjectHash(candidate, settler,
							KingdomGrowthLocationKind.Cell, candidate.LodgingX,
							candidate.LodgingY), StringComparison.Ordinal)
					&& string.Equals(step.AfterObjectGraphHash, ArrivalPersonHash(settler),
						StringComparison.Ordinal)
					&& string.Equals(step.AfterTopologyHash,
						ArrivalTopologyHash(zone, candidate.ObjectId, candidate.Marker,
							candidate.EscrowKey, KingdomGrowthLocationKind.Cell,
							candidate.LodgingX, candidate.LodgingY), StringComparison.Ordinal);
			return ExactRefusedCandidate(candidate, settler, zone)
				&& string.Equals(step.AfterOwnerGraphHash,
					HashText("arrival-object-absent", candidate.ObjectId, candidate.Marker,
						candidate.Blueprint), StringComparison.Ordinal)
				&& string.Equals(step.AfterObjectGraphHash,
					HashText("arrival-person-absent", candidate.ObjectId, candidate.Marker,
						candidate.Blueprint), StringComparison.Ordinal)
				&& string.Equals(step.AfterTopologyHash,
					ArrivalTopologyHash(zone, candidate.ObjectId, candidate.Marker,
						candidate.EscrowKey, KingdomGrowthLocationKind.Graveyard, -1, -1),
					StringComparison.Ordinal);
		}
	}
}
