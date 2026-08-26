using System.Collections.Generic;

using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomProcedureRules
	{
		// --- The knowledge gate ----------------------------------------------------------------

		/// <summary>
		/// Whether a city's keepers hold everything a record asks for.
		/// <para>
		/// <b>The shipped roster grammar and nothing of ours.</b> ALL tokens are required and each
		/// may carry alternatives, exactly as a <c>&lt;building&gt;</c>'s <c>Knowledge</c> does
		/// (<c>KingdomZoningRules.Knows</c>) &mdash; which is what lets a procedure gate on a
		/// research node, a shared rite, a taught disk or a certified machine with one attribute,
		/// and lets a third party's procedure gate on a third party's research with no C# at all.
		/// </para>
		/// </summary>
		/// <param name="Roster">The city's rolls. Null reads as a city that knows nothing.</param>
		/// <param name="Knowledge">The record's declaration. Null or empty asks nothing.</param>
		public static bool KnowledgeMet(ICollection<string> Roster, string Knowledge)
		{
			foreach (string token in KingdomZoningRules.Tokens(Knowledge))
			{
				if (!KingdomZoningRules.Knows(Roster, token))
				{
					return false;
				}
			}
			return true;
		}

		// --- The magnitude band (QUESTION-BACKLOG QB-10) --------------------------------------

		/// <summary>
		/// Reads a <c>Magnitude</c> attribute: <c>Field:Low-High</c>, both ends inclusive.
		/// </summary>
		/// <returns>False when the attribute is present and unreadable, which is a typo wearing a
		/// rule's clothes and is refused at load. An absent attribute reads as true with a null
		/// field, which is the ordinary state of every record that takes any source.</returns>
		public static bool TryParseMagnitude(string Source, out string Field, out int Low, out int High, out string Error)
		{
			Field = null;
			Low = 0;
			High = 0;
			Error = null;
			if (string.IsNullOrEmpty(Source) || Source.Trim().Length == 0)
			{
				return true;
			}
			string text = Source.Trim();
			int colon = text.IndexOf(':');
			int dash = text.IndexOf('-', (colon < 0) ? 0 : colon + 1);
			if (colon <= 0 || dash <= colon + 1)
			{
				Error = "\"" + Source + "\" is not Field:Low-High.";
				return false;
			}
			string field = text.Substring(0, colon).Trim();
			int low;
			int high;
			if (field.Length == 0
				|| !int.TryParse(text.Substring(colon + 1, dash - colon - 1).Trim(), out low)
				|| !int.TryParse(text.Substring(dash + 1).Trim(), out high))
			{
				Error = "\"" + Source + "\" carries no readable band.";
				return false;
			}
			if (low > high)
			{
				Error = "\"" + Source + "\" runs backwards.";
				return false;
			}
			Field = field;
			Low = low;
			High = high;
			return true;
		}

		/// <summary>
		/// Whether a stamped source falls in a record's band. A record with no band takes anything;
		/// a record WITH a band refuses a source whose field could not be read, because admitting a
		/// number nobody could read is exactly how a rung-2 price buys a rung-3 product.
		/// </summary>
		public static bool MagnitudeAdmits(LabProcedure Procedure, string Stamp)
		{
			if (Procedure == null)
			{
				return false;
			}
			string field;
			int low;
			int high;
			string error;
			if (!TryParseMagnitude(Procedure.Magnitude, out field, out low, out high, out error) || field == null)
			{
				return true;
			}
			int value;
			return int.TryParse(StampedField(Stamp, Procedure.Grants, field), out value) && value >= low && value <= high;
		}

		// --- The slot judgment (DIVERSITY §3.4 hard rules 2 and 3) -----------------------------

		/// <summary>The slot types a record names, folded and trimmed. Empty means the record names
		/// none, which no judgment below will ever match &mdash; deliberately, because a record that
		/// forgot to say where it goes must not go everywhere.</summary>
		public static List<string> SlotTypes(LabProcedure Procedure)
		{
			return Split((Procedure == null) ? null : Procedure.Slots);
		}

		/// <summary>
		/// The category names a record names, in the case the file wrote them. Empty admits any live
		/// category.
		/// <para>
		/// Trimmed and NOT folded, unlike <see cref="SlotTypes"/>, and the asymmetry is forced by
		/// vanilla: <c>BodyPartCategory.GetCode</c> switches on exact strings and answers zero for
		/// anything it does not recognise (<c>D/XRL/World/Anatomy/BodyPartCategory.cs:104-160</c>).
		/// Slot types are compared against a founder's own anatomy by us, so we fold both sides;
		/// category names are handed to the engine, so they go as written.
		/// </para>
		/// </summary>
		public static List<string> SlotCategoryNames(LabProcedure Procedure)
		{
			return SplitTrimmed((Procedure == null) ? null : Procedure.SlotCategories);
		}

		/// <summary>
		/// Whether one place on the founder's body could take one procedure, and if not, why.
		/// <para>
		/// The order is the order the founder would want to hear it in: the wrong place first
		/// (nothing can answer that), then the wrong kind of place, then the place already spoken
		/// for, then the place with nothing on it to ride. Each is a different sentence and each
		/// names a different thing to go and do.
		/// </para>
		/// </summary>
		/// <param name="Procedure">The record.</param>
		/// <param name="Slot">One place, read off the real anatomy.</param>
		/// <param name="Categories">The category CODES this record admits, resolved by the engine
		/// half through <c>BodyPartCategory</c>'s own name table
		/// (<c>D/XRL/World/Anatomy/BodyPartCategory.cs:104,163</c>). Null or empty admits any.</param>
		public static LabVerdict JudgeSlot(LabProcedure Procedure, LabSlot Slot, IList<int> Categories)
		{
			if (Procedure == null)
			{
				return LabVerdict.RefusedNoSlot;
			}
			List<string> wanted = SlotTypes(Procedure);
			if (wanted.Count == 0 || !wanted.Contains(Fold(Slot.Type)))
			{
				return LabVerdict.RefusedNoSlot;
			}
			// Vanilla's own disqualifier, and it leads because it is the one that is true about the
			// place rather than about the record: worn scaffolding is not a body, whatever the
			// record wants (BodyPart.CanReceiveCyberneticImplant, D/…/BodyPart.cs:7074-7077).
			if (Slot.Extrinsic)
			{
				return LabVerdict.RefusedCategory;
			}
			if (Categories != null && Categories.Count > 0 && !Categories.Contains(Slot.Category))
			{
				return LabVerdict.RefusedCategory;
			}
			if (Slot.Grafted != null)
			{
				return LabVerdict.RefusedSlotTaken;
			}
			if (Procedure.Attach == LabAttach.Weapon && !Slot.Bears)
			{
				return LabVerdict.RefusedNoWeapon;
			}
			return LabVerdict.Allowed;
		}

		/// <summary>
		/// The places on this body that would take this procedure, in anatomy order.
		/// <para>
		/// Anatomy order rather than sorted, because the founder reads their own body top to bottom
		/// and the slate must list it the way they would say it.
		/// </para>
		/// </summary>
		public static List<int> LegalSlots(LabProcedure Procedure, IList<LabSlot> Anatomy, IList<int> Categories)
		{
			List<int> legal = new List<int>();
			if (Procedure == null || Anatomy == null)
			{
				return legal;
			}
			for (int i = 0; i < Anatomy.Count; i++)
			{
				if (JudgeSlot(Procedure, Anatomy[i], Categories) == LabVerdict.Allowed)
				{
					legal.Add(i);
				}
			}
			return legal;
		}

		/// <summary>
		/// The kindest true refusal for a procedure with no legal slot at all. Walking the body
		/// once and keeping the most specific answer, so a founder with a taken arm hears "already
		/// spoken for" rather than "there is nowhere on you", which would be a lie.
		/// </summary>
		public static LabVerdict BestRefusal(LabProcedure Procedure, IList<LabSlot> Anatomy, IList<int> Categories)
		{
			LabVerdict best = LabVerdict.RefusedNoSlot;
			if (Procedure == null || Anatomy == null)
			{
				return best;
			}
			for (int i = 0; i < Anatomy.Count; i++)
			{
				LabVerdict verdict = JudgeSlot(Procedure, Anatomy[i], Categories);
				if (verdict == LabVerdict.Allowed)
				{
					return LabVerdict.Allowed;
				}
				// Ranked by how near the founder is to having it: a slot bearing no weapon is one
				// natural weapon away; a taken slot is one removal away; a wrong-kind slot is a
				// body away; no slot at all is the furthest of the four.
				if (Rank(verdict) > Rank(best))
				{
					best = verdict;
				}
			}
			return best;
		}

		private static int Rank(LabVerdict Verdict)
		{
			switch (Verdict)
			{
			case LabVerdict.RefusedNoWeapon:
				return 3;
			case LabVerdict.RefusedSlotTaken:
				return 2;
			case LabVerdict.RefusedCategory:
				return 1;
			default:
				return 0;
			}
		}

		/// <summary>
		/// The whole verdict on one commission, anatomy and hall and vat-house and history
		/// together. What the slate calls before it offers a row, and what the commit calls again
		/// before it takes a dram &mdash; because the founder may have been away and the answer may
		/// have changed.
		/// </summary>
		/// <param name="Procedure">The record.</param>
		/// <param name="Anatomy">The founder's own body.</param>
		/// <param name="Categories">Resolved category codes; null admits any.</param>
		/// <param name="Rung">The highest rung of lab standing in this city.</param>
		/// <param name="Kept">Preserved parts in the vat-house that are lawful sources.</param>
		/// <param name="Discovered">Whether a named procedure has been found in the world. Ignored
		/// for every other class.</param>
		/// <param name="AlreadyDone">Whether a named procedure has already been performed on this
		/// founder.</param>
		public static LabVerdict Judge(LabProcedure Procedure, IList<LabSlot> Anatomy, IList<int> Categories,
			int Rung, int Kept, bool Discovered, bool AlreadyDone)
		{
			if (Procedure == null)
			{
				return LabVerdict.RefusedNoSlot;
			}
			// Discovery is asked first and answered in silence, because every other refusal names
			// the procedure and this is the one that may not (Addendum 14, Addendum 20's hidden
			// clause). A named procedure nobody has found has no row at all.
			if (Procedure.IsNamed && !Discovered)
			{
				return LabVerdict.RefusedUndiscovered;
			}
			if (Procedure.IsNamed && AlreadyDone)
			{
				return LabVerdict.RefusedOnceEver;
			}
			if (Rung < Procedure.MinRung)
			{
				return LabVerdict.RefusedRung;
			}
			if (Kept < Procedure.Preserved)
			{
				return LabVerdict.RefusedUnkept;
			}
			return BestRefusal(Procedure, Anatomy, Categories);
		}

	}
}
