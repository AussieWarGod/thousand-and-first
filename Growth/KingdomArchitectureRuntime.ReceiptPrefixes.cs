using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitectureRuntime
	{
		private static bool TryAcceptReceiptPrefix(GameObject Target,
			KingdomArchitectureIntent Expected, out bool Complete, out string Failure)
		{
			Complete = false;
			Failure = null;
			if (Target.HasStringProperty(SchemaProperty))
				return Fail("architecture receipt schema has an opposite-type collision",
					out Failure);
			if (Target.HasIntProperty(SchemaProperty))
			{
				if (Target.GetIntProperty(SchemaProperty) != ReceiptSchema)
					return Fail("architecture receipt target carries another schema", out Failure);
				KingdomArchitectureIntent observed;
				if (!TryRead(Target, out observed, out Failure)) return false;
				if (!SameFrozenIntent(observed, Expected))
					return Fail("architecture receipt target carries another frozen intent",
						out Failure);
				Complete = true;
				return true;
			}
			if (!ArchitectureStringPrefix(Target, BuildKeyProperty, Expected.BuildKey)
				|| !ArchitectureStringPrefix(Target, PlanKeyProperty, Expected.PlanKey)
				|| !ArchitectureStringPrefix(Target, BindingKeyProperty, Expected.BindingKey)
				|| !ArchitectureStringPrefix(Target, TierKeyProperty, Expected.TierKey)
				|| !ArchitectureStringPrefix(Target, VariantKeyProperty, Expected.VariantKey)
				|| !ArchitectureStringPrefix(Target, PaletteKeyProperty, Expected.PaletteKey)
				|| !ArchitectureStringPrefix(Target, LotTypeProperty, Expected.LotType)
				|| !ArchitectureIntPrefix(Target, LotSizeProperty, (int)Expected.LotSize)
				|| !ArchitectureIntPrefix(Target, FacingProperty, (int)Expected.Facing)
				|| !ArchitectureStringPrefix(Target, SnapshotProperty, Expected.EncodedSnapshot)
				|| !ArchitectureStringPrefix(Target, HashProperty, Expected.SnapshotHash)
				|| !ArchitectureIntPrefix(Target, RectX1Property, Expected.Rect.X1)
				|| !ArchitectureIntPrefix(Target, RectY1Property, Expected.Rect.Y1)
				|| !ArchitectureIntPrefix(Target, RectX2Property, Expected.Rect.X2)
				|| !ArchitectureIntPrefix(Target, RectY2Property, Expected.Rect.Y2)
				|| !ArchitectureIntPrefix(Target, MainXProperty, Expected.MainWorldX)
				|| !ArchitectureIntPrefix(Target, MainYProperty, Expected.MainWorldY))
				return Fail("architecture receipt prefix carries a third or opposite-type value",
					out Failure);
			return true;
		}

		private static bool ArchitectureStringPrefix(GameObject Target, string Property,
			string Expected)
		{
			return KingdomArchitectureReceiptPrefixRules.ExactOrAbsentString(
				Target.HasStringProperty(Property), Target.GetStringProperty(Property),
				Target.HasIntProperty(Property), Expected);
		}

		private static bool ArchitectureIntPrefix(GameObject Target, string Property,
			int Expected)
		{
			return KingdomArchitectureReceiptPrefixRules.ExactOrAbsentInt(
				Target.HasIntProperty(Property), Target.GetIntProperty(Property),
				Target.HasStringProperty(Property), Expected);
		}

		private static bool SameFrozenIntent(KingdomArchitectureIntent A,
			KingdomArchitectureIntent B)
		{
			return A != null && B != null && A.SchemaVersion == B.SchemaVersion
				&& A.EncodedSnapshot == B.EncodedSnapshot && A.SnapshotHash == B.SnapshotHash
				&& A.Rect.X1 == B.Rect.X1 && A.Rect.Y1 == B.Rect.Y1
				&& A.Rect.X2 == B.Rect.X2 && A.Rect.Y2 == B.Rect.Y2
				&& A.MainWorldX == B.MainWorldX && A.MainWorldY == B.MainWorldY;
		}

		private static bool TryReadExactFrozen(GameObject Target,
			KingdomArchitectureIntent Expected, out string Failure)
		{
			KingdomArchitectureIntent observed;
			return TryRead(Target, out observed, out Failure)
				&& (SameFrozenIntent(observed, Expected)
					|| Fail("architecture receipt changed during publication", out Failure));
		}
	}
}
