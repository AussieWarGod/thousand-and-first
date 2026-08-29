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
		Strike = 11,
		/// <summary>Exact other-city purpose cargo carried through a live mirror pair.</summary>
		PurposeConsignment = 12,
		/// <summary>One paid lot raised inside an exact hosted arcology carrier.</summary>
		HostedArcology = 13
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
		Repair = 7,
		PurposeConsignment = 8,
		HostedLot = 9
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
		RoadTallySettled = 30,
		CargoOutputPending = 31,
		CargoOutputSettled = 32,
		CargoTransferPending = 33,
		CargoDelivered = 34
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
		/// <summary>True only when strike order froze an exact authored lot before removal.</summary>
		public bool HasTypedLot;
		public string LotType;
		public ArchitectureLotSize LotSize;
		public ArchitectureFacing Facing;
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

}
