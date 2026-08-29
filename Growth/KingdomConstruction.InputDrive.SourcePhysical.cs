using System;
using System.Globalization;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomConstruction
	{
		private static bool DriveInputSplit(KingdomSystem system, Zone zone,
			GameObject holder, GameObject carrier, GameObject item,
			ref KingdomConstructionJob job,
			KingdomConstructionInputReceipt receipt, KingdomConstructionInputSourceLine source,
			out string failure)
		{
			failure = null;
			string before = InputWitness("split", source.LineId, source.SourceObjectId,
				source.HolderId, source.Blueprint, source.Before, 0, 0);
			string after = InputWitness("split", source.LineId, source.SourceObjectId,
				source.HolderId, source.Blueprint, source.Take, source.ResidualAfter, 1);
			if (string.IsNullOrEmpty(source.BeforeWitnessHash))
				return InputSourceEvidence(ref job, receipt, source.Ordinal,
					source.RemainderObjectId, before, source.AfterWitnessHash,
					source.ProvedLost, out failure);
			if (string.IsNullOrEmpty(source.AfterWitnessHash))
				return InputSourceEvidence(ref job, receipt, source.Ordinal,
					source.RemainderObjectId, source.BeforeWitnessHash, after,
					source.ProvedLost, out failure);
			GameObject remainder;
			string observed = ObserveSplit(holder, item, source, out remainder);
			KingdomConstructionInputDecision decision =
				KingdomConstructionInputRules.DecidePhysicalMutation(before, after,
					observed, receipt.Paused);
			if (decision == KingdomConstructionInputDecision.WaitPaused) return false;
			if (decision == KingdomConstructionInputDecision.Apply)
			{
				if (!KingdomMaster.NewWorkAllowed(system)) return false;
				KingdomConstructionInputCargoLine callbackCargo =
					receipt.CargoAt(source.CargoOrdinal);
				string standingMarker = item.HasStringProperty(InputMarkerProperty)
					? item.GetStringProperty(InputMarkerProperty) : null;
				if (!ExactSourcePickupManifest(zone, carrier, job, receipt, callbackCargo,
						source)
					|| KingdomPurpose.HasProtectedCargoEvidence(item)
					|| standingMarker != null && standingMarker != source.RemainderMarker
					|| !ExactRoutedMaterialAtHolder(zone, holder, item, job, receipt, source,
						receipt.CargoAt(source.CargoOrdinal), source.Before, standingMarker,
						standingMarker == null ? 0 : 1))
				{
					failure = "The split source changed immediately before its physical callback.";
					return false;
				}
				try
				{
					item.SetIntProperty("NeverStack", 1);
					item.SetStringProperty(InputMarkerProperty, source.RemainderMarker);
					if (ExactRoutedMaterialAtHolder(zone, holder, item, job, receipt, source,
						receipt.CargoAt(source.CargoOrdinal), source.Before,
						source.RemainderMarker, 1))
						remainder = item.SplitStack(source.Take, holder, NoRemove: true);
					if (GameObject.Validate(remainder)
						&& !KingdomPurpose.HasProtectedCargoEvidence(item)
						&& !KingdomPurpose.HasProtectedCargoEvidence(remainder))
					{
						remainder.SetIntProperty("NeverStack", 1);
						remainder.SetStringProperty(InputMarkerProperty,
							source.RemainderMarker);
						item.SetStringProperty(InputMarkerProperty,
							receipt.CargoAt(source.CargoOrdinal).CargoKey);
					}
				}
				catch { remainder = null; }
				KingdomSurvey.ObserveChangedInActive(zone, holder);
				observed = ObserveSplit(holder, item, source, out remainder);
				if (!ExactSourcePickupManifest(zone, carrier, job, receipt, callbackCargo,
					source)) return false;
			}
			KingdomConstructionInputCargoLine splitCargo = receipt.CargoAt(source.CargoOrdinal);
			if (observed == after)
				NormalizeSplitCallbackCut(zone, holder, item, remainder, job, receipt,
					source, splitCargo);
			if (observed != after || !GameObject.Validate(remainder)
				|| !ExactRoutedMaterialAtHolder(zone, holder, item, job, receipt, source,
					splitCargo, source.Take, splitCargo.CargoKey, 1)
				|| KingdomPurpose.HasProtectedCargoEvidence(remainder)
				|| remainder.Blueprint != source.Blueprint
				|| remainder.Count != source.ResidualAfter
				|| !ReferenceEquals(remainder.InInventory, holder)
				|| ReferenceCount(holder.Inventory.Objects, remainder) != 1
				|| remainder.GetStringProperty(InputMarkerProperty) != source.RemainderMarker
				|| remainder.HasIntProperty(InputMarkerProperty)
				|| remainder.GetIntProperty("NeverStack") != 1
				|| remainder.IsImportant() || remainder.Equipped != null
				|| !remainder.IsTakeable() || remainder.HasTag("AlwaysStack")
				|| !KingdomOrdinaryCustody.TryProveEmpty(remainder, out string _)
				|| !TryInputClassification(remainder, out KingdomConstructionInputKind kind,
					out string classification) || kind != source.Kind
				|| classification != source.Classification
				|| !RoutedInputItemAuthorized(job, receipt, remainder))
			{
				failure = "The exact material stack split has an ambiguous aftermath.";
				return false;
			}
			if (string.IsNullOrEmpty(source.RemainderObjectId))
				return InputSourceEvidence(ref job, receipt, source.Ordinal, remainder.IDIfAssigned,
					source.BeforeWitnessHash, source.AfterWitnessHash, source.ProvedLost,
					out failure);
			if (source.RemainderObjectId != remainder.IDIfAssigned)
			{
				failure = "The split remainder identity changed after adoption.";
				return false;
			}
			return InputSourcePhase(ref job, receipt, source.Ordinal,
				KingdomConstructionInputSourcePhase.SplitProved, out failure);
		}

		private static bool DriveInputMaterialMove(KingdomSystem system, Zone zone,
			GameObject holder, GameObject carrier, GameObject item,
			ref KingdomConstructionJob job, KingdomConstructionInputReceipt receipt,
			KingdomConstructionInputSourceLine source, KingdomConstructionInputCargoLine cargo,
			out string failure)
		{
			failure = null;
			bool partial = source.ResidualAfter > 0;
			string before = InputWitness("move", source.LineId, source.SourceObjectId,
				source.HolderId, carrier.IDIfAssigned, source.SourceZoneId, "holder", source.Take);
			string after = InputWitness("move", source.LineId, source.SourceObjectId,
				source.HolderId, carrier.IDIfAssigned, source.SourceZoneId, "carrier", source.Take);
			if (!partial && string.IsNullOrEmpty(source.BeforeWitnessHash))
				return InputSourceEvidence(ref job, receipt, source.Ordinal,
					source.RemainderObjectId, before, source.AfterWitnessHash,
					source.ProvedLost, out failure);
			if (!partial && string.IsNullOrEmpty(source.AfterWitnessHash))
				return InputSourceEvidence(ref job, receipt, source.Ordinal,
					source.RemainderObjectId, source.BeforeWitnessHash, after,
					source.ProvedLost, out failure);
			int holderRefs = ReferenceCount(holder.Inventory.Objects, item);
			bool atHolder = ReferenceEquals(item.InInventory, holder)
				&& holderRefs == 1 && item.Count == source.Take;
			bool atCarrier = ReferenceEquals(item.InInventory, carrier)
				&& ReferenceCount(carrier.Inventory.Objects, item) == 1 && item.Count == source.Take;
			bool protectedCargo = KingdomPurpose.HasProtectedCargoEvidence(item);
			int routeNeverStack = protectedCargo ? -1 : 1;
			bool exactHolder = partial
				? ExactRoutedMaterialAtHolder(zone, holder, item, job, receipt,
					source, cargo, source.Take, cargo.CargoKey, 1)
				: ExactRoutedMaterialAtHolder(zone, holder, item, job, receipt,
					source, cargo, source.Take, null, protectedCargo ? -1 : 0)
					|| ExactRoutedMaterialAtHolder(zone, holder, item, job, receipt,
						source, cargo, source.Take, cargo.CargoKey, routeNeverStack);
			if (atHolder && !exactHolder) atHolder = false;
			if (atCarrier && !ExactRoutedMaterialAtCarrier(zone, carrier, item, job, receipt,
				source, cargo, cargo.CargoKey, routeNeverStack)) atCarrier = false;
			if (atHolder && !atCarrier)
			{
				if (!KingdomMaster.NewWorkAllowed(system)) return false;
				if (!ExactSourcePickupManifest(zone, carrier, job, receipt, cargo, source))
					return false;
				if (partial && !ExactLiveRoutedSplitRemainder(zone, holder, job,
					receipt, source)) return false;
				GameObject accepted = null;
				try
				{
					if (!protectedCargo) item.SetIntProperty("NeverStack", 1);
					item.SetStringProperty(InputMarkerProperty, cargo.CargoKey);
					// AddObject alone only rewrites Physics.InInventory; it does not remove the
					// exact reference from the old holder's Objects list. Use Qud's context-aware
					// transfer so one object cannot remain enumerated by both inventories.
					if (ExactRoutedMaterialAtHolder(zone, holder, item, job, receipt, source,
						cargo, source.Take, cargo.CargoKey, routeNeverStack)
						&& (!partial || ExactLiveRoutedSplitRemainder(zone, holder, job,
							receipt, source)))
						accepted = carrier.Inventory.AddObjectToInventory(item, null, Silent: true,
							NoStack: true);
				}
				catch { }
				finally
				{
					KingdomSurvey.ObserveChangedInActive(zone, holder);
					KingdomSurvey.ObserveChangedInActive(zone, carrier);
					KingdomSurvey.ObserveAddResultInActive(zone, item, accepted);
				}
				holderRefs = ReferenceCount(holder.Inventory.Objects, item);
				atHolder = ReferenceEquals(item.InInventory, holder) && holderRefs == 1;
				atCarrier = ReferenceEquals(accepted, item)
					&& ExactRoutedMaterialAtCarrier(zone, carrier, item, job, receipt, source,
						cargo, cargo.CargoKey, routeNeverStack);
				if (!ExactSourcePickupManifest(zone, carrier, job, receipt, cargo, source))
					return false;
			}
			if (!atCarrier || atHolder || holderRefs != 0)
			{
				failure = "The exact material object is outside source or carrier custody.";
				return false;
			}
			if (cargo.CustodyTopology != KingdomConstructionInputTopology.CarrierInventory
				|| cargo.CustodyOwnerId != carrier.IDIfAssigned)
				return InputCargoEvidence(ref job, receipt, cargo.Ordinal, cargo.ObjectId,
					KingdomConstructionInputTopology.CarrierInventory, carrier.IDIfAssigned,
					source.SourceZoneId, source.X, source.Y, cargo.BeforeWitnessHash,
					cargo.AfterWitnessHash, cargo.Spent, cargo.Lost, out failure);
			if (!ReleaseDebitedInputRemaindersOnActiveSource(job, receipt, zone, out failure))
				return false;
			return InputSourcePhase(ref job, receipt, source.Ordinal,
				KingdomConstructionInputSourcePhase.Debited, out failure);
		}
		private static bool ExactRoutedMaterialObject(KingdomConstructionJob job,
			KingdomConstructionInputReceipt receipt, KingdomConstructionInputSourceLine source,
			KingdomConstructionInputCargoLine cargo, GameObject item, int expectedCount,
			string expectedMarker, int expectedNeverStack)
		{
			if (!GameObject.Validate(item) || source == null || cargo == null) return false;
			bool markerExact = expectedMarker == null
				? !item.HasStringProperty(InputMarkerProperty)
					&& !item.HasIntProperty(InputMarkerProperty)
				: item.HasStringProperty(InputMarkerProperty)
					&& !item.HasIntProperty(InputMarkerProperty)
					&& item.GetStringProperty(InputMarkerProperty) == expectedMarker;
			return item.IDIfAssigned == source.SourceObjectId
				&& item.IDIfAssigned == cargo.ObjectId && item.Blueprint == source.Blueprint
				&& item.Count == expectedCount && markerExact
				&& (expectedNeverStack < 0
					|| item.GetIntProperty("NeverStack") == expectedNeverStack)
				&& !item.IsImportant() && item.Equipped == null && item.IsTakeable()
				&& !item.HasTag("AlwaysStack")
				&& KingdomOrdinaryCustody.TryProveEmpty(item, out string _)
				&& TryInputClassification(item, out KingdomConstructionInputKind kind,
					out string classification) && kind == source.Kind
				&& classification == source.Classification
				&& RoutedInputItemAuthorized(job, receipt, item);
		}
		private static bool ExactRoutedMaterialAtHolder(Zone zone, GameObject holder,
			GameObject item, KingdomConstructionJob job, KingdomConstructionInputReceipt receipt,
			KingdomConstructionInputSourceLine source, KingdomConstructionInputCargoLine cargo,
			int expectedCount, string expectedMarker, int expectedNeverStack)
		{
			return FindExactId(zone, source.HolderId, out GameObject exactHolder)
				== KingdomPhysicalLookupState.Exact && ReferenceEquals(exactHolder, holder)
				&& FindExactId(zone, source.SourceObjectId, out GameObject exactItem)
					== KingdomPhysicalLookupState.Exact && ReferenceEquals(exactItem, item)
				&& holder.Inventory != null && holder.CurrentZone == zone
				&& holder.CurrentCell == zone.GetCell(source.X, source.Y)
				&& ReferenceEquals(item.InInventory, holder)
				&& ReferenceCount(holder.Inventory.Objects, item) == 1
				&& ExactInputDedication(zone, source, holder, item)
				&& ExactRoutedMaterialObject(job, receipt, source, cargo, item,
					expectedCount, expectedMarker, expectedNeverStack);
		}
		private static bool ExactRoutedMaterialAtCarrier(Zone zone, GameObject carrier,
			GameObject item, KingdomConstructionJob job, KingdomConstructionInputReceipt receipt,
			KingdomConstructionInputSourceLine source, KingdomConstructionInputCargoLine cargo,
			string expectedMarker, int expectedNeverStack)
		{
			return FindExactId(zone, source.SourceObjectId, out GameObject exact)
				== KingdomPhysicalLookupState.Exact && ReferenceEquals(exact, item)
				&& GameObject.Validate(carrier) && carrier.Inventory != null
				&& ReferenceEquals(item.InInventory, carrier)
				&& ReferenceCount(carrier.Inventory.Objects, item) == 1
				&& ExactRoutedMaterialObject(job, receipt, source, cargo, item, source.Take,
					expectedMarker, expectedNeverStack);
		}
		private static string ObserveSplit(GameObject holder, GameObject source,
			KingdomConstructionInputSourceLine line, out GameObject remainder)
		{
			remainder = null;
			if (!GameObject.Validate(holder) || holder.Inventory == null
				|| !GameObject.Validate(source) || !ReferenceEquals(source.InInventory, holder)
				|| source.Blueprint != line.Blueprint) return InputWitness("invalid");
			int markerCount = 0;
			for (int i = 0; i < holder.Inventory.Objects.Count; i++)
			{
				GameObject held = holder.Inventory.Objects[i];
				if (ReferenceEquals(held, source) || !GameObject.Validate(held)
					|| held.GetStringProperty(InputMarkerProperty) != line.RemainderMarker) continue;
				markerCount++;
				if (markerCount == 1) remainder = held;
			}
			int remainderCount = markerCount == 1 && GameObject.Validate(remainder)
				? remainder.Count : 0;
			return InputWitness("split", line.LineId, line.SourceObjectId, line.HolderId,
				line.Blueprint, source.Count, remainderCount, markerCount);
		}

		private static string InputWitness(params object[] values)
		{
			System.Text.StringBuilder text = new System.Text.StringBuilder();
			for (int i = 0; i < values.Length; i++)
			{
				if (i > 0) text.Append('\0');
				IFormattable formatted = values[i] as IFormattable;
				text.Append(formatted == null ? values[i]?.ToString() ?? ""
					: formatted.ToString(null, CultureInfo.InvariantCulture));
			}
			return KingdomConstructionInputRules.HashBytes(
				KingdomConstructionInputRules.StrictUtf8.GetBytes(text.ToString()));
		}
	}
}
