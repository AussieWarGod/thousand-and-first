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
		private static bool HasStrikePlotParts(Zone Z, KingdomPlotRules.PlotRect Rect,
			string PlotId)
		{
			KingdomSurvey active = KingdomSurvey.ActiveFor(Z);
			if (active != null)
			{
				for (int i = 0; i < active.PlotParts.Count; i++)
				{
					GameObject item = active.PlotParts[i];
					Cell cell = item?.CurrentCell;
					if (GameObject.Validate(item) && cell != null
						&& cell.X >= Rect.X1 && cell.X <= Rect.X2
						&& cell.Y >= Rect.Y1 && cell.Y <= Rect.Y2
						&& item.GetStringProperty(KingdomPlots.PlotIdProperty) == PlotId)
						return true;
				}
				return false;
			}
			for (int y = Rect.Y1; y <= Rect.Y2; y++)
			{
				for (int x = Rect.X1; x <= Rect.X2; x++)
				{
					Cell cell = Z.GetCell(x, y);
					if (cell == null) continue;
					List<GameObject> objects = cell.GetObjects();
					for (int i = 0; i < objects.Count; i++)
					{
						GameObject item = objects[i];
						if (GameObject.Validate(item)
							&& item.GetIntProperty(KingdomPlots.PlotPartProperty) == 1
							&& item.GetStringProperty(KingdomPlots.PlotIdProperty) == PlotId)
							return true;
					}
				}
			}
			return false;
		}

		private static bool ExactConversionOutput(GameObject Output, Zone Z,
			KingdomConstructionJob Job)
		{
			if (Z == null || Job == null || !KingdomPlots.TryDecodePlotPayload(Job.Payload,
				out KingdomPlotRules.PlotRect rect, out _,
				out KingdomArchitectureIntent architecture, out bool legacyArchitecture, out _)
				|| (!legacyArchitecture && (architecture == null
					|| architecture.BuildKey != Job.TargetKey
					|| Job.X != architecture.MainWorldX || Job.Y != architecture.MainWorldY))
				|| (legacyArchitecture && (Job.X != rect.CenterX || Job.Y != rect.CenterY))
				|| !GameObject.Validate(Output) || Output.ID != Job.OutputId
				|| Output.CurrentZone != Z || !KingdomConstruction.HasReceipt(Output, Job)
				|| !KingdomPlots.ExpectedArchitectureReceipt(Output, Z.GetCell(Job.X, Job.Y),
					Job.TargetKey, architecture, legacyArchitecture))
				return false;
			r_KingdomPlotWorks works = Output.GetPart<r_KingdomPlotWorks>();
			return (works != null && works.DesignKey == Job.TargetKey)
				|| (Output.GetIntProperty("KingdomBuilt") == 1
					&& Output.GetStringProperty(KingdomUpgrade.BuildKeyProperty) == Job.TargetKey);
		}

		private static bool ExactSocketOutput(GameObject Output, Zone Z,
			KingdomPlotRules.PlotRect Rect, KingdomStrikeIntent Intent,
			KingdomConstructionJob Job)
		{
			r_KingdomSocket socket = GameObject.Validate(Output)
				? Output.GetPart<r_KingdomSocket>() : null;
			if (socket == null || Output.ID != Job.OutputId || Output.CurrentZone != Z
				|| Output.CurrentCell != Z.GetCell(Rect.CenterX, Rect.CenterY)
				|| socket.LastDesignKey != Intent.BuildKey
				|| !KingdomConstruction.HasReceipt(Output, Job)
				|| !KingdomPlots.TryReadRect(Output, out var observed)) return false;
			return observed.X1 == Rect.X1 && observed.Y1 == Rect.Y1
				&& observed.X2 == Rect.X2 && observed.Y2 == Rect.Y2;
		}

		/// <summary>
		/// Called from <c>KingdomMaterials.WorkStrike</c> the instant a strike finishes, while
		/// <paramref name="Building"/> still stands and still carries its own stamped rect &mdash;
		/// see MODDING.md / the wiring note in <c>KingdomMaterials.cs</c> for exactly where.
		/// Does nothing, and returns false, for a building that never stood on a plot at all: every
		/// single-cell design in this mod is untouched by any of this.
		/// <para>
		/// For a plot design, this always sweeps the plot's own walls, floor, door, and
		/// furnishings off the rect (everything <c>KingdomPlots.Furnish</c> and
		/// <c>KingdomPlots.Apply</c> stamped with this same plot's own id) before doing anything
		/// else, because a struck plot whose shell is left standing is not a re-buildable slot; it
		/// is dead ground the survey will refuse forever. Then:
		/// </para>
		/// <list type="bullet">
		/// <item>if <see cref="ExecuteConvert"/> staged a true retype, the new design is projected
		/// on the distinct fresh site frozen in its paid receipt, with a new LotId. The old rectangle
		/// is left bare rather than renamed into the successor; one combined line is chronicled and
		/// the caller suppresses its ordinary "struck" message (return value <c>true</c>);</item>
		/// <item>otherwise, or if the restake could not land (a design withdrawn mid-strike, a torn
		/// down zone), the rect is left a plain <see cref="r_KingdomSocket"/> marker and the caller
		/// proceeds exactly as an ordinary strike always has (return value <c>false</c>).</item>
		/// </list>
		/// </summary>
		/// <returns>True when this call fully told the conversion's story and the caller's own
		/// "struck" chronicle/message should not also fire; false for every ordinary strike,
		/// where the caller's own messaging still applies unchanged.</returns>
		public static bool OnCleared(KingdomSystem System, Zone Z, GameObject Building)
		{
			if (System == null || Z == null || !GameObject.Validate(Building)
				|| Building.CurrentZone != Z) return false;
			string receipt = Building.GetStringProperty(KingdomConstruction.ReceiptProperty);
			if (string.IsNullOrEmpty(receipt)
				|| !KingdomConstruction.TryFind(receipt, out var construction)
				|| !KingdomConstruction.Owns(System, Z, construction)
				|| KingdomConstructionRules.IsTerminal(construction.Phase)
				|| (construction.Route != KingdomConstructionRoute.Strike
					&& construction.Route != KingdomConstructionRoute.SocketConvert)
				|| construction.SourceId != Building.ID
				|| construction.PhysicalPhase == KingdomPhysicalPhase.None
				|| !KingdomConstructionRules.TryDecodeStrikeIntent(
					construction.PhysicalReceipt, out var intent)) return false;
			if (intent.HasPlot)
			{
				if (!KingdomPlots.TryReadRect(Building, out var rect)
					|| rect.X1 != intent.X1 || rect.Y1 != intent.Y1
					|| rect.X2 != intent.X2 || rect.Y2 != intent.Y2
					|| Building.GetStringProperty(KingdomPlots.PlotIdProperty) != intent.PlotId)
					return false;
			}
			// Legacy hook owns no destructive mutation. Durable strike inspector alone may advance.
			KingdomMaterials.InspectConstruction(System, Z, construction);
			return false;
		}

		/// <summary>Removes every object this plot raised over its own rect &mdash; walls, floor,
		/// door, contents &mdash; leaving bare cells. Scoped to <paramref name="PlotId"/> so a
		/// neighbouring plot's own lane, which never overlaps this rect by construction (
		/// <c>KingdomPlotRules.CrowdsExisting</c>), is never touched even if IDs collide across
		/// zones somehow. Only ever objects the settlement itself created and marked
		/// (<c>KingdomPlots.PlotPartProperty</c>) &mdash; the protection law's own exemption,
		/// exercised here the same way striking already exercises it on the building itself.</summary>
		private static bool TrySweepLegacyPlotParts(Zone Z,
			KingdomPlotRules.PlotRect Rect, string PlotId, GameObject Owner)
		{
			if (Z == null || !GameObject.Validate(Owner)
				|| Owner.HasIntProperty(KingdomArchitectureRuntime.SchemaProperty)
				|| Owner.HasStringProperty(KingdomArchitectureRuntime.SchemaProperty)) return false;
			List<GameObject> targets = new List<GameObject>();
			for (int y = Rect.Y1; y <= Rect.Y2; y++)
			{
				for (int x = Rect.X1; x <= Rect.X2; x++)
				{
					Cell cell = Z.GetCell(x, y);
					if (cell == null)
					{
						continue;
					}
					List<GameObject> standing = new List<GameObject>(cell.GetObjects());
					for (int i = 0; i < standing.Count; i++)
					{
						GameObject item = standing[i];
						if (item == null || !GameObject.Validate(item) || item.GetIntProperty(KingdomPlots.PlotPartProperty) != 1)
						{
							continue;
						}
						if (!string.IsNullOrEmpty(PlotId) && item.GetStringProperty(KingdomPlots.PlotIdProperty) != PlotId)
						{
							continue;
						}
						if (item.Inventory != null && item.Inventory.Objects.Count != 0) return false;
						LiquidVolume liquid = item.GetPart<LiquidVolume>();
						if (liquid != null && liquid.Volume > 0) return false;
						if (item.GetIntProperty("KingdomCitizen") == 1
							|| item.GetIntProperty("KingdomStores") == 1
							|| item.GetIntProperty("KingdomLarder") == 1
							|| item.GetIntProperty(KingdomMaterials.StockpileProperty) == 1)
							return false;
						targets.Add(item);
					}
				}
			}
			for (int i = 0; i < targets.Count; i++)
			{
				GameObject target = targets[i];
				if (!GameObject.Validate(target))
					return false;
				bool removed = target.Obliterate(null, Silent: true);
				if (removed || !GameObject.Validate(target))
					KingdomSurvey.ObserveRemovedFromActive(Z, target);
				if (!removed || GameObject.Validate(target)) return false;
			}
			return true;
		}

		/// <summary>Leaves a socket marker at the rect's centre, stamped with the rect itself so
		/// every later siting pass counts it exactly as it counts a standing plot.</summary>
		private static void LeaveSocket(Zone Z, KingdomPlotRules.PlotRect Rect, string OldKey,
			KingdomConstructionJob Job = null)
		{
			Cell cell = Z.GetCell(Rect.CenterX, Rect.CenterY);
			if (cell == null)
			{
				return;
			}
			GameObject marker = GameObject.Create(SocketBlueprint);
			if (marker == null)
			{
				return;
			}
			r_KingdomSocket part = marker.GetPart<r_KingdomSocket>();
			if (part == null)
			{
				marker.Obliterate();
				return;
			}
			part.LastDesignKey = OldKey;
			KingdomPlots.StampRect(marker, Rect);
			if (Job != null)
			{
				KingdomConstruction.Bind(marker, Job);
			}
			GameObject accepted;
			try { accepted = cell.AddObject(marker); }
			catch
			{
				KingdomSurvey.ObserveCurrentTopologyInActive(Z, marker);
				throw;
			}
			KingdomSurvey.ObserveAddResultInActive(Z, marker, accepted);
			if (marker.CurrentCell != cell)
			{
				bool removed = marker.Obliterate(null, Silent: true);
				if (removed || !GameObject.Validate(marker))
					KingdomSurvey.ObserveRemovedFromActive(Z, marker);
			}
		}
	}
}
