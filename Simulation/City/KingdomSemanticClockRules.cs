using System;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>Why the attended semantic pass is owed.</summary>
	public enum KingdomSemanticDispatchKind : byte
	{
		None = 0,
		Cadence = 1,
		Activation = 2
	}

	/// <summary>How a durable subsystem receipt relates to the pass now requested.</summary>
	public enum KingdomSemanticPassReceiptVerdict : byte
	{
		Start = 0,
		Resume = 1,
		RefuseDifferentGround = 2
	}

	/// <summary>
	/// The two logical stamps which make attendance scheduling independent of event partitioning.
	/// <para><see cref="LastBoundaryTick"/> owns elapsed semantic time. <see cref="LastDispatchTick"/>
	/// suppresses a repeat inside the same absolute day. At runtime both are derived from the
	/// settlement's durable <c>LastSemanticTick</c>; homecoming presentation has its own clock.</para>
	/// </summary>
	public readonly struct KingdomSemanticClockState
	{
		public readonly long LastBoundaryTick;

		public readonly long LastDispatchTick;

		public KingdomSemanticClockState(long LastBoundaryTick, long LastDispatchTick)
		{
			this.LastBoundaryTick = (LastBoundaryTick > 0L) ? LastBoundaryTick : 0L;
			this.LastDispatchTick = (LastDispatchTick > 0L) ? LastDispatchTick : 0L;
		}
	}

	/// <summary>A pure scheduling verdict. The proposed state is published only after the pass succeeds.</summary>
	public readonly struct KingdomSemanticClockDecision
	{
		public readonly KingdomSemanticDispatchKind Kind;

		public readonly long DueBoundaryTick;

		public readonly KingdomSemanticClockState Next;

		public bool ShouldDispatch
		{
			get { return Kind != KingdomSemanticDispatchKind.None; }
		}

		public KingdomSemanticClockDecision(KingdomSemanticDispatchKind Kind, long DueBoundaryTick,
			KingdomSemanticClockState Next)
		{
			this.Kind = Kind;
			this.DueBoundaryTick = DueBoundaryTick;
			this.Next = Next;
		}
	}

	/// <summary>
	/// Pure absolute-cadence arithmetic for attended settlement reconciliation.
	/// <para>
	/// The boundary is derived from world time rather than advanced from the previous observation.
	/// Consequently, observing every turn, once a day, or only at the final tick proposes the same
	/// terminal checkpoint. Activation may seed a city's first-ever pass, but cannot manufacture a
	/// second semantic opportunity inside an already-settled day.
	/// </para>
	/// </summary>
	public static class KingdomSemanticClockRules
	{
		/// <summary>Physical settlement semantics settle once per in-game day while attended.</summary>
		public const long CadenceTicks = KingdomRules.TicksPerDay;

		public static KingdomSemanticClockDecision Decide(KingdomSemanticClockState State, long NowTick,
			bool ForceActivation)
		{
			if (NowTick <= 0L)
			{
				return None(State);
			}
			long boundary = AbsoluteBoundary(NowTick);
			bool cadenceDue = boundary > State.LastBoundaryTick;
			if (cadenceDue)
			{
				return new KingdomSemanticClockDecision(KingdomSemanticDispatchKind.Cadence, boundary,
					new KingdomSemanticClockState(boundary, NowTick));
			}
			if (ForceActivation && State.LastDispatchTick <= 0L)
			{
				return new KingdomSemanticClockDecision(KingdomSemanticDispatchKind.Activation,
					boundary, new KingdomSemanticClockState(boundary, NowTick));
			}
			return None(State);
		}

		/// <summary>Builds the logical clock from its one persisted settlement stamp.</summary>
		public static KingdomSemanticClockState FromLastDispatchTick(long LastDispatchTick)
		{
			return new KingdomSemanticClockState(AbsoluteBoundary(LastDispatchTick), LastDispatchTick);
		}

		public static long AbsoluteBoundary(long Tick)
		{
			if (Tick <= 0L)
			{
				return 0L;
			}
			return Tick - Tick % CadenceTicks;
		}

		/// <summary>
		/// Classifies the pass-level receipt without touching it. A fully completed receipt is not
		/// replaced until its dispatch tick was published; this closes the crash seam between a
		/// callback returning success and the dispatcher writing <c>LastSemanticTick</c>.
		/// </summary>
		public static KingdomSemanticPassReceiptVerdict ReceiptVerdict(bool Active,
			long StartedTick, string BoundZoneId, long CompletedMask, long RequiredMask,
			long LastSemanticTick, string RequestedZoneId)
		{
			bool completed = RequiredMask > 0L
				&& (CompletedMask & RequiredMask) == RequiredMask;
			bool published = completed && LastSemanticTick >= StartedTick;
			if (!Active || published)
			{
				return KingdomSemanticPassReceiptVerdict.Start;
			}
			return string.Equals(BoundZoneId, RequestedZoneId, StringComparison.Ordinal)
				? KingdomSemanticPassReceiptVerdict.Resume
				: KingdomSemanticPassReceiptVerdict.RefuseDifferentGround;
		}

		private static KingdomSemanticClockDecision None(KingdomSemanticClockState State)
		{
			return new KingdomSemanticClockDecision(KingdomSemanticDispatchKind.None,
				State.LastBoundaryTick, State);
		}
	}
}
