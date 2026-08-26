using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

// The engine resolves an XML <part Name="X"/> as the single type "XRL.World.Parts.X"
// (GamePartBlueprint.Namespace, :178; T => ModManager.ResolveType(Namespace, Name), :240), and
// the promised bare-TypeID fallback does not exist in the code. A part named in XML must live in
// this namespace or the object is built without it, silently. Only the part moves; the rest of
// the posted price stays where the mod's code lives.
namespace XRL.World.Parts
{
	/// <summary>
	/// A notice staked at the heart: a task, a price, and whatever the pass has made of it.
	/// <para>
	/// Carries no <c>WantTurnTick</c>. The canonical settlement pass calls
	/// <see cref="ThousandAndFirst.KingdomBounty.OnSettlementPass"/>; absolute daily schedule
	/// ticks decide which opportunities exist, so re-entry cannot mint another reader and elapsed
	/// missed opportunities are consumed rather than resolved against people who arrived later.
	/// </para>
	/// <para>
	/// Nothing is escrowed on this part. <see cref="Price"/> is a promise; <see cref="Paid"/> is
	/// the only number that ever corresponds to water having left the stores.
	/// </para>
	/// </summary>
	[Serializable]
	public partial class r_KingdomNotice : IPart
	{
		private const int SerializationMagic = 1413562964;

		private const int CurrentSerializationVersion = 1;

		/// <summary>The posted task, as <c>ThousandAndFirst.BountyTask</c>.</summary>
		public int TaskCode;

		/// <summary>Drams promised. Never taken from the stores until the work is done.</summary>
		public int Price;

		/// <summary>Drams actually handed over so far. The remainder is what is owed.</summary>
		public int Paid;

		/// <summary>Tick the notice went up. Preserved as legacy draw identity and schedule anchor;
		/// new reader attempts use their absolute scheduled tick in <see cref="EventStreamId"/>.</summary>
		public long PostedTick;

		/// <summary>Opportunities this notice has already resolved. Retained across migration so
		/// legacy visit-counted outcomes remain consumed; new draws use absolute scheduled ticks.</summary>
		public int Passes;

		/// <summary>Version of the absolute attempt schedule. Zero identifies a legacy notice.</summary>
		public int ScheduleVersion;

		/// <summary>Persisted semantic lane minted from the notice object's stable Qud id.</summary>
		public string EventStreamId;

		/// <summary>Absolute world tick of the next opportunity. Zone entry never changes it.</summary>
		public long NextAttemptTick;

		/// <summary>True only when long tick space has no later representable opportunity.</summary>
		public bool AttemptScheduleExhausted;

		/// <summary>Whoever took it, or null while it still stands unclaimed.</summary>
		public string WorkerName;

		/// <summary>Tick it was taken. Zero while unclaimed.</summary>
		public long TakenTick;

		/// <summary>Tick the work falls due, or zero for a task whose finish is read off the
		/// world rather than off a clock.</summary>
		public long DueTick;

		/// <summary>Cells, units, or works &mdash; whatever the task counts, kept for the prose
		/// and for the haul's own length.</summary>
		public int Magnitude;

		/// <summary>True once the work itself is finished and only the price remains.</summary>
		public bool Done;

		/// <summary>West edge of the staked rect, for a clearance notice.</summary>
		public int X1;

		/// <summary>North edge of the staked rect.</summary>
		public int Y1;

		/// <summary>East edge of the staked rect.</summary>
		public int X2;

		/// <summary>South edge of the staked rect.</summary>
		public int Y2;

		/// <summary>Object id of the marked pile, for a fetch notice.</summary>
		public string PileId;

		/// <summary>The reason last given for this notice standing still, as
		/// <c>ThousandAndFirst.BountyBlock</c>. Zero when nothing has been said. STANDARDS 7b:
		/// the reason is given once per stall, and again only when it changes.</summary>
		public int AnnouncedBlock;

		/// <summary>Set once the founder has been told the clearing gang would not take the
		/// staked rect. Cleared when it does.</summary>
		public bool StakeFailedAnnounced;

		/// <summary>Set once a refusal has been chronicled for this notice. Later refusals are
		/// remembered in the ledger instead, so a notice nobody wants never becomes a nag.</summary>
		public bool RefusalTold;

		/// <summary>Stable key for every deed, chronicle, ledger, and message receipt.</summary>
		public string LifecycleId;

		/// <summary>Malformed or physically ambiguous state is never guessed through.</summary>
		public bool LifecycleQuarantined;

		public string QuarantineReason;

		public bool QuarantineTold;
		public int QuarantineLedgerState;
		public int QuarantineMessageState;

		/// <summary>Durable publication of a newly inserted notice.</summary>
		public int PostPhase;
		public string PostZoneId;
		public int PostCellX;
		public int PostCellY;
		public int PostPileCellX;
		public int PostPileCellY;
		public string PostChronicleLine;
		public string PostMessageLine;
		public int PostMessageState;
		public int StakeCleanupState;

		/// <summary>Durable founder withdrawal and its one-shot cleanup callback.</summary>
		public int WithdrawPhase;
		public string WithdrawChronicleLine;
		public string WithdrawMessageLine;
		public string WithdrawPileId;
		public string WithdrawZoneId;
		public int WithdrawCellX;
		public int WithdrawCellY;
		public int WithdrawPileCellX;
		public int WithdrawPileCellY;
		public int WithdrawMessageState;

		/// <summary>Persisted reader selection and takeover transaction.</summary>
		public int TakePhase;
		public long PendingAttemptTick;
		public string PendingWorkerName;
		public int PendingWorkerResidentId;
		public int PendingVirtueIndex;
		public int PendingFlawIndex;
		public bool PendingTasteMatched;
		public bool PendingAttemptConsumed;
		public int TakeLedgerState;
		public int TakeMessageState;

		/// <summary>One exact fetch item, source, and destination.</summary>
		public int TransferPhase;
		public string TransferItemId;
		public string TransferSourceId;
		public string TransferDestinationId;
		public int TransferUnits;
		public int TransferTotalBefore;
		public int TransferredUnits;
		public int HaulPhase;

		/// <summary>Persisted scouting result and at-most-once deed publication.</summary>
		public int ScoutPhase;
		public string ScoutZoneId;
		public string ScoutGround;
		public int ScoutDeedState;

		/// <summary>Exact dedicated-vessel payout receipt, encoded as parallel canonical rows.</summary>
		public int PaymentPhase;
		public int PaymentAmount;
		public int PaymentPaidBefore;
		public int PaymentProved;
		public string PaymentZoneId;
		public string PaymentVesselIds;
		public string PaymentOriginalVolumes;
		public string PaymentMaxVolumes;
		public string PaymentAllocations;

		/// <summary>Terminal outputs precede cleanup, each through a durable phase.</summary>
		public int TerminalPhase;
		public int CompletionPhase;
		public string CompletionExtra;
		public int CompletionLedgerState;
		public int TerminalLedgerState;
		public int TerminalMessageState;
	}
}
