using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Host boundary for the pure public-registry schema verdict.</summary>
	internal static class KingdomXmlSchema
	{
		internal static void HandleRoot(XmlDataHelper Xml,
			Dictionary<string, Action<XmlDataHelper>> Handlers, string Registry)
		{
			int version;
			string declared = Xml.GetAttribute("Schema");
			KingdomXmlSchemaVerdict verdict = KingdomXmlSchemaRules.Judge(declared, out version);
			if (KingdomXmlSchemaRules.IsReadable(verdict))
			{
				Xml.HandleNodes(Handlers);
				return;
			}
			string detail = verdict == KingdomXmlSchemaVerdict.Malformed
				? "malformed Schema attribute"
				: "unsupported Schema " + version + "; this build reads schema "
					+ KingdomXmlSchemaRules.CurrentVersion;
			MetricsManager.LogError("ThousandAndFirst " + Registry + ": " + detail
				+ "; this stream was ignored");
			Xml.HandleNodes(new Dictionary<string, Action<XmlDataHelper>>(),
				delegate(XmlDataHelper Child) { Child.DoneWithElement(); });
		}
	}
}
