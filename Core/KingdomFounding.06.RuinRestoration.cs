using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using XRL;
using XRL.Language;
using XRL.Rules;
using XRL.World;
using XRL.World.ZoneBuilders;

namespace ThousandAndFirst
{
	public static partial class KingdomFounding
	{
		/// <summary>
		/// Credits a ruin founding with whatever of the ground's own history still stands. Every
		/// object already in the zone that carries a part the settlement already knows how to use
		/// for free &mdash; a bed for housing, a shrine for petitions &mdash; is stamped
		/// <c>KingdomBuilt</c>, the exact marker <c>r_KingdomScaffold.Complete</c> stamps on
		/// anything it finishes building, so <c>KingdomSurvey</c>, <c>KingdomCommission</c>, and
		/// <c>KingdomPetitions</c> count it without any change to those files.
		/// <para>
		/// Nothing is moved, replaced, or destroyed here &mdash; only recognised. Binding a ruin's
		/// standing furniture to the settlement's fate the moment the founder pours the rite over
		/// it is the explicit designation the protection law (STANDARDS 7) asks for: the founder
		/// chose this exact ground, once, deliberately, and the chronicle says so.
		/// </para>
		/// </summary>
		private const string RuinRestorationTransactionProperty =
			"r_TAF_RuinRestorationTransaction_v1";

		/// <summary>Second-founding restoration receipt. Each eligible object retains the
		/// exact transaction before KingdomBuilt changes, so interruption can recount and
		/// finish the same set without losing already-stamped structures.</summary>
		internal static bool TryRestoreRuinStructures(Zone Site, string TransactionId,
			out int Restored)
		{
			Restored = 0;
			if (Site == null || !KingdomIdentityRules.IsFoundingTransaction(TransactionId))
				return false;
			try
			{
				List<GameObject> objects = Site.GetObjects();
				if (objects == null || objects.Count > 65536) return false;
				for (int i = 0; i < objects.Count; i++)
				{
					GameObject item = objects[i];
					if (!GameObject.Validate(item)) return false;
					bool eligible = item.HasPart("Bed") || item.HasPart("Shrine");
					string owner = item.GetStringProperty(
						RuinRestorationTransactionProperty, null);
					if (!string.IsNullOrEmpty(owner) && owner != TransactionId)
					{
						// Completed furniture from an older realm is ordinary prebuilt ground for
						// this rite. Only a foreign incomplete or malformed marker blocks reuse.
						if (eligible && item.GetIntProperty("KingdomBuilt") == 1) continue;
						return false;
					}
					if (owner == TransactionId)
					{
						if (!eligible) return false;
						if (item.GetIntProperty("KingdomBuilt") != 1)
							item.SetIntProperty("KingdomBuilt", 1);
						if (item.GetIntProperty("KingdomBuilt") != 1) return false;
						continue;
					}
					if (!eligible || item.GetIntProperty("KingdomBuilt") == 1) continue;
					item.SetStringProperty(RuinRestorationTransactionProperty, TransactionId);
					if (item.GetStringProperty(RuinRestorationTransactionProperty, null) !=
						TransactionId) return false;
					item.SetIntProperty("KingdomBuilt", 1);
					if (item.GetIntProperty("KingdomBuilt") != 1) return false;
				}
				for (int i = 0; i < objects.Count; i++)
				{
					GameObject item = objects[i];
					if (item.GetStringProperty(RuinRestorationTransactionProperty, null) !=
						TransactionId) continue;
					if (item.GetIntProperty("KingdomBuilt") != 1 ||
						(!item.HasPart("Bed") && !item.HasPart("Shrine"))) return false;
					if (Restored == int.MaxValue) return false;
					Restored++;
				}
				return true;
			}
			catch
			{
				Restored = 0;
				return false;
			}
		}

	}
}
