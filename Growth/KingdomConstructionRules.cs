using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Every costed construction route carried by one durable registry.</summary>
	public enum KingdomConstructionRoute : byte
	{
		None = 0,
		CommissionScaffold = 1,
		PlanScaffold = 2,
		PlotCommission = 3,
		PlotPlan = 4,
		SocketBuild = 5,
		SocketConvert = 6,
		SocketRedress = 7,
		Improvement = 8,
		RoadPaving = 9,
		WearRepair = 10,
		Strike = 11
	}

	/// <summary>The physical fact a route must prove before it may finish.</summary>
	public enum KingdomConstructionProjection : byte
	{
		None = 0,
		Scaffold = 1,
		PlotWorks = 2,
		StrikeOrder = 3,
		Redress = 4,
		Improvement = 5,
		Paving = 6,
		Repair = 7
	}

	/// <summary>
	/// Durable before/after states. Every state ending in <c>Pending</c> is written before the
	/// named external mutation. Finding one after reload is ambiguous and therefore inspect-only:
	/// retrying it would risk a duplicate debit or projection.
	/// </summary>
	public enum KingdomConstructionPhase : byte
	{
		Invalid = 0,
		Published = 1,
		WaterPending = 2,
		WaterSettled = 3,
		MaterialPending = 4,
		Funded = 5,
		ProjectionPending = 6,
		Projected = 7,
		Working = 8,
		Outstanding = 9,
		CompensationPending = 10,
		Compensated = 11,
		Complete = 12,
		Cancelled = 13,
		InspectionRequired = 14
	}

	/// <summary>What a reload is allowed to do without risking a second charge or free result.</summary>
	public enum KingdomConstructionResumeAction : byte
	{
		None = 0,
		ResumeFunding = 1,
		RetryProjection = 2,
		AdvanceWork = 3,
		Inspect = 4
	}

	/// <summary>Whether a newly published action was refused cleanly, funded, or kept as debt.</summary>
	public enum KingdomConstructionStartResult : byte
	{
		Refused = 0,
		Funded = 1,
		Outstanding = 2
	}

	/// <summary>Pure next action for a receipt-bearing single-cell scaffold continuation.</summary>
	public enum KingdomScaffoldContinuationAction : byte
	{
		None = 0,
		AdvanceWork = 1,
		CreateSuccessor = 2,
		RemovePredecessor = 3,
		CompleteReceipt = 4,
		TellCompletion = 5,
		Quarantine = 6
	}

	/// <summary>Durable state of one physical callback chain.</summary>
	public enum KingdomPhysicalPhase : byte
	{
		None = 0,
		OutputIntent = 1,
		StrikeOrdered = 2,
		PlotPartRemovalPending = 3,
		PredecessorRemovalPending = 4,
		PredecessorRemoved = 5,
		SalvageAddPending = 6,
		SalvageSettled = 7,
		SuccessorPending = 8,
		SuccessorSettled = 9,
		TellingsPending = 10,
		Settled = 11,
		Quarantined = 12,
		StrikeStampPending = 13,
		StrikeWorking = 14,
		StrikeWorkComplete = 15,
		StrikeCancellationPending = 16,
		FinalOutputPending = 17,
		FinalOutputSettled = 18,
		FurnishingPending = 19,
		FurnishingSettled = 20,
		FinalRemovalPending = 21,
		FinalRemoved = 22,
		EffectsPending = 23,
		EffectsSettled = 24,
		RoadPlanFrozen = 25,
		RoadOutputPending = 26,
		RoadOutputSettled = 27,
		RoadRemovalPending = 28,
		RoadTallyPending = 29,
		RoadTallySettled = 30
	}

	/// <summary>One durable sink disposition. Attempting is retried only for inspectable sinks.</summary>
	public enum KingdomConstructionSinkDisposition : byte
	{
		None = 0,
		Pending = 1,
		Attempting = 2,
		Delivered = 3,
		Skipped = 4,
		Lost = 5
	}

	/// <summary>Frozen physical facts required after the strike predecessor no longer exists.</summary>
	public sealed class KingdomStrikeIntent
	{
		public string DisplayName;
		public string BuildKey;
		public string TargetDisplayName;
		public string SalvageClaim;
		public bool HasPlot;
		public int X1;
		public int Y1;
		public int X2;
		public int Y2;
		public string PlotId;
		public int Effort;
		public List<KingdomStrikeTarget> Targets;
	}

	/// <summary>One exact plot part frozen when a strike is ordered.</summary>
	public sealed class KingdomStrikeTarget
	{
		public string Id;
		public string Blueprint;
		public int X;
		public int Y;
	}

	/// <summary>Pure recovery decision for one published destructive callback.</summary>
	public enum KingdomExactRemovalAction : byte
	{
		InvokeOnce = 1,
		ProvedAbsent = 2,
		Quarantine = 3
	}

	/// <summary>Exact next action for one persisted integer/list before-after receipt.</summary>
	public enum KingdomConstructionCasAction : byte
	{
		Apply = 1,
		Confirm = 2,
		Quarantine = 3
	}

	/// <summary>Loaded-zone identity result; ambiguity is never treated as absence.</summary>
	public enum KingdomPhysicalLookupState : byte
	{
		Absent = 0,
		Exact = 1,
		Ambiguous = 2
	}

	/// <summary>Exact rooted item location across inventory and Cell.AddObject callbacks.</summary>
	public enum KingdomHandoverItemTopology : byte
	{
		Invalid = 0,
		Source = 1,
		Loose = 2,
		EnteringCell = 3,
		DestinationInventory = 4,
		DestinationCell = 5
	}

	/// <summary>Frozen construction telling, independent of later option changes.</summary>
	public sealed class KingdomConstructionOutbox
	{
		public string EventId;
		public int Mode;
		public string Chronicle;
		public KingdomConstructionSinkDisposition ChronicleState;
		public string Ledger;
		public KingdomConstructionSinkDisposition LedgerState;
		public int LedgerBeforeCount = -1;
		public string LedgerBeforeHash;
		public int LedgerAfterCount = -1;
		public string LedgerAfterHash;
		public string Message;
		public KingdomConstructionSinkDisposition MessageState;
		public string Deed;
		public KingdomConstructionSinkDisposition DeedState;

		public KingdomConstructionOutbox Copy()
		{
			return new KingdomConstructionOutbox
			{
				EventId = EventId,
				Mode = Mode,
				Chronicle = Chronicle,
				ChronicleState = ChronicleState,
				Ledger = Ledger,
				LedgerState = LedgerState,
				LedgerBeforeCount = LedgerBeforeCount,
				LedgerBeforeHash = LedgerBeforeHash,
				LedgerAfterCount = LedgerAfterCount,
				LedgerAfterHash = LedgerAfterHash,
				Message = Message,
				MessageState = MessageState,
				Deed = Deed,
				DeedState = DeedState
			};
		}
	}

	/// <summary>One stable cell in a route payload.</summary>
	public struct KingdomConstructionCell
	{
		public readonly int X;
		public readonly int Y;

		public KingdomConstructionCell(int X, int Y)
		{
			this.X = X;
			this.Y = Y;
		}
	}

	/// <summary>
	/// Persistable measured claims. Material lanes use
	/// <see cref="KingdomMaterialDebitCost.ToClaimString"/>; live engine receipts never cross a
	/// save. <see cref="Exact"/> is false only when an engine receipt could not prove its physical
	/// aftermath, so uncertainty is explicit rather than silently rounded to success or refusal.
	/// </summary>
	public sealed class KingdomConstructionClaims
	{
		public int WaterRequested;
		public int WaterSpent;
		public int WaterOutstanding;
		public int WaterLost;
		public bool Exact;
		public string MaterialRequested;
		public string MaterialSpent;
		public string MaterialOutstanding;
		public string MaterialLost;

		public KingdomConstructionClaims Copy()
		{
			return new KingdomConstructionClaims
			{
				WaterRequested = WaterRequested,
				WaterSpent = WaterSpent,
				WaterOutstanding = WaterOutstanding,
				WaterLost = WaterLost,
				Exact = Exact,
				MaterialRequested = MaterialRequested,
				MaterialSpent = MaterialSpent,
				MaterialOutstanding = MaterialOutstanding,
				MaterialLost = MaterialLost
			};
		}
	}

	/// <summary>One copy-on-write durable construction job.</summary>
	public sealed class KingdomConstructionJob
	{
		public string Id;
		public string OwnerKey;
		public string ZoneId;
		public KingdomConstructionRoute Route;
		public KingdomConstructionPhase Phase;
		public KingdomConstructionProjection Projection;
		public int X;
		public int Y;
		public string SubjectId;
		/// <summary>Immutable exact predecessor captured when intent is first published.</summary>
		public string SourceId;
		/// <summary>Exact generated object ID, published before its first AddObject callback.</summary>
		public string OutputId;
		public KingdomPhysicalPhase PhysicalPhase;
		public int PhysicalIndex;
		public int PhysicalAmount;
		public int PhysicalSpilled;
		public string PhysicalItemId;
		public string PhysicalDestinationId;
		public string PhysicalReceipt;
		public string TargetKey;
		public string Payload;
		public long CreatedTick;
		public long StartedTick;
		public long DueTick;
		public long UpdatedTick;
		public int Revision;
		public KingdomConstructionClaims Claims;
		public string Failure;
		public KingdomConstructionOutbox Outbox;
		/// <summary>A settled terminal row reduced to an immutable replay proof.</summary>
		public bool Compacted;
		/// <summary>SHA-256 of the canonical compact identity/counter record. It proves that
		/// retained replay membership was not edited; it deliberately does not claim to hash
		/// payload/outbox bytes discarded during compaction.</summary>
		public string CompactHash;

		public KingdomConstructionJob Copy()
		{
			return new KingdomConstructionJob
			{
				Id = Id,
				OwnerKey = OwnerKey,
				ZoneId = ZoneId,
				Route = Route,
				Phase = Phase,
				Projection = Projection,
				X = X,
				Y = Y,
				SubjectId = SubjectId,
				SourceId = SourceId,
				OutputId = OutputId,
				PhysicalPhase = PhysicalPhase,
				PhysicalIndex = PhysicalIndex,
				PhysicalAmount = PhysicalAmount,
				PhysicalSpilled = PhysicalSpilled,
				PhysicalItemId = PhysicalItemId,
				PhysicalDestinationId = PhysicalDestinationId,
				PhysicalReceipt = PhysicalReceipt,
				TargetKey = TargetKey,
				Payload = Payload,
				CreatedTick = CreatedTick,
				StartedTick = StartedTick,
				DueTick = DueTick,
				UpdatedTick = UpdatedTick,
				Revision = Revision,
				Claims = Claims == null ? null : Claims.Copy(),
				Failure = Failure,
				Outbox = Outbox == null ? null : Outbox.Copy(),
				Compacted = Compacted,
				CompactHash = CompactHash
			};
		}
	}

	/// <summary>Pure phase, claim, owner, route and registry format laws.</summary>
	public static class KingdomConstructionRules
	{
		public const string FormatHeader = "TAF-CONSTRUCTION-3";
		public const string PriorFormatHeader = "TAF-CONSTRUCTION-2";
		public const string LegacyFormatHeader = "TAF-CONSTRUCTION-1";
		public const int MaxRows = 4096;
		public const int MaxActiveRows = 128;
		public const int MaxRegistryChars = 4194304;
		public const int MaxOwnerChars = 384;
		public const int MaxZoneChars = 512;
		public const int MaxSubjectChars = 128;
		public const int MaxTargetChars = 256;
		public const int MaxPayloadChars = 8192;
		public const int MaxFailureChars = 2048;
		public const int MaxPhysicalReceiptChars = 65536;
		public const int MaxStrikeTargets = 256;
		public const int MaxOutboxTextChars = 4096;
		public const int MaxRouteCells = 128;
		public const int MaxLedgerNotes = 12;

		private static string EmptyCost
		{
			get { return new KingdomMaterialDebitCost().ToClaimString(); }
		}

		/// <summary>
		/// Decides one continuation step from durable phase plus observed physical facts. Creation is
		/// allowed only from <see cref="KingdomConstructionPhase.Outstanding"/>, whose writer has
		/// already proved that an attempted create/Add left no live successor. Pending and inspection
		/// rows never guess across an ambiguous engine callback.
		/// </summary>
		public static KingdomScaffoldContinuationAction ScaffoldContinuation(
			KingdomConstructionPhase Phase, bool PredecessorExact, int ExactSuccessors,
			bool RemovalProved, bool TellingDone)
		{
			if (ExactSuccessors < 0 || ExactSuccessors > 1)
			{
				return KingdomScaffoldContinuationAction.Quarantine;
			}
			if (Phase == KingdomConstructionPhase.Complete)
			{
				if (ExactSuccessors == 0) return KingdomScaffoldContinuationAction.None;
				if (!RemovalProved) return KingdomScaffoldContinuationAction.Quarantine;
				return !TellingDone ? KingdomScaffoldContinuationAction.TellCompletion
					: KingdomScaffoldContinuationAction.None;
			}
			if (Phase == KingdomConstructionPhase.InspectionRequired)
			{
				return KingdomScaffoldContinuationAction.Quarantine;
			}
			if (ExactSuccessors == 1)
			{
				if (PredecessorExact)
				{
					return KingdomScaffoldContinuationAction.RemovePredecessor;
				}
				return RemovalProved
					? KingdomScaffoldContinuationAction.CompleteReceipt
					: KingdomScaffoldContinuationAction.Quarantine;
			}
			if (!PredecessorExact)
			{
				return KingdomScaffoldContinuationAction.Quarantine;
			}
			if (Phase == KingdomConstructionPhase.Working)
			{
				return KingdomScaffoldContinuationAction.AdvanceWork;
			}
			if (Phase == KingdomConstructionPhase.Outstanding)
			{
				return KingdomScaffoldContinuationAction.CreateSuccessor;
			}
			return KingdomScaffoldContinuationAction.Quarantine;
		}

		public static string OwnerKey(string Realm, long FoundedTick, string Settlement)
		{
			string realm = (Realm ?? "").Trim();
			string settlement = (Settlement ?? "").Trim();
			if (realm.Length == 0 || settlement.Length == 0 || FoundedTick < 0L)
			{
				return null;
			}
			string key = "v1:" + FoundedTick.ToString(CultureInfo.InvariantCulture) + ":" + realm.Length.ToString(CultureInfo.InvariantCulture)
				+ ":" + realm + ":" + settlement;
			return key.Length <= MaxOwnerChars ? key : null;
		}

		public static KingdomConstructionProjection ProjectionFor(KingdomConstructionRoute Route)
		{
			switch (Route)
			{
			case KingdomConstructionRoute.CommissionScaffold:
			case KingdomConstructionRoute.PlanScaffold:
				return KingdomConstructionProjection.Scaffold;
			case KingdomConstructionRoute.PlotCommission:
			case KingdomConstructionRoute.PlotPlan:
			case KingdomConstructionRoute.SocketBuild:
				return KingdomConstructionProjection.PlotWorks;
			case KingdomConstructionRoute.SocketConvert:
				return KingdomConstructionProjection.StrikeOrder;
			case KingdomConstructionRoute.SocketRedress:
				return KingdomConstructionProjection.Redress;
			case KingdomConstructionRoute.Improvement:
				return KingdomConstructionProjection.Improvement;
			case KingdomConstructionRoute.RoadPaving:
				return KingdomConstructionProjection.Paving;
			case KingdomConstructionRoute.WearRepair:
				return KingdomConstructionProjection.Repair;
			case KingdomConstructionRoute.Strike:
				return KingdomConstructionProjection.StrikeOrder;
			default:
				return KingdomConstructionProjection.None;
			}
		}

		public static bool IsLongRunning(KingdomConstructionRoute Route)
		{
			return Route == KingdomConstructionRoute.CommissionScaffold
				|| Route == KingdomConstructionRoute.PlanScaffold
				|| Route == KingdomConstructionRoute.PlotCommission
				|| Route == KingdomConstructionRoute.PlotPlan
				|| Route == KingdomConstructionRoute.SocketBuild
				|| Route == KingdomConstructionRoute.SocketConvert
				|| Route == KingdomConstructionRoute.Improvement
				|| Route == KingdomConstructionRoute.WearRepair
				|| Route == KingdomConstructionRoute.Strike;
		}

		public static bool IsTerminal(KingdomConstructionPhase Phase)
		{
			return Phase == KingdomConstructionPhase.Compensated
				|| Phase == KingdomConstructionPhase.Complete
				|| Phase == KingdomConstructionPhase.Cancelled;
		}

		public static bool IsMutationPending(KingdomConstructionPhase Phase)
		{
			return Phase == KingdomConstructionPhase.WaterPending
				|| Phase == KingdomConstructionPhase.MaterialPending
				|| Phase == KingdomConstructionPhase.ProjectionPending
				|| Phase == KingdomConstructionPhase.CompensationPending;
		}

		public static bool SinkSettled(KingdomConstructionSinkDisposition State)
		{
			return State == KingdomConstructionSinkDisposition.Delivered
				|| State == KingdomConstructionSinkDisposition.Skipped
				|| State == KingdomConstructionSinkDisposition.Lost;
		}

		public static bool OutboxSettled(KingdomConstructionOutbox Outbox)
		{
			return Outbox != null && SinkSettled(Outbox.ChronicleState)
				&& SinkSettled(Outbox.LedgerState) && SinkSettled(Outbox.MessageState)
				&& SinkSettled(Outbox.DeedState);
		}

		/// <summary>True only when a terminal row carries the route's final event, not a
		/// settled intermediate event such as socket-staked or conversion-strike.</summary>
		public static bool TerminalClosureSettled(KingdomConstructionJob Job)
		{
			if (Job == null || !IsTerminal(Job.Phase) || !OutboxSettled(Job.Outbox)) return false;
			string suffix;
			if (Job.Phase != KingdomConstructionPhase.Complete) suffix = "closed";
			else
			{
				switch (Job.Route)
				{
				case KingdomConstructionRoute.SocketRedress: suffix = "redressed"; break;
				case KingdomConstructionRoute.RoadPaving: suffix = "paved"; break;
				case KingdomConstructionRoute.WearRepair: suffix = "mended"; break;
				case KingdomConstructionRoute.Strike: suffix = "strike"; break;
				default: suffix = "raised"; break;
				}
			}
			if (Job.Outbox.EventId != "construction:" + Job.Id + ":" + suffix) return false;
			if (Job.Phase != KingdomConstructionPhase.Complete) return true;
			switch (Job.Route)
			{
			case KingdomConstructionRoute.SocketRedress:
			case KingdomConstructionRoute.RoadPaving:
			case KingdomConstructionRoute.WearRepair:
			case KingdomConstructionRoute.Strike:
				return Job.PhysicalPhase == KingdomPhysicalPhase.Settled;
			default:
				return Job.PhysicalPhase == KingdomPhysicalPhase.EffectsSettled;
			}
		}

		/// <summary>
		/// A destructive callback may be invoked only before its pending marker is published.
		/// After publication, absence proves removal only in the same turn as a recorded successful
		/// callback; a reload has no callback tombstone and must quarantine even when loaded lookup
		/// finds nothing.
		/// </summary>
		public static KingdomExactRemovalAction ExactRemovalAction(bool IntentPublished,
			bool CallbackSucceeded, bool ExactReferenceValid, bool ExactIdResolves,
			bool IdentityStillMatches)
		{
			if (!IntentPublished)
			{
				return ExactReferenceValid && ExactIdResolves && IdentityStillMatches
					? KingdomExactRemovalAction.InvokeOnce
					: KingdomExactRemovalAction.Quarantine;
			}
			return CallbackSucceeded && !ExactReferenceValid && !ExactIdResolves
				? KingdomExactRemovalAction.ProvedAbsent
				: KingdomExactRemovalAction.Quarantine;
		}

		public static bool ValidOutbox(KingdomConstructionOutbox Outbox)
		{
			if (Outbox == null) return true;
			if (!TextLength(Outbox.EventId, 1, 256) || Outbox.Mode < 1 || Outbox.Mode > 3
				|| !TextLength(Outbox.Chronicle, 0, MaxOutboxTextChars)
				|| !TextLength(Outbox.Ledger, 0, MaxOutboxTextChars)
				|| !TextLength(Outbox.Message, 0, MaxOutboxTextChars)
				|| !TextLength(Outbox.Deed, 0, MaxOutboxTextChars)
				|| !ValidSink(Outbox.Chronicle, Outbox.ChronicleState)
				|| !ValidSink(Outbox.Ledger, Outbox.LedgerState)
				|| !ValidSink(Outbox.Message, Outbox.MessageState)
				|| !ValidSink(Outbox.Deed, Outbox.DeedState)) return false;
			bool receiptEmpty = Outbox.LedgerBeforeCount == -1
				&& string.IsNullOrEmpty(Outbox.LedgerBeforeHash)
				&& Outbox.LedgerAfterCount == -1
				&& string.IsNullOrEmpty(Outbox.LedgerAfterHash);
			bool receiptComplete = Outbox.LedgerBeforeCount >= 0
				&& Outbox.LedgerBeforeCount < MaxLedgerNotes
				&& Outbox.LedgerAfterCount == Outbox.LedgerBeforeCount + 1
				&& IsSha256(Outbox.LedgerBeforeHash) && IsSha256(Outbox.LedgerAfterHash);
			if (!receiptEmpty && !receiptComplete) return false;
			if (Outbox.LedgerState == KingdomConstructionSinkDisposition.Attempting
				&& !receiptComplete) return false;
			return true;
		}

		public static bool TryCounterAfter(int Before, int Delta, out int After)
		{
			After = 0;
			if (Delta <= 0 || Before < 0 || Before > int.MaxValue - Delta) return false;
			After = Before + Delta;
			return true;
		}

		public static KingdomConstructionCasAction CounterCasAction(int Current,
			int Before, int After)
		{
			if (Before < 0 || After <= Before) return KingdomConstructionCasAction.Quarantine;
			if (Current == Before) return KingdomConstructionCasAction.Apply;
			return Current == After ? KingdomConstructionCasAction.Confirm
				: KingdomConstructionCasAction.Quarantine;
		}

		/// <summary>Strong, length-framed hash for an inspectable ledger snapshot.</summary>
		public static string HashLedger(IList<string> Notes)
		{
			if (Notes == null || Notes.Count > MaxLedgerNotes) return null;
			StringBuilder framed = new StringBuilder();
			for (int i = 0; i < Notes.Count; i++)
			{
				string note = Notes[i] ?? "";
				if (note.Length > MaxOutboxTextChars) return null;
				framed.Append(note.Length.ToString(CultureInfo.InvariantCulture)).Append(':')
					.Append(note);
			}
			return Sha256(framed.ToString());
		}

		public static bool TryFreezeLedger(IList<string> Notes, string Entry,
			out int BeforeCount, out string BeforeHash, out int AfterCount,
			out string AfterHash)
		{
			BeforeCount = AfterCount = -1;
			BeforeHash = AfterHash = null;
			if (Notes == null || Notes.Count >= MaxLedgerNotes
				|| !TextLength(Entry, 1, MaxOutboxTextChars)) return false;
			BeforeCount = Notes.Count;
			BeforeHash = HashLedger(Notes);
			List<string> after = new List<string>(Notes);
			after.Add(Entry);
			AfterCount = after.Count;
			AfterHash = HashLedger(after);
			return IsSha256(BeforeHash) && IsSha256(AfterHash);
		}

		public static KingdomConstructionCasAction LedgerCasAction(IList<string> Notes,
			int BeforeCount, string BeforeHash, int AfterCount, string AfterHash)
		{
			if (Notes == null || BeforeCount < 0 || AfterCount != BeforeCount + 1
				|| !IsSha256(BeforeHash) || !IsSha256(AfterHash))
				return KingdomConstructionCasAction.Quarantine;
			string hash = HashLedger(Notes);
			if (Notes.Count == BeforeCount && hash == BeforeHash)
				return KingdomConstructionCasAction.Apply;
			return Notes.Count == AfterCount && hash == AfterHash
				? KingdomConstructionCasAction.Confirm : KingdomConstructionCasAction.Quarantine;
		}

		public static string InterruptedFundingDiagnostic(KingdomConstructionPhase Phase)
		{
			if (Phase == KingdomConstructionPhase.WaterPending)
				return "A save interrupted the aggregate water debit; exact vessel bindings were not persisted. Inspect stores; automatic recharge is disabled.";
			if (Phase == KingdomConstructionPhase.MaterialPending)
				return "A save interrupted the aggregate material debit; exact source bindings were not persisted. Inspect stores; automatic recharge is disabled.";
			return null;
		}

		public static bool CanSupersedeTerminal(KingdomConstructionJob Job,
			string OwnerKey, string ZoneId, string ReceiptId, string ObjectId)
		{
			if (Job == null || Job.Id != ReceiptId || Job.OwnerKey != OwnerKey
				|| Job.ZoneId != ZoneId || !IsTerminal(Job.Phase)
				|| (!Job.Compacted && !TerminalClosureSettled(Job))
				|| string.IsNullOrEmpty(ObjectId)) return false;
			return Job.OutputId == ObjectId || (string.IsNullOrEmpty(Job.OutputId)
				&& Job.SourceId == ObjectId && Job.SubjectId == ObjectId);
		}

		/// <summary>Last row/active slot is reserved for one durable saturation diagnostic.</summary>
		public static bool CapacityInspectionRequired(int TotalRows, int ActiveRows)
		{
			return TotalRows >= MaxRows - 1 || ActiveRows >= MaxActiveRows - 1;
		}

		private static bool ValidSink(string Text, KingdomConstructionSinkDisposition State)
		{
			if (State <= KingdomConstructionSinkDisposition.None
				|| State > KingdomConstructionSinkDisposition.Lost) return false;
			return State == KingdomConstructionSinkDisposition.Skipped
				? string.IsNullOrEmpty(Text) : !string.IsNullOrEmpty(Text);
		}

		public static KingdomConstructionResumeAction ResumeAction(KingdomConstructionJob Job)
		{
			if (Job == null || Job.Claims == null || IsTerminal(Job.Phase))
			{
				return KingdomConstructionResumeAction.None;
			}
			if (!Job.Claims.Exact || IsMutationPending(Job.Phase)
				|| Job.Phase == KingdomConstructionPhase.InspectionRequired)
			{
				return KingdomConstructionResumeAction.Inspect;
			}
			if (Job.Phase == KingdomConstructionPhase.Published
				|| Job.Phase == KingdomConstructionPhase.WaterSettled
				|| Job.Claims.WaterOutstanding > 0 || !MaterialOutstanding(Job.Claims).IsEmpty)
			{
				return KingdomConstructionResumeAction.ResumeFunding;
			}
			if (Job.Phase == KingdomConstructionPhase.Funded
				|| Job.Phase == KingdomConstructionPhase.Outstanding)
			{
				return KingdomConstructionResumeAction.RetryProjection;
			}
			if (Job.Phase == KingdomConstructionPhase.Projected || Job.Phase == KingdomConstructionPhase.Working)
			{
				return KingdomConstructionResumeAction.AdvanceWork;
			}
			return KingdomConstructionResumeAction.Inspect;
		}

		public static KingdomConstructionClaims NewClaims(int Water, KingdomMaterialDebitCost Material)
		{
			int water = Water > 0 ? Water : 0;
			string requested = (Material ?? new KingdomMaterialDebitCost()).ToClaimString();
			return new KingdomConstructionClaims
			{
				WaterRequested = water,
				WaterOutstanding = water,
				Exact = true,
				MaterialRequested = requested,
				MaterialSpent = EmptyCost,
				MaterialOutstanding = requested,
				MaterialLost = EmptyCost
			};
		}

		public static bool FullyFundedExact(KingdomConstructionJob Job)
		{
			KingdomMaterialDebitCost outstanding;
			return Job != null && Job.Claims != null && Job.Claims.Exact
				&& ValidateClaims(Job.Claims)
				&& Job.Claims.WaterOutstanding == 0
				&& KingdomMaterialDebitCost.TryParseClaim(Job.Claims.MaterialOutstanding,
					out outstanding) && outstanding.IsEmpty;
		}

		public static KingdomConstructionClaims ApplyWaterCommit(KingdomConstructionClaims Claims,
			bool Committed, bool RestorationExact)
		{
			KingdomConstructionClaims next = Claims.Copy();
			if (Committed)
			{
				next.WaterSpent = next.WaterRequested;
				next.WaterOutstanding = 0;
				next.WaterLost = next.WaterRequested;
			}
			else if (!RestorationExact)
			{
				next.Exact = false;
			}
			return next;
		}

		/// <summary>Merges one measured debit whose request equals current water outstanding.</summary>
		public static bool TryApplyWaterAttempt(KingdomConstructionClaims Claims,
			int Requested, int Spent, int Outstanding, int Lost, bool Exact,
			out KingdomConstructionClaims Next)
		{
			Next = null;
			if (Claims == null || Requested != Claims.WaterOutstanding || Requested < 0
				|| Spent < 0 || Outstanding < 0 || Lost < Spent
				|| (long)Spent + Outstanding != Requested
				|| (long)Claims.WaterSpent + Spent > int.MaxValue
				|| (long)Claims.WaterLost + Lost > int.MaxValue)
			{
				return false;
			}
			KingdomConstructionClaims next = Claims.Copy();
			next.WaterSpent += Spent;
			next.WaterOutstanding = Outstanding;
			next.WaterLost += Lost;
			next.Exact &= Exact;
			if (!ValidateClaims(next))
			{
				return false;
			}
			Next = next;
			return true;
		}

		public static KingdomConstructionClaims ApplyWaterRollback(KingdomConstructionClaims Claims,
			bool RestoredExact)
		{
			KingdomConstructionClaims next = Claims.Copy();
			if (RestoredExact)
			{
				next.WaterSpent = 0;
				next.WaterOutstanding = next.WaterRequested;
				next.WaterLost = 0;
			}
			else
			{
				next.Exact = false;
			}
			return next;
		}

		/// <summary>Merges a receipt requested against the job's current outstanding claim.</summary>
		public static bool TryApplyMaterial(KingdomConstructionClaims Claims,
			KingdomMaterialDebitResult Result, out KingdomConstructionClaims Next)
		{
			Next = null;
			if (Claims == null || Result == null)
			{
				return false;
			}
			KingdomMaterialDebitCost outstanding;
			if (!KingdomMaterialDebitCost.TryParseClaim(Claims.MaterialOutstanding, out outstanding)
				|| !SameCost(outstanding, Result.Requested))
			{
				return false;
			}
			KingdomMaterialDebitCost spent;
			KingdomMaterialDebitCost lost;
			if (!KingdomMaterialDebitCost.TryParseClaim(Claims.MaterialSpent, out spent)
				|| !KingdomMaterialDebitCost.TryParseClaim(Claims.MaterialLost, out lost))
			{
				return false;
			}
			KingdomConstructionClaims next = Claims.Copy();
			next.MaterialSpent = AddCost(spent, Result.Spent).ToClaimString();
			next.MaterialOutstanding = Result.Outstanding.ToClaimString();
			next.MaterialLost = AddCost(lost, Result.Lost).ToClaimString();
			Next = next;
			return ValidateClaims(next);
		}

		public static KingdomConstructionJob Transition(KingdomConstructionJob Job,
			KingdomConstructionPhase Phase, long Tick, string Failure = null)
		{
			KingdomConstructionJob next = Job.Copy();
			next.Phase = Phase;
			next.UpdatedTick = Tick >= next.UpdatedTick ? Tick : next.UpdatedTick;
			next.Revision = next.Revision < int.MaxValue ? next.Revision + 1 : next.Revision;
			next.Failure = Limit(Failure, MaxFailureChars);
			return next;
		}

		public static bool ValidRegistryUpdate(KingdomConstructionJob Current,
			KingdomConstructionJob Next)
		{
			if (!ValidJob(Current) || !ValidJob(Next) || Current.Revision == int.MaxValue
				|| Next.Revision != Current.Revision + 1 || Current.Id != Next.Id
				|| Current.OwnerKey != Next.OwnerKey || Current.ZoneId != Next.ZoneId
				|| Current.Route != Next.Route || Current.Projection != Next.Projection
				|| Current.X != Next.X || Current.Y != Next.Y
				|| Current.TargetKey != Next.TargetKey
				|| Current.CreatedTick != Next.CreatedTick
				|| Next.UpdatedTick < Current.UpdatedTick
				|| (RequiresFullFunding(Next.Phase) && !FullyFundedExact(Next))
				|| !ValidPhaseUpdate(Current, Next)
				|| (IsTerminal(Current.Phase) && Next.Phase != Current.Phase)
				|| (Current.Compacted && !Next.Compacted)) return false;
			return true;
		}

		private static bool RequiresFullFunding(KingdomConstructionPhase Phase)
		{
			return Phase == KingdomConstructionPhase.Funded
				|| Phase == KingdomConstructionPhase.ProjectionPending
				|| Phase == KingdomConstructionPhase.Projected
				|| Phase == KingdomConstructionPhase.Working
				|| Phase == KingdomConstructionPhase.Complete;
		}

		private static bool ValidPhaseUpdate(KingdomConstructionJob Current,
			KingdomConstructionJob Next)
		{
			if (Next.Phase == Current.Phase) return true;
			if (IsTerminal(Current.Phase)) return false;
			if (Next.Phase == KingdomConstructionPhase.InspectionRequired
				|| Next.Phase == KingdomConstructionPhase.Cancelled) return true;
			if (Next.Phase == KingdomConstructionPhase.Complete)
				return FullyFundedExact(Next)
					&& (Current.Phase == KingdomConstructionPhase.Funded
						|| Current.Phase == KingdomConstructionPhase.ProjectionPending
						|| Current.Phase == KingdomConstructionPhase.Projected
						|| Current.Phase == KingdomConstructionPhase.Working
						|| Current.Phase == KingdomConstructionPhase.Outstanding);
			switch (Current.Phase)
			{
				case KingdomConstructionPhase.Published:
					return Next.Phase == KingdomConstructionPhase.WaterPending;
				case KingdomConstructionPhase.WaterPending:
					return Next.Phase == KingdomConstructionPhase.WaterSettled;
				case KingdomConstructionPhase.WaterSettled:
					return Next.Phase == KingdomConstructionPhase.WaterPending
						|| Next.Phase == KingdomConstructionPhase.MaterialPending
						|| Next.Phase == KingdomConstructionPhase.Compensated;
				case KingdomConstructionPhase.MaterialPending:
					return Next.Phase == KingdomConstructionPhase.Funded
						|| Next.Phase == KingdomConstructionPhase.Outstanding
						|| Next.Phase == KingdomConstructionPhase.CompensationPending;
				case KingdomConstructionPhase.CompensationPending:
					return Next.Phase == KingdomConstructionPhase.Compensated
						|| Next.Phase == KingdomConstructionPhase.Outstanding;
				case KingdomConstructionPhase.Funded:
				case KingdomConstructionPhase.Projected:
				case KingdomConstructionPhase.Working:
					return Next.Phase == KingdomConstructionPhase.ProjectionPending
						|| Next.Phase == KingdomConstructionPhase.Working
						|| Next.Phase == KingdomConstructionPhase.Outstanding;
				case KingdomConstructionPhase.ProjectionPending:
					return Next.Phase == KingdomConstructionPhase.Projected
						|| Next.Phase == KingdomConstructionPhase.Working
						|| Next.Phase == KingdomConstructionPhase.Outstanding;
				case KingdomConstructionPhase.Outstanding:
					return Next.Phase == KingdomConstructionPhase.WaterPending
						|| Next.Phase == KingdomConstructionPhase.ProjectionPending
						|| Next.Phase == KingdomConstructionPhase.Working;
				default:
					return false;
			}
		}

		public static KingdomPhysicalLookupState PhysicalLookupState(int Count,
			bool ExactShape)
		{
			return Count == 0 ? KingdomPhysicalLookupState.Absent
				: Count == 1 && ExactShape ? KingdomPhysicalLookupState.Exact
				: KingdomPhysicalLookupState.Ambiguous;
		}

		/// <param name="InventoryOwner">0 none, 1 source, 2 destination, 3 other.</param>
		/// <param name="CellOwner">0 none, 1 exact destination, 2 other.</param>
		public static KingdomHandoverItemTopology HandoverItemTopology(int SourceRefs,
			int DestinationRefs, int CellRefs, int IdOccurrences, int ExactOccurrences,
			int InventoryOwner, int CellOwner)
		{
			if (SourceRefs < 0 || DestinationRefs < 0 || CellRefs < 0
				|| IdOccurrences < 0 || ExactOccurrences < 0 || ExactOccurrences > IdOccurrences
				|| InventoryOwner < 0 || InventoryOwner > 3 || CellOwner < 0 || CellOwner > 2)
				return KingdomHandoverItemTopology.Invalid;
			if (SourceRefs == 1 && DestinationRefs == 0 && CellRefs == 0
				&& IdOccurrences == 1 && ExactOccurrences == 1
				&& InventoryOwner == 1 && CellOwner == 0)
				return KingdomHandoverItemTopology.Source;
			if (SourceRefs == 0 && DestinationRefs == 1 && CellRefs == 0
				&& IdOccurrences == 1 && ExactOccurrences == 1
				&& InventoryOwner == 2 && CellOwner == 0)
				return KingdomHandoverItemTopology.DestinationInventory;
			if (SourceRefs == 0 && DestinationRefs == 0 && CellRefs == 1
				&& IdOccurrences == 1 && ExactOccurrences == 1
				&& InventoryOwner == 0 && CellOwner == 1)
				return KingdomHandoverItemTopology.DestinationCell;
			if (SourceRefs == 0 && DestinationRefs == 0 && CellRefs == 0
				&& IdOccurrences == 0 && ExactOccurrences == 0 && InventoryOwner == 0)
				return CellOwner == 0 ? KingdomHandoverItemTopology.Loose
					: CellOwner == 1 ? KingdomHandoverItemTopology.EnteringCell
					: KingdomHandoverItemTopology.Invalid;
			return KingdomHandoverItemTopology.Invalid;
		}

		public static bool ValidJob(KingdomConstructionJob Job)
		{
			Guid ignored;
			if (Job == null || !Guid.TryParseExact(Job.Id, "N", out ignored)
				|| !TextLength(Job.OwnerKey, 1, MaxOwnerChars)
				|| !TextLength(Job.ZoneId, 1, MaxZoneChars)
				|| Job.Route <= KingdomConstructionRoute.None || Job.Route > KingdomConstructionRoute.Strike
				|| Job.Phase <= KingdomConstructionPhase.Invalid || Job.Phase > KingdomConstructionPhase.InspectionRequired
				|| Job.Projection != ProjectionFor(Job.Route)
				|| Job.X < -1 || Job.X > 1023 || Job.Y < -1 || Job.Y > 1023
				|| !TextLength(Job.SubjectId, 0, MaxSubjectChars)
				|| !TextLength(Job.SourceId, 0, MaxSubjectChars)
				|| !TextLength(Job.OutputId, 0, MaxSubjectChars)
				|| Job.PhysicalPhase < KingdomPhysicalPhase.None
				|| Job.PhysicalPhase > KingdomPhysicalPhase.RoadTallySettled
				|| Job.PhysicalIndex < 0 || Job.PhysicalIndex > 4096
				|| Job.PhysicalAmount < 0 || Job.PhysicalSpilled < 0
				|| !TextLength(Job.PhysicalItemId, 0, MaxSubjectChars)
				|| !TextLength(Job.PhysicalDestinationId, 0, MaxSubjectChars)
				|| !TextLength(Job.PhysicalReceipt, 0, MaxPhysicalReceiptChars)
				|| !TextLength(Job.TargetKey, 0, MaxTargetChars)
				|| !TextLength(Job.Payload, 0, MaxPayloadChars)
				|| !TextLength(Job.Failure, 0, MaxFailureChars)
				|| Job.CreatedTick < 0L || Job.StartedTick < Job.CreatedTick
				|| Job.DueTick < Job.StartedTick
				|| Job.UpdatedTick < Job.CreatedTick || Job.Revision < 1
				|| !ValidateClaims(Job.Claims) || !ValidOutbox(Job.Outbox))
			{
				return false;
			}
			if (Job.Compacted)
			{
				return IsTerminal(Job.Phase) && Job.Outbox == null
					&& string.IsNullOrEmpty(Job.Payload)
					&& string.IsNullOrEmpty(Job.PhysicalReceipt)
					&& string.IsNullOrEmpty(Job.Failure)
					&& IsSha256(Job.CompactHash) && Job.CompactHash == CompactIdentityHash(Job);
			}
			if (!string.IsNullOrEmpty(Job.CompactHash)) return false;
			return true;
		}

		public static bool ValidateClaims(KingdomConstructionClaims Claims)
		{
			if (Claims == null || Claims.WaterRequested < 0 || Claims.WaterSpent < 0
				|| Claims.WaterOutstanding < 0 || Claims.WaterLost < 0
				|| (long)Claims.WaterSpent + Claims.WaterOutstanding != Claims.WaterRequested
				|| Claims.WaterLost < Claims.WaterSpent)
			{
				return false;
			}
			KingdomMaterialDebitCost requested;
			KingdomMaterialDebitCost spent;
			KingdomMaterialDebitCost outstanding;
			KingdomMaterialDebitCost lost;
			if (!KingdomMaterialDebitCost.TryParseClaim(Claims.MaterialRequested, out requested)
				|| !KingdomMaterialDebitCost.TryParseClaim(Claims.MaterialSpent, out spent)
				|| !KingdomMaterialDebitCost.TryParseClaim(Claims.MaterialOutstanding, out outstanding)
				|| !KingdomMaterialDebitCost.TryParseClaim(Claims.MaterialLost, out lost))
			{
				return false;
			}
			return SumMatches(requested, spent, outstanding) && Covers(lost, spent);
		}

		public static bool TryEncode(IList<KingdomConstructionJob> Jobs, out string Text)
		{
			Text = null;
			List<KingdomConstructionJob> rows;
			if (!TryNormalize(Jobs, out rows))
			{
				return false;
			}
			StringBuilder output = new StringBuilder(FormatHeader);
			for (int i = 0; i < rows.Count; i++)
			{
				KingdomConstructionJob row = rows[i];
				KingdomConstructionClaims claim = row.Claims;
				KingdomConstructionOutbox box = row.Outbox;
				output.Append('\n').Append(row.Id).Append('|')
					.Append(EncodeText(row.OwnerKey)).Append('|').Append(EncodeText(row.ZoneId)).Append('|')
					.Append((int)row.Route).Append('|').Append((int)row.Phase).Append('|').Append((int)row.Projection).Append('|')
					.Append(row.X.ToString(CultureInfo.InvariantCulture)).Append('|').Append(row.Y.ToString(CultureInfo.InvariantCulture)).Append('|')
					.Append(EncodeText(row.SubjectId)).Append('|').Append(EncodeText(row.SourceId)).Append('|')
					.Append(EncodeText(row.OutputId)).Append('|').Append((int)row.PhysicalPhase).Append('|')
					.Append(row.PhysicalIndex.ToString(CultureInfo.InvariantCulture)).Append('|')
					.Append(row.PhysicalAmount.ToString(CultureInfo.InvariantCulture)).Append('|')
					.Append(row.PhysicalSpilled.ToString(CultureInfo.InvariantCulture)).Append('|')
					.Append(EncodeText(row.PhysicalItemId)).Append('|').Append(EncodeText(row.PhysicalDestinationId)).Append('|')
					.Append(EncodeText(row.PhysicalReceipt)).Append('|')
					.Append(EncodeText(row.TargetKey)).Append('|').Append(EncodeText(row.Payload)).Append('|')
					.Append(row.CreatedTick.ToString(CultureInfo.InvariantCulture)).Append('|').Append(row.StartedTick.ToString(CultureInfo.InvariantCulture)).Append('|')
					.Append(row.DueTick.ToString(CultureInfo.InvariantCulture)).Append('|').Append(row.UpdatedTick.ToString(CultureInfo.InvariantCulture)).Append('|')
					.Append(row.Revision.ToString(CultureInfo.InvariantCulture)).Append('|')
					.Append(claim.WaterRequested.ToString(CultureInfo.InvariantCulture)).Append('|').Append(claim.WaterSpent.ToString(CultureInfo.InvariantCulture)).Append('|')
					.Append(claim.WaterOutstanding.ToString(CultureInfo.InvariantCulture)).Append('|').Append(claim.WaterLost.ToString(CultureInfo.InvariantCulture)).Append('|')
					.Append(claim.Exact ? '1' : '0').Append('|')
					.Append(EncodeText(claim.MaterialRequested)).Append('|').Append(EncodeText(claim.MaterialSpent)).Append('|')
					.Append(EncodeText(claim.MaterialOutstanding)).Append('|').Append(EncodeText(claim.MaterialLost)).Append('|')
					.Append(EncodeText(row.Failure)).Append('|')
					.Append(EncodeText(box == null ? null : box.EventId)).Append('|')
					.Append(box == null ? 0 : box.Mode).Append('|')
					.Append(EncodeText(box == null ? null : box.Chronicle)).Append('|')
					.Append(box == null ? 0 : (int)box.ChronicleState).Append('|')
					.Append(EncodeText(box == null ? null : box.Ledger)).Append('|')
					.Append(box == null ? 0 : (int)box.LedgerState).Append('|')
					.Append(EncodeText(box == null ? null : box.Message)).Append('|')
					.Append(box == null ? 0 : (int)box.MessageState).Append('|')
					.Append(EncodeText(box == null ? null : box.Deed)).Append('|')
					.Append(box == null ? 0 : (int)box.DeedState).Append('|')
					.Append(box == null ? -1 : box.LedgerBeforeCount).Append('|')
					.Append(EncodeText(box == null ? null : box.LedgerBeforeHash)).Append('|')
					.Append(box == null ? -1 : box.LedgerAfterCount).Append('|')
					.Append(EncodeText(box == null ? null : box.LedgerAfterHash)).Append('|')
					.Append(row.Compacted ? '1' : '0').Append('|')
					.Append(EncodeText(row.CompactHash));
				if (output.Length > MaxRegistryChars)
				{
					return false;
				}
			}
			Text = output.ToString();
			return true;
		}

		public static bool TryDecode(string Text, out List<KingdomConstructionJob> Jobs)
		{
			Jobs = null;
			if (Text == null || Text.Length > MaxRegistryChars)
			{
				return false;
			}
			string[] lines = Text.Split('\n');
			bool legacy = lines.Length > 0 && lines[0] == LegacyFormatHeader;
			bool prior = lines.Length > 0 && lines[0] == PriorFormatHeader;
			if (lines.Length == 0 || (!legacy && !prior && lines[0] != FormatHeader)
				|| lines.Length - 1 > MaxRows)
			{
				return false;
			}
			List<KingdomConstructionJob> rows = new List<KingdomConstructionJob>();
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 1; i < lines.Length; i++)
			{
				if (lines[i].Length == 0)
				{
					return false;
				}
				KingdomConstructionJob row;
				if (!TryDecodeRow(lines[i], legacy, prior, out row) || !ids.Add(row.Id))
				{
					return false;
				}
				rows.Add(row);
			}
			List<KingdomConstructionJob> normalized;
			if (!TryNormalize(rows, out normalized))
			{
				return false;
			}
			Jobs = normalized;
			return true;
		}

		/// <summary>Canonical sort plus lossless terminal compaction. Never drops replay IDs.</summary>
		public static bool TryNormalize(IList<KingdomConstructionJob> Jobs,
			out List<KingdomConstructionJob> Normalized)
		{
			Normalized = null;
			if (Jobs == null)
			{
				return false;
			}
			List<KingdomConstructionJob> active = new List<KingdomConstructionJob>();
			List<KingdomConstructionJob> terminal = new List<KingdomConstructionJob>();
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Jobs.Count; i++)
			{
				KingdomConstructionJob row = Jobs[i];
				if (!ValidJob(row) || !ids.Add(row.Id))
				{
					return false;
				}
				KingdomConstructionJob copy = row.Copy();
				// Missing/unsettled telling remains active so dispatcher can reconstruct/retry it.
				if (copy.Compacted)
				{
					terminal.Add(copy);
				}
				else if (TerminalClosureSettled(copy)
					&& copy.PhysicalPhase != KingdomPhysicalPhase.TellingsPending)
				{
					terminal.Add(Compact(copy));
				}
				else
				{
					active.Add(copy);
				}
			}
			if (active.Count > MaxActiveRows || Jobs.Count > MaxRows)
			{
				return false;
			}
			active.AddRange(terminal);
			active.Sort(CompareCanonical);
			Normalized = active;
			return true;
		}

		public static bool TryEncodeCells(IList<KingdomConstructionCell> Cells, out string Payload)
		{
			Payload = null;
			if (Cells == null || Cells.Count <= 0 || Cells.Count > MaxRouteCells)
			{
				return false;
			}
			StringBuilder text = new StringBuilder("v1");
			HashSet<int> seen = new HashSet<int>();
			for (int i = 0; i < Cells.Count; i++)
			{
				int x = Cells[i].X;
				int y = Cells[i].Y;
				int packed = x + y * 1024;
				if (x < 0 || x > 1023 || y < 0 || y > 1023 || !seen.Add(packed))
				{
					return false;
				}
				text.Append(';').Append(x.ToString(CultureInfo.InvariantCulture)).Append(',')
					.Append(y.ToString(CultureInfo.InvariantCulture));
			}
			Payload = text.ToString();
			return true;
		}

		/// <summary>Canonical, bounded physical strike receipt.</summary>
		public static bool TryEncodeStrikeIntent(KingdomStrikeIntent Intent, out string Receipt)
		{
			Receipt = null;
			KingdomMaterialDebitCost salvage;
			if (Intent == null || !TextLength(Intent.DisplayName, 1, 512)
				|| !TextLength(Intent.BuildKey, 0, MaxTargetChars)
				|| !TextLength(Intent.TargetDisplayName, 0, 512)
				|| !TextLength(Intent.PlotId, 0, MaxSubjectChars)
				|| Intent.Effort <= 0 || Intent.Effort > int.MaxValue
				|| Intent.Targets == null || Intent.Targets.Count > MaxStrikeTargets
				|| !KingdomMaterialDebitCost.TryParseClaim(Intent.SalvageClaim, out salvage)
				|| !salvage.Bits.IsEmpty() || !salvage.Exotics.IsEmpty())
			{
				return false;
			}
			if (Intent.HasPlot)
			{
				if (Intent.X1 < 0 || Intent.X1 > Intent.X2 || Intent.X2 > 1023
					|| Intent.Y1 < 0 || Intent.Y1 > Intent.Y2 || Intent.Y2 > 1023
					|| string.IsNullOrEmpty(Intent.PlotId)) return false;
			}
			else if (Intent.X1 != -1 || Intent.Y1 != -1 || Intent.X2 != -1
				|| Intent.Y2 != -1 || !string.IsNullOrEmpty(Intent.PlotId)
				|| Intent.Targets.Count != 0) return false;
			List<KingdomStrikeTarget> targets = new List<KingdomStrikeTarget>(Intent.Targets);
			targets.Sort(delegate(KingdomStrikeTarget a, KingdomStrikeTarget b)
			{
				int compare = a.Y.CompareTo(b.Y);
				if (compare != 0) return compare;
				compare = a.X.CompareTo(b.X);
				return compare != 0 ? compare : string.CompareOrdinal(a.Id, b.Id);
			});
			StringBuilder targetText = new StringBuilder();
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < targets.Count; i++)
			{
				KingdomStrikeTarget target = targets[i];
				if (target == null || !TextLength(target.Id, 1, MaxSubjectChars)
					|| !TextLength(target.Blueprint, 1, MaxTargetChars)
					|| target.X < Intent.X1 || target.X > Intent.X2
					|| target.Y < Intent.Y1 || target.Y > Intent.Y2 || !ids.Add(target.Id))
					return false;
				if (i > 0) targetText.Append(';');
				targetText.Append(EncodeText(target.Id)).Append(',')
					.Append(EncodeText(target.Blueprint)).Append(',')
					.Append(target.X.ToString(CultureInfo.InvariantCulture)).Append(',')
					.Append(target.Y.ToString(CultureInfo.InvariantCulture));
			}
			string text = "v2|" + EncodeText(Intent.DisplayName) + "|"
				+ EncodeText(Intent.BuildKey) + "|" + EncodeText(Intent.TargetDisplayName) + "|"
				+ EncodeText(Intent.SalvageClaim) + "|"
				+ (Intent.HasPlot ? "1" : "0") + "|"
				+ Intent.X1.ToString(CultureInfo.InvariantCulture) + "|"
				+ Intent.Y1.ToString(CultureInfo.InvariantCulture) + "|"
				+ Intent.X2.ToString(CultureInfo.InvariantCulture) + "|"
				+ Intent.Y2.ToString(CultureInfo.InvariantCulture) + "|"
				+ EncodeText(Intent.PlotId) + "|"
				+ Intent.Effort.ToString(CultureInfo.InvariantCulture) + "|" + targetText;
			if (text.Length > MaxPhysicalReceiptChars) return false;
			Receipt = text;
			return true;
		}

		public static bool TryDecodeStrikeIntent(string Receipt, out KingdomStrikeIntent Intent)
		{
			Intent = null;
			if (string.IsNullOrEmpty(Receipt) || Receipt.Length > MaxPhysicalReceiptChars)
				return false;
			string[] f = Receipt.Split('|');
			string displayName, buildKey, targetDisplayName, salvageClaim, plotId;
			int x1, y1, x2, y2;
			if (!((f.Length == 11 && f[0] == "v1")
					|| (f.Length == 13 && f[0] == "v2"))
				|| (f[5] != "0" && f[5] != "1")
				|| !TryDecodeText(f[1], 512, out displayName)
				|| !TryDecodeText(f[2], MaxTargetChars, out buildKey)
				|| !TryDecodeText(f[3], 512, out targetDisplayName)
				|| !TryDecodeText(f[4], 4096, out salvageClaim)
				|| !TryInt(f[6], -1, 1023, out x1) || !TryInt(f[7], -1, 1023, out y1)
				|| !TryInt(f[8], -1, 1023, out x2) || !TryInt(f[9], -1, 1023, out y2)
				|| !TryDecodeText(f[10], MaxSubjectChars, out plotId)) return false;
			KingdomStrikeIntent parsed = new KingdomStrikeIntent
			{
				DisplayName = displayName, BuildKey = buildKey,
				TargetDisplayName = targetDisplayName, SalvageClaim = salvageClaim,
				HasPlot = f[5] == "1", X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
				PlotId = plotId, Effort = 0, Targets = null
			};
			if (f[0] == "v1")
			{
				// Legacy receipts did not freeze exact plot-part IDs or effort. They remain
				// readable only so execution can fail closed; never infer a new target set.
				string legacy = "v1|" + EncodeText(parsed.DisplayName) + "|"
					+ EncodeText(parsed.BuildKey) + "|" + EncodeText(parsed.TargetDisplayName) + "|"
					+ EncodeText(parsed.SalvageClaim) + "|" + (parsed.HasPlot ? "1" : "0") + "|"
					+ parsed.X1.ToString(CultureInfo.InvariantCulture) + "|"
					+ parsed.Y1.ToString(CultureInfo.InvariantCulture) + "|"
					+ parsed.X2.ToString(CultureInfo.InvariantCulture) + "|"
					+ parsed.Y2.ToString(CultureInfo.InvariantCulture) + "|" + EncodeText(parsed.PlotId);
				if (legacy != Receipt) return false;
				Intent = parsed;
				return true;
			}
			int effort;
			if (!TryInt(f[11], 1, int.MaxValue, out effort)) return false;
			List<KingdomStrikeTarget> targets = new List<KingdomStrikeTarget>();
			if (!string.IsNullOrEmpty(f[12]))
			{
				string[] rows = f[12].Split(';');
				if (rows.Length > MaxStrikeTargets) return false;
				for (int i = 0; i < rows.Length; i++)
				{
					string[] values = rows[i].Split(',');
					string id, blueprint; int x, y;
					if (values.Length != 4
						|| !TryDecodeText(values[0], MaxSubjectChars, out id)
						|| !TryDecodeText(values[1], MaxTargetChars, out blueprint)
						|| !TryInt(values[2], 0, 1023, out x)
						|| !TryInt(values[3], 0, 1023, out y)) return false;
					targets.Add(new KingdomStrikeTarget
						{ Id = id, Blueprint = blueprint, X = x, Y = y });
				}
			}
			parsed.Effort = effort;
			parsed.Targets = targets;
			string canonical;
			if (!TryEncodeStrikeIntent(parsed, out canonical) || canonical != Receipt) return false;
			Intent = parsed;
			return true;
		}

		public static bool TryDecodeCells(string Payload, out List<KingdomConstructionCell> Cells)
		{
			Cells = null;
			if (string.IsNullOrEmpty(Payload) || Payload.Length > MaxPayloadChars)
			{
				return false;
			}
			string[] terms = Payload.Split(';');
			if (terms.Length < 2 || terms.Length - 1 > MaxRouteCells || terms[0] != "v1")
			{
				return false;
			}
			List<KingdomConstructionCell> cells = new List<KingdomConstructionCell>();
			HashSet<int> seen = new HashSet<int>();
			for (int i = 1; i < terms.Length; i++)
			{
				string[] pair = terms[i].Split(',');
				int x;
				int y;
				if (pair.Length != 2 || !TryInt(pair[0], -1, 1023, out x)
					|| !TryInt(pair[1], -1, 1023, out y) || x < 0 || y < 0
					|| !seen.Add(x + y * 1024))
				{
					return false;
				}
				cells.Add(new KingdomConstructionCell(x, y));
			}
			Cells = cells;
			return true;
		}

		private static bool TryDecodeRow(string Line, bool Legacy, bool Prior,
			out KingdomConstructionJob Row)
		{
			if (Legacy) return TryDecodeLegacyRow(Line, out Row);
			Row = null;
			string[] f = Line.Split('|');
			if (f.Length != (Prior ? 45 : 51)) return false;
			string owner, zone, subject, source, output, physicalItem, physicalDestination;
			string physicalReceipt, target, payload, requested, spent, outstanding, lost, failure;
			string eventId, chronicle, ledger, message, deed;
			int route, phase, projection, x, y, physicalPhase, physicalIndex, physicalAmount;
			int physicalSpilled, revision, waterRequested, waterSpent, waterOutstanding, waterLost;
			int mode, chronicleState, ledgerState, messageState, deedState;
			long created, started, due, updated;
			int ledgerBeforeCount = -1, ledgerAfterCount = -1;
			string ledgerBeforeHash = null, ledgerAfterHash = null, proofHash = null;
			bool compacted = false;
			if (!TryDecodeText(f[1], MaxOwnerChars, out owner)
				|| !TryDecodeText(f[2], MaxZoneChars, out zone)
				|| !TryInt(f[3], 1, (int)KingdomConstructionRoute.Strike, out route)
				|| !TryInt(f[4], 1, (int)KingdomConstructionPhase.InspectionRequired, out phase)
				|| !TryInt(f[5], 1, (int)KingdomConstructionProjection.Repair, out projection)
				|| !TryInt(f[6], -1, 1023, out x) || !TryInt(f[7], -1, 1023, out y)
				|| !TryDecodeText(f[8], MaxSubjectChars, out subject)
				|| !TryDecodeText(f[9], MaxSubjectChars, out source)
				|| !TryDecodeText(f[10], MaxSubjectChars, out output)
				|| !TryInt(f[11], 0, (int)KingdomPhysicalPhase.RoadTallySettled, out physicalPhase)
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
			if (!Prior && (!TryInt(f[45], -1, MaxLedgerNotes - 1, out ledgerBeforeCount)
				|| !TryDecodeText(f[46], 64, out ledgerBeforeHash)
				|| !TryInt(f[47], -1, MaxLedgerNotes, out ledgerAfterCount)
				|| !TryDecodeText(f[48], 64, out ledgerAfterHash)
				|| (f[49] != "0" && f[49] != "1")
				|| !TryDecodeText(f[50], 64, out proofHash))) return false;
			if (!Prior) compacted = f[49] == "1";
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
				if (Prior && box.LedgerState == KingdomConstructionSinkDisposition.Attempting)
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
				TargetKey = target, Payload = payload, CreatedTick = created,
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

		private static KingdomMaterialDebitCost MaterialOutstanding(KingdomConstructionClaims Claims)
		{
			KingdomMaterialDebitCost cost;
			return Claims != null && KingdomMaterialDebitCost.TryParseClaim(Claims.MaterialOutstanding, out cost)
				? cost : new KingdomMaterialDebitCost();
		}

		private static KingdomMaterialDebitCost AddCost(KingdomMaterialDebitCost A,
			KingdomMaterialDebitCost B)
		{
			KingdomMaterialTally materials = A.Materials.Copy();
			KingdomBitTally bits = A.Bits.Copy();
			KingdomExoticTally exotics = A.Exotics.Copy();
			for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
			{
				materials.Add((KingdomMaterial)i, B.Materials.Get((KingdomMaterial)i));
			}
			for (int i = 0; i < KingdomMaterialRules.BitTierCount; i++)
			{
				bits.Add(i, B.Bits.Get(i));
			}
			for (int i = 0; i < KingdomMaterialRules.ExoticCount; i++)
			{
				exotics.Add((KingdomExotic)i, B.Exotics.Get((KingdomExotic)i));
			}
			return new KingdomMaterialDebitCost(materials, bits, exotics);
		}

		private static bool SameCost(KingdomMaterialDebitCost A, KingdomMaterialDebitCost B)
		{
			return SumMatches(A, B, new KingdomMaterialDebitCost());
		}

		private static bool SumMatches(KingdomMaterialDebitCost Whole,
			KingdomMaterialDebitCost A, KingdomMaterialDebitCost B)
		{
			for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
			{
				KingdomMaterial kind = (KingdomMaterial)i;
				if ((long)A.Materials.Get(kind) + B.Materials.Get(kind) != Whole.Materials.Get(kind)) return false;
			}
			for (int i = 0; i < KingdomMaterialRules.BitTierCount; i++)
			{
				if ((long)A.Bits.Get(i) + B.Bits.Get(i) != Whole.Bits.Get(i)) return false;
			}
			for (int i = 0; i < KingdomMaterialRules.ExoticCount; i++)
			{
				KingdomExotic kind = (KingdomExotic)i;
				if ((long)A.Exotics.Get(kind) + B.Exotics.Get(kind) != Whole.Exotics.Get(kind)) return false;
			}
			return true;
		}

		private static bool Covers(KingdomMaterialDebitCost Whole, KingdomMaterialDebitCost Part)
		{
			for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
			{
				KingdomMaterial kind = (KingdomMaterial)i;
				if (Whole.Materials.Get(kind) < Part.Materials.Get(kind)) return false;
			}
			for (int i = 0; i < KingdomMaterialRules.BitTierCount; i++)
			{
				if (Whole.Bits.Get(i) < Part.Bits.Get(i)) return false;
			}
			for (int i = 0; i < KingdomMaterialRules.ExoticCount; i++)
			{
				KingdomExotic kind = (KingdomExotic)i;
				if (Whole.Exotics.Get(kind) < Part.Exotics.Get(kind)) return false;
			}
			return true;
		}

		private static int CompareCanonical(KingdomConstructionJob A, KingdomConstructionJob B)
		{
			int compare = string.CompareOrdinal(A.OwnerKey, B.OwnerKey);
			if (compare != 0) return compare;
			compare = string.CompareOrdinal(A.ZoneId, B.ZoneId);
			if (compare != 0) return compare;
			compare = A.CreatedTick.CompareTo(B.CreatedTick);
			return compare != 0 ? compare : string.CompareOrdinal(A.Id, B.Id);
		}

		private static int CompareNewest(KingdomConstructionJob A, KingdomConstructionJob B)
		{
			int compare = B.UpdatedTick.CompareTo(A.UpdatedTick);
			return compare != 0 ? compare : string.CompareOrdinal(B.Id, A.Id);
		}

		private static KingdomConstructionJob Compact(KingdomConstructionJob Job)
		{
			KingdomConstructionJob compact = Job.Copy();
			compact.Payload = null;
			compact.PhysicalReceipt = null;
			compact.Failure = null;
			compact.Outbox = null;
			compact.Compacted = true;
			compact.CompactHash = CompactIdentityHash(compact);
			return compact;
		}

		private static string CompactIdentityHash(KingdomConstructionJob Job)
		{
			if (Job == null || Job.Claims == null) return null;
			StringBuilder text = new StringBuilder("TAF-CONSTRUCTION-PROOF-1");
			text.Append('|').Append(Job.Id)
				.Append('|').Append(EncodeText(Job.OwnerKey)).Append('|').Append(EncodeText(Job.ZoneId))
				.Append('|').Append((int)Job.Route).Append('|').Append((int)Job.Phase)
				.Append('|').Append((int)Job.Projection).Append('|').Append(Job.X).Append('|').Append(Job.Y)
				.Append('|').Append(EncodeText(Job.SubjectId)).Append('|').Append(EncodeText(Job.SourceId))
				.Append('|').Append(EncodeText(Job.OutputId)).Append('|').Append((int)Job.PhysicalPhase)
				.Append('|').Append(Job.PhysicalIndex).Append('|').Append(Job.PhysicalAmount)
				.Append('|').Append(Job.PhysicalSpilled).Append('|').Append(EncodeText(Job.PhysicalItemId))
				.Append('|').Append(EncodeText(Job.PhysicalDestinationId)).Append('|').Append(EncodeText(Job.TargetKey))
				.Append('|').Append(Job.CreatedTick).Append('|').Append(Job.StartedTick)
				.Append('|').Append(Job.DueTick).Append('|').Append(Job.UpdatedTick).Append('|').Append(Job.Revision)
				.Append('|').Append(Job.Claims.WaterRequested).Append('|').Append(Job.Claims.WaterSpent)
				.Append('|').Append(Job.Claims.WaterOutstanding).Append('|').Append(Job.Claims.WaterLost)
				.Append('|').Append(Job.Claims.Exact ? '1' : '0')
				.Append('|').Append(EncodeText(Job.Claims.MaterialRequested))
				.Append('|').Append(EncodeText(Job.Claims.MaterialSpent))
				.Append('|').Append(EncodeText(Job.Claims.MaterialOutstanding))
				.Append('|').Append(EncodeText(Job.Claims.MaterialLost));
			return Sha256(text.ToString());
		}

		private static string Sha256(string Text)
		{
			using (SHA256 sha = SHA256.Create())
			{
				byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(Text ?? ""));
				StringBuilder encoded = new StringBuilder(64);
				for (int i = 0; i < hash.Length; i++)
					encoded.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
				return encoded.ToString();
			}
		}

		private static bool IsSha256(string Text)
		{
			if (Text == null || Text.Length != 64) return false;
			for (int i = 0; i < Text.Length; i++)
				if ((Text[i] < '0' || Text[i] > '9') && (Text[i] < 'a' || Text[i] > 'f'))
					return false;
			return true;
		}

		private static bool TextLength(string Text, int Min, int Max)
		{
			int length = Text == null ? 0 : Text.Length;
			return length >= Min && length <= Max;
		}

		private static string Limit(string Text, int Max)
		{
			if (Text == null || Text.Length <= Max) return Text;
			return Text.Substring(0, Max);
		}

		private static string EncodeText(string Text)
		{
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(Text ?? ""));
		}

		private static bool TryDecodeText(string Encoded, int Max, out string Text)
		{
			Text = null;
			try
			{
				byte[] bytes = Convert.FromBase64String(Encoded);
				string decoded = Encoding.UTF8.GetString(bytes);
				if (decoded.Length > Max || EncodeText(decoded) != Encoded)
				{
					return false;
				}
				Text = decoded.Length == 0 ? null : decoded;
				return true;
			}
			catch
			{
				return false;
			}
		}

		private static bool TryInt(string Text, int Min, int Max, out int Value)
		{
			return int.TryParse(Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out Value)
				&& Value >= Min && Value <= Max && Value.ToString(CultureInfo.InvariantCulture) == Text;
		}

		private static bool TryLong(string Text, out long Value)
		{
			return long.TryParse(Text, NumberStyles.None, CultureInfo.InvariantCulture, out Value)
				&& Value >= 0L && Value.ToString(CultureInfo.InvariantCulture) == Text;
		}
	}
}
