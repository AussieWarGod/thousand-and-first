using System;

using XRL.World;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// The one entry seam for physical, attended settlement semantics.
	/// <para>
	/// Both ZoneActivated and the stationary EndTurn pump enter here. The dispatcher owns only
	/// scheduling, persistence, and reentrancy; the ordered pass remains a callback owned by
	/// <c>KingdomSystem</c>. No survey or zone scan happens until the pure clock says work is due.
	/// </para>
	/// </summary>
	public static class KingdomSemanticDispatcher
	{
		/// <summary>The canonical attended pass. False means no durable checkpoint may be published.</summary>
		public delegate bool AttendedPass(Zone Zone);

		[ThreadStatic]
		private static bool dispatching;

		[ThreadStatic]
		private static bool stationaryDispatch;

		/// <summary>True only while the daily EndTurn path is inside the canonical pass. Presentation
		/// uses this to distinguish "still standing here" from a genuine homecoming.</summary>
		public static bool IsStationaryDispatch
		{
			get { return dispatching && stationaryDispatch; }
		}

		/// <summary>O(1) stationary check. The callback is entered only at a new absolute boundary.</summary>
		public static bool OnEndTurn(KingdomSystem System, Zone Zone, long NowTick, AttendedPass Pass)
		{
			return TryDispatch(System, Zone, NowTick, ForceActivation: false, Pass);
		}

		/// <summary>
		/// Reconciles newly activated ground only when this city's absolute day is due, or when the
		/// city has never had a semantic pass. Crossing a boundary cannot mint extra passes.
		/// </summary>
		public static bool OnZoneActivated(KingdomSystem System, Zone Zone, long NowTick, AttendedPass Pass)
		{
			return TryDispatch(System, Zone, NowTick, ForceActivation: true, Pass);
		}

		private static bool TryDispatch(KingdomSystem System, Zone Zone, long NowTick,
			bool ForceActivation, AttendedPass Pass)
		{
			if (dispatching || System == null || !System.Founded || Zone == null || Pass == null
				|| System.ClaimedZones == null || !System.ClaimedZones.Contains(Zone.ZoneID))
			{
				return false;
			}
			KingdomSemanticClockState state = KingdomSemanticClockRules.FromLastDispatchTick(System.LastSemanticTick);
			KingdomSemanticClockDecision decision = KingdomSemanticClockRules.Decide(state, NowTick,
				ForceActivation);
			if (!decision.ShouldDispatch)
			{
				return false;
			}
			dispatching = true;
			stationaryDispatch = !ForceActivation;
			try
			{
				if (!Pass(Zone))
				{
					return false;
				}
				System.LastSemanticTick = decision.Next.LastDispatchTick;
				if (KingdomLog.Enabled)
				{
					KingdomLog.Log("semantic: " + decision.Kind.ToString().ToLowerInvariant() + " "
						+ Zone.ZoneID + " tick=" + NowTick + " boundary=" + decision.DueBoundaryTick);
				}
				return true;
			}
			finally
			{
				stationaryDispatch = false;
				dispatching = false;
			}
		}

	}
}
