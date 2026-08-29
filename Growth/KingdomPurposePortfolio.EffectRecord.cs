using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPurpose
	{
		internal const string PortfolioEffectAttemptProperty = "r_TAF_PurposeEffectAttempt";
		internal const string PortfolioEffectReadyProperty = "r_TAF_PurposeEffectReady";
		internal const string PortfolioEffectOfferProperty = "r_TAF_PurposeEffectOffer";
		internal const string PortfolioEffectCountProperty = "r_TAF_PurposeEffectCount";
		internal const string PortfolioEffectFaultProperty = "r_TAF_PurposeEffectFault";
		internal const string PortfolioEffectMarkProperty = "r_TAF_PurposeEffectMark";
		internal const string PortfolioEffectIndexProperty = "r_TAF_PurposeEffectIndex";

		private static bool TryPurposeEffectScope(KingdomPurposeOperationReceipt Operation,
			out string Receipt, out int Prefilter)
		{
			Receipt = null;
			Prefilter = 0;
			if (Operation == null || !KingdomPurposePortfolioRules.TryEffectReceipt(
				Operation.PairId, Operation.PairEpoch, Operation.OperationId,
				Operation.SourceKind, out Receipt)) return false;
			Prefilter = KingdomPurposePortfolioRules.EffectIndex(
				Simulation.City.KingdomCityRules.StableId(Receipt));
			return true;
		}

		private static bool TryReadPurposeEffectAttempt(GameObject Work, string Receipt,
			out KingdomPurposeEffectAttempt Attempt, out bool Present)
		{
			Attempt = null;
			Present = GameObject.Validate(Work)
				&& OwnedFieldPresent(Work, PortfolioEffectAttemptProperty);
			return !Present || OwnedStringField(Work, PortfolioEffectAttemptProperty)
				&& KingdomPurposePortfolioRules.TryReadEffectAttempt(
					Work.GetStringProperty(PortfolioEffectAttemptProperty), Receipt, out Attempt);
		}

		private static bool StampPurposeEffectAttempt(GameObject Owner, string Witness)
		{
			if (!GameObject.Validate(Owner) || string.IsNullOrEmpty(Witness)) return false;
			if (OwnedFieldPresent(Owner, PortfolioEffectAttemptProperty))
				return OwnedStringField(Owner, PortfolioEffectAttemptProperty)
					&& Owner.GetStringProperty(PortfolioEffectAttemptProperty) == Witness;
			Owner.SetStringProperty(PortfolioEffectAttemptProperty, Witness);
			return OwnedStringField(Owner, PortfolioEffectAttemptProperty)
				&& Owner.GetStringProperty(PortfolioEffectAttemptProperty) == Witness;
		}

		private static bool ClearPurposeEffectAttempt(GameObject Owner, string Witness)
		{
			if (!GameObject.Validate(Owner) || string.IsNullOrEmpty(Witness)) return false;
			if (!OwnedFieldPresent(Owner, PortfolioEffectAttemptProperty)) return true;
			if (!OwnedStringField(Owner, PortfolioEffectAttemptProperty)
				|| Owner.GetStringProperty(PortfolioEffectAttemptProperty) != Witness) return false;
			Owner.RemoveStringProperty(PortfolioEffectAttemptProperty);
			return !OwnedFieldPresent(Owner, PortfolioEffectAttemptProperty);
		}

		internal static bool ExactPurposeEffectDebitReservation(GameObject Item,
			string Witness)
		{
			return GameObject.Validate(Item) && !string.IsNullOrEmpty(Witness)
				&& OwnedStringField(Item, PortfolioEffectAttemptProperty)
				&& Item.GetStringProperty(PortfolioEffectAttemptProperty) == Witness
				&& !OwnedFieldPresent(Item, PortfolioEffectReadyProperty)
				&& !OwnedFieldPresent(Item, PortfolioEffectOfferProperty)
				&& !OwnedFieldPresent(Item, PortfolioEffectCountProperty)
				&& !OwnedFieldPresent(Item, PortfolioEffectFaultProperty)
				&& !OwnedFieldPresent(Item, PortfolioEffectMarkProperty)
				&& !OwnedFieldPresent(Item, PortfolioEffectIndexProperty);
		}

		private static bool StampPurposeEffectReady(GameObject Work, string Witness)
		{
			if (!GameObject.Validate(Work) || string.IsNullOrEmpty(Witness)) return false;
			if (OwnedFieldPresent(Work, PortfolioEffectReadyProperty))
				return OwnedStringField(Work, PortfolioEffectReadyProperty)
					&& Work.GetStringProperty(PortfolioEffectReadyProperty) == Witness;
			Work.SetStringProperty(PortfolioEffectReadyProperty, Witness);
			return OwnedStringField(Work, PortfolioEffectReadyProperty)
				&& Work.GetStringProperty(PortfolioEffectReadyProperty) == Witness;
		}

		private static bool ExactPurposeEffectReady(GameObject Work, string Witness)
		{
			return GameObject.Validate(Work) && !string.IsNullOrEmpty(Witness)
				&& OwnedStringField(Work, PortfolioEffectReadyProperty)
				&& Work.GetStringProperty(PortfolioEffectReadyProperty) == Witness;
		}

		private static bool ClearPurposeEffectReady(GameObject Work, string Witness)
		{
			if (!GameObject.Validate(Work) || string.IsNullOrEmpty(Witness)) return false;
			if (!OwnedFieldPresent(Work, PortfolioEffectReadyProperty)) return true;
			if (!ExactPurposeEffectReady(Work, Witness)) return false;
			Work.RemoveStringProperty(PortfolioEffectReadyProperty);
			return !OwnedFieldPresent(Work, PortfolioEffectReadyProperty);
		}

		private static bool StampPurposeEffectOffer(GameObject Work, string Witness)
		{
			if (!GameObject.Validate(Work) || string.IsNullOrEmpty(Witness)) return false;
			if (OwnedFieldPresent(Work, PortfolioEffectOfferProperty))
				return OwnedStringField(Work, PortfolioEffectOfferProperty)
					&& Work.GetStringProperty(PortfolioEffectOfferProperty) == Witness;
			Work.SetStringProperty(PortfolioEffectOfferProperty, Witness);
			return OwnedStringField(Work, PortfolioEffectOfferProperty)
				&& Work.GetStringProperty(PortfolioEffectOfferProperty) == Witness;
		}

		private static bool ExactPurposeEffectOffer(GameObject Work, string Witness)
		{
			return GameObject.Validate(Work) && !string.IsNullOrEmpty(Witness)
				&& OwnedStringField(Work, PortfolioEffectOfferProperty)
				&& Work.GetStringProperty(PortfolioEffectOfferProperty) == Witness;
		}

		private static bool ClearPurposeEffectOffer(GameObject Work, string Witness)
		{
			if (!GameObject.Validate(Work) || string.IsNullOrEmpty(Witness)) return false;
			if (!OwnedFieldPresent(Work, PortfolioEffectOfferProperty)) return true;
			if (!ExactPurposeEffectOffer(Work, Witness)) return false;
			Work.RemoveStringProperty(PortfolioEffectOfferProperty);
			return !OwnedFieldPresent(Work, PortfolioEffectOfferProperty);
		}

		private static bool PurposeEffectIsFaulted(GameObject Work)
		{
			return GameObject.Validate(Work)
				&& OwnedFieldPresent(Work, PortfolioEffectFaultProperty);
		}

		private static bool StampPurposeEffectFault(GameObject Work, string Receipt,
			int Step, string Observation)
		{
			if (!GameObject.Validate(Work)) return false;
			if (OwnedFieldPresent(Work, PortfolioEffectFaultProperty)) return true;
			if (!KingdomPurposePortfolioRules.TryEffectFault(Receipt, Step, Observation,
				out string witness)) return false;
			Work.SetStringProperty(PortfolioEffectFaultProperty, witness);
			return OwnedStringField(Work, PortfolioEffectFaultProperty)
				&& Work.GetStringProperty(PortfolioEffectFaultProperty) == witness;
		}

		private static bool TryReadPurposeEffectProducts(GameObject Work, string Receipt,
			out KingdomPurposeEffectProductRecord Record)
		{
			Record = new KingdomPurposeEffectProductRecord();
			if (!GameObject.Validate(Work)) return false;
			if (!OwnedFieldPresent(Work, PortfolioEffectCountProperty)) return true;
			return OwnedStringField(Work, PortfolioEffectCountProperty)
				&& KingdomPurposePortfolioRules.TryReadEffectProductRecord(
					Work.GetStringProperty(PortfolioEffectCountProperty), Receipt, out Record);
		}

		private static bool RecordPurposeEffectProducts(GameObject Work, string Receipt,
			KingdomPurposeEffectProductRecord Next)
		{
			if (!TryReadPurposeEffectProducts(Work, Receipt,
				out KingdomPurposeEffectProductRecord before)
				|| Next.Refined < before.Refined || Next.Seed < before.Seed
				|| Next.Staple < before.Staple
				|| !KingdomPurposePortfolioRules.TryEffectProductRecord(
					Receipt, Next, out string encoded)) return false;
			Work.SetStringProperty(PortfolioEffectCountProperty, encoded);
			return OwnedStringField(Work, PortfolioEffectCountProperty)
				&& Work.GetStringProperty(PortfolioEffectCountProperty) == encoded;
		}

		private static bool ClearPurposeEffectProducts(GameObject Work, string Receipt)
		{
			if (!TryReadPurposeEffectProducts(Work, Receipt, out _)) return false;
			if (OwnedFieldPresent(Work, PortfolioEffectCountProperty))
				Work.RemoveStringProperty(PortfolioEffectCountProperty);
			return !OwnedFieldPresent(Work, PortfolioEffectCountProperty);
		}

		private static bool WearsPurposeEffectMark(GameObject Item, string ProductReceipt,
			int Prefilter)
		{
			return GameObject.Validate(Item)
				&& KingdomPurposePortfolioRules.EffectMarkerIsOurs(ProductReceipt, Prefilter,
					OwnedIntField(Item, PortfolioEffectIndexProperty),
					Item.GetIntProperty(PortfolioEffectIndexProperty),
					OwnedStringField(Item, PortfolioEffectMarkProperty),
					Item.GetStringProperty(PortfolioEffectMarkProperty));
		}

		private static bool AnyPurposeEffectField(GameObject Item)
		{
			return OwnedFieldPresent(Item, PortfolioEffectAttemptProperty)
				|| OwnedFieldPresent(Item, PortfolioEffectReadyProperty)
				|| OwnedFieldPresent(Item, PortfolioEffectOfferProperty)
				|| OwnedFieldPresent(Item, PortfolioEffectCountProperty)
				|| OwnedFieldPresent(Item, PortfolioEffectFaultProperty)
				|| OwnedFieldPresent(Item, PortfolioEffectMarkProperty)
				|| OwnedFieldPresent(Item, PortfolioEffectIndexProperty);
		}
	}
}
