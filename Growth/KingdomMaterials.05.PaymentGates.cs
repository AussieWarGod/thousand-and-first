using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomMaterials
	{
		/// <summary>Reserves a composite claim against exact physical stockpile contents.</summary>
		public static KingdomMaterialDebit ReserveComposite(Zone Z, KingdomMaterialDebitCost Cost)
		{
			return KingdomMaterialDebit.Reserve(Stock(Z), Cost);
		}

		/// <summary>Reserves an outstanding claim while requiring the exact receipted object.</summary>
		public static KingdomMaterialDebit ReserveCompositeWithRequiredItem(Zone Z,
			KingdomMaterialDebitCost Cost, GameObject RequiredItem)
		{
			return KingdomMaterialDebit.Reserve(Stock(Z), Cost, RequiredItem);
		}

		/// <summary>Read-only exact reservation of a catalogue design's composite price.</summary>
		public static KingdomMaterialDebit ReservePayment(Zone Z, string Key)
		{
			return ReserveComposite(Z, new KingdomMaterialDebitCost(
				CostFor(Key), BitCostFor(Key), ExoticCostFor(Key)));
		}

		/// <summary>Reserves a design price and requires its exact delivered consignment.</summary>
		public static KingdomMaterialDebit ReservePaymentWithRequiredItem(Zone Z, string Key,
			GameObject RequiredItem)
		{
			return KingdomMaterialDebit.Reserve(Stock(Z), new KingdomMaterialDebitCost(
				CostFor(Key), BitCostFor(Key), ExoticCostFor(Key)), RequiredItem);
		}

		/// <summary>Read-only exact reservation of a registered improvement price.</summary>
		public static KingdomMaterialDebit ReserveUpgradePayment(Zone Z, string PredecessorKey)
		{
			return ReserveComposite(Z, new KingdomMaterialDebitCost(
				UpgradeCostFor(PredecessorKey), null, null));
		}

		/// <summary>Read-only exact reservation of one authored same-set transition.</summary>
		public static KingdomMaterialDebit ReserveTransitionPayment(Zone Z,
			KingdomMaterialTally Materials)
		{
			return ReserveComposite(Z, new KingdomMaterialDebitCost(Materials, null, null));
		}

		/// <summary>Whether dedicated stockpiles cover one authored same-set transition.</summary>
		public static bool CanPayTransition(Zone Z, KingdomMaterialTally Cost, out string Failure)
		{
			Failure = null;
			KingdomMaterialTally cost = Cost ?? new KingdomMaterialTally();
			if (cost.IsEmpty()) return true;
			MaterialStock stock = Stock(Z);
			if (!stock.InputLeaseAuthorityExact)
			{
				Failure = stock.InputLeaseFailure
					?? "The durable routed-input leases cannot be read.";
				return false;
			}
			if (KingdomMaterialRules.Covers(stock.Tally, cost)) return true;
			string missing = KingdomMaterialRules.Missing(stock.Tally, cost).Describe();
			Failure = "The change wants {{C|" + cost.Describe()
				+ "}}, and the stockpiles are short "
				+ (missing == null ? "of it" : "{{C|" + missing + "}}") + ".";
			return false;
		}

		/// <summary>Read-only exact reservation of an arbitrary bit price.</summary>
		public static KingdomMaterialDebit ReserveBits(Zone Z, KingdomBitTally Bits)
		{
			return ReserveComposite(Z, new KingdomMaterialDebitCost(null, Bits, null));
		}

		/// <summary>Whether this ground can pay the exact registered composite design price.</summary>
		public static bool CanPay(Zone Z, string Key, out string Failure)
		{
			Failure = null;
			if (!AllowsInfrastructure(Z, Key, out Failure)) return false;
			KingdomMaterialTally cost = CostFor(Key);
			MaterialStock stock = null;
			if (!cost.IsEmpty())
			{
				stock = Stock(Z);
				if (!HasExactLeaseAuthority(stock, out Failure)) return false;
				if (!KingdomMaterialRules.Covers(stock.Tally, cost))
				{
					string missing = KingdomMaterialRules.Missing(stock.Tally, cost).Describe();
					Failure = "The work wants {{C|" + cost.Describe()
						+ "}}, and the stockpiles are short "
						+ (missing == null ? "of it" : "{{C|" + missing + "}}")
						+ ". Clear ground for it, trade for it, or strike something that was built of it."
						+ (stock.None ? " Nothing here is dedicated as a stockpile yet." : "");
					return false;
				}
			}
			KingdomBitTally bits = BitCostFor(Key);
			KingdomExoticTally exotics = ExoticCostFor(Key);
			if (bits.IsEmpty() && exotics.IsEmpty()) return true;
			if (stock == null)
			{
				stock = Stock(Z);
				if (!HasExactLeaseAuthority(stock, out Failure)) return false;
			}
			if (!KingdomMaterialRules.CoversBits(stock.Bits, bits))
			{
				string missing = KingdomMaterialRules.MissingBits(stock.Bits, bits).Describe();
				Failure = "This is high-craft work. It wants {{C|" + bits.Describe()
					+ "}} out of the stockpiles, and the keepers are short "
					+ (missing == null ? "of it" : "{{C|" + missing + "}}")
					+ ". Bring scrap home and put it in a stockpile; whatever comes apart into the right stock will do.";
				return false;
			}
			if (!KingdomMaterialRules.CoversExotics(stock.Exotics, exotics))
			{
				string missing = KingdomMaterialRules.MissingExotics(stock.Exotics, exotics).Describe();
				Failure = "A work like this is finished in something rarer than stone. It wants {{C|"
					+ exotics.Describe() + "}}, and the stockpiles hold no "
					+ (missing == null ? "such thing" : "{{C|" + missing + "}}")
					+ ". Nobody here can make one. Somebody has to find one and carry it home.";
				return false;
			}
			return true;
		}

		private static bool HasExactLeaseAuthority(MaterialStock Stock, out string Failure)
		{
			Failure = null;
			if (Stock.InputLeaseAuthorityExact) return true;
			Failure = Stock.InputLeaseFailure
				?? "The durable routed-input leases cannot be read.";
			return false;
		}
	}
}
