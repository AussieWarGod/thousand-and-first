using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public enum KingdomLifecycleOptionAction : byte
	{
		None = 0,
		StayDisabled = 1,
		Disable = 2,
		EnableAndRestamp = 3,
		Quarantine = 4
	}

	public sealed class KingdomLifecycleOptionDecision
	{
		public bool Valid;
		public KingdomLifecycleOptionAction Action;
		public KingdomLifecycleOptionState State;
		public long Tick;
		public bool AllowNewWork;
		public bool ReconcileOpenWork;
	}

	public sealed class KingdomGrowthAvailabilityDecision
	{
		public bool Valid;
		public string Failure;
		public bool AllowStarters;
		public bool ReconcileOpen;
		public KingdomLifecycleOptionState OptionState;
		public KingdomGrowthHealthState HealthState;
		public long ObservedTick;
		public bool WorkPaused;
		public long PauseStartedTick;
		public long PausedTicks;
		public long EffectiveWorkTick;
		public bool RestampClocks;
		public long NextArrivalTick;
		public long ArrivalIntervalTicks;
	}

	public enum KingdomLifecycleMutationAction : byte
	{
		Settled = 0,
		InvokeOnce = 1,
		ConfirmAfter = 2,
		Quarantine = 3
	}

	public enum KingdomLifecycleCasAction : byte
	{
		Apply = 1,
		Confirm = 2,
		Quarantine = 3
	}

	/// <summary>
	/// Runtime trust boundary. Implementations must expose opaque live engine references and derive
	/// every field from a bounded scan of the real Qud object graph. They must never wrap a
	/// caller-authored DTO. The dormant lane's engine adapter belongs at the shell, outside Rules.
	/// </summary>
	internal interface IKingdomLifecycleTrustedObservation
	{
		object Reference { get; }
		string ObjectId { get; }
		string Marker { get; }
		string Blueprint { get; }
		string SettlementId { get; }
		string OwnerId { get; }
		string ZoneId { get; }
		KingdomLifecycleTopology Topology { get; }
		int X { get; }
		int Y { get; }
		int Count { get; }
		int Capacity { get; }
		string Composition { get; }
		long Value { get; }
		long Revision { get; }
		string LastOperationId { get; }
	}

	/// <summary>
	/// Trusted shell owns both the global scan and callback. Rules callers cannot supply observation
	/// literals, success booleans, or a callback detached from that same live graph.
	/// </summary>
	internal interface IKingdomLifecycleTrustedWorld
	{
		int ObservationCount { get; }
		IKingdomLifecycleTrustedObservation Observe(int Index);
		object InvokeCarryOutput(KingdomLifecycleProjection Output);
		object InvokeWater(object VesselReference, int Amount);
		object InvokeSchedule(object ScheduleReference, long DueTick, string OperationId);
		object InvokeCarryRemoval(object SourceReference, int Count, string UnitEventId);
		object InvokeCarrySignRemoval(object SignReference, int Count, string ReceiptId);
		object InvokeCarryMove(object SourceReference, int TripId,
			KingdomLifecycleTopology TargetTopology, string TargetOwnerId,
			string TargetZoneId, int TargetX, int TargetY, string ReceiptId);
		object InvokeLifecycleProjection(KingdomLifecycleProjection Projection);
		object InvokeLifecycleRemoval(object ObjectReference, int Count, string OperationId);
	}

	[Flags]
	public enum KingdomLifecycleSinkMask : byte
	{
		None = 0,
		Chronicle = 1,
		Ledger = 2,
		Message = 4,
		Deed = 8,
		Guestbook = 16
	}

}
