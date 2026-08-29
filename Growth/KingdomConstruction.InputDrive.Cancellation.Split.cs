using System;

using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomConstruction
	{
		private static bool RestoreCancelledSplit(KingdomSystem system, Zone zone,
			GameObject holder, GameObject carrier, GameObject item,
			ref KingdomConstructionJob job,
			KingdomConstructionInputReceipt receipt, KingdomConstructionInputSourceLine source,
			KingdomConstructionInputCargoLine cargo, out string failure)
		{
			failure = null;
			if (KingdomPurpose.HasProtectedCargoEvidence(item))
			{ failure = "The split source acquired protected cargo evidence."; return false; }
			if (string.IsNullOrEmpty(source.RemainderObjectId))
			{
				int marked = FindActiveCancellationMarker(zone, source.RemainderMarker,
					out GameObject unpublished);
				if (marked < 0 || marked > 2)
				{ failure = "The split marker is ambiguous."; return false; }
				if (marked == 2 && ExactCancellationSplitPair(zone, holder, item, job,
					receipt, source, cargo, out unpublished))
					return InputSourceEvidence(ref job, receipt, source.Ordinal,
						unpublished.IDIfAssigned, source.BeforeWitnessHash,
						source.AfterWitnessHash, source.ProvedLost, out failure);
				if (marked == 1 && ReferenceEquals(unpublished, item)
					&& source.Phase == KingdomConstructionInputSourcePhase.RestoreIntent
					&& cargo.Phase == KingdomConstructionInputCargoPhase.ReleaseIntent
					&& ExactRoutedMaterialAtHolder(zone, holder, item, job, receipt,
						source, cargo, source.Before, source.RemainderMarker, 1))
				{
					if (!KingdomMaster.NewWorkAllowed(system)) return false;
					if (!ExactCancellationCarrierManifest(system, zone, job, receipt, cargo,
						carrier)) return false;
					item.SetStringProperty(InputMarkerProperty, cargo.CargoKey);
					KingdomSurvey.ObserveChangedInActive(zone, holder);
					return ExactCancellationCarrierManifest(system, zone, job, receipt, cargo,
						carrier) && ExactRoutedMaterialAtHolder(zone, holder, item, job, receipt,
						source, cargo, source.Before, cargo.CargoKey, 1);
				}
				if (marked == 1)
				{
					if (!ExactRoutedMaterialAtHolder(zone, holder, item, job, receipt,
							source, cargo, source.Take, cargo.CargoKey, 1)
						|| !GameObject.Validate(unpublished)
						|| unpublished.Blueprint != source.Blueprint
						|| unpublished.Count != source.ResidualAfter
						|| !ReferenceEquals(unpublished.InInventory, holder)
						|| ReferenceCount(holder.Inventory.Objects, unpublished) != 1
						|| !unpublished.HasStringProperty(InputMarkerProperty)
						|| unpublished.HasIntProperty(InputMarkerProperty)
						|| unpublished.GetStringProperty(InputMarkerProperty)
							!= source.RemainderMarker
						|| unpublished.GetIntProperty("NeverStack") != 1
						|| unpublished.IsImportant() || unpublished.Equipped != null
						|| !unpublished.IsTakeable() || unpublished.HasTag("AlwaysStack")
						|| KingdomPurpose.HasProtectedCargoEvidence(unpublished)
						|| !KingdomOrdinaryCustody.TryProveEmpty(unpublished, out string _)
						|| !TryInputClassification(unpublished,
							out KingdomConstructionInputKind kind, out string classification)
						|| kind != source.Kind || classification != source.Classification
						|| !RoutedInputItemAuthorized(job, receipt, unpublished))
					{ failure = "The unpublished split remainder left its exact holder."; return false; }
					return InputSourceEvidence(ref job, receipt, source.Ordinal,
						unpublished.IDIfAssigned, source.BeforeWitnessHash,
						source.AfterWitnessHash, source.ProvedLost, out failure);
				}
				string marker = item.HasStringProperty(InputMarkerProperty)
					? item.GetStringProperty(InputMarkerProperty) : null;
				if (item.Count != source.Before || marker != null && marker != cargo.CargoKey
					|| !ExactRoutedMaterialAtHolder(zone, holder, item, job, receipt, source,
						cargo, source.Before, marker, marker == null ? 0 : 1))
				{ failure = "The marker-free pre-split state is not proved."; return false; }
				return true;
			}
			GameObject remainder;
			bool graveyard;
			KingdomPhysicalLookupState state = FindGlobalInputId(receipt,
				source.RemainderObjectId, out remainder, out graveyard);
			if (item.Count == source.Before)
				return state == KingdomPhysicalLookupState.Exact && graveyard
					&& ExactGraveyardSplitRemainder(job, receipt, source, remainder)
					&& ExactRoutedMaterialAtHolder(zone, holder, item, job, receipt, source,
						cargo, source.Before, cargo.CargoKey, 1);
			if (item.Count != source.Take)
			{ failure = "The split source count is neither before nor taken."; return false; }
			if (state == KingdomPhysicalLookupState.Exact && !graveyard)
			{
				if (item.GetStringProperty(InputMarkerProperty) == source.RemainderMarker
					&& ExactCancellationSplitPair(zone, holder, item, job, receipt,
						source, cargo, out GameObject pair)
					&& ReferenceEquals(pair, remainder))
				{
					if (!KingdomMaster.NewWorkAllowed(system)) return false;
					if (!ExactCancellationCarrierManifest(system, zone, job, receipt, cargo,
						carrier)) return false;
					item.SetStringProperty(InputMarkerProperty, cargo.CargoKey);
					KingdomSurvey.ObserveChangedInActive(zone, holder);
					return ExactCancellationCarrierManifest(system, zone, job, receipt, cargo,
						carrier) && ExactRoutedMaterialAtHolder(zone, holder, item, job, receipt,
						source, cargo, source.Take, cargo.CargoKey, 1);
				}
				if (!ExactRoutedMaterialAtHolder(zone, holder, item, job, receipt, source,
						cargo, source.Take, cargo.CargoKey, 1)
					|| !ExactRoutedSplitRemainder(zone, holder, job, receipt, source, remainder))
				{ failure = "The exact split remainder left its holder."; return false; }
				if (!KingdomMaster.NewWorkAllowed(system)) return false;
				if (!ExactRoutedMaterialAtHolder(zone, holder, item, job, receipt, source,
						cargo, source.Take, cargo.CargoKey, 1)
					|| !ExactRoutedSplitRemainder(zone, holder, job, receipt, source, remainder)
					|| !ExactCancellationCarrierManifest(system, zone, job, receipt, cargo,
						carrier))
					return false;
				bool removed = false;
				try { removed = remainder.Obliterate(null, Silent: true); } catch { }
				KingdomSurvey.ObserveChangedInActive(zone, holder);
				state = FindGlobalInputId(receipt, source.RemainderObjectId,
					out remainder, out graveyard);
				if (!removed && !graveyard) return false;
			}
			if (state != KingdomPhysicalLookupState.Exact || !graveyard
				|| !ExactGraveyardSplitRemainder(job, receipt, source, remainder))
			{ failure = "The split restoration aftermath is ambiguous."; return false; }
			if (!KingdomMaster.NewWorkAllowed(system)
				|| !ExactRoutedMaterialAtHolder(zone, holder, item, job, receipt, source,
					cargo, source.Take, cargo.CargoKey, 1)
				|| !ExactCancellationCarrierManifest(system, zone, job, receipt, cargo,
					carrier)) return false;
			try { item.Count = source.Before; holder.Inventory.FlushWeightCache();
				item.FlushContextWeightCaches(); } catch { }
			KingdomSurvey.ObserveChangedInActive(zone, holder);
			return ExactCancellationCarrierManifest(system, zone, job, receipt, cargo,
				carrier) && ExactRoutedMaterialAtHolder(zone, holder, item, job, receipt,
				source, cargo, source.Before, cargo.CargoKey, 1);
		}

		private static bool ExactGraveyardSplitRemainder(KingdomConstructionJob job,
			KingdomConstructionInputReceipt receipt, KingdomConstructionInputSourceLine source,
			GameObject remainder)
		{
			return remainder != null && remainder.IsInGraveyard()
				&& remainder.IDIfAssigned == source.RemainderObjectId
				&& remainder.Blueprint == source.Blueprint
				&& remainder.Count == source.ResidualAfter
				&& remainder.HasStringProperty(InputMarkerProperty)
				&& !remainder.HasIntProperty(InputMarkerProperty)
				&& remainder.GetStringProperty(InputMarkerProperty) == source.RemainderMarker
				&& remainder.GetIntProperty("NeverStack") == 1
				&& !remainder.IsImportant() && remainder.Equipped == null
				&& remainder.IsTakeable() && !remainder.HasTag("AlwaysStack")
				&& !KingdomPurpose.HasProtectedCargoEvidence(remainder)
				&& KingdomOrdinaryCustody.TryProveRetiredEmpty(remainder, out string _)
				&& TryInputClassification(remainder, out KingdomConstructionInputKind kind,
					out string classification) && kind == source.Kind
				&& classification == source.Classification
				&& RetiredRoutedInputItemAuthorized(job, receipt, remainder);
		}

		private static bool ExactCancellationSplitWriteAheadStanding(Zone zone,
			GameObject holder, GameObject item, KingdomConstructionJob job,
			KingdomConstructionInputReceipt receipt, KingdomConstructionInputSourceLine source,
			KingdomConstructionInputCargoLine cargo)
		{
			if (ExactRoutedMaterialAtHolder(zone, holder, item, job, receipt, source, cargo,
				source.Before, source.RemainderMarker, 1))
				return FindActiveCancellationMarker(zone, source.RemainderMarker,
					out GameObject exact) == 1 && ReferenceEquals(exact, item);
			return source.ResidualAfter > 0 && item.Count == source.Take
				&& ExactCancellationSplitPair(zone, holder, item, job, receipt,
					source, cargo, out GameObject _);
		}

		private static bool ExactCancellationSplitPair(Zone zone, GameObject holder,
			GameObject item, KingdomConstructionJob job, KingdomConstructionInputReceipt receipt,
			KingdomConstructionInputSourceLine source, KingdomConstructionInputCargoLine cargo,
			out GameObject remainder)
		{
			remainder = null;
			if (!ExactRoutedMaterialAtHolder(zone, holder, item, job, receipt, source, cargo,
				source.Take, source.RemainderMarker, 1)
				|| FindActiveCancellationMarker(zone, source.RemainderMarker,
					out GameObject _) != 2) return false;
			int matches = 0;
			for (int i = 0; holder?.Inventory != null && i < holder.Inventory.Objects.Count; i++)
			{
				GameObject candidate = holder.Inventory.Objects[i];
				if (ReferenceEquals(candidate, item) || !GameObject.Validate(candidate)
					|| candidate.GetStringProperty(InputMarkerProperty)
						!= source.RemainderMarker) continue;
				matches++; remainder = candidate;
			}
			return matches == 1 && remainder.Blueprint == source.Blueprint
				&& remainder.Count == source.ResidualAfter
				&& ReferenceEquals(remainder.InInventory, holder)
				&& ReferenceCount(holder.Inventory.Objects, remainder) == 1
				&& remainder.HasStringProperty(InputMarkerProperty)
				&& !remainder.HasIntProperty(InputMarkerProperty)
				&& remainder.GetIntProperty("NeverStack") == 1
				&& !remainder.IsImportant() && remainder.Equipped == null
				&& remainder.IsTakeable() && !remainder.HasTag("AlwaysStack")
				&& !KingdomPurpose.HasProtectedCargoEvidence(remainder)
				&& KingdomOrdinaryCustody.TryProveEmpty(remainder, out string _)
				&& TryInputClassification(remainder, out KingdomConstructionInputKind kind,
					out string classification) && kind == source.Kind
				&& classification == source.Classification
				&& RoutedInputItemAuthorized(job, receipt, remainder);
		}

		private static bool CloseCancelledLine(KingdomSystem system, Zone active,
			ref KingdomConstructionJob job,
			KingdomConstructionInputReceipt receipt, KingdomConstructionInputSourceLine source,
			KingdomConstructionInputCargoLine cargo, string ownerId, string zoneId,
			int x, int y, out string failure)
		{
			failure = null;
			if (cargo.Phase == KingdomConstructionInputCargoPhase.ReleaseIntent
				&& cargo.CustodyTopology != KingdomConstructionInputTopology.Released)
				return InputCargoEvidence(ref job, receipt, cargo.Ordinal, cargo.ObjectId,
					KingdomConstructionInputTopology.Released, ownerId, zoneId, x, y,
					cargo.BeforeWitnessHash, cargo.AfterWitnessHash, cargo.Spent, cargo.Lost,
					out failure);
			if (cargo.Phase == KingdomConstructionInputCargoPhase.CompensationIntent
				&& cargo.CustodyTopology != (source.Kind == KingdomConstructionInputKind.Water
					? KingdomConstructionInputTopology.Consumed
					: KingdomConstructionInputTopology.Returned))
				return InputCargoEvidence(ref job, receipt, cargo.Ordinal, cargo.ObjectId,
					source.Kind == KingdomConstructionInputKind.Water
						? KingdomConstructionInputTopology.Consumed
						: KingdomConstructionInputTopology.Returned,
					ownerId, zoneId, x, y, cargo.BeforeWitnessHash, cargo.AfterWitnessHash,
					cargo.Spent, cargo.Lost, out failure);
			if (cargo.Phase == KingdomConstructionInputCargoPhase.ReleaseIntent)
				return InputCargoPhase(ref job, receipt, cargo.Ordinal,
					KingdomConstructionInputCargoPhase.Released, out failure);
			if (cargo.Phase == KingdomConstructionInputCargoPhase.CompensationIntent)
				return InputCargoPhase(ref job, receipt, cargo.Ordinal,
					KingdomConstructionInputCargoPhase.Compensated, out failure);
			if (source.Phase == KingdomConstructionInputSourcePhase.RestoreIntent)
			{
				if (!KingdomMaster.NewWorkAllowed(system)) return false;
				if (!ReleaseInputLineMarkers(job, receipt, source, active)
					|| !ExactCancellationSourceStanding(active, job, receipt, source, cargo, null))
					return false;
				return InputSourcePhase(ref job, receipt, source.Ordinal,
					KingdomConstructionInputSourcePhase.Restored, out failure);
			}
			if (source.Phase == KingdomConstructionInputSourcePhase.CompensationIntent)
			{
				if (!KingdomMaster.NewWorkAllowed(system)) return false;
				if (!ReleaseInputLineMarkers(job, receipt, source, active)
					|| !ExactCancellationSourceStanding(active, job, receipt, source, cargo, null))
					return false;
				return InputSourcePhase(ref job, receipt, source.Ordinal,
					KingdomConstructionInputSourcePhase.Compensated, out failure);
			}
			return CancellationLineComplete(source, cargo);
		}
	}
}
