using System;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitectureRuntime
	{
		public static bool TryFreeze(GameObject Target, KingdomArchitectureIntent Intent,
			out string Failure)
		{
			ArchitectureLayoutSnapshot snapshot;
			if (!TryValidateIntent(Intent, out snapshot, out Failure)) return false;
			if (Target == null) return Fail("architecture receipt target is absent", out Failure);
			bool complete;
			if (!TryAcceptReceiptPrefix(Target, Intent, out complete, out Failure)) return false;
			if (complete) return true;
			try
			{
				Target.SetStringProperty(BuildKeyProperty, Intent.BuildKey);
				Target.SetStringProperty(PlanKeyProperty, Intent.PlanKey);
				Target.SetStringProperty(BindingKeyProperty, Intent.BindingKey);
				Target.SetStringProperty(TierKeyProperty, Intent.TierKey);
				Target.SetStringProperty(VariantKeyProperty, Intent.VariantKey);
				Target.SetStringProperty(PaletteKeyProperty, Intent.PaletteKey);
				Target.SetStringProperty(LotTypeProperty, Intent.LotType);
				Target.SetIntProperty(LotSizeProperty, (int)Intent.LotSize);
				Target.SetIntProperty(FacingProperty, (int)Intent.Facing);
				Target.SetStringProperty(SnapshotProperty, Intent.EncodedSnapshot);
				Target.SetStringProperty(HashProperty, Intent.SnapshotHash);
				Target.SetIntProperty(RectX1Property, Intent.Rect.X1);
				Target.SetIntProperty(RectY1Property, Intent.Rect.Y1);
				Target.SetIntProperty(RectX2Property, Intent.Rect.X2);
				Target.SetIntProperty(RectY2Property, Intent.Rect.Y2);
				Target.SetIntProperty(MainXProperty, Intent.MainWorldX);
				Target.SetIntProperty(MainYProperty, Intent.MainWorldY);
				Target.SetIntProperty(SchemaProperty, ReceiptSchema);
			}
			catch (Exception exception)
			{
				string ignored;
				if (TryReadExactFrozen(Target, Intent, out ignored)) return true;
				return Fail("architecture receipt publication remains retryable: "
					+ exception.Message, out Failure);
			}
			return TryReadExactFrozen(Target, Intent, out Failure);
		}

		/// <summary>Reads and proves a complete canonical receipt without consulting live data.</summary>
		public static bool TryRead(GameObject Source, out KingdomArchitectureIntent Intent,
			out string Failure)
		{
			Intent = null;
			Failure = null;
			if (Source == null) return Fail("architecture receipt source is absent", out Failure);
			if (!Source.HasIntProperty(SchemaProperty)
				|| Source.HasStringProperty(SchemaProperty))
				return Fail("architecture receipt is absent or only partially written", out Failure);
			int schema = Source.GetIntProperty(SchemaProperty);
			if (schema != ReceiptSchema)
				return Fail("architecture receipt schema " + schema + " is unknown", out Failure);

			string buildKey;
			string planKey;
			string bindingKey;
			string tierKey;
			string variantKey;
			string paletteKey;
			string lotType;
			string encoded;
			string hash;
			int lotSize;
			int facing;
			int x1;
			int y1;
			int x2;
			int y2;
			int mainX;
			int mainY;
			if (!ReadString(Source, BuildKeyProperty, KingdomArchitectureRules.MaxKeyChars,
				out buildKey, out Failure)
				|| !ReadString(Source, PlanKeyProperty, KingdomArchitectureRules.MaxKeyChars,
					out planKey, out Failure)
				|| !ReadString(Source, BindingKeyProperty, KingdomArchitectureRules.MaxKeyChars,
					out bindingKey, out Failure)
				|| !ReadString(Source, TierKeyProperty, KingdomArchitectureRules.MaxKeyChars,
					out tierKey, out Failure)
				|| !ReadString(Source, VariantKeyProperty, KingdomArchitectureRules.MaxKeyChars,
					out variantKey, out Failure)
				|| !ReadString(Source, PaletteKeyProperty, KingdomArchitectureRules.MaxKeyChars,
					out paletteKey, out Failure)
				|| !ReadString(Source, LotTypeProperty, KingdomArchitectureRules.MaxKeyChars,
					out lotType, out Failure)
				|| !ReadInt(Source, LotSizeProperty, out lotSize, out Failure)
				|| !ReadInt(Source, FacingProperty, out facing, out Failure)
				|| !ReadString(Source, SnapshotProperty, KingdomArchitectureRules.MaxSnapshotChars,
					out encoded, out Failure)
				|| !ReadString(Source, HashProperty, 64, out hash, out Failure)
				|| !ReadInt(Source, RectX1Property, out x1, out Failure)
				|| !ReadInt(Source, RectY1Property, out y1, out Failure)
				|| !ReadInt(Source, RectX2Property, out x2, out Failure)
				|| !ReadInt(Source, RectY2Property, out y2, out Failure)
				|| !ReadInt(Source, MainXProperty, out mainX, out Failure)
				|| !ReadInt(Source, MainYProperty, out mainY, out Failure)) return false;

			KingdomArchitectureIntent read = KingdomArchitectureIntent.CreateRaw(schema,
				buildKey, planKey, bindingKey, tierKey, variantKey, paletteKey, lotType,
				(ArchitectureLotSize)lotSize, (ArchitectureFacing)facing, encoded, hash,
				new KingdomPlotRules.PlotRect(x1, y1, x2, y2), mainX, mainY);
			ArchitectureLayoutSnapshot snapshot;
			if (!TryValidateIntent(read, out snapshot, out Failure)) return false;
			Intent = read;
			return true;
		}

		/// <summary>
		/// Copies a works receipt to its final behavior root. Source is fully read before Target is
		/// touched; no architecture catalogue or current building entry is consulted.
		/// </summary>
		public static bool TryCopyFrozen(GameObject Source, GameObject Target, out string Failure)
		{
			KingdomArchitectureIntent intent;
			if (!TryRead(Source, out intent, out Failure)) return false;
			return TryFreeze(Target, intent, out Failure);
		}

		public static bool TryValidate(KingdomArchitectureIntent Intent, out string Failure)
		{
			ArchitectureLayoutSnapshot snapshot;
			return TryValidateIntent(Intent, out snapshot, out Failure);
		}

		public static bool TryDecode(KingdomArchitectureIntent Intent,
			out ArchitectureLayoutSnapshot Snapshot, out string Failure)
		{
			return TryValidateIntent(Intent, out Snapshot, out Failure);
		}

		private static bool TryValidateIntent(KingdomArchitectureIntent Intent,
			out ArchitectureLayoutSnapshot Snapshot, out string Failure)
		{
			Snapshot = null;
			Failure = null;
			if (Intent == null) return Fail("architecture intent is absent", out Failure);
			if (Intent.SchemaVersion != ReceiptSchema)
				return Fail("architecture intent schema is absent or unknown", out Failure);
			if (!ValidKey(Intent.BuildKey) || !ValidKey(Intent.PlanKey)
				|| !ValidKey(Intent.BindingKey) || !ValidKey(Intent.TierKey)
				|| !ValidKey(Intent.VariantKey) || !ValidKey(Intent.PaletteKey)
				|| !ValidKey(Intent.LotType))
				return Fail("architecture intent scalar identity is malformed", out Failure);
			if (string.IsNullOrEmpty(Intent.EncodedSnapshot)
				|| Intent.EncodedSnapshot.Length > KingdomArchitectureRules.MaxSnapshotChars)
				return Fail("architecture intent snapshot is absent or over the bound", out Failure);
			if (!CanonicalHash(Intent.SnapshotHash))
				return Fail("architecture intent hash is malformed", out Failure);
			ArchitectureLayoutSnapshot snapshot;
			if (!KingdomArchitectureRules.TryDecodeSnapshot(Intent.EncodedSnapshot,
				out snapshot, out Failure)) return false;
			string hash;
			if (!KingdomArchitectureRules.TryEncodedSnapshotHash(Intent.EncodedSnapshot,
				out hash, out Failure)
				|| hash != Intent.SnapshotHash)
				return Fail("architecture intent hash disagrees with its canonical snapshot", out Failure);
			if (snapshot.BuildKey != Intent.BuildKey || snapshot.PlanKey != Intent.PlanKey
				|| snapshot.BindingKey != Intent.BindingKey || snapshot.TierKey != Intent.TierKey
				|| snapshot.VariantKey != Intent.VariantKey || snapshot.PaletteKey != Intent.PaletteKey
				|| snapshot.LotType != Intent.LotType || snapshot.LotSize != Intent.LotSize
				|| snapshot.Facing != Intent.Facing)
				return Fail("architecture intent scalars disagree with the canonical snapshot", out Failure);
			if (!ValidRect(Intent.Rect))
				return Fail("architecture intent rectangle is malformed", out Failure);
			int worldWidth;
			int worldHeight;
			if (!KingdomArchitectureRules.TryWorldDimensions(snapshot.Width, snapshot.Height,
				snapshot.Facing, out worldWidth, out worldHeight)
				|| Intent.Rect.Width != worldWidth || Intent.Rect.Height != worldHeight)
				return Fail("architecture intent rectangle does not fit its canonical pose", out Failure);
			int mainX;
			int mainY;
			if (!KingdomArchitectureRules.TryToWorld(Intent.Rect.X1, Intent.Rect.Y1,
				snapshot.Width, snapshot.Height, snapshot.Facing, snapshot.MainX, snapshot.MainY,
				out mainX, out mainY)
				|| !Intent.Rect.Contains(mainX, mainY)
				|| mainX != Intent.MainWorldX || mainY != Intent.MainWorldY)
				return Fail("architecture intent world main cell disagrees with its snapshot and rect",
					out Failure);
			Snapshot = snapshot;
			return true;
		}

		// --- Exact canonical-to-world helpers ---------------------------------------------
	}
}
