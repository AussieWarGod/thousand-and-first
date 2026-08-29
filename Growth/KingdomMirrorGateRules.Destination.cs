using System;

namespace ThousandAndFirst
{
	internal static partial class KingdomMirrorGateRules
	{
		/// <summary>Register-order spokes which presently answer one hub.</summary>
		internal static int[] HubSpokeIndices(KingdomGateRow[] Rows, string HubKey)
		{
			int hub = IndexOfKey(Rows, HubKey);
			if (Rows == null || !Storable(HubKey) || hub < 0)
				return new int[0];
			int count = 0;
			for (int i = 0; i < Rows.Length; i++)
				if (i != hub && !string.Equals(Rows[i].City, Rows[hub].City,
						StringComparison.OrdinalIgnoreCase)
					&& string.Equals(Rows[i].Partner, HubKey, StringComparison.Ordinal)) count++;
			int[] answer = new int[count];
			int at = 0;
			for (int i = 0; i < Rows.Length; i++)
				if (i != hub && !string.Equals(Rows[i].City, Rows[hub].City,
						StringComparison.OrdinalIgnoreCase)
					&& string.Equals(Rows[i].Partner, HubKey, StringComparison.Ordinal))
					answer[at++] = i;
			return answer;
		}

		/// <summary>
		/// Changes only the hub's outgoing destination. Every spoke continues to answer the hub;
		/// no row, city, remote zone, or second pairing authority is touched.
		/// </summary>
		internal static KingdomGateVerdict TrySelectHubDestination(KingdomGateRow[] Rows,
			string HubKey, string DestinationKey, out KingdomGateRow[] Next,
			out string PreviousDestination)
		{
			Next = Rows ?? new KingdomGateRow[0];
			PreviousDestination = "";
			int hub = IndexOfKey(Next, HubKey);
			int destination = IndexOfKey(Next, DestinationKey);
			if (hub < 0 || destination < 0) return KingdomGateVerdict.RefusedUnkeyed;
			if (hub == destination) return KingdomGateVerdict.RefusedNamed;
			if (string.Equals(Next[hub].City, Next[destination].City,
				StringComparison.OrdinalIgnoreCase)) return KingdomGateVerdict.RefusedCityKeyed;
			if (!string.Equals(Next[destination].Partner, HubKey, StringComparison.Ordinal))
				return KingdomGateVerdict.RefusedUnkeyed;
			PreviousDestination = Next[hub].Partner;
			if (string.Equals(PreviousDestination, DestinationKey, StringComparison.Ordinal))
				return KingdomGateVerdict.Joined;
			KingdomGateRow[] built = new KingdomGateRow[Next.Length];
			Array.Copy(Next, built, Next.Length);
			built[hub] = built[hub].WithPartner(DestinationKey);
			Next = built;
			return KingdomGateVerdict.Joined;
		}

		internal static string DestinationPrompt(string HubCity, string FromCity,
			string ToCity)
		{
			return "Re-key the capital arch at " + Named(HubCity) + " from "
				+ Named(FromCity) + " to " + Named(ToCity) + "?\n\nEvery keyed spoke still "
				+ "answers the capital. Only the capital arch's outward crossing changes; no "
				+ "arch moves, no distant city is loaded, and nothing is spent.";
		}

		internal static string DestinationChangedLine(string HubCity, string ToCity)
		{
			return "The capital arch at " + Named(HubCity) + " now answers "
				+ Named(ToCity) + ". Every other keyed arch still answers the capital.";
		}
	}
}
