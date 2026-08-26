using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// Frozen commitment shown before a purposeful building is debited. It binds one exact delivered
	/// cargo object and its terminal consignment receipt to one distinct site reading.
	/// </summary>
	public sealed class KingdomPurposeCommitment
	{
		public const int Schema = 1;
		public string Manifest;
		public string ConsignmentId;
		public string CargoItemId;
		public string SiteProof;
		public string SpecialistId;
		public string SpecialistName;
	}
}
