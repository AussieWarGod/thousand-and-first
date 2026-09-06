namespace ThousandAndFirst
{
	public partial class KingdomSystem
	{
		/// <summary>Singular write-ahead authority for destructive resident departure. A second
		/// operation cannot begin until this exact body finishes or rolls back.</summary>
		public KingdomResidentDepartureOperation ResidentDeparture =
			new KingdomResidentDepartureOperation();

		/// <summary>Realm-spanning unread capacity warnings. This named field is not seat state:
		/// reset and identity changes retain every row until its own settlement reads it.</summary>
		public string ResidentDepartureCapacityWarnings = KingdomResidentDepartureCapacityArchive.None;
	}
}
