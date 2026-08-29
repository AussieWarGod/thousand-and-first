using System;

namespace ThousandAndFirst
{
	/// <summary>Typed, zero-economy D12 offer and durable-receipt law.</summary>
	public static partial class KingdomVocationServiceRules
	{
		public const int MaxRowsPerSeries = 16;
		public const int MaxRows = 48; // 16 receipts for each of at most three settlements.
		internal const int PriorMaxRows = 16;
		public const int MaxIdBytes = 128;
		public const int MaxTextBytes = 2300;
		public const int MaxSourceTextBytes = 160;
		public const int MaxResultTextBytes = 1400;
		public const int MaxOfferTextBytes = 3200;

		internal static bool TryBuildAvailableOffer(KingdomVocationServiceSource source,
			out KingdomVocationServiceOffer offer, out string failure)
		{
			offer = null; failure = null;
			if (!ValidSource(source))
				return Fail("vocation service source is invalid", out failure);
			offer = new KingdomVocationServiceOffer(
				KingdomVocationServiceOfferState.Available, source.SettlementId,
				source.Vocation, source.Kind, source.Authority, OfferVerb(source.Kind),
				AuthorityText(source.Authority), source.ReceiptId, source.Description,
				source.ResultText,
				"C18 civic-practice vocation-service history",
				"once per exact source receipt; 16 for this city and vocation; 48 realm-wide",
				"one durable information/title receipt; no item, yield, power, or passive effect",
				OfferReport(source.Kind, source.Description), null, null);
			return TryValidateOffer(offer, out failure);
		}

		internal static bool TryBuildHoldingReport(string settlementId,
			out KingdomVocationServiceOffer offer, out string failure)
		{
			offer = null; failure = null;
			if (!Id(settlementId)) return Fail("holding city identity is invalid", out failure);
			offer = new KingdomVocationServiceOffer(
				KingdomVocationServiceOfferState.Neutral, settlementId, "holding",
				KingdomVocationServiceKind.None, KingdomVocationServiceAuthority.None,
				null, AuthorityText(KingdomVocationServiceAuthority.None), null, null, null,
				"none", "none", "no operation opens",
				"This holding promises no vocation service. Its ground remains available to the realm.",
				null, null);
			return TryValidateOffer(offer, out failure);
		}

		internal static bool TryBuildUnavailable(string settlementId, string vocation,
			string cause, string remedy, out KingdomVocationServiceOffer offer,
			out string failure)
		{
			offer = null; failure = null;
			KingdomVocationServiceKind kind = KindFor(vocation);
			KingdomVocationServiceAuthority authority = AuthorityFor(kind);
			if (!Id(settlementId) || kind == KingdomVocationServiceKind.None ||
				!OfferText(cause) || !OfferText(remedy))
				return Fail("unavailable vocation service evidence is invalid", out failure);
			offer = new KingdomVocationServiceOffer(
				KingdomVocationServiceOfferState.Unavailable, settlementId, vocation,
				kind, authority, null, AuthorityText(authority), null, null, null,
				"none", "unavailable", "no operation opened",
				"The vocation service is unavailable.", cause, remedy);
			return TryValidateOffer(offer, out failure);
		}

		public static bool TryValidate(KingdomVocationServiceBook book, out string failure)
		{
			failure = null;
			if (book == null || book.Revision < 0L || book.Rows == null ||
				book.Rows.Count > MaxRows)
				return Fail("vocation service book is invalid", out failure);
			string prior = null;
			for (int i = 0; i < book.Rows.Count; i++)
			{
				KingdomVocationServiceReceipt row = book.Rows[i];
				if (row == null || row.Version != KingdomVocationServiceReceipt.CurrentVersion ||
					!ValidCurrentRequest(row.Request) || row.ServiceId != ServiceId(row.Request) ||
					prior != null && string.CompareOrdinal(prior, row.ServiceId) >= 0 ||
					row.Verb != OfferVerb(row.Request.Kind) ||
					row.OutputText != ReceiptOutput(row.Request) || !Text(row.OutputText) ||
					row.OutputUnits != 0 ||
					row.CompletedTick < row.Request.RequestedTick)
					return Fail("vocation service row is invalid", out failure);
				prior = row.ServiceId;
				for (int j = 0; j < i; j++)
				{
					KingdomVocationServiceReceipt other = book.Rows[j];
					if (SameSource(other.Request, row.Request))
						return Fail("vocation source receipt was recorded more than once", out failure);
					if (SameCadence(other.Request, row.Request))
						return Fail("vocation cadence ordinal was recorded more than once", out failure);
				}
				int earlier = 0;
				for (int j = 0; j < book.Rows.Count; j++)
					if (SameSeries(book.Rows[j].Request, row.Request) &&
						book.Rows[j].Request.CadenceOrdinal < row.Request.CadenceOrdinal) earlier++;
				if (earlier != row.Request.CadenceOrdinal)
					return Fail("vocation cadence contains a gap", out failure);
			}
			return true;
		}

		internal static bool TryValidateOffer(KingdomVocationServiceOffer offer,
			out string failure)
		{
			failure = null;
			if (offer == null || !Id(offer.SettlementId) || offer.InputUnits != 0 ||
				offer.OutputUnits != 0 || offer.MutatesSource || !OfferText(offer.Report) ||
				!OfferText(offer.SourceAuthority) || !OfferText(offer.Sink) ||
				!OfferText(offer.Cadence) || !OfferText(offer.Closure))
				return Fail("vocation service offer is invalid", out failure);
			switch (offer.State)
			{
			case KingdomVocationServiceOfferState.Neutral:
				return offer.Vocation == "holding" && offer.Kind == KingdomVocationServiceKind.None &&
					offer.Authority == KingdomVocationServiceAuthority.None &&
					offer.SourceAuthority == AuthorityText(offer.Authority) && offer.Verb == null &&
					offer.SourceReceiptId == null && offer.SourceDescription == null &&
					offer.ResultText == null &&
					offer.UnavailableCause == null && offer.Remedy == null ||
					Fail("holding report is invalid", out failure);
			case KingdomVocationServiceOfferState.Available:
				return CurrentKindAndAuthority(offer) && Id(offer.SourceReceiptId) &&
					SourceText(offer.SourceDescription) && ResultText(offer.ResultText) &&
					offer.Verb == OfferVerb(offer.Kind) &&
					offer.UnavailableCause == null && offer.Remedy == null ||
					Fail("available vocation service offer is invalid", out failure);
			case KingdomVocationServiceOfferState.Unavailable:
				return CurrentKindAndAuthority(offer) && offer.Verb == null &&
					offer.SourceReceiptId == null && offer.SourceDescription == null &&
					offer.ResultText == null &&
					OfferText(offer.UnavailableCause) && OfferText(offer.Remedy) ||
					Fail("unavailable vocation service offer is invalid", out failure);
			default: return Fail("vocation service offer state is unknown", out failure);
			}
		}

		private static bool CurrentKindAndAuthority(KingdomVocationServiceOffer offer) =>
			offer.Kind == KindFor(offer.Vocation) && offer.Kind != KingdomVocationServiceKind.None &&
			offer.Authority == AuthorityFor(offer.Kind) &&
			offer.SourceAuthority == AuthorityText(offer.Authority);

		private static bool ValidSource(KingdomVocationServiceSource source) =>
			source != null && Id(source.SettlementId) && source.Kind == KindFor(source.Vocation) &&
			source.Kind != KingdomVocationServiceKind.None &&
			source.Authority == AuthorityFor(source.Kind) && Id(source.ReceiptId) &&
			SourceText(source.Description) && ResultText(source.ResultText);

		private static bool ValidCurrentRequest(KingdomVocationServiceRequest request) =>
			request != null && Id(request.SettlementId) && request.Kind == KindFor(request.Vocation) &&
			request.Kind != KingdomVocationServiceKind.None && Id(request.SourceReceiptId) &&
			SourceText(request.SourceDescription) && ResultText(request.ResultText) &&
			Id(request.SinkReceiptId) &&
			request.SinkReceiptId == SinkId(request) && request.InputUnits == 0 &&
			request.CadenceOrdinal >= 0L && request.CadenceOrdinal < MaxRowsPerSeries &&
			request.RequestedTick >= 0L && Digest(request.Digest) &&
			request.Digest == RequestDigest(request);

		internal static KingdomVocationServiceKind KindFor(string vocation) =>
			vocation == "waystation" ? KingdomVocationServiceKind.RouteBrief :
			vocation == "refuge" ? KingdomVocationServiceKind.SanctuaryTitle :
			vocation == "reliquary" ? KingdomVocationServiceKind.ProvenanceReading :
			KingdomVocationServiceKind.None;

		private static KingdomVocationServiceAuthority AuthorityFor(KingdomVocationServiceKind kind) =>
			kind == KingdomVocationServiceKind.RouteBrief ? KingdomVocationServiceAuthority.PolityRoute :
			kind == KingdomVocationServiceKind.SanctuaryTitle ? KingdomVocationServiceAuthority.BuiltShelter :
			kind == KingdomVocationServiceKind.ProvenanceReading ?
				KingdomVocationServiceAuthority.ArtifactRecognition : KingdomVocationServiceAuthority.None;

		private static string AuthorityText(KingdomVocationServiceAuthority authority) =>
			authority == KingdomVocationServiceAuthority.PolityRoute ? "polity route authority" :
			authority == KingdomVocationServiceAuthority.BuiltShelter ? "built shelter authority" :
			authority == KingdomVocationServiceAuthority.ArtifactRecognition ?
				"artifact recognition authority" : "none declared";

		internal static string OfferVerb(KingdomVocationServiceKind kind) =>
			kind == KingdomVocationServiceKind.RouteBrief ? "Ask for a route brief" :
			kind == KingdomVocationServiceKind.SanctuaryTitle ? "Read a shelter title" :
			kind == KingdomVocationServiceKind.ProvenanceReading ?
				"Request a provenance reading" : null;

		private static string OfferReport(KingdomVocationServiceKind kind, string source) =>
			kind == KingdomVocationServiceKind.RouteBrief ? "Route authority: " + source :
			kind == KingdomVocationServiceKind.SanctuaryTitle ? "Shelter authority: " + source :
			"Recognition authority: " + source;
	}
}
