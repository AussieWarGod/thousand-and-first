using System;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomUpgrade
	{
		/// <summary>
		/// Settles the heart's rung effects for an improvement that has just closed physically.
		/// <para>
		/// Every rung of the heart above the first climbs by THIS route and no other: the plot
		/// commissioner refuses a heart rung outright
		/// (<c>Growth/KingdomPlot2.10.Commission.cs</c>) and zoning never offers one. Until this
		/// call existed the zone's rung was written only by the plot route, so a heart that rose
		/// from rung one to rung two kept a zone stamped at rung one, and the rung above it was
		/// refused for not accreting from its standing rung
		/// (<c>Growth/KingdomArchitectureRuntime.Successors.cs</c>).
		/// </para>
		/// <para>
		/// The exact endpoint proof here is the IMPROVEMENT route's own, not the plot route's:
		/// this successor, on this job, on this ground, still carrying the removal proof of the
		/// predecessor it replaced. It is proved before the shared helper runs and re-asked by
		/// the helper after the ceremony callback, so a callback that moved or replaced the
		/// building refuses instead of settling.
		/// </para>
		/// </summary>
		/// <returns>False only when a rung was owed and could not be settled exactly. The caller
		/// keeps its receipt non-terminal, and the recovery path quarantines it with the caller's
		/// exact reason (a row that was already terminal before this seam existed is completed
		/// with that reason as its diagnostic instead, because a terminal phase cannot be
		/// quarantined).</returns>
		internal static bool TrySettleImprovementHeartRung(KingdomSystem System, Zone Z,
			GameObject Successor, KingdomConstructionJob Job)
		{
			if (System == null || Z == null || Job == null
				|| Job.Route != KingdomConstructionRoute.Improvement) return false;
			// The cheap gate first, then the WHOLE handover proof as the delegate the helper
			// re-asks after every callback. Not a parallel predicate: it is the very function the
			// handover proved this successor with, carve-out and all.
			return ExactImprovementHeartEndpoint(System, Z, Successor, Job)
				&& KingdomPlots.TrySettleHeartRung(System, Z, Successor, Job.TargetKey,
					() => ExactImprovementHandoverProof(System, Z, Successor, Job, out _));
		}

		/// <summary>The cheap pre-call gate: this successor, this job, this ground. It is what
		/// refuses before any work starts; the full handover proof
		/// (<c>ExactImprovementHandoverProof</c>) is what the settlement helper re-asks after
		/// every callback, because a callback can leave the root exactly where it was and still
		/// have changed what the root IS.</summary>
		private static bool ExactImprovementHeartEndpoint(KingdomSystem System, Zone Z,
			GameObject Successor, KingdomConstructionJob Job)
		{
			GameObject exact;
			return GameObject.Validate(Successor)
				&& KingdomConstruction.Owns(System, Z, Job)
				&& KingdomConstruction.IsCurrent(Job)
				&& KingdomConstruction.FindExactId(Z, Job.OutputId, out exact)
					== KingdomPhysicalLookupState.Exact
				&& ReferenceEquals(exact, Successor)
				&& Successor.IDIfAssigned == Job.OutputId
				&& Successor.CurrentZone == Z
				&& Successor.CurrentCell == Z.GetCell(Job.X, Job.Y)
				&& KingdomConstruction.HasReceipt(Successor, Job)
				&& Successor.GetIntProperty(BuiltProperty) == 1
				&& Successor.GetStringProperty(BuildKeyProperty) == Job.TargetKey
				&& r_KingdomScaffold.HasRemovalProof(Successor, Job.SubjectId);
		}
	}
}
