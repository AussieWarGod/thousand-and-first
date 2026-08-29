using System;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	internal static partial class KingdomLab
	{
		/// <summary>
		/// Freezes D5 identity before any water, bits, or kept parts are reserved. Job fields use
		/// Qud's explicit named-field block: SerializationWriter.cs:2981-3007 writes names/count,
		/// and SerializationReader.cs:395-418 keeps unknown/missing names compatible.
		/// </summary>
		private static bool TryFreezeRulerLife(KingdomSystem System, GameObject Actor,
			string ExactRealmId, out KingdomRulerLifeSnapshot Snapshot)
		{
			Snapshot = null;
			if (KingdomBodyHistoryRulerLifeRuntime.TryReadCurrent(System, Actor,
				out KingdomRulerLifeSnapshot candidate, out string failure)
				&& string.Equals(candidate.RealmId, ExactRealmId, StringComparison.Ordinal))
			{
				Snapshot = candidate;
				return true;
			}
			Popup.Show("The commission cannot bind this exact ruler life ("
				+ (failure ?? "realm identity changed")
				+ "). Nothing was reserved or spent.");
			return false;
		}
	}
}
