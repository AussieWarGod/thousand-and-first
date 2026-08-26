using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{

	/// <summary>Value returned by every live founding operation.</summary>
	public struct KingdomFoundingResult
	{
		public KingdomFoundingOutcome Outcome;
		public KingdomFoundingWaterDisposition Water;
		public KingdomFoundingProjection Projection;
		public string Failure;

		public bool Committed => Outcome == KingdomFoundingOutcome.Committed;

		public bool ChargesEnergy => KingdomFoundingTransactionRules.ChargesEnergy(Outcome);

		public bool RequestsInventoryExit =>
			KingdomFoundingTransactionRules.RequestsInventoryExit(Outcome);

		public static KingdomFoundingResult From(KingdomFoundingOutcome Outcome,
			KingdomFoundingWaterDisposition Water, KingdomFoundingProjection Projection,
			string Failure = null)
		{
			return new KingdomFoundingResult
			{
				Outcome = Outcome,
				Water = Water,
				Projection = Projection,
				Failure = Failure ?? ""
			};
		}
	}
}
