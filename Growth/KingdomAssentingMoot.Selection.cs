using System;
using System.Collections.Generic;
using XRL.UI;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomAssentingMoot
	{
		private sealed class ResidentChoice
		{
			internal int Id;
			internal string Name;
		}

		private static bool TryChoose(KingdomCityBook Book,
			KingdomAssentingMootReceipt Receipt, KingdomAssentingMootRole Role,
			bool Add, out int ResidentId, out string ResidentName)
		{
			ResidentId = 0;
			ResidentName = null;
			List<ResidentChoice> rows = new List<ResidentChoice>();
			Dictionary<string, int> names = new Dictionary<string, int>(StringComparer.Ordinal);
			if (Add)
			{
				List<KingdomResidentRow> residents = KingdomResidents.RollRows(Book);
				for (int i = 0; i < residents.Count; i++)
				{
					KingdomResidentRow resident = residents[i];
					int id = resident.ResidentId;
					if (resident.Standing != KingdomResidentStanding.Resident
						|| KingdomAssentingMootRules.Contains(Receipt, Role, id)) continue;
					AddChoice(rows, names, id, resident.Name);
				}
			}
			else
			{
				List<int> ids = Role == KingdomAssentingMootRole.Assent
					? Receipt.AssentResidentIds : Receipt.ExemptResidentIds;
				List<string> memberNames = Role == KingdomAssentingMootRole.Assent
					? Receipt.AssentResidentNames : Receipt.ExemptResidentNames;
				for (int i = 0; i < ids.Count; i++)
					AddChoice(rows, names, ids[i], memberNames[i]);
			}
			if (rows.Count == 0)
			{
				Popup.Show(Add ? "No other standing named resident is eligible."
					: "No named resident holds that moot role.");
				return false;
			}
			string[] options = new string[rows.Count];
			for (int i = 0; i < rows.Count; i++)
				options[i] = KingdomPresentation.Rich(rows[i].Name)
					+ (names[rows[i].Name] > 1 ? " {{K|[roll " + rows[i].Id + "]}}" : "");
			int pick = Popup.PickOption(Title: Add ? "Choose a named resident"
				: "Choose a recorded member", Intro: "No name substitutes for another exact roll.",
				Options: options, AllowEscape: true);
			if (pick < 0 || pick >= rows.Count) return false;
			ResidentId = rows[pick].Id;
			ResidentName = rows[pick].Name;
			return true;
		}

		private static void AddChoice(List<ResidentChoice> Rows,
			Dictionary<string, int> Names, int Id, string Name)
		{
			string name = Name ?? "";
			Rows.Add(new ResidentChoice { Id = Id, Name = name });
			Names[name] = Names.TryGetValue(name, out int count) ? count + 1 : 1;
		}
	}
}
