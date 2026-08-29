using System;

namespace ThousandAndFirst
{
	public static partial class KingdomPurposePortfolioRules
	{
		/// <summary>Servings one operation may land. Both carried rows in the frozen catalogue sit
		/// at exactly this figure; a larger row is a table edit this recovery was never reconciled
		/// against, and is refused rather than trusted.</summary>
		public const int MaxCarriedFood = 6;

		private const string LandingReceiptTag = "purpose-landing";

		private const string CargoRootTag = "purpose-cargo-root";

		/// <summary>The exact identity of one operation's landing. Canonical and injective: the
		/// fields are length-prefixed through the codec's own <see cref="EncodeFields"/>, because
		/// <see cref="Id"/> admits <c>':'</c> and a plain delimiter join is therefore ambiguous
		/// &mdash; <c>("a", 1, "2:b")</c> and <c>("a:1", 2, "b")</c> would produce one string and
		/// let one operation read another's provision as its own. Any integer carried beside this is
		/// a cheap index and never a proof: a hash collides, and a hash can be zero.</summary>
		public static bool TryLandingReceipt(string PairId, long PairEpoch, string OperationId,
			out string Receipt)
		{
			return TryCanonicalKey(LandingReceiptTag, PairId, PairEpoch, OperationId, true,
				out Receipt);
		}

		/// <summary>The rooted-cargo game-state key body, canonicalised the same way and for the
		/// same reason: two distinct pair/epoch/operation tuples must never name one root.</summary>
		public static bool TryCargoRootBody(string PairId, long PairEpoch, string OperationId,
			out string Body)
		{
			return TryCanonicalKey(CargoRootTag, PairId, PairEpoch, OperationId, true, out Body);
		}

		/// <summary>Whether the operation field is present is a caller's declaration, never an
		/// inference from a null argument: a missing operation id must refuse a receipt outright
		/// rather than quietly yield a scope, which every operation in the pair would then share.
		/// </summary>
		private static bool TryCanonicalKey(string Tag, string PairId, long PairEpoch,
			string OperationId, bool WithOperation, out string Key)
		{
			Key = null;
			if (!Id(PairId) || PairEpoch < 1L) return false;
			if (!WithOperation)
				return (Key = EncodeFields(new string[] { Tag, PairId, N(PairEpoch) })) != null;
			return Id(OperationId) && (Key = EncodeFields(
				new string[] { Tag, PairId, N(PairEpoch), OperationId })) != null;
		}

		/// <summary>Pure recovery verdict for the destination provision landing. Amount equality
		/// alone is never authority: the receiving larders must still carry this operation's exact
		/// marker, and the marked count proves how many exact servings the callback already landed.
		/// Any unrelated change to those larders cuts the transaction instead of being mistaken for
		/// this operation's receipt. Mirrors <c>KingdomScalarReceiptRules.TryRecover</c> branch for
		/// branch; its caller deliberately diverges on severity, cutting where the exemplar
		/// retries, because a purpose hop has no second sweep to be reconciled by.</summary>
		public static bool TryRecoverCarriedFood(int Carried, int Before, int Observed,
			bool MarkerMatches, int Marked, out KingdomPurposeFoodLandingAction Action)
		{
			Action = KingdomPurposeFoodLandingAction.Refuse;
			if (Carried <= 0 || Carried > MaxCarriedFood || Before < 0 || Observed < 0
				|| Marked < 0 || Marked > Carried) return false;
			if (!MarkerMatches)
			{
				Action = KingdomPurposeFoodLandingAction.Interference;
				return true;
			}
			if (Observed != Before + Marked)
			{
				Action = KingdomPurposeFoodLandingAction.Interference;
				return true;
			}
			Action = Marked == 0 ? KingdomPurposeFoodLandingAction.Apply
				: (Marked == Carried ? KingdomPurposeFoodLandingAction.AlreadyApplied
					: KingdomPurposeFoodLandingAction.Continue);
			return true;
		}

		/// <summary>What one cargo's durable landing record proves. A record is a receipt and a
		/// count together: an unstamped cargo with no count is simply clean, but a stamped record
		/// this operation cannot claim, a count under no receipt, or a stamp with no count is
		/// ownership nobody can name. Such a state must never read as zero progress, because zero
		/// is what a fresh landing overwrites, and overwriting another operation's record is how
		/// its provision becomes this one's.</summary>
		public static bool TryLandingRecord(bool Stamped, bool Ours, bool Counted, int Count,
			int Carried, out int HighWater)
		{
			HighWater = 0;
			if (Carried <= 0 || Carried > MaxCarriedFood || Count < 0) return false;
			if (!Stamped && !Counted) return true;
			if (!Stamped || !Ours || !Counted || Count < 1 || Count > Carried) return false;
			HighWater = Count;
			return true;
		}

		/// <summary>How a record that outruns the surviving marks must be read. Consumption leaves
		/// nothing wearing the receipt, so a stray mark anywhere off the measured larders proves the
		/// shortfall was a callback moving a serving rather than a settler eating one. Calling that
		/// consumption would publish Delivered over provision the destination never kept.</summary>
		public static KingdomPurposeLandingRecordState ClassifyLandingRecord(int PhysicalMarked,
			int HighWater, int StrayMarks)
		{
			if (PhysicalMarked < 0 || HighWater < 0 || StrayMarks < 0)
				return KingdomPurposeLandingRecordState.Invalid;
			if (StrayMarks > 0) return KingdomPurposeLandingRecordState.Stranded;
			return PhysicalMarked < HighWater ? KingdomPurposeLandingRecordState.Consumed
				: KingdomPurposeLandingRecordState.Intact;
		}

		/// <summary>What this operation still owes, conserved against durable progress rather than
		/// against a count other hands can lower. The physical marks lead while they exceed the
		/// record &mdash; a save cut between creating servings and recording them &mdash; and the
		/// record leads once they fall below it, which is what happens when somebody eats the
		/// provision after it landed. Either way the carriage lands exactly once and no retry mints
		/// a replacement for a serving that was eaten. More of either than the row ever carried is
		/// refused: that is forgery, not progress.</summary>
		public static bool TryLandingOutstanding(int Carried, int PhysicalMarked, int HighWater,
			out int Outstanding, out int Progress)
		{
			Outstanding = 0;
			Progress = 0;
			if (Carried <= 0 || Carried > MaxCarriedFood || PhysicalMarked < 0 || HighWater < 0
				|| PhysicalMarked > Carried || HighWater > Carried) return false;
			Progress = PhysicalMarked > HighWater ? PhysicalMarked : HighWater;
			Outstanding = Carried - Progress;
			return true;
		}

		/// <summary>Normalises the cheap index. A 31-bit FNV can legitimately return zero for a
		/// lawful receipt, and zero is also how an unmarked serving reads, so a raw zero is folded
		/// onto a sentinel instead of refusing the operation that owns it. The index is only a
		/// comparison; the full canonical receipt beside it is the authority either way.</summary>
		public static int LandingIndex(int RawIndex)
		{
			return RawIndex == 0 ? 1 : RawIndex;
		}

		/// <summary>A marked serving belongs to this operation only when both halves of the mark
		/// are present and both agree. Presence is the property existing, never its value being
		/// non-empty or non-zero: an emptied stamp or a zeroed index is a torn mark, and reading
		/// either as absence would turn evidence into ordinary unmarked food.</summary>
		public static bool LandingMarkerIsOurs(string Receipt, int Prefilter, bool MarkPresent,
			int MarkPrefilter, bool StampPresent, string MarkReceipt)
		{
			return !string.IsNullOrEmpty(Receipt) && Prefilter != 0 && MarkPresent && StampPresent
				&& string.Equals(MarkReceipt, Receipt, StringComparison.Ordinal)
				&& MarkPrefilter == Prefilter;
		}

		/// <summary>Whether an object carries a landing mark at all, in either half. Decided on the
		/// properties existing, so a half-bound or emptied mark is still evidence.</summary>
		public static bool LandingMarkerIsPresent(bool MarkPresent, bool StampPresent)
		{
			return MarkPresent || StampPresent;
		}

		/// <summary>Whether a mark names one exact retired operation. Retirement demands exactly
		/// what ownership demands: both halves present, the whole canonical receipt, and the
		/// normalised index agreeing. A prefix match, a missing index, or a wrong index would let a
		/// crafted, future, or half-bound mark be erased, which is how unknown ownership becomes
		/// availability. Anything short of the whole mark survives and is cut on.</summary>
		public static bool LandingMarkerIsRetiredReceipt(string RetiredReceipt, int Prefilter,
			bool MarkPresent, int MarkPrefilter, bool StampPresent, string MarkReceipt)
		{
			return LandingMarkerIsOurs(RetiredReceipt, Prefilter, MarkPresent, MarkPrefilter,
				StampPresent, MarkReceipt);
		}

		/// <summary>Whether a delivered operation's provision can be <em>proved</em> landed. A row
		/// carrying nothing is not applicable. A row that carries provision but holds no exact
		/// recorded progress &mdash; a legacy delivery written before the record existed &mdash; is
		/// unverified, and must be reported as unknown rather than inferred to have landed food.
		/// </summary>
		public static bool LandingIsProved(bool Recorded, int Progress, int Carried)
		{
			return Recorded && Carried > 0 && Progress == Carried;
		}

		/// <summary>Pure aftermath classification for one offered serving. Every observation is an
		/// input, so the table is provable without an engine: a serving never offered is a clean
		/// shortfall, and a serving that was offered and cannot be proved whole, exact, marked, and
		/// inside the exact target larder is stranded, whatever the engine returned or threw.</summary>
		public static KingdomPurposeServingAftermath ClassifyServingAftermath(bool Offered,
			bool Threw, bool SameObject, bool Valid, bool InTargetLarder, bool Whole,
			bool ExactContent, bool MarkerIntact)
		{
			if (!Offered) return KingdomPurposeServingAftermath.Unavailable;
			return Threw || !SameObject || !Valid || !InTargetLarder || !Whole || !ExactContent
				|| !MarkerIntact
				? KingdomPurposeServingAftermath.Stranded
				: KingdomPurposeServingAftermath.Settled;
		}

		/// <summary>Whether both halves of the larder partition came back exactly as the settled
		/// offers predict. Every precondition the engine could refuse on was proved before a serving
		/// was ever offered, so a settled offer owes an exact increment: a short delta is not a
		/// shortfall to retry but a callback moving an earlier mark while the latest settled, and a
		/// retry over it would mint around servings that physically exist. The unmarked half must be
		/// untouched for the same reason, and any inexact mark at all is ownership this operation
		/// cannot account for.</summary>
		public static bool LandingPartitionIsExact(int Before, int Unmarked, int Settled,
			int MarkedAfter, int UnmarkedAfter, bool ExactAfter)
		{
			return Before >= 0 && Unmarked >= 0 && Settled >= 0 && MarkedAfter >= 0
				&& UnmarkedAfter >= 0 && ExactAfter && MarkedAfter == Before + Settled
				&& UnmarkedAfter == Unmarked;
		}

		/// <summary>Whether a root-table entry may be removed on this consumed cargo's behalf. Root
		/// keys share one namespace &mdash; the legacy delimiter form can be named by a different
		/// pair/epoch/operation tuple &mdash; so an entry is deleted only when the value under it is
		/// the object the receipt names: alive and reproving its whole receipt, or the dead remains
		/// of that same identity, which leaves nothing behind but a stale key. A foreign object, or
		/// a value that is not an object at all, is another owner's entry and survives.</summary>
		public static bool RootEntryIsRetirable(bool Rooted, bool SameObjectId, bool StillValid,
			bool ReprovesReceipt)
		{
			return Rooted && SameObjectId && (!StillValid || ReprovesReceipt);
		}

		/// <summary>Whether this phase belongs to an operation whose receipt is already published.
		/// Doctrine: a paused realm refuses new work but must still finish exact committed
		/// recovery, so every phase the drive can reach proceeds under pause. Only the entrance to
		/// a brand-new operation consults the master gate.</summary>
		public static bool OperationPhaseIsCommitted(KingdomPurposeOperationPhase Phase)
		{
			switch (Phase)
			{
			case KingdomPurposeOperationPhase.Prepared:
			case KingdomPurposeOperationPhase.InputDebitPending:
			case KingdomPurposeOperationPhase.InputDebited:
			case KingdomPurposeOperationPhase.LocalDebitPending:
			case KingdomPurposeOperationPhase.LocalDebited:
			case KingdomPurposeOperationPhase.EffectPending:
			case KingdomPurposeOperationPhase.EffectApplied:
			case KingdomPurposeOperationPhase.OutputPending:
			case KingdomPurposeOperationPhase.Dispatching:
			case KingdomPurposeOperationPhase.PickupComplete:
			case KingdomPurposeOperationPhase.LandingPending:
			case KingdomPurposeOperationPhase.Delivered:
				return true;
			default:
				return false;
			}
		}

		/// <summary>The frozen carriage accounting for one directed row: what the source larders
		/// pay, what the destination larders receive, and the difference the founder is owed an
		/// honest word about. No row may land more than it debited, or more than the bound.</summary>
		public static bool TryCarriedFood(KingdomPurposeKind Source,
			KingdomPurposeKind Destination, out int Debited, out int Landed, out int Lost)
		{
			Debited = 0;
			Landed = 0;
			Lost = 0;
			if (!TryRecipe(Source, Destination, out KingdomPurposePortfolioRecipe recipe)
				|| recipe.FoodServings < 0 || recipe.CarriedFood < 0
				|| recipe.CarriedFood > MaxCarriedFood
				|| recipe.CarriedFood > recipe.FoodServings) return false;
			Debited = recipe.FoodServings;
			Landed = recipe.CarriedFood;
			Lost = recipe.FoodServings - recipe.CarriedFood;
			return true;
		}
	}
}
