using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	internal sealed class KingdomPurposeEffectRuntimeContext
	{
		internal Zone Zone;
		internal GameObject Work;
		internal GameObject Store;
		internal KingdomPurposeKind Kind;
		internal KingdomMaterial RawMaterial;
		internal KingdomMaterial ProductMaterial;
		internal string CropBlueprint;
		internal string SeedBlueprint;
		internal string StapleBlueprint;
	}

	public static partial class KingdomPurpose
	{
		private static bool TryPurposeEffectContext(KingdomSystem System,
			KingdomPurposeOperationReceipt Operation,
			out KingdomPurposeEffectRuntimeContext Context, out string Failure)
		{
			Context = null;
			Failure = null;
			if (System == null || Operation == null
				|| !KingdomPurposePortfolioRules.EffectIsOwed(Operation.SourceKind)
				|| !TryOperationGround(Operation, out Zone zone, out GameObject work,
					out GameObject input, out _, out _, out _, out Failure)) return false;
			GameObject store = input;
			KingdomPurposeEffectRuntimeContext context = new KingdomPurposeEffectRuntimeContext
			{
				Zone = zone, Work = work, Store = store, Kind = Operation.SourceKind
			};
			if (KingdomPurposePortfolioRules.TryEffectRefine(Operation.SourceKind,
				out context.RawMaterial, out context.ProductMaterial))
			{
				if (!ExactPurposeEffectStore(zone, input, Operation.SourceInputStoreId,
					false, out Failure)) return false;
			}
			else
			{
				context.Store = work;
				context.CropBlueprint = KingdomData.CropForStyle(System.Style);
				context.SeedBlueprint = KingdomData.SeedForStyle(System.Style);
				context.StapleBlueprint = KingdomRules.PreservedStapleFor(
					context.CropBlueprint);
				if (!KingdomPurposePortfolioRules.TryEffectHarvest(context.CropBlueprint,
					context.SeedBlueprint, context.StapleBlueprint, out _, out _, out _)
					|| !ExactPurposeEffectStore(zone, work, Operation.SourceWorkId,
						true, out Failure)) return false;
			}
			Context = context;
			return true;
		}

		private static bool ExactPurposeEffectStore(Zone Zone, GameObject Store,
			string StoreId, bool Larder, out string Failure)
		{
			Failure = null;
			if (Zone == null || !GameObject.Validate(Store) || Store.IDIfAssigned != StoreId
				|| FindExactKnown(Zone, StoreId, out GameObject exact)
					!= KingdomPhysicalLookupState.Exact
				|| !ReferenceEquals(exact, Store) || Store.CurrentZone != Zone
				|| Store.InInventory != null || Store.CurrentCell == null
				|| !ReferenceEquals(Store.CurrentCell.ParentZone, Zone)
				|| Store.CurrentCell.Objects == null || !Store.CurrentCell.Objects.Contains(Store)
				|| Store.Inventory == null)
				return Fail("The bounded effect's exact store lost bidirectional ground custody.",
					out Failure);
			if (Larder)
				return Store.GetIntProperty("KingdomLarder") == 1
					&& KingdomSurvey.HeldIn(Store) <= KingdomSurvey.CapacityOf(Store)
					|| Fail("The Granary-Colossus lost its exact dry-store dedication or capacity.",
						out Failure);
			return KingdomMaterials.IsStockpile(Store)
				|| Fail("The bounded effect's source lost its exact stockpile dedication.",
					out Failure);
		}

		private static bool TryPurposeEffectCustody(
			KingdomPurposeEffectRuntimeContext Context, out IList<GameObject> Loaded,
			out string Failure)
		{
			Loaded = null;
			Failure = null;
			if (Context == null || !ExactPurposeEffectStore(Context.Zone, Context.Store,
				Context.Kind == KingdomPurposeKind.Harvest
					? Context.Work.IDIfAssigned : Context.Store.IDIfAssigned,
				Context.Kind == KingdomPurposeKind.Harvest, out Failure)) return false;
			if (!TryLoadedLandingCustody(Context.Zone, out Loaded))
				return Fail("The bounded effect's loaded custody index is incomplete.", out Failure);
			return true;
		}

		private static bool PurposeEffectGroundStartsClean(
			KingdomPurposeEffectRuntimeContext Context, out string Failure)
		{
			Failure = null;
			if (!TryPurposeEffectCustody(Context, out IList<GameObject> loaded, out Failure))
				return false;
			for (int i = 0; i < loaded.Count; i++)
				if (AnyPurposeEffectField(loaded[i]))
					return Fail("Earlier bounded-effect evidence still stands on this loaded ground.",
						out Failure);
			return true;
		}

		private static bool PurposeEffectEvidenceOnlyOnWorkOrProducts(
			KingdomPurposeEffectRuntimeContext Context, out IList<GameObject> Loaded,
			out string Failure)
		{
			if (!TryPurposeEffectCustody(Context, out Loaded, out Failure)) return false;
			bool workAttemptPresent = OwnedFieldPresent(Context.Work,
				PortfolioEffectAttemptProperty);
			string workAttempt = null;
			if (workAttemptPresent)
			{
				if (!OwnedStringField(Context.Work, PortfolioEffectAttemptProperty))
					return Fail("The bounded-effect work attempt is dual-typed or torn.",
						out Failure);
				workAttempt = Context.Work.GetStringProperty(PortfolioEffectAttemptProperty);
			}
			if (OwnedFieldPresent(Context.Work, PortfolioEffectReadyProperty)
				&& (!workAttemptPresent
					|| !OwnedStringField(Context.Work, PortfolioEffectReadyProperty)
					|| Context.Work.GetStringProperty(PortfolioEffectReadyProperty) != workAttempt))
				return Fail("The bounded-effect ready checkpoint is torn or detached.",
					out Failure);
			if (OwnedFieldPresent(Context.Work, PortfolioEffectOfferProperty)
				&& (!workAttemptPresent
					|| !ExactPurposeEffectReady(Context.Work, workAttempt)
					|| !OwnedStringField(Context.Work, PortfolioEffectOfferProperty)
					|| Context.Work.GetStringProperty(PortfolioEffectOfferProperty) != workAttempt))
				return Fail("The bounded-effect callback offer is torn or detached.",
					out Failure);
			int carriers = 0;
			for (int i = 0; i < Loaded.Count; i++)
			{
				GameObject item = Loaded[i];
				if (!AnyPurposeEffectField(item)) continue;
				if (ReferenceEquals(item, Context.Work))
				{
					if (OwnedFieldPresent(item, PortfolioEffectMarkProperty)
						|| OwnedFieldPresent(item, PortfolioEffectIndexProperty))
						return Fail("The bounded-effect work carries an impossible product mark.",
							out Failure);
					continue;
				}
				bool itemAttempt = OwnedFieldPresent(item, PortfolioEffectAttemptProperty);
				bool itemMark = OwnedFieldPresent(item, PortfolioEffectMarkProperty)
					|| OwnedFieldPresent(item, PortfolioEffectIndexProperty);
				if (OwnedFieldPresent(item, PortfolioEffectReadyProperty)
					|| OwnedFieldPresent(item, PortfolioEffectOfferProperty)
					|| OwnedFieldPresent(item, PortfolioEffectCountProperty)
					|| OwnedFieldPresent(item, PortfolioEffectFaultProperty))
					return Fail("Bounded-effect work evidence escaped onto another object.",
						out Failure);
				if (itemAttempt && (!workAttemptPresent || itemMark
					|| !OwnedStringField(item, PortfolioEffectAttemptProperty)
					|| item.GetStringProperty(PortfolioEffectAttemptProperty) != workAttempt))
					return Fail("A debit reservation is foreign, torn, or detached from its work.",
						out Failure);
				if ((itemAttempt || itemMark) && ++carriers > 1)
					return Fail("More than one object carries one bounded-effect attempt.",
						out Failure);
			}
			return true;
		}
	}
}
