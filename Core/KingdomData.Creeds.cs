using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomData
	{
		private static void LoadCreeds()
		{
			Dictionary<string, Action<XmlDataHelper>> handlers = null;
			handlers = new Dictionary<string, Action<XmlDataHelper>>
			{
				{
					"kingdomcreeds",
					delegate(XmlDataHelper xml)
					{
						KingdomXmlSchema.HandleRoot(xml, handlers, "KingdomCreeds");
					}
				},
				{ "creed", HandleCreed }
			};
			foreach (XmlDataHelper item in DataManager.YieldXMLStreamsWithRoot("KingdomCreeds"))
				item.HandleNodes(handlers);
		}

		private static void HandleCreed(XmlDataHelper xml)
		{
			KingdomCreedDraft declared = new KingdomCreedDraft
			{
				Name = xml.GetAttribute("Name"),
				Kind = xml.GetAttribute("Kind"),
				Theology = xml.GetAttribute("Theology")
			};
			if (!KingdomCreedKindRules.ValidName(declared.Name))
			{
				MetricsManager.LogError("ThousandAndFirst KingdomCreeds: creed needs a valid Name");
				xml.DoneWithElement();
				return;
			}
			declared.Name = declared.Name.Trim();
			int found = FindCreed(declared.Name);
			KingdomCreedDraft merged;
			if (!KingdomCreedKindRules.TryMerge(found < 0 ? null : _creedDrafts[found],
				declared, out merged, out string error)
				|| !KingdomCreedKindRules.TryParse(merged,
					out KingdomCreedDefinition parsed, out error))
			{
				MetricsManager.LogError("ThousandAndFirst KingdomCreeds: " + error);
				xml.DoneWithElement();
				return;
			}
			if (found < 0)
			{
				if (_creedDefinitions.Count >= KingdomCreedKindRules.MaxDefinitions)
					MetricsManager.LogError("ThousandAndFirst KingdomCreeds: too many definitions; "
						+ declared.Name + " was refused");
				else
				{
					_creedDrafts.Add(merged);
					_creedDefinitions.Add(parsed);
				}
			}
			else
			{
				_creedDrafts[found] = merged;
				_creedDefinitions[found] = parsed;
			}
			xml.DoneWithElement();
		}

		private static int FindCreed(string Name)
		{
			for (int i = 0; i < _creedDefinitions.Count; i++)
				if (string.Equals(_creedDefinitions[i].Name, Name,
					StringComparison.OrdinalIgnoreCase)) return i;
			return -1;
		}
	}
}
