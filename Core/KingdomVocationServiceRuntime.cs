using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Exact loaded D12 source adapters. No source, stock, or value is changed.</summary>
	public static partial class KingdomVocationServiceRuntime
	{
		/// <summary>
		/// Opens one current-loaded D12 report. Reliquary evidence is read from C18 section 1;
		/// a refused or newer authority becomes an explicit Unavailable offer, never empty truth.
		/// </summary>
		public static bool TryOpenCurrent(KingdomSystem system, Zone zone,
			out KingdomVocationServiceOffer offer, out string failure)
		{
			offer = null;
			failure = null;
			if (!KingdomCurrentCityEvidenceRuntime.TryContext(system, zone, null, false,
				out KingdomCurrentCityEvidenceRuntime.Context _,
				out failure)) return false;
			KingdomCivicArtifactsEnvelope artifacts = null;
			if (system != null && system.Vocation == "reliquary")
			{
				if (!system.TryGetCurrentIdentity(out string exactRealmId,
					out string _))
				{
					failure = "The current realm identity cannot be proved for artifact authority.";
					return false;
				}
				KingdomCivicMemorySystem memory =
					The.Game?.GetSystem<KingdomCivicMemorySystem>();
				KingdomCivicMemorySectionLease lease = null;
				string sectionFailure = null;
				if (memory == null || !memory.TryReadSection(
					KingdomCivicMemoryLimits.SectionCivicArtifacts,
					out lease, out sectionFailure))
				{
					artifacts = RefusedArtifacts(sectionFailure ??
						"Civic artifact memory is unavailable in this save.");
				}
				else
				{
					artifacts = KingdomCivicArtifactsStore.ReadForRealm(lease.Payload(),
						exactRealmId, out string artifactFailure);
					if (artifacts == null)
						artifacts = RefusedArtifacts(artifactFailure ??
							"Civic artifact memory returned no authority.");
				}
			}
			return TryDescribeCurrent(system, zone, artifacts,
				out offer, out failure);
		}

		internal static bool TryDescribeCurrent(KingdomSystem system, Zone zone,
			KingdomCivicArtifactsEnvelope artifacts,
			out KingdomVocationServiceOffer offer, out string failure)
		{
			offer = null;
			failure = null;
			if (!KingdomCurrentCityEvidenceRuntime.TryContext(system, zone, null, false,
				out KingdomCurrentCityEvidenceRuntime.Context context,
				out failure)) return false;
			if (!system.TryGetCurrentIdentity(out string exactRealmId,
				out string exactSettlementId) || !string.Equals(exactSettlementId,
				context.SettlementId, StringComparison.Ordinal))
			{
				failure = "The current realm and loaded-city identity changed while opening the view.";
				return false;
			}
			switch (context.Vocation)
			{
			case "waystation":
				return TryWaystation(context, exactRealmId, out offer, out failure);
			case "refuge":
				return TryRefuge(context, out offer, out failure);
			case "reliquary":
				return TryReliquary(context, exactRealmId, artifacts, out offer, out failure);
			case "holding":
				return KingdomVocationServiceRules.TryBuildHoldingReport(
					context.SettlementId, out offer, out failure);
			default:
				failure = "The current city has no known explicit vocation.";
				return false;
			}
		}

		private static bool TryWaystation(KingdomCurrentCityEvidenceRuntime.Context context,
			string exactRealmId, out KingdomVocationServiceOffer offer, out string failure)
		{
			offer = null;
			failure = null;
			KingdomPolityLedger ledger = context.System.PolityLedger;
			if (!KingdomPolityRules.TryValidate(ledger, out string sourceFailure) ||
				!ledger.IdentityBound || !string.Equals(ledger.RealmId, exactRealmId,
					StringComparison.Ordinal))
			{
				return Missing(context, sourceFailure ??
					"The polity route authority is unbound or belongs to another realm.",
					"Restore the exact current-realm route authority, then ask again.",
					out offer, out failure);
			}
			KingdomPolityRouteRecord selected = null;
			string selectedReceipt = null;
			for (int i = 0; i < ledger.Routes.Count; i++)
			{
				KingdomPolityRouteRecord route = ledger.Routes[i];
				if (!Touches(route, context.SettlementId) ||
					!TryRouteReceipt(route, out string receipt)) continue;
				if (selected == null || route.DepartureOrdinal > selected.DepartureOrdinal ||
					route.DepartureOrdinal == selected.DepartureOrdinal &&
					string.CompareOrdinal(route.RouteId, selected.RouteId) > 0)
				{
					selected = route;
					selectedReceipt = receipt;
				}
			}
			if (selected == null)
			{
				return Missing(context, "No exact polity route names this city.",
					"Establish or receive a semantic route that names this city.",
					out offer, out failure);
			}
			if (!TryRouteResult(selected, out string result))
			{
				return Missing(context, "The exact route is too large for one bounded service receipt.",
					"Establish a route whose full endpoint path fits the bounded route brief.",
					out offer, out failure);
			}
			KingdomVocationServiceSource source = new KingdomVocationServiceSource(
				context.SettlementId, context.Vocation,
				KingdomVocationServiceKind.RouteBrief,
				KingdomVocationServiceAuthority.PolityRoute, selectedReceipt,
				"exact current-realm route authority naming this city", result);
			return KingdomVocationServiceRules.TryBuildAvailableOffer(source,
				out offer, out failure);
		}

		private static bool TryRefuge(KingdomCurrentCityEvidenceRuntime.Context context,
			out KingdomVocationServiceOffer offer, out string failure)
		{
			offer = null;
			failure = null;
			KingdomSurvey survey = KingdomSurvey.ActiveFor(context.Zone)
				?? KingdomSurvey.Take(context.Zone, context.System);
			KingdomBenefitIndex benefits = null;
			string benefitFailure = null;
			if (survey == null || !survey.TryBenefits(out benefits, out benefitFailure))
				return Missing(context, "Physical shelter evidence is unavailable: "
					+ (benefitFailure ?? "no exact current-ground reading"),
					"Restore the exact room designation and its physical bed providers.",
					out offer, out failure);
			if (!KingdomCurrentCityEvidenceRuntime.TryBuiltWorksReadOnly(context,
				out List<KingdomCurrentCityEvidenceRuntime.BuiltWorkSnapshot> works,
				out string sourceFailure))
			{
				return Missing(context, sourceFailure,
					"Complete a receipted shelter here and let the city book observe it.",
					out offer, out failure);
			}
			KingdomCurrentCityEvidenceRuntime.BuiltWorkSnapshot shelter = null;
			for (int i = 0; i < works.Count; i++)
			{
				KingdomCurrentCityEvidenceRuntime.BuiltWorkSnapshot candidate = works[i];
				if (!IsShelter(context, candidate, benefits)) continue;
				if (shelter == null || candidate.CompletedTick > shelter.CompletedTick ||
					candidate.CompletedTick == shelter.CompletedTick && string.CompareOrdinal(
						candidate.WorkReceiptId, shelter.WorkReceiptId) > 0) shelter = candidate;
			}
			if (shelter == null)
			{
				return Missing(context, "No exact completed built work carries roof capacity.",
					"Complete a receipted housing design in this loaded city.",
					out offer, out failure);
			}
			string result = SanctuaryResult(context, shelter);
			if (!KingdomVocationServiceRules.ResultText(result))
			{
				return Missing(context, "The exact shelter title exceeds one bounded service receipt.",
					"Complete a shelter with a bounded authored design name, then ask again.",
					out offer, out failure);
			}
			KingdomVocationServiceSource source = new KingdomVocationServiceSource(
				context.SettlementId, context.Vocation,
				KingdomVocationServiceKind.SanctuaryTitle,
				KingdomVocationServiceAuthority.BuiltShelter, shelter.WorkReceiptId,
				"exact completed shelter authority on this city's loaded ground", result);
			return KingdomVocationServiceRules.TryBuildAvailableOffer(source,
				out offer, out failure);
		}

		private static bool TryReliquary(KingdomCurrentCityEvidenceRuntime.Context context,
			string exactRealmId, KingdomCivicArtifactsEnvelope artifacts,
			out KingdomVocationServiceOffer offer, out string failure)
		{
			offer = null;
			failure = null;
			if (artifacts == null || artifacts.Quarantined || artifacts.IsOpaqueFuture)
			{
				string cause = artifacts == null ? "Artifact recognition authority is absent." :
					artifacts.IsOpaqueFuture ? "Artifact recognition authority belongs to a newer build." :
					string.IsNullOrEmpty(artifacts.Fault)
						? "Artifact recognition authority is quarantined." : artifacts.Fault;
				return Missing(context, cause,
					"Restore a readable artifact-recognition authority, then ask again.",
					out offer, out failure);
			}
			if (!KingdomCivicArtifactsStore.TryValidateIdentity(artifacts,
				out string sourceFailure) || !artifacts.IdentityBound ||
				!string.Equals(artifacts.RealmId, exactRealmId, StringComparison.Ordinal))
			{
				return Missing(context, sourceFailure ??
					"Artifact recognition authority is unbound or belongs to another realm.",
					"Restore the exact current-realm recognition authority, then ask again.",
					out offer, out failure);
			}
			KingdomArtifactRecognitionReceipt selected = null;
			for (int i = 0; i < artifacts.Recognitions.Rows.Count; i++)
			{
				KingdomArtifactRecognitionReceipt row = artifacts.Recognitions.Rows[i];
				if (!TryLocationZone(row.Source.LocationId, out string zoneId) ||
					context.System.SettlementIdForOwnedZone(zoneId) != context.SettlementId) continue;
				if (selected == null || row.RecognizedTick > selected.RecognizedTick ||
					row.RecognizedTick == selected.RecognizedTick && string.CompareOrdinal(
						row.RecognitionId, selected.RecognitionId) > 0) selected = row;
			}
			if (selected == null)
			{
				return Missing(context, "No exact recognition receipt was observed on this city's ground.",
					"Recognize one explicit object while standing on ground owned by this city.",
					out offer, out failure);
			}
			string result = ProvenanceResult(selected);
			if (!KingdomVocationServiceRules.ResultText(result))
			{
				return Missing(context, "The exact recognition is too large for one bounded reading.",
					"Record a bounded recognition whose full deed and attribution remain readable.",
					out offer, out failure);
			}
			KingdomVocationServiceSource source = new KingdomVocationServiceSource(
				context.SettlementId, context.Vocation,
				KingdomVocationServiceKind.ProvenanceReading,
				KingdomVocationServiceAuthority.ArtifactRecognition, selected.RecognitionId,
				"exact artifact-recognition authority on this city's loaded ground", result);
			return KingdomVocationServiceRules.TryBuildAvailableOffer(source,
				out offer, out failure);
		}

		private static bool TryLocationZone(string locationId, out string zoneId)
		{
			zoneId = null;
			const string prefix = "taf:zone:";
			if (string.IsNullOrEmpty(locationId) || !locationId.StartsWith(prefix,
				StringComparison.Ordinal)) return false;
			int last = locationId.LastIndexOf(':');
			int prior = last <= prefix.Length ? -1 : locationId.LastIndexOf(':', last - 1);
			if (prior <= prefix.Length || !int.TryParse(locationId.Substring(last + 1),
				NumberStyles.None, CultureInfo.InvariantCulture, out int y) ||
				!int.TryParse(locationId.Substring(prior + 1, last - prior - 1),
					NumberStyles.None, CultureInfo.InvariantCulture, out int x) ||
				x < 0 || x > 1023 || y < 0 || y > 1023) return false;
			zoneId = locationId.Substring(prefix.Length, prior - prefix.Length);
			return zoneId.Length > 0;
		}

		private static bool Missing(KingdomCurrentCityEvidenceRuntime.Context context,
			string cause, string remedy, out KingdomVocationServiceOffer offer,
			out string failure)
		{
			return KingdomVocationServiceRules.TryBuildUnavailable(context.SettlementId,
				context.Vocation, cause, remedy, out offer, out failure);
		}

		private static KingdomCivicArtifactsEnvelope RefusedArtifacts(string failure)
		{
			string cause = !string.IsNullOrWhiteSpace(failure) && failure.Length <= 300 &&
				failure.IndexOf('\0') < 0 ? failure : "Civic artifact memory was refused.";
			return new KingdomCivicArtifactsEnvelope
			{
				Quarantined = true,
				Fault = cause
			};
		}
	}
}
