using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPlots
	{
		/// <summary>
		/// The sealed founding heart after its root has lawfully CLIMBED a rung.
		///
		/// <para>THE DEFECT THIS ANSWERS. The founding heart's final root is pinned to one
		/// deterministic reserved identity (<c>FoundingHeartFinalId</c>): the terminal plan
		/// carries it, the reservation store reserves it, and the saved root key is named after
		/// it. Every rung above the first climbs by the improvement route
		/// (<c>KingdomPlot2.10.Commission.cs</c> refuses to commission one), and that route
		/// REPLACES the standing object with a successor carrying its own engine identity --
		/// recorded in the construction receipt, in the layout output receipts and in the
		/// component census tokens, and therefore not something that could be renamed to the
		/// reserved one. So after the first climb the sealed identity resolves to nothing, sealed
		/// recovery refused, and <c>KingdomConstruction.Settlement</c> aborted every later
		/// settlement pass on that ground -- which is also why the city book's own work row was
		/// never refreshed, since the pass that rebuilds it never ran.</para>
		///
		/// <para>WHAT THIS ACCEPTS, AND ONLY THIS. One extra way to recover a heart whose seal
		/// already proved out: the sealed identity is gone AND its retirement is proved AND
		/// exactly one heart plot of this settlement's own lot stands on the sealed ground,
		/// functionally built, at the very rung the zone records. Nothing is written, nothing is
		/// renamed, no receipt is rewritten and no existing refusal is relaxed: a heart whose
		/// root simply vanished still refuses, because a vanished root leaves no successor
		/// standing at the sealed cell, and a foreign building on that cell refuses because it
		/// carries neither the lot nor the rung.</para>
		/// </summary>
		private static bool HasClimbedFoundingHeartRoot(KingdomSystem System, Zone Z,
			FoundingHeartContext Context)
		{
			KingdomFoundingHeartPlan plan = Context?.Plan;
			if (System == null || Z == null || Context?.Architecture == null
				|| !KingdomFoundingHeartRules.Valid(plan)
				|| !KingdomFoundingHeartRules.Complete(plan)) return false;
			// The sealed root is really gone, and gone LAWFULLY: absent globally, never merely
			// unresolvable, and its retirement proved by the same authority the terminal drive
			// asks for when it settles a predecessor.
			if (FindGlobalFoundingHeartId(FoundingHeartFinalId(plan), out _, out _)
				!= KingdomPhysicalLookupState.Absent) return false;
			if (!ExactFoundingHeartRetiredAuthority(Z, FoundingHeartFinalId(plan), out _))
				return false;
			int rung = HeartRung(Z);
			if (rung < 2) return false;
			Cell cell = Z.GetCell(Context.Architecture.MainWorldX, Context.Architecture.MainWorldY);
			if (cell == null) return false;
			GameObject climbed = null;
			for (int i = 0; i < cell.Objects.Count; i++)
			{
				GameObject item = cell.Objects[i];
				if (!GameObject.Validate(item)
					|| item.GetIntProperty(HeartPlotProperty) != 1
					|| item.GetStringProperty(PlotIdProperty) != plan.PlotId) continue;
				if (climbed != null) return false;
				climbed = item;
			}
			if (climbed == null || !KingdomUpgrade.IsFunctionallyBuilt(climbed)) return false;
			// The rung the zone records and the rung this design IS must be the same one, so a
			// half-settled climb -- a successor standing at a rung the zone never stamped, or a
			// stamp with no successor under it -- is refused rather than recovered.
			string key = KingdomUpgrade.DesignKeyOf(climbed);
			return KingdomPlotRules.HeartRungOf(key) == rung
				&& KingdomArchitectureStamper.TryReadOwner(climbed, out _, out _, out string lot,
					out _) && lot == plan.PlotId;
		}
	}
}
