using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// The heart's own voice at a raising. Every rung of the heart closes through the ordinary
	/// raising ceremony first &mdash; the crew gathers, a measure of water is shared, whoever
	/// stood there is named (<c>KingdomCeremony.OnBuildingRaised</c>) &mdash; and then this adds
	/// the one sentence that ceremony cannot: what the GROUND has become.
	/// <para>
	/// The heart grows by authored building work, not one universal construction trick. A rung may
	/// retain a useful layer, renovate cramped rooms, add into newly proved ground, or combine both.
	/// The first basin and every live custody record survive. The sentence each rung earns therefore
	/// names what the ground has become, in the register Qud's great places use: old authority held
	/// inside a place repeatedly adapted to new lives.
	/// </para>
	/// <para>
	/// Deliberately NOT in <c>Experience/KingdomCeremony.cs</c>: that file owns the one grammar
	/// every building rises by, and the heart must not fork it. This is an addition on top of it,
	/// and it fires only for the designs on the ladder.
	/// </para>
	/// </summary>
	public static class KingdomCeremonyHeart
	{
		/// <summary>
		/// Closes one rung of the heart: stamps which rung now stands on the rite ground, so the
		/// layout grammar can start drawing the city back onto it, and tells the founder and both
		/// registers what the ground has become.
		/// </summary>
		/// <param name="System">The realm. Null or unfounded is a no-op.</param>
		/// <param name="Z">The zone the rung stands in, whose rite ground the rung is stamped
		/// against. Null stamps nothing and still speaks.</param>
		/// <param name="Key">Registry key of the design just finished. Anything off the heart's
		/// ladder is silent, which is all but four designs.</param>
		/// <param name="Heart">Whether the plot that finished was the heart's own. A third-party
		/// file re-declaring a rung key elsewhere in the city raises an ordinary building, and
		/// gets the ordinary ceremony.</param>
		public static void OnRungRaised(KingdomSystem System, Zone Z, string Key, bool Heart)
		{
			if (System == null || !System.Founded || !Heart)
			{
				return;
			}
			int rung = KingdomPlotRules.HeartRungOf(Key);
			if (rung < 1)
			{
				return;
			}
			KingdomSystem.Guard("ceremony: the heart", delegate
			{
				if (Z != null)
				{
					Z.SetZoneProperty(KingdomPlots.HeartRungProperty, rung.ToString());
				}
				string realm = KingdomPresentation.Rich(System.KingdomDisplayName);
				string line = KingdomCeremonyHeartRules.ChronicleLine(rung, realm);
				KingdomChronicle.Record(System, line, Accomplishment: KingdomCeremonyHeartRules.IsAccomplishment(rung));
				System.Ledger.Note("{{G|" + KingdomCeremonyHeartRules.MessageLine(rung, realm) + "}}");
				MessageQueue.AddPlayerMessage("{{G|" + KingdomCeremonyHeartRules.MessageLine(rung, realm) + "}}");
				KingdomLog.Log("heart rung raised: " + rung + " (" + Key + ")");
			});
		}
	}
}
