using System.Collections.Generic;

namespace ThousandAndFirst
{
	public sealed partial class KingdomExperienceLedger
	{
		/// <summary>At most one finite First Feast proposal/practice per owned settlement.</summary>
		public List<KingdomFirstFeastReceipt> FirstFeasts =
			new List<KingdomFirstFeastReceipt>();
	}
}
