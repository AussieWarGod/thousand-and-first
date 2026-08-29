using System.Collections.Generic;

using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomConstruction
	{
		private static int ReferenceCount(IList<GameObject> rows, GameObject exact)
		{
			int count = 0;
			for (int i = 0; rows != null && i < rows.Count; i++)
				if (ReferenceEquals(rows[i], exact)) count++;
			return count;
		}

		private static void NormalizeSplitCallbackCut(Zone zone, GameObject holder,
			GameObject item, GameObject remainder, KingdomConstructionJob job,
			KingdomConstructionInputReceipt receipt, KingdomConstructionInputSourceLine source,
			KingdomConstructionInputCargoLine cargo)
		{
			if (!GameObject.Validate(remainder)
				|| source.Phase != KingdomConstructionInputSourcePhase.SplitIntent
				|| item.GetStringProperty(InputMarkerProperty) != source.RemainderMarker
				|| !item.HasStringProperty(InputMarkerProperty)
				|| item.HasIntProperty(InputMarkerProperty)
				|| item.GetIntProperty("NeverStack") != 1
				|| remainder.GetStringProperty(InputMarkerProperty) != source.RemainderMarker
				|| !remainder.HasStringProperty(InputMarkerProperty)
				|| remainder.HasIntProperty(InputMarkerProperty)
				|| remainder.GetIntProperty("NeverStack") != 1
				|| item.Count != source.Take || remainder.Count != source.ResidualAfter
				|| item.Blueprint != source.Blueprint || remainder.Blueprint != source.Blueprint
				|| !ReferenceEquals(item.InInventory, holder)
				|| !ReferenceEquals(remainder.InInventory, holder)
				|| ReferenceCount(holder.Inventory.Objects, item) != 1
				|| ReferenceCount(holder.Inventory.Objects, remainder) != 1
				|| item.IsImportant() || item.Equipped != null || !item.IsTakeable()
				|| item.HasTag("AlwaysStack") || remainder.IsImportant()
				|| remainder.Equipped != null || !remainder.IsTakeable()
				|| remainder.HasTag("AlwaysStack")
				|| KingdomPurpose.HasProtectedCargoEvidence(item)
				|| KingdomPurpose.HasProtectedCargoEvidence(remainder)
				|| !KingdomOrdinaryCustody.TryProveEmpty(remainder, out string _)
				|| !TryInputClassification(remainder, out KingdomConstructionInputKind kind,
					out string classification) || kind != source.Kind
				|| classification != source.Classification
				|| !RoutedInputItemAuthorized(job, receipt, remainder)) return;
			item.SetStringProperty(InputMarkerProperty, cargo.CargoKey);
			KingdomSurvey.ObserveChangedInActive(zone, holder);
		}
	}
}
