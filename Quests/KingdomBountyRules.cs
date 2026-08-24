using System.Collections.Generic;
using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	/// <summary>What a posted notice asks for. Four tasks, each grounded in a system that already
	/// exists: clearance, the stockpiles, the works, and the claim's own edge.</summary>
	public enum BountyTask
	{
		/// <summary>Clear a staked rect. Pays twice: the price, and the clearance yield.</summary>
		Clearance = 0,

		/// <summary>Carry a marked pile into the settlement's stockpiles.</summary>
		Fetch = 1,

		/// <summary>Man one idle work for a season.</summary>
		Manning = 2,

		/// <summary>Walk the frontier edge and bring back a report of the ground beyond.</summary>
		Scouting = 3
	}

	/// <summary>
	/// Why a standing notice is not moving. Two families, and the difference decides how it is
	/// spoken (STANDARDS 7b): a <b>block</b> can lift on its own and is announced once per stall,
	/// while a <b>permanent</b> reason means the notice can never be attempted at all and is
	/// announced once, for good.
	/// </summary>
	public enum BountyBlock
	{
		/// <summary>Nothing is wrong; the notice simply has not been taken yet.</summary>
		None = 0,

		/// <summary>Nobody lives here to read it.</summary>
		NobodyToTry = 1,

		/// <summary>The rect holds nothing that has to come down. Permanent.</summary>
		NothingStanding = 2,

		/// <summary>The marked pile holds no material. Permanent.</summary>
		PileEmpty = 3,

		/// <summary>No stockpile is dedicated to carry the pile into.</summary>
		NowhereToCarry = 4,

		/// <summary>The settlement has no works at all. Permanent.</summary>
		NoWorks = 5,

		/// <summary>Every work already has its hands.</summary>
		NoIdleWork = 6,

		/// <summary>The claim has no unclaimed edge left to walk. Permanent.</summary>
		NoFrontier = 7,

		/// <summary>The work is done and the stores cannot cover the price.</summary>
		StoresCannotPay = 8
	}

	/// <summary>What one attended pass did with a standing notice.</summary>
	public enum BountyOutcome
	{
		/// <summary>Nobody came to read it, or there was nobody to come.</summary>
		NobodyTried = 0,

		/// <summary>Somebody read it and walked away. Free, and remembered.</summary>
		Refused = 1,

		/// <summary>Somebody took it.</summary>
		Taken = 2
	}

	/// <summary>Durable take-over phases. Values are save format: append only.</summary>
	public enum BountyTakePhase
	{
		None = 0,
		Bound = 1,
		TaskIntent = 2,
		TaskDone = 3,
		ChronicleDone = 4,
		LedgerIntent = 5,
		LedgerDone = 6,
		MessageIntent = 7,
		MessageDone = 8,
		Complete = 9,
		Quarantined = 10
	}

	/// <summary>One exact inventory transfer's durable phase. Values are save format.</summary>
	public enum BountyTransferPhase
	{
		None = 0,
		Bound = 1,
		RemoveIntent = 2,
		Detached = 3,
		AddIntent = 4,
		Arrived = 5,
		Quarantined = 6
	}

	/// <summary>Where a receipt-bound item can be proved to be.</summary>
	public enum BountyTransferLocation
	{
		Missing = 0,
		SourceOnly = 1,
		Detached = 2,
		DestinationOnly = 3,
		Both = 4,
		Elsewhere = 5
	}

	public enum BountyTransferAction
	{
		Wait = 0,
		Bind = 1,
		Remove = 2,
		Add = 3,
		Confirm = 4,
		Quarantine = 5
	}

	/// <summary>Honest outcome of an uninspectable founder-facing sink.</summary>
	public enum BountySinkDisposition
	{
		None = 0,
		Pending = 1,
		Attempting = 2,
		Delivered = 3,
		Skipped = 4,
		Lost = 5
	}

	/// <summary>Publication of a newly staked, already-durable notice.</summary>
	public enum BountyPostPhase
	{
		None = 0,
		Bound = 1,
		ChronicleDone = 2,
		MessageSettled = 3,
		Complete = 4
	}

	/// <summary>Durable founder withdrawal, including its one-shot destruction callback.</summary>
	public enum BountyWithdrawPhase
	{
		None = 0,
		Bound = 1,
		MarkCleared = 2,
		ChronicleDone = 3,
		MessageSettled = 4,
		CleanupAttempting = 5,
		CleanupLost = 6
	}

	/// <summary>Exact-water payout phases. Bound and DebitIntent always carry vessel rows.</summary>
	public enum BountyPaymentPhase
	{
		None = 0,
		Bound = 1,
		DebitIntent = 2,
		Debited = 3,
		Credited = 4,
		Quarantined = 5
	}

	public enum BountyPaymentObservation
	{
		Malformed = 0,
		Original = 1,
		Debited = 2,
		Mixed = 3,
		Uncertain = 4
	}

	public enum BountyPaymentAction
	{
		Wait = 0,
		Bind = 1,
		Debit = 2,
		Credit = 3,
		Quarantine = 4
	}

	/// <summary>Paid-notice publication phases. Intent precedes every uninspectable output.</summary>
	public enum BountyTerminalPhase
	{
		None = 0,
		ChronicleDone = 1,
		LedgerIntent = 2,
		LedgerDone = 3,
		MessageIntent = 4,
		MessageDone = 5,
		CleanupAttempting = 6,
		CleanupLost = 7
	}

	/// <summary>
	/// The posted price: engine-free arithmetic and every hand-written line behind the notice a
	/// founder stakes at the heart (<see cref="KingdomBounty"/> is the engine-coupled shell).
	/// <para>
	/// Two things are load-bearing here. The first is that <b>nothing is escrowed</b>: this file
	/// never asks what the stores hold at posting time, because the price does not leave them
	/// until the work is done. The second is that <b>who attempts what is drawn, not chosen</b>
	/// &mdash; every draw goes through <see cref="CounterRandom"/> on a key built from the
	/// settlement, the notice's posted tick, and the pass ordinal, so the same notice on the same
	/// pass always finds the same reader on any reload.
	/// </para>
	/// <para>
	/// The weighting reads a settler's tastes and traits out of <see cref="KingdomCeremonyRules"/>
	/// rather than inventing a second vocabulary for the same people. See
	/// <see cref="PersonOrdinal"/> for why a person-keyed ceremony draw can never collide with a
	/// tick-keyed one.
	/// </para>
	/// </summary>
	public static class KingdomBountyRules
	{
		public const int MaxSavedTextChars = 4096;
		public const int MaxPaymentRows = 256;
		public const int MaxPaymentRowsChars = 8192;
		public const int MaxObjectIdChars = 256;
		public const int MaxCanonicalIntegerChars = 10;

		public static bool SinkSettled(BountySinkDisposition State)
		{
			return State == BountySinkDisposition.Delivered
				|| State == BountySinkDisposition.Skipped
				|| State == BountySinkDisposition.Lost;
		}

		/// <summary>An interrupted uninspectable call is explicit loss, never assumed delivery.</summary>
		public static BountySinkDisposition RecoverUninspectable(BountySinkDisposition State)
		{
			return State == BountySinkDisposition.Attempting
				? BountySinkDisposition.Lost : State;
		}

		/// <summary>Strict bounded canonical non-negative integer rows.</summary>
		public static bool TryCanonicalIntRows(string Text, out int[] Values)
		{
			Values = null;
			if (string.IsNullOrEmpty(Text) || Text.Length > MaxPaymentRowsChars) return false;
			int separators = 0;
			for (int i = 0; i < Text.Length; i++)
			{
				if (Text[i] == '|') separators++;
				if (separators >= MaxPaymentRows) return false;
			}
			string[] rows = Text.Split('|');
			if (rows.Length == 0 || rows.Length > MaxPaymentRows) return false;
			Values = new int[rows.Length];
			for (int i = 0; i < rows.Length; i++)
			{
				if (rows[i].Length == 0 || rows[i].Length > MaxCanonicalIntegerChars
					|| !int.TryParse(rows[i], global::System.Globalization.NumberStyles.None,
						global::System.Globalization.CultureInfo.InvariantCulture, out Values[i])
					|| Values[i] < 0 || Values[i].ToString(
						global::System.Globalization.CultureInfo.InvariantCulture) != rows[i]) return false;
			}
			return true;
		}

		/// <summary>Strict bounded object-id rows; separators are never valid inside an id.</summary>
		public static bool TryObjectIdRows(string Text, out string[] Values)
		{
			Values = null;
			if (string.IsNullOrEmpty(Text) || Text.Length > MaxPaymentRowsChars) return false;
			int separators = 0;
			for (int i = 0; i < Text.Length; i++)
			{
				if (Text[i] == '|') separators++;
				if (separators >= MaxPaymentRows) return false;
			}
			string[] rows = Text.Split('|');
			if (rows.Length == 0 || rows.Length > MaxPaymentRows) return false;
			for (int i = 0; i < rows.Length; i++)
			{
				if (string.IsNullOrEmpty(rows[i]) || rows[i].Length > MaxObjectIdChars) return false;
				for (int j = 0; j < i; j++)
				{
					if (string.Equals(rows[j], rows[i], global::System.StringComparison.Ordinal)) return false;
				}
			}
			Values = rows;
			return true;
		}
		private const int BountyRulesVersion = 1;

		private const int ScheduledBountyRulesVersion = 2;

		/// <summary>Fixed, all-zero seed, exactly as <c>KingdomChronicle</c>,
		/// <c>KingdomVoiceRules</c>, and <c>KingdomCeremonyRules</c> use it: domain separation is
		/// carried entirely by the settlement id, stream, kind, and ordinal folded into each
		/// key.</summary>
		private static readonly KernelSeed128 BountySeed = default(KernelSeed128);

		/// <summary>Ordinal lane for notice draws &mdash; one per settlement, shared with no other
		/// kernel-backed draw in the mod.</summary>
		private const string NoticeEventStreamId = "taf:bounty:notice:v1";

		private const string ScheduledNoticeStreamPrefix = "taf:bounty:notice:v2:";

		/// <summary>Ordinal lane for the frontier pick. A lane of its own rather than another draw
		/// index on the notice lane: the notice's indices are <c>pass * 3 + k</c> and already
		/// cover every non-negative index, so any frontier index on that lane would be some other
		/// pass's read or take draw.</summary>
		private const string FrontierEventStreamId = "taf:bounty:frontier:v1";

		private const uint NoticeEventKind = 1u;

		/// <summary>Draws one pass of one notice spends: whether anybody reads it, which settler
		/// does, and whether they take it. Fixed, because a semantic draw index must name a
		/// purpose forever.</summary>
		public const uint DrawsPerPass = 3u;

		/// <summary>Passes a single notice can be resolved for before the draw index would run
		/// past <c>uint</c>. Ten million passes is roughly ten million attended visits to one
		/// notice; the cap exists so the arithmetic is total, not because it can be reached.</summary>
		public const int MaxPasses = 10000000;

		/// <summary>One opportunity per Qud day, independent of zone activation cadence.</summary>
		public const long AttemptIntervalTicks = 1200L;

		/// <summary>
		/// Compatibility cap for callers which inspect an absolute schedule as a prefix. Runtime
		/// notices deliberately resolve only the latest due opportunity, because an unattended
		/// historical draw has no historically captured roster to resolve against.
		/// </summary>
		public const int MaxAttemptsPerSettlementPass = 4096;

		/// <summary>Compatibility presentation cap for schedule-inspection clients. Runtime's
		/// latest-only policy can produce at most one refusal in an attended pass.</summary>
		public const int MaxAttemptPresentations = 3;

		/// <summary>
		/// Persistent semantic lane for one notice. Qud object ids are decimal game-object ids, but
		/// folding is total for imported or hand-edited values too.
		/// </summary>
		public static string NoticeEventStream(string NoticeId)
		{
			StringBuilder builder = new StringBuilder(ScheduledNoticeStreamPrefix);
			if (string.IsNullOrEmpty(NoticeId))
			{
				builder.Append("unknown");
			}
			else
			{
				for (int i = 0; i < NoticeId.Length && builder.Length < 128; i++)
				{
					char c = NoticeId[i];
					if (c >= 'A' && c <= 'Z')
					{
						c = (char)(c + 32);
					}
					bool allowed = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')
						|| c == '.' || c == '_' || c == ':' || c == '-';
					builder.Append(allowed ? c : '-');
				}
			}
			return builder.ToString();
		}

		/// <summary>Stable caller key for keyed chronicle and durable output receipts.</summary>
		public static string NoticeEventId(string NoticeId)
		{
			const string prefix = "taf:bounty:event:v1:";
			StringBuilder builder = new StringBuilder(prefix);
			string source = string.IsNullOrEmpty(NoticeId) ? "unknown" : NoticeId;
			for (int i = 0; i < source.Length && builder.Length < 180; i++)
			{
				char c = source[i];
				if (c >= 'A' && c <= 'Z') c = (char)(c + 32);
				bool allowed = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')
					|| c == '.' || c == '_' || c == ':' || c == '-';
				builder.Append(allowed ? c : '-');
			}
			return builder.ToString();
		}

		public static bool IsNoticeEventStream(string Value)
		{
			return !string.IsNullOrEmpty(Value) && Value.Length <= 128
				&& Value.StartsWith(ScheduledNoticeStreamPrefix, System.StringComparison.Ordinal);
		}

		public static bool IsNoticeEventId(string Value)
		{
			return !string.IsNullOrEmpty(Value) && Value.Length <= 180
				&& Value.StartsWith("taf:bounty:event:v1:", System.StringComparison.Ordinal);
		}

		/// <summary>Pure recovery law for one exact item move.</summary>
		public static BountyTransferAction TransferAction(BountyTransferPhase Phase,
			BountyTransferLocation Location)
		{
			if (Phase == BountyTransferPhase.Quarantined)
			{
				return BountyTransferAction.Wait;
			}
			if (Phase == BountyTransferPhase.None)
			{
				return BountyTransferAction.Bind;
			}
			if (Phase == BountyTransferPhase.Bound
				&& Location == BountyTransferLocation.SourceOnly)
			{
				return BountyTransferAction.Remove;
			}
			if (Phase == BountyTransferPhase.Arrived
				&& Location == BountyTransferLocation.DestinationOnly)
			{
				return BountyTransferAction.Confirm;
			}
			return BountyTransferAction.Quarantine;
		}

		/// <summary>
		/// Classifies a persisted exact-water receipt. A row only proves payment when same bound
		/// vessel has exactly its intended post-debit volume. Any other deficit is uncertain and
		/// may not authorize another debit.
		/// </summary>
		public static BountyPaymentObservation ObservePayment(int Requested,
			int[] OriginalVolumes, int[] CurrentVolumes, int[] Allocations,
			bool[] SameVessel, bool[] EmptyOrPureWater, out int ProvedRemoved)
		{
			ProvedRemoved = 0;
			if (Requested <= 0 || OriginalVolumes == null || CurrentVolumes == null
				|| Allocations == null || SameVessel == null || EmptyOrPureWater == null
				|| OriginalVolumes.Length == 0
				|| OriginalVolumes.Length != CurrentVolumes.Length
				|| OriginalVolumes.Length != Allocations.Length
				|| OriginalVolumes.Length != SameVessel.Length
				|| OriginalVolumes.Length != EmptyOrPureWater.Length)
			{
				return BountyPaymentObservation.Malformed;
			}
			bool allOriginal = true;
			bool allDebited = true;
			bool everyRowExact = true;
			long proved = 0L;
			long allocated = 0L;
			for (int i = 0; i < OriginalVolumes.Length; i++)
			{
				int original = OriginalVolumes[i];
				int allocation = Allocations[i];
				if (original <= 0 || allocation <= 0 || allocation > original)
				{
					return BountyPaymentObservation.Malformed;
				}
				allocated += allocation;
				bool identity = SameVessel[i] && EmptyOrPureWater[i];
				bool originalRow = identity && CurrentVolumes[i] == original;
				bool debitedRow = identity && CurrentVolumes[i] == original - allocation;
				allOriginal &= originalRow;
				allDebited &= debitedRow;
				if (debitedRow) proved += allocation;
				if (!originalRow && !debitedRow) everyRowExact = false;
			}
			if (allocated != Requested || proved > int.MaxValue)
			{
				return BountyPaymentObservation.Malformed;
			}
			ProvedRemoved = (int)proved;
			if (allOriginal) return BountyPaymentObservation.Original;
			if (allDebited) return BountyPaymentObservation.Debited;
			return everyRowExact ? BountyPaymentObservation.Mixed : BountyPaymentObservation.Uncertain;
		}

		public static BountyPaymentAction PaymentAction(BountyPaymentPhase Phase,
			BountyPaymentObservation Observation)
		{
			if (Phase == BountyPaymentPhase.Quarantined) return BountyPaymentAction.Wait;
			if (Phase == BountyPaymentPhase.None) return BountyPaymentAction.Bind;
			if (Phase == BountyPaymentPhase.Bound
				&& Observation == BountyPaymentObservation.Original)
			{
				return BountyPaymentAction.Debit;
			}
			if (Phase == BountyPaymentPhase.Credited) return BountyPaymentAction.Wait;
			return BountyPaymentAction.Quarantine;
		}

		/// <summary>Save-facing scalar lifecycle validity. Engine shell additionally validates bindings.</summary>
		public static bool ValidLifecycleScalars(int TaskCode, int Price, int Paid, bool Done,
			string WorkerName, int ScheduleVersion, string EventStreamId, long NextAttemptTick,
			bool ScheduleExhausted, int Passes, int TakePhase, int TransferPhase,
			int PaymentPhase, int TerminalPhase)
		{
			if (TaskCode < 0 || TaskCode >= TaskCount || Price < MinPrice || Price > MaxPrice
				|| Paid < 0 || Paid > Price || Passes < 0 || Passes > MaxPasses)
			{
				return false;
			}
			if (Done && string.IsNullOrEmpty(WorkerName)) return false;
			if (ScheduleVersion != 0 && ScheduleVersion != ScheduledBountyRulesVersion) return false;
			if (ScheduleVersion == ScheduledBountyRulesVersion
				&& (!IsNoticeEventStream(EventStreamId)
					|| (ScheduleExhausted ? NextAttemptTick != 0L : NextAttemptTick <= 0L)))
			{
				return false;
			}
			return TakePhase >= 0 && TakePhase <= (int)BountyTakePhase.Quarantined
				&& TransferPhase >= 0 && TransferPhase <= (int)BountyTransferPhase.Quarantined
				&& PaymentPhase >= 0 && PaymentPhase <= (int)BountyPaymentPhase.Quarantined
				&& TerminalPhase >= 0 && TerminalPhase <= (int)BountyTerminalPhase.CleanupLost;
		}

		/// <summary>First opportunity strictly after posting. False only at tick exhaustion.</summary>
		public static bool TryFirstAttemptTick(long PostedTick, out long Tick)
		{
			Tick = 0L;
			long posted = (PostedTick > 0L) ? PostedTick : 0L;
			if (posted > long.MaxValue - AttemptIntervalTicks)
			{
				return false;
			}
			Tick = posted + AttemptIntervalTicks;
			return true;
		}

		/// <summary>Next opportunity in the same absolute daily lane.</summary>
		public static bool TryAdvanceAttemptTick(long CurrentTick, out long NextTick)
		{
			NextTick = 0L;
			if (CurrentTick < 0L || CurrentTick > long.MaxValue - AttemptIntervalTicks)
			{
				return false;
			}
			NextTick = CurrentTick + AttemptIntervalTicks;
			return true;
		}

		/// <summary>
		/// First aligned opportunity strictly after Now. Used only to migrate visit-counted legacy
		/// notices: old outcomes remain consumed, and loading the new build cannot immediately roll
		/// another reader.
		/// </summary>
		public static bool TryAttemptAfter(long NowTick, long PostedTick, out long Tick)
		{
			Tick = 0L;
			long now = (NowTick > 0L) ? NowTick : 0L;
			long first;
			if (!TryFirstAttemptTick(PostedTick, out first))
			{
				return false;
			}
			if (now < first)
			{
				Tick = first;
				return true;
			}
			long elapsed = now - first;
			long steps = elapsed / AttemptIntervalTicks + 1L;
			if (steps > (long.MaxValue - first) / AttemptIntervalTicks)
			{
				return false;
			}
			Tick = first + steps * AttemptIntervalTicks;
			return true;
		}

		/// <summary>
		/// Bounded prefix arithmetic retained for diagnostics and compatibility. It does not decide
		/// which roster may answer those opportunities; runtime uses <see cref="TryLatestDueAttempt"/>
		/// and consumes older opportunities without drawing them.
		/// </summary>
		public static int DueAttemptPrefix(long NowTick, long NextTick, bool Exhausted, int Cap)
		{
			if (Exhausted || Cap <= 0 || NextTick < 0L || NowTick < NextTick)
			{
				return 0;
			}
			long count = (NowTick - NextTick) / AttemptIntervalTicks + 1L;
			return (count > Cap) ? Cap : (int)count;
		}

		/// <summary>
		/// Selects only the latest due opportunity. Earlier unattended opportunities are skipped,
		/// because resolving them against a future roster lets a newcomer act before they arrived.
		/// The returned skip count is durable audit truth; callers advance both cursor and consumed
		/// count before asking the current roster about <paramref name="LatestTick"/>.
		/// </summary>
		public static bool TryLatestDueAttempt(long NowTick, long NextTick, bool Exhausted,
			out long LatestTick, out long Skipped)
		{
			LatestTick = 0L;
			Skipped = 0L;
			if (Exhausted || NextTick < 0L || NowTick < NextTick)
			{
				return false;
			}
			Skipped = (NowTick - NextTick) / AttemptIntervalTicks;
			if (Skipped > 0L && Skipped > (long.MaxValue - NextTick) / AttemptIntervalTicks)
			{
				return false;
			}
			LatestTick = NextTick + Skipped * AttemptIntervalTicks;
			return LatestTick <= NowTick && NowTick - LatestTick < AttemptIntervalTicks;
		}

		// ==================================================================================
		// The tasks
		// ==================================================================================

		/// <summary>Number of values in <see cref="BountyTask"/>. Sized against the enum by
		/// <c>KingdomBountyRulesTests</c>, which is what stops a task being added here and
		/// forgotten in the tables below.</summary>
		public const int TaskCount = 4;

		/// <summary>Stable keys, in enum order &mdash; what a log line or a save-facing string
		/// writes.</summary>
		public static readonly string[] TaskKeys = new string[TaskCount] { "clearance", "fetch", "manning", "scouting" };

		/// <summary>Player-facing names, in enum order. Lowercase, in the game's register.</summary>
		public static readonly string[] TaskNames = new string[TaskCount]
		{
			"clear the staked ground",
			"carry the marked pile in",
			"man an idle work for a season",
			"walk the frontier edge"
		};

		/// <summary>The taste family each task belongs to, as
		/// <see cref="KingdomCeremonyRules.TasteCategories"/> names them. Matching a settler's
		/// stated taste is what makes them likelier to take the notice.</summary>
		public static readonly string[] TaskTasteCategories = new string[TaskCount] { "craft", "storage", "power", "defense" };

		/// <summary>The task's key, or the clearance key for a value outside the enum.</summary>
		public static string TaskKey(BountyTask Task)
		{
			int index = (int)Task;
			return (index >= 0 && index < TaskKeys.Length) ? TaskKeys[index] : TaskKeys[0];
		}

		/// <summary>The task's player-facing name, or the clearance name for a value outside the
		/// enum.</summary>
		public static string TaskName(BountyTask Task)
		{
			int index = (int)Task;
			return (index >= 0 && index < TaskNames.Length) ? TaskNames[index] : TaskNames[0];
		}

		/// <summary>
		/// Index into <see cref="KingdomCeremonyRules.TasteCategories"/> for a task's family,
		/// found rather than hardcoded, so reordering the ceremony's ten families cannot silently
		/// point a task at the wrong taste.
		/// </summary>
		/// <returns>The index, or -1 when the ceremony does not carry that family &mdash; which
		/// reads downstream as "no settler can ever match this task", never as index zero.</returns>
		public static int TasteIndexFor(BountyTask Task)
		{
			int index = (int)Task;
			if (index < 0 || index >= TaskTasteCategories.Length)
			{
				return -1;
			}
			string wanted = TaskTasteCategories[index];
			for (int i = 0; i < KingdomCeremonyRules.TasteCategories.Length; i++)
			{
				if (string.Equals(KingdomCeremonyRules.TasteCategories[i], wanted, System.StringComparison.Ordinal))
				{
					return i;
				}
			}
			return -1;
		}

		// ==================================================================================
		// The price
		// ==================================================================================

		/// <summary>The least a notice may promise. A notice for nothing is not a notice.</summary>
		public const int MinPrice = 1;

		/// <summary>The most a notice may promise. A founder with a full cistern can still only
		/// move one settlement's worth of opinion; past this the price stops buying enthusiasm and
		/// starts buying nothing at all.</summary>
		public const int MaxPrice = 40;

		/// <summary>Notices that may stand at one heart at once.</summary>
		public const int MaxNotices = 3;

		/// <summary>Folds any number into a payable price.</summary>
		public static int ClampPrice(int Drams)
		{
			if (Drams < MinPrice)
			{
				return MinPrice;
			}
			return (Drams > MaxPrice) ? MaxPrice : Drams;
		}

		/// <summary>
		/// What the notice is worth posting at, given how much work it names: the founder's
		/// starting point in the price picker, never a floor and never a ceiling.
		/// </summary>
		/// <param name="Task">The task.</param>
		/// <param name="Magnitude">Task-specific size &mdash; cells for a clearance, units for a
		/// fetch, works for a manning, zero for a scouting. Negative reads as zero.</param>
		public static int SuggestedPrice(BountyTask Task, int Magnitude)
		{
			int size = (Magnitude > 0) ? Magnitude : 0;
			switch (Task)
			{
			case BountyTask.Clearance:
				return ClampPrice(3 + size / 4);
			case BountyTask.Fetch:
				return ClampPrice(2 + size / 3);
			case BountyTask.Manning:
				return ClampPrice(8);
			case BountyTask.Scouting:
				return ClampPrice(6);
			default:
				return ClampPrice(3);
			}
		}

		// ==================================================================================
		// Who reads it, and what they think of it
		// ==================================================================================

		/// <summary>
		/// A settler's own ordinal for the ceremony's taste and trait draws, folded from their
		/// name so the same settler always carries the same tastes for as long as they are called
		/// that.
		/// <para>
		/// The top bit is always set, and that is the whole safety argument: every other ceremony
		/// draw keys its ordinal on <c>The.Game.TimeTicks</c>, which is a signed tick count that
		/// would need something on the order of 7.6 quadrillion in-game days to reach 2^63. A
		/// person-keyed draw therefore cannot land on a tick-keyed one, so a settler's tastes can
		/// never be an accidental copy of a notable's.
		/// </para>
		/// </summary>
		/// <param name="Name">The settler's roster name. Null or empty folds to a stable ordinal
		/// of its own rather than throwing.</param>
		public static ulong PersonOrdinal(string Name)
		{
			// FNV-1a, 64-bit: written out rather than taken from string.GetHashCode, which is
			// randomised per process in .NET and would give one settler different tastes on every
			// launch.
			ulong hash = 14695981039346656037uL;
			if (!string.IsNullOrEmpty(Name))
			{
				for (int i = 0; i < Name.Length; i++)
				{
					char c = Name[i];
					hash ^= (byte)(c & 0xFF);
					hash *= 1099511628211uL;
					hash ^= (byte)((c >> 8) & 0xFF);
					hash *= 1099511628211uL;
				}
			}
			return hash | 0x8000000000000000uL;
		}

		/// <summary>How eager for paid work the settler's drawn pair of traits leaves them.</summary>
		public const int AppetiteEager = 1;

		/// <summary>How reluctant the other third of pairs leaves them.</summary>
		public const int AppetiteReluctant = -1;

		/// <summary>
		/// Reads a settler's appetite for posted work off <i>which</i> virtue and flaw they drew
		/// rather than off what those lines say.
		/// <para>
		/// Deliberate: the ceremony owns the vocabulary and may grow it, and a table here keyed on
		/// what each line means would go quietly wrong the day a ninth virtue is written. A
		/// function of the pair stays total however long the arrays get, and the prose the founder
		/// reads still comes from the ceremony's own text.
		/// </para>
		/// </summary>
		/// <param name="VirtueIndex">Index the ceremony drew. Negative reads as zero.</param>
		/// <param name="FlawIndex">Index the ceremony drew. Negative reads as zero.</param>
		/// <returns><see cref="AppetiteEager"/>, 0, or <see cref="AppetiteReluctant"/>.</returns>
		public static int TraitAppetite(int VirtueIndex, int FlawIndex)
		{
			int virtueIndex = (VirtueIndex > 0) ? VirtueIndex : 0;
			int flawIndex = (FlawIndex > 0) ? FlawIndex : 0;
			switch ((virtueIndex + flawIndex) % 3)
			{
			case 1:
				return AppetiteEager;
			case 2:
				return AppetiteReluctant;
			default:
				return 0;
			}
		}

		/// <summary>Chance in 100 that anybody reads a standing notice on a given attended pass,
		/// before anyone decides whether to take it.</summary>
		public const int ReadBaseChance = 20;

		/// <summary>Added to the read chance per dram promised.</summary>
		public const int ReadChancePerDram = 2;

		/// <summary>Ceiling on the read chance: a notice is never certain to be looked at, however
		/// rich, because the settlement has its own day to get through.</summary>
		public const int ReadChanceCeiling = 90;

		/// <summary>
		/// Whether the price is loud enough to pull somebody over to the notice board at all.
		/// Depends on the price and nothing else &mdash; who reads it is drawn separately, and what
		/// they think of it is judged in <see cref="TakeChancePercent"/>.
		/// </summary>
		/// <param name="Price">Drams promised. Clamped before use.</param>
		public static int ReadChancePercent(int Price)
		{
			int chance = ReadBaseChance + (ClampPrice(Price) * ReadChancePerDram);
			return (chance > ReadChanceCeiling) ? ReadChanceCeiling : chance;
		}

		/// <summary>Base chance in 100 that a reader takes each task, before anything about them
		/// is considered. In enum order: clearing is honest work, carrying is easy, a whole season
		/// on one work is a commitment, and walking out past the claim is the one that asks
		/// something.</summary>
		public static readonly int[] TakeBaseChance = new int[TaskCount] { 45, 60, 30, 25 };

		/// <summary>Added when the task's family is one the reader stated a taste for.</summary>
		public const int TakeTasteBonus = 20;

		/// <summary>Added when the reader is the settlement's notable &mdash; the longest-served
		/// settler, who holds its one office.</summary>
		public const int TakeNotableBonus = 10;

		/// <summary>Added, or taken away, per point of <see cref="TraitAppetite"/>.</summary>
		public const int TakeAppetiteWeight = 12;

		/// <summary>Added per dram promised.</summary>
		public const int TakeChancePerDram = 1;

		/// <summary>Floor on the take chance: no notice is impossible to take, because a refusal
		/// that can never be anything else is a stall wearing a settler's face.</summary>
		public const int TakeChanceFloor = 5;

		/// <summary>Ceiling on the take chance: refusal is always on the table.</summary>
		public const int TakeChanceCeiling = 95;

		/// <summary>
		/// Chance in 100 that the settler who read the notice takes it. Everything that shades it
		/// is something the founder can see and act on: what they are offering, who is on the
		/// roster, and what those people have said they care about.
		/// </summary>
		/// <param name="Task">The task posted.</param>
		/// <param name="Price">Drams promised. Clamped before use.</param>
		/// <param name="Notable">True when the reader is the settlement's office holder.</param>
		/// <param name="TasteMatched">True when the task's family is one of the reader's tastes.</param>
		/// <param name="Appetite">The reader's <see cref="TraitAppetite"/>.</param>
		public static int TakeChancePercent(BountyTask Task, int Price, bool Notable, bool TasteMatched, int Appetite)
		{
			int index = (int)Task;
			int chance = ((index >= 0 && index < TakeBaseChance.Length) ? TakeBaseChance[index] : TakeBaseChance[0])
				+ (ClampPrice(Price) * TakeChancePerDram)
				+ (TasteMatched ? TakeTasteBonus : 0)
				+ (Notable ? TakeNotableBonus : 0)
				+ (Appetite * TakeAppetiteWeight);
			if (chance < TakeChanceFloor)
			{
				return TakeChanceFloor;
			}
			return (chance > TakeChanceCeiling) ? TakeChanceCeiling : chance;
		}

		/// <summary>Everything one pass drew about one notice, so the shell can chronicle it with
		/// names without re-deriving any of it.</summary>
		public struct BountyAttempt
		{
			/// <summary>False only when the kernel refused before an outcome existed. Callers must
			/// leave the scheduled cursor on this event and retry it; no truth was burned.</summary>
			public bool Determined;

			/// <summary>What the pass came to.</summary>
			public BountyOutcome Outcome;

			/// <summary>Who read the notice, or null when nobody did.</summary>
			public string Name;

			/// <summary>Index into the roster the reader was drawn from, or -1.</summary>
			public int RosterIndex;

			/// <summary>Ceremony virtue index for the reader, for the prose. Meaningless when
			/// <see cref="Name"/> is null.</summary>
			public int VirtueIndex;

			/// <summary>Ceremony flaw index for the reader, for the prose.</summary>
			public int FlawIndex;

			/// <summary>True when the task's family was one of the reader's stated tastes.</summary>
			public bool TasteMatched;
		}

		/// <summary>
		/// Resolves one attended pass against one standing notice: whether anybody read it, who,
		/// and whether they took it.
		/// <para>
		/// Pure and total. Every draw is keyed on the settlement, the notice's posted tick, and a
		/// draw index derived from <paramref name="PassIndex"/>, so replaying the same pass always
		/// produces the same reader and the same answer. A kernel that refuses &mdash; an unnamed
		/// settlement, a machine whose crypto provider is failing &mdash; yields
		/// <see cref="BountyOutcome.NobodyTried"/>, which costs the founder nothing and loses
		/// nothing: the notice simply stands another day.
		/// </para>
		/// </summary>
		/// <param name="SettlementId">The settlement's kernel id
		/// (<c>KingdomChronicle.SettlementId</c>).</param>
		/// <param name="PostedTick">The tick the notice was staked at, its ordinal forever.</param>
		/// <param name="PassIndex">How many passes this notice has already been resolved for.
		/// Negative reads as zero; anything past <see cref="MaxPasses"/> is clamped.</param>
		/// <param name="Roster">Living settlers, longest-served first. Null or empty yields
		/// <see cref="BountyOutcome.NobodyTried"/>.</param>
		/// <param name="Task">The task posted.</param>
		/// <param name="Price">Drams promised.</param>
		public static BountyAttempt Resolve(string SettlementId, long PostedTick, int PassIndex, IList<string> Roster, BountyTask Task, int Price)
		{
			int pass = (PassIndex > 0) ? PassIndex : 0;
			if (pass > MaxPasses)
			{
				pass = MaxPasses;
			}
			SemanticEventKey key;
			KernelFaultCode fault;
			if (!SemanticEventKey.TryCreate(BountyRulesVersion, SettlementId, NoticeEventStreamId, NoticeEventKind, (ulong)((PostedTick > 0L) ? PostedTick : 0L), out key, out fault))
			{
				return EmptyAttempt();
			}
			return ResolveKey(SettlementId, key, (uint)pass * DrawsPerPass, Roster, Task, Price);
		}

		/// <summary>
		/// Resolves one absolute scheduled opportunity. Notice identity owns the event stream and
		/// the scheduled world tick owns the ordinal, so entering a zone cannot mint a new draw.
		/// </summary>
		public static BountyAttempt ResolveScheduled(string SettlementId, string EventStreamId,
			long ScheduledTick, IList<string> Roster, BountyTask Task, int Price)
		{
			if (ScheduledTick < 0L)
			{
				return EmptyAttempt();
			}
			SemanticEventKey key;
			KernelFaultCode fault;
			if (!SemanticEventKey.TryCreate(ScheduledBountyRulesVersion, SettlementId,
				EventStreamId, NoticeEventKind, (ulong)ScheduledTick, out key, out fault))
			{
				return EmptyAttempt();
			}
			return ResolveKey(SettlementId, key, 0u, Roster, Task, Price);
		}

		private static BountyAttempt EmptyAttempt()
		{
			BountyAttempt attempt = default(BountyAttempt);
			attempt.Outcome = BountyOutcome.NobodyTried;
			attempt.RosterIndex = -1;
			return attempt;
		}

		private static BountyAttempt ResolveKey(string SettlementId, SemanticEventKey Key,
			uint DrawBase, IList<string> Roster, BountyTask Task, int Price)
		{
			BountyAttempt attempt = EmptyAttempt();
			if (Roster == null || Roster.Count == 0)
			{
				attempt.Determined = true;
				return attempt;
			}
			KernelFaultCode fault;
			ulong value;
			if (!CounterRandom.TryDrawBelow(BountySeed, Key, DrawBase, 100uL, out value, out fault))
			{
				return attempt;
			}
			if (value >= (ulong)ReadChancePercent(Price))
			{
				attempt.Determined = true;
				return attempt;
			}
			if (!CounterRandom.TryDrawBelow(BountySeed, Key, DrawBase + 1u, (ulong)Roster.Count, out value, out fault))
			{
				return attempt;
			}
			int index = (int)value;
			string name = Roster[index];
			attempt.Name = name;
			attempt.RosterIndex = index;
			ulong person = PersonOrdinal(name);
			KingdomCeremonyRules.ChooseLeaderTraits(SettlementId, person, out attempt.VirtueIndex, out attempt.FlawIndex);
			int wantedTaste = TasteIndexFor(Task);
			if (wantedTaste >= 0)
			{
				List<int> tastes = KingdomCeremonyRules.ChooseTastes(SettlementId, person);
				for (int i = 0; i < tastes.Count; i++)
				{
					if (tastes[i] == wantedTaste)
					{
						attempt.TasteMatched = true;
						break;
					}
				}
			}
			attempt.Outcome = BountyOutcome.Refused;
			if (!CounterRandom.TryDrawBelow(BountySeed, Key, DrawBase + 2u, 100uL, out value, out fault))
			{
				return attempt;
			}
			int take = TakeChancePercent(Task, Price, index == 0, attempt.TasteMatched, TraitAppetite(attempt.VirtueIndex, attempt.FlawIndex));
			if (value < (ulong)take)
			{
				attempt.Outcome = BountyOutcome.Taken;
			}
			attempt.Determined = true;
			return attempt;
		}

		// ==================================================================================
		// The frontier edge
		// ==================================================================================

		/// <summary>Zones to a parasang on each axis, matching the engine's own zone-ID
		/// grammar.</summary>
		public const int ZonesPerParasang = 3;

		/// <summary>Neighbours a zone has on its own stratum.</summary>
		public const int NeighbourCount = 8;

		private static readonly int[] NeighbourDX = new int[NeighbourCount] { 0, 1, 1, 1, 0, -1, -1, -1 };

		private static readonly int[] NeighbourDY = new int[NeighbourCount] { -1, -1, 0, 1, 1, 1, 0, -1 };

		/// <summary>
		/// One of the eight neighbours of a zone, in a fixed order (north, then clockwise), given
		/// its position on the world's continuous zone grid &mdash; the parasang and in-parasang
		/// coordinates folded together as <c>parasang * 3 + zone</c>, which is the same fold
		/// <c>KingdomFounding.ZonesAdjacent</c> uses.
		/// </summary>
		/// <param name="GlobalX">Continuous zone X.</param>
		/// <param name="GlobalY">Continuous zone Y.</param>
		/// <param name="Step">0 through 7.</param>
		/// <param name="NeighbourX">Continuous zone X of the neighbour.</param>
		/// <param name="NeighbourY">Continuous zone Y of the neighbour.</param>
		/// <returns>False for a step outside 0..7, or a neighbour that would fall off the north or
		/// west edge of the world. Nothing is written when it does.</returns>
		public static bool TryNeighbour(int GlobalX, int GlobalY, int Step, out int NeighbourX, out int NeighbourY)
		{
			NeighbourX = 0;
			NeighbourY = 0;
			if (Step < 0 || Step >= NeighbourCount)
			{
				return false;
			}
			int x = GlobalX + NeighbourDX[Step];
			int y = GlobalY + NeighbourDY[Step];
			if (x < 0 || y < 0)
			{
				return false;
			}
			NeighbourX = x;
			NeighbourY = y;
			return true;
		}

		/// <summary>Splits a continuous zone coordinate back into a parasang and an in-parasang
		/// zone. Negative input is refused rather than floored, because the world has no ground
		/// there and a wrong-signed remainder would name a zone that exists.</summary>
		public static bool TrySplitGlobal(int Global, out int Parasang, out int Zone)
		{
			Parasang = 0;
			Zone = 0;
			if (Global < 0)
			{
				return false;
			}
			Parasang = Global / ZonesPerParasang;
			Zone = Global % ZonesPerParasang;
			return true;
		}

		/// <summary>Picks one of the frontier zones a scout could walk to, deterministically.</summary>
		/// <param name="SettlementId">The settlement's kernel id.</param>
		/// <param name="PostedTick">The notice's posted tick.</param>
		/// <param name="PassIndex">The pass being resolved, so a scout sent later reports
		/// different ground.</param>
		/// <param name="Count">Candidates the caller found. Zero or less yields false.</param>
		/// <param name="Index">Index in <c>[0, Count)</c>.</param>
		/// <returns>False only when there was nothing to pick from; a refusing kernel falls back
		/// to index zero, which is a real candidate rather than a sentinel.</returns>
		public static bool TryPickFrontier(string SettlementId, long PostedTick, int PassIndex, int Count, out int Index)
		{
			Index = 0;
			if (Count <= 0)
			{
				return false;
			}
			int pass = (PassIndex > 0) ? PassIndex : 0;
			if (pass > MaxPasses)
			{
				pass = MaxPasses;
			}
			SemanticEventKey key;
			KernelFaultCode fault;
			if (!SemanticEventKey.TryCreate(BountyRulesVersion, SettlementId, FrontierEventStreamId, NoticeEventKind, (ulong)((PostedTick > 0L) ? PostedTick : 0L), out key, out fault))
			{
				return true;
			}
			ulong value;
			if (CounterRandom.TryDrawBelow(BountySeed, key, (uint)pass, (ulong)Count, out value, out fault))
			{
				Index = (int)value;
			}
			return true;
		}

		// ==================================================================================
		// How long the work takes
		// ==================================================================================

		/// <summary>Days a fetch takes before the first unit is set down, however small.</summary>
		public const int HaulBaseDays = 1;

		/// <summary>Units one porter shifts in a day beyond the first.</summary>
		public const int HaulUnitsPerDay = 8;

		/// <summary>The most days one fetch can take, however big the pile.</summary>
		public const int HaulMaxDays = 5;

		/// <summary>How long carrying a marked pile in takes.</summary>
		/// <param name="Units">Material units in the pile. Zero or less still takes the base day,
		/// because the porter still walks there.</param>
		public static int HaulDays(int Units)
		{
			int units = (Units > 0) ? Units : 0;
			int days = HaulBaseDays + (units / HaulUnitsPerDay);
			return (days > HaulMaxDays) ? HaulMaxDays : days;
		}

		/// <summary>Days a settler stands a work for, once they take a manning notice. A season,
		/// in the settlement's own reckoning.</summary>
		public const int ManningSeasonDays = 30;

		/// <summary>Days walking the frontier edge and coming back takes.</summary>
		public const int ScoutDays = 4;

		/// <summary>How long a taken task runs before the price falls due.</summary>
		/// <param name="Task">The task taken.</param>
		/// <param name="Magnitude">Units for a fetch; ignored otherwise.</param>
		/// <returns>Days, or 0 for a clearance &mdash; whose clock is the clearing gang's own
		/// effort, not a countdown.</returns>
		public static int WorkDays(BountyTask Task, int Magnitude)
		{
			switch (Task)
			{
			case BountyTask.Fetch:
				return HaulDays(Magnitude);
			case BountyTask.Manning:
				return ManningSeasonDays;
			case BountyTask.Scouting:
				return ScoutDays;
			default:
				return 0;
			}
		}

		/// <summary>Absolute completion tick, saturated rather than wrapped into the past.</summary>
		public static long WorkDueTick(long TakenTick, int Days)
		{
			long taken = (TakenTick > 0L) ? TakenTick : 0L;
			if (Days <= 0)
			{
				return 0L;
			}
			long duration = (long)Days * KingdomRules.TicksPerDay;
			return (taken > long.MaxValue - duration) ? long.MaxValue : taken + duration;
		}

		// ==================================================================================
		// Saying why, once
		// ==================================================================================

		/// <summary>
		/// Whether a reason means the notice can never be attempted, as opposed to merely not
		/// today. A permanent reason is announced once and then left alone; a block is announced
		/// once per stall and re-announced if it lifts and returns.
		/// </summary>
		public static bool IsPermanent(BountyBlock Block)
		{
			return Block == BountyBlock.NothingStanding
				|| Block == BountyBlock.PileEmpty
				|| Block == BountyBlock.NoWorks
				|| Block == BountyBlock.NoFrontier;
		}

		/// <summary>
		/// The founder-facing sentence for a reason a notice is not moving, ready for the ledger.
		/// Names the task, because a founder with three notices standing needs to know which one
		/// went quiet.
		/// </summary>
		/// <param name="Block">The reason. <see cref="BountyBlock.None"/> yields null.</param>
		/// <param name="Task">The task the notice posted.</param>
		/// <param name="SeatName">The settlement's seat name.</param>
		/// <returns>A complete sentence, or null when there is nothing to say.</returns>
		public static string BlockReason(BountyBlock Block, BountyTask Task, string SeatName)
		{
			string seat = string.IsNullOrEmpty(SeatName) ? "the settlement" : SeatName;
			switch (Block)
			{
			case BountyBlock.NobodyToTry:
				return "A notice stands at the heart of " + seat + " offering water to whoever will " + TaskName(Task) + ", and there is nobody living here to read it.";
			case BountyBlock.NothingStanding:
				return "The notice posted over the staked ground names nothing that has to come down. No one will ever claim it; take it down when you like.";
			case BountyBlock.PileEmpty:
				return "The pile the notice was posted over holds nothing the settlement counts as material. No one will ever claim it; take it down when you like.";
			case BountyBlock.NowhereToCarry:
				return "A porter would carry the marked pile in, and " + seat + " has no stockpile dedicated to put it in. Dedicate a container.";
			case BountyBlock.NoWorks:
				return seat + " has no works at all, so the notice offering a season's manning names nothing. No one will ever claim it; take it down when you like.";
			case BountyBlock.NoIdleWork:
				return "The notice offering a season's manning stands unclaimed: every work in " + seat + " already has its hands.";
			case BountyBlock.NoFrontier:
				return "There is no unclaimed ground left along the edge of " + seat + " for a scout to walk to. No one will ever claim it; take it down when you like.";
			case BountyBlock.StoresCannotPay:
				return "The notice at " + seat + " is claimed and the work is done, and the stores cannot cover the price. It stays owed until they can.";
			default:
				return null;
			}
		}

		// ==================================================================================
		// The prose
		// ==================================================================================

		/// <summary>The line cut into the notice itself, read off it by anyone who looks.</summary>
		/// <param name="Task">The task posted.</param>
		/// <param name="Price">Drams promised.</param>
		/// <param name="Detail">A short clause naming the particular ground, pile, or work, or
		/// null when the task names no particular thing.</param>
		public static string NoticeText(BountyTask Task, int Price, string Detail)
		{
			int price = ClampPrice(Price);
			string drams = price + ((price == 1) ? " dram" : " drams");
			string tail = string.IsNullOrEmpty(Detail) ? "" : (" " + Detail);
			switch (Task)
			{
			case BountyTask.Clearance:
				return "A notice on a stake, and a cord run round the ground it means: " + drams + " of fresh water to whoever clears it." + tail;
			case BountyTask.Fetch:
				return "A notice on a stake, and a mark cut into the pile it means: " + drams + " of fresh water to whoever carries it in." + tail;
			case BountyTask.Manning:
				return "A notice on a stake: " + drams + " of fresh water to whoever stands a work through the season and does not walk off it." + tail;
			case BountyTask.Scouting:
				return "A notice on a stake, facing out past the claim: " + drams + " of fresh water to whoever walks the edge and comes back able to say what is out there." + tail;
			default:
				return "A notice on a stake, promising " + drams + " of fresh water to whoever does what it asks." + tail;
			}
		}

		/// <summary>The chronicle's line for a notice going up. Lower-case clause, no trailing
		/// period &mdash; the chronicle supplies both.</summary>
		public static string PostedChronicle(string SeatName, BountyTask Task, int Price)
		{
			int price = ClampPrice(Price);
			return "a notice was staked at the heart of " + (string.IsNullOrEmpty(SeatName) ? "the settlement" : SeatName)
				+ ", promising " + price + ((price == 1) ? " dram" : " drams") + " to whoever would " + TaskName(Task);
		}

		/// <summary>The chronicle's line for a settler reading the notice and walking away. Named,
		/// and free: nothing is spent, nothing is held against them, and the reason given is their
		/// own drawn flaw rather than an accusation.</summary>
		public static string RefusedChronicle(string Name, BountyTask Task, int FlawIndex)
		{
			string who = string.IsNullOrEmpty(Name) ? "somebody" : Name;
			return who + " read the notice offering water to " + TaskName(Task) + " and left it standing -- " + KingdomCeremonyRules.FlawText(FlawIndex);
		}

		/// <summary>The chronicle's line for a settler taking the notice down off the stake.</summary>
		public static string TakenChronicle(string Name, BountyTask Task, int VirtueIndex, bool TasteMatched)
		{
			string who = string.IsNullOrEmpty(Name) ? "somebody" : Name;
			return who + " took the notice offering water to " + TaskName(Task)
				+ (TasteMatched ? ", which is the very thing they had said they wanted to see, and " : ", and ")
				+ KingdomCeremonyRules.VirtueText(VirtueIndex);
		}

		/// <summary>The chronicle's line for a claimed notice paid out in full.</summary>
		public static string PaidChronicle(string Name, string SeatName, BountyTask Task, int Paid)
		{
			string who = string.IsNullOrEmpty(Name) ? "whoever did it" : Name;
			return who + " did what the notice asked at " + (string.IsNullOrEmpty(SeatName) ? "the settlement" : SeatName)
				+ ", and was paid " + Paid + ((Paid == 1) ? " dram" : " drams") + " out of the stores in front of everyone";
		}

		/// <summary>The chronicle's line for work done that the stores could only part-cover. The
		/// debt is stated plainly rather than quietly written off.</summary>
		public static string OwedChronicle(string Name, string SeatName, int Paid, int Owed)
		{
			string who = string.IsNullOrEmpty(Name) ? "whoever did it" : Name;
			string seat = string.IsNullOrEmpty(SeatName) ? "the settlement" : SeatName;
			if (Paid <= 0)
			{
				return who + " did what the notice asked at " + seat + ", and " + seat + " had not a dram to pay it with, and said so";
			}
			return who + " did what the notice asked at " + seat + ", and took " + Paid + ((Paid == 1) ? " dram" : " drams")
				+ " of the price, with " + Owed + " still owed and written down";
		}

		/// <summary>The ledger's line while a debt stands. Announced once, and again only if the
		/// amount changes.</summary>
		public static string OwedLedgerNote(string Name, int Owed)
		{
			return "{{r|" + (string.IsNullOrEmpty(Name) ? "Somebody" : Name) + " is still owed " + Owed
				+ ((Owed == 1) ? " dram" : " drams") + " for a notice they claimed. It will be paid the day the stores can cover it.}}";
		}

		/// <summary>The chronicle's line for the founder taking a notice down. Always free, and
		/// always remembered.</summary>
		public static string WithdrawnChronicle(string SeatName, BountyTask Task, bool Claimed, string Name)
		{
			string seat = string.IsNullOrEmpty(SeatName) ? "the settlement" : SeatName;
			if (Claimed && !string.IsNullOrEmpty(Name))
			{
				return "the notice at " + seat + " was taken off its stake while " + Name + " was still at it, and nobody was made to give anything back";
			}
			return "the notice at " + seat + " offering water to " + TaskName(Task) + " was taken off its stake, unclaimed and unpaid for";
		}

		/// <summary>The scout's own report, named ground and all. Lower-case clause, no trailing
		/// period.</summary>
		public static string ScoutChronicle(string Name, string SeatName, string GroundName)
		{
			string who = string.IsNullOrEmpty(Name) ? "a scout" : Name;
			string seat = string.IsNullOrEmpty(SeatName) ? "the settlement" : SeatName;
			if (string.IsNullOrEmpty(GroundName))
			{
				return who + " walked the edge of what " + seat + " holds and came back with the shape of the ground beyond it";
			}
			return who + " walked the edge of what " + seat + " holds and came back able to say what lies past it: " + GroundName;
		}

		/// <summary>The deed the settlement is known for after a frontier is walked &mdash; the
		/// same currency every other notable act is recorded in, so word of it draws settlers the
		/// ordinary way.</summary>
		public static string ScoutDeed(string SeatName)
		{
			return "the frontier " + (string.IsNullOrEmpty(SeatName) ? "the settlement" : SeatName) + " walked and mapped";
		}
	}
}
