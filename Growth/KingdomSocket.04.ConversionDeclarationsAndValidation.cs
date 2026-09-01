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
		/// <summary>Blueprint the socket marker stands as. Inherits vanilla <c>Sign</c> the same
		/// way every other inert stake in this mod does (<c>r_KingdomClearanceStake</c>,
		/// <c>r_KingdomNotice</c>) and carries only this file's own part &mdash; never
		/// <c>r_KingdomClearance</c>, which would make <c>KingdomMaterials.OnSettlementPass</c>
		/// mistake a socket for a live clearance order. See MODDING.md / ObjectBlueprints.xml for
		/// the declaration; this file only ever creates it, never defines it.</summary>
		public const string SocketBlueprint = "r_KingdomSocket";

		/// <summary>Registry key of the design a condemned building is being converted into,
		/// staged on the building itself the moment the founder orders the ceremony and read back
		/// by <see cref="OnCleared"/> once the crew finishes taking it down &mdash; which may be
		/// days, or a save and reload, later. A property rather than a part field for the reason
		/// every other staged choice in this mod (skins, plans) is one: STANDARDS.md &sect;1
		/// forbids appending a serialized field to a part that already ships, and the building
		/// this rides on ships today with no opinion about being converted.</summary>
		public const string PendingConvertKeyProperty = "KingdomConvertKey";

		/// <summary>The skin key chosen alongside <see cref="PendingConvertKeyProperty"/>, or
		/// absent for the new design's own unmodified look.</summary>
		public const string PendingConvertSkinProperty = "KingdomConvertSkin";

		// ==================================================================================
		// Convert: "change what this plot is" as one ceremony
		// ==================================================================================

		/// <summary>What one <see cref="Validate"/> pass resolved, so <see cref="AssessConvert"/>
		/// and <see cref="ExecuteConvert"/> never have to re-derive it separately and can never
		/// disagree about which design, which spec, or which rect they are talking about.</summary>
		private struct ConvertContext
		{
			public KingdomRules.BuildEntry OldEntry;
			public KingdomPlotRules.PlotSpec OldSpec;
			public KingdomRules.BuildEntry NewEntry;
			public KingdomPlotRules.PlotSpec NewSpec;
			public KingdomPlotRules.PlotRect Rect;
			public KingdomPlotRules.PlotRect TargetRect;
			public KingdomPlotRules.PlotSize ActualSize;
			public KingdomSocketRules.ChangeKind Kind;
			public KingdomSocketTransition Transition;
		}

		/// <summary>Read-only production receipt shared by conversion preview and commit.</summary>
		private sealed class PreparedConvert
		{
			public string BuildingId;
			public string SkinKey;
			public ConvertContext Context;
			public KingdomSocketRules.ConversionQuote Quote;
			public KingdomArchitectureIntent Architecture;
			public string Payload;
			public KingdomUpgrade.Assessment Improvement;
			public KingdomUpgrade.PreparedImprovement PreparedImprovement;
			public ArchitectureLayoutDelta Delta;
			public bool RequiresRestakePreflight;
		}

		/// <summary>
		/// Every eligibility check a conversion has to pass, run in the order a founder should
		/// read the refusals in: whose ground it is, whether the settlement actually raised it,
		/// whether it is free to be touched at all, then what the new design itself asks for.
		/// Read-only &mdash; spends nothing, strikes nothing, stakes nothing.
		/// </summary>
		private static bool Validate(KingdomSystem System, Zone Z, GameObject Building, string NewKey, out ConvertContext Context, out string Failure)
		{
			Context = default(ConvertContext);
			Failure = null;
			if (System == null || !System.Founded)
			{
				Failure = "You rule nothing yet.";
				return false;
			}
			if (Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				Failure = "A lot can only be replanned on the kingdom's own ground, not in other people's streets.";
				return false;
			}
			if (Building == null || !GameObject.Validate(Building) || Building.CurrentZone == null || Building.CurrentZone.ZoneID != Z.ZoneID)
			{
				Failure = "There is nothing there to change.";
				return false;
			}
			if (!KingdomUpgrade.IsFunctionallyBuilt(Building))
			{
				Failure = "The settlement converts what it raised. That is not one of its buildings.";
				return false;
			}
			if (!KingdomMirrorGate.TryPreflightRemoval(Building, Z, out Failure)) return false;
			if (HasBlockingReceipt(Building))
			{
				Failure = "That building already has construction work in hand.";
				return false;
			}
			if (KingdomConstruction.HasActiveSubject(System, Z,
				KingdomConstructionRoute.SocketConvert, Building))
			{
				Failure = "That building already has a conversion receipt in hand.";
				return false;
			}
			if (Building.GetIntProperty(KingdomAdopt.AdoptedProperty) == 1)
			{
				Failure = KingdomSocketRules.RefuseAdopted(Building.ShortDisplayName);
				return false;
			}
			if (Building.GetIntProperty(KingdomMaterials.StrikeEffortProperty) > 0)
			{
				Failure = KingdomSocketRules.RefuseCondemned(Building.ShortDisplayName);
				return false;
			}
			r_KingdomImprovement improvement = Building.GetPart<r_KingdomImprovement>();
			if (improvement != null && (improvement.Working || improvement.Held))
			{
				Failure = KingdomSocketRules.RefuseImproving(Building.ShortDisplayName);
				return false;
			}
			string oldKey = Building.GetStringProperty(KingdomUpgrade.BuildKeyProperty);
			if (!KingdomData.TryGetBuilding(oldKey, out KingdomRules.BuildEntry oldEntry) || !KingdomPlots.TryGetSpec(oldKey, out KingdomPlotRules.PlotSpec oldSpec))
			{
				Failure = KingdomSocketRules.RefuseNotAPlot(Building.ShortDisplayName);
				return false;
			}
			if (!KingdomPlots.TryReadRect(Building, out KingdomPlotRules.PlotRect rect))
			{
				Failure = KingdomSocketRules.RefuseNotAPlot(Building.ShortDisplayName);
				return false;
			}
			if (!KingdomSocketRules.TryActualSize(rect.Width, rect.Height,
				out KingdomPlotRules.PlotSize actualSize))
			{
				Failure = "The standing plot rectangle has no recognized actual size.";
				return false;
			}
			if (oldKey == NewKey)
			{
				Failure = KingdomSocketRules.RefuseAlreadyThat(Building.ShortDisplayName);
				return false;
			}
			if (!KingdomData.TryGetBuilding(NewKey, out KingdomRules.BuildEntry newEntry))
			{
				Failure = "No such design.";
				return false;
			}
			if (!KingdomPlots.TryGetSpec(NewKey, out KingdomPlotRules.PlotSpec newSpec))
			{
				Failure = KingdomSocketRules.RefuseNotAPlot(newEntry.Name);
				return false;
			}
			if (!KingdomRules.StyleAllows(newEntry.Styles, KingdomData.StyleKeys(System.Style)))
			{
				Failure = "The " + newEntry.Name + " is not built in this city's own style.";
				return false;
			}
			Failure = KingdomCommission.StageRefusal(System, newEntry);
			if (Failure != null)
			{
				return false;
			}
			if (!KingdomPlotRules.Allows(System.Stage, newSpec.Size))
			{
				Failure = KingdomPlotRules.RefuseStage(newSpec.Size, KingdomPresentation.Rich(System.SeatName), System.Stage);
				return false;
			}
			if (!KingdomPlotRules.TryDimensions(newSpec.Size, out int needWidth, out int needHeight))
			{
				Failure = "No such design.";
				return false;
			}
			KingdomSocketRules.ChangeKind kind = KingdomSocketRules.FitsSameSet(
				oldEntry.Category, actualSize, newEntry.Category, newSpec.Size)
				? KingdomSocketRules.ChangeKind.SameSet
				: KingdomSocketRules.ChangeKind.Retype;
			KingdomSocketTransition transition = null;
			KingdomPlotRules.PlotRect targetRect = rect;
			if (kind == KingdomSocketRules.ChangeKind.SameSet)
			{
				if (!KingdomWear.CanCarryStableState(Building, out Failure)) return false;
				KingdomArchitectureIntent standing;
				if (!KingdomArchitectureRuntime.TryRead(Building, out standing, out _)
					|| !KingdomArchitectureRules.IsLatestSnapshotEncoding(
						standing.EncodedSnapshot))
				{
					Failure = "That save-era plot has no exact authored transition delta. Strike it and commission fresh.";
					return false;
				}
				if (!KingdomSocketTransitions.TryGet(oldKey, NewKey, standing.LotType,
					standing.LotSize, out transition))
				{
					Failure = KingdomSocketTransitionRules.RefuseUndeclared(oldEntry.Name,
						newEntry.Name);
					return false;
				}
			}
			else
			{
				List<KingdomPlotRules.PlotRect> remaining = KingdomPlots.ReadPlots(Z);
				for (int i = remaining.Count - 1; i >= 0; i--)
				{
					KingdomPlotRules.PlotRect laid = remaining[i];
					if (laid.X1 == rect.X1 && laid.Y1 == rect.Y1
						&& laid.X2 == rect.X2 && laid.Y2 == rect.Y2)
					{
						remaining.RemoveAt(i);
						break;
					}
				}
				if (KingdomPlotRules.WouldExceedBudget(remaining, newSpec.Size,
					Z.Width, Z.Height))
				{
					Failure = KingdomPlotRules.RefuseBudget(KingdomPresentation.Rich(System.SeatName));
					return false;
				}
				KingdomLayoutRules.LayoutOutcome outcome;
				if (!KingdomPlots.TryFindRect(Z, System, newEntry, newSpec,
					new KingdomPlots.GroundGrid(Z), null, out targetRect, out outcome, out Failure))
					return false;
			}
			// The way down before the weather: a conversion is still a building raised, and rock
			// whose shaft was struck since this plot went up will not take another one.
			Failure = KingdomDelve.Refusal(System, Z.ZoneID, newEntry.Key, newEntry.Name);
			if (Failure != null)
			{
				return false;
			}
			if (KingdomPlotRules.IsUnderground(Z.Z) && newSpec.RequiresSky)
			{
				Failure = KingdomPlotRules.RefuseSky(newEntry.Name);
				return false;
			}
			if (!KingdomZoning.Permits(System, Z.ZoneID, newEntry, out string zoningFailure))
			{
				Failure = zoningFailure;
				return false;
			}
			Context.OldEntry = oldEntry;
			Context.OldSpec = oldSpec;
			Context.NewEntry = newEntry;
			Context.NewSpec = newSpec;
			Context.Rect = rect;
			Context.TargetRect = targetRect;
			Context.ActualSize = actualSize;
			Context.Kind = kind;
			Context.Transition = transition;
			return true;
		}
	}
}
