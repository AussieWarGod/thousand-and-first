namespace ThousandAndFirst
{
	public partial class KingdomSystem
	{
		/// <summary>Singular write-ahead authority for destructive resident departure. A second
		/// operation cannot begin until this exact body finishes or rolls back.</summary>
		public KingdomResidentDepartureOperation ResidentDeparture =
			new KingdomResidentDepartureOperation();
	}
}
