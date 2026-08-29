using System;

namespace ThousandAndFirst
{
	public static partial class KingdomPurposePortfolioRules
	{
		private const string LandingAttemptTag = "purpose-landing-attempt";

		private const string LandingFaultTag = "purpose-landing-fault";

		/// <summary>The over-bound diagnostic sentinel. A figure outside the carriage bounds is
		/// forged or excess evidence, which is exactly the ambiguity that most needs a durable
		/// fault; it is recorded as over-bound rather than refusing the stamp.</summary>
		public const int OverBoundLandingFigure = MaxCarriedFood + 1;

		/// <summary>Folds any diagnostic figure into a recordable one.</summary>
		public static int LandingFaultFigure(int Value)
		{
			return Value < 0 || Value > MaxCarriedFood ? OverBoundLandingFigure : Value;
		}

		/// <summary>The durable fault one ambiguous aftermath leaves on the cargo. It is a separate
		/// witness from the attempt, and it must be, because the attempt witness can be honestly
		/// reconciled: a callback that throws after placing the exact unit leaves a ground the
		/// attempt reads as settled, and a refused quarantine would then let the next pass retire
		/// the attempt and carry on. The fault is written before any post-offer ambiguity is
		/// returned and binds the whole receipt with the step it was judging.</summary>
		public static bool TryLandingFault(string Receipt, int Expected, int Observed,
			out string Witness)
		{
			Witness = null;
			if (string.IsNullOrEmpty(Receipt) || Expected < 0
				|| Expected > OverBoundLandingFigure || Observed < 0
				|| Observed > OverBoundLandingFigure) return false;
			return (Witness = EncodeFields(new string[] { LandingFaultTag, Receipt, N(Expected),
				N(Observed) })) != null;
		}

		/// <summary>The composed durable verdict. A present fault outranks everything: it is
		/// unconditionally ambiguous, whatever the attempt witness or the ground now say, and no
		/// reading of either can retire it. Only where no fault stands does the attempt witness
		/// decide, and only then can a save cut after a clean settled callback recover.</summary>
		public static KingdomPurposeLandingAttemptState ClassifyLandingWitnesses(bool Faulted,
			bool Present, bool Ours, int Expected, int Observed, bool Exact)
		{
			return Faulted ? KingdomPurposeLandingAttemptState.Ambiguous
				: ClassifyLandingAttempt(Present, Ours, Expected, Observed, Exact);
		}

		/// <summary>The durable witness one offered serving leaves on the cargo before the engine is
		/// ever handed the object. It binds the whole canonical landing receipt to the one-step
		/// progress that offer promises, canonically encoded for the same reason the receipt is: a
		/// delimiter join would let one operation's witness be read as another's. Without a witness
		/// written first, a callback that obliterates the serving leaves nothing behind, and a retry
		/// after a refused quarantine would see a clean ground and land again.</summary>
		public static bool TryLandingAttempt(string Receipt, int Expected, out string Witness)
		{
			Witness = null;
			if (string.IsNullOrEmpty(Receipt) || Expected < 1 || Expected > MaxCarriedFood)
				return false;
			return (Witness = EncodeFields(
				new string[] { LandingAttemptTag, Receipt, N(Expected) })) != null;
		}

		/// <summary>Reads back a witness only when it is this operation's own, whole, and lawful.
		/// A torn or foreign string yields nothing, which is ambiguity rather than absence: the
		/// caller must never treat an unreadable witness as no offer having been made.</summary>
		public static bool TryReadLandingAttempt(string Witness, string Receipt, out int Expected)
		{
			Expected = 0;
			return !string.IsNullOrEmpty(Witness) && !string.IsNullOrEmpty(Receipt)
				&& TryDecodeFields(Witness, 3, out string[] fields)
				&& string.Equals(fields[0], LandingAttemptTag, StringComparison.Ordinal)
				&& string.Equals(fields[1], Receipt, StringComparison.Ordinal)
				&& Int(fields[2], out Expected) && Expected >= 1 && Expected <= MaxCarriedFood;
		}

		/// <summary>What an outstanding witness permits. Recovery is allowed for exactly one
		/// reading: this operation's own witness, against an exact partition, whose observed marked
		/// count is precisely the increment the offer promised. Every other reading &mdash; a
		/// serving obliterated, moved, nested, replaced or mutated, a callback that threw, a witness
		/// another operation wrote, a witness that will not parse &mdash; stays ambiguous, and the
		/// transaction reattempts its quarantine without offering a further serving, however many
		/// passes a refused publication costs.</summary>
		public static KingdomPurposeLandingAttemptState ClassifyLandingAttempt(bool Present,
			bool Ours, int Expected, int Observed, bool Exact)
		{
			if (!Present) return KingdomPurposeLandingAttemptState.Clear;
			if (!Ours || Expected < 1 || Observed < 0)
				return KingdomPurposeLandingAttemptState.Ambiguous;
			return Exact && Observed == Expected ? KingdomPurposeLandingAttemptState.Settled
				: KingdomPurposeLandingAttemptState.Ambiguous;
		}
	}
}
