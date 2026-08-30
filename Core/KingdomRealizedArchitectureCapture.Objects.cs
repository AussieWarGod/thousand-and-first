using System;
using System.Collections.Generic;

using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomRealizedArchitectureCapture
	{
		/// <summary>A finished layout has advanced past its last authored layer.</summary>
		private const int CompleteStage = 3;

		/// <summary>The stamper's own settled output state for one placement.</summary>
		private const int SettledOutput = 2;

		/// <summary>
		/// The owner plus exactly the components its own frozen receipts name.
		/// <para>
		/// The census is driven by the completed owner's snapshot placements and per-slot output
		/// receipts, never by whatever in the zone happens to carry a matching hash. A hash and a
		/// component token are values on an object; anything that can write a property can write
		/// both. Only the owner's output id, recomputed token, and exact rotated coordinate bind a
		/// component to the layout that claims it, so a forged or copied marking is refused before it
		/// can enter a digest rather than measured as if it were architecture.
		/// </para>
		/// </summary>
		private static bool TryObjects(Zone Zone, GameObject Owner, KingdomArchitectureIntent Intent,
			ArchitectureLayoutSnapshot Snapshot, string Lot, int X1, int Y1, int Width, int Height,
			out List<KingdomRealizedObjectFact> Objects, out string Failure)
		{
			Objects = null;
			Failure = null;
			if (!TryProveOwnerAuthority(Owner, Lot, out Failure)) return false;
			if (Snapshot == null || Snapshot.Placements == null)
				return Fail("the owner receipt decodes to no placement list", out Failure);
			if (Snapshot.Placements.Count > KingdomRealizedCaptureRules.MaxObjects)
				return Fail("the lot exceeds the bounded capture object count", out Failure);
			List<KingdomRealizedObjectFact> facts = new List<KingdomRealizedObjectFact>();
			Dictionary<string, GameObject> receipted =
				new Dictionary<string, GameObject>(StringComparer.Ordinal);
			if (!TryOwnerFact(Zone, Owner, Intent, X1, Y1, Width, Height, facts, out Failure))
				return false;
			for (int i = 0; i < Snapshot.Placements.Count; i++)
			{
				ArchitecturePlacement placement = Snapshot.Placements[i];
				GameObject item;
				if (!TryReceiptedComponent(Zone, Owner, Intent, Snapshot, Lot, placement, receipted,
					out item, out Failure)) return false;
				KingdomRealizedObjectFact fact;
				if (!TryFact(item, item.CurrentCell.X - X1, item.CurrentCell.Y - Y1, false,
					out fact, out Failure)) return false;
				facts.Add(fact);
			}
			if (!TryRefuseUnreceipted(Zone, Owner, Intent, Lot, X1, Y1, Width, Height, receipted,
				out Failure)) return false;
			Objects = facts;
			return true;
		}

		/// <summary>
		/// The owner's own top-level layout authority, by exact type presence and exact value.
		/// <para>
		/// The per-slot receipts are only as good as the record that holds them. A dual-typed stage
		/// key answers a default int read with a lawful-looking number, so a half-built layout could
		/// present itself as complete and be measured as a finished one.
		/// </para>
		/// </summary>
		private static bool TryProveOwnerAuthority(GameObject Owner, string Lot, out string Failure)
		{
			Failure = null;
			if (!ExactInt(Owner, KingdomArchitectureStamper.SchemaProperty)
				|| Owner.GetIntProperty(KingdomArchitectureStamper.SchemaProperty)
					!= KingdomArchitectureStamper.LayoutSchema)
				return Fail("the layout owner's schema key is absent, dual-typed, or not this "
					+ "schema", out Failure);
			if (!ExactText(Owner, KingdomArchitectureStamper.LotIdProperty)
				|| !ExactText(Owner, KingdomArchitectureStamper.HashProperty))
				return Fail("the layout owner's lot or hash key is absent or lives under the "
					+ "wrong durable type table", out Failure);
			// Plot custody has TWO lawful shapes, and which one is present is provenance
			// validity, never cross-path identity (the carried-marker and lot-id rulings):
			// an ordinary commission stakes a plot, so its owner carries exact string custody
			// naming this lot; a gallery staging deliberately stakes NO plot, so its owner
			// carries a gallery receipt and no custody key under any table. A custody key of
			// the wrong type, a dual-typed key, or a receipt-and-custody mix is torn and
			// refuses. Proven live 2026-08-30: the first real gallery capture carried no plot
			// custody, which is the lawful gallery shape, not damage.
			bool custodyString = Owner.HasStringProperty(KingdomPlots.PlotIdProperty);
			bool custodyInt = Owner.HasIntProperty(KingdomPlots.PlotIdProperty);
			bool galleryReceipt = ExactText(Owner, GalleryReceiptProperty);
			if (custodyInt)
				return Fail("the layout owner's plot custody key lives under the wrong durable "
					+ "type table", out Failure);
			if (custodyString && galleryReceipt)
				return Fail("the layout owner carries both plot custody and a gallery receipt; "
					+ "no lawful path writes both", out Failure);
			if (!custodyString && !galleryReceipt)
				return Fail("the layout owner carries neither plot custody nor a gallery "
					+ "receipt; no lawful path staged it", out Failure);
			// Type is not custody. Every component receipt below is keyed to this lot, so a valid
			// layout receipt sitting on a root whose plot custody names a DIFFERENT lot would drag
			// another lot's ground into this digest.
			if (custodyString
				&& !string.Equals(Owner.GetStringProperty(KingdomPlots.PlotIdProperty), Lot,
					StringComparison.Ordinal))
				return Fail("the layout owner's plot custody names a different lot than its layout "
					+ "receipt", out Failure);
			if (!string.Equals(Owner.GetStringProperty(
					KingdomArchitectureStamper.LotIdProperty), Lot, StringComparison.Ordinal))
				return Fail("the layout owner's layout receipt names a different lot", out Failure);
			if (!ExactInt(Owner, KingdomArchitectureStamper.NextLayerProperty))
				return Fail("the layout owner's stage key is absent or dual-typed", out Failure);
			if (Owner.GetIntProperty(KingdomArchitectureStamper.NextLayerProperty) != CompleteStage)
				return Fail("this layout has not finished staging; a partial build has no realized "
					+ "state to compare", out Failure);
			return true;
		}

		private const string GalleryReceiptProperty = "r_TAF_ArchitectureGalleryReceipt";

		private static bool ExactInt(GameObject Item, string Property)
		{
			return Item.HasIntProperty(Property) && !Item.HasStringProperty(Property);
		}

		private static bool ExactText(GameObject Item, string Property)
		{
			return Item.HasStringProperty(Property) && !Item.HasIntProperty(Property);
		}

		private static bool TryOwnerFact(Zone Zone, GameObject Owner,
			KingdomArchitectureIntent Intent, int X1, int Y1, int Width, int Height,
			IList<KingdomRealizedObjectFact> Facts, out string Failure)
		{
			Failure = null;
			if (string.IsNullOrEmpty(Owner.IDIfAssigned))
				return Fail("the architecture owner carries no assigned identity", out Failure);
			Cell cell = Owner.CurrentCell;
			if (cell == null) return Fail("the architecture owner stands in no cell", out Failure);
			if (cell != Zone.GetCell(Intent.MainWorldX, Intent.MainWorldY))
				return Fail("the architecture owner has left its frozen main cell", out Failure);
			int x = cell.X - X1;
			int y = cell.Y - Y1;
			if (x < 0 || y < 0 || x >= Width || y >= Height)
				return Fail("the architecture owner stands outside its own lot", out Failure);
			KingdomRealizedObjectFact fact;
			if (!TryFact(Owner, x, y, true, out fact, out Failure)) return false;
			Facts.Add(fact);
			return true;
		}

		/// <summary>
		/// The one object this placement's receipt names, proved exact in identity, blueprint,
		/// marking, authority, and rotated world coordinate. Refuses rather than narrowing.
		/// </summary>
		private static bool TryReceiptedComponent(Zone Zone, GameObject Owner,
			KingdomArchitectureIntent Intent, ArchitectureLayoutSnapshot Snapshot, string Lot,
			ArchitecturePlacement Placement, IDictionary<string, GameObject> Receipted,
			out GameObject Item, out string Failure)
		{
			Item = null;
			Failure = null;
			if (Placement == null || string.IsNullOrEmpty(Placement.Slot))
				return Fail("the owner receipt names an unreadable placement", out Failure);
			string slot = Bounded(Placement.Slot);
			string stateKey = OutputStateProperty(Placement);
			string idKey = OutputIdProperty(Placement);
			// The per-slot receipt keys are read by exact type presence, never through a default
			// getter: a state key living under the string table would answer 0, which reads as an
			// unsettled slot rather than as the corruption it is.
			if (!Owner.HasIntProperty(stateKey) || Owner.HasStringProperty(stateKey)
				|| !Owner.HasStringProperty(idKey) || Owner.HasIntProperty(idKey))
				return Fail("the owner receipt for authored slot " + slot + " is absent or lives "
					+ "under the wrong type table", out Failure);
			if (Owner.GetIntProperty(stateKey) != SettledOutput)
				return Fail("authored slot " + slot + " is not settled in the owner receipt",
					out Failure);
			string id = Owner.GetStringProperty(idKey);
			if (string.IsNullOrEmpty(id))
				return Fail("authored slot " + slot + " has a settled receipt with no object id",
					out Failure);
			if (Receipted.ContainsKey(id))
				return Fail("two authored slots name the same object id; component authority is "
					+ "duplicated", out Failure);
			GameObject exact;
			if (KingdomConstruction.FindExactId(Zone, id, out exact) != KingdomPhysicalLookupState.Exact
				|| !GameObject.Validate(exact) || ReferenceEquals(exact, Owner))
				return Fail("authored slot " + slot + " names an object that is absent, ambiguous, "
					+ "or the owner itself", out Failure);
			if (!TryExactAuthority(exact, Intent, Lot, Placement, out Failure)) return false;
			int worldX;
			int worldY;
			if (!KingdomArchitectureRuntime.TryWorldPlacement(Snapshot, Intent.Rect, Placement,
				out worldX, out worldY, out Failure)) return false;
			if (exact.CurrentCell == null || exact.CurrentCell != Zone.GetCell(worldX, worldY))
				return Fail("authored slot " + slot + " has moved off its exact rotated coordinate",
					out Failure);
			Receipted[id] = exact;
			Item = exact;
			return true;
		}

		/// <summary>
		/// Nothing else in the zone may carry this lot's component or owner authority. An extra,
		/// copied, or partially marked object is a refusal: leaving it out would let a damaged lot
		/// quietly measure the same as an intact one.
		/// </summary>
		private static bool TryRefuseUnreceipted(Zone Zone, GameObject Owner,
			KingdomArchitectureIntent Intent, string Lot, int X1, int Y1, int Width, int Height,
			IDictionary<string, GameObject> Receipted, out string Failure)
		{
			Failure = null;
			List<GameObject> candidates = Zone.GetObjects() ?? new List<GameObject>();
			for (int i = 0; i < candidates.Count; i++)
			{
				GameObject item = candidates[i];
				if (!GameObject.Validate(item) || ReferenceEquals(item, Owner)) continue;
				string id = item.IDIfAssigned;
				if (!string.IsNullOrEmpty(id) && Receipted.ContainsKey(id)
					&& ReferenceEquals(Receipted[id], item)) continue;
				KingdomRealizedAuthorityVerdict verdict = KingdomRealizedAuthorityShape.Judge(
					Observe(item, Intent, Lot, X1, Y1, Width, Height));
				if (verdict == KingdomRealizedAuthorityVerdict.Unrelated) continue;
				return Fail(KingdomRealizedAuthorityShape.Describe(verdict), out Failure);
			}
			return true;
		}

		/// <summary>
		/// One object's raw claim on this layout, in booleans. The verdict is the pure census's, so
		/// every partial-marker case executes without a live zone.
		/// </summary>
		private static KingdomRealizedMarkerObservation Observe(GameObject Item,
			KingdomArchitectureIntent Intent, string Lot, int X1, int Y1, int Width, int Height)
		{
			Cell cell = Item.CurrentCell;
			int x = cell == null ? -1 : cell.X - X1;
			int y = cell == null ? -1 : cell.Y - Y1;
			return new KingdomRealizedMarkerObservation
			{
				ComponentMarker = Marked(Item),
				PlotIdString = Item.HasStringProperty(KingdomPlots.PlotIdProperty),
				PlotIdInt = Item.HasIntProperty(KingdomPlots.PlotIdProperty),
				PlotPart = Item.HasIntProperty(KingdomPlots.PlotPartProperty)
					|| Item.HasStringProperty(KingdomPlots.PlotPartProperty),
				ClaimsLot = Item.HasStringProperty(KingdomPlots.PlotIdProperty)
					&& string.Equals(Item.GetStringProperty(KingdomPlots.PlotIdProperty), Lot,
						StringComparison.Ordinal),
				InsideRect = cell != null && x >= 0 && y >= 0 && x < Width && y < Height,
				CarriesLayoutOwnerSchema =
					Item.HasIntProperty(KingdomArchitectureStamper.SchemaProperty)
					|| Item.HasStringProperty(KingdomArchitectureStamper.SchemaProperty),
				CarriesThisSnapshotHash = string.Equals(Item.GetStringProperty(
						KingdomArchitectureStamper.ComponentHashProperty), Intent.SnapshotHash,
					StringComparison.Ordinal)
			};
		}

		/// <summary>Any trace of the stamper's own component marking, complete or not.</summary>
		private static bool Marked(GameObject Item)
		{
			for (int i = 0; i < IntMarkers.Length; i++)
				if (Item.HasIntProperty(IntMarkers[i]) || Item.HasStringProperty(IntMarkers[i]))
					return true;
			for (int i = 0; i < TextMarkers.Length; i++)
				if (Item.HasStringProperty(TextMarkers[i]) || Item.HasIntProperty(TextMarkers[i]))
					return true;
			return false;
		}

		private static string OutputIdProperty(ArchitecturePlacement Placement)
		{
			return KingdomArchitectureStamper.OutputIdPrefix + PropertySlot(Placement.Slot);
		}

		private static string OutputStateProperty(ArchitecturePlacement Placement)
		{
			return KingdomArchitectureStamper.OutputStatePrefix + PropertySlot(Placement.Slot);
		}

		/// <summary>
		/// Mirrors the stamper's own per-slot property spelling. Pinned by a source contract test so
		/// a change on either side is a failure rather than a silent census of the wrong properties.
		/// </summary>
		private static string PropertySlot(string Slot)
		{
			return Slot == null ? "invalid" : Slot.Replace(':', '_');
		}

		private static string Bounded(string Value)
		{
			if (string.IsNullOrEmpty(Value)) return "(none)";
			return Value.Length <= 64 ? Value : Value.Substring(0, 64);
		}
	}
}
