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
		internal static bool TryPreparePlan(KingdomSystem System, GameObject Marker,
			KingdomRules.BuildEntry Entry, out KingdomPlotRules.PlotRect Rect,
			out string Payload, out long TotalTicks, out int MainX, out int MainY)
		{
			Rect = default(KingdomPlotRules.PlotRect);
			Payload = null;
			TotalTicks = 0L;
			MainX = 0;
			MainY = 0;
			Zone zone = Marker?.CurrentZone;
			Cell cell = Marker?.CurrentCell;
			if (System == null || zone == null || cell == null || Entry == null
				|| System.Stage < Entry.MinStage)
			{
				return false;
			}
			if (Marker.HasIntProperty(PlanSchemaProperty))
			{
				if (!TryFrozenPlanReady(System, Marker, Entry, out Rect, out Payload,
					out TotalTicks, out KingdomArchitectureIntent frozen,
					out string frozenFailure))
				{
					AnnounceOnce(System, Marker, "The plan staked at " + KingdomPresentation.Rich(System.KingdomDisplayName)
						+ " waits. " + (frozenFailure
							?? "Its frozen production receipt cannot be proved."));
					return false;
				}
				MainX = frozen.MainWorldX;
				MainY = frozen.MainWorldY;
				return true;
			}
			if (!TryGetSpec(Entry.Key, out var spec)) return false;
			if (!KingdomZoning.Permits(System, zone.ZoneID, Entry, out string zoningFailure))
			{
				AnnounceOnce(System, Marker,
					"The plan staked at " + KingdomPresentation.Rich(System.KingdomDisplayName) + " waits. " + zoningFailure);
				return false;
			}
			GroundGrid grid = new GroundGrid(zone);
			if (!TryFindRect(zone, System, Entry, spec, grid, cell, out Rect, out _,
				out string sitingFailure))
			{
				if (!string.IsNullOrEmpty(sitingFailure)) AnnounceOnce(System, Marker,
					"The plan staked at " + KingdomPresentation.Rich(System.KingdomDisplayName) + " waits. " + sitingFailure);
				return false;
			}
			string skin = Marker.GetStringProperty(KingdomDesign.PlannedSkinProperty);
			if (!TryPreparePlotPayload(System, zone, Rect, Entry.Key, Entry.Category, skin,
				out KingdomArchitectureIntent architecture, out Payload,
				out string architectureFailure))
			{
				AnnounceOnce(System, Marker, "The plan staked at " + KingdomPresentation.Rich(System.KingdomDisplayName)
					+ " waits. " + (architectureFailure
						?? "No authored architecture fits its exact ground."));
				Payload = null;
				return false;
			}
			if (!KingdomArchitectureRuntime.TryWorldFootprint(architecture,
				out KingdomPlotRules.PlotRect footprint, out string footprintFailure))
			{
				AnnounceOnce(System, Marker, "The plan staked at "
					+ KingdomPresentation.Rich(System.KingdomDisplayName) + " waits. "
					+ footprintFailure);
				Payload = null;
				return false;
			}
			MainX = architecture.MainWorldX;
			MainY = architecture.MainWorldY;
			Cell main = zone.GetCell(MainX, MainY);
			if (main == null || KingdomConstruction.HasActiveAt(System, zone, main))
			{
				AnnounceOnce(System, Marker, "The plan staked at " + KingdomPresentation.Rich(System.KingdomDisplayName)
					+ " waits. Its authored main ground already has paid construction in hand.");
				Payload = null;
				return false;
			}
			bool carved = KingdomPlotRules.IsUnderground(zone.Z);
			if (!KingdomArchitectureRuntime.TryRoofOnGround(architecture, carved,
				out KingdomPlotRules.RoofState roof, out string roofFailure))
			{
				AnnounceOnce(System, Marker, "The plan staked at "
					+ KingdomPresentation.Rich(System.KingdomDisplayName) + " waits. "
					+ roofFailure);
				Payload = null;
				return false;
			}
			TotalTicks = KingdomPlotRules.RaiseTicks(
				KingdomCommission.CraftBuildTicks(Entry.BuildTicks, System.ZoneDistricts.Values),
				grid.CellsOf(Rect), footprint, roof, carved);
			return TotalTicks > 0L;
		}

		/// <summary>
		/// Turns a staked plan into plotted works. Current markers carry the exact lot, authored map,
		/// main anchor, price, and labour quoted when the founder drove the adjacent survey stake;
		/// legacy markers retain their old marker-centred dynamic siting path.
		/// </summary>
		/// <returns>False for a design that is not a plot, leaving the caller's own
		/// scaffold path untouched.</returns>
		public static bool StakeFromPlan(KingdomSystem System, GameObject Marker, KingdomRules.BuildEntry Entry)
		{
			return false;
		}

		public static bool StakeFromPlan(KingdomSystem System, GameObject Marker,
			KingdomRules.BuildEntry Entry, KingdomConstructionJob Job,
			out KingdomConstructionJob Updated)
		{
			KingdomConstructionJob current = Job;
			Updated = current;
			if (System == null || Marker == null || Entry == null || !TryGetSpec(Entry.Key, out var spec))
			{
				KingdomConstruction.FinishProjection(ref Updated, false, false,
					"The plan no longer names a plotted design.");
				return false;
			}
			string stageRefusal = KingdomCommission.StageRefusal(System, Entry);
			if (stageRefusal != null)
			{
				KingdomConstruction.FinishProjection(ref Updated, false, false, stageRefusal);
				return false;
			}
			Zone zone = Marker.CurrentZone;
			Cell cell = Marker.CurrentCell;
			if (zone == null || cell == null)
			{
				KingdomConstruction.FinishProjection(ref Updated, false, false,
					"The plot plan marker is no longer on ground.");
				return false;
			}
			bool staked = false;
			bool yielding = false;
			GameObject projected = null;
			KingdomSystem.Guard("plot from plan", delegate
			{
				GroundGrid grid = new GroundGrid(zone);
				KingdomPlotRules.PlotRect rect;
				string skinKey;
				KingdomArchitectureIntent architecture;
				bool legacyArchitecture;
				if (!TryDecodePlotPayload(current.Payload, out rect, out skinKey,
					out architecture, out legacyArchitecture, out string payloadFailure)
					|| current.TargetKey != Entry.Key
					|| (!legacyArchitecture && (architecture == null
						|| architecture.BuildKey != Entry.Key
						|| current.X != architecture.MainWorldX
						|| current.Y != architecture.MainWorldY))
					|| (legacyArchitecture
						&& (current.X != rect.CenterX || current.Y != rect.CenterY)))
				{
					KingdomConstruction.Quarantine(ref current, payloadFailure
						?? "The paid plot plan has no valid frozen authored payload.");
					return;
				}
				// Read before the marker comes down and carried after the works stands, because a
				// plot measures its rect out of the marker's own cell and cannot leave it standing
				// while it does. Same fact the single-cell path transfers in one step
				// (KingdomPlanMarker.Realize), and it is what lets the chronicle quote a plan for a
				// house rather than only for a wall.
				string planQuote = KingdomCeremony.ReadPlanQuote(Marker);
				projected = Stake(System, zone, rect, Entry, spec, grid, skinKey,
					KingdomPlotRules.IsUnderground(zone.Z), architecture,
					legacyArchitecture, ref current);
				Cell main = legacyArchitecture ? zone.GetCell(rect.CenterX, rect.CenterY)
					: zone.GetCell(architecture.MainWorldX, architecture.MainWorldY);
				if (!ExpectedWorks(projected, main, Entry.Key, architecture, legacyArchitecture,
					current))
				{
					return;
				}
				KingdomCeremony.CarryPlanQuote(planQuote, projected);
				string markerId = Marker.IDIfAssigned;
				bool markerRemoved;
				try { markerRemoved = Marker.Destroy(null, Silent: true); }
				catch (System.Exception ex)
				{
					KingdomSurvey.ObserveCurrentTopologyInActive(zone, Marker);
					KingdomConstruction.Quarantine(ref current,
						"Plot plan-marker removal threw after works placement: " + ex.Message);
					return;
				}
				if (markerRemoved && !GameObject.Validate(Marker))
					KingdomSurvey.ObserveRemovedFromActive(zone, Marker);
				if (KingdomConstructionRules.ExactRemovalAction(true, markerRemoved,
					GameObject.Validate(Marker), KingdomConstruction.FindExactId(
						zone, markerId, out _) != KingdomPhysicalLookupState.Absent, true)
					!= KingdomExactRemovalAction.ProvedAbsent)
				{
					KingdomConstruction.Quarantine(ref current,
						"Plot plan-marker removal was vetoed, moved, replaced, or partially changed.");
					return;
				}
				if (!TryProvePlotPlanMarkerRemoval(System, zone, projected, false, markerId,
					ref current, out string removalFailure))
				{
					KingdomConstruction.Quarantine(ref current,
						removalFailure ?? "Plot plan endpoints changed during marker removal.");
					return;
				}
				if (!KingdomConstruction.UpdateSubject(ref current, projected.IDIfAssigned)) return;
				staked = true;
				yielding = projected.GetIntProperty(YieldingProperty) == 1;
			});
			Updated = current;
			if (staked)
			{
				KingdomConstruction.FinishProjection(ref Updated, true, true);
				string realm = KingdomPresentation.Rich(System.KingdomDisplayName);
				KingdomChronicle.Record(System, "the ground staked at " + realm + " was measured out for " + XRL.Language.Grammar.A(Entry.Name));
				System.Ledger.Note("{{G|The plan staked at " + realm + " is under way: the ground for the " + Entry.Name + " is measured out.}}");
				MessageQueue.AddPlayerMessage("{{G|The plan staked at " + realm + " is under way. The ground for the " + Entry.Name + " is measured out.}}");
				SayYielding(System, yielding, Entry.Name);
			}
			else
			{
				if (!string.IsNullOrEmpty(Updated.OutputId))
					KingdomConstruction.Quarantine(ref Updated,
						"Plot-plan projection crossed output publication without exact completion proof.");
				else KingdomConstruction.FinishProjection(ref Updated, false, false,
					"The paid plot plan could not be verified on its ground.");
			}
			return staked;
		}

		// --- The adoption path ------------------------------------------------------------

		/// <summary>
		/// Records the ground a founder-raised structure occupies when the design it was adopted
		/// under is a plot design. Nothing is stamped over what the founder built &mdash; their
		/// walls, their floor, their door, all untouched &mdash; the settlement simply learns that
		/// this much ground is spoken for, so later plots keep their lane from it and the road
		/// budget counts it.
		/// </summary>
		/// <returns>False when the design is not a plot design, which leaves adoption exactly as
		/// it was.</returns>
	}
}
