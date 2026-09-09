using System;
using System.Collections.Generic;
using System.Reflection;
using Qud.API;
using XRL;
using XRL.Language;
using XRL.Rules;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>
	/// Live, same-basin founding transaction. The receipt is serialized on the basin part; engine
	/// projections are idempotent and verified before success is returned. Before an irreversible
	/// publication, failure restores the exact liquid snapshot. Afterwards it retains one paid
	/// receipt and a named recovery point instead of lying that an engine publication was undone.
	/// </summary>
	public static partial class KingdomFoundingTransaction
	{
		private const string PendingFactionProperty = "TAFFoundingPending";
		internal const string PendingFactionTransactionProperty = "TAFFoundingTransaction";
		internal const string PendingFactionAuthorityProperty = "TAFFoundingAuthority";
		internal const string RealmReservationProperty = "TAFFoundingRealmReservation";
		internal const string VillageReservationProperty = "TAFFoundingVillageReservation";
		internal const string GlobalReservationState = "r_TAF_FoundingGlobalReservation_v1";
		internal const string SiteReservationProperty = "r_TAF_FoundingSiteAuthority_v1";
		private const string SiteReservationNameProperty = "r_TAF_FoundingSiteName_v1";
		private const string SiteReservationVocationProperty = "r_TAF_FoundingSiteVocation_v1";
		private const string SiteReservationVillageProperty = "r_TAF_FoundingSiteVillage_v1";
		private const string SiteReservationDisplayProperty = "r_TAF_FoundingSiteDisplay_v1";
		private const string SiteReservationTickProperty = "r_TAF_FoundingSiteTick_v1";
		private const string SecondChronicleProperty = "r_TAF_SecondFoundingChronicle";
		private const string SecondChronicleStageProperty = "r_TAF_SecondFoundingChronicleStage";
		private const string SecondChronicleDispositionProperty =
			"r_TAF_SecondFoundingChronicleDisposition_v1";
		private const string SecondRestoredProperty = "r_TAF_SecondFoundingRestored_v1";
		private const string SecondPublicationAuthorityProperty =
			"r_TAF_SecondFoundingPublicationAuthority_v1";
		private const string SecondIdentityTransactionProperty =
			"r_TAF_SecondFoundingIdentityTransaction_v1";
		private const string SecondIdentityRealmProperty =
			"r_TAF_SecondFoundingIdentityRealm_v1";
		private const string SecondIdentitySettlementProperty =
			"r_TAF_SecondFoundingIdentitySettlement_v1";
		private const string SecondIdentityVersionProperty =
			"r_TAF_SecondFoundingIdentityVersion_v1";
		private const string SecondIdentityOriginProperty =
			"r_TAF_SecondFoundingIdentityOrigin_v1";
		internal const string ClaimChronicleEventProperty = "r_TAF_ClaimChronicleEvent_v1";
		internal const string ClaimChronicleStageProperty = "r_TAF_ClaimChronicleStage_v1";
		internal const string ClaimChronicleDispositionProperty =
			"r_TAF_ClaimChronicleDisposition_v1";
		internal const string ClaimFoundingProperty = "r_TAF_ClaimWasFounding_v1";
		private const string DirectRecoveryNameProperty = "r_TAF_SecondFoundingRecoveryName";
		private const string DirectRecoveryVocationProperty = "r_TAF_SecondFoundingRecoveryVocation";
		private const string DirectRecoveryRiteXProperty = "r_TAF_SecondFoundingRecoveryRiteX";
		private const string DirectRecoveryRiteYProperty = "r_TAF_SecondFoundingRecoveryRiteY";
		private const string DirectRecoveryTickProperty = "r_TAF_SecondFoundingRecoveryTick";
		private const string DirectRecoveryRealmProperty = "r_TAF_SecondFoundingRecoveryRealm";
		private const string DirectRecoveryTransactionProperty = "r_TAF_SecondFoundingRecoveryTransaction";
		private static readonly FieldInfo FactionListField = typeof(Factions).GetField(
			"FactionList", BindingFlags.Static | BindingFlags.NonPublic);
		private static readonly object InFlightSync = new object();
		private static FoundingLease InFlight;

		/// <summary>Process-local guard for synchronous engine callbacks. Founding is single-threaded,
		/// but JournalAPI invokes listeners before it returns. A nested route must see the guard before
		/// RequireSystem, reservation, receipt, liquid, faction, or journal mutation.</summary>
		private sealed class FoundingLease : IDisposable
		{
			internal string Authority;
			internal r_FounderBasin Basin;

			internal bool Bind(string Value, r_FounderBasin Receipt)
			{
				if (string.IsNullOrEmpty(Value) ||
					!KingdomFoundingTransactionRules.TryParseAuthority(Value, out var parsed))
				{
					return false;
				}
				lock (InFlightSync)
				{
					if (!ReferenceEquals(InFlight, this) ||
						(!string.IsNullOrEmpty(Authority) && Authority != Value))
					{
						return false;
					}
					Authority = Value;
					Basin = Receipt;
					return true;
				}
			}

			public void Dispose()
			{
				lock (InFlightSync)
				{
					if (ReferenceEquals(InFlight, this))
					{
						InFlight = null;
					}
				}
			}
		}

		private static bool TryEnterFounding(string AuthorityHint, r_FounderBasin Basin,
			out FoundingLease Lease)
		{
			lock (InFlightSync)
			{
				if (InFlight != null)
				{
					Lease = null;
					return false;
				}
				Lease = new FoundingLease { Authority = AuthorityHint, Basin = Basin };
				InFlight = Lease;
				return true;
			}
		}

		private static KingdomFoundingResult ReentryRefusal()
		{
			return Result(KingdomFoundingOutcome.Refused,
				KingdomFoundingWaterDisposition.Untouched,
				KingdomFoundingProjection.None,
				"Another founding callback is already in flight; this nested attempt changed nothing.");
		}

		internal static bool AuthorityIsSynchronouslyInFlight(string Authority,
			r_FounderBasin Basin = null)
		{
			lock (InFlightSync)
			{
				return InFlight != null && !string.IsNullOrEmpty(Authority) &&
					InFlight.Authority == Authority &&
					(Basin == null || ReferenceEquals(InFlight.Basin, Basin));
			}
		}
	}
}
