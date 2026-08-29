using System;
using System.Collections.Generic;
using System.Text;
using XRL.World;

namespace ThousandAndFirst
{
	internal enum KingdomPurposeEffectRosterMode : byte
	{
		Exact = 0,
		DebitReserved = 1,
		ProductRelease = 2
	}

	internal sealed class KingdomPurposeEffectRosterRow
	{
		internal GameObject Item;
		internal string ObjectId;
		internal int Count;
		internal string Canonical;
	}

	internal sealed class KingdomPurposeEffectRosterSnapshot
	{
		internal string Digest;
		internal readonly List<KingdomPurposeEffectRosterRow> Rows =
			new List<KingdomPurposeEffectRosterRow>();
	}

	public static partial class KingdomPurpose
	{
		private static readonly string[] PurposeEffectRosterProperties = new string[]
		{
			CargoSchemaProperty, CargoKeyProperty, CargoManifestProperty,
			CargoConsignmentProperty, CargoOriginProperty, CargoDestinationProperty,
			PortfolioCargoSchemaProperty, PortfolioCargoReceiptProperty,
			PortfolioCargoKeyProperty, PortfolioCargoFoodProperty,
			PortfolioLandedFoodProperty, PortfolioLandedReceiptProperty,
			PortfolioLandedCountProperty, PortfolioLandedAttemptProperty,
			PortfolioLandedFaultProperty, PortfolioEffectAttemptProperty,
			PortfolioEffectReadyProperty,
			PortfolioEffectOfferProperty,
			PortfolioEffectCountProperty, PortfolioEffectFaultProperty,
			PortfolioEffectMarkProperty, PortfolioEffectIndexProperty,
			KingdomConstruction.InputMarkerProperty,
			KingdomOrdinaryFoodAuthority.ExpeditionReceiptProperty,
			KingdomOrdinaryFoodAuthority.DeliveryReceiptProperty,
			KingdomOrdinaryFoodAuthority.PorterReceiptProperty
		};

		private static bool TryCapturePurposeEffectRoster(
			KingdomPurposeEffectRuntimeContext Context, string OwnedObjectId,
			KingdomPurposeEffectRosterMode Mode, string Witness, string ProductReceipt,
			int Prefilter, out KingdomPurposeEffectRosterSnapshot Snapshot,
			out string Failure)
		{
			Snapshot = null;
			Failure = null;
			if (Context == null || !GameObject.Validate(Context.Store)
				|| Context.Store.Inventory == null || Context.Store.Inventory.Objects == null)
				return Fail("The bounded-effect store roster is unavailable.", out Failure);
			KingdomPurposeEffectRosterSnapshot result =
				new KingdomPurposeEffectRosterSnapshot();
			List<GameObject> held = new List<GameObject>(Context.Store.Inventory.Objects);
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < held.Count; i++)
			{
				GameObject item = held[i];
				if (!GameObject.Validate(item) || item.IsInvalid() || item.IsInGraveyard()
					|| item.Count < 1 || string.IsNullOrEmpty(item.IDIfAssigned)
					|| !ReferenceEquals(item.InInventory, Context.Store)
					|| item.CurrentCell != null
					|| !Context.Store.Inventory.InventoryContains(item)
					|| !ids.Add(item.IDIfAssigned))
					return Fail("The direct bounded-effect store roster lacks unique bidirectional identity.",
						out Failure);
				KingdomPurposeEffectRosterMode rowMode = item.IDIfAssigned == OwnedObjectId
					? Mode : KingdomPurposeEffectRosterMode.Exact;
				if (!TryPurposeEffectRosterRow(item, rowMode, Witness, ProductReceipt,
					Prefilter, item.Count, out string canonical, out Failure)) return false;
				result.Rows.Add(new KingdomPurposeEffectRosterRow
				{
					Item = item, ObjectId = item.IDIfAssigned, Count = item.Count,
					Canonical = canonical
				});
			}
			result.Rows.Sort((a, b) => string.CompareOrdinal(a.ObjectId, b.ObjectId));
			result.Digest = PurposeEffectRosterDigest(result.Rows);
			Snapshot = result;
			return KingdomPurposePortfolioRules.EffectRosterDigest(result.Digest)
				|| Fail("The direct bounded-effect roster digest is not canonical.", out Failure);
		}

		private static bool TryPurposeEffectExpectedDebitAfter(
			KingdomPurposeEffectRosterSnapshot Before, string ObjectId,
			out string Digest, out string Failure)
		{
			Digest = null;
			Failure = null;
			if (Before == null || string.IsNullOrEmpty(ObjectId)) return false;
			List<KingdomPurposeEffectRosterRow> after =
				new List<KingdomPurposeEffectRosterRow>();
			bool found = false;
			for (int i = 0; i < Before.Rows.Count; i++)
			{
				KingdomPurposeEffectRosterRow row = Before.Rows[i];
				if (row.ObjectId != ObjectId) { after.Add(row); continue; }
				if (found || row.Count < 1) return false;
				found = true;
				if (row.Count == 1) continue;
				if (!TryPurposeEffectRosterRow(row.Item,
					KingdomPurposeEffectRosterMode.Exact, null, null, 0, row.Count - 1,
					out string changed, out Failure)) return false;
				after.Add(new KingdomPurposeEffectRosterRow
				{
					Item = row.Item, ObjectId = row.ObjectId, Count = row.Count - 1,
					Canonical = changed
				});
			}
			if (!found) return Fail("The exact debit target is absent from its frozen roster.",
				out Failure);
			Digest = PurposeEffectRosterDigest(after);
			return true;
		}

		private static bool TryPurposeEffectExpectedProductAfter(
			KingdomPurposeEffectRosterSnapshot Before, GameObject Product,
			string ProductReceipt, int Prefilter, out string Digest, out string Failure)
		{
			Digest = null;
			Failure = null;
			if (Before == null || !GameObject.Validate(Product)
				|| string.IsNullOrEmpty(Product.IDIfAssigned)) return false;
			List<KingdomPurposeEffectRosterRow> after =
				new List<KingdomPurposeEffectRosterRow>(Before.Rows);
			for (int i = 0; i < after.Count; i++)
				if (after[i].ObjectId == Product.IDIfAssigned)
					return Fail("The new product identity already exists in the frozen roster.",
						out Failure);
			if (!TryPurposeEffectRosterRow(Product,
				KingdomPurposeEffectRosterMode.ProductRelease, null, ProductReceipt,
				Prefilter, 1, out string canonical, out Failure)) return false;
			after.Add(new KingdomPurposeEffectRosterRow
			{
				Item = Product, ObjectId = Product.IDIfAssigned, Count = 1,
				Canonical = canonical
			});
			after.Sort((a, b) => string.CompareOrdinal(a.ObjectId, b.ObjectId));
			Digest = PurposeEffectRosterDigest(after);
			return true;
		}

		private static bool TryPurposeEffectRosterRow(GameObject Item,
			KingdomPurposeEffectRosterMode Mode, string Witness, string ProductReceipt,
			int Prefilter, int Count, out string Canonical, out string Failure)
		{
			Canonical = null;
			Failure = null;
			if (!GameObject.Validate(Item) || Count < 1 || string.IsNullOrEmpty(Item.IDIfAssigned)
				|| string.IsNullOrEmpty(Item.Blueprint)) return false;
			if (Mode == KingdomPurposeEffectRosterMode.DebitReserved
				&& !ExactPurposeEffectDebitReservation(Item, Witness))
				return Fail("The exact debit reservation is absent or torn.", out Failure);
			if (Mode == KingdomPurposeEffectRosterMode.ProductRelease
				&& PurposeEffectProductReleaseStage(Item, ProductReceipt, Prefilter) < 0)
				return Fail("The exact product release checkpoint is torn.", out Failure);
			StringBuilder text = new StringBuilder(512);
			AppendRosterValue(text, Item.IDIfAssigned);
			AppendRosterValue(text, Item.Blueprint);
			AppendRosterValue(text, Count.ToString());
			AppendRosterValue(text, "owner=1;listed=1;cell=0");
			bool material = KingdomMaterials.TryMaterialOf(Item, out KingdomMaterial kind);
			AppendRosterValue(text, material ? ((int)kind).ToString() : "-");
			AppendRosterValue(text, KingdomOrdinaryFoodAuthority.IsEdible(Item) ? "1" : "0");
			AppendRosterValue(text, Item.HasPart("r_KingdomSeed") ? "1" : "0");
			AppendRosterValue(text, Item.Physics != null && Item.Physics.Takeable ? "1" : "0");
			AppendRosterValue(text, Item.HasPart("Stacker") ? "1" : "0");
			AppendRosterValue(text, Item.IsImportant() ? "1" : "0");
			AppendRosterValue(text, Item.Equipped == null ? "0" : "1");
			AppendRosterValue(text, Item.Inventory == null ? "-"
				: Item.Inventory.Objects == null ? "torn" : Item.Inventory.Objects.Count.ToString());
			for (int i = 0; i < PurposeEffectRosterProperties.Length; i++)
			{
				string field = PurposeEffectRosterProperties[i];
				if (Mode == KingdomPurposeEffectRosterMode.DebitReserved
					&& field == PortfolioEffectAttemptProperty)
					AppendRosterValue(text, "none");
				else if (Mode == KingdomPurposeEffectRosterMode.ProductRelease
					&& field == PortfolioEffectMarkProperty)
					AppendRosterValue(text, "s:" + ProductReceipt);
				else if (Mode == KingdomPurposeEffectRosterMode.ProductRelease
					&& field == PortfolioEffectIndexProperty)
					AppendRosterValue(text, "i:" + Prefilter);
				else AppendRosterValue(text, PurposeEffectPropertyToken(Item, field));
			}
			int neverStack = Mode == KingdomPurposeEffectRosterMode.ProductRelease
				? 1 : Item.GetIntProperty("NeverStack");
			AppendRosterValue(text, neverStack.ToString());
			AppendRosterValue(text, Item.GetIntProperty(
				Simulation.City.KingdomPorters.StockProperty).ToString());
			bool protectedCargo = Mode == KingdomPurposeEffectRosterMode.DebitReserved
				? false : Mode == KingdomPurposeEffectRosterMode.ProductRelease
					? true : HasProtectedCargoEvidence(Item);
			AppendRosterValue(text, protectedCargo ? "1" : "0");
			Canonical = text.ToString();
			return true;
		}

		private static string PurposeEffectPropertyToken(GameObject Item, string Field)
		{
			bool hasInt = Item.HasIntProperty(Field);
			bool hasString = Item.HasStringProperty(Field);
			if (hasInt && hasString) return "dual:" + Item.GetIntProperty(Field)
				+ ":" + Item.GetStringProperty(Field);
			if (hasInt) return "i:" + Item.GetIntProperty(Field);
			return hasString ? "s:" + Item.GetStringProperty(Field) : "none";
		}

		private static void AppendRosterValue(StringBuilder Text, string Value)
		{
			Value = Value ?? "";
			Text.Append(Value.Length).Append(':').Append(Value).Append(';');
		}

		private static string PurposeEffectRosterDigest(
			IList<KingdomPurposeEffectRosterRow> Rows)
		{
			StringBuilder text = new StringBuilder();
			for (int i = 0; Rows != null && i < Rows.Count; i++)
				AppendRosterValue(text, Rows[i].Canonical);
			return PurposeDigest("purpose-effect-roster-v1", text.ToString());
		}
	}
}
