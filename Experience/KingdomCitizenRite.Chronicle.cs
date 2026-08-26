using Qud.API;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomCitizenRite
	{
		/// <summary>
		/// Files what the roads are saying about the city as a secret the founder can trade.
		/// <para>
		/// <b>W5's named remainder, and the narrowest honest slice of it.</b> This builds no trade
		/// system, no new conversation, and no new interest table. It writes ONE
		/// <c>JournalObservation</c> per settlement pass and hands it to vanilla, which already
		/// owns every part of what happens next: <c>IWaterRitualSecretPart.ShuffleNotes</c> walks
		/// <c>JournalAPI.Observations</c> into the ritual's ball bag,
		/// <c>Faction.GetBuySecretWeight</c> asks each faction's own
		/// <c>&lt;interest Tags="..."/&gt;</c> table whether it wants to hear it, and
		/// <c>WaterRitualSellSecret.SellEntry</c> pays the reputation and stamps the entry
		/// <i>shared with</i> whoever bought it
		/// (<c>D/XRL/World/Conversations/Parts/IWaterRitualSecretPart.cs</c>,
		/// <c>D/XRL/World/Faction.cs:1197-1280,1533</c>,
		/// <c>D/XRL/World/Conversations/Parts/WaterRitualSellSecret.cs</c>).
		/// </para>
		/// <para>
		/// <b>It is sellable and never buyable</b>, which falls out of vanilla's own rule rather
		/// than being enforced: <c>CanSell</c> is <c>Tradable &amp;&amp; Revealed</c> and
		/// <c>CanBuy</c> is <c>Tradable &amp;&amp; !Revealed</c>
		/// (<c>D/Qud/API/IBaseJournalEntry.cs:193-206</c>). A founder knows their own city's
		/// history the day it happens, so the entry is filed revealed — you can tell the world about
		/// your city, and nobody can sell it back to you.
		/// </para>
		/// <para>
		/// <b>Bounded and idempotent.</b> One entry per pass, the newest telling only; the id is
		/// derived from the realm and the words, so re-filing after a reload, a seat swap or a
		/// register trim is a no-op inside <c>JournalAPI.AddObservation</c>, which refuses an id it
		/// already holds. There is no cursor to fall out of step with the two-hundred-entry ring.
		/// </para>
		/// <para>
		/// Under the chronicle's own option, because it is the chronicle leaving the city.
		/// </para>
		/// </summary>
		private static void Chronicle(KingdomSystem System)
		{
			if (!System.Founded || System.OutsiderEntries == null || System.OutsiderEntries.Count == 0
				|| Options.GetOption("r_TAF_OptionChronicle") == "No")
			{
				return;
			}
			string id;
			string text;
			if (!KingdomCitizenRiteRules.TryTradableSecret(
					System.CurrentRealmId,
					System.OutsiderEntries[System.OutsiderEntries.Count - 1],
					out id,
					out text)
				|| JournalAPI.GetObservation(id) != null)
			{
				return;
			}
			JournalAPI.AddObservation(
				text,
				id,
				KingdomCitizenRiteRules.SecretCategory,
				id,
				KingdomCitizenRiteRules.SecretTags(),
				revealed: true,
				-1L);
			KingdomLog.Log("citizen rite: filed a telling of " + System.KingdomDisplayName + " as tradable gossip (" + id + ")");
		}

	}
}
