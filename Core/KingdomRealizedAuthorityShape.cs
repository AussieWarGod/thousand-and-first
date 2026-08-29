namespace ThousandAndFirst
{
	/// <summary>
	/// What one object in the zone is claiming about a layout's authority, in booleans.
	/// <para>
	/// Every field is raw presence or a raw relationship. Nothing here is a value read through a
	/// default getter: a plot custody key living under the int table answers 0 to a text read, which
	/// is indistinguishable from an object that never had one.
	/// </para>
	/// </summary>
	public sealed class KingdomRealizedMarkerObservation
	{
		/// <summary>Any of the stamper's own component markers, under either durable table.</summary>
		public bool ComponentMarker;

		/// <summary>Plot custody id present under the string table, where it belongs.</summary>
		public bool PlotIdString;

		/// <summary>Plot custody id present under the int table. Alone or as well, unreadable.</summary>
		public bool PlotIdInt;

		/// <summary>Plot-part custody, the marker the stamper writes on every non-relic component.</summary>
		public bool PlotPart;

		/// <summary>The string-table plot id equals the lot being measured.</summary>
		public bool ClaimsLot;

		/// <summary>The object stands inside the measured lot rect.</summary>
		public bool InsideRect;

		/// <summary>The object reads as a layout owner in its own right.</summary>
		public bool CarriesLayoutOwnerSchema;

		/// <summary>The object carries the measured lot's exact snapshot hash.</summary>
		public bool CarriesThisSnapshotHash;
	}

	/// <summary>How an object relates to the layout being measured.</summary>
	public enum KingdomRealizedAuthorityVerdict : byte
	{
		/// <summary>Not this layout's business. Excluded from the measurement, not refused.</summary>
		Unrelated = 0,

		/// <summary>A second architecture owner carrying this lot id.</summary>
		SecondOwner = 1,

		/// <summary>Plot custody under the wrong table, or under two. Never resolved.</summary>
		UnreadableCustody = 2,

		/// <summary>Claims this lot's authority but no owner receipt names it.</summary>
		Unreceipted = 3,

		/// <summary>Carries this lot's snapshot authority while belonging to another lot.</summary>
		CopiedAuthority = 4
	}

	/// <summary>Whether a component was carried across a tier upgrade, and whether that reads.</summary>
	public enum KingdomRealizedCarriedShape : byte
	{
		/// <summary>No carried key. A freshly stamped component that was never retained.</summary>
		Absent = 0,

		/// <summary>Exactly one int key holding 1: retained across a same-lot upgrade.</summary>
		Carried = 1,

		/// <summary>Any other shape. Never resolved in either direction.</summary>
		Invalid = 2
	}

	/// <summary>
	/// The operative census, kept pure so its mutants execute.
	/// <para>
	/// A partial marker set is the case that matters. The stamper writes plot-part custody as part of
	/// stamping a component, so an object holding only that custody inside the lot rect is either a
	/// half-stamped component or foreign matter standing on the finished build. Either way the
	/// realized state is not the frozen one, and measuring around it would let a damaged lot match an
	/// intact one. Ordinary plot objects elsewhere on the same plot are outside the rect and stay
	/// excluded, which is why the lot relationship is part of the predicate rather than a comment.
	/// </para>
	/// </summary>
	public static class KingdomRealizedAuthorityShape
	{
		/// <summary>
		/// Whether this object is claiming to be part of the measured layout at all, judged BEFORE
		/// any value relationship.
		/// <para>
		/// The lot relationship cannot gate this. An object whose custody key lives under the int
		/// table has no readable lot at all, so asking whether it claims THIS lot answers no for the
		/// same reason it answers no for an ordinary bystander - and the claim then escapes the
		/// census entirely. Raw plot-part authority inside the rect is a claim; whose lot it is comes
		/// afterwards.
		/// </para>
		/// </summary>
		public static bool ClaimsComponentAuthority(KingdomRealizedMarkerObservation Observed)
		{
			if (Observed == null) return false;
			return Observed.ComponentMarker || (Observed.InsideRect && Observed.PlotPart);
		}

		/// <summary>
		/// The verdict for an object the owner's receipts did NOT name. Anything but Unrelated is a
		/// refusal; narrowing the measured world is what this exists to prevent.
		/// </summary>
		public static KingdomRealizedAuthorityVerdict Judge(
			KingdomRealizedMarkerObservation Observed)
		{
			if (Observed == null) return KingdomRealizedAuthorityVerdict.UnreadableCustody;
			if (Observed.ClaimsLot && Observed.CarriesLayoutOwnerSchema)
				return KingdomRealizedAuthorityVerdict.SecondOwner;
			if (!ClaimsComponentAuthority(Observed))
				return KingdomRealizedAuthorityVerdict.Unrelated;
			// A claim with missing, int-typed, or dual-typed custody has no readable lot, so it can
			// never be judged by value and may never be dismissed for belonging to another one.
			if (Observed.PlotIdInt || !Observed.PlotIdString)
				return KingdomRealizedAuthorityVerdict.UnreadableCustody;
			if (Observed.ClaimsLot) return KingdomRealizedAuthorityVerdict.Unreceipted;
			if (Observed.ComponentMarker && Observed.CarriesThisSnapshotHash)
				return KingdomRealizedAuthorityVerdict.CopiedAuthority;
			return KingdomRealizedAuthorityVerdict.Unrelated;
		}

		/// <summary>
		/// The carried marker's shape.
		/// <para>
		/// A carried component is LAWFUL, not corrupt. The shipped same-lot upgrade path restamps a
		/// retained placement and then writes this marker, and no completion path removes it, so a
		/// completed upgraded building carries it forever. Refusing it would make every upgraded
		/// building permanently uncapturable as ordinary-play evidence.
		/// </para>
		/// <para>
		/// It is provenance VALIDITY, not cross-path identity. A fresh gallery realization and a
		/// lawful upgraded one with identical final placements describe the same realized result, so
		/// the marker is proved here and never enters the digest.
		/// </para>
		/// </summary>
		public static KingdomRealizedCarriedShape Carried(bool HasInt, int Value, bool HasText)
		{
			if (HasText) return KingdomRealizedCarriedShape.Invalid;
			if (!HasInt) return KingdomRealizedCarriedShape.Absent;
			return Value == 1
				? KingdomRealizedCarriedShape.Carried
				: KingdomRealizedCarriedShape.Invalid;
		}

		/// <summary>The operator-facing reason, so a refusal names what it saw.</summary>
		public static string Describe(KingdomRealizedAuthorityVerdict Verdict)
		{
			switch (Verdict)
			{
				case KingdomRealizedAuthorityVerdict.SecondOwner:
					return "a second architecture owner carries this lot id";
				case KingdomRealizedAuthorityVerdict.UnreadableCustody:
					return "an object claiming this layout's authority carries its plot custody key "
						+ "under the wrong durable type table";
				case KingdomRealizedAuthorityVerdict.Unreceipted:
					return "an object carrying this lot's component or plot-part authority inside the "
						+ "lot rect is named by no owner receipt";
				case KingdomRealizedAuthorityVerdict.CopiedAuthority:
					return "a component outside this lot carries this lot's snapshot authority";
				default:
					return null;
			}
		}
	}
}
