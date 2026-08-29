using System;
using System.Collections.Generic;

using XRL;
using XRL.World;
using XRL.World.AI.GoalHandlers;
using XRL.World.Parts;

using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	public static partial class KingdomPorters
	{
		// ==================================================================================
		// Rendering, stepping, and closing
		// ==================================================================================

		/// <summary>
		/// Puts every open job's carrier where the model says it is, in the zone that has just
		/// become attended.
		/// <para>
		/// LIVING-CITY-ARCHITECTURE &sect;3.7, the edge handoff: <b>materialisation places the
		/// carrier at <c>At(job, now)</c></b>, and because <c>now</c> is barely past the leg's
		/// departure that is just inside the entry edge, a cell or two along. Cross slower and the
		/// porter is further on; both are correct renderings of the same one answer, which is the
		/// whole of I5.
		/// </para>
		/// </summary>
		public static void Render(KingdomSystem System, Zone Z, long TimeTicks)
		{
			KingdomJobTable table;
			KingdomCityFault fault;
			if (System == null || !System.Founded || Z == null || System.Jobs == null || System.Jobs.Count == 0
				|| !System.Jobs.TryRead(out table, out fault))
			{
				return;
			}
			for (int i = 0; i < table.Count; i++)
			{
				KingdomJobRow row;
				if (!table.TryAt(i, out row) || row.Kind != KingdomJobKind.Delivery)
				{
					continue;
				}
				KingdomItineraryFix fix;
				int bindingId = row.JobId;
				bool central = KingdomJobRules.IsCentralDelivery(row);
				if (central)
				{
					// Construction freight has no walking body between attended pickup and
					// attended landing. Exact body+inventory remain rooted in semantic transit.
					if (row.DeliveryCargoAuthority
							== KingdomDeliveryCargoAuthority.ConstructionInput) continue;
					if (row.JobId != row.DeliveryTripId
						|| !TryActiveTripRow(table, row.DeliveryTripId, TimeTicks, out row, out fix))
						continue;
					bindingId = row.DeliveryTripId;
				}
				else if (!KingdomItineraryRules.TryAt(row.Legs(), row.LegCount, TimeTicks, out fix, out fault))
				{
					continue;
				}
				if (!string.Equals(fix.ZoneId, Z.ZoneID, StringComparison.Ordinal)
					|| fix.Phase == KingdomItineraryPhase.Delivered
					|| fix.Phase == KingdomItineraryPhase.Pending)
				{
					continue;
				}
				Place(System, Z, row, fix, TimeTicks, bindingId, central);
			}
		}

		/// <summary>
		/// One porter's turn: deposit on arrival, then leave, then be gone.
		/// <para>
		/// A part rather than a <c>DelegateGoal</c> chain, and deliberately: <c>DelegateGoal</c>'s
		/// three delegates are all <c>[NonSerialized]</c>
		/// (<c>D/XRL/World/AI/GoalHandlers/DelegateGoal.cs:8-19</c>), so a save taken mid-walk would
		/// come back with a carrier who has forgotten what they were carrying it for. The walk is
		/// still vanilla's &mdash; <c>Brain.PushGoal(new MoveTo(...))</c> and nothing else &mdash;
		/// and this only decides what happens when it ends.
		/// </para>
		/// </summary>
		internal static void Step(GameObject Body, r_KingdomPorter Part, long TimeTick)
		{
			KingdomSystem system = (The.Game == null) ? null : The.Game.RequireSystem<KingdomSystem>();
			Zone zone = (Body == null) ? null : Body.CurrentZone;
			if (system == null || !system.Founded || zone == null || Part == null || Part.JobId == 0 || system.Jobs == null)
			{
				return;
			}
			KingdomJobTable table;
			KingdomCityFault fault;
			if (!system.Jobs.TryRead(out table, out fault))
			{
				return;
			}
			KingdomJobRow row;
			if (!table.TryGet(Part.JobId, out row) || row.Kind != KingdomJobKind.Delivery)
			{
				// The model closed this job while the ground was elsewhere. The sweep is the place
				// that removes the body; a turn tick is not, because a body that deleted itself
				// mid-turn is a body the engine is still iterating over.
				return;
			}
			if (KingdomJobRules.IsCentralDelivery(row))
			{
				if (row.DeliveryCargoAuthority
					== KingdomDeliveryCargoAuthority.ConstructionInput) return;
				KingdomItineraryFix centralFix;
				KingdomJobRow active;
				if (!TryActiveTripRow(table, Part.JobId, TimeTick, out active, out centralFix)) return;
				// Authority-2 cargo does not enter itinerary custody until the parent has
				// durably debited every source and acknowledged pickup.
				if (active.DeliveryCargoAuthority
					== KingdomDeliveryCargoAuthority.ConstructionInput) return;
				if (Near(Body, Part.DestX, Part.DestY))
				{
					if (active.DeliveryCargoAuthority == KingdomDeliveryCargoAuthority.ScalarStock)
						KingdomCentralLogistics.SettleScalarArrivals(system, zone,
							KingdomSurvey.Take(zone, system), TimeTick,
							KingdomData.CropForStyle(system.Style));
					HandoffCentral(system, Part.JobId, Body, active, centralFix, TimeTick);
				}
				return;
			}
			if (row.CargoAmount > 0 && Near(Body, Part.DestX, Part.DestY))
			{
				Deposit(system, zone, Body, Part, row, TimeTick);
				return;
			}
			if (row.CargoAmount <= 0 && Near(Body, Part.ExitX, Part.ExitY))
			{
				KingdomLeg final;
				if (row.TryLeg(row.LegCount - 1, out final)
					&& string.Equals(final.ZoneId, zone.ZoneID, StringComparison.Ordinal))
				{
					Close(system, Part.JobId,
						"the load reached the store and the carrier went back the way they came");
				}
				else
				{
					Handoff(system, Part.JobId, Body, zone.ZoneID);
				}
				return;
			}
			bool overrun;
			if (KingdomItineraryRules.TryHasOverrun(row.Legs(), row.LegCount, TimeTick, out overrun, out fault) && overrun)
			{
				// §3.7: a job whose elapsed exceeds twice its projected duration FAILS and is told,
				// so a founder who blocks a doorway forever produces a story, not an unbounded job
				// set. The cargo is real items and stays exactly where it is.
				Fail(system, Part.JobId);
			}
		}

		/// <summary>
		/// The stale-transient sweep. LIVING-CITY-ARCHITECTURE &sect;3.8's t3, and <b>this is the
		/// wave that lands the despawn</b>: W2 shipped the verdict.
		/// <para>
		/// Runs at <c>ZoneThawedEvent</c>, before intake and before any reify, because that is the
		/// one instant the goods could exist twice &mdash; in the larder and in a frozen pack. What
		/// it removes is <c>_stock</c>: items the simulation made and may remove, vanilla's own
		/// protocol. Anything that is not <c>_stock</c>, or that answers <c>IsImportant()</c>, is
		/// <b>dropped to the cell first and never destroyed.</b>
		/// </para>
		/// <para>
		/// Licensed for transients only. A resident is a person, and &sect;8.3's <i>materialisation
		/// may never remove a body</i> stands untouched &mdash; there is no input for it here,
		/// because the sweep is keyed on a job id and a person does not have one.
		/// </para>
		/// </summary>
		public static int Sweep(KingdomSystem System, Zone Z, KingdomSurvey Survey = null)
		{
			if (System == null || !System.Founded || Z == null)
			{
				return 0;
			}
			KingdomSurvey survey = Survey ?? KingdomSurvey.Take(Z, System);
			List<GameObject> found = new List<GameObject>(survey.Transients);
			int swept = 0;
			for (int i = 0; found != null && i < found.Count; i++)
			{
				GameObject body = found[i];
				if (!GameObject.Validate(body) || KingdomResidents.SweepVerdict(System, body) != KingdomSweepVerdict.Stale)
				{
					continue;
				}
				if (ConstructionInputSweepProtected(System, body)) continue;
				Spill(body);
				int jobId = body.GetPart<r_KingdomPorter>()?.JobId ?? 0;
				if (!CanRetireBody(body, jobId)) continue;
				KingdomLog.Log("porter: swept a stale carrier out of " + Z.ZoneID + " (job "
					+ body.GetIntProperty(KingdomResidents.JobIdProperty) + ")");
				try { body.Obliterate(); }
				catch { continue; }
				if (GameObject.Validate(body)) continue;
				survey.ObserveRemoved(body);
				swept++;
			}
			if (swept > 0)
			{
				System.Ledger.Note("{{K|" + KingdomCityRules.SweptNote(swept) + "}}");
			}
			return swept;
		}

		/// <summary>
		/// Closes every job whose itinerary has run out while nobody was there to render it, and
		/// puts what the carrier was still holding back on the road.
		/// <para>
		/// &sect;3.8's t2, exactly: the model closes the job, evicts the binding, and
		/// <b>re-attributes the outstanding deposit unit from the porter to the ordinary
		/// materialisation path</b>. Which is what makes the sweep at t3 deduplication rather than
		/// destruction of property &mdash; the load is back in the city's own books before the
		/// frozen pack is ever opened.
		/// </para>
		/// </summary>
		public static int Retire(KingdomSystem System, long TimeTicks)
		{
			KingdomJobTable table;
			KingdomCityFault fault;
			if (System == null || !System.Founded || System.Jobs == null || System.Jobs.Count == 0
				|| !System.Jobs.TryRead(out table, out fault))
			{
				// The ordinary case, and the one that has to cost nothing: a realm with no carrier
				// on the road does not read a registry to find that out.
				return 0;
			}
			int[] ids = table.OpenIds();
			int retired = 0;
			for (int i = 0; i < ids.Length; i++)
			{
				KingdomJobRow row;
				if (!table.TryGet(ids[i], out row) || row.Kind != KingdomJobKind.Delivery)
				{
					continue;
				}
				if (KingdomJobRules.IsCentralDelivery(row))
				{
					// Exact central cargo is never proxy-closed off-screen. Its persisted receipt or
					// opaque owner must settle it on trusted ground.
					continue;
				}
				KingdomItineraryFix fix;
				bool overrun;
				bool done = KingdomItineraryRules.TryAt(row.Legs(), row.LegCount, TimeTicks, out fix, out fault)
					&& fix.Phase == KingdomItineraryPhase.Delivered;
				if (!done && (!KingdomItineraryRules.TryHasOverrun(row.Legs(), row.LegCount, TimeTicks, out overrun, out fault) || !overrun))
				{
					continue;
				}
				KingdomBodyPresence presence = KingdomResidents.PresenceOfKey(System, ids[i], KingdomBindingKind.Transient, row.DestZoneId);
				if (presence == KingdomBodyPresence.Here || presence == KingdomBodyPresence.Elsewhere)
				{
					// §3.8 t2 is about a carrier the model has outlived, and a carrier still on
					// resident ground has not been outlived: they are walking, and KingdomPorters
					// Step closes them where they stand. Closing one here would delete a body in
					// front of the founder for arithmetic they cannot see.
					continue;
				}
				if (Close(System, ids[i], null))
				{
					retired++;
				}
			}
			return retired;
		}
	}
}
