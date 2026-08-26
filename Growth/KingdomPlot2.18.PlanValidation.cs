using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomPlots
	{
		private static bool RectOutsideZone(KingdomPlotRules.PlotRect Rect, Zone Z)
		{
			return Z == null || Rect.X1 < 0 || Rect.Y1 < 0
				|| Rect.X2 >= Z.Width || Rect.Y2 >= Z.Height;
		}

		/// <summary>Frozen plan price, with a current-catalogue fallback only for legacy markers.</summary>
		internal static bool TryPlanPrice(GameObject Marker, KingdomRules.BuildEntry Entry,
			out int WaterDrams, out KingdomMaterialDebitCost Material)
		{
			WaterDrams = Entry == null ? 0 : Entry.CostDrams;
			Material = Entry == null ? null : new KingdomMaterialDebitCost(
				KingdomMaterials.CostFor(Entry.Key), KingdomMaterials.BitCostFor(Entry.Key),
				KingdomMaterials.ExoticCostFor(Entry.Key));
			if (Marker == null || Entry == null) return false;
			if (!Marker.HasIntProperty(PlanSchemaProperty)) return Material != null;
			return TryReadFrozenPlan(Marker, Entry, true, out _, out _, out _,
				out WaterDrams, out Material, out _);
		}

		private static bool TryFrozenPlanReady(KingdomSystem System, GameObject Marker,
			KingdomRules.BuildEntry Entry, out KingdomPlotRules.PlotRect Rect,
			out string Payload, out long LabourTicks, out KingdomArchitectureIntent Architecture,
			out string Failure)
		{
			Architecture = null;
			if (Entry == null || !TryGetSpec(Entry.Key, out _))
			{
				Rect = default(KingdomPlotRules.PlotRect);
				Payload = null;
				LabourTicks = 0L;
				Failure = "Its design no longer declares the plotted behavior needed to raise it.";
				return false;
			}
			if (!TryReadFrozenPlan(Marker, Entry, true, out Rect, out Payload,
				out LabourTicks, out _, out KingdomMaterialDebitCost material, out Failure))
				return false;
			Zone zone = Marker.CurrentZone;
			if (!TryDecodePlotPayload(Payload, out _, out _, out Architecture,
				out bool legacy, out Failure) || legacy)
				return false;
			if (!KingdomZoning.Permits(System, zone.ZoneID, Entry, out Failure)) return false;
			Failure = KingdomDelve.Refusal(System, zone.ZoneID, Entry.Key, Entry.Name);
			if (Failure != null) return false;
			GroundGrid grid = new GroundGrid(zone);
			if (grid.AnyRefusal(Rect))
			{
				if (grid.TryFirstRefusal(Rect, out int x, out int y,
					out KingdomPlotRules.GroundKind kind, out string blocker))
					Failure = kind == KingdomPlotRules.GroundKind.Liquid
						? KingdomPlotRules.RefuseLiquid(x, y)
						: KingdomPlotRules.RefuseObstruction(blocker ?? "something", x, y);
				else Failure = "Something protected now stands in the reserved lot.";
				return false;
			}
			if (!KingdomArchitectureStamper.TryPreflight(System, zone, Architecture,
				material, out Failure)) return false;
			Cell main = zone.GetCell(Architecture.MainWorldX, Architecture.MainWorldY);
			if (main == null || KingdomConstruction.HasActiveAt(System, zone, main))
			{
				Failure = "The reserved lot's authored main ground already has paid construction in hand.";
				return false;
			}
			return true;
		}

		/// <summary>
		/// Whether a staked plan must wait this pass, and why. The authored stage gate applies to
		/// every plan; plot-sized designs then add their ground, weather, and budget gates. Announces
		/// the reason once on the marker and never again until the block lifts (STANDARDS 7b's
		/// established idiom, carried on a property rather than a field so no part's serialized
		/// layout moves).
		/// <para>
		/// Called BEFORE the water is drawn, so a plan whose ground is blocked never spends
		/// anything: waiting is not failing, and a waiting plan has nothing to refund.
		/// </para>
		/// </summary>
		/// <returns>False when no current blocker applies.</returns>
		public static bool PlanBlocked(KingdomSystem System, GameObject Marker, KingdomRules.BuildEntry Entry)
		{
			if (System == null || Marker == null || Entry == null)
			{
				return false;
			}
			string stageRefusal = KingdomCommission.StageRefusal(System, Entry);
			if (stageRefusal != null)
			{
				AnnounceOnce(System, Marker,
					"The plan staked at " + KingdomPresentation.Rich(System.KingdomDisplayName) + " waits. " + stageRefusal);
				return true;
			}
			Zone zone = Marker.CurrentZone;
			if (zone == null)
			{
				return true;
			}
			string refusal = null;
			if (Marker.HasIntProperty(PlanSchemaProperty))
			{
				if (TryFrozenPlanReady(System, Marker, Entry, out _, out _, out _, out _,
					out refusal))
				{
					Marker.SetStringProperty(BlockAnnouncedProperty, null, RemoveIfNull: true);
					return false;
				}
				AnnounceOnce(System, Marker, "The plan staked at " + KingdomPresentation.Rich(System.KingdomDisplayName)
					+ " waits. " + (refusal ?? "Its frozen production receipt cannot be proved."));
				return true;
			}
			if (!TryGetSpec(Entry.Key, out var spec))
			{
				Marker.SetStringProperty(BlockAnnouncedProperty, null, RemoveIfNull: true);
				return false;
			}
			KingdomSystem.Guard("plot plan", delegate
			{
				if (KingdomPlotRules.HeartRungOf(Entry.Key) > 0)
				{
					refusal = KingdomPlotRules.RefuseSecondHeart(KingdomPresentation.Rich(System.SeatName));
					return;
				}
				if (!KingdomPlotRules.Allows(System.Stage, spec.Size))
				{
					refusal = KingdomPlotRules.RefuseStage(spec.Size, KingdomPresentation.Rich(System.SeatName), System.Stage);
					return;
				}
				// Before the weather, because the way down is a fact about the ground and the
				// founder should hear it whichever building they asked for: a design refused for
				// want of sky in rock nobody has cut to would name the wrong lack twice over.
				refusal = KingdomDelve.Refusal(System, zone.ZoneID, Entry.Key, Entry.Name);
				if (refusal != null)
				{
					return;
				}
				if (KingdomPlotRules.IsUnderground(zone.Z) && spec.RequiresSky)
				{
					refusal = KingdomPlotRules.RefuseSky(Entry.Name);
					return;
				}
				if (KingdomPlotRules.RoofRefusesSky(spec))
				{
					refusal = KingdomPlotRules.RefuseRoofSky(Entry.Name, spec.Roof);
					return;
				}
				if (KingdomPlotRules.WouldExceedBudget(ReadPlots(zone), spec.Size, zone.Width, zone.Height))
				{
					refusal = KingdomPlotRules.RefuseBudget(KingdomPresentation.Rich(System.SeatName));
					return;
				}
				if (!TryFindRect(zone, System, Entry, spec, new GroundGrid(zone), Marker.CurrentCell, out _, out _, out var reason))
				{
					refusal = reason;
				}
			});
			if (refusal == null)
			{
				Marker.SetStringProperty(BlockAnnouncedProperty, null, RemoveIfNull: true);
				return false;
			}
			AnnounceOnce(System, Marker, "The plan staked at " + KingdomPresentation.Rich(System.KingdomDisplayName) + " waits. " + refusal);
			return true;
		}

	}
}
