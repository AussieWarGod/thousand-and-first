using System;
using XRL.World;

namespace ThousandAndFirst
{
		internal sealed class KingdomPurposeEffectProductCensus
	{
		internal string Receipt;
		internal int Prefilter;
		internal KingdomPurposeEffectProductRecord Recorded;
		internal KingdomPurposeEffectAttempt Attempt;
		internal bool AttemptPresent;
		internal GameObject EvidenceCarrier;
		internal int Refined;
		internal int Seed;
		internal int Staple;
	}

	public static partial class KingdomPurpose
	{
		private static bool PurposeEffectProductIsMakeable(
			KingdomPurposeEffectRuntimeContext Context,
			KingdomPurposeEffectProductRole Role)
		{
			if (!KingdomPurposePortfolioRules.TryEffectProductReceipt(
				"purpose-effect-sample", Role, out string receipt)) return false;
			GameObject sample = ExactPurposeEffectProduct(Context, Role, receipt, 1);
			if (sample == null) return false;
			try { sample.Obliterate(); }
			catch { return false; }
			return !GameObject.Validate(sample);
		}

		private static GameObject ExactPurposeEffectProduct(
			KingdomPurposeEffectRuntimeContext Context,
			KingdomPurposeEffectProductRole Role, string ProductReceipt, int Prefilter)
		{
			string blueprint = PurposeEffectProductBlueprint(Context, Role);
			if (string.IsNullOrEmpty(blueprint) || string.IsNullOrEmpty(ProductReceipt)
				|| Prefilter == 0) return null;
			GameObject product;
			try { product = GameObject.Create(blueprint); }
			catch { return null; }
			if (!GameObject.Validate(product)) return null;
			try { product.RemovePart("Stacker"); }
			catch
			{
				try { product.Obliterate(); } catch { }
				return null;
			}
			if (!ExactPurposeEffectProductShape(Context, product, Role, false))
			{
				try { product.Obliterate(); } catch { }
				return null;
			}
			product.SetIntProperty("NeverStack", 1);
			if (Role == KingdomPurposeEffectProductRole.Staple)
				product.SetIntProperty(Simulation.City.KingdomPorters.StockProperty, 1);
			product.SetStringProperty(PortfolioEffectMarkProperty, ProductReceipt);
			product.SetIntProperty(PortfolioEffectIndexProperty, Prefilter);
			return WearsPurposeEffectMark(product, ProductReceipt, Prefilter)
				&& ExactPurposeEffectProductShape(Context, product, Role, true)
				? product : AbandonPurposeEffectSample(product);
		}

		private static GameObject AbandonPurposeEffectSample(GameObject Product)
		{
			try { Product?.Obliterate(); } catch { }
			return null;
		}

		private static bool ExactPurposeEffectProductShape(
			KingdomPurposeEffectRuntimeContext Context, GameObject Product,
			KingdomPurposeEffectProductRole Role, bool Marked)
		{
			if (Context == null || !GameObject.Validate(Product) || Product.IsInvalid()
				|| Product.IsInGraveyard() || Product.Count != 1 || Product.Physics == null
				|| !Product.Physics.Takeable || Product.HasPart("Stacker")
				|| Product.Blueprint != PurposeEffectProductBlueprint(Context, Role)
				|| Product.GetIntProperty("NeverStack") != (Marked ? 1 : 0)) return false;
			if (Role == KingdomPurposeEffectProductRole.Refined)
				return KingdomMaterials.TryOrdinaryMaterialOf(Product,
					out KingdomMaterial material) && material == Context.ProductMaterial;
			if (Role == KingdomPurposeEffectProductRole.Seed)
				return Product.HasPart("r_KingdomSeed")
					&& Product.GetIntProperty(Simulation.City.KingdomPorters.StockProperty) == 0;
			return Role == KingdomPurposeEffectProductRole.Staple
				&& KingdomOrdinaryFoodAuthority.IsEdible(Product)
				&& (!Marked || Product.GetIntProperty(
					Simulation.City.KingdomPorters.StockProperty) == 1);
		}

		private static string PurposeEffectProductBlueprint(
			KingdomPurposeEffectRuntimeContext Context,
			KingdomPurposeEffectProductRole Role)
		{
			if (Context == null) return null;
			if (Role == KingdomPurposeEffectProductRole.Refined)
				return KingdomMaterials.BlueprintFor(Context.ProductMaterial);
			if (Role == KingdomPurposeEffectProductRole.Seed) return Context.SeedBlueprint;
			return Role == KingdomPurposeEffectProductRole.Staple
				? Context.StapleBlueprint : null;
		}

		private static int PurposeEffectProductReleaseStage(GameObject Product,
			string ProductReceipt, int Prefilter)
		{
			if (!GameObject.Validate(Product) || string.IsNullOrEmpty(ProductReceipt)
				|| Prefilter == 0 || OwnedFieldPresent(Product, PortfolioEffectAttemptProperty)
				|| OwnedFieldPresent(Product, PortfolioEffectReadyProperty)
				|| OwnedFieldPresent(Product, PortfolioEffectOfferProperty)
				|| OwnedFieldPresent(Product, PortfolioEffectCountProperty)
				|| OwnedFieldPresent(Product, PortfolioEffectFaultProperty)
				|| Product.HasStringProperty("NeverStack")) return -1;
			bool markPresent = OwnedFieldPresent(Product, PortfolioEffectMarkProperty);
			bool indexPresent = OwnedFieldPresent(Product, PortfolioEffectIndexProperty);
			bool markExact = OwnedStringField(Product, PortfolioEffectMarkProperty)
				&& Product.GetStringProperty(PortfolioEffectMarkProperty) == ProductReceipt;
			bool indexExact = OwnedIntField(Product, PortfolioEffectIndexProperty)
				&& Product.GetIntProperty(PortfolioEffectIndexProperty) == Prefilter;
			int neverStack = Product.GetIntProperty("NeverStack");
			if (markPresent && !markExact || indexPresent && !indexExact) return -1;
			if (markExact && indexExact && neverStack == 1) return 0;
			if (!markPresent && indexExact && neverStack == 1) return 1;
			if (!markPresent && !indexPresent && neverStack == 1) return 2;
			return !markPresent && !indexPresent && neverStack == 0 ? 3 : -1;
		}

		private static KingdomPurposeEffectCallbackKind ProductCallback(
			KingdomPurposeEffectProductRole Role)
		{
			return Role == KingdomPurposeEffectProductRole.Refined
				? KingdomPurposeEffectCallbackKind.RefinedProduct
				: Role == KingdomPurposeEffectProductRole.Seed
					? KingdomPurposeEffectCallbackKind.HarvestSeed
					: Role == KingdomPurposeEffectProductRole.Staple
						? KingdomPurposeEffectCallbackKind.HarvestStaple
						: KingdomPurposeEffectCallbackKind.Invalid;
		}
	}
}
