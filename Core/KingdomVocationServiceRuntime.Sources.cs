using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomVocationServiceRuntime
	{
		private static bool Touches(KingdomPolityRouteRecord route, string settlementId)
		{
			if (route == null || route.OrderedPath == null) return false;
			for (int i = 0; i < route.OrderedPath.Count; i++)
				if (route.OrderedPath[i] == settlementId) return true;
			return false;
		}

		private static bool TryRouteReceipt(KingdomPolityRouteRecord route,
			out string receipt)
		{
			receipt = null;
			if (route == null) return false;
			if (route.Phase == KingdomPolityRoutePhase.Returned)
				receipt = route.ReturnReceiptId;
			else if (route.Phase == KingdomPolityRoutePhase.Arrived)
				receipt = route.DeliveryReceiptId;
			else if (route.Phase == KingdomPolityRoutePhase.Traveling ||
				route.Phase == KingdomPolityRoutePhase.AvailableToWitness ||
				route.Phase == KingdomPolityRoutePhase.Blocked ||
				route.Phase == KingdomPolityRoutePhase.ConfrontationAvailable)
				receipt = route.DepartureReceiptId;
			return !string.IsNullOrEmpty(receipt);
		}

		private static bool TryRouteResult(KingdomPolityRouteRecord route, out string result)
		{
			result = null;
			if (route?.OrderedPath == null || route.OrderedPath.Count < 2) return false;
			StringBuilder path = new StringBuilder();
			for (int i = 0; i < route.OrderedPath.Count; i++)
			{
				if (i > 0) path.Append(" > ");
				path.Append(route.OrderedPath[i]);
			}
			string candidate = "Waystation route brief: origin " + route.OriginId +
				"; destination " + route.DestinationId + "; exact path " + path +
				"; stage " + route.Phase.ToString().ToLowerInvariant() + " at segment " +
				route.SegmentIndex.ToString(CultureInfo.InvariantCulture) + "/" +
				(route.OrderedPath.Count - 1).ToString(CultureInfo.InvariantCulture) +
				"; mode " + route.Mode.ToString().ToLowerInvariant() + "; purpose " +
				route.Purpose.ToString().ToLowerInvariant() + ".";
			if (!KingdomVocationServiceRules.ResultText(candidate)) return false;
			result = candidate; return true;
		}

		private static string SanctuaryResult(KingdomCurrentCityEvidenceRuntime.Context context,
			KingdomCurrentCityEvidenceRuntime.BuiltWorkSnapshot shelter) => "Sanctuary title: " +
			shelter.DisplayName + " [" + shelter.DesignKey + "], shelter receipt " +
			shelter.WorkReceiptId + ", held by " + context.SettlementId + ".";

		private static string ProvenanceResult(KingdomArtifactRecognitionReceipt recognition) =>
			"Provenance reading; kind " + recognition.Kind.ToString().ToLowerInvariant() +
			"; attribution " + (string.IsNullOrEmpty(recognition.AttributionName)
				? "the city" : recognition.AttributionName) + "; artifact " +
			recognition.Source.DisplayName + "; deed " + (recognition.Source.DeedId ?? "none") +
			(recognition.Source.DeedText == null ? "." : ": " + recognition.Source.DeedText + ".");
	}
}
