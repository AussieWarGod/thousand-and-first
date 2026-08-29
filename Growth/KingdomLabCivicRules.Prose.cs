namespace ThousandAndFirst
{
	internal static partial class KingdomLabCivicRules
	{
		internal static string CauseLine(KingdomLabCivicReceipt R)
		{
			if (R == null) return "The hall cannot prove what was asked here.";
			if (R.Kind == KingdomLabCivicKind.RefusalDeparture)
				return R.SubjectName + " refuses " + R.RefusedTag + ", which this "
					+ "laboratory reaches at their exact roof.";
			return R.SubjectName + " keeps " + R.SubjectCreed + " while the city keeps "
				+ R.CityCreed + ", and names a price from their lodged taste for "
				+ R.TasteTag + ".";
		}

		internal static string RequestLine(KingdomLabCivicReceipt R)
		{
			if (R == null) return "No exact request is recorded.";
			if (R.Request == KingdomLabCivicRequest.ShrineUnconsecrated)
				return "Leave " + R.TargetName
					+ " unconsecrated while this cause stands.";
			if (R.Request == KingdomLabCivicRequest.NeighbourRehoused)
				return "Rehouse " + R.TargetName + " from " + R.SourceHomeName
					+ " to the already-proved " + R.TargetHomeName + ".";
			return "Rehouse " + R.SubjectName + " away from the reached "
				+ R.RefusedTag + " work before their roof window ends.";
		}

		internal static string StatusLine(KingdomLabCivicReceipt R)
		{
			if (R == null || R.Kind == KingdomLabCivicKind.None) return "";
			if (R.Phase == KingdomLabCivicPhase.Quarantined)
				return "{{r|civic receipt quarantined: " + R.Fault + "}}";
			if (R.Phase == KingdomLabCivicPhase.Closed)
				return "{{K|civic receipt closed: " + R.Closure + "}}";
			if (R.Phase == KingdomLabCivicPhase.Active)
				return "{{W|active civic request: " + RequestLine(R) + "}}";
			return "{{W|unanswered civic request: " + RequestLine(R) + "}}";
		}

		internal static string ClosureLine(KingdomLabCivicReceipt R)
		{
			if (R == null) return "A laboratory cause closed without evidence.";
			switch (R.Closure)
			{
			case KingdomLabCivicClosure.Refused:
				return R.SubjectName + " was refused at the laboratory; nothing was promised.";
			case KingdomLabCivicClosure.Rehoused:
				return (R.Kind == KingdomLabCivicKind.SavantPrice ? R.TargetName : R.SubjectName)
					+ " was rehoused, closing the laboratory cause.";
			case KingdomLabCivicClosure.Departed:
				return R.SubjectName + " left through the city's warned roof-brink path.";
			case KingdomLabCivicClosure.OwnerGone:
				return "The exact laboratory owner was removed; its nonvaluable request ended.";
			default:
				return "The exact laboratory cause no longer stands; its request ended.";
			}
		}
	}
}
