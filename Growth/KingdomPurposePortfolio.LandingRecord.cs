using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPurpose
	{
		// Landed-ness rides the physical servings rather than the operation receipt, so the frozen
		// cargo/operation/pair wire widths are untouched. Mirrors FoodReceiptJobProperty, except
		// that the integer is only a cheap index: the receipt string beside it is the identity, and
		// a hash alone would let an old colliding mark be counted as this operation's provision.
		private const string PortfolioLandedFoodProperty = "r_TAF_PurposeLandedFood";
		private const string PortfolioLandedReceiptProperty = "r_TAF_PurposeLandedReceipt";
		private const string PortfolioLandedCountProperty = "r_TAF_PurposeLandedCount";
		private const string PortfolioLandedAttemptProperty = "r_TAF_PurposeLandedAttempt";
		private const string PortfolioLandedFaultProperty = "r_TAF_PurposeLandedFault";

		/// <summary>Presence of one owned field under either type table. Both are consulted for
		/// every owned name: a property written under the wrong type, or under both at once, is
		/// still something standing on a name this lane owns, and reading it as absence is how a
		/// crafted or torn mark becomes ordinary food.</summary>
		private static bool OwnedFieldPresent(GameObject Item, string Name)
		{
			return Item.HasStringProperty(Name) || Item.HasIntProperty(Name);
		}

		/// <summary>Presence under exactly the one type this lane writes, and no other. A dual-typed
		/// name is never this lane's own value.</summary>
		private static bool OwnedStringField(GameObject Item, string Name)
		{
			return Item.HasStringProperty(Name) && !Item.HasIntProperty(Name);
		}

		private static bool OwnedIntField(GameObject Item, string Name)
		{
			return Item.HasIntProperty(Name) && !Item.HasStringProperty(Name);
		}

		/// <summary>The durable, monotonic count of servings this operation has landed. A record is
		/// a receipt and a count together, so a false return is ambiguity rather than absence: a
		/// cargo stamped by another operation, a count under no stamp, or a stamp with no count is
		/// ownership this operation cannot name, and reading it as zero progress would let the next
		/// write erase it. Only a clean cargo, or one carrying this operation's own whole receipt,
		/// yields a figure.</summary>
		private static bool TryPurposeLandedRecord(GameObject Cargo, string Receipt, int Carried,
			out int HighWater)
		{
			HighWater = 0;
			if (!GameObject.Validate(Cargo) || string.IsNullOrEmpty(Receipt)) return false;
			// Presence is the property existing. An emptied stamp or a zeroed count is a torn
			// record, and reading either as absence would let the next write take its progress.
			return KingdomPurposePortfolioRules.TryLandingRecord(
				OwnedFieldPresent(Cargo, PortfolioLandedReceiptProperty),
				OwnedStringField(Cargo, PortfolioLandedReceiptProperty)
					&& Cargo.GetStringProperty(PortfolioLandedReceiptProperty) == Receipt,
				OwnedFieldPresent(Cargo, PortfolioLandedCountProperty),
				// Absent is zero, so a cargo that never landed anything reads clean. Present under
				// the wrong type, or under both, is a negative that no reading can accept.
				OwnedIntField(Cargo, PortfolioLandedCountProperty)
					? Cargo.GetIntProperty(PortfolioLandedCountProperty)
					: (OwnedFieldPresent(Cargo, PortfolioLandedCountProperty) ? -1 : 0),
				Carried, out HighWater);
		}

		/// <summary>Raises the record to measured progress and never lowers it, stamping the exact
		/// landing receipt beside it so the pair cannot read one operation's progress as another's.
		/// Written after the physical delta is measured, so a save cut leaves the record low rather
		/// than high and the retry re-observes servings that exist instead of minting them. The two
		/// halves are written together under one guard, with no callback, yield, or save point
		/// between them, so a torn record cannot be produced here; a torn record read back is
		/// therefore foreign, and <see cref="TryPurposeLandedRecord"/> refuses it.</summary>
		private static void RecordPurposeLanded(GameObject Cargo, string Receipt, int Carried,
			int Progress)
		{
			if (!GameObject.Validate(Cargo) || string.IsNullOrEmpty(Receipt) || Progress < 1
				|| !TryPurposeLandedRecord(Cargo, Receipt, Carried, out int recorded)
				|| Progress <= recorded) return;
			Cargo.SetIntProperty(PortfolioLandedCountProperty, Progress);
			Cargo.SetStringProperty(PortfolioLandedReceiptProperty, Receipt);
		}

		/// <summary>Retires the marks of one exact finished operation, removing only the marks and
		/// never their objects. Retirement is by the whole mark &mdash; receipt and normalised index
		/// together &mdash; so malformed, future, wrong-index and half-bound evidence survives and
		/// is cut on: erasing unknown ownership is how it becomes availability. The walk is the
		/// fresh recursive destination custody, not the cached larder children, because a marked
		/// serving that has already been carried out of a larder is exactly the one that must not
		/// be missed. A custody that cannot be walked refuses rather than reporting nothing to
		/// retire. Idempotent, so a cut between this and its checkpoint costs nothing.</summary>
		private static bool TryRetirePurposeLandingMarks(Zone DestinationZone,
			string RetiredReceipt, int Prefilter)
		{
			if (DestinationZone == null || string.IsNullOrEmpty(RetiredReceipt) || Prefilter == 0
				|| !TryLoadedLandingCustody(DestinationZone, out IList<GameObject> loaded))
				return false;
			for (int i = 0; i < loaded.Count; i++)
			{
				GameObject item = loaded[i];
				if (!KingdomPurposePortfolioRules.LandingMarkerIsRetiredReceipt(RetiredReceipt,
						Prefilter, OwnedIntField(item, PortfolioLandedFoodProperty),
						item.GetIntProperty(PortfolioLandedFoodProperty),
						OwnedStringField(item, PortfolioLandedReceiptProperty),
						item.GetStringProperty(PortfolioLandedReceiptProperty))) continue;
				item.RemoveStringProperty(PortfolioLandedReceiptProperty);
				item.RemoveIntProperty(PortfolioLandedFoodProperty);
			}
			return true;
		}

		/// <summary>Stamps the durable witness for one offer before the serving is handed to any
		/// inventory. Written first and on the cargo rather than on the serving, because the
		/// serving is exactly what a callback may destroy: a witness that lived on it would vanish
		/// with the evidence it exists to keep.</summary>
		private static bool StampPurposeLandingAttempt(GameObject Cargo, string Receipt,
			int Expected)
		{
			if (!GameObject.Validate(Cargo) || !KingdomPurposePortfolioRules.TryLandingAttempt(
				Receipt, Expected, out string witness)) return false;
			Cargo.SetStringProperty(PortfolioLandedAttemptProperty, witness);
			return true;
		}

		/// <summary>Retires the witness, and only the exact witness this caller wrote. The pending
		/// value must still be there, and must still name this receipt and this expected step: a
		/// callback that replaced it with a foreign or torn one left evidence, and one that removed
		/// it after placing the exact unit erased the proof that the offer ever happened. Blessing
		/// either is the escape the witness exists to close. A false return is that refusal, and
		/// its caller owes a fault.</summary>
		private static bool TryClearPurposeLandingAttempt(GameObject Cargo, string Receipt,
			int Expected)
		{
			if (!GameObject.Validate(Cargo)
				|| !OwnedFieldPresent(Cargo, PortfolioLandedAttemptProperty)) return false;
			if (!OwnedStringField(Cargo, PortfolioLandedAttemptProperty)
				|| !KingdomPurposePortfolioRules.TryReadLandingAttempt(
					Cargo.GetStringProperty(PortfolioLandedAttemptProperty), Receipt,
					out int pending) || pending != Expected) return false;
			Cargo.RemoveStringProperty(PortfolioLandedAttemptProperty);
			return true;
		}

		/// <summary>Stamps the durable fault for one ambiguous aftermath, before that ambiguity is
		/// ever returned. Separate from the attempt witness because the attempt witness can be
		/// honestly reconciled: a callback that throws after placing the exact unit leaves a ground
		/// the attempt reads as settled, and a refused quarantine would then let the next pass
		/// retire it and carry on. Written on the cargo, so it outlives the serving.</summary>
		private static bool StampPurposeLandingFault(GameObject Cargo, string Receipt, int Expected,
			int Observed)
		{
			// The diagnostic figures are folded onto an explicit over-bound sentinel rather than
			// refused. Forged or excess evidence is exactly the ambiguity that most needs a durable
			// fault, and it must never be the one case that quarantines without one. The stamp is
			// read back, so a refusal to persist is itself reported rather than assumed away.
			if (!GameObject.Validate(Cargo) || !KingdomPurposePortfolioRules.TryLandingFault(Receipt,
				KingdomPurposePortfolioRules.LandingFaultFigure(Expected),
				KingdomPurposePortfolioRules.LandingFaultFigure(Observed), out string witness))
				return false;
			Cargo.SetStringProperty(PortfolioLandedFaultProperty, witness);
			return PurposeLandingIsFaulted(Cargo)
				&& Cargo.GetStringProperty(PortfolioLandedFaultProperty) == witness;
		}

		/// <summary>Whether any fault stands on this cargo. Presence only, in either property type:
		/// an emptied, wrong-typed, foreign or torn fault is still the record that something went
		/// wrong, and reading any of them as absence is exactly the escape the fault exists to
		/// close.</summary>
		private static bool PurposeLandingIsFaulted(GameObject Cargo)
		{
			return GameObject.Validate(Cargo)
				&& OwnedFieldPresent(Cargo, PortfolioLandedFaultProperty);
		}

		/// <summary>Whether a delivered cargo carries a shape this operation may retire. Exactly two
		/// are lawful: nothing at all &mdash; a legacy delivery written before the record existed
		/// &mdash; or one whole record of this operation's own whole carriage. A partial, torn,
		/// wrong-typed or foreign record, a serving index, or an unexpected attempt or fault is
		/// evidence that will name nobody once the operation and its root are forgotten, and every
		/// later landing in that city would cut on it. It blocks the retirement; it is never
		/// quietly cleared.</summary>
		private static bool PurposeCargoRecordIsRetirable(GameObject Cargo, string Receipt,
			int Carried)
		{
			if (!GameObject.Validate(Cargo)) return false;
			if (OwnedFieldPresent(Cargo, PortfolioLandedFoodProperty)
				|| OwnedFieldPresent(Cargo, PortfolioLandedAttemptProperty)
				|| OwnedFieldPresent(Cargo, PortfolioLandedFaultProperty)) return false;
			if (!OwnedFieldPresent(Cargo, PortfolioLandedReceiptProperty)
				&& !OwnedFieldPresent(Cargo, PortfolioLandedCountProperty)) return true;
			return Carried > 0 && OwnedStringField(Cargo, PortfolioLandedReceiptProperty)
				&& Cargo.GetStringProperty(PortfolioLandedReceiptProperty) == Receipt
				&& TryPurposeLandedRecord(Cargo, Receipt, Carried, out int recorded)
				&& recorded == Carried;
		}

		/// <summary>Clears the classified shape and reproves its absence. Idempotent: a cargo that
		/// already carries nothing is already retired.</summary>
		private static bool TryClearPurposeLandingWitnesses(GameObject Cargo, string Receipt,
			int Carried)
		{
			if (!PurposeCargoRecordIsRetirable(Cargo, Receipt, Carried)) return false;
			Cargo.RemoveStringProperty(PortfolioLandedReceiptProperty);
			Cargo.RemoveIntProperty(PortfolioLandedCountProperty);
			return !OwnedFieldPresent(Cargo, PortfolioLandedReceiptProperty)
				&& !OwnedFieldPresent(Cargo, PortfolioLandedCountProperty);
		}

		/// <summary>What an outstanding witness permits this pass. An unreadable or foreign witness
		/// is ambiguity, never absence, so a refused quarantine publication cannot be forgotten by
		/// the next pass and answered with a fresh serving. Presence is the property existing, not
		/// its value being non-empty: a witness torn down to an empty string is still the record of
		/// an offer, and reading emptiness as "no offer" would hand exactly that case a fresh
		/// serving.</summary>
		private static KingdomPurposeLandingAttemptState ReadPurposeLandingAttempt(GameObject Cargo,
			string Receipt, int Observed, bool Exact)
		{
			int expected = 0;
			// A same-name integer is torn presence too: string ownership of the witness requires
			// that no integer property claims the name.
			bool present = GameObject.Validate(Cargo)
				&& OwnedFieldPresent(Cargo, PortfolioLandedAttemptProperty);
			bool ours = present && OwnedStringField(Cargo, PortfolioLandedAttemptProperty)
				&& KingdomPurposePortfolioRules.TryReadLandingAttempt(
					Cargo.GetStringProperty(PortfolioLandedAttemptProperty), Receipt, out expected);
			return KingdomPurposePortfolioRules.ClassifyLandingWitnesses(
				PurposeLandingIsFaulted(Cargo), present, ours, expected, Observed, Exact);
		}
	}
}
