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
		/// <summary>
		/// Finishes the plot: furnishes the interior from the design's own contents table the way
		/// vanilla huts furnish, raises the object that stands for the building, hands it every
		/// property the rest of the settlement reads a work by, and takes the works down.
		/// </summary>
		private static bool Finish(r_KingdomPlotWorks Works, Zone Z,
			KingdomPlotRules.PlotRect Rect, KingdomPlotRules.PlotRect Footprint,
			KingdomPlotRules.RoofState Roof)
		{
			GameObject parent = Works.ParentObject;
			Cell cell = parent?.CurrentCell;
			if (cell == null || !KingdomData.TryGetBuilding(Works.DesignKey, out var entry))
			{
				return false;
			}
			bool architectureMarker = HasArchitectureReceiptEvidence(parent);
			KingdomArchitectureIntent architecture = null;
			bool legacyArchitecture = !architectureMarker;
			if (architectureMarker
				&& (!KingdomArchitectureRuntime.TryRead(parent, out architecture,
					out string architectureFailure)
					|| architecture.BuildKey != Works.DesignKey || !SameRect(architecture.Rect, Rect)
					|| cell.X != architecture.MainWorldX || cell.Y != architecture.MainWorldY))
			{
				KingdomLog.Log("architecture: completed plot receipt refused: "
					+ (architectureFailure ?? "receipt identity or main anchor changed"));
				return false;
			}
			bool currentAuthored = architecture != null
				&& KingdomArchitectureRules.IsCurrentSnapshotEncoding(
					architecture.EncodedSnapshot);
			if (currentAuthored && !KingdomArchitectureStamper.TryVerifyComplete(parent, Z,
				out string layoutFailure))
			{
				KingdomLog.Log("architecture: completed plot layout refused: " + layoutFailure);
				return false;
			}
			string id = parent.GetStringProperty(PlotIdProperty);
			string skinColorString = parent.GetStringProperty(KingdomDesign.StagedColorStringProperty);
			string skinDetailColor = parent.GetStringProperty(KingdomDesign.StagedDetailColorProperty);
			string skinRenderString = parent.GetStringProperty(KingdomDesign.StagedRenderStringProperty);
			string skinTile = parent.GetStringProperty(KingdomDesign.StagedTileProperty);
			int defence = Works.DefencePending;
			bool heart = parent.GetIntProperty(HeartPlotProperty) == 1;
			bool yielding = parent.GetIntProperty(YieldingProperty) == 1;
			int staff = Works.StaffNeeded;
			bool threshold = Works.ThresholdManning;
			string contents = Works.ContentsTable;
			string displayName = Works.DisplayName ?? entry.Name;
			// Read before the works comes down, not after: everything the founder chose when they
			// staked this ground rides on the works object, and the works is about to stop being a
			// thing to read from. The plan quote and the due tick are the raising ceremony's own
			// two facts, and they are read here for exactly the same reason.
			string planQuote = KingdomCeremony.ReadPlanQuote(parent);
			long completeTick;
			if (!TryGetPlotWorkLong(parent, PlotWorkCompletedTickProperty, out completeTick))
			{
				// Legacy plot works have no labour receipt and preserve their old nominal tick.
				completeTick = Works.StartTick + Works.TotalTicks;
			}
			string receipt = parent.GetStringProperty(KingdomConstruction.ReceiptProperty);
			KingdomConstructionJob construction = null;
			if (!string.IsNullOrEmpty(receipt))
			{
				KingdomSystem currentSystem = The.Game == null
					? null : The.Game.RequireSystem<KingdomSystem>();
				if (!KingdomConstruction.TryFind(receipt, out construction)
					|| !KingdomConstruction.Owns(currentSystem, Z, construction)
					|| KingdomConstructionRules.IsTerminal(construction.Phase)
					|| (construction.Phase != KingdomConstructionPhase.ProjectionPending
						&& !KingdomConstruction.BeginProjection(ref construction, out _)))
				{
					return false;
				}
				if (!TryDecodePlotPayload(construction.Payload, out var paidRect, out _,
					out KingdomArchitectureIntent paidArchitecture, out bool legacyPayload,
					out string payloadFailure) || !SameRect(paidRect, Rect)
					|| legacyPayload != legacyArchitecture
					|| (!legacyPayload && (!SameIntent(paidArchitecture, architecture)
						|| construction.X != architecture.MainWorldX
						|| construction.Y != architecture.MainWorldY))
					|| (legacyPayload
						&& (construction.X != Rect.CenterX || construction.Y != Rect.CenterY)))
				{
					KingdomConstruction.Quarantine(ref construction, payloadFailure
						?? "The plot works disagree with their frozen authored payload.");
					return false;
				}
			}
			GameObject building;
			bool created;
			if (!TryFinishOutput(Works, Z, Rect, Footprint, Roof, parent, cell, entry,
				architecture, legacyArchitecture, currentAuthored, id, skinColorString,
				skinDetailColor, skinRenderString, skinTile, displayName, completeTick,
				planQuote, heart, yielding, defence, staff, threshold, receipt,
				ref construction, out building, out created)) return false;
			if (!TryFinishRemoval(Z, cell, Footprint, entry, parent, building, created, currentAuthored,
				contents, id, receipt, ref construction)) return false;
			return TryFinishEffects(Z, cell, entry, building, Rect, currentAuthored, heart,
				displayName, completeTick, planQuote, ref construction);
		}
	}
}
