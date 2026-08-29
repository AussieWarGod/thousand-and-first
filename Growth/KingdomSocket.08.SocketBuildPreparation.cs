using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using ThousandAndFirst;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomSocket
	{
		// ==================================================================================
		// Building fresh on ground a strike already cleared
		// ==================================================================================

		private sealed class PreparedSocketBuild
		{
			public string MarkerId;
			public string SkinKey;
			public KingdomRules.BuildEntry Entry;
			public KingdomPlotRules.PlotRect Rect;
			public KingdomArchitectureIntent Architecture;
			public string Payload;
			public long LabourTicks;
		}

		/// <summary>
		/// Stakes a design on ground a strike already left a socket on. Runs every check an
		/// ordinary commission runs &mdash; style, stage, footprint, sky, zoning, water, material
		/// &mdash; minus the two that make no sense for ground the settlement already claimed
		/// (the plan cap and the road budget: this is not new plotted area, it is the same rect
		/// coming back into use) and pays the design's own full cost, with no strike effort added
		/// on top &mdash; nothing stands here to take down.
		/// </summary>
		public static bool BuildOnSocket(KingdomSystem System, Zone Z, GameObject Marker, string Key, string SkinKey, out string Failure)
		{
			if (!TryPrepareSocketBuild(System, Z, Marker, Key, SkinKey,
				out PreparedSocketBuild prepared, out Failure)) return false;
			return ExecuteSocketBuild(System, Z, Marker, prepared, out Failure);
		}

		private static bool TryPrepareSocketBuild(KingdomSystem System, Zone Z,
			GameObject Marker, string Key, string SkinKey, out PreparedSocketBuild Prepared,
			out string Failure)
		{
			Prepared = null;
			Failure = null;
			if (System == null || !System.Founded)
			{
				Failure = "You rule nothing yet.";
				return false;
			}
			if (Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				Failure = "A building is raised on a reserved lot in the kingdom's own ground, not in other people's streets.";
				return false;
			}
			if (Marker == null || !GameObject.Validate(Marker) || Marker.CurrentZone == null || Marker.CurrentZone.ZoneID != Z.ZoneID || Marker.GetPart<r_KingdomSocket>() == null)
			{
				Failure = "There is no cleared lot there to build on.";
				return false;
			}
			if (!TryReadSocketLot(Marker, out string frozenType,
				out ArchitectureLotSize frozenSize, out ArchitectureFacing frozenFacing,
				out bool legacySocket, out Failure)) return false;
			if (HasBlockingReceipt(Marker))
			{
				Failure = "That cleared lot already has construction work in hand.";
				return false;
			}
			if (KingdomConstruction.HasActiveSubject(System, Z,
				KingdomConstructionRoute.SocketBuild, Marker))
			{
				Failure = "That cleared lot already has a construction receipt in hand.";
				return false;
			}
			if (!KingdomPlots.TryReadRect(Marker, out KingdomPlotRules.PlotRect rect))
			{
				Failure = "That ground cannot be read.";
				return false;
			}
			if (!KingdomData.TryGetBuilding(Key, out KingdomRules.BuildEntry entry))
			{
				Failure = "No such design.";
				return false;
			}
			if (!KingdomPlots.TryGetSpec(Key, out KingdomPlotRules.PlotSpec spec))
			{
				Failure = KingdomSocketRules.RefuseNotAPlot(entry.Name);
				return false;
			}
			ArchitectureLotSize requestedSize = (ArchitectureLotSize)(int)spec.Size;
			if (!legacySocket
				&& (!KingdomArchitectureRules.TryClassifySetChange(frozenType, frozenSize,
					entry.Category, requestedSize, out ArchitectureSetChange setChange)
					|| setChange != ArchitectureSetChange.SameSet))
			{
				Failure = "That is " + SocketLotLabel(Marker)
					+ ". Rebuild its exact type and size, or order a full re-type while a predecessor still stands.";
				return false;
			}
			if (!KingdomRules.StyleAllows(entry.Styles, System.Style))
			{
				Failure = "The " + entry.Name + " is not built in this city's own style.";
				return false;
			}
			Failure = KingdomCommission.StageRefusal(System, entry);
			if (Failure != null)
			{
				return false;
			}
			if (!KingdomPlotRules.Allows(System.Stage, spec.Size))
			{
				Failure = KingdomPlotRules.RefuseStage(spec.Size, KingdomPresentation.Rich(System.SeatName), System.Stage);
				return false;
			}
			if (!KingdomPlotRules.TryDimensions(spec.Size, out int needWidth, out int needHeight))
			{
				Failure = "No such design.";
				return false;
			}
			if (!KingdomSocketRules.FootprintFits(rect.Width, rect.Height, needWidth, needHeight))
			{
				Failure = KingdomSocketRules.RefuseTooSmall(entry.Name, rect.Width, rect.Height, needWidth, needHeight);
				return false;
			}
			// The way down before the weather, for the same reason the conversion path asks it.
			Failure = KingdomDelve.Refusal(System, Z.ZoneID, entry.Key, entry.Name);
			if (Failure != null)
			{
				return false;
			}
			if (KingdomPlotRules.IsUnderground(Z.Z) && spec.RequiresSky)
			{
				Failure = KingdomPlotRules.RefuseSky(entry.Name);
				return false;
			}
			if (!KingdomZoning.Permits(System, Z.ZoneID, entry, out string zoningFailure))
			{
				Failure = zoningFailure;
				return false;
			}
			string lotType = legacySocket ? entry.Category : frozenType;
			if (!KingdomPlots.TryPreparePlotPayload(System, Z, rect, entry.Key, lotType,
				SkinKey,
				out KingdomArchitectureIntent architecture, out string payload, out Failure))
				return false;
			if (!SocketAcceptsArchitecture(Marker, architecture, out Failure)) return false;
			if (!TrySocketBuildLabour(System, Z, rect, entry, spec,
				out long labourTicks, out Failure)) return false;
			Prepared = new PreparedSocketBuild
			{
				MarkerId = Marker.IDIfAssigned, SkinKey = SkinKey, Entry = entry, Rect = rect,
				Architecture = architecture, Payload = payload, LabourTicks = labourTicks
			};
			return true;
		}

		private static bool TrySocketBuildLabour(KingdomSystem System, Zone Z,
			KingdomPlotRules.PlotRect Rect, KingdomRules.BuildEntry Entry,
			KingdomPlotRules.PlotSpec Spec, out long LabourTicks, out string Failure)
		{
			LabourTicks = 0L;
			Failure = null;
			if (System == null || Z == null || Entry == null || Spec == null)
			{
				Failure = "The cleared lot has no exact labour context.";
				return false;
			}
			KingdomPlots.GroundGrid grid = new KingdomPlots.GroundGrid(Z);
			KingdomPlots.HeartFor(Z, Rect, out int heartX, out int heartY);
			KingdomPlotRules.PlotRect footprint = KingdomPlots.FootprintFor(Rect, Spec,
				heartX, heartY);
			bool carved = KingdomPlotRules.IsUnderground(Z.Z);
			LabourTicks = KingdomPlotRules.RaiseTicks(
				KingdomCommission.CraftBuildTicks(Entry.BuildTicks,
					System.ZoneDistricts.Values), grid.CellsOf(Rect), footprint,
				KingdomPlotRules.RoofOnGround(Spec.Roof, carved), carved);
			if (LabourTicks > 0L) return true;
			Failure = "The cleared lot's exact labour quote is empty.";
			return false;
		}
	}
}
