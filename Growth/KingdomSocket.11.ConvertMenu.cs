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
		/// <summary>
		/// The Charter's "change what stands on a lot" action. Standing beside a work the settlement
		/// raised offers a conversion; standing beside a cleared socket offers to build fresh on
		/// it. Both list only designs a plot may actually be raised as, and a live conversion's
		/// list is annotated with Addendum 2's own verb (<c>change</c>/<c>re-type</c>) for each
		/// choice before the founder commits to one.
		/// </summary>
		public static void OpenConvert(KingdomSystem System, GameObject Founder)
		{
			if (System == null || Founder == null)
			{
				return;
			}
			Zone zone = Founder.CurrentZone;
			Cell cell = Founder.CurrentCell;
			if (zone == null || cell == null || !System.ClaimedZones.Contains(zone.ZoneID))
			{
				Popup.Show("A lot can only be replanned on the kingdom's own ground.");
				return;
			}
			List<GameObject> buildings = new List<GameObject>();
			List<GameObject> sockets = new List<GameObject>();
			Func<GameObject, bool> isBuilding = o => o.GetIntProperty("KingdomBuilt") == 1 && KingdomPlots.TryReadRect(o, out _);
			Func<GameObject, bool> isSocket = o => o.GetPart<r_KingdomSocket>() != null;
			CollectNearby(cell, buildings, isBuilding);
			CollectNearby(cell, sockets, isSocket);
			foreach (Cell adjacent in cell.GetLocalAdjacentCells())
			{
				CollectNearby(adjacent, buildings, isBuilding);
				CollectNearby(adjacent, sockets, isSocket);
			}
			if (buildings.Count == 0 && sockets.Count == 0)
			{
				Popup.Show("Stand beside a plot " + KingdomPresentation.Rich(System.SeatName) + " raised, or ground it cleared, to change what stands there.");
				return;
			}
			List<string> options = new List<string>();
			List<GameObject> targets = new List<GameObject>();
			for (int i = 0; i < buildings.Count; i++)
			{
				options.Add(buildings[i].ShortDisplayName);
				targets.Add(buildings[i]);
			}
			int socketsStart = targets.Count;
			for (int i = 0; i < sockets.Count; i++)
			{
				options.Add("{{K|" + SocketLotLabel(sockets[i]) + "}}");
				targets.Add(sockets[i]);
			}
			int picked = Popup.PickOption(Title: "Change what stands on a lot, at " + KingdomPresentation.Rich(System.SeatName), Options: options.ToArray(), AllowEscape: true);
			if (picked < 0)
			{
				return;
			}
			GameObject target = targets[picked];
			bool onSocket = picked >= socketsStart;
			string currentCategory = null;
			string currentKey = null;
			KingdomPlotRules.PlotSize currentSize = KingdomPlotRules.PlotSize.None;
			KingdomArchitectureIntent standingArchitecture = null;
			string socketType = null;
			ArchitectureLotSize socketSize = default(ArchitectureLotSize);
			ArchitectureFacing socketFacing = default(ArchitectureFacing);
			bool legacySocket = false;
			KingdomPlotRules.PlotRect socketRect = default(KingdomPlotRules.PlotRect);
			if (!onSocket)
			{
				currentKey = target.GetStringProperty(KingdomUpgrade.BuildKeyProperty);
				KingdomArchitectureRuntime.TryRead(target, out standingArchitecture, out _);
				if (KingdomData.TryGetBuilding(currentKey, out KingdomRules.BuildEntry oldEntry) && KingdomPlots.TryGetSpec(currentKey, out KingdomPlotRules.PlotSpec oldSpec))
				{
					currentCategory = oldEntry.Category;
					if (!KingdomPlots.TryReadRect(target, out KingdomPlotRules.PlotRect actualRect)
						|| !KingdomSocketRules.TryActualSize(actualRect.Width, actualRect.Height,
						out currentSize)) currentSize = oldSpec.Size;
				}
			}
			else
			{
				if (!TryReadSocketLot(target, out socketType, out socketSize,
					out socketFacing, out legacySocket, out string socketReceiptFailure)
					|| !KingdomPlots.TryReadRect(target, out socketRect))
				{
					Popup.Show(socketReceiptFailure ?? "That cleared lot's rectangle cannot be read.");
					return;
				}
				if (legacySocket)
				{
					if (!KingdomSocketRules.TryActualSize(socketRect.Width, socketRect.Height,
						out KingdomPlotRules.PlotSize actualSocketSize))
					{
						Popup.Show("That legacy cleared lot has no recognized actual size.");
						return;
					}
					socketSize = (ArchitectureLotSize)(int)actualSocketSize;
				}
			}
			List<KingdomRules.BuildEntry> available = new List<KingdomRules.BuildEntry>();
			foreach (KingdomRules.BuildEntry entry in KingdomData.Buildings)
			{
				if (!KingdomPlots.TryGetSpec(entry.Key, out KingdomPlotRules.PlotSpec spec) || spec.Size == KingdomPlotRules.PlotSize.None)
				{
					continue;
				}
				// KingdomZoning.Offered rather than the two checks by hand: a settlement that
				// chooses its own next work must not choose a creed-work it has no way to.
				if (!KingdomZoning.Offered(System, entry))
				{
					continue;
				}
				if (!onSocket)
				{
					if (entry.Key == currentKey) continue;
					KingdomSocketRules.ChangeKind shown = KingdomSocketRules.FitsSameSet(
						currentCategory, currentSize, entry.Category, spec.Size)
						? KingdomSocketRules.ChangeKind.SameSet
						: KingdomSocketRules.ChangeKind.Retype;
					if (shown == KingdomSocketRules.ChangeKind.SameSet
						&& (standingArchitecture == null
							|| !KingdomSocketTransitions.TryGet(currentKey, entry.Key,
								standingArchitecture.LotType, standingArchitecture.LotSize, out _)))
						continue;
				}
				else if (legacySocket)
				{
					if (!KingdomArchitecture.TryGetMapping(entry.Key, entry.Category,
						socketSize, out _)) continue;
				}
				else
				{
					ArchitectureLotSize candidateSize = (ArchitectureLotSize)(int)spec.Size;
					if (!KingdomArchitectureRules.TryClassifySetChange(socketType, socketSize,
						entry.Category, candidateSize, out ArchitectureSetChange socketChange)
						|| socketChange != ArchitectureSetChange.SameSet
						|| !KingdomArchitectureRuntime.TryPrepare(System, zone, socketRect,
							entry.Key, socketType, out KingdomArchitectureIntent candidate, out _)
						|| candidate.Facing != socketFacing) continue;
				}
				available.Add(entry);
			}
			if (available.Count == 0)
			{
				Popup.Show("No plot design is known here.");
				return;
			}
			string[] designOptions = new string[available.Count];
			for (int i = 0; i < available.Count; i++)
			{
				string tag = "";
				if (!onSocket && KingdomPlots.TryGetSpec(available[i].Key, out KingdomPlotRules.PlotSpec size))
				{
					KingdomSocketRules.ChangeKind shown = KingdomSocketRules.FitsSameSet(
						currentCategory, currentSize, available[i].Category, size.Size)
						? KingdomSocketRules.ChangeKind.SameSet
						: KingdomSocketRules.ChangeKind.Retype;
					if (shown == KingdomSocketRules.ChangeKind.SameSet
						&& KingdomSocketTransitions.TryGet(currentKey, available[i].Key,
							standingArchitecture.LotType, standingArchitecture.LotSize,
							out KingdomSocketTransition transition))
					{
						string material = transition.Materials?.Describe();
						tag = " {{C|[change: " + transition.WaterDrams + " drams"
							+ (material == null ? "" : "; " + material)
							+ "; " + transition.WorkTicks + " ticks]}}";
					}
					else tag = " {{C|[re-type: full build " + available[i].CostDrams
						+ " drams]}}";
				}
				designOptions[i] = available[i].DisplayName
					+ (onSocket ? " {{C|[" + available[i].CostDrams + " drams]}}" : "")
					+ (onSocket && legacySocket
						? " {{y|[legacy lot: establishes type and facing]}}" : "") + tag;
			}
			int designPicked = Popup.PickOption(Title: onSocket
				? "Build on " + SocketLotLabel(target)
				: ("Change the " + target.ShortDisplayName + " into"),
				Options: designOptions, AllowEscape: true);
			if (designPicked < 0)
			{
				return;
			}
			KingdomRules.BuildEntry chosen = available[designPicked];
			string skinKey = KingdomDesign.ChooseSkin(chosen, System.Style)?.Key;
			if (onSocket)
			{
				if (!TryPrepareSocketBuild(System, zone, target, chosen.Key, skinKey,
					out PreparedSocketBuild socketBuild, out string socketFailure)
					|| !KingdomArchitecturePreview.TryRender(socketBuild.Architecture, chosen,
						socketBuild.LabourTicks, out string socketPreview, out socketFailure))
				{
					Popup.Show(socketFailure);
					return;
				}
				if (legacySocket)
					socketPreview = "This save-era cleared lot did not record its type or facing. "
						+ "The exact plan below establishes both now; nothing is inferred from what stood here.\n\n"
						+ socketPreview;
				int socketConfirmed = Popup.PickOption(Title: "Build exact plan: " + chosen.Name,
					Intro: socketPreview, Options: new string[1] { "Build this exact plan" },
					AllowEscape: true);
				if (socketConfirmed < 0) return;
				if (!ExecuteSocketBuild(System, zone, target, socketBuild, out socketFailure))
					Popup.Show(socketFailure);
				return;
			}
			if (!TryPrepareConvert(System, zone, target, chosen.Key, skinKey,
				out PreparedConvert conversion, out string assessFailure))
			{
				Popup.Show(assessFailure);
				return;
			}
			string productionPreview;
			bool rendered = conversion.Context.Kind == KingdomSocketRules.ChangeKind.SameSet
				? KingdomArchitecturePreview.TryRenderTransition(conversion.Architecture, chosen,
					conversion.Context.Transition, conversion.Delta, out productionPreview,
					out assessFailure)
				: KingdomArchitecturePreview.TryRenderRetype(conversion.Architecture, chosen,
					conversion.Quote, out productionPreview, out assessFailure);
			if (!rendered)
			{
				Popup.Show(assessFailure);
				return;
			}
			string question = productionPreview + "\n"
				+ KingdomSocketRules.DescribeConversion(target.ShortDisplayName, chosen.Name,
					conversion.Context.Kind, conversion.Quote);
			int confirmed = Popup.PickOption(Title: "Preview exact change: " + chosen.Name,
				Intro: question, Options: new string[1] { "Order this exact change" },
				AllowEscape: true);
			if (confirmed < 0) return;
			if (!ExecutePreparedConvert(System, zone, target, conversion,
				out string executeFailure))
			{
				Popup.Show(executeFailure);
			}
		}
	}
}
