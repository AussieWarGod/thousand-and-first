using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	/// <summary>
	/// Brings a standing heart's first basin into the settlement's water accounts once, on load.
	/// <para>
	/// A world built before the basin was a store carries a basin with no <c>KingdomStores</c>
	/// dedication and whatever capacity its blueprint shipped with. Nothing else in the mod ever
	/// revisits it, so without this the founder would have to raise a whole new rung before the
	/// water they already own started counting. The reconciliation is idempotent, never lowers a
	/// capacity, and reads only the ACTIVE zone &mdash; unvisited ground is never thawed to look
	/// for a heart.
	/// </para>
	/// <para>
	/// The attribute pair is the mod's established loader shape (<c>KingdomLoader</c>,
	/// <c>KingdomProcedureLoader</c>): <c>[CallAfterGameLoaded]</c> is only scanned for on a type
	/// that declares <c>[HasCallAfterGameLoaded]</c>, so the method and the marker have to travel
	/// together. This file exists so that no shipped loader has to be touched to add one.
	/// </para>
	/// </summary>
	[HasCallAfterGameLoaded]
	public static class KingdomHeartBasinLoader
	{
		[CallAfterGameLoaded]
		public static void ReconcileStandingHeartBasin()
		{
			XRLGame game = The.Game;
			KingdomSystem system = game?.GetSystem<KingdomSystem>();
			if (system == null || !system.Founded) return;
			Zone zone = The.ZoneManager?.ActiveZone;
			if (zone == null) return;
			// The seat is whatever the save was written with, so the claim gate below reads the
			// settlement whose accounts the dedication would land in.
			KingdomSystem.Guard("heart basin capacity", delegate
			{
				KingdomPlots.ReconcileBasinCapacity(system, zone);
			});
		}
	}

	public static partial class KingdomPlots
	{
		/// <summary>The authored functional role the heart's own basin fills at every rung.</summary>
		public const string HeartBasinRole = "fixture:first-basin";

		/// <summary>Set on the basin when its capacity could not be raised, so the founder is told
		/// once rather than every visit. Removed the moment the block lifts
		/// (<c>STANDARDS.md</c> &sect;7b).</summary>
		public const string BasinCapacityHeldProperty = "r_TAF_BasinCapacityHeldAnnounced";

		/// <summary>
		/// Brings this zone's first basin up to the capacity its heart's rung is worth, and puts
		/// it into the settlement's water accounts if it is not there yet.
		/// <para>
		/// Capacity is only ever RAISED. A basin holding sixteen drams when the waterstone closes
		/// around it keeps all sixteen and gains room for thirty-two more; nothing is poured,
		/// nothing spills, and a vessel someone else widened is never shrunk back to the table.
		/// </para>
		/// </summary>
		/// <param name="System">The realm. Null or unfounded reconciles nothing.</param>
		/// <param name="Z">The zone the heart stands in. Never thawed by this call. Ground the
		/// SEATED settlement does not claim reconciles nothing: the dedication writes into the
		/// seated city's water accounts and the hold reads the seated city's growth book, so a
		/// foreign, seceded, exiled or not-yet-seated heart is refused rather than guessed at.
		/// Callers on the activation path must therefore ask AFTER the seat exchange.</param>
		/// <returns>True when the basin was found and left at or above its rung's capacity.</returns>
		/// <remarks>
		/// The basin is resolved only through <c>TryExactAnchoredComponent</c>, which needs an
		/// a3|/a4| managed layout receipt on the heart's owner. A heart raised before managed
		/// layouts, whose owner carries an a2 or absent snapshot, is therefore NOT reconciled by
		/// this call and its basin stays outside the water accounts until the next rung restamps
		/// the layout. That is deliberate: guessing a basin from a standing relic mark alone would
		/// let any relic-marked object in the zone be dedicated as a settlement store.
		/// </remarks>
		internal static bool ReconcileBasinCapacity(KingdomSystem System, Zone Z)
		{
			GameObject root;
			return System != null && System.Founded && Z != null
				&& System.ClaimedZones != null && System.ClaimedZones.Contains(Z.ZoneID)
				&& TryStandingHeartRoot(Z, out root)
				&& ReconcileBasinCapacity(System, root, Z);
		}

		/// <summary>Same reconciliation against a heart root the caller already holds &mdash; the
		/// rung's own completion stamp, which knows its building without searching for it.
		/// <para>
		/// The seated realm's claim is proved HERE and not only by the searching overload above,
		/// because this is the entry the rung's own completion uses and that completion resolves
		/// its realm as the SEATED one rather than the one that owns the ground. A rung finishing
		/// on ground that left <c>ClaimedZones</c> &mdash; seceded, exiled, or never seated
		/// &mdash; would otherwise dedicate a foreign basin into the seated city's water accounts
		/// and read the seated city's growth book for the hold: the exact wrong-ledger widening
		/// the claim gate exists to refuse.
		/// </para></summary>
		internal static bool ReconcileBasinCapacity(KingdomSystem System, GameObject Root, Zone Z)
		{
			if (System == null || !System.Founded || Z == null || !GameObject.Validate(Root)
				|| System.ClaimedZones == null || !System.ClaimedZones.Contains(Z.ZoneID)
				|| Root.GetIntProperty(HeartPlotProperty) != 1) return false;
			int rung = HeartRung(Z);
			int capacity = KingdomPlotRules.HeartBasinCapacityForRung(rung);
			if (capacity <= 0) return false;
			GameObject basin;
			if (!KingdomArchitectureStamper.TryExactAnchoredComponent(Root, Z, HeartBasinRole,
					out basin, out _)
				|| !GameObject.Validate(basin) || basin.Blueprint != HeartRelicBlueprint
				|| basin.GetIntProperty(HeartRelicProperty) != 1) return false;
			LiquidVolume vessel = basin.GetPart<LiquidVolume>();
			if (vessel == null || vessel.MaxVolume < 0) return false;
			DedicateBasinStore(System, Z, basin);
			if (vessel.MaxVolume >= capacity)
			{
				ReleaseBasinCapacityHold(basin);
				return true;
			}
			string reason;
			if (BasinCapacityHeld(System, basin, vessel, out reason))
			{
				AnnounceBasinCapacityHold(System, basin, capacity, reason);
				return false;
			}
			ReleaseBasinCapacityHold(basin);
			vessel.MaxVolume = capacity;
			KingdomSurvey.ObserveChangedInActive(Z, basin);
			// Worded for the catch-up callers too: the load and zone-activation paths raise no
			// rung on the visit that widens the basin, so the line says what the rung is worth
			// rather than announcing a rung that just rose.
			System.Ledger.Note("The first basin now holds " + capacity
				+ " drams, which is what rung " + rung + " is worth.");
			KingdomLog.Log("basin: capacity raised to " + capacity + " at rung " + rung);
			return true;
		}

		/// <summary>
		/// Marks the basin as one of the settlement's water stores. The survey counts a vessel's
		/// capacity only when this mark is on its owner, and it strips the mark from old furnished
		/// plot pieces &mdash; the basin survives that sweep because an existing-authority
		/// placement is stamped <c>PlotPartProperty = 0</c> and the sweep only releases a mark
		/// from a piece stamped 1.
		/// <para>
		/// The ledger line below is not seen at a fresh founding: the rite stamps the dedication
		/// itself in <c>KingdomPlot2.07c.FoundingHeartMarks</c> as the relic slot is created, so
		/// the first reconciliation of a new settlement finds the mark already set and says
		/// nothing. It is written for the pre-change save that gains the dedication on load.
		/// </para>
		/// </summary>
		private static void DedicateBasinStore(KingdomSystem System, Zone Z, GameObject Basin)
		{
			if (Basin.GetIntProperty("KingdomStores") == 1) return;
			Basin.SetIntProperty("KingdomStores", 1);
			KingdomSurvey.ObserveChangedInActive(Z, Basin);
			System.Ledger.Note("The first basin is the settlement's water store.");
			KingdomLog.Log("basin: dedicated as a settlement water store");
		}

		/// <summary>
		/// Whether something is holding this basin's size still. Every answer is an honest refusal
		/// rather than a failure: each of these three readers asserts that the vessel's MaxVolume
		/// has not moved since it was measured, so widening underneath one would refuse or
		/// quarantine a receipt the founder is in the middle of spending.
		/// <para>
		/// The three are genuinely different windows. <c>TransactionOpen</c> is call-stack depth,
		/// so it only answers for a drain that is executing underneath this call.
		/// <c>VesselReserved</c> is the reserve-then-work-then-commit window, which spans
		/// publishes and is where the hazard actually lives. A prepared growth water leg is the
		/// one MaxVolume freeze that survives a save: the arrival endpoint refuses forever if
		/// <c>vessel.MaxVolume != leg.Capacity</c>, so an unsettled leg on the basin outranks the
		/// rung's capacity until the arrival settles or is retired.
		/// </para>
		/// </summary>
		private static bool BasinCapacityHeld(KingdomSystem System, GameObject Basin,
			LiquidVolume Vessel, out string Reason)
		{
			Reason = null;
			if (KingdomWaterDebit.TransactionOpen)
			{
				Reason = "a water debit is being settled";
				return true;
			}
			if (KingdomWaterDebit.VesselReserved(Vessel))
			{
				Reason = "an open water debit is bound to it";
				return true;
			}
			if (GrowthLegHoldsBasin(System, Basin))
			{
				Reason = "an arrival is drawing from it";
				return true;
			}
			KingdomConstructionInputLeaseSnapshot leases;
			string failure;
			if (!KingdomConstructionInputLeaseAuthority.TryCapture(out leases, out failure))
			{
				Reason = failure ?? "the routed-input leases cannot be read";
				return true;
			}
			if (KingdomConstructionInputLeaseAuthority.IsLeased(leases, Basin))
			{
				Reason = "a construction lease holds its water";
				return true;
			}
			return false;
		}

		/// <summary>
		/// Whether any prepared-but-unsettled growth water leg names this basin. A leg records the
		/// vessel's MaxVolume as <c>leg.Capacity</c> when it is prepared and the arrival endpoint
		/// re-proves that number before it settles, so a widen landing between the two would strand
		/// the arrival permanently. Legs before the operation's cursor are already settled and hold
		/// nothing.
		/// </summary>
		private static bool GrowthLegHoldsBasin(KingdomSystem System, GameObject Basin)
		{
			KingdomGrowthBook growth = System?.LifecycleBook?.Growth;
			if (growth == null || !GameObject.Validate(Basin)) return false;
			string id = Basin.IDIfAssigned;
			if (string.IsNullOrEmpty(id)) return false;
			return UnsettledLegNames(growth.HeartbeatOp, id)
				|| UnsettledLegNames(growth.ArrivalOp, id)
				|| UnsettledLegNames(growth.DepartureOp, id)
				|| UnsettledLegNames(growth.DeliveryOp, id)
				|| UnsettledLegNames(growth.FetchOp, id)
				|| UnsettledLegNames(growth.MillOp, id);
		}

		private static bool UnsettledLegNames(KingdomGrowthOperation Operation, string Id)
		{
			if (Operation == null || Operation.WaterLegs == null) return false;
			for (int i = (Operation.WaterCursor > 0 ? Operation.WaterCursor : 0);
				i < Operation.WaterLegs.Count; i++)
			{
				KingdomGrowthWaterLeg leg = Operation.WaterLegs[i];
				if (leg != null && string.Equals(leg.ContainerId, Id, StringComparison.Ordinal))
					return true;
			}
			return false;
		}

		private static void AnnounceBasinCapacityHold(KingdomSystem System, GameObject Basin,
			int Capacity, string Reason)
		{
			if (Basin.GetIntProperty(BasinCapacityHeldProperty) == 1) return;
			Basin.SetIntProperty(BasinCapacityHeldProperty, 1);
			System.Ledger.Note("The first basin cannot be widened to " + Capacity
				+ " drams yet: " + Reason + ". It will be widened when that clears.");
			KingdomLog.Log("basin: capacity hold (" + Reason + ")");
		}

		private static void ReleaseBasinCapacityHold(GameObject Basin)
		{
			if (Basin.GetIntProperty(BasinCapacityHeldProperty) != 1) return;
			Basin.RemoveIntProperty(BasinCapacityHeldProperty);
		}

		/// <summary>
		/// The one standing heart building in this zone, if there is exactly one. Reads the loaded
		/// zone only; a second heart-marked building is an ambiguity the reconciler refuses rather
		/// than guesses at.
		/// </summary>
		private static bool TryStandingHeartRoot(Zone Z, out GameObject Root)
		{
			Root = null;
			if (Z == null) return false;
			List<GameObject> roots = Z.GetObjects();
			if (roots == null) return false;
			for (int i = 0; i < roots.Count; i++)
			{
				GameObject item = roots[i];
				if (!GameObject.Validate(item) || item.GetIntProperty(HeartPlotProperty) != 1
					|| item.GetIntProperty("KingdomBuilt") != 1) continue;
				if (Root != null)
				{
					Root = null;
					return false;
				}
				Root = item;
			}
			return Root != null;
		}
	}
}
