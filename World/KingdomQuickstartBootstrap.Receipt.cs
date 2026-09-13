using System;
using XRL;

namespace ThousandAndFirst
{
	/// <summary>
	/// The three ways a quickstart receipt may change, and the exact-readback proof each of them
	/// owes. Nothing else in the bootstrap writes the receipt state.
	/// </summary>
	public static partial class KingdomQuickstartBootstrap
	{
		private static bool Publish(XRLGame Game, KingdomQuickstartReceipt Receipt,
			out string Failure)
		{
			Failure = "";
			string encoded = KingdomQuickstartRules.Encode(Receipt);
			if (Game == null || encoded == null)
			{
				Failure = "The quickstart receipt could not be encoded.";
				return false;
			}
			Game.SetStringGameState(KingdomQuickstartRules.ReceiptState, encoded);
			string observed = Game.GetStringGameState(KingdomQuickstartRules.ReceiptState,
				null);
			KingdomQuickstartReceipt read;
			if (!string.Equals(observed, encoded, StringComparison.Ordinal)
				|| !KingdomQuickstartRules.TryDecode(observed, out read)
				|| !string.Equals(KingdomQuickstartRules.Encode(read), encoded,
					StringComparison.Ordinal))
			{
				Failure = "The quickstart receipt did not publish exactly.";
				return false;
			}
			return true;
		}

		private static bool Advance(XRLGame Game, ref KingdomQuickstartReceipt Receipt,
			KingdomQuickstartPhase Next, string Value,
			KingdomQuickstartAdvisorDisposition Advisor, out string Failure)
		{
			Failure = "";
			KingdomQuickstartReceipt advanced;
			if (!KingdomQuickstartRules.TryAdvance(Receipt, Next, Value, Advisor,
				out advanced) || !Publish(Game, advanced, out Failure))
			{
				if (string.IsNullOrEmpty(Failure))
					Failure = "The quickstart receipt refused a non-monotone phase.";
				return false;
			}
			Receipt = advanced;
			return true;
		}

		/// <summary>
		/// Moves the founding cohort between its states WITHIN the Complete phase, where a phase
		/// advance cannot reach. Publishing is the same exact-readback publish every phase advance
		/// uses, so a wake that makes progress always changes the wire and therefore always
		/// authorizes the next attempt.
		/// </summary>
		private static bool Restate(XRLGame Game, ref KingdomQuickstartReceipt Receipt,
			KingdomQuickstartFoundersDisposition Disposition, string[] Ids, out string Failure)
		{
			Failure = "";
			KingdomQuickstartReceipt restated;
			if (!KingdomQuickstartRules.TryRestateFounders(Receipt, Disposition, Ids,
				out restated) || !Publish(Game, restated, out Failure))
			{
				if (string.IsNullOrEmpty(Failure))
					Failure = "The quickstart receipt refused an unlawful founders restatement.";
				return false;
			}
			Receipt = restated;
			return true;
		}
	}
}
