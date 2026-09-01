using System;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Engine publication and exact reproof for an adopted room's staffing contract.</summary>
	public static class KingdomAdoptionOperation
	{
		public const int ReceiptSchema = 1;
		public const string SchemaProperty = "r_TAF_AdoptOperationSchema";
		public const string ReceiptProperty = "r_TAF_AdoptOperationReceipt";
		public const string RevisionProperty = "r_TAF_AdoptOperationRevision";
		public const string CategoryProperty = "r_TAF_AdoptWorkCategory";
		public const string ThresholdProperty = "KingdomThresholdManning";

		public static bool TryStamp(GameObject Root, KingdomRules.BuildEntry Entry,
			out KingdomAdoptionOperationReceipt Receipt, out string Failure)
		{
			Receipt = null; Failure = null;
			if (!GameObject.Validate(Root) || Entry == null)
				return Fail("adoption operation target or design is absent", out Failure);
			if (!KingdomAdoptionOperationRules.RequiresContract(Entry.Category, Entry.Staff))
			{
				Clear(Root); return true;
			}
			if (!KingdomAdoptionOperationRules.TryCreate(Root.ID, Entry.Key, Entry.Category,
				Entry.Staff, string.Equals(Entry.Manning, "threshold",
					StringComparison.OrdinalIgnoreCase), out Receipt, out Failure)) return false;
			return TryPublish(Root, Receipt, out Failure);
		}

		public static bool TryRead(GameObject Root,
			out KingdomAdoptionOperationReceipt Receipt, out string Failure)
		{
			if (!GameObject.Validate(Root) || Root.Blueprint != KingdomAdopt.WorkMarkerBlueprint
				|| Root.GetIntProperty(KingdomAdopt.AdoptedProperty) != 1
				|| Root.GetIntProperty(KingdomAdopt.BuiltProperty) != 1)
			{
				Receipt = null;
				return Fail("adopted operation has no exact civic marker", out Failure);
			}
			if (!TryReadProjection(Root, out Receipt, out Failure)) return false;
			return KingdomAdoptionDesignation.TryRead(Root,
				out KingdomAdoptionDesignationReceipt designation, out Failure)
				&& KingdomAdoptionDesignation.TryReproveLocal(Root, designation, out Failure);
		}

		internal static bool TryPublish(GameObject Root,
			KingdomAdoptionOperationReceipt Receipt, out string Failure)
		{
			Failure = null;
			if (!GameObject.Validate(Root) || Receipt == null || Root.ID != Receipt.RootId)
				return Fail("adoption operation target or identity is malformed", out Failure);
			string encoded = KingdomAdoptionOperationRules.Encode(Receipt);
			if (encoded == null)
				return Fail("adoption operation receipt cannot be encoded", out Failure);
			try
			{
				Clear(Root);
				Root.SetStringProperty(KingdomUpgrade.BuildKeyProperty, Receipt.BuildingKey);
				Root.SetIntProperty(KingdomAdopt.StaffNeededProperty, Receipt.StaffNeeded);
				Root.SetIntProperty(ThresholdProperty, Receipt.ThresholdManning ? 1 : 0);
				Root.SetStringProperty(CategoryProperty, Receipt.Category);
				Root.SetStringProperty(ReceiptProperty, encoded);
				Root.SetStringProperty(RevisionProperty, Receipt.Revision);
				Root.SetIntProperty(SchemaProperty, ReceiptSchema);
			}
			catch (Exception exception)
			{
				if (TryReadProjection(Root, out KingdomAdoptionOperationReceipt existing, out _)
					&& existing.Revision == Receipt.Revision) return true;
				return Fail("adoption operation publication remains retryable: "
					+ exception.GetType().Name, out Failure);
			}
			return TryReadProjection(Root, out KingdomAdoptionOperationReceipt read, out Failure)
				&& read.Revision == Receipt.Revision;
		}

		internal static bool HasState(GameObject Root)
		{
			return Root != null && (Root.HasIntProperty(SchemaProperty)
				|| Root.HasStringProperty(SchemaProperty)
				|| Root.HasIntProperty(ReceiptProperty) || Root.HasStringProperty(ReceiptProperty)
				|| Root.HasIntProperty(RevisionProperty) || Root.HasStringProperty(RevisionProperty)
				|| Root.HasIntProperty(CategoryProperty) || Root.HasStringProperty(CategoryProperty));
		}

		public static void Clear(GameObject Root)
		{
			if (Root == null) return;
			bool projection = HasState(Root) || Root.Blueprint == KingdomAdopt.WorkMarkerBlueprint;
			ClearTyped(Root, SchemaProperty); ClearTyped(Root, ReceiptProperty);
			ClearTyped(Root, RevisionProperty); ClearTyped(Root, CategoryProperty);
			if (!projection) return;
			ClearTyped(Root, KingdomUpgrade.BuildKeyProperty);
			ClearTyped(Root, KingdomAdopt.StaffNeededProperty);
			ClearTyped(Root, ThresholdProperty);
			ClearTyped(Root, "KingdomStaffed");
			ClearTyped(Root, "KingdomEffectiveness");
			ClearTyped(Root, KingdomCrews.IdentityAffinityProperty);
		}

		private static bool TryReadProjection(GameObject Root,
			out KingdomAdoptionOperationReceipt Receipt, out string Failure)
		{
			Receipt = null; Failure = null;
			if (!GameObject.Validate(Root) || !Root.HasIntProperty(SchemaProperty)
				|| Root.HasStringProperty(SchemaProperty)
				|| Root.GetIntProperty(SchemaProperty) != ReceiptSchema
				|| !Root.HasStringProperty(ReceiptProperty) || Root.HasIntProperty(ReceiptProperty)
				|| !Root.HasStringProperty(RevisionProperty) || Root.HasIntProperty(RevisionProperty)
				|| !Root.HasStringProperty(CategoryProperty) || Root.HasIntProperty(CategoryProperty)
				|| !Root.HasStringProperty(KingdomUpgrade.BuildKeyProperty)
				|| Root.HasIntProperty(KingdomUpgrade.BuildKeyProperty)
				|| !Root.HasIntProperty(KingdomAdopt.StaffNeededProperty)
				|| Root.HasStringProperty(KingdomAdopt.StaffNeededProperty)
				|| !Root.HasIntProperty(ThresholdProperty) || Root.HasStringProperty(ThresholdProperty))
				return Fail("adoption operation receipt is absent or incomplete", out Failure);
			if (!KingdomAdoptionOperationRules.TryDecode(Root.GetStringProperty(ReceiptProperty),
				out Receipt, out Failure)) return false;
			if (Receipt.RootId != Root.IDIfAssigned
				|| Receipt.Revision != Root.GetStringProperty(RevisionProperty)
				|| Receipt.BuildingKey != Root.GetStringProperty(KingdomUpgrade.BuildKeyProperty)
				|| Receipt.Category != Root.GetStringProperty(CategoryProperty)
				|| Receipt.StaffNeeded != Root.GetIntProperty(KingdomAdopt.StaffNeededProperty)
				|| Receipt.ThresholdManning != (Root.GetIntProperty(ThresholdProperty) == 1))
				return Fail("adoption operation receipt disagrees with its projection", out Failure);
			if (Root.GetIntProperty(ThresholdProperty) != 0
				&& Root.GetIntProperty(ThresholdProperty) != 1)
				return Fail("adoption operation manning projection is malformed", out Failure);
			if (!KingdomAdoptionDesignation.TryRead(Root,
				out KingdomAdoptionDesignationReceipt designation, out Failure)
				|| designation.ContainerOnly || designation.RootId != Receipt.RootId
				|| designation.BuildingKey != Receipt.BuildingKey)
				return Failure != null ? false
					: Fail("adoption operation disagrees with its exact room", out Failure);
			return true;
		}

		private static void ClearTyped(GameObject Root, string Property)
		{
			Root.RemoveIntProperty(Property); Root.RemoveStringProperty(Property);
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
