namespace ThousandAndFirst
{
	public sealed class KingdomWitnessWorkPlan
	{
		public readonly string WorkId;
		public readonly string SourceDigest;
		public readonly string ObjectId;
		public readonly string ZoneId;
		public readonly string ConstructionReceiptId;
		public readonly int X;
		public readonly int Y;
		public readonly long Tick;
		public readonly string CarrierReceiptId;
		public readonly string Description;

		internal KingdomWitnessWorkPlan(KingdomWitnessWorkReceipt Row, long Tick)
		{
			WorkId = Row.WorkId; SourceDigest = Row.Source.SnapshotDigest;
			ObjectId = Row.CarrierObjectId; ZoneId = Row.CarrierZoneId;
			ConstructionReceiptId = Row.CarrierConstructionReceiptId;
			X = Row.CarrierX; Y = Row.CarrierY; this.Tick = Tick;
			CarrierReceiptId = Row.CarrierReceiptId; Description = Row.Description;
		}

		public string Disclosure(KingdomWitnessWorkSource Source)
		{
			return "Event: " + Source.EventId + "\nAdapter: " + Source.EventKind
				+ "\nEvent date: civic tick " + Source.ClosedTick
				+ "\nMaker: " + Source.MakerName + " (resident "
				+ Source.MakerResidentId + ")\nAccount: " + Source.EventText
				+ "\nSurface: " + ObjectId + " at " + ZoneId + " (" + X + "," + Y
				+ ")\nConstruction proof: " + ConstructionReceiptId
				+ "\n\nThe text is immutable. The surface remains fixed, nonportable, empty, "
				+ "and worth 0. No item, inventory, custody, Journal, or economy record is created.";
		}
	}
}
