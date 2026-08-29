using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomConstruction
	{
		/// <summary>Once every take reached carrier custody, exact residual stacks become ordinary
		/// physical stock again. Durable receipt identity still reserves them until terminal.</summary>
		private static bool ReleaseDebitedInputRemaindersOnActiveSource(
			KingdomConstructionJob job, KingdomConstructionInputReceipt receipt, Zone active,
			out string failure)
		{
			failure = null;
			if (active == null || KingdomSurvey.ActiveFor(active) == null) return false;
			for (int i = 0; i < receipt.SourceCount; i++)
			{
				KingdomConstructionInputSourceLine source = receipt.SourceAt(i);
				if (source.SourceZoneId != active.ZoneID || source.ResidualAfter <= 0
					|| source.Phase != KingdomConstructionInputSourcePhase.Debited
						&& source.Phase != KingdomConstructionInputSourcePhase.TransferIntent) continue;
				if (FindExactId(active, source.HolderId, out GameObject holder)
					!= KingdomPhysicalLookupState.Exact
					|| FindExactId(active, source.RemainderObjectId, out GameObject remainder)
						!= KingdomPhysicalLookupState.Exact)
				{
					failure = "Exact split remainder changed before attended custody release.";
					return false;
				}
				if (ExactRoutedSplitRemainderState(active, holder, job, receipt, source,
					remainder, null, 0)) continue;
				if (!ExactRoutedSplitRemainderState(active, holder, job, receipt, source,
					remainder, source.RemainderMarker, 1))
				{
					failure = "Split remainder acquired foreign route or stack custody.";
					return false;
				}
				if (!ExactRoutedSplitRemainderState(active, holder, job, receipt, source,
					remainder, source.RemainderMarker, 1)) return false;
				remainder.RemoveStringProperty(InputMarkerProperty);
				remainder.RemoveIntProperty("NeverStack");
				KingdomSurvey.ObserveChangedInActive(active, holder);
				if (!ExactRoutedSplitRemainderState(active, holder, job, receipt, source,
					remainder, null, 0)) return false;
			}
			return true;
		}

		private static bool ExactRoutedSplitRemainder(Zone zone, GameObject holder,
			KingdomConstructionJob job, KingdomConstructionInputReceipt receipt,
			KingdomConstructionInputSourceLine source, GameObject remainder)
		{
			return ExactRoutedSplitRemainderState(zone, holder, job, receipt, source,
					remainder, source.RemainderMarker, 1)
				|| ExactRoutedSplitRemainderState(zone, holder, job, receipt, source,
					remainder, null, 0);
		}

		private static bool ExactLiveRoutedSplitRemainder(Zone zone, GameObject holder,
			KingdomConstructionJob job, KingdomConstructionInputReceipt receipt,
			KingdomConstructionInputSourceLine source)
		{
			return source != null && source.ResidualAfter > 0
				&& !string.IsNullOrEmpty(source.RemainderObjectId)
				&& FindExactId(zone, source.RemainderObjectId, out GameObject remainder)
					== KingdomPhysicalLookupState.Exact
				&& ExactRoutedSplitRemainder(zone, holder, job, receipt, source, remainder);
		}

		private static bool ExactRoutedSplitRemainderState(Zone zone, GameObject holder,
			KingdomConstructionJob job, KingdomConstructionInputReceipt receipt,
			KingdomConstructionInputSourceLine source, GameObject remainder,
			string expectedMarker, int expectedNeverStack)
		{
			if (zone == null || source == null || !GameObject.Validate(remainder)) return false;
			bool markerExact = expectedMarker == null
				? !remainder.HasStringProperty(InputMarkerProperty)
					&& !remainder.HasIntProperty(InputMarkerProperty)
				: remainder.HasStringProperty(InputMarkerProperty)
					&& !remainder.HasIntProperty(InputMarkerProperty)
					&& remainder.GetStringProperty(InputMarkerProperty) == expectedMarker;
			return FindExactId(zone, source.HolderId, out GameObject exactHolder)
				== KingdomPhysicalLookupState.Exact && ReferenceEquals(exactHolder, holder)
				&& FindExactId(zone, source.RemainderObjectId, out GameObject exact)
				== KingdomPhysicalLookupState.Exact && ReferenceEquals(exact, remainder)
				&& GameObject.Validate(holder) && holder.Inventory != null
				&& holder.CurrentZone == zone && holder.CurrentCell == zone.GetCell(source.X, source.Y)
				&& GameObject.Validate(remainder) && remainder.Blueprint == source.Blueprint
				&& remainder.Count == source.ResidualAfter
				&& ReferenceEquals(remainder.InInventory, holder)
				&& ReferenceCount(holder.Inventory.Objects, remainder) == 1
				&& KingdomSurvey.ActiveFor(zone) != null
				&& source.DedicationOrdinal >= 0
				&& source.DedicationOrdinal
					< KingdomSurvey.ActiveFor(zone).MaterialStockpiles.Count
				&& ReferenceEquals(KingdomSurvey.ActiveFor(zone)
					.MaterialStockpiles[source.DedicationOrdinal], holder)
				&& markerExact && remainder.GetIntProperty("NeverStack") == expectedNeverStack
				&& !remainder.IsImportant() && remainder.Equipped == null
				&& remainder.IsTakeable() && !remainder.HasTag("AlwaysStack")
				&& !KingdomPurpose.HasProtectedCargoEvidence(remainder)
				&& KingdomOrdinaryCustody.TryProveEmpty(remainder, out string _)
				&& TryInputClassification(remainder, out KingdomConstructionInputKind kind,
					out string classification) && kind == source.Kind
				&& classification == source.Classification
				&& RoutedInputItemAuthorized(job, receipt, remainder);
		}
	}
}
