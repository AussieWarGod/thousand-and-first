using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Mergeable engine-facing registry for explicit same-set plan changes.</summary>
	public static class KingdomSocketTransitions
	{
		private sealed class Raw
		{
			public string Key;
			public readonly Dictionary<string, string> Values =
				new Dictionary<string, string>(StringComparer.Ordinal);
		}

		private static readonly Dictionary<string, KingdomSocketTransition> byRoute =
			new Dictionary<string, KingdomSocketTransition>(StringComparer.Ordinal);
		private static bool loaded;

		public const string ReceiptSchemaProperty = "r_TAF_SocketTransitionSchema";
		public const string ReceiptKeyProperty = "r_TAF_SocketTransitionKey";
		public const string ReceiptBeforeHashProperty = "r_TAF_SocketTransitionBefore";
		public const string ReceiptAfterHashProperty = "r_TAF_SocketTransitionAfter";
		public const string ReceiptJobProperty = "r_TAF_SocketTransitionJob";

		public static void Reload()
		{
			loaded = false;
			byRoute.Clear();
			EnsureLoaded();
		}

		public static bool TryGet(string From, string To, string Type,
			ArchitectureLotSize Size, out KingdomSocketTransition Transition)
		{
			Transition = null;
			EnsureLoaded();
			string key = KingdomSocketTransitionRules.IndexKey(From, To, Type, Size);
			return key != null && byRoute.TryGetValue(key, out Transition);
		}

		private static void EnsureLoaded()
		{
			if (loaded) return;
			Dictionary<string, Raw> merged = new Dictionary<string, Raw>(StringComparer.Ordinal);
			int count = 0;
			foreach (XmlDataHelper xml in DataManager.YieldXMLStreamsWithRoot(
				"KingdomArchitectureTransitions"))
			{
				Dictionary<string, Action<XmlDataHelper>> roots =
					new Dictionary<string, Action<XmlDataHelper>>(StringComparer.Ordinal)
					{
						{ "KingdomArchitectureTransitions", delegate(XmlDataHelper root)
							{
								string schema = root.GetAttribute("Schema");
								if (schema != KingdomSocketTransitionRules.Schema.ToString(
									System.Globalization.CultureInfo.InvariantCulture))
								{
									Skip(root);
									return;
								}
								root.HandleNodes(new Dictionary<string, Action<XmlDataHelper>>
								{
									{ "transition", delegate(XmlDataHelper child)
										{
											string key = child.GetAttribute("Key");
											if (string.IsNullOrEmpty(key) || count >= KingdomSocketTransitionRules.MaxTransitions)
											{
												child.DoneWithElement();
												return;
											}
											Raw raw;
											if (!merged.TryGetValue(key, out raw))
											{
												raw = new Raw { Key = key };
												merged.Add(key, raw);
												count++;
											}
											Set(raw, "From", child.GetAttribute("From"));
											Set(raw, "To", child.GetAttribute("To"));
											Set(raw, "Type", child.GetAttribute("Type"));
											Set(raw, "Size", child.GetAttribute("Size"));
											Set(raw, "Water", child.GetAttribute("Water"));
											Set(raw, "Materials", child.GetAttribute("Materials"));
											Set(raw, "Ticks", child.GetAttribute("Ticks"));
											child.DoneWithElement();
										} }
								}, delegate(XmlDataHelper unknown) { Skip(unknown); });
							} }
					};
				xml.HandleNodes(roots, delegate(XmlDataHelper unknown) { Skip(unknown); });
			}
			foreach (Raw raw in merged.Values)
			{
				KingdomSocketTransition parsed;
				string failure;
				if (!KingdomSocketTransitionRules.TryParse(raw.Key, Get(raw, "From"),
					Get(raw, "To"), Get(raw, "Type"), Get(raw, "Size"), Get(raw, "Water"),
					Get(raw, "Materials"), Get(raw, "Ticks"), out parsed, out failure))
				{
					MetricsManager.LogError("ThousandAndFirst transitions: " + failure);
					continue;
				}
				KingdomArchitectureMapping from;
				KingdomArchitectureMapping to;
				if (!KingdomArchitecture.TryGetMapping(parsed.FromBuildKey, parsed.LotType,
					parsed.LotSize, out from)
					|| !KingdomArchitecture.TryGetMapping(parsed.ToBuildKey, parsed.LotType,
						parsed.LotSize, out to)
					|| from.TypeKey != to.TypeKey || from.LotSize != to.LotSize)
				{
					MetricsManager.LogError("ThousandAndFirst transitions: " + parsed.Key
						+ " does not resolve two exact mappings in one typed lot");
					continue;
				}
				string route = KingdomSocketTransitionRules.IndexKey(parsed.FromBuildKey,
					parsed.ToBuildKey, parsed.LotType, parsed.LotSize);
				if (byRoute.ContainsKey(route))
				{
					MetricsManager.LogError("ThousandAndFirst transitions: duplicate route "
						+ parsed.FromBuildKey + " -> " + parsed.ToBuildKey);
					continue;
				}
				byRoute.Add(route, parsed);
			}
			loaded = true;
		}

		internal static bool BindReceipt(GameObject Owner, KingdomConstructionJob Job,
			KingdomArchitectureIntent Before, KingdomArchitectureIntent After,
			KingdomSocketTransition Transition, out string Failure)
		{
			Failure = null;
			if (!GameObject.Validate(Owner) || Job == null || Before == null || After == null
				|| Transition == null || string.IsNullOrEmpty(Job.Id))
			{
				Failure = "Same-set transition receipt is incomplete.";
				return false;
			}
			Owner.SetStringProperty(ReceiptKeyProperty, Transition.Key);
			Owner.SetStringProperty(ReceiptBeforeHashProperty, Before.SnapshotHash);
			Owner.SetStringProperty(ReceiptAfterHashProperty, After.SnapshotHash);
			Owner.SetStringProperty(ReceiptJobProperty, Job.Id);
			Owner.SetIntProperty(ReceiptSchemaProperty, 1);
			return Owner.GetIntProperty(ReceiptSchemaProperty) == 1
				&& Owner.GetStringProperty(ReceiptKeyProperty) == Transition.Key
				&& Owner.GetStringProperty(ReceiptBeforeHashProperty) == Before.SnapshotHash
				&& Owner.GetStringProperty(ReceiptAfterHashProperty) == After.SnapshotHash
				&& Owner.GetStringProperty(ReceiptJobProperty) == Job.Id;
		}

		internal static bool Authorizes(GameObject Owner, KingdomArchitectureIntent Before,
			KingdomArchitectureIntent After)
		{
			return GameObject.Validate(Owner) && Before != null && After != null
				&& Owner.GetIntProperty(ReceiptSchemaProperty) == 1
				&& !string.IsNullOrEmpty(Owner.GetStringProperty(ReceiptKeyProperty))
				&& Owner.GetStringProperty(ReceiptBeforeHashProperty) == Before.SnapshotHash
				&& Owner.GetStringProperty(ReceiptAfterHashProperty) == After.SnapshotHash
				&& Owner.GetStringProperty(ReceiptJobProperty)
					== Owner.GetStringProperty(KingdomConstruction.ReceiptProperty);
		}

		internal static void ClearReceipt(GameObject Owner)
		{
			if (!GameObject.Validate(Owner)) return;
			Owner.RemoveIntProperty(ReceiptSchemaProperty);
			Owner.SetStringProperty(ReceiptKeyProperty, null, RemoveIfNull: true);
			Owner.SetStringProperty(ReceiptBeforeHashProperty, null, RemoveIfNull: true);
			Owner.SetStringProperty(ReceiptAfterHashProperty, null, RemoveIfNull: true);
			Owner.SetStringProperty(ReceiptJobProperty, null, RemoveIfNull: true);
		}

		private static void Set(Raw Raw, string Key, string Value)
		{
			if (Value != null) Raw.Values[Key] = Value;
		}

		private static string Get(Raw Raw, string Key)
		{
			string value;
			return Raw.Values.TryGetValue(Key, out value) ? value : null;
		}

		private static void Skip(XmlDataHelper Xml)
		{
			Xml.HandleNodes(new Dictionary<string, Action<XmlDataHelper>>(),
				delegate(XmlDataHelper child) { Skip(child); });
		}
	}
}
