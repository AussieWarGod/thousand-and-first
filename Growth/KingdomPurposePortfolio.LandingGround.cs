using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPurpose
	{
		// The same bound the construction lookup walks under, so an unbounded or cyclic custody
		// refuses the scan instead of hanging it.
		private const int MaxLandingCustodyObjects = 4096;

		/// <summary>The measured larders, taken once and then never rebuilt. The survey's own list
		/// is a mutable roster a callback can shorten or lengthen, and a rebuilt roster that
		/// quietly filtered the invalid entries would make every later universal proof vacuous: an
		/// emptied larder simply disappears, and an injected one is never measured. Every entry is
		/// proved rather than filtered, so a roster that cannot all be proved refuses outright.</summary>
		private static bool TryPurposeLarderRoster(KingdomSurvey Survey, Zone DestinationZone,
			out List<GameObject> Roster)
		{
			Roster = new List<GameObject>();
			if (Survey == null || DestinationZone == null) return false;
			for (int i = 0; i < Survey.Larders.Count; i++)
			{
				// A duplicate reference would make the roster non-injective, and a later
				// membership comparison would then accept [A,A] for [A,B].
				if (!ExactMeasuredLarder(Survey.Larders[i], DestinationZone)
					|| IsMeasuredLarder(Roster, Survey.Larders[i])) return false;
				Roster.Add(Survey.Larders[i]);
			}
			Roster.Sort((a, b) => string.CompareOrdinal(a.IDIfAssigned, b.IDIfAssigned));
			return true;
		}

		/// <summary>Whether the survey still names exactly the roster this landing measured. The
		/// snapshot is what every proof runs over, so the survey diverging from it means the ground
		/// changed under the transaction even where each surviving entry still proves out.</summary>
		private static bool SamePurposeLarderRoster(List<GameObject> Roster, KingdomSurvey Survey)
		{
			if (Roster == null || Survey == null || Survey.Larders.Count != Roster.Count)
				return false;
			// Injective both ways, and duplicate-free: equal counts plus one-way membership would
			// accept a roster a callback rewrote to [A,A] in place of [A,B].
			for (int i = 0; i < Survey.Larders.Count; i++)
			{
				if (!IsMeasuredLarder(Roster, Survey.Larders[i])) return false;
				for (int j = i + 1; j < Survey.Larders.Count; j++)
					if (ReferenceEquals(Survey.Larders[i], Survey.Larders[j])) return false;
			}
			for (int i = 0; i < Roster.Count; i++)
				if (!IsMeasuredLarder(Survey.Larders, Roster[i])) return false;
			return true;
		}

		/// <summary>Reproves the ground the landing just wrote on, after every callback and before
		/// any durable record or checkpoint. Four separate proofs, because four different things
		/// can have gone wrong: the survey's roster may no longer be the one measured, a larder in
		/// the snapshot may no longer be a dedicated larder on this ground, the survey may refuse
		/// to resynchronise one of them, and a callback may have hidden marked or malformed
		/// evidence in custody the pre-callback index never saw.</summary>
		private static bool TryRevalidateLandingGround(KingdomSurvey Survey,
			List<GameObject> Larders, Zone DestinationZone, GameObject Cargo, string Receipt,
			int Prefilter, int Carried, out string Fault)
		{
			Fault = null;
			if (!SamePurposeLarderRoster(Larders, Survey))
				return Fail("The destination's measured larder roster changed under the provision callbacks.",
					out Fault);
			for (int i = 0; i < Larders.Count; i++)
			{
				if (!ExactMeasuredLarder(Larders[i], DestinationZone))
					return Fail("A measured destination larder is no longer a dedicated larder on this ground.",
						out Fault);
				if (!Survey.SynchronizeReceiptObject(Larders[i]))
					return Fail("A measured destination larder refused to resynchronise after the provision callbacks.",
						out Fault);
			}
			if (!TryPurposeCustodyStrays(Larders, DestinationZone, Cargo, Receipt, Prefilter,
				Carried, false, out int strays))
				return Fail("The destination's loaded custody could not be proved complete.",
					out Fault);
			return strays == 0
				|| Fail("Evidence under this operation's landing fields stands outside its measured larders.",
					out Fault);
		}

		/// <summary>One measured larder as this lane requires it, proved again rather than trusted
		/// from the cached survey row: still a valid object, still standing directly on the
		/// destination ground rather than inside something, still dedicated, and still holding an
		/// inventory. A callback may undedicate, move, or invalidate a larder while the serving
		/// sits inside it, and the food-owner reference alone would not notice.</summary>
		private static bool ExactMeasuredLarder(GameObject Larder, Zone DestinationZone)
		{
			return GameObject.Validate(Larder) && !Larder.IsInvalid() && !Larder.IsInGraveyard()
				&& Larder.CurrentZone == DestinationZone && Larder.InInventory == null
				&& Larder.GetIntProperty("KingdomLarder") == 1 && Larder.Inventory != null
				&& KingdomSurvey.HeldIn(Larder) <= KingdomSurvey.CapacityOf(Larder);
		}

		/// <summary>Whether every measured larder still holds no more than it can hold. A callback
		/// may shrink a larder's capacity under the servings already inside it, and a landing that
		/// published Delivered over an overheld store would be recording provision the destination
		/// cannot keep. Reproved after every offer and again before the record, because the room a
		/// loop measured once is not the room the next callback leaves behind.</summary>
		private static bool PurposeLardersWithinCapacity(List<GameObject> Larders)
		{
			for (int i = 0; Larders != null && i < Larders.Count; i++)
				if (!GameObject.Validate(Larders[i]) || Larders[i].Inventory == null
					|| KingdomSurvey.HeldIn(Larders[i]) > KingdomSurvey.CapacityOf(Larders[i]))
					return false;
			return true;
		}

		/// <summary>Reproves the frozen destination store itself against the operation's own frozen
		/// identity, not against an object reference a caller happens to hold. A callback may
		/// invalidate the store, move it off the ground, change what its id resolves to, or strip
		/// its stockpile dedication while <c>Cargo.InInventory</c> still points at the same
		/// object.</summary>
		private static bool TryExactDestinationStore(KingdomPurposeOperationReceipt Operation,
			out GameObject Store, out string Fault)
		{
			Store = null;
			Fault = null;
			if (Operation == null || !TryPurposeZone(Operation.DestinationZoneId, out Zone zone))
				return Fail("The frozen destination ground is unavailable at the landing checkpoint.",
					out Fault);
			// Corruption defence, not a nesting hole: CurrentZone is CurrentCell?.ParentZone
			// (XRL/World/GameObject.cs:532) and does not follow InInventory, so ordinary nesting
			// already fails the zone test. These three restate the same fact from the other side,
			// so a torn state whose cell pointer still names this ground while its ownership or
			// cell list disagrees cannot pass as a store standing on it.
			if (FindExactKnown(zone, Operation.DestinationInputStoreId, out Store)
					!= KingdomPhysicalLookupState.Exact
				|| !GameObject.Validate(Store) || Store.CurrentZone != zone
				|| Store.InInventory != null || Store.CurrentCell == null
				|| !ReferenceEquals(Store.CurrentCell.ParentZone, zone)
				|| Store.CurrentCell.Objects == null
				|| !Store.CurrentCell.Objects.Contains(Store))
				return Fail("The frozen destination store is no longer exactly on its own ground.",
					out Fault);
			return KingdomMaterials.IsStockpile(Store) && Store.Inventory != null
				|| Fail("The frozen destination store lost its stockpile dedication.", out Fault);
		}

		/// <summary>Everything in the destination's loaded custody standing on a name this lane
		/// owns that this operation cannot account for. Exactly two things are permitted: the exact
		/// cargo carrying a record that reads whole, and this operation's own exact marks on direct
		/// children of the immutable measured roster. Every other presence under an owned name
		/// &mdash; half-bound, emptied, wrong-typed, foreign, or simply somewhere else &mdash; is
		/// interference, because the malformed evidence that must survive retirement is exactly the
		/// evidence a scan for well-formed marks would never see. A false return is an unproved
		/// scan, never a clean one.</summary>
		private static bool TryPurposeCustodyStrays(List<GameObject> Larders, Zone DestinationZone,
			GameObject Cargo, string Receipt, int Prefilter, int Carried, bool Attempted,
			out int Strays)
		{
			Strays = 0;
			if (!TryLoadedLandingCustody(DestinationZone, out IList<GameObject> loaded))
				return false;
			for (int i = 0; i < loaded.Count; i++)
			{
				GameObject item = loaded[i];
				bool marked = OwnedFieldPresent(item, PortfolioLandedFoodProperty)
					|| OwnedFieldPresent(item, PortfolioLandedReceiptProperty);
				bool held = OwnedFieldPresent(item, PortfolioLandedCountProperty)
					|| OwnedFieldPresent(item, PortfolioLandedAttemptProperty)
					|| OwnedFieldPresent(item, PortfolioLandedFaultProperty);
				if (!marked && !held) continue;
				if (ReferenceEquals(item, Cargo))
				{
					// The cargo shares the receipt field name with a serving's mark, so the record
					// is disambiguated by its whole shape: a record, and nothing else at all. An
					// attempt or fault standing beside it is an offer this pass did not reconcile,
					// and a serving index on the cargo is not a record in any reading.
					// The one exception is entry recovery: an attempt this pass has already proved
					// reconciled against the fresh physical count is authorised to stand until it
					// is strictly cleared, or the single legitimate save-cut recovery could never
					// pass its own custody proof. Nothing else authorises an attempt, ever.
					if (!OwnedFieldPresent(item, PortfolioLandedFoodProperty)
						&& (Attempted || !OwnedFieldPresent(item, PortfolioLandedAttemptProperty))
						&& !OwnedFieldPresent(item, PortfolioLandedFaultProperty)
						&& TryPurposeLandedRecord(item, Receipt, Carried, out _)) continue;
					Strays++;
					continue;
				}
				if (held || !IsMeasuredLarder(Larders, item.InInventory)
					|| !WearsPurposeLandingMark(item, Receipt, Prefilter)) Strays++;
			}
			return true;
		}

		/// <summary>The complete loaded custody of the destination ground: every object standing on
		/// it and every object held inside those, to any depth. One inventory level is not custody:
		/// an inventory callback is arbitrary engine code and may nest a serving inside a container
		/// inside an actor. The walk is always taken fresh from the current zone roots rather than
		/// read from the survey's cached index, because that index is maintained by observations
		/// the callback never made and would bless exactly the clone this scan exists to find. It
		/// is bounded and cycle-safe, loads no other zone, and refuses rather than reporting an
		/// incomplete custody as an empty one.</summary>
		private static bool TryLoadedLandingCustody(Zone DestinationZone,
			out IList<GameObject> Loaded)
		{
			Loaded = null;
			if (DestinationZone == null) return false;
			// An index that cannot be read is incomplete, never empty. A null root list, an
			// inventory that reports itself present but hands back no list, and an entry that
			// cannot be inspected are all custody this scan did not see, and blessing any of them
			// as absence is exactly how a marked serving disappears from a proof.
			List<GameObject> roots = DestinationZone.GetObjects();
			if (roots == null) return false;
			List<GameObject> pending = new List<GameObject>();
			for (int i = 0; i < roots.Count; i++)
			{
				if (!GameObject.Validate(roots[i]) || roots[i].CurrentZone != DestinationZone)
					return false;
				pending.Add(roots[i]);
			}
			List<GameObject> walked = new List<GameObject>();
			HashSet<GameObject> seen = new HashSet<GameObject>();
			for (int cursor = 0; cursor < pending.Count; cursor++)
			{
				GameObject item = pending[cursor];
				if (!GameObject.Validate(item) || !seen.Add(item)
					|| walked.Count >= MaxLandingCustodyObjects) return false;
				walked.Add(item);
				if (item.Inventory == null) continue;
				if (item.Inventory.Objects == null) return false;
				for (int i = 0; i < item.Inventory.Objects.Count; i++)
					pending.Add(item.Inventory.Objects[i]);
			}
			Loaded = walked;
			return true;
		}

		/// <summary>Pre-retirement scan. Nothing is mutated until every piece of owned evidence on
		/// the ground has been read and allowed: the prevalidated cargo, and exact marks of the
		/// operation about to be retired. Any other shape &mdash; malformed, half-bound,
		/// wrong-index, foreign, or a stray record, attempt or fault &mdash; refuses the retirement
		/// while the serving marks are still standing, so a refused retirement leaves every witness
		/// exactly where it was.</summary>
		private static bool OnlyRetirableLandingEvidence(Zone DestinationZone, GameObject Allowed,
			string Receipt, int Prefilter)
		{
			if (!TryLoadedLandingCustody(DestinationZone, out IList<GameObject> loaded))
				return false;
			for (int i = 0; i < loaded.Count; i++)
			{
				GameObject item = loaded[i];
				if (!AnyPurposeLandingField(item) || ReferenceEquals(item, Allowed)) continue;
				if (OwnedFieldPresent(item, PortfolioLandedCountProperty)
					|| OwnedFieldPresent(item, PortfolioLandedAttemptProperty)
					|| OwnedFieldPresent(item, PortfolioLandedFaultProperty)
					|| !WearsPurposeLandingMark(item, Receipt, Prefilter)) return false;
			}
			return true;
		}

		/// <summary>Absence reproof, over every one of the five owned landing names in both
		/// property tables, allowing at most one prevalidated object. Proving only well-formed
		/// marks gone would be no proof at all: retirement deliberately preserves malformed,
		/// half-bound and wrong-index evidence, and that is exactly what would outlive the
		/// operation and cut every later landing in this city.</summary>
		private static bool NoPurposeLandingEvidenceRemains(Zone DestinationZone,
			GameObject Allowed)
		{
			if (!TryLoadedLandingCustody(DestinationZone, out IList<GameObject> loaded))
				return false;
			for (int i = 0; i < loaded.Count; i++)
				if (!ReferenceEquals(loaded[i], Allowed)
					&& AnyPurposeLandingField(loaded[i])) return false;
			return true;
		}

		private static bool AnyPurposeLandingField(GameObject Item)
		{
			return OwnedFieldPresent(Item, PortfolioLandedFoodProperty)
				|| OwnedFieldPresent(Item, PortfolioLandedReceiptProperty)
				|| OwnedFieldPresent(Item, PortfolioLandedCountProperty)
				|| OwnedFieldPresent(Item, PortfolioLandedAttemptProperty)
				|| OwnedFieldPresent(Item, PortfolioLandedFaultProperty);
		}

		private static bool WearsPurposeLandingMark(GameObject Item, string Receipt, int Prefilter)
		{
			return GameObject.Validate(Item)
				&& KingdomPurposePortfolioRules.LandingMarkerIsOurs(Receipt, Prefilter,
					OwnedIntField(Item, PortfolioLandedFoodProperty),
					Item.GetIntProperty(PortfolioLandedFoodProperty),
					OwnedStringField(Item, PortfolioLandedReceiptProperty),
					Item.GetStringProperty(PortfolioLandedReceiptProperty));
		}

		private static bool IsMeasuredLarder(List<GameObject> Larders, GameObject Candidate)
		{
			for (int i = 0; Larders != null && i < Larders.Count; i++)
				if (ReferenceEquals(Larders[i], Candidate)) return true;
			return false;
		}
	}
}
