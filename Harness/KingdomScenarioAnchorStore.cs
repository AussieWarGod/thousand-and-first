using System;
using System.Collections.Generic;

using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// The curated ordinary-play anchor evidence, held separately from the scenario roster.
	/// <para>
	/// Authority constraint: rows here are written by a reviewer from a state ordinary play
	/// actually reached, and are never produced by a scenario run. The harness reads this store; it
	/// has no path that writes one. An empty store is the correct state until a reviewer captures
	/// an anchor, and every scenario verdict is ineligible until then.
	/// </para>
	/// </summary>
	internal static class KingdomScenarioAnchorStore
	{
		internal const string RegistryRoot = "KingdomScenarioAnchors";

		private static List<KingdomScenarioAnchorEvidence> _anchors;
		private static List<string> _findings;

		internal static IList<KingdomScenarioAnchorEvidence> Anchors
		{
			get { EnsureLoaded(); return _anchors; }
		}

		internal static IList<string> Findings
		{
			get { EnsureLoaded(); return _findings; }
		}

		/// <summary>
		/// The one evidence record for an anchor id and authority class, or null. Null is a normal
		/// answer, not a fault: it makes a verdict ineligible rather than green.
		/// </summary>
		internal static KingdomScenarioAnchorEvidence Find(string AnchorId, string AuthorityClass)
		{
			EnsureLoaded();
			if (string.IsNullOrEmpty(AnchorId)) return null;
			KingdomScenarioAnchorEvidence found = null;
			for (int i = 0; i < _anchors.Count; i++)
			{
				KingdomScenarioAnchorEvidence row = _anchors[i];
				if (!string.Equals(row.AnchorId, AnchorId, StringComparison.Ordinal)
					|| !string.Equals(row.AuthorityClass, AuthorityClass, StringComparison.Ordinal))
					continue;
				// An ambiguous store must not silently pick one; two rows for one anchor is a fault.
				if (found != null) return null;
				found = row;
			}
			return found;
		}

		internal static void Invalidate()
		{
			_anchors = null;
			_findings = null;
		}

		private static void EnsureLoaded()
		{
			if (_anchors != null) return;
			_anchors = new List<KingdomScenarioAnchorEvidence>();
			_findings = new List<string>();
			Dictionary<string, Action<XmlDataHelper>> handlers = null;
			handlers = new Dictionary<string, Action<XmlDataHelper>>
			{
				{
					"kingdomscenarioanchors",
					delegate(XmlDataHelper xml)
					{
						KingdomXmlSchema.HandleRoot(xml, handlers, RegistryRoot);
					}
				},
				{ "anchor", HandleAnchor }
			};
			foreach (XmlDataHelper item in DataManager.YieldXMLStreamsWithRoot(RegistryRoot))
			{
				item.HandleNodes(handlers);
			}
			HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < _anchors.Count; i++)
			{
				KingdomScenarioAnchorEvidence row = _anchors[i];
				string identity = row.AnchorId + "|" + row.AuthorityClass;
				if (!seen.Add(identity))
					_findings.Add("duplicate anchor evidence for " + identity);
			}
			for (int i = 0; i < _findings.Count; i++)
				KingdomLog.Log("KingdomScenarioAnchors: " + _findings[i]);
		}

		private static void HandleAnchor(XmlDataHelper Xml)
		{
			string anchorId = Trim(Xml.GetAttribute("AnchorId"));
			string authority = Trim(Xml.GetAttribute("AuthorityClass"));
			string verbs = Trim(Xml.GetAttribute("Verbs"));
			string keySet = Trim(Xml.GetAttribute("KeySetDigest"));
			string definition = Trim(Xml.GetAttribute("DefinitionDigest"));
			string plan = Trim(Xml.GetAttribute("PlanDigest"));
			string mod = Trim(Xml.GetAttribute("ModVersion"));
			string core = Trim(Xml.GetAttribute("QudCoreVersion"));
			string reached = Trim(Xml.GetAttribute("Reached"));
			// Only the exact word admits an ordinary-play anchor; anything else stays Unknown and
			// the signing law refuses it. A typo can never promote curated evidence.
			KingdomScenarioAnchorRules.Provenance provenance =
				string.Equals(reached, "ordinary-play", StringComparison.Ordinal)
					? KingdomScenarioAnchorRules.Provenance.OrdinaryPlay
					: KingdomScenarioAnchorRules.Provenance.Unknown;
			if (provenance != KingdomScenarioAnchorRules.Provenance.OrdinaryPlay)
				_findings.Add((anchorId ?? "(unkeyed)")
					+ " does not declare Reached=\"ordinary-play\"");
			_anchors.Add(new KingdomScenarioAnchorEvidence
			{
				AnchorId = anchorId,
				AuthorityClass = authority,
				Verbs = verbs,
				KeySetDigest = keySet,
				DefinitionDigest = definition,
				PlanDigest = plan,
				ModVersion = mod,
				QudCoreVersion = core,
				Reached = provenance
			});
			Xml.DoneWithElement();
		}

		private static string Trim(string Value)
		{
			return string.IsNullOrEmpty(Value) ? Value : Value.Trim();
		}
	}
}
