using System;
using System.Collections.Generic;

using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Authored scenario roster, loaded through the shared registry law so a third party could
	/// extend it by shipping a matching root element. Load-time only: nothing here is persisted.
	/// </summary>
	internal static class KingdomScenarioRegistry
	{
		private static List<KingdomScenarioDefinition> _scenarios;
		private static List<string> _findings;
		private static string _digest;

		internal static IList<KingdomScenarioDefinition> Scenarios
		{
			get { EnsureLoaded(); return _scenarios; }
		}

		/// <summary>Structural faults found at load. A non-empty list makes the roster unusable.</summary>
		internal static IList<string> Findings
		{
			get { EnsureLoaded(); return _findings; }
		}

		/// <summary>Digest over the whole authored roster, recorded in every stamp.</summary>
		internal static string Digest
		{
			get { EnsureLoaded(); return _digest; }
		}

		internal static bool Healthy
		{
			get { EnsureLoaded(); return _findings.Count == 0 && _scenarios.Count > 0; }
		}

		internal static KingdomScenarioDefinition Find(string Key)
		{
			EnsureLoaded();
			for (int i = 0; i < _scenarios.Count; i++)
				if (string.Equals(_scenarios[i].Key, Key, StringComparison.Ordinal))
					return _scenarios[i];
			return null;
		}

		/// <summary>Re-read on demand; the roster is data and never survives in a save.</summary>
		internal static void Invalidate()
		{
			_scenarios = null;
			_findings = null;
			_digest = null;
		}

		private static void EnsureLoaded()
		{
			if (_scenarios != null) return;
			_scenarios = new List<KingdomScenarioDefinition>();
			_findings = new List<string>();
			Dictionary<string, Action<XmlDataHelper>> handlers = null;
			handlers = new Dictionary<string, Action<XmlDataHelper>>
			{
				{
					"kingdomscenarios",
					delegate(XmlDataHelper xml)
					{
						KingdomXmlSchema.HandleRoot(xml, handlers,
							KingdomScenarioHarness.RegistryRoot);
					}
				},
				{ "scenario", HandleScenario }
			};
			foreach (XmlDataHelper item in DataManager.YieldXMLStreamsWithRoot(
				KingdomScenarioHarness.RegistryRoot))
			{
				item.HandleNodes(handlers);
			}
			_findings.AddRange(KingdomScenarioRules.Validate(_scenarios));
			_digest = KingdomScenarioDigests.Registry(_scenarios);
			if (_digest == null) _findings.Add("the scenario roster has no canonical digest");
			for (int i = 0; i < _findings.Count; i++)
				KingdomLog.Log("KingdomScenarios: " + _findings[i]);
		}

		private static void HandleScenario(XmlDataHelper Xml)
		{
			// Every attribute is read unconditionally: the engine records which attributes a pass
			// asked for and warns about the rest, so a skipped read makes the loader complain.
			string key = Xml.GetAttribute("Key");
			string family = Xml.GetAttribute("Family");
			string authority = Xml.GetAttribute("AuthorityClass");
			string synthetic = Xml.GetAttribute("Synthetic");
			string anchor = Xml.GetAttribute("AnchorId");
			string displayName = Xml.GetAttribute("DisplayName");
			KingdomScenarioDefinition definition = new KingdomScenarioDefinition
			{
				Key = Trim(key),
				Family = Trim(family),
				AuthorityClass = Trim(authority),
				// Kept verbatim: Validate requires exactly "true" or "false", so a typo is a
				// finding rather than a silent downgrade to acceptance-eligible.
				SyntheticRaw = Trim(synthetic),
				AnchorId = string.IsNullOrEmpty(Trim(anchor)) ? null : Trim(anchor),
				// Raw, not Bounded: truncating here silently repaired oversize authored text and
				// made the validator's own length guard unreachable. Bounding belongs in operator
				// error rendering, never between the author and the rule that judges them.
				DisplayName = Trim(displayName)
			};
			Xml.HandleNodes(new Dictionary<string, Action<XmlDataHelper>>
			{
				{
					"description",
					delegate(XmlDataHelper child)
					{
						definition.Description = Trim(child.GetAttribute("Text"));
						child.DoneWithElement();
					}
				},
				{ "param", delegate(XmlDataHelper child) { HandleParam(definition, child); } },
				{ "step", delegate(XmlDataHelper child) { HandleStep(definition, child); } }
			});
			_scenarios.Add(definition);
		}

		private static void HandleParam(KingdomScenarioDefinition Definition, XmlDataHelper Xml)
		{
			string name = Trim(Xml.GetAttribute("Name"));
			string domain = Trim(Xml.GetAttribute("Domain"));
			KingdomScenarioParameter parameter = new KingdomScenarioParameter { Name = name };
			if (!string.IsNullOrEmpty(domain))
				// Empty members are KEPT so the shared validator can refuse them: discarding them
				// turned a malformed "a||b" into a lawful "a|b" before anyone could object.
				parameter.Domain = new List<string>(domain.Split('|'));
			Definition.Parameters.Add(parameter);
			Xml.DoneWithElement();
		}

		private static void HandleStep(KingdomScenarioDefinition Definition, XmlDataHelper Xml)
		{
			string verbText = Trim(Xml.GetAttribute("Verb"));
			KingdomScenarioVerb verb;
			string failure;
			if (!KingdomScenarioRules.TryParseVerb(verbText, out verb, out failure))
			{
				_findings.Add((Definition.Key ?? "(unkeyed)") + ": " + failure);
				Xml.DoneWithElement();
				return;
			}
			// Read exactly the arguments this verb admits. An attribute outside the schema is
			// never read, so the loader warns about it and the row cannot smuggle an argument.
			KingdomScenarioStep step = new KingdomScenarioStep { Verb = verb };
			IList<KingdomScenarioArgumentSpec> specs = KingdomScenarioVerbSchema.Arguments(verb);
			for (int i = 0; i < specs.Count; i++)
			{
				string value = Trim(Xml.GetAttribute(specs[i].Name));
				if (!string.IsNullOrEmpty(value)) step.Arguments[specs[i].Name] = value;
			}
			Definition.Steps.Add(step);
			Xml.DoneWithElement();
		}

		private static string Trim(string Value)
		{
			return string.IsNullOrEmpty(Value) ? Value : Value.Trim();
		}
	}
}
