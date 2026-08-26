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
	public class r_KingdomNotice : IPart
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

		/// <summary>
		/// Qud's default IPart serializer is positional reflection (IComponent.Write/Read). Adding
		/// schedule fields would make an older notice under-consume and get skipped. This part now
		/// writes a named payload, while the reader consumes the exact eighteen-field legacy layout
		/// when the first boxed integer is not our marker.
		/// </summary>
		public override void Write(GameObject Basis, SerializationWriter Writer)
		{
			Writer.WriteObject(SerializationMagic);
			Writer.WriteObject(CurrentSerializationVersion);
			Writer.WriteNamedFields(this, typeof(r_KingdomNotice));
		}

		public override void Read(GameObject Basis, SerializationReader Reader)
		{
			object first = Reader.ReadObject();
			if (first is int && (int)first == SerializationMagic)
			{
				object savedVersion = Reader.ReadObject();
				if (!(savedVersion is int) || (int)savedVersion != CurrentSerializationVersion)
				{
					throw new InvalidOperationException("Unsupported ThousandAndFirst bounty notice save version.");
				}
				Reader.ReadNamedFields(this, typeof(r_KingdomNotice));
				NormalizeSerializedFields(Basis);
				return;
			}
			ReadLegacy(first, Reader);
			NormalizeSerializedFields(Basis);
		}

		private void ReadLegacy(object First, SerializationReader Reader)
		{
			TaskCode = Convert.ToInt32(First);
			Price = Convert.ToInt32(Reader.ReadObject());
			Paid = Convert.ToInt32(Reader.ReadObject());
			PostedTick = Convert.ToInt64(Reader.ReadObject());
			Passes = Convert.ToInt32(Reader.ReadObject());
			WorkerName = Reader.ReadObject() as string;
			TakenTick = Convert.ToInt64(Reader.ReadObject());
			DueTick = Convert.ToInt64(Reader.ReadObject());
			Magnitude = Convert.ToInt32(Reader.ReadObject());
			Done = Convert.ToBoolean(Reader.ReadObject());
			X1 = Convert.ToInt32(Reader.ReadObject());
			Y1 = Convert.ToInt32(Reader.ReadObject());
			X2 = Convert.ToInt32(Reader.ReadObject());
			Y2 = Convert.ToInt32(Reader.ReadObject());
			PileId = Reader.ReadObject() as string;
			AnnouncedBlock = Convert.ToInt32(Reader.ReadObject());
			StakeFailedAnnounced = Convert.ToBoolean(Reader.ReadObject());
			RefusalTold = Convert.ToBoolean(Reader.ReadObject());
		}

		private void NormalizeSerializedFields(GameObject Basis)
		{
			bool malformed = false;
			if (!SavedTextWithin(WorkerName, ThousandAndFirst.KingdomBountyRules.MaxSavedTextChars)
				|| !SavedTextWithin(PileId, ThousandAndFirst.KingdomBountyRules.MaxObjectIdChars)
				|| !SavedTextWithin(EventStreamId, 128)
				|| !SavedTextWithin(LifecycleId, 180)
				|| !SavedTextWithin(QuarantineReason, ThousandAndFirst.KingdomBountyRules.MaxSavedTextChars)
				|| !SavedTextWithin(PendingWorkerName, ThousandAndFirst.KingdomBountyRules.MaxSavedTextChars)
				|| !SavedTextWithin(TransferItemId, ThousandAndFirst.KingdomBountyRules.MaxObjectIdChars)
				|| !SavedTextWithin(TransferSourceId, ThousandAndFirst.KingdomBountyRules.MaxObjectIdChars)
				|| !SavedTextWithin(TransferDestinationId, ThousandAndFirst.KingdomBountyRules.MaxObjectIdChars)
				|| !SavedTextWithin(ScoutZoneId, ThousandAndFirst.KingdomBountyRules.MaxObjectIdChars)
				|| !SavedTextWithin(ScoutGround, ThousandAndFirst.KingdomBountyRules.MaxSavedTextChars)
				|| !SavedTextWithin(PaymentZoneId, ThousandAndFirst.KingdomBountyRules.MaxObjectIdChars)
				|| !SavedTextWithin(PaymentVesselIds, ThousandAndFirst.KingdomBountyRules.MaxPaymentRowsChars)
				|| !SavedTextWithin(PaymentOriginalVolumes, ThousandAndFirst.KingdomBountyRules.MaxPaymentRowsChars)
				|| !SavedTextWithin(PaymentMaxVolumes, ThousandAndFirst.KingdomBountyRules.MaxPaymentRowsChars)
				|| !SavedTextWithin(PaymentAllocations, ThousandAndFirst.KingdomBountyRules.MaxPaymentRowsChars)
				|| !SavedTextWithin(CompletionExtra, ThousandAndFirst.KingdomBountyRules.MaxSavedTextChars)
				|| !SavedTextWithin(PostChronicleLine, ThousandAndFirst.KingdomBountyRules.MaxSavedTextChars)
				|| !SavedTextWithin(PostMessageLine, ThousandAndFirst.KingdomBountyRules.MaxSavedTextChars)
				|| !SavedTextWithin(PostZoneId, ThousandAndFirst.KingdomBountyRules.MaxObjectIdChars)
				|| !SavedTextWithin(WithdrawChronicleLine, ThousandAndFirst.KingdomBountyRules.MaxSavedTextChars)
				|| !SavedTextWithin(WithdrawMessageLine, ThousandAndFirst.KingdomBountyRules.MaxSavedTextChars)
				|| !SavedTextWithin(WithdrawPileId, ThousandAndFirst.KingdomBountyRules.MaxObjectIdChars)
				|| !SavedTextWithin(WithdrawZoneId, ThousandAndFirst.KingdomBountyRules.MaxObjectIdChars))
			{
				malformed = true;
			}
			if (TaskCode < 0 || TaskCode >= ThousandAndFirst.KingdomBountyRules.TaskCount)
			{
				TaskCode = (int)ThousandAndFirst.BountyTask.Clearance;
				malformed = true;
			}
			if (Price < ThousandAndFirst.KingdomBountyRules.MinPrice
				|| Price > ThousandAndFirst.KingdomBountyRules.MaxPrice)
			{
				Price = ThousandAndFirst.KingdomBountyRules.ClampPrice(Price);
				malformed = true;
			}
			if (Paid < 0 || Paid > Price)
			{
				Paid = (Paid < 0) ? 0 : Price;
				malformed = true;
			}
			if (Passes < 0 || Passes > ThousandAndFirst.KingdomBountyRules.MaxPasses)
			{
				Passes = (Passes < 0) ? 0 : ThousandAndFirst.KingdomBountyRules.MaxPasses;
				malformed = true;
			}
			if (PostedTick < 0L || TakenTick < 0L || DueTick < 0L || NextAttemptTick < 0L)
			{
				PostedTick = (PostedTick < 0L) ? 0L : PostedTick;
				TakenTick = (TakenTick < 0L) ? 0L : TakenTick;
				DueTick = (DueTick < 0L) ? 0L : DueTick;
				NextAttemptTick = (NextAttemptTick < 0L) ? 0L : NextAttemptTick;
				malformed = true;
			}
			if (Magnitude < 0 || TransferredUnits < 0 || PaymentAmount < 0
				|| PaymentPaidBefore < 0 || PaymentProved < 0 || TransferUnits < 0)
			{
				Magnitude = (Magnitude < 0) ? 0 : Magnitude;
				TransferredUnits = (TransferredUnits < 0) ? 0 : TransferredUnits;
				PaymentAmount = (PaymentAmount < 0) ? 0 : PaymentAmount;
				PaymentPaidBefore = (PaymentPaidBefore < 0) ? 0 : PaymentPaidBefore;
				PaymentProved = (PaymentProved < 0) ? 0 : PaymentProved;
				TransferUnits = (TransferUnits < 0) ? 0 : TransferUnits;
				malformed = true;
			}
			if (Done && string.IsNullOrEmpty(WorkerName)) malformed = true;
			if (ScheduleVersion != 0 && ScheduleVersion != 2) malformed = true;
			if (ScheduleVersion == 2
				&& (!ThousandAndFirst.KingdomBountyRules.IsNoticeEventStream(EventStreamId)
					|| (AttemptScheduleExhausted ? NextAttemptTick != 0L : NextAttemptTick <= 0L)))
			{
				malformed = true;
			}
			if (TakePhase < 0 || TakePhase > (int)ThousandAndFirst.BountyTakePhase.Quarantined
				|| TransferPhase < 0 || TransferPhase > (int)ThousandAndFirst.BountyTransferPhase.Quarantined
				|| PaymentPhase < 0 || PaymentPhase > (int)ThousandAndFirst.BountyPaymentPhase.Quarantined
				|| TerminalPhase < 0 || TerminalPhase > (int)ThousandAndFirst.BountyTerminalPhase.CleanupLost
				|| ScoutPhase < 0 || ScoutPhase > 5 || HaulPhase < 0 || HaulPhase > 4
				|| CompletionPhase < 0 || CompletionPhase > 4
				|| PostPhase < 0 || PostPhase > (int)ThousandAndFirst.BountyPostPhase.Complete
				|| WithdrawPhase < 0 || WithdrawPhase > (int)ThousandAndFirst.BountyWithdrawPhase.CleanupLost
				|| !ValidSink(QuarantineLedgerState) || !ValidSink(QuarantineMessageState)
				|| !ValidSink(PostMessageState) || !ValidSink(StakeCleanupState)
				|| !ValidSink(WithdrawMessageState) || !ValidSink(TakeLedgerState)
				|| !ValidSink(TakeMessageState) || !ValidSink(ScoutDeedState)
				|| !ValidSink(CompletionLedgerState) || !ValidSink(TerminalLedgerState)
				|| !ValidSink(TerminalMessageState))
			{
				malformed = true;
			}
			if (TakePhase != (int)ThousandAndFirst.BountyTakePhase.None
				&& TakePhase != (int)ThousandAndFirst.BountyTakePhase.Complete
				&& (string.IsNullOrEmpty(PendingWorkerName) || PendingAttemptTick < 0L))
			{
				malformed = true;
			}
			if (TransferPhase != (int)ThousandAndFirst.BountyTransferPhase.None
				&& TransferPhase != (int)ThousandAndFirst.BountyTransferPhase.Quarantined
				&& (string.IsNullOrEmpty(TransferItemId) || string.IsNullOrEmpty(TransferSourceId)
					|| string.IsNullOrEmpty(TransferDestinationId) || TransferUnits <= 0))
			{
				malformed = true;
			}
			if (PaymentPhase != (int)ThousandAndFirst.BountyPaymentPhase.None
				&& PaymentPhase != (int)ThousandAndFirst.BountyPaymentPhase.Credited
				&& PaymentPhase != (int)ThousandAndFirst.BountyPaymentPhase.Quarantined
				&& (PaymentAmount <= 0 || PaymentPaidBefore > Price
					|| string.IsNullOrEmpty(PaymentZoneId)
					|| string.IsNullOrEmpty(PaymentVesselIds)
					|| string.IsNullOrEmpty(PaymentOriginalVolumes)
					|| string.IsNullOrEmpty(PaymentMaxVolumes)
					|| string.IsNullOrEmpty(PaymentAllocations)))
			{
				malformed = true;
			}
			if (ScoutPhase > 0 && string.IsNullOrEmpty(ScoutZoneId)) malformed = true;
			if (PostPhase > (int)ThousandAndFirst.BountyPostPhase.None
				&& (string.IsNullOrEmpty(PostChronicleLine) || string.IsNullOrEmpty(PostMessageLine)
					|| string.IsNullOrEmpty(PostZoneId) || PostCellX < 0 || PostCellY < 0
					|| (!string.IsNullOrEmpty(PileId) && (PostPileCellX < 0 || PostPileCellY < 0))))
			{
				malformed = true;
			}
			if (WithdrawPhase > (int)ThousandAndFirst.BountyWithdrawPhase.None
				&& (string.IsNullOrEmpty(WithdrawChronicleLine)
					|| string.IsNullOrEmpty(WithdrawMessageLine)
					|| string.IsNullOrEmpty(WithdrawZoneId)
					|| WithdrawCellX < 0 || WithdrawCellY < 0
					|| (!string.IsNullOrEmpty(WithdrawPileId)
						&& (WithdrawPileCellX < 0 || WithdrawPileCellY < 0))))
			{
				malformed = true;
			}
			if (string.IsNullOrEmpty(LifecycleId))
			{
				LifecycleId = ThousandAndFirst.KingdomBountyRules.NoticeEventId(
					(Basis != null) ? Basis.ID : null);
			}
			else if (!ThousandAndFirst.KingdomBountyRules.IsNoticeEventId(LifecycleId))
			{
				malformed = true;
			}
			if (malformed)
			{
				LifecycleQuarantined = true;
				QuarantineReason = "The notice's saved lifecycle is malformed; no work or payment was guessed through it.";
				AttemptScheduleExhausted = true;
				NextAttemptTick = 0L;
			}
		}

		private static bool SavedTextWithin(string Text, int Maximum)
		{
			return Text == null || (Maximum >= 0 && Text.Length <= Maximum);
		}

		private static bool ValidSink(int Raw)
		{
			return Raw >= (int)ThousandAndFirst.BountySinkDisposition.None
				&& Raw <= (int)ThousandAndFirst.BountySinkDisposition.Lost;
		}
	}
}

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	/// <summary>
	/// The posted price: a notice staked at the heart offering drams to whoever performs a named
	/// task, and the settlers and notables who read it and decide for themselves.
	/// <para>
	/// Three rules run through everything here.
	/// <b>Nothing is escrowed</b> &mdash; the price is a promise until the work is done, and the
	/// only water that ever leaves the stores is water paid to somebody who finished something.
	/// <b>Nothing nags</b> &mdash; an unclaimed notice just stands there, with no expiry, no
	/// reminder, and no penalty; the founder takes it down for free whenever they like, and the
	/// chronicle remembers that they did. <b>Nothing stalls in silence</b> (STANDARDS 7b) &mdash;
	/// a notice that cannot be moved says why once, and a notice that can never be attempted at
	/// all says so once and then keeps quiet forever.
	/// </para>
	/// <para>
	/// Every founder-facing entry point does its own eligibility check, its own messaging, and its
	/// own chronicle entry, and surfaces only a decline &mdash; the <c>KingdomLarder</c> idiom the
	/// rest of the mod follows. A refusal changes nothing.
	/// </para>
	/// </summary>
	public static class KingdomBounty
	{
		public static bool Enabled => Options.GetOption("r_TAF_OptionBounty") != "No";

		/// <summary>The one blueprint a staked notice can be, named here rather than inferred.</summary>
		public const string NoticeBlueprint = "r_KingdomNotice";

		/// <summary>
		/// String property written on a container the founder marks for a fetch notice, carrying
		/// the notice's own object id. The mark <b>is</b> the designation the protection law
		/// requires: nothing is ever carried out of a container that does not name a live notice
		/// of this settlement's.
		/// </summary>
		public const string FetchMarkProperty = "KingdomFetchNotice";

		/// <summary>
		/// Takeover point for the carry-sign, which generalises the fetch task past this
		/// settlement's own ground: distance-scaled days, chronicled porters, and a load that can
		/// be lost to the road.
		/// <para>
		/// Left null, the fetch task resolves the short way below &mdash; a marked pile standing in
		/// the same ground as the notice, carried into the dedicated stockpiles. Set, it is
		/// consulted first and its answer is final: return true once the haul has been taken over
		/// (this file then only pays the price when the hook reports the load home), false to let
		/// the short way run.
		/// </para>
		/// <para>
		/// Arguments, in order: the realm, the marked pile, the porter's name, and the tick the
		/// haul was taken. Not supported API &mdash; a coordination seam between two systems of
		/// this mod, and it moves when they do.
		/// </para>
		/// </summary>
		public static Func<KingdomSystem, GameObject, string, long, bool> HaulHook;

		private static readonly int[] PriceLadder = new int[8] { 1, 2, 3, 5, 8, 12, 20, 40 };

		// ==================================================================================
		// Posting
		// ==================================================================================

		/// <summary>
		/// The Charter's own entry: shows what is standing at this heart, lets the founder post
		/// another, and lets them take one down. Does all its own messaging.
		/// </summary>
		/// <param name="System">The realm. Unfounded is refused with a reason.</param>
		/// <param name="Founder">The object holding the Charter, read for where it is standing.</param>
		public static void OpenNotices(KingdomSystem System, GameObject Founder)
		{
			if (System == null || !System.Founded)
			{
				Popup.Show("You rule nothing yet.");
				return;
			}
			if (!Enabled)
			{
				Popup.Show("The settlement posts no prices. (Options: the posted price)");
				return;
			}
			Zone zone = (Founder != null) ? Founder.CurrentZone : null;
			if (zone == null || !System.ClaimedZones.Contains(zone.ZoneID))
			{
				Popup.Show("A notice is staked on the kingdom's own ground, where its people will walk past it.");
				return;
			}
			while (true)
			{
				List<GameObject> notices = Notices(zone);
				List<string> options = new List<string>();
				for (int i = 0; i < notices.Count; i++)
				{
					options.Add(StatusLine(System, notices[i]));
				}
				options.Add((notices.Count >= KingdomBountyRules.MaxNotices)
					? ("{{K|" + KingdomBountyRules.MaxNotices + " notices already stand here}}")
					: "{{W|Post a new notice}}");
				int pick = Popup.PickOption(
					Title: "The notice board of " + KingdomPresentation.Rich(System.SeatName),
					Intro: "Nothing here is set aside. A price leaves the stores the day the work is done, and not before.",
					Options: options, AllowEscape: true);
				if (pick < 0)
				{
					return;
				}
				if (pick >= notices.Count)
				{
					if (notices.Count >= KingdomBountyRules.MaxNotices)
					{
						Popup.Show("Three notices is as many as one heart can carry without the settlement stopping to read them all.");
						continue;
					}
					PostNotice(System, zone, Founder);
					if (KingdomGovernanceScope.HasCommitted)
					{
						return;
					}
					continue;
				}
				Inspect(System, zone, notices[pick]);
			}
		}

		private static void Inspect(KingdomSystem System, Zone Z, GameObject Notice)
		{
			r_KingdomNotice notice = Notice.GetPart<r_KingdomNotice>();
			if (notice == null)
			{
				return;
			}
			BountyTask task = (BountyTask)notice.TaskCode;
			string body = KingdomBountyRules.NoticeText(task, notice.Price, DetailOf(System, Z, notice))
				+ "\n\n" + Progress(System, notice);
			int pick = Popup.PickOption(
				Title: "A posted notice",
				Intro: body,
				Options: new string[2] { "Leave it standing", "{{W|Take it down}}" },
				AllowEscape: true);
			if (pick != 1)
			{
				return;
			}
			if (Popup.ShowYesNo("Take the notice down?\n\nNothing is owed either way, and nobody who took it is made to give anything back.") != DialogResult.Yes)
			{
				return;
			}
			Withdraw(System, Z, Notice, notice);
		}

		private static void Withdraw(KingdomSystem System, Zone Z, GameObject Notice, r_KingdomNotice Data)
		{
			EnsureLifecycleIdentity(Notice, Data);
			if ((BountyWithdrawPhase)Data.WithdrawPhase == BountyWithdrawPhase.None)
			{
				Cell initialCell = (Notice != null) ? Notice.CurrentCell : null;
				if (!NoticeBindingExact(Notice, Data, Z, initialCell))
				{
					Quarantine(Data, "Withdrawal could not bind the exact notice object, part, cell, and zone.");
					TellQuarantine(System, Data);
					return;
				}
				BountyTask task = (BountyTask)Data.TaskCode;
				bool claimed = !string.IsNullOrEmpty(Data.WorkerName);
				Data.WithdrawPileId = Data.PileId;
				Data.WithdrawZoneId = Z.ZoneID;
				Data.WithdrawCellX = Notice.CurrentCell.X;
				Data.WithdrawCellY = Notice.CurrentCell.Y;
				GameObject withdrawPile = string.IsNullOrEmpty(Data.WithdrawPileId)
					? null : Z.FindObjectByID(Data.WithdrawPileId);
				Data.WithdrawPileCellX = (withdrawPile != null && withdrawPile.CurrentCell != null)
					? withdrawPile.CurrentCell.X : 0;
				Data.WithdrawPileCellY = (withdrawPile != null && withdrawPile.CurrentCell != null)
					? withdrawPile.CurrentCell.Y : 0;
				Data.WithdrawChronicleLine = KingdomBountyRules.WithdrawnChronicle(
					KingdomPresentation.Rich(System.SeatName), task, claimed,
					KingdomPresentation.Rich(Data.WorkerName));
				Data.WithdrawMessageLine = "{{K|The notice comes off the stake.}} " + (claimed
					? ("Word will reach " + KingdomPresentation.Rich(Data.WorkerName)
						+ " that the settlement has changed its mind. Nothing is asked back.")
					: "Nobody had taken it, and nothing was spent.");
				Data.WithdrawMessageState = (int)BountySinkDisposition.Pending;
				Data.WithdrawPhase = (int)BountyWithdrawPhase.Bound;
			}
			ContinueWithdraw(System, Z, Notice, Data);
		}

		private static void ContinueWithdraw(KingdomSystem System, Zone Z, GameObject Notice,
			r_KingdomNotice Data)
		{
			BountyWithdrawPhase phase = (BountyWithdrawPhase)Data.WithdrawPhase;
			if (phase == BountyWithdrawPhase.None || phase == BountyWithdrawPhase.CleanupLost) return;
			Cell noticeCell = BoundCell(Z, Data.WithdrawZoneId,
				Data.WithdrawCellX, Data.WithdrawCellY);
			if (!NoticeBindingExact(Notice, Data, Z, noticeCell))
			{
				Data.WithdrawPhase = (int)BountyWithdrawPhase.CleanupLost;
				Quarantine(Data, "The withdrawing notice no longer has its exact object, part, cell, and zone binding.");
				return;
			}
			if (phase == BountyWithdrawPhase.CleanupAttempting)
			{
				Data.WithdrawPhase = (int)BountyWithdrawPhase.CleanupLost;
				Quarantine(Data, "Withdrawal cleanup was interrupted; the destructive callback was not repeated.");
				return;
			}
			if (phase == BountyWithdrawPhase.Bound)
			{
				if (!ClearBoundFetchMark(Z, Notice, Data, Data.WithdrawPileId))
				{
					Data.WithdrawPhase = (int)BountyWithdrawPhase.CleanupLost;
					Quarantine(Data, "The withdrawing notice could not prove an exact fetch-mark compare-and-clear.");
					return;
				}
				Data.WithdrawPhase = (int)BountyWithdrawPhase.MarkCleared;
				phase = BountyWithdrawPhase.MarkCleared;
			}
			if (phase == BountyWithdrawPhase.MarkCleared)
			{
				if (!KingdomChronicle.RecordOnce(System, EventId(Data, "withdrawn"),
					Data.WithdrawChronicleLine)) return;
				Data.WithdrawPhase = (int)BountyWithdrawPhase.ChronicleDone;
				phase = BountyWithdrawPhase.ChronicleDone;
			}
			if (phase == BountyWithdrawPhase.ChronicleDone)
			{
				if (!DeliverMessage(ref Data.WithdrawMessageState, Data.WithdrawMessageLine)) return;
				Data.WithdrawPhase = (int)BountyWithdrawPhase.MessageSettled;
				phase = BountyWithdrawPhase.MessageSettled;
			}
			if (phase == BountyWithdrawPhase.MessageSettled)
			{
				CleanupFrame cleanup;
				if (!TryCaptureCleanup(Notice, Data, out cleanup))
				{
					Data.WithdrawPhase = (int)BountyWithdrawPhase.CleanupLost;
					Quarantine(Data,
						"Withdrawal cleanup could not capture its exact notice and data-part identity.");
					return;
				}
				Data.WithdrawPhase = (int)BountyWithdrawPhase.CleanupAttempting;
				KingdomLog.Log("bounty: withdrawing " + EventId(Data, "withdrawn"));
				InvokeCleanupOnce(Notice, false);
				if (!CleanupFinalized(cleanup))
				{
					Data.WithdrawPhase = (int)BountyWithdrawPhase.CleanupLost;
					Quarantine(Data, "Withdrawal cleanup was vetoed or changed by its destructive callback; it was not repeated.");
				}
			}
		}

		private static void PostNotice(KingdomSystem System, Zone Z, GameObject Founder)
		{
			KingdomSurvey survey = KingdomSurvey.Take(Z);
			BountyTask[] tasks = new BountyTask[KingdomBountyRules.TaskCount]
			{
				BountyTask.Clearance, BountyTask.Fetch, BountyTask.Manning, BountyTask.Scouting
			};
			string[] refusals = new string[KingdomBountyRules.TaskCount];
			string[] options = new string[KingdomBountyRules.TaskCount];
			for (int i = 0; i < tasks.Length; i++)
			{
				refusals[i] = WhyNotPostable(System, Z, Founder, survey, tasks[i]);
				options[i] = (refusals[i] == null)
					? KingdomBountyRules.TaskName(tasks[i]).Capitalize()
					: ("{{K|" + KingdomBountyRules.TaskName(tasks[i]).Capitalize() + " -- " + refusals[i] + "}}");
			}
			int pick = Popup.PickOption(
				Title: "What is the price for?",
				Intro: "Name a task and a price. Settlers read the notice on their own time and decide for themselves; nobody is ordered to take it.",
				Options: options, AllowEscape: true);
			if (pick < 0)
			{
				return;
			}
			if (refusals[pick] != null)
			{
				Popup.Show(refusals[pick].Capitalize() + ".");
				return;
			}
			Stake(System, Z, Founder, survey, tasks[pick]);
		}

		/// <summary>Why a task cannot be posted on this ground right now, or null when it can.
		/// Lower-case clause, no trailing period.</summary>
		private static string WhyNotPostable(KingdomSystem System, Zone Z, GameObject Founder, KingdomSurvey Survey, BountyTask Task)
		{
			switch (Task)
			{
			case BountyTask.Clearance:
			{
				if (!KingdomMaterials.Enabled)
				{
					return "the settlement is not clearing ground";
				}
				Cell cell = (Founder != null) ? Founder.CurrentCell : null;
				if (cell == null)
				{
					return "there is nowhere here to stake a cord";
				}
				return null;
			}
			case BountyTask.Fetch:
			{
				if (MarkablePiles(Z, Founder).Count == 0)
				{
					return "there is no pile of materials within reach to mark";
				}
				if (KingdomMaterials.Stock(Z).None)
				{
					return "there is no stockpile dedicated to carry anything into";
				}
				return null;
			}
			case BountyTask.Manning:
				return (Survey.Works.Count == 0) ? "the settlement has no works to stand" : null;
			case BountyTask.Scouting:
				return (Frontier(System).Count == 0) ? "the claim has no unclaimed edge left" : null;
			default:
				return "the notice board does not know that word";
			}
		}

		private static void Stake(KingdomSystem System, Zone Z, GameObject Founder, KingdomSurvey Survey, BountyTask Task)
		{
			int magnitude = 0;
			int x1 = 0;
			int y1 = 0;
			int x2 = 0;
			int y2 = 0;
			GameObject pile = null;
			if (Task == BountyTask.Clearance && !PickRect(System, Z, Founder, out x1, out y1, out x2, out y2, out magnitude))
			{
				return;
			}
			if (Task == BountyTask.Fetch && !PickPile(System, Z, Founder, out pile, out magnitude))
			{
				return;
			}
			if (Task == BountyTask.Manning)
			{
				magnitude = Survey.Works.Count;
			}
			int price = PickPrice(System, Survey, Task, magnitude);
			if (price <= 0)
			{
				return;
			}
			string warning = (Survey.StoredWater < price)
				? ("\n\n{{r|The stores hold " + Survey.StoredWater + " drams, which will not cover it today.}} Nothing is set aside now, so the notice may stand until they do -- but whoever claims it will be owed until then.")
				: "";
			if (Popup.ShowYesNo("Stake the notice?\n\n" + KingdomBountyRules.NoticeText(Task, price, null)
				+ "\n\nNo water leaves the stores until somebody has done it." + warning) != DialogResult.Yes)
			{
				return;
			}
			Cell cell = HeartCell(Z, Founder);
			if (cell == null)
			{
				Popup.Show("There is nowhere at the heart to drive a stake.");
				return;
			}
			GameObject notice = GameObject.Create(NoticeBlueprint);
			r_KingdomNotice data = (notice != null) ? notice.GetPart<r_KingdomNotice>() : null;
			if (data == null)
			{
				InvokeCleanupOnce(notice, true);
				Popup.Show("The stake could not be driven.");
				return;
			}
			data.TaskCode = (int)Task;
			data.Price = price;
			data.PostedTick = The.Game.TimeTicks;
			data.ScheduleVersion = 2;
			data.EventStreamId = KingdomBountyRules.NoticeEventStream(notice.ID);
			data.LifecycleId = KingdomBountyRules.NoticeEventId(notice.ID);
			data.AttemptScheduleExhausted = !KingdomBountyRules.TryFirstAttemptTick(data.PostedTick, out data.NextAttemptTick);
			data.Magnitude = magnitude;
			data.X1 = x1;
			data.Y1 = y1;
			data.X2 = x2;
			data.Y2 = y2;
			data.PostChronicleLine = KingdomBountyRules.PostedChronicle(
				KingdomPresentation.Rich(System.SeatName), Task, price);
			data.PostMessageLine = "{{G|The notice is up.}} "
				+ KingdomBountyRules.NoticeText(Task, price, null);
			data.PostZoneId = Z.ZoneID;
			data.PostCellX = cell.X;
			data.PostCellY = cell.Y;
			data.PostPileCellX = (pile == null || pile.CurrentCell == null) ? 0 : pile.CurrentCell.X;
			data.PostPileCellY = (pile == null || pile.CurrentCell == null) ? 0 : pile.CurrentCell.Y;
			data.PostMessageState = (int)BountySinkDisposition.Pending;
			data.PostPhase = (int)BountyPostPhase.Bound;
			string oldPileMark = (pile == null) ? null : pile.GetStringProperty(FetchMarkProperty);
			Cell pileCell = (pile == null) ? null : pile.CurrentCell;
			bool inserted = false;
			GameObject acceptedNotice = null;
			try
			{
				acceptedNotice = cell.AddObject(notice);
				inserted = ReferenceEquals(acceptedNotice, notice)
					&& NoticeBindingExact(notice, data, Z, cell);
				if (!inserted) throw new InvalidOperationException(
					"The notice insertion callback changed its exact object, part, cell, or zone binding.");
				notice.MakeActive();
				if (!NoticeBindingExact(notice, data, Z, cell)) throw new InvalidOperationException(
					"Activating the notice changed its exact object, part, cell, or zone binding.");
				if (pile != null)
				{
					if (!PileBindingExact(pile, Z, pileCell)
						|| pile.GetStringProperty(FetchMarkProperty) != oldPileMark
						|| !string.IsNullOrEmpty(oldPileMark)) throw new InvalidOperationException(
							"The selected fetch pile changed before its mark compare-and-set.");
					data.PileId = pile.ID;
					pile.SetStringProperty(FetchMarkProperty, notice.ID);
					if (!PileBindingExact(pile, Z, pileCell)
						|| pile.GetStringProperty(FetchMarkProperty) != notice.ID
						|| !NoticeBindingExact(notice, data, Z, cell)) throw new InvalidOperationException(
							"The fetch mark compare-and-set did not leave its exact binding.");
				}
			}
			catch (Exception error)
			{
				if (pile != null && PileBindingExact(pile, Z, pileCell)
					&& pile.GetStringProperty(FetchMarkProperty) == notice.ID)
				{
					pile.SetStringProperty(FetchMarkProperty, oldPileMark, RemoveIfNull: true);
				}
				Quarantine(data, inserted
					? "Posting crossed an uncertain activation or fetch-mark callback seam."
					: "Posting crossed an uncertain insertion callback seam.");
				data.StakeCleanupState = (int)BountySinkDisposition.Pending;
				CleanupFrame cleanup;
				if (TryCaptureCleanup(notice, data, out cleanup))
				{
					data.StakeCleanupState = (int)BountySinkDisposition.Attempting;
					InvokeCleanupOnce(notice, true);
					data.StakeCleanupState = (int)(CleanupFinalized(cleanup)
						? BountySinkDisposition.Delivered : BountySinkDisposition.Lost);
				}
				else
				{
					data.StakeCleanupState = (int)BountySinkDisposition.Skipped;
				}
				MetricsManager.LogError("ThousandAndFirst bounty posting", error);
				Popup.Show((data.StakeCleanupState == (int)BountySinkDisposition.Delivered)
					? "The stake could not be driven cleanly; its one cleanup attempt removed it."
					: "The stake crossed an uncertain callback seam. It is quarantined and cleanup was not repeated.");
				return;
			}
			finally
			{
				KingdomSurvey.ObserveAddResultInActive(Z, notice, acceptedNotice);
			}
			// The notice, its active schedule, and any protected fetch mark are now durable.
			// Telling and description may fail without turning that completed publication free.
			KingdomGovernanceScope.Commit("post bounty");
			Describe(System, Z, notice, data);
			ContinuePost(System, Z, notice, data);
			KingdomLog.Log("bounty: posted task=" + KingdomBountyRules.TaskKey(Task) + " price=" + price + " magnitude=" + magnitude);
		}

		private static bool PickRect(KingdomSystem System, Zone Z, GameObject Founder, out int X1, out int Y1, out int X2, out int Y2, out int Cells)
		{
			X1 = 0;
			Y1 = 0;
			X2 = 0;
			Y2 = 0;
			Cells = 0;
			Cell here = (Founder != null) ? Founder.CurrentCell : null;
			if (here == null)
			{
				return false;
			}
			int[] widths = new int[3] { 3, 5, 9 };
			int[] heights = new int[3] { 3, 5, 7 };
			string[] names = new string[3] { "the ground you stand on", "a working yard", "a wide clearing" };
			string[] options = new string[3];
			int[][] rects = new int[3][];
			KingdomMaterials.ClearanceAssessment[] assessed = new KingdomMaterials.ClearanceAssessment[3];
			for (int i = 0; i < 3; i++)
			{
				int left = here.X - widths[i] / 2;
				int top = here.Y - heights[i] / 2;
				rects[i] = new int[4] { left, top, left + widths[i] - 1, top + heights[i] - 1 };
				assessed[i] = KingdomMaterials.Assess(System, Z, rects[i][0], rects[i][1], rects[i][2], rects[i][3]);
				string size = " (" + widths[i] + " by " + heights[i] + ")";
				if (!assessed[i].Valid)
				{
					options[i] = "{{K|" + names[i] + size + " -- runs off the edge of this ground}}";
				}
				else if (assessed[i].Refusal != null)
				{
					options[i] = "{{K|" + names[i] + size + " -- something stands in it}}";
				}
				else if (assessed[i].Standing <= 0)
				{
					options[i] = "{{K|" + names[i] + size + " -- nothing in it has to come down}}";
				}
				else
				{
					options[i] = names[i] + size + " {{K|(" + assessed[i].Standing + " standing, and "
						+ (assessed[i].Yield.Describe() ?? "turned earth") + " out of it)}}";
				}
			}
			int pick = Popup.PickOption(
				Title: "Which ground?",
				Intro: "A clearance notice pays twice: the price you post, and whatever comes out of the ground.",
				Options: options, AllowEscape: true);
			if (pick < 0)
			{
				return false;
			}
			if (!assessed[pick].Valid)
			{
				Popup.Show("That ground runs off the edge of this one.");
				return false;
			}
			if (assessed[pick].Refusal != null)
			{
				Popup.Show(assessed[pick].Refusal);
				return false;
			}
			if (assessed[pick].Standing <= 0)
			{
				Popup.Show("There is nothing in it that has to come down. A notice over clear ground would never be claimed.");
				return false;
			}
			X1 = rects[pick][0];
			Y1 = rects[pick][1];
			X2 = rects[pick][2];
			Y2 = rects[pick][3];
			Cells = assessed[pick].Cells;
			return true;
		}

		private static bool PickPile(KingdomSystem System, Zone Z, GameObject Founder, out GameObject Pile, out int Units)
		{
			Pile = null;
			Units = 0;
			List<GameObject> candidates = MarkablePiles(Z, Founder);
			if (candidates.Count == 0)
			{
				Popup.Show("There is no pile of materials within reach to mark.");
				return false;
			}
			string[] options = new string[candidates.Count];
			int[] counts = new int[candidates.Count];
			for (int i = 0; i < candidates.Count; i++)
			{
				counts[i] = MaterialUnits(candidates[i]);
				options[i] = candidates[i].ShortDisplayName + " {{K|(" + counts[i]
					+ ((counts[i] == 1) ? " load" : " loads") + " of material)}}";
			}
			int pick = Popup.PickOption(
				Title: "Which pile?",
				Intro: "The mark is the whole of the designation. Nothing is ever carried out of a container you have not marked, and the mark comes off when the notice does.",
				Options: options, AllowEscape: true);
			if (pick < 0)
			{
				return false;
			}
			Pile = candidates[pick];
			Units = counts[pick];
			return true;
		}

		private static int PickPrice(KingdomSystem System, KingdomSurvey Survey, BountyTask Task, int Magnitude)
		{
			int suggested = KingdomBountyRules.SuggestedPrice(Task, Magnitude);
			string[] options = new string[PriceLadder.Length];
			for (int i = 0; i < PriceLadder.Length; i++)
			{
				int price = KingdomBountyRules.ClampPrice(PriceLadder[i]);
				options[i] = price + ((price == 1) ? " dram" : " drams")
					+ ((price == suggested) ? " {{G|[what the work is worth]}}" : "")
					+ ((price > Survey.StoredWater) ? " {{r|(more than the stores hold today)}}" : "");
			}
			int pick = Popup.PickOption(
				Title: "Name the price",
				Intro: "The stores hold " + Survey.StoredWater + " drams. None of it is set aside by posting; the price is drawn the day the work is done.",
				Options: options, AllowEscape: true);
			return (pick < 0) ? 0 : KingdomBountyRules.ClampPrice(PriceLadder[pick]);
		}

		// ==================================================================================
		// The pass
		// ==================================================================================

		/// <summary>
		/// Resolves every notice standing on this ground through the current absolute schedule: who read them, who
		/// took them, what got finished, and what got paid.
		/// <para>
		/// Call from the settlement's canonical attended pass <b>after</b> growth and
		/// improvement, and for the same reason those two are ordered against each other: this
		/// pays out of what the stores have left once the settlement's own upkeep and arrivals are
		/// done with them, and it mans works out of the idleness growth has just finished
		/// measuring.
		/// </para>
		/// </summary>
		/// <param name="System">The realm. Does nothing when unfounded.</param>
		/// <param name="Z">The activated ground. Does nothing when it is not the kingdom's.</param>
		/// <param name="Survey">This pass's shared survey, drawn from and written to.</param>
		public static void OnSettlementPass(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			if (!Enabled || System == null || !System.Founded || Z == null || Survey == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			List<GameObject> notices = new List<GameObject>(Survey.Notices);
			for (int i = 0; i < notices.Count; i++)
			{
				GameObject notice = notices[i];
				r_KingdomNotice data = notice.GetPart<r_KingdomNotice>();
				if (data == null || !GameObject.Validate(notice))
				{
					continue;
				}
				Resolve(System, Z, Survey, notice, data);
			}
		}

		private static void Resolve(KingdomSystem System, Zone Z, KingdomSurvey Survey, GameObject Notice, r_KingdomNotice Data)
		{
			EnsureLifecycleIdentity(Notice, Data);
			if ((BountySinkDisposition)Data.StakeCleanupState == BountySinkDisposition.Attempting)
			{
				Data.StakeCleanupState = (int)BountySinkDisposition.Lost;
				Quarantine(Data, "Stake cleanup was interrupted; its destructive callback was not repeated.");
			}
			if ((BountyWithdrawPhase)Data.WithdrawPhase != BountyWithdrawPhase.None)
			{
				ContinueWithdraw(System, Z, Notice, Data);
				if (Data.LifecycleQuarantined) TellQuarantine(System, Data);
				return;
			}
			if (Data.LifecycleQuarantined)
			{
				TellQuarantine(System, Data);
				return;
			}
			if ((BountyPostPhase)Data.PostPhase != BountyPostPhase.None
				&& (BountyPostPhase)Data.PostPhase != BountyPostPhase.Complete)
			{
				ContinuePost(System, Z, Notice, Data);
				if (Data.LifecycleQuarantined) TellQuarantine(System, Data);
				return;
			}
			if ((BountyTakePhase)Data.TakePhase != BountyTakePhase.None
				&& (BountyTakePhase)Data.TakePhase != BountyTakePhase.Complete)
			{
				ContinueTake(System, Z, Notice, Data);
			}
			if ((BountyTakePhase)Data.TakePhase == BountyTakePhase.Complete)
			{
				CompleteTakeCursor(Data);
			}
			if (Data.LifecycleQuarantined
				|| (BountyTakePhase)Data.TakePhase != BountyTakePhase.None) return;
			if (Data.Done)
			{
				if (Data.CompletionPhase > 0 && Data.CompletionPhase < 4)
				{
					ContinueFinish(System, Z, Survey, Notice, Data);
					return;
				}
				Settle(System, Z, Survey, Notice, Data);
				return;
			}
			if (!string.IsNullOrEmpty(Data.WorkerName))
			{
				Work(System, Z, Survey, Notice, Data);
				return;
			}
			EnsureAttemptSchedule(Notice, Data, The.Game.TimeTicks);
			if (Data.AttemptScheduleExhausted || Data.Passes >= KingdomBountyRules.MaxPasses)
			{
				Data.AttemptScheduleExhausted = true;
				return;
			}
			BountyBlock block = Blocking(System, Z, Survey, Data);
			if (block != BountyBlock.None)
			{
				Announce(System, Data, block);
				return;
			}
			Announce(System, Data, BountyBlock.None);
			BountyTask task = (BountyTask)Data.TaskCode;
			long latestTick;
			long skipped;
			if (!KingdomBountyRules.TryLatestDueAttempt(The.Game.TimeTicks, Data.NextAttemptTick,
				Data.AttemptScheduleExhausted, out latestTick, out skipped))
			{
				return;
			}
			if (!ConsumeMissedAttempts(Data, latestTick, skipped))
			{
				return;
			}
			int due = 1;
			int presented = 0;
			int omittedRefusals = 0;
			string settlementId = KingdomChronicle.SettlementId(System);
			Simulation.City.KingdomCityState residentState;
			Simulation.City.KingdomResidentRollProjection roll;
			List<string> residentNames = Simulation.City.KingdomResidents.TryRoll(System,
				out residentState, out roll) ? roll.Names : new List<string>();
			for (int i = 0; i < due && string.IsNullOrEmpty(Data.WorkerName)
				&& !Data.AttemptScheduleExhausted
				&& Data.Passes < KingdomBountyRules.MaxPasses; i++)
			{
				long scheduledTick = Data.NextAttemptTick;
				KingdomBountyRules.BountyAttempt attempt = KingdomBountyRules.ResolveScheduled(
					settlementId, Data.EventStreamId, scheduledTick, residentNames, task, Data.Price);
				if (!attempt.Determined)
				{
					// Kernel failure has no outcome. Keep this exact scheduled event at the cursor;
					// a later pass retries it instead of silently burning it.
					break;
				}
				if (attempt.Outcome == BountyOutcome.Taken)
				{
					bool taken = Take(System, Z, Notice, Data, task, attempt, scheduledTick);
					if ((BountyTakePhase)Data.TakePhase == BountyTakePhase.Complete) CompleteTakeCursor(Data);
					if (taken)
					{
						break;
					}
					continue;
				}
				ConsumeAttempt(Data, scheduledTick);
				if (attempt.Outcome != BountyOutcome.Refused)
				{
					continue;
				}
				if (!Data.RefusalTold)
				{
					if (KingdomChronicle.RecordOnce(System, EventId(Data, "refused"),
						KingdomBountyRules.RefusedChronicle(attempt.Name, task, attempt.FlawIndex)))
					{
						Data.RefusalTold = true;
					}
				}
				if (presented < KingdomBountyRules.MaxAttemptPresentations)
				{
					presented++;
					System.Ledger.Note("{{K|" + attempt.Name + " read the notice offering water to " + KingdomBountyRules.TaskName(task) + ", and left it standing.}}");
					KingdomLog.Log("bounty: refused by " + attempt.Name + " task=" + KingdomBountyRules.TaskKey(task) + " scheduled=" + scheduledTick);
				}
				else
				{
					omittedRefusals++;
				}
			}
			if (omittedRefusals > 0)
			{
				System.Ledger.Note("{{K|" + omittedRefusals + ((omittedRefusals == 1)
					? " other settler read the notice and left it standing.}}"
					: " other settlers read the notice and left it standing.}}"));
			}
		}

		private static void EnsureAttemptSchedule(GameObject Notice, r_KingdomNotice Data, long NowTick)
		{
			if (Data.ScheduleVersion == 2)
			{
				if (string.IsNullOrEmpty(Data.EventStreamId))
				{
					Data.EventStreamId = KingdomBountyRules.NoticeEventStream((Notice != null) ? Notice.ID : null);
				}
				if (!Data.AttemptScheduleExhausted && Data.NextAttemptTick <= 0L)
				{
					Data.AttemptScheduleExhausted = !KingdomBountyRules.TryAttemptAfter(NowTick,
						Data.PostedTick, out Data.NextAttemptTick);
				}
				return;
			}
			// Legacy Passes are already-consumed outcomes. Start their absolute lane strictly after
			// migration time, retaining Passes only as the audit count; loading cannot reroll a reader.
			Data.EventStreamId = KingdomBountyRules.NoticeEventStream((Notice != null) ? Notice.ID : null);
			Data.AttemptScheduleExhausted = !KingdomBountyRules.TryAttemptAfter(NowTick,
				Data.PostedTick, out Data.NextAttemptTick);
			Data.ScheduleVersion = 2;
		}

		private static void EnsureLifecycleIdentity(GameObject Notice, r_KingdomNotice Data)
		{
			if (string.IsNullOrEmpty(Data.LifecycleId))
			{
				Data.LifecycleId = KingdomBountyRules.NoticeEventId(
					GameObject.Validate(Notice) ? Notice.ID : null);
			}
			else if (!KingdomBountyRules.IsNoticeEventId(Data.LifecycleId))
			{
				Quarantine(Data, "The notice's stable event identity is malformed.");
			}
		}

		private static void ContinuePost(KingdomSystem System, Zone Z, GameObject Notice,
			r_KingdomNotice Data)
		{
			BountyPostPhase phase = (BountyPostPhase)Data.PostPhase;
			if (phase == BountyPostPhase.None || phase == BountyPostPhase.Complete) return;
			Cell expectedCell = BoundCell(Z, Data.PostZoneId, Data.PostCellX, Data.PostCellY);
			if (!NoticeBindingExact(Notice, Data, Z, expectedCell))
			{
				Quarantine(Data, "The posted notice no longer has its exact object, part, cell, and zone binding.");
				return;
			}
			if (!string.IsNullOrEmpty(Data.PileId))
			{
				Cell pileCell = BoundCell(Z, Data.PostZoneId,
					Data.PostPileCellX, Data.PostPileCellY);
				GameObject pile = (Z == null) ? null : Z.FindObjectByID(Data.PileId);
				if (!PileBindingExact(pile, Z, pileCell)
					|| pile.GetStringProperty(FetchMarkProperty) != Notice.ID)
				{
					Quarantine(Data, "The posted fetch notice no longer owns its exact pile mark.");
					return;
				}
			}
			if (phase == BountyPostPhase.Bound)
			{
				if (!KingdomChronicle.RecordOnce(System, EventId(Data, "posted"),
					Data.PostChronicleLine)) return;
				Data.PostPhase = (int)BountyPostPhase.ChronicleDone;
				phase = BountyPostPhase.ChronicleDone;
			}
			if (phase == BountyPostPhase.ChronicleDone)
			{
				if (!DeliverMessage(ref Data.PostMessageState, Data.PostMessageLine)) return;
				Data.PostPhase = (int)BountyPostPhase.MessageSettled;
				phase = BountyPostPhase.MessageSettled;
			}
			if (phase == BountyPostPhase.MessageSettled)
			{
				Data.PostPhase = (int)BountyPostPhase.Complete;
			}
		}

		private static Cell BoundCell(Zone Z, string ZoneId, int X, int Y)
		{
			if (Z == null || string.IsNullOrEmpty(ZoneId) || Z.ZoneID != ZoneId
				|| X < 0 || Y < 0) return null;
			return Z.GetCell(X, Y);
		}

		private static bool NoticeBindingExact(GameObject Notice, r_KingdomNotice Data,
			Zone Z, Cell Cell)
		{
			return GameObject.Validate(Notice) && Data != null && Z != null && Cell != null
				&& Notice.CurrentZone == Z && Notice.CurrentCell == Cell && Cell.ParentZone == Z
				&& ReferenceEquals(Data.ParentObject, Notice)
				&& ReferenceEquals(Notice.GetPart<r_KingdomNotice>(), Data);
		}

		private static bool PileBindingExact(GameObject Pile, Zone Z, Cell Cell)
		{
			return GameObject.Validate(Pile) && Z != null && Cell != null
				&& Pile.CurrentZone == Z && Pile.CurrentCell == Cell && Cell.ParentZone == Z;
		}

		private static bool ClearBoundFetchMark(Zone Z, GameObject Notice,
			r_KingdomNotice Data, string PileId)
		{
			Cell noticeCell = BoundCell(Z, Data.WithdrawZoneId,
				Data.WithdrawCellX, Data.WithdrawCellY);
			if (!NoticeBindingExact(Notice, Data, Z, noticeCell)) return false;
			if (string.IsNullOrEmpty(PileId))
			{
				Data.PileId = null;
				return true;
			}
			Cell pileCell = BoundCell(Z, Data.WithdrawZoneId,
				Data.WithdrawPileCellX, Data.WithdrawPileCellY);
			GameObject pile = (Z == null) ? null : Z.FindObjectByID(PileId);
			if (!PileBindingExact(pile, Z, pileCell)
				|| pile.GetStringProperty(FetchMarkProperty) != Notice.ID) return false;
			pile.RemoveStringProperty(FetchMarkProperty);
			if (!PileBindingExact(pile, Z, pileCell)
				|| !string.IsNullOrEmpty(pile.GetStringProperty(FetchMarkProperty))
				|| !NoticeBindingExact(Notice, Data, Z, noticeCell)) return false;
			Data.PileId = null;
			return true;
		}

		private static bool DeliverMessage(ref int RawState, string Line)
		{
			BountySinkDisposition state = KingdomBountyRules.RecoverUninspectable(
				(BountySinkDisposition)RawState);
			RawState = (int)state;
			if (KingdomBountyRules.SinkSettled(state)) return true;
			if (string.IsNullOrEmpty(Line))
			{
				RawState = (int)BountySinkDisposition.Skipped;
				return true;
			}
			RawState = (int)BountySinkDisposition.Attempting;
			MessageQueue.AddPlayerMessage(Line);
			RawState = (int)BountySinkDisposition.Delivered;
			return true;
		}

		private static bool DeliverLedger(KingdomSystem System, ref int RawState, string Line)
		{
			BountySinkDisposition state = KingdomBountyRules.RecoverUninspectable(
				(BountySinkDisposition)RawState);
			RawState = (int)state;
			if (KingdomBountyRules.SinkSettled(state)) return true;
			if (System == null || string.IsNullOrEmpty(Line))
			{
				RawState = (int)BountySinkDisposition.Skipped;
				return true;
			}
			RawState = (int)BountySinkDisposition.Attempting;
			System.Ledger.Note(Line);
			RawState = (int)BountySinkDisposition.Delivered;
			return true;
		}

		private sealed class CleanupFrame
		{
			internal GameObject Notice;
			internal string NoticeId;
			internal r_KingdomNotice Data;
			internal Zone Zone;
			internal Cell Cell;
		}

		private static bool TryCaptureCleanup(GameObject Notice, r_KingdomNotice Data,
			out CleanupFrame Frame)
		{
			Frame = null;
			if (!GameObject.Validate(Notice) || string.IsNullOrEmpty(Notice.ID) || Data == null
				|| Data.ParentObject != Notice
				|| !ReferenceEquals(Notice.GetPart<r_KingdomNotice>(), Data)) return false;
			Zone zone = Notice.CurrentZone;
			Cell cell = Notice.CurrentCell;
			if ((zone == null) != (cell == null)
				|| (cell != null && cell.ParentZone != zone)) return false;
			Frame = new CleanupFrame
			{
				Notice = Notice,
				NoticeId = Notice.ID,
				Data = Data,
				Zone = zone,
				Cell = cell
			};
			return true;
		}

		private static bool CleanupFinalized(CleanupFrame Frame)
		{
			if (Frame == null || GameObject.Validate(Frame.Notice)) return false;
			GameObject sameId = GameObject.FindByID(Frame.NoticeId);
			return !GameObject.Validate(sameId);
		}

		/// <summary>The only destructive bounty call site. Attempting recovery never enters it.</summary>
		private static bool InvokeCleanupOnce(GameObject Target, bool Silent)
		{
			if (Target == null || !GameObject.Validate(Target)) return true;
			Zone zone = Target.CurrentZone;
			try { return Target.Obliterate(null, Silent); }
			finally { KingdomSurvey.ObserveCurrentTopologyInActive(zone, Target); }
		}

		private static string EventId(r_KingdomNotice Data, string Suffix)
		{
			return (Data?.LifecycleId ?? "taf:bounty:event:v1:unknown") + ":" + Suffix;
		}

		private static void Quarantine(r_KingdomNotice Data, string Reason)
		{
			if (Data == null) return;
			Data.LifecycleQuarantined = true;
			if (string.IsNullOrEmpty(Data.QuarantineReason)) Data.QuarantineReason = Reason;
		}

		private static void TellQuarantine(KingdomSystem System, r_KingdomNotice Data)
		{
			if (System == null || Data == null) return;
			if (Data.QuarantineTold
				&& Data.QuarantineLedgerState == (int)BountySinkDisposition.None
				&& Data.QuarantineMessageState == (int)BountySinkDisposition.None)
			{
				Data.QuarantineLedgerState = (int)BountySinkDisposition.Skipped;
				Data.QuarantineMessageState = (int)BountySinkDisposition.Skipped;
			}
			if (Data.QuarantineTold) return;
			string reason = string.IsNullOrEmpty(Data.QuarantineReason)
				? "A notice receipt is uncertain. It is quarantined; no task, transfer, or payment will be repeated."
				: Data.QuarantineReason;
			DeliverLedger(System, ref Data.QuarantineLedgerState, "{{r|" + reason + "}}");
			DeliverMessage(ref Data.QuarantineMessageState, "{{r|" + reason + "}}");
			Data.QuarantineTold = KingdomBountyRules.SinkSettled(
				(BountySinkDisposition)Data.QuarantineLedgerState)
				&& KingdomBountyRules.SinkSettled(
					(BountySinkDisposition)Data.QuarantineMessageState);
			KingdomLog.Log("bounty: quarantined " + (Data.LifecycleId ?? "unknown")
				+ " reason=" + reason);
		}

		/// <summary>Consumes opportunities nobody could have taken while unattended, without drawing
		/// an outcome from a later roster. False means the notice exhausted its bounded lane.</summary>
		private static bool ConsumeMissedAttempts(r_KingdomNotice Data, long LatestTick, long Skipped)
		{
			if (Skipped <= 0L)
			{
				Data.NextAttemptTick = LatestTick;
				return true;
			}
			long room = (long)KingdomBountyRules.MaxPasses - Data.Passes;
			if (Skipped >= room)
			{
				Data.Passes = KingdomBountyRules.MaxPasses;
				Data.AttemptScheduleExhausted = true;
				Data.NextAttemptTick = 0L;
				return false;
			}
			Data.Passes += (int)Skipped;
			Data.NextAttemptTick = LatestTick;
			KingdomLog.Log("bounty: skipped " + Skipped
				+ " unattended notice opportunities; latest=" + LatestTick);
			return true;
		}

		private static void ConsumeAttempt(r_KingdomNotice Data, long ScheduledTick)
		{
			if (Data.Passes < KingdomBountyRules.MaxPasses)
			{
				Data.Passes++;
			}
			long next;
			if (Data.Passes >= KingdomBountyRules.MaxPasses
				|| !KingdomBountyRules.TryAdvanceAttemptTick(ScheduledTick, out next))
			{
				Data.AttemptScheduleExhausted = true;
				Data.NextAttemptTick = 0L;
				return;
			}
			Data.NextAttemptTick = next;
		}

		private static bool Take(KingdomSystem System, Zone Z, GameObject Notice, r_KingdomNotice Data, BountyTask Task, KingdomBountyRules.BountyAttempt Attempt, long ScheduledTick)
		{
			if ((BountyTakePhase)Data.TakePhase == BountyTakePhase.None)
			{
				Data.PendingAttemptTick = ScheduledTick;
				Data.PendingWorkerName = Attempt.Name;
				Data.PendingWorkerResidentId = ResidentIdFor(System, Attempt.RosterIndex, Attempt.Name);
				Data.PendingVirtueIndex = Attempt.VirtueIndex;
				Data.PendingFlawIndex = Attempt.FlawIndex;
				Data.PendingTasteMatched = Attempt.TasteMatched;
				Data.PendingAttemptConsumed = false;
				Data.TakePhase = (int)BountyTakePhase.Bound;
			}
			ContinueTake(System, Z, Notice, Data);
			return !string.IsNullOrEmpty(Data.WorkerName);
		}

		private static void ContinueTake(KingdomSystem System, Zone Z, GameObject Notice,
			r_KingdomNotice Data)
		{
			BountyTakePhase phase = (BountyTakePhase)Data.TakePhase;
			if (phase == BountyTakePhase.Quarantined || phase == BountyTakePhase.None
				|| phase == BountyTakePhase.Complete) return;
			if (string.IsNullOrEmpty(Data.PendingWorkerName) || Data.PendingAttemptTick < 0L)
			{
				Quarantine(Data, "The notice lost its bound reader before takeover completed.");
				return;
			}
			BountyTask task = (BountyTask)Data.TaskCode;
			bool taskMayStart = false;
			if (phase == BountyTakePhase.Bound)
			{
				Data.TakePhase = (int)BountyTakePhase.TaskIntent;
				phase = BountyTakePhase.TaskIntent;
				taskMayStart = true;
			}
			if (phase == BountyTakePhase.TaskIntent)
			{
				if (task == BountyTask.Clearance && !HasMatchingClearance(Z, Data))
				{
					if (!taskMayStart)
					{
						Quarantine(Data,
							"A clearance takeover crossed an uncertain staking callback seam.");
						Data.TakePhase = (int)BountyTakePhase.Quarantined;
						return;
					}
					string failure;
					if (!KingdomMaterials.StakeClearance(System, Z, Data.X1, Data.Y1,
						Data.X2, Data.Y2, out failure))
					{
						if (!Data.StakeFailedAnnounced)
						{
							Data.StakeFailedAnnounced = true;
						System.Ledger.Note("{{r|" + KingdomPresentation.Rich(Data.PendingWorkerName)
								+ " would have taken the clearance notice, and could not: "
								+ failure + "}}");
						}
						// Clean refusal consumes this scheduled answer but never binds a worker.
						Data.PendingWorkerName = null;
						Data.TakePhase = (int)BountyTakePhase.Complete;
						return;
					}
					Data.StakeFailedAnnounced = false;
				}
				Data.TakePhase = (int)BountyTakePhase.TaskDone;
				phase = BountyTakePhase.TaskDone;
			}
			if (phase == BountyTakePhase.TaskDone)
			{
				Data.WorkerName = Data.PendingWorkerName;
				Data.TakenTick = Data.PendingAttemptTick;
				Data.DueTick = KingdomBountyRules.WorkDueTick(Data.TakenTick,
					KingdomBountyRules.WorkDays(task, Data.Magnitude));
				if (!KingdomChronicle.RecordOnce(System, EventId(Data, "taken"),
					KingdomBountyRules.TakenChronicle(
						KingdomPresentation.Rich(Data.PendingWorkerName), task,
						Data.PendingVirtueIndex, Data.PendingTasteMatched)))
				{
					return;
				}
				Data.TakePhase = (int)BountyTakePhase.ChronicleDone;
				phase = BountyTakePhase.ChronicleDone;
			}
			if (phase == BountyTakePhase.ChronicleDone)
			{
				if (Data.TakeLedgerState == (int)BountySinkDisposition.None)
					Data.TakeLedgerState = (int)BountySinkDisposition.Pending;
				Data.TakePhase = (int)BountyTakePhase.LedgerIntent;
				DeliverLedger(System, ref Data.TakeLedgerState, "{{G|" + KingdomPresentation.Rich(Data.PendingWorkerName)
					+ " took the notice offering water to " + KingdomBountyRules.TaskName(task) + ".}}");
				Data.TakePhase = (int)BountyTakePhase.LedgerDone;
				phase = BountyTakePhase.LedgerDone;
			}
			else if (phase == BountyTakePhase.LedgerIntent)
			{
				if (Data.TakeLedgerState == (int)BountySinkDisposition.None)
					Data.TakeLedgerState = (int)BountySinkDisposition.Attempting;
				Data.TakeLedgerState = (int)KingdomBountyRules.RecoverUninspectable(
					(BountySinkDisposition)Data.TakeLedgerState);
				Data.TakePhase = (int)BountyTakePhase.LedgerDone;
				phase = BountyTakePhase.LedgerDone;
			}
			if (phase == BountyTakePhase.LedgerDone)
			{
				if (Data.TakeMessageState == (int)BountySinkDisposition.None)
					Data.TakeMessageState = (int)BountySinkDisposition.Pending;
				Data.TakePhase = (int)BountyTakePhase.MessageIntent;
				DeliverMessage(ref Data.TakeMessageState, "{{G|" + KingdomPresentation.Rich(Data.PendingWorkerName)
					+ " takes the posted notice.}}");
				Data.TakePhase = (int)BountyTakePhase.MessageDone;
				phase = BountyTakePhase.MessageDone;
			}
			else if (phase == BountyTakePhase.MessageIntent)
			{
				if (Data.TakeMessageState == (int)BountySinkDisposition.None)
					Data.TakeMessageState = (int)BountySinkDisposition.Attempting;
				Data.TakeMessageState = (int)KingdomBountyRules.RecoverUninspectable(
					(BountySinkDisposition)Data.TakeMessageState);
				Data.TakePhase = (int)BountyTakePhase.MessageDone;
				phase = BountyTakePhase.MessageDone;
			}
			if (phase == BountyTakePhase.MessageDone)
			{
				Describe(System, Z, Notice, Data);
				Data.TakePhase = (int)BountyTakePhase.Complete;
				KingdomLog.Log("bounty: taken by " + Data.WorkerName + " resident="
					+ Data.PendingWorkerResidentId + " task=" + KingdomBountyRules.TaskKey(task)
					+ " due=" + Data.DueTick);
			}
		}

		private static void CompleteTakeCursor(r_KingdomNotice Data)
		{
			if (!Data.PendingAttemptConsumed)
			{
				long next;
				if (Data.NextAttemptTick == Data.PendingAttemptTick)
				{
					ConsumeAttempt(Data, Data.PendingAttemptTick);
				}
				else if (KingdomBountyRules.TryAdvanceAttemptTick(Data.PendingAttemptTick, out next)
					&& Data.NextAttemptTick != next && !Data.AttemptScheduleExhausted)
				{
					Quarantine(Data, "The bound reader's schedule cursor no longer matches its event.");
					return;
				}
				Data.PendingAttemptConsumed = true;
			}
			Data.TakePhase = (int)BountyTakePhase.None;
			Data.PendingAttemptTick = 0L;
			Data.PendingWorkerName = null;
		}

		private static int ResidentIdFor(KingdomSystem System, int Index, string Name)
		{
			List<Simulation.City.KingdomResidentRow> rows =
				Simulation.City.KingdomResidents.RollRows(System);
			if (Index < 0 || Index >= rows.Count
				|| !string.Equals(rows[Index].Name, Name, StringComparison.Ordinal))
			{
				return 0;
			}
			return rows[Index].ResidentId;
		}

		private static bool HasMatchingClearance(Zone Z, r_KingdomNotice Data)
		{
			if (Z == null) return false;
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Z);
			IEnumerable<GameObject> clearances = survey != null
				? (IEnumerable<GameObject>)survey.Clearances : KingdomSurvey.ObjectsFor(Z);
			foreach (GameObject item in clearances)
			{
				r_KingdomClearance order = item.GetPart<r_KingdomClearance>();
				if (order != null && order.X1 == Data.X1 && order.Y1 == Data.Y1
					&& order.X2 == Data.X2 && order.Y2 == Data.Y2) return true;
			}
			return false;
		}

		private static void Work(KingdomSystem System, Zone Z, KingdomSurvey Survey, GameObject Notice, r_KingdomNotice Data)
		{
			BountyTask task = (BountyTask)Data.TaskCode;
			long now = The.Game.TimeTicks;
			switch (task)
			{
			case BountyTask.Clearance:
			{
				KingdomMaterials.ClearanceAssessment assessment = KingdomMaterials.Assess(System, Z, Data.X1, Data.Y1, Data.X2, Data.Y2);
				if (!assessment.Valid || assessment.Standing > 0)
				{
					return;
				}
				Finish(System, Z, Survey, Notice, Data, null);
				return;
			}
			case BountyTask.Manning:
			{
				ManOneWork(System, Survey);
				if (now < Data.DueTick)
				{
					return;
				}
				Finish(System, Z, Survey, Notice, Data, null);
				return;
			}
			case BountyTask.Fetch:
			{
				if (now < Data.DueTick)
				{
					return;
				}
				Carry(System, Z, Notice, Data, Survey);
				return;
			}
			case BountyTask.Scouting:
			{
				if (now < Data.DueTick)
				{
					return;
				}
				Scout(System, Z, Survey, Notice, Data);
				return;
			}
			default:
				Finish(System, Z, Survey, Notice, Data, null);
				return;
			}
		}

		private static void Carry(KingdomSystem System, Zone Z, GameObject Notice, r_KingdomNotice Data, KingdomSurvey Survey)
		{
			if ((BountyTransferPhase)Data.TransferPhase == BountyTransferPhase.Quarantined)
			{
				TellQuarantine(System, Data);
				return;
			}
			if ((BountyTransferPhase)Data.TransferPhase != BountyTransferPhase.None
				&& !ContinueTransfer(Z, Notice, Data))
			{
				TellQuarantine(System, Data);
				return;
			}
			GameObject pile = FindPile(Z, Notice, Data);
			if (pile == null && (BountyTransferPhase)Data.TransferPhase == BountyTransferPhase.None)
			{
				Announce(System, Data, BountyBlock.PileEmpty);
				return;
			}
			if (Data.HaulPhase == 1)
			{
				Quarantine(Data, "A porter handoff returned through an uninspectable callback seam.");
				TellQuarantine(System, Data);
				return;
			}
			if (Data.HaulPhase == 3)
			{
				return;
			}
			if (HaulHook != null && Data.HaulPhase == 0)
			{
				Data.HaulPhase = 1;
				if (HaulHook(System, pile, Data.WorkerName, Data.TakenTick))
				{
					Data.HaulPhase = 3;
					return;
				}
				Data.HaulPhase = 2;
			}
			KingdomMaterials.MaterialStock stock = KingdomMaterials.Stock(Z);
			GameObject container = null;
			for (int i = 0; i < stock.Stockpiles.Count; i++)
			{
				if (stock.Stockpiles[i].Inventory != null && stock.Stockpiles[i] != pile)
				{
					container = stock.Stockpiles[i];
					break;
				}
			}
			if (container == null)
			{
				Announce(System, Data, BountyBlock.NowhereToCarry);
				return;
			}
			if (pile == null || pile.Inventory == null)
			{
				Announce(System, Data, BountyBlock.PileEmpty);
				return;
			}
			int guard = pile.Inventory.Objects.Count + 1;
			while (guard-- > 0)
			{
				if ((BountyTransferPhase)Data.TransferPhase == BountyTransferPhase.None)
				{
					GameObject next = null;
					for (int i = 0; i < pile.Inventory.Objects.Count; i++)
					{
						GameObject candidate = pile.Inventory.Objects[i];
						if (GameObject.Validate(candidate) && KingdomMaterials.TryMaterialOf(candidate, out _))
						{
							next = candidate;
							break;
						}
					}
					if (next == null) break;
					Data.TransferItemId = next.ID;
					Data.TransferSourceId = pile.ID;
					Data.TransferDestinationId = container.ID;
					Data.TransferUnits = next.Count;
					Data.TransferTotalBefore = Data.TransferredUnits;
					Data.TransferPhase = (int)BountyTransferPhase.Bound;
				}
				if (!ContinueTransfer(Z, Notice, Data))
				{
					TellQuarantine(System, Data);
					return;
				}
			}
			if ((BountyTransferPhase)Data.TransferPhase != BountyTransferPhase.None)
			{
				return;
			}
			if (Data.TransferredUnits <= 0)
			{
				Announce(System, Data, BountyBlock.PileEmpty);
				return;
			}
			Announce(System, Data, BountyBlock.None);
			int moved = Data.TransferredUnits;
			Finish(System, Z, Survey, Notice, Data, moved
				+ ((moved == 1) ? " load was carried in" : " loads were carried in"));
		}

		private sealed class InventoryFrame
		{
			internal GameObject Owner;
			internal Inventory Part;
			internal List<GameObject> List;
			internal GameObject[] Items;
			internal string[] ItemIds;
			internal int[] Counts;
			internal Zone Zone;
			internal Cell Cell;
			internal string Id;
		}

		private static bool TryCaptureInventory(GameObject Owner, Zone Z,
			out InventoryFrame Frame)
		{
			Frame = null;
			Inventory part = GameObject.Validate(Owner) ? Owner.Inventory : null;
			if (part == null || part.Objects == null || part.ParentObject != Owner
				|| Z == null || Owner.CurrentZone != Z || Owner.CurrentCell == null
				|| Owner.CurrentCell.ParentZone != Z) return false;
			GameObject[] items = part.Objects.ToArray();
			string[] ids = new string[items.Length];
			int[] counts = new int[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				GameObject item = items[i];
				if (!GameObject.Validate(item) || item.Physics == null || item.InInventory != Owner
					|| item.CurrentCell != null || item.Count <= 0
					|| string.IsNullOrEmpty(item.ID)) return false;
				for (int j = 0; j < i; j++) if (ReferenceEquals(items[j], item)) return false;
				ids[i] = item.ID;
				counts[i] = item.Count;
			}
			Frame = new InventoryFrame
			{
				Owner = Owner,
				Part = part,
				List = part.Objects,
				Items = items,
				ItemIds = ids,
				Counts = counts,
				Zone = Z,
				Cell = Owner.CurrentCell,
				Id = Owner.ID
			};
			return true;
		}

		private static bool InventoryHeaderExact(InventoryFrame Frame)
		{
			return Frame != null && GameObject.Validate(Frame.Owner) && Frame.Part != null
				&& Frame.Owner.ID == Frame.Id
				&& Frame.Owner.CurrentZone == Frame.Zone && Frame.Owner.CurrentCell == Frame.Cell
				&& Frame.Cell != null && Frame.Cell.ParentZone == Frame.Zone
				&& Frame.Part.ParentObject == Frame.Owner
				&& ReferenceEquals(Frame.Owner.Inventory, Frame.Part)
				&& ReferenceEquals(Frame.Part.Objects, Frame.List);
		}

		private static bool InventoryOriginalExact(InventoryFrame Frame)
		{
			if (!InventoryHeaderExact(Frame) || Frame.List.Count != Frame.Items.Length) return false;
			for (int i = 0; i < Frame.Items.Length; i++)
			{
				GameObject item = Frame.Items[i];
				if (!ReferenceEquals(Frame.List[i], item) || !GameObject.Validate(item)
					|| item.ID != Frame.ItemIds[i] || item.Count != Frame.Counts[i]
					|| item.InInventory != Frame.Owner
					|| item.CurrentCell != null) return false;
			}
			return true;
		}

		private static bool InventoryMinusExact(InventoryFrame Frame, GameObject Removed,
			int Units)
		{
			if (!InventoryHeaderExact(Frame) || !GameObject.Validate(Removed)
				|| Removed.Count != Units || Removed.InInventory != null
				|| Removed.CurrentCell != null || Frame.List.Contains(Removed)) return false;
			int removedIndex = -1;
			for (int i = 0; i < Frame.Items.Length; i++)
				if (ReferenceEquals(Frame.Items[i], Removed))
				{
					if (removedIndex >= 0) return false;
					removedIndex = i;
				}
			if (removedIndex < 0 || Frame.List.Count != Frame.Items.Length - 1) return false;
			int current = 0;
			for (int i = 0; i < Frame.Items.Length; i++)
			{
				if (i == removedIndex) continue;
				GameObject item = Frame.Items[i];
				if (!ReferenceEquals(Frame.List[current++], item) || !GameObject.Validate(item)
					|| item.ID != Frame.ItemIds[i] || item.Count != Frame.Counts[i]
					|| item.InInventory != Frame.Owner
					|| item.CurrentCell != null) return false;
			}
			return true;
		}

		private static bool InventoryPlusExact(InventoryFrame Frame, GameObject Added,
			int Units)
		{
			if (!InventoryHeaderExact(Frame) || !GameObject.Validate(Added)
				|| Added.Count != Units || Added.InInventory != Frame.Owner
				|| Added.CurrentCell != null || Frame.List.Count != Frame.Items.Length + 1) return false;
			for (int i = 0; i < Frame.Items.Length; i++)
			{
				GameObject item = Frame.Items[i];
				if (!ReferenceEquals(Frame.List[i], item) || !GameObject.Validate(item)
					|| item.ID != Frame.ItemIds[i] || item.Count != Frame.Counts[i]
					|| item.InInventory != Frame.Owner
					|| item.CurrentCell != null) return false;
			}
			return ReferenceEquals(Frame.List[Frame.Items.Length], Added);
		}

		private static bool ContinueTransfer(Zone Z, GameObject Notice, r_KingdomNotice Data)
		{
			if ((BountyTransferPhase)Data.TransferPhase != BountyTransferPhase.Bound)
			{
				Quarantine(Data, "A fetch transfer reloaded after a mutation intent; neither callback was repeated.");
				Data.TransferPhase = (int)BountyTransferPhase.Quarantined;
				return false;
			}
			GameObject item = GameObject.FindByID(Data.TransferItemId);
			GameObject source = GameObject.FindByID(Data.TransferSourceId);
			GameObject destination = GameObject.FindByID(Data.TransferDestinationId);
			Cell noticeCell = (Notice != null) ? Notice.CurrentCell : null;
			InventoryFrame sourceFrame;
			InventoryFrame destinationFrame;
			if (!GameObject.Validate(item) || source == destination || Data.TransferUnits <= 0
				|| item.Count != Data.TransferUnits || !NoticeBindingExact(Notice, Data, Z, noticeCell)
				|| !TryCaptureInventory(source, Z, out sourceFrame)
				|| !TryCaptureInventory(destination, Z, out destinationFrame)
				|| !sourceFrame.List.Contains(item) || item.InInventory != source
				|| destinationFrame.List.Contains(item))
			{
				Quarantine(Data, "A bound fetch item or container can no longer be proved.");
				Data.TransferPhase = (int)BountyTransferPhase.Quarantined;
				return false;
			}
			string itemId = Data.TransferItemId;
			string sourceId = Data.TransferSourceId;
			string destinationId = Data.TransferDestinationId;
			int units = Data.TransferUnits;
			int totalBefore = Data.TransferTotalBefore;
			int creditedBefore = Data.TransferredUnits;
			if (totalBefore != creditedBefore)
			{
				Quarantine(Data, "A bound fetch transfer no longer matches its credited total.");
				Data.TransferPhase = (int)BountyTransferPhase.Quarantined;
				return false;
			}
			Data.TransferPhase = (int)BountyTransferPhase.RemoveIntent;
			try
			{
				sourceFrame.Part.RemoveObject(item);
			}
			catch (Exception error)
			{
				MetricsManager.LogError("ThousandAndFirst bounty fetch removal", error);
			}
			KingdomSurvey.ObserveCurrentTopologyInActive(Z, sourceFrame.Owner);
			if (!TransferReceiptExact(Data, BountyTransferPhase.RemoveIntent, itemId,
				sourceId, destinationId, units, totalBefore, creditedBefore)
				|| item.ID != itemId
				|| !NoticeBindingExact(Notice, Data, Z, noticeCell)
				|| !InventoryMinusExact(sourceFrame, item, units)
				|| !InventoryOriginalExact(destinationFrame))
			{
				Quarantine(Data, "The fetch removal callback changed an exact item, inventory, list, owner, cell, zone, notice, or count witness.");
				Data.TransferPhase = (int)BountyTransferPhase.Quarantined;
				return false;
			}
			Data.TransferPhase = (int)BountyTransferPhase.Detached;
			Data.TransferPhase = (int)BountyTransferPhase.AddIntent;
			GameObject accepted = null;
			try
			{
				accepted = destinationFrame.Part.AddObject(item, Silent: true, NoStack: true);
			}
			catch (Exception error)
			{
				MetricsManager.LogError("ThousandAndFirst bounty fetch addition", error);
			}
			KingdomSurvey.ObserveCurrentTopologyInActive(Z, destinationFrame.Owner);
			KingdomSurvey.ObserveAddResultInActive(Z, item, accepted);
			if (!TransferReceiptExact(Data, BountyTransferPhase.AddIntent, itemId,
				sourceId, destinationId, units, totalBefore, creditedBefore)
				|| item.ID != itemId
				|| !NoticeBindingExact(Notice, Data, Z, noticeCell)
				|| !InventoryMinusExact(sourceFrame, item, units)
				|| !InventoryPlusExact(destinationFrame, item, units))
			{
				Quarantine(Data, "The fetch addition callback changed an exact item, inventory, list, owner, cell, zone, notice, or count witness.");
				Data.TransferPhase = (int)BountyTransferPhase.Quarantined;
				return false;
			}
			Data.TransferPhase = (int)BountyTransferPhase.Arrived;
			Data.TransferredUnits = totalBefore + units;
			Data.TransferPhase = (int)BountyTransferPhase.None;
			Data.TransferItemId = null;
			Data.TransferSourceId = null;
			Data.TransferDestinationId = null;
			Data.TransferUnits = 0;
			return true;
		}

		private static bool TransferReceiptExact(r_KingdomNotice Data,
			BountyTransferPhase Phase, string ItemId, string SourceId, string DestinationId,
			int Units, int TotalBefore, int CreditedBefore)
		{
			return Data != null && (BountyTransferPhase)Data.TransferPhase == Phase
				&& Data.TransferItemId == ItemId && Data.TransferSourceId == SourceId
				&& Data.TransferDestinationId == DestinationId && Data.TransferUnits == Units
				&& Data.TransferTotalBefore == TotalBefore
				&& Data.TransferredUnits == CreditedBefore;
		}

		private static void Scout(KingdomSystem System, Zone Z, KingdomSurvey Survey, GameObject Notice, r_KingdomNotice Data)
		{
			if (Data.ScoutPhase == 0)
			{
				List<string> frontier = Frontier(System);
				if (frontier.Count == 0)
				{
					Announce(System, Data, BountyBlock.NoFrontier);
					return;
				}
				string settlementId = KingdomChronicle.SettlementId(System);
				if (!KingdomIdentityRules.IsSettlementId(settlementId)) return;
				int index;
				if (!KingdomBountyRules.TryPickFrontier(settlementId,
					Data.PostedTick, Data.Passes, frontier.Count, out index)) return;
				if (index < 0 || index >= frontier.Count) index = 0;
				Data.ScoutZoneId = frontier[index];
				Data.ScoutPhase = 1;
			}
			if (Data.ScoutPhase == 1 && string.IsNullOrEmpty(Data.ScoutGround))
			{
				string ground = null;
				KingdomSystem.Guard("bounty: name bound frontier", delegate
				{
					ground = The.ZoneManager.GetZoneDisplayName(Data.ScoutZoneId,
						WithIndefiniteArticle: true);
				});
				Data.ScoutGround = ground ?? "";
			}
			if (Data.ScoutPhase == 1)
			{
				if (!KingdomChronicle.RecordOnce(System, EventId(Data, "scout"),
					KingdomBountyRules.ScoutChronicle(
						KingdomPresentation.Rich(Data.WorkerName),
						KingdomPresentation.Rich(System.SeatName),
						KingdomPresentation.Rich(Data.ScoutGround)))) return;
				Data.ScoutPhase = 2;
			}
			if (Data.ScoutPhase == 2)
			{
				if (Data.ScoutDeedState == (int)BountySinkDisposition.None)
					Data.ScoutDeedState = (int)BountySinkDisposition.Pending;
				Data.ScoutPhase = 3;
				Data.ScoutDeedState = (int)BountySinkDisposition.Attempting;
				System.RecordDeed(KingdomBountyRules.ScoutDeed(
					KingdomPresentation.Rich(System.SeatName)));
				Data.ScoutDeedState = (int)BountySinkDisposition.Delivered;
				Data.ScoutPhase = 4;
			}
			else if (Data.ScoutPhase == 3)
			{
				if (Data.ScoutDeedState == (int)BountySinkDisposition.None)
					Data.ScoutDeedState = (int)BountySinkDisposition.Attempting;
				Data.ScoutDeedState = (int)KingdomBountyRules.RecoverUninspectable(
					(BountySinkDisposition)Data.ScoutDeedState);
				Data.ScoutPhase = 4;
			}
			if (Data.ScoutPhase != 4 && Data.ScoutPhase != 5) return;
			Data.ScoutPhase = 5;
			Announce(System, Data, BountyBlock.None);
			Finish(System, Z, Survey, Notice, Data, string.IsNullOrEmpty(Data.ScoutGround)
				? "the frontier was walked"
				: ("the frontier was walked, and " + Data.ScoutGround + " lies past it"));
		}

		private static void ManOneWork(KingdomSystem System, KingdomSurvey Survey)
		{
			for (int i = 0; i < Survey.Works.Count; i++)
			{
				GameObject work = Survey.Works[i];
				if (work.GetIntProperty("KingdomEffectiveness") > 0)
				{
					continue;
				}
				work.SetIntProperty("KingdomStaffed", 1);
				work.SetIntProperty("KingdomEffectiveness", 100);
				// The hired hand is not a witnessed resident identity. Neutral is the only
				// honest factor; retaining yesterday's crew would lend their culture to a stranger.
				work.SetIntProperty(KingdomCrews.IdentityAffinityProperty,
					KingdomIdentityAffinityRules.NeutralPercent);
				if (System.IdleWorks > 0)
				{
					System.IdleWorks--;
				}
				if (System.IdleWorks == 0)
				{
					System.IdleWorksAnnounced = false;
				}
				return;
			}
		}

		/// <summary>Marks the work finished and tries to pay for it in the same breath.</summary>
		private static void Finish(KingdomSystem System, Zone Z, KingdomSurvey Survey, GameObject Notice, r_KingdomNotice Data, string Extra)
		{
			if (Data.CompletionPhase == 0)
			{
				Data.CompletionExtra = Extra;
				Data.CompletionPhase = 1;
				Data.Done = true;
			}
			ContinueFinish(System, Z, Survey, Notice, Data);
		}

		private static void ContinueFinish(KingdomSystem System, Zone Z, KingdomSurvey Survey,
			GameObject Notice, r_KingdomNotice Data)
		{
			if (Data.CompletionPhase == 1)
			{
				ClearFetchMark(Z, Notice, Data);
				if (string.IsNullOrEmpty(Data.CompletionExtra))
				{
					Data.CompletionLedgerState = (int)BountySinkDisposition.Skipped;
					Data.CompletionPhase = 3;
				}
				else
				{
					if (Data.CompletionLedgerState == (int)BountySinkDisposition.None)
						Data.CompletionLedgerState = (int)BountySinkDisposition.Pending;
					Data.CompletionPhase = 2;
					DeliverLedger(System, ref Data.CompletionLedgerState,
						"{{G|" + Data.CompletionExtra.Capitalize() + ".}}");
					Data.CompletionPhase = 3;
				}
			}
			else if (Data.CompletionPhase == 2)
			{
				if (Data.CompletionLedgerState == (int)BountySinkDisposition.None)
					Data.CompletionLedgerState = (int)BountySinkDisposition.Attempting;
				Data.CompletionLedgerState = (int)KingdomBountyRules.RecoverUninspectable(
					(BountySinkDisposition)Data.CompletionLedgerState);
				Data.CompletionPhase = 3;
			}
			if (Data.CompletionPhase == 3)
			{
				Data.CompletionPhase = 4;
			}
			if (Data.CompletionPhase == 4) Settle(System, Z, Survey, Notice, Data);
		}

		private static void Settle(KingdomSystem System, Zone Z, KingdomSurvey Survey, GameObject Notice, r_KingdomNotice Data)
		{
			if ((BountyPaymentPhase)Data.PaymentPhase == BountyPaymentPhase.Quarantined)
			{
				TellQuarantine(System, Data);
				return;
			}
			int owed = Data.Price - Data.Paid;
			if (owed > 0)
			{
				if (!ContinuePayment(System, Z, Survey, Notice, Data, owed))
				{
					if (Data.LifecycleQuarantined)
					{
						TellQuarantine(System, Data);
						return;
					}
					if (Data.AnnouncedBlock != (int)BountyBlock.StoresCannotPay)
					{
						KingdomChronicle.RecordOnce(System, EventId(Data, "owed"),
							KingdomBountyRules.OwedChronicle(
								KingdomPresentation.Rich(Data.WorkerName),
								KingdomPresentation.Rich(System.SeatName),
								Data.Paid, Data.Price - Data.Paid));
					}
					Announce(System, Data, BountyBlock.StoresCannotPay);
					Describe(System, Z, Notice, Data);
					return;
				}
				owed = Data.Price - Data.Paid;
			}
			if (owed > 0) return;
			ContinueTerminal(System, Notice, Data);
		}

		private static bool ContinuePayment(KingdomSystem System, Zone Z, KingdomSurvey Survey,
			GameObject Notice, r_KingdomNotice Data, int Owed)
		{
			if ((BountyPaymentPhase)Data.PaymentPhase == BountyPaymentPhase.Credited)
			{
				if (Data.Paid >= Data.Price) return true;
				ResetPaymentReceipt(Data);
			}
			BountyPaymentPhase startingPhase = (BountyPaymentPhase)Data.PaymentPhase;
			if (startingPhase == BountyPaymentPhase.DebitIntent
				|| startingPhase == BountyPaymentPhase.Debited)
			{
				QuarantinePayment(Data, 0,
					"A payout reloaded after debit intent; physical shape cannot authorize credit or another draw.");
				return false;
			}
			if ((BountyPaymentPhase)Data.PaymentPhase == BountyPaymentPhase.None)
			{
				int request = (Survey.StoredWater < Owed) ? Survey.StoredWater : Owed;
				if (request <= 0) return false;
				string ids;
				string originals;
				string capacities;
				string allocations;
				if (!TryPaymentPlan(Survey, Z, request, out ids, out originals,
					out capacities, out allocations)) return false;
				Data.PaymentAmount = request;
				Data.PaymentPaidBefore = Data.Paid;
				Data.PaymentProved = 0;
				Data.PaymentZoneId = Z.ZoneID;
				Data.PaymentVesselIds = ids;
				Data.PaymentOriginalVolumes = originals;
				Data.PaymentMaxVolumes = capacities;
				Data.PaymentAllocations = allocations;
				Data.PaymentPhase = (int)BountyPaymentPhase.Bound;
			}
			BountyPaymentObservation observation;
			int proved;
			if (!ObserveBoundPayment(Data, Z, out observation, out proved))
			{
				QuarantinePayment(Data, 0, "The payout receipt cannot be decoded or rebound to its vessels.");
				return false;
			}
			BountyPaymentAction action = KingdomBountyRules.PaymentAction(
				(BountyPaymentPhase)Data.PaymentPhase, observation);
			if (action == BountyPaymentAction.Debit)
			{
				string ids;
				string originals;
				string capacities;
				string allocations;
				if (!TryPaymentPlan(Survey, Z, Data.PaymentAmount, out ids, out originals,
						out capacities, out allocations)
					|| ids != Data.PaymentVesselIds || originals != Data.PaymentOriginalVolumes
					|| capacities != Data.PaymentMaxVolumes || allocations != Data.PaymentAllocations)
				{
					QuarantinePayment(Data, proved,
						"The stores changed before the bound payout could begin.");
					return false;
				}
				PaymentFrame frame;
				if (!TryCaptureBoundPayment(Data, Z, Survey, Notice, out frame))
				{
					QuarantinePayment(Data, 0,
						"The exact payout vessels could not be captured before debit.");
					return false;
				}
				KingdomWaterDebit debit = Survey.ReserveExactWater(Data.PaymentAmount);
				Data.PaymentPhase = (int)BountyPaymentPhase.DebitIntent;
				bool committed = debit.Commit();
				BountyPaymentObservation after;
				int afterProved;
				if (!ObserveCapturedPayment(frame, out after, out afterProved))
				{
					QuarantinePayment(Data, 0,
						"The payout callback changed an exact notice, owner, vessel, dictionary, stores-list, cell, zone, capacity, or receipt witness.");
				}
				else if (afterProved > 0 && !ReconcilePaymentCounters(frame, committed, afterProved))
				{
					QuarantinePayment(Data, 0,
						"The payout's exact physical delta did not match its survey-counter transition.");
				}
				else if (afterProved > 0)
				{
					Data.PaymentProved = afterProved;
					Data.PaymentPhase = (int)BountyPaymentPhase.Debited;
					long paid = (long)Data.PaymentPaidBefore + afterProved;
					Data.Paid = (paid > Data.Price) ? Data.Price : (int)paid;
					if (after == BountyPaymentObservation.Debited
						&& afterProved == Data.PaymentAmount)
					{
						Data.PaymentPhase = (int)BountyPaymentPhase.Credited;
						return Data.Paid >= Data.Price;
					}
					Data.PaymentPhase = (int)BountyPaymentPhase.Quarantined;
					Quarantine(Data,
						"Only part of the exact live payout remained; that proved amount was credited and the rest was not retried.");
				}
				else QuarantinePayment(Data, 0,
					"The live payout attempt left no proved debit and was quarantined rather than retried.");
				return false;
			}
			if (action == BountyPaymentAction.Quarantine)
			{
				QuarantinePayment(Data, 0,
					"The bound payout is physically ambiguous; no further water will be drawn.");
			}
			return false;
		}

		private static bool TryPaymentPlan(KingdomSurvey Survey, Zone Z, int Amount, out string Ids,
			out string Originals, out string Capacities, out string Allocations)
		{
			Ids = Originals = Capacities = Allocations = null;
			if (Survey == null || Z == null || Amount <= 0) return false;
			int count = Survey.Stores.Count;
			int[] volumes = new int[count];
			bool[] pure = new bool[count];
			bool[] dedicated = new bool[count];
			GameObject[] owners = new GameObject[count];
			for (int i = 0; i < count; i++)
			{
				LiquidVolume vessel = Survey.Stores[i];
				bool duplicate = false;
				for (int j = 0; j < i; j++) if (ReferenceEquals(Survey.Stores[j], vessel)) duplicate = true;
				if (vessel == null || duplicate) continue;
				GameObject owner = vessel.ParentObject;
				owners[i] = owner;
				volumes[i] = vessel.Volume;
				pure[i] = KingdomLiquids.HasFreshWater(vessel);
				dedicated[i] = GameObject.Validate(owner) && owner.GetIntProperty("KingdomStores") == 1
					&& owner.CurrentZone == Z
					&& ReferenceEquals(owner.GetPart<LiquidVolume>(), vessel)
					&& vessel.ParentObject == owner;
			}
			int[] plan;
			int total;
			KingdomWaterDebitFault fault;
			if (!KingdomWaterDebitRules.TryPlan(Amount, volumes, pure, dedicated,
				out plan, out total, out fault) || total != Amount) return false;
			List<string> ids = new List<string>();
			List<int> original = new List<int>();
			List<int> capacity = new List<int>();
			List<int> allocated = new List<int>();
			for (int i = 0; i < plan.Length; i++)
			{
				if (plan[i] <= 0) continue;
				if (!GameObject.Validate(owners[i]) || owners[i].ID.IndexOf('|') >= 0
					|| owners[i].ID.Length > KingdomBountyRules.MaxObjectIdChars
					|| ids.Count >= KingdomBountyRules.MaxPaymentRows) return false;
				ids.Add(owners[i].ID);
				original.Add(volumes[i]);
				capacity.Add(Survey.Stores[i].MaxVolume);
				allocated.Add(plan[i]);
			}
			Ids = string.Join("|", ids.ToArray());
			Originals = JoinInts(original);
			Capacities = JoinInts(capacity);
			Allocations = JoinInts(allocated);
			return ids.Count > 0 && Ids.Length <= KingdomBountyRules.MaxPaymentRowsChars
				&& Originals.Length <= KingdomBountyRules.MaxPaymentRowsChars
				&& Capacities.Length <= KingdomBountyRules.MaxPaymentRowsChars
				&& Allocations.Length <= KingdomBountyRules.MaxPaymentRowsChars;
		}

		private static bool ObserveBoundPayment(r_KingdomNotice Data, Zone Z,
			out BountyPaymentObservation Observation, out int Proved)
		{
			Observation = BountyPaymentObservation.Malformed;
			Proved = 0;
			string[] ids;
			int[] original;
			int[] capacity;
			int[] allocation;
			if (!KingdomBountyRules.TryObjectIdRows(Data.PaymentVesselIds, out ids)
				|| !TryInts(Data.PaymentOriginalVolumes, out original)
				|| !TryInts(Data.PaymentMaxVolumes, out capacity)
				|| !TryInts(Data.PaymentAllocations, out allocation)
				|| ids.Length == 0 || ids.Length != original.Length
				|| ids.Length != capacity.Length || ids.Length != allocation.Length) return false;
			int[] current = new int[ids.Length];
			bool[] same = new bool[ids.Length];
			bool[] pure = new bool[ids.Length];
			for (int i = 0; i < ids.Length; i++)
			{
				GameObject owner = GameObject.FindByID(ids[i]);
				LiquidVolume vessel = GameObject.Validate(owner) ? owner.GetPart<LiquidVolume>() : null;
				same[i] = vessel != null && vessel.ParentObject == owner
					&& Z != null && Data.PaymentZoneId == Z.ZoneID && owner.CurrentZone == Z
					&& vessel.MaxVolume == capacity[i]
					&& owner.GetIntProperty("KingdomStores") == 1;
				current[i] = (vessel == null) ? -1 : vessel.Volume;
				pure[i] = vessel != null && (vessel.Volume == 0 || vessel.IsFreshWater());
			}
			Observation = KingdomBountyRules.ObservePayment(Data.PaymentAmount,
				original, current, allocation, same, pure, out Proved);
			return Observation != BountyPaymentObservation.Malformed;
		}

		private sealed class PaymentFrame
		{
			internal r_KingdomNotice Data;
			internal GameObject Notice;
			internal Zone Zone;
			internal Cell NoticeCell;
			internal KingdomSurvey Survey;
			internal List<LiquidVolume> Stores;
			internal LiquidVolume[] StoreRows;
			internal int StoredWater;
			internal int StorageSpace;
			internal string VesselIds;
			internal string OriginalVolumes;
			internal string MaxVolumes;
			internal string AllocationsText;
			internal int Amount;
			internal int PaidBefore;
			internal GameObject[] Owners;
			internal string[] OwnerIds;
			internal Cell[] OwnerCells;
			internal LiquidVolume[] Vessels;
			internal Dictionary<string, int>[] Dictionaries;
			internal Dictionary<string, int>[] Components;
			internal int[] Originals;
			internal int[] Capacities;
			internal int[] Allocations;
		}

		private static bool TryCaptureBoundPayment(r_KingdomNotice Data, Zone Z,
			KingdomSurvey Survey, GameObject Notice, out PaymentFrame Frame)
		{
			Frame = null;
			string[] ids;
			int[] originals;
			int[] capacities;
			int[] allocations;
			Cell noticeCell = (Notice != null) ? Notice.CurrentCell : null;
			if (Survey == null || Survey.Stores == null || Z == null || Data == null
				|| Data.PaymentZoneId != Z.ZoneID
				|| !NoticeBindingExact(Notice, Data, Z, noticeCell)
				|| !KingdomBountyRules.TryObjectIdRows(Data.PaymentVesselIds, out ids)
				|| !TryInts(Data.PaymentOriginalVolumes, out originals)
				|| !TryInts(Data.PaymentMaxVolumes, out capacities)
				|| !TryInts(Data.PaymentAllocations, out allocations)
				|| ids.Length == 0 || ids.Length != originals.Length
				|| ids.Length != capacities.Length || ids.Length != allocations.Length) return false;
			Frame = new PaymentFrame
			{
				Data = Data,
				Notice = Notice,
				Zone = Z,
				NoticeCell = noticeCell,
				Survey = Survey,
				Stores = Survey.Stores,
				StoreRows = Survey.Stores.ToArray(),
				StoredWater = Survey.StoredWater,
				StorageSpace = Survey.StorageSpace,
				VesselIds = Data.PaymentVesselIds,
				OriginalVolumes = Data.PaymentOriginalVolumes,
				MaxVolumes = Data.PaymentMaxVolumes,
				AllocationsText = Data.PaymentAllocations,
				Amount = Data.PaymentAmount,
				PaidBefore = Data.PaymentPaidBefore,
				Owners = new GameObject[ids.Length],
				OwnerIds = ids,
				OwnerCells = new Cell[ids.Length],
				Vessels = new LiquidVolume[ids.Length],
				Dictionaries = new Dictionary<string, int>[ids.Length],
				Components = new Dictionary<string, int>[ids.Length],
				Originals = originals,
				Capacities = capacities,
				Allocations = allocations
			};
			for (int i = 0; i < ids.Length; i++)
			{
				GameObject owner = GameObject.FindByID(ids[i]);
				LiquidVolume vessel = GameObject.Validate(owner) ? owner.GetPart<LiquidVolume>() : null;
				if (vessel == null || owner.ID != ids[i] || owner.CurrentZone != Z
					|| owner.CurrentCell == null || owner.CurrentCell.ParentZone != Z
					|| vessel.ParentObject != owner
					|| !ReferenceEquals(owner.GetPart<LiquidVolume>(), vessel)
					|| !Survey.Stores.Contains(vessel)
					|| owner.GetIntProperty("KingdomStores") != 1
					|| vessel.ComponentLiquids == null
					|| vessel.MaxVolume != capacities[i] || vessel.Volume != originals[i]
					|| !vessel.IsFreshWater() || allocations[i] <= 0
					|| allocations[i] > originals[i]) return false;
				Frame.Owners[i] = owner;
				Frame.OwnerCells[i] = owner.CurrentCell;
				Frame.Vessels[i] = vessel;
				Frame.Dictionaries[i] = vessel.ComponentLiquids;
				Frame.Components[i] = new Dictionary<string, int>(vessel.ComponentLiquids);
			}
			return true;
		}

		private static bool ObserveCapturedPayment(PaymentFrame Frame,
			out BountyPaymentObservation Observation, out int Proved)
		{
			Observation = BountyPaymentObservation.Uncertain;
			Proved = 0;
			if (Frame == null || Frame.Data == null || Frame.Survey == null
				|| !ReferenceEquals(Frame.Survey.Stores, Frame.Stores)
				|| Frame.Stores.Count != Frame.StoreRows.Length
				|| !NoticeBindingExact(Frame.Notice, Frame.Data, Frame.Zone, Frame.NoticeCell)
				|| (BountyPaymentPhase)Frame.Data.PaymentPhase != BountyPaymentPhase.DebitIntent
				|| Frame.Data.PaymentAmount != Frame.Amount
				|| Frame.Data.PaymentPaidBefore != Frame.PaidBefore
				|| Frame.Data.PaymentVesselIds != Frame.VesselIds
				|| Frame.Data.PaymentOriginalVolumes != Frame.OriginalVolumes
				|| Frame.Data.PaymentMaxVolumes != Frame.MaxVolumes
				|| Frame.Data.PaymentAllocations != Frame.AllocationsText) return false;
			for (int i = 0; i < Frame.StoreRows.Length; i++)
				if (!ReferenceEquals(Frame.Stores[i], Frame.StoreRows[i])) return false;
			int[] current = new int[Frame.Owners.Length];
			bool[] same = new bool[Frame.Owners.Length];
			bool[] pure = new bool[Frame.Owners.Length];
			for (int i = 0; i < Frame.Owners.Length; i++)
			{
				GameObject owner = Frame.Owners[i];
				LiquidVolume vessel = Frame.Vessels[i];
				same[i] = GameObject.Validate(owner) && owner.ID == Frame.OwnerIds[i]
					&& owner.CurrentZone == Frame.Zone && owner.CurrentCell == Frame.OwnerCells[i]
					&& Frame.OwnerCells[i] != null && Frame.OwnerCells[i].ParentZone == Frame.Zone
					&& vessel != null && vessel.ParentObject == owner
					&& ReferenceEquals(owner.GetPart<LiquidVolume>(), vessel)
					&& owner.GetIntProperty("KingdomStores") == 1
					&& vessel.MaxVolume == Frame.Capacities[i]
					&& ReferenceEquals(vessel.ComponentLiquids, Frame.Dictionaries[i])
					&& ComponentsExact(vessel.ComponentLiquids, Frame.Components[i]);
				current[i] = (vessel == null) ? -1 : vessel.Volume;
				pure[i] = vessel != null && (vessel.Volume == 0 || vessel.IsFreshWater());
			}
			Observation = KingdomBountyRules.ObservePayment(Frame.Amount,
				Frame.Originals, current, Frame.Allocations, same, pure, out Proved);
			return Observation != BountyPaymentObservation.Malformed
				&& Observation != BountyPaymentObservation.Uncertain;
		}

		private static bool ComponentsExact(Dictionary<string, int> Current,
			Dictionary<string, int> Expected)
		{
			if (Current == null || Expected == null || Current.Count != Expected.Count) return false;
			foreach (KeyValuePair<string, int> pair in Expected)
			{
				int value;
				if (!Current.TryGetValue(pair.Key, out value) || value != pair.Value) return false;
			}
			return true;
		}

		private static bool ReconcilePaymentCounters(PaymentFrame Frame, bool Committed,
			int Proved)
		{
			if (Frame == null || Proved <= 0 || Proved > Frame.Amount
				|| Frame.StoredWater < Proved || Frame.StorageSpace < 0) return false;
			int expectedStored = Frame.StoredWater - Proved;
			int expectedSpace;
			try { expectedSpace = checked(Frame.StorageSpace + Proved); }
			catch (OverflowException) { return false; }
			if (Frame.Survey.StoredWater == expectedStored
				&& Frame.Survey.StorageSpace == expectedSpace) return true;
			if (Committed || Frame.Survey.StoredWater != Frame.StoredWater
				|| Frame.Survey.StorageSpace != Frame.StorageSpace) return false;
			Frame.Survey.StoredWater = expectedStored;
			Frame.Survey.StorageSpace = expectedSpace;
			return true;
		}

		private static string JoinInts(List<int> Values)
		{
			string[] rows = new string[Values.Count];
			for (int i = 0; i < Values.Count; i++) rows[i] = Values[i].ToString(
				global::System.Globalization.CultureInfo.InvariantCulture);
			return string.Join("|", rows);
		}

		private static bool TryInts(string Text, out int[] Values)
		{
			return KingdomBountyRules.TryCanonicalIntRows(Text, out Values);
		}

		private static void QuarantinePayment(r_KingdomNotice Data, int Proved, string Reason)
		{
			Data.PaymentProved = (Proved > 0) ? Proved : 0;
			long paid = (long)Data.PaymentPaidBefore + Data.PaymentProved;
			Data.Paid = (paid > Data.Price) ? Data.Price : (int)paid;
			Data.PaymentPhase = (int)BountyPaymentPhase.Quarantined;
			Quarantine(Data, Reason);
		}

		private static void ResetPaymentReceipt(r_KingdomNotice Data)
		{
			Data.PaymentPhase = (int)BountyPaymentPhase.None;
			Data.PaymentAmount = 0;
			Data.PaymentPaidBefore = Data.Paid;
			Data.PaymentProved = 0;
			Data.PaymentZoneId = null;
			Data.PaymentVesselIds = null;
			Data.PaymentOriginalVolumes = null;
			Data.PaymentMaxVolumes = null;
			Data.PaymentAllocations = null;
		}

		private static void ContinueTerminal(KingdomSystem System, GameObject Notice,
			r_KingdomNotice Data)
		{
			BountyTerminalPhase phase = (BountyTerminalPhase)Data.TerminalPhase;
			if (phase == BountyTerminalPhase.None)
			{
				if (!KingdomChronicle.RecordOnce(System, EventId(Data, "paid"),
					KingdomBountyRules.PaidChronicle(
						KingdomPresentation.Rich(Data.WorkerName),
						KingdomPresentation.Rich(System.SeatName),
						(BountyTask)Data.TaskCode, Data.Paid))) return;
				Data.TerminalPhase = (int)BountyTerminalPhase.ChronicleDone;
				phase = BountyTerminalPhase.ChronicleDone;
			}
			if (phase == BountyTerminalPhase.ChronicleDone)
			{
				if (Data.TerminalLedgerState == (int)BountySinkDisposition.None)
					Data.TerminalLedgerState = (int)BountySinkDisposition.Pending;
				Data.TerminalPhase = (int)BountyTerminalPhase.LedgerIntent;
				DeliverLedger(System, ref Data.TerminalLedgerState,
					"{{G|" + KingdomPresentation.Rich(Data.WorkerName) + " was paid " + Data.Paid
					+ ((Data.Paid == 1) ? " dram" : " drams") + " off the notice board.}}");
				Data.TerminalPhase = (int)BountyTerminalPhase.LedgerDone;
				phase = BountyTerminalPhase.LedgerDone;
			}
			else if (phase == BountyTerminalPhase.LedgerIntent)
			{
				if (Data.TerminalLedgerState == (int)BountySinkDisposition.None)
					Data.TerminalLedgerState = (int)BountySinkDisposition.Attempting;
				Data.TerminalLedgerState = (int)KingdomBountyRules.RecoverUninspectable(
					(BountySinkDisposition)Data.TerminalLedgerState);
				Data.TerminalPhase = (int)BountyTerminalPhase.LedgerDone;
				phase = BountyTerminalPhase.LedgerDone;
			}
			if (phase == BountyTerminalPhase.LedgerDone)
			{
				if (Data.TerminalMessageState == (int)BountySinkDisposition.None)
					Data.TerminalMessageState = (int)BountySinkDisposition.Pending;
				Data.TerminalPhase = (int)BountyTerminalPhase.MessageIntent;
				DeliverMessage(ref Data.TerminalMessageState,
					"{{G|The notice is claimed and paid.}} "
					+ Data.Paid + ((Data.Paid == 1) ? " dram goes" : " drams go")
					+ " to " + KingdomPresentation.Rich(Data.WorkerName) + ".");
				Data.TerminalPhase = (int)BountyTerminalPhase.MessageDone;
				phase = BountyTerminalPhase.MessageDone;
			}
			else if (phase == BountyTerminalPhase.MessageIntent)
			{
				if (Data.TerminalMessageState == (int)BountySinkDisposition.None)
					Data.TerminalMessageState = (int)BountySinkDisposition.Attempting;
				Data.TerminalMessageState = (int)KingdomBountyRules.RecoverUninspectable(
					(BountySinkDisposition)Data.TerminalMessageState);
				Data.TerminalPhase = (int)BountyTerminalPhase.MessageDone;
				phase = BountyTerminalPhase.MessageDone;
			}
			if (phase == BountyTerminalPhase.MessageDone)
			{
				CleanupFrame cleanup;
				if (!TryCaptureCleanup(Notice, Data, out cleanup))
				{
					Data.TerminalPhase = (int)BountyTerminalPhase.CleanupLost;
					Quarantine(Data,
						"Paid-notice cleanup could not capture its exact notice and data-part identity.");
					return;
				}
				Data.TerminalPhase = (int)BountyTerminalPhase.CleanupAttempting;
				KingdomLog.Log("bounty: paid " + Data.Paid + " to " + Data.WorkerName
					+ " task=" + KingdomBountyRules.TaskKey((BountyTask)Data.TaskCode));
				InvokeCleanupOnce(Notice, false);
				if (!CleanupFinalized(cleanup))
				{
					Data.TerminalPhase = (int)BountyTerminalPhase.CleanupLost;
					Quarantine(Data, "Paid-notice cleanup was vetoed or changed; its destructive callback was not repeated.");
				}
			}
			else if (phase == BountyTerminalPhase.CleanupAttempting)
			{
				Data.TerminalPhase = (int)BountyTerminalPhase.CleanupLost;
				Quarantine(Data, "Paid-notice cleanup was interrupted; its destructive callback was not repeated.");
			}
		}

		// ==================================================================================
		// Saying why, once
		// ==================================================================================

		private static BountyBlock Blocking(KingdomSystem System, Zone Z, KingdomSurvey Survey, r_KingdomNotice Data)
		{
			if (Simulation.City.KingdomResidents.OnRollCount(System) == 0)
			{
				return BountyBlock.NobodyToTry;
			}
			switch ((BountyTask)Data.TaskCode)
			{
			case BountyTask.Clearance:
			{
				KingdomMaterials.ClearanceAssessment assessment = KingdomMaterials.Assess(System, Z, Data.X1, Data.Y1, Data.X2, Data.Y2);
				return (assessment.Valid && assessment.Standing <= 0) ? BountyBlock.NothingStanding : BountyBlock.None;
			}
			case BountyTask.Fetch:
			{
				GameObject pile = FindPile(Z, null, Data);
				if (pile == null || MaterialUnits(pile) <= 0)
				{
					return BountyBlock.PileEmpty;
				}
				return KingdomMaterials.Stock(Z).None ? BountyBlock.NowhereToCarry : BountyBlock.None;
			}
			case BountyTask.Manning:
				if (Survey.Works.Count == 0)
				{
					return BountyBlock.NoWorks;
				}
				return (System.IdleWorks <= 0) ? BountyBlock.NoIdleWork : BountyBlock.None;
			case BountyTask.Scouting:
				return (Frontier(System).Count == 0) ? BountyBlock.NoFrontier : BountyBlock.None;
			default:
				return BountyBlock.None;
			}
		}

		/// <summary>
		/// Says why once, and only once, per stall. A permanent reason is never repeated even if
		/// the notice is looked at again; an ordinary block is re-armed the moment it lifts, so a
		/// stall that comes back is spoken about again rather than swallowed. STANDARDS 7b.
		/// </summary>
		private static void Announce(KingdomSystem System, r_KingdomNotice Data, BountyBlock Block)
		{
			if (Data.AnnouncedBlock == (int)Block)
			{
				return;
			}
			if (Block == BountyBlock.None)
			{
				if (!KingdomBountyRules.IsPermanent((BountyBlock)Data.AnnouncedBlock))
				{
					Data.AnnouncedBlock = 0;
				}
				return;
			}
			if (KingdomBountyRules.IsPermanent((BountyBlock)Data.AnnouncedBlock))
			{
				return;
			}
			Data.AnnouncedBlock = (int)Block;
			string reason = KingdomBountyRules.BlockReason(Block, (BountyTask)Data.TaskCode,
				KingdomPresentation.Rich(System.SeatName));
			if (reason != null)
			{
				System.Ledger.Note("{{r|" + reason + "}}");
				MessageQueue.AddPlayerMessage("{{r|" + reason + "}}");
			}
			KingdomLog.Log("bounty: blocked " + Block + " task=" + KingdomBountyRules.TaskKey((BountyTask)Data.TaskCode));
		}

		// ==================================================================================
		// Reading the ground
		// ==================================================================================

		/// <summary>Every notice standing on this ground, in the order the zone yields them.</summary>
		public static List<GameObject> Notices(Zone Z)
		{
			List<GameObject> found = new List<GameObject>();
			if (Z == null)
			{
				return found;
			}
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Z))
			{
				if (item.HasPart(typeof(r_KingdomNotice)))
				{
					found.Add(item);
				}
			}
			return found;
		}

		/// <summary>
		/// Containers within reach of the founder that a fetch notice could be posted over: things
		/// that hold material, are not already dedicated stockpiles, and are not already marked for
		/// another notice.
		/// </summary>
		private static List<GameObject> MarkablePiles(Zone Z, GameObject Founder)
		{
			List<GameObject> found = new List<GameObject>();
			Cell here = (Founder != null) ? Founder.CurrentCell : null;
			if (Z == null || here == null)
			{
				return found;
			}
			List<Cell> reach = new List<Cell>();
			reach.Add(here);
			here.GetAdjacentCells(1, reach);
			for (int i = 0; i < reach.Count; i++)
			{
				Cell cell = reach[i];
				if (cell == null || cell.ParentZone != Z)
				{
					continue;
				}
				foreach (GameObject item in cell.GetObjects())
				{
					if (item.Inventory == null || item.IsCreature || KingdomMaterials.IsStockpile(item))
					{
						continue;
					}
					if (!string.IsNullOrEmpty(item.GetStringProperty(FetchMarkProperty)) || found.Contains(item))
					{
						continue;
					}
					if (MaterialUnits(item) > 0)
					{
						found.Add(item);
					}
				}
			}
			return found;
		}

		private static int MaterialUnits(GameObject Container)
		{
			if (Container == null || Container.Inventory == null)
			{
				return 0;
			}
			int units = 0;
			foreach (GameObject held in Container.Inventory.Objects)
			{
				if (KingdomMaterials.TryMaterialOf(held, out _))
				{
					units += held.Count;
				}
			}
			return units;
		}

		private static GameObject FindPile(Zone Z, GameObject Notice, r_KingdomNotice Data)
		{
			if (Z == null || Data == null || string.IsNullOrEmpty(Data.PileId))
			{
				return null;
			}
			GameObject pile = Z.FindObjectByID(Data.PileId);
			if (pile == null || !GameObject.Validate(pile))
			{
				return null;
			}
			// The mark is the designation, so the mark is what is checked - not the id we happen
			// to have stored. A founder who cleared it has taken their permission back.
			string marked = pile.GetStringProperty(FetchMarkProperty);
			if (string.IsNullOrEmpty(marked))
			{
				return null;
			}
			if (Notice != null && marked != Notice.ID)
			{
				return null;
			}
			return pile;
		}

		private static void ClearFetchMark(Zone Z, GameObject Notice, r_KingdomNotice Data)
		{
			GameObject pile = FindPile(Z, Notice, Data);
			pile?.RemoveStringProperty(FetchMarkProperty);
			Data.PileId = null;
		}

		/// <summary>
		/// Every zone touching the realm's claim that the realm does not hold: the ground a scout
		/// can be sent to look at. Sorted ordinally, so the kernel's pick lands on the same ground
		/// on any reload.
		/// </summary>
		public static List<string> Frontier(KingdomSystem System)
		{
			List<string> found = new List<string>();
			if (System == null || !System.Founded)
			{
				return found;
			}
			for (int i = 0; i < System.ClaimedZones.Count; i++)
			{
				string world;
				int px;
				int py;
				int zx;
				int zy;
				int z;
				if (!ZoneID.Parse(System.ClaimedZones[i], out world, out px, out py, out zx, out zy, out z))
				{
					continue;
				}
				int globalX = px * KingdomBountyRules.ZonesPerParasang + zx;
				int globalY = py * KingdomBountyRules.ZonesPerParasang + zy;
				for (int step = 0; step < KingdomBountyRules.NeighbourCount; step++)
				{
					int nx;
					int ny;
					if (!KingdomBountyRules.TryNeighbour(globalX, globalY, step, out nx, out ny))
					{
						continue;
					}
					int npx;
					int nzx;
					int npy;
					int nzy;
					if (!KingdomBountyRules.TrySplitGlobal(nx, out npx, out nzx) || !KingdomBountyRules.TrySplitGlobal(ny, out npy, out nzy))
					{
						continue;
					}
					string id = ZoneID.Assemble(world, npx, npy, nzx, nzy, z);
					if (System.ClaimedZones.Contains(id) || found.Contains(id))
					{
						continue;
					}
					if (System.Away != null && System.Away.ClaimedZones.Contains(id))
					{
						continue;
					}
					found.Add(id);
				}
			}
			found.Sort(StringComparer.Ordinal);
			return found;
		}

		// ==================================================================================
		// Prose on the object itself
		// ==================================================================================

		private static void Describe(KingdomSystem System, Zone Z, GameObject Notice, r_KingdomNotice Data)
		{
			BountyTask task = (BountyTask)Data.TaskCode;
			string text = KingdomBountyRules.NoticeText(task, Data.Price, DetailOf(System, Z, Data)) + " " + Progress(System, Data);
			Notice.DisplayName = string.IsNullOrEmpty(Data.WorkerName) ? "a posted notice" : "a claimed notice";
			Notice.RequirePart<Description>().Short = text;
		}

		private static string DetailOf(KingdomSystem System, Zone Z, r_KingdomNotice Data)
		{
			switch ((BountyTask)Data.TaskCode)
			{
			case BountyTask.Clearance:
				return "The cord runs round " + Data.Magnitude + " paces of it.";
			case BountyTask.Fetch:
			{
				GameObject pile = FindPile(Z, null, Data);
				return (pile == null) ? null : ("The mark is cut into " + pile.ShortDisplayName + ".");
			}
			case BountyTask.Manning:
				return "A season is " + KingdomBountyRules.ManningSeasonDays + " days, and the settlement counts them.";
			default:
				return null;
			}
		}

		private static string Progress(KingdomSystem System, r_KingdomNotice Data)
		{
			if (Data.Done)
			{
				int owed = Data.Price - Data.Paid;
				return (owed > 0)
					? ("{{r|The work is done and " + owed + ((owed == 1) ? " dram is" : " drams are") + " still owed on it.}}")
					: "{{G|The work is done and the price is paid.}}";
			}
			if (string.IsNullOrEmpty(Data.WorkerName))
			{
				string reason = KingdomBountyRules.BlockReason((BountyBlock)Data.AnnouncedBlock,
					(BountyTask)Data.TaskCode, KingdomPresentation.Rich(System.SeatName));
				return (reason == null) ? "{{K|Nobody has taken it yet.}}" : ("{{r|" + reason + "}}");
			}
			if (Data.DueTick <= 0L)
			{
				return "{{W|" + KingdomPresentation.Rich(Data.WorkerName) + " has it, and is at it now.}}";
			}
			long left = Data.DueTick - The.Game.TimeTicks;
			int days = (int)((left + KingdomRules.TicksPerDay - 1L) / KingdomRules.TicksPerDay);
			if (days <= 0)
			{
				return "{{W|" + KingdomPresentation.Rich(Data.WorkerName) + " has it, and is due back.}}";
			}
			return "{{W|" + KingdomPresentation.Rich(Data.WorkerName) + " has it. " + days + ((days == 1) ? " day" : " days") + " left of it.}}";
		}

		private static string StatusLine(KingdomSystem System, GameObject Notice)
		{
			r_KingdomNotice data = Notice.GetPart<r_KingdomNotice>();
			if (data == null)
			{
				return "{{K|an unreadable notice}}";
			}
			int price = KingdomBountyRules.ClampPrice(data.Price);
			return KingdomBountyRules.TaskName((BountyTask)data.TaskCode).Capitalize()
				+ " {{K|(" + price + ((price == 1) ? " dram" : " drams") + ")}} -- " + Progress(System, data);
		}

		private static Cell HeartCell(Zone Z, GameObject Founder)
		{
			int heartX = -1;
			int heartY = -1;
			KingdomSystem.Guard("bounty: heart", delegate
			{
				List<KingdomLayoutRules.LayoutMark> marks = KingdomLayout.ReadMarks(Z);
				int centreX;
				int centreY;
				if (KingdomLayoutRules.TryHeart(marks, out centreX, out centreY))
				{
					heartX = centreX;
					heartY = centreY;
				}
			});
			Cell here = (Founder != null) ? Founder.CurrentCell : null;
			if (heartX < 0 && here != null)
			{
				heartX = here.X;
				heartY = here.Y;
			}
			if (heartX < 0)
			{
				return null;
			}
			// Empty cells only, per the protection law: a notice never lands on top of anything,
			// and least of all on top of something the founder put there.
			for (int radius = 0; radius <= 6; radius++)
			{
				for (int y = heartY - radius; y <= heartY + radius; y++)
				{
					for (int x = heartX - radius; x <= heartX + radius; x++)
					{
						if (radius > 0 && x != heartX - radius && x != heartX + radius && y != heartY - radius && y != heartY + radius)
						{
							continue;
						}
						Cell candidate = Z.GetCell(x, y);
						if (candidate != null && candidate.IsEmpty() && candidate.IsPassable())
						{
							return candidate;
						}
					}
				}
			}
			return null;
		}
	}
}
