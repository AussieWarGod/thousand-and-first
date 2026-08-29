using System.Collections.Generic;
using XRL;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomWaterRite
	{
		// ==================================================================================
		// The stamp a refusal leaves
		// ==================================================================================

		private static void WriteStamp(GameObject Resident, WaterRiteStamp Stamp)
		{
			Resident.SetIntProperty(StampAnswerProperty, (int)Stamp.Answer + 1);
			Resident.SetIntProperty(StampHostilityProperty, Stamp.Hostility);
			Resident.SetIntProperty(StampShrineProperty, Stamp.RivalShrine ? 1 : 0);
			Resident.SetIntProperty(StampAbsoluteProperty, Stamp.Absolute ? 1 : 0);
			Resident.SetIntProperty(StampNeededProperty, Stamp.NeededDays);
			Resident.SetStringProperty(StampCreedProperty, Stamp.RealmCreed ?? "");
		}

		private static bool TryReadStamp(GameObject Resident, out WaterRiteStamp Stamp)
		{
			int answer = Resident.GetIntProperty(StampAnswerProperty);
			if (answer <= 0)
			{
				Stamp = default(WaterRiteStamp);
				return false;
			}
			Stamp = new WaterRiteStamp(
				(WaterRiteAnswer)(answer - 1),
				Resident.GetIntProperty(StampHostilityProperty),
				Resident.GetIntProperty(StampShrineProperty) == 1,
				Resident.GetIntProperty(StampAbsoluteProperty) == 1,
				Resident.GetIntProperty(StampNeededProperty),
				Resident.GetStringProperty(StampCreedProperty));
			return true;
		}

		private static void ClearStamp(GameObject Resident)
		{
			Resident.SetIntProperty(StampAnswerProperty, 0, RemoveIfZero: true);
			Resident.SetIntProperty(StampHostilityProperty, 0, RemoveIfZero: true);
			Resident.SetIntProperty(StampShrineProperty, 0, RemoveIfZero: true);
			Resident.SetIntProperty(StampAbsoluteProperty, 0, RemoveIfZero: true);
			Resident.SetIntProperty(StampNeededProperty, 0, RemoveIfZero: true);
			Resident.SetStringProperty(StampCreedProperty, null, RemoveIfNull: true);
		}

		// ==================================================================================
		// People
		// ==================================================================================

		// Everyone the rite could be put to: a citizen of this settlement whom the roll carries
		// under a name, because water is shared with a person and a person has a name. Sorted by
		// that name, so the same settlement always offers the same list in the same order.
		private static List<GameObject> CandidatesIn(KingdomSystem System, Zone Z)
		{
			List<GameObject> people = new List<GameObject>();
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Z))
			{
				if (KingdomCitizenship.BelongsTo(System, item)
					&& !string.IsNullOrEmpty(item.GetStringProperty("KingdomName")))
				{
					people.Add(item);
				}
			}
			people.Sort((a, b) => string.CompareOrdinal(a.GetStringProperty("KingdomName"), b.GetStringProperty("KingdomName")));
			return people;
		}

		private static string NameOf(GameObject Resident)
		{
			if (Resident == null)
			{
				return "";
			}
			string name = Resident.GetStringProperty("KingdomName");
			return string.IsNullOrEmpty(name) ? Resident.BaseDisplayNameStripped : name;
		}
	}
}
