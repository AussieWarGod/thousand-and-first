using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Pure phase, claim, owner, route and registry format laws.</summary>
	public static partial class KingdomConstructionRules
	{
		public const string FormatHeader = "TAF-CONSTRUCTION-4";
		public const string PriorFormatHeader = "TAF-CONSTRUCTION-3";
		public const string OlderFormatHeader = "TAF-CONSTRUCTION-2";
		public const string LegacyFormatHeader = "TAF-CONSTRUCTION-1";
		public const int BuildTruthSchema = 1;
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
			case KingdomConstructionRoute.PurposeConsignment:
				return KingdomConstructionProjection.PurposeConsignment;
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
				case KingdomConstructionRoute.PurposeConsignment: suffix = "delivered"; break;
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
			case KingdomConstructionRoute.PurposeConsignment:
				return Job.PhysicalPhase == KingdomPhysicalPhase.CargoDelivered;
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

	}
}
