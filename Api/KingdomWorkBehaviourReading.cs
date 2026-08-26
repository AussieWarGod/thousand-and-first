using System;

namespace ThousandAndFirst.Api
{
	/// <summary>Frozen durable run-state and outstanding physical debt for one extension behaviour
	/// on one ordinary city work row.</summary>
	public readonly struct KingdomWorkBehaviourReading
	{
		/// <summary>Owner-qualified behaviour key.</summary>
		public readonly string BehaviourKey;
		/// <summary>Ordinary city work id.</summary>
		public readonly int WorkId;
		/// <summary>Opaque extension-owned run state.</summary>
		public readonly long State;
		/// <summary>Next absolute breakpoint.</summary>
		public readonly long NextTick;
		/// <summary>Exact Qud blueprint still owed to the attended ground, or empty.</summary>
		public readonly string OwedBlueprint;
		/// <summary>Objects of <see cref="OwedBlueprint"/> still owed.</summary>
		public readonly int OwedCount;
		/// <summary>Monotonic host receipt generation for physical output. It changes whenever this
		/// work accepts new materialisation debt, so a stale landed marker cannot settle a later run.</summary>
		public readonly long MaterialisationSequence;

		/// <summary>Builds a frozen reading without a receipt generation. Kept for source and binary
		/// compatibility; host-produced readings use the overload below.</summary>
		public KingdomWorkBehaviourReading(string BehaviourKey, int WorkId, long State, long NextTick,
			string OwedBlueprint, int OwedCount)
			: this(BehaviourKey, WorkId, State, NextTick, OwedBlueprint, OwedCount, 0L)
		{
		}

		/// <summary>Builds a frozen reading with the exact physical-output receipt generation.</summary>
		public KingdomWorkBehaviourReading(string BehaviourKey, int WorkId, long State, long NextTick,
			string OwedBlueprint, int OwedCount, long MaterialisationSequence)
		{
			this.BehaviourKey = BehaviourKey ?? "";
			this.WorkId = WorkId;
			this.State = State;
			this.NextTick = NextTick;
			this.OwedBlueprint = OwedBlueprint ?? "";
			this.OwedCount = OwedCount;
			this.MaterialisationSequence = MaterialisationSequence;
		}
	}
}
