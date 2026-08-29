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
		public const string ReceiptDeclarationProperty = "r_TAF_SocketTransitionDeclaration";
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
			KingdomSocketTransition registered;
			return key != null && byRoute.TryGetValue(key, out registered)
				&& KingdomSocketTransitionRules.TrySnapshot(registered, out Transition);
		}

		/// <summary>Re-resolves and exactly matches every field of a caller-supplied declaration.</summary>
		internal static bool TryResolveCurrent(KingdomSocketTransition Supplied, string From,
			string To, string Type, ArchitectureLotSize Size,
			out KingdomSocketTransition Current)
		{
			Current = null;
			KingdomSocketTransition declared;
			if (!TryGet(From, To, Type, Size, out declared)
				|| !KingdomSocketTransitionRules.MatchesRoute(Supplied, declared)) return false;
			Current = declared;
			return true;
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
				|| string.IsNullOrEmpty(Job.Id) || Before.LotType != After.LotType
				|| Before.LotSize != After.LotSize
				|| !TryResolveCurrent(Transition, Before.BuildKey, After.BuildKey, Before.LotType,
					Before.LotSize, out KingdomSocketTransition declared)
				|| !KingdomSocketTransitionRules.TryDeclarationDigest(declared,
					out string declarationDigest))
			{
				Failure = "Same-set transition receipt lacks its exact current declaration.";
				return false;
			}
			KingdomSocketTransitionReceiptShape intended = IntendedReceipt(declared.Key,
				declarationDigest, Before.SnapshotHash, After.SnapshotHash, Job.Id);
			bool intendedLegacy;
			if (!KingdomSocketTransitionRules.ReceiptAuthorizes(intended, declared.Key,
				declarationDigest, Before.SnapshotHash, After.SnapshotHash, Job.Id,
				out intendedLegacy) || intendedLegacy)
			{
				Failure = "Same-set transition receipt values are malformed.";
				return false;
			}
			try
			{
				// Invalidate any prior commit before changing one payload field. Schema publishes last.
				Owner.RemoveIntProperty(ReceiptSchemaProperty);
				Owner.RemoveStringProperty(ReceiptSchemaProperty);
				RemoveIntPayloadTypes(Owner);
				Owner.SetStringProperty(ReceiptKeyProperty, declared.Key);
				Owner.SetStringProperty(ReceiptDeclarationProperty, declarationDigest);
				Owner.SetStringProperty(ReceiptBeforeHashProperty, Before.SnapshotHash);
				Owner.SetStringProperty(ReceiptAfterHashProperty, After.SnapshotHash);
				Owner.SetStringProperty(ReceiptJobProperty, Job.Id);
				Owner.SetIntProperty(ReceiptSchemaProperty,
					KingdomSocketTransitionRules.ReceiptSchema);
			}
			catch (Exception exception)
			{
				InvalidateReceipt(Owner);
				Failure = "Same-set transition receipt write failed: " + exception.Message;
				return false;
			}
			KingdomSocketTransitionReceiptShape receipt = ReadReceiptShape(Owner);
			bool legacy;
			if (KingdomSocketTransitionRules.ReceiptAuthorizes(receipt, declared.Key,
				declarationDigest, Before.SnapshotHash, After.SnapshotHash, Job.Id, out legacy)
				&& !legacy) return true;
			InvalidateReceipt(Owner);
			Failure = "Same-set transition receipt was not published in one exact shape.";
			return false;
		}

		internal static bool Authorizes(GameObject Owner, KingdomArchitectureIntent Before,
			KingdomArchitectureIntent After)
		{
			if (!GameObject.Validate(Owner) || Before == null || After == null
				|| Before.LotType != After.LotType || Before.LotSize != After.LotSize
				|| !Owner.HasStringProperty(KingdomConstruction.ReceiptProperty)
				|| Owner.HasIntProperty(KingdomConstruction.ReceiptProperty)) return false;
			string jobId = Owner.GetStringProperty(KingdomConstruction.ReceiptProperty);
			KingdomSocketTransition declared;
			string declarationDigest;
			if (string.IsNullOrEmpty(jobId)
				|| !TryGet(Before.BuildKey, After.BuildKey, Before.LotType, Before.LotSize,
					out declared)
				|| !KingdomSocketTransitionRules.TryDeclarationDigest(declared,
					out declarationDigest)) return false;
			KingdomSocketTransitionReceiptShape receipt = ReadReceiptShape(Owner);
			bool legacy;
			if (!KingdomSocketTransitionRules.ReceiptAuthorizes(receipt, declared.Key,
				declarationDigest, Before.SnapshotHash, After.SnapshotHash, jobId, out legacy))
				return false;
			return !legacy || TryAdoptLegacyReceipt(Owner, declared.Key, declarationDigest,
				Before.SnapshotHash, After.SnapshotHash, jobId);
		}

		/// <summary>Clears only the exact receipt named by this declaration, job, and endpoint pair.</summary>
		internal static bool ClearReceipt(GameObject Owner, KingdomConstructionJob Job,
			KingdomArchitectureIntent Before, KingdomArchitectureIntent After,
			KingdomSocketTransition Transition)
		{
			if (!GameObject.Validate(Owner) || Job == null || Before == null || After == null
				|| string.IsNullOrEmpty(Job.Id) || Before.LotType != After.LotType
				|| Before.LotSize != After.LotSize
				|| !TryResolveCurrent(Transition, Before.BuildKey, After.BuildKey, Before.LotType,
					Before.LotSize, out KingdomSocketTransition declared)
				|| !KingdomSocketTransitionRules.TryDeclarationDigest(declared,
					out string declarationDigest)) return false;
			bool legacy;
			if (!KingdomSocketTransitionRules.ReceiptAuthorizes(ReadReceiptShape(Owner),
				declared.Key, declarationDigest, Before.SnapshotHash, After.SnapshotHash,
				Job.Id, out legacy)) return false;
			try
			{
				if (!TryInvalidateReceipt(Owner)) return false;
				RemovePayload(Owner);
			}
			catch
			{
				InvalidateReceipt(Owner);
				return false;
			}
			return !HasAnyReceiptField(Owner);
		}

		private static bool TryAdoptLegacyReceipt(GameObject Owner, string Key,
			string DeclarationDigest, string BeforeHash, string AfterHash, string JobId)
		{
			try
			{
				// Legacy schema is invalidated before its sole new field is written.
				Owner.RemoveIntProperty(ReceiptSchemaProperty);
				Owner.RemoveStringProperty(ReceiptSchemaProperty);
				Owner.RemoveIntProperty(ReceiptDeclarationProperty);
				Owner.SetStringProperty(ReceiptDeclarationProperty, DeclarationDigest);
				Owner.SetIntProperty(ReceiptSchemaProperty,
					KingdomSocketTransitionRules.ReceiptSchema);
			}
			catch
			{
				InvalidateReceipt(Owner);
				return false;
			}
			bool legacy;
			return KingdomSocketTransitionRules.ReceiptAuthorizes(ReadReceiptShape(Owner), Key,
				DeclarationDigest, BeforeHash, AfterHash, JobId, out legacy) && !legacy;
		}

		private static KingdomSocketTransitionReceiptShape ReadReceiptShape(GameObject Owner)
		{
			return new KingdomSocketTransitionReceiptShape
			{
				SchemaHasInt = Owner.HasIntProperty(ReceiptSchemaProperty),
				SchemaHasString = Owner.HasStringProperty(ReceiptSchemaProperty),
				Schema = Owner.GetIntProperty(ReceiptSchemaProperty),
				KeyHasInt = Owner.HasIntProperty(ReceiptKeyProperty),
				KeyHasString = Owner.HasStringProperty(ReceiptKeyProperty),
				Key = Owner.GetStringProperty(ReceiptKeyProperty),
				DeclarationHasInt = Owner.HasIntProperty(ReceiptDeclarationProperty),
				DeclarationHasString = Owner.HasStringProperty(ReceiptDeclarationProperty),
				DeclarationDigest = Owner.GetStringProperty(ReceiptDeclarationProperty),
				BeforeHasInt = Owner.HasIntProperty(ReceiptBeforeHashProperty),
				BeforeHasString = Owner.HasStringProperty(ReceiptBeforeHashProperty),
				BeforeHash = Owner.GetStringProperty(ReceiptBeforeHashProperty),
				AfterHasInt = Owner.HasIntProperty(ReceiptAfterHashProperty),
				AfterHasString = Owner.HasStringProperty(ReceiptAfterHashProperty),
				AfterHash = Owner.GetStringProperty(ReceiptAfterHashProperty),
				JobHasInt = Owner.HasIntProperty(ReceiptJobProperty),
				JobHasString = Owner.HasStringProperty(ReceiptJobProperty),
				JobId = Owner.GetStringProperty(ReceiptJobProperty)
			};
		}

		private static KingdomSocketTransitionReceiptShape IntendedReceipt(string Key,
			string DeclarationDigest, string BeforeHash, string AfterHash, string JobId)
		{
			return new KingdomSocketTransitionReceiptShape
			{
				SchemaHasInt = true,
				Schema = KingdomSocketTransitionRules.ReceiptSchema,
				KeyHasString = true,
				Key = Key,
				DeclarationHasString = true,
				DeclarationDigest = DeclarationDigest,
				BeforeHasString = true,
				BeforeHash = BeforeHash,
				AfterHasString = true,
				AfterHash = AfterHash,
				JobHasString = true,
				JobId = JobId
			};
		}

		private static void RemoveIntPayloadTypes(GameObject Owner)
		{
			Owner.RemoveIntProperty(ReceiptKeyProperty);
			Owner.RemoveIntProperty(ReceiptDeclarationProperty);
			Owner.RemoveIntProperty(ReceiptBeforeHashProperty);
			Owner.RemoveIntProperty(ReceiptAfterHashProperty);
			Owner.RemoveIntProperty(ReceiptJobProperty);
		}

		private static void RemovePayload(GameObject Owner)
		{
			Owner.RemoveIntProperty(ReceiptKeyProperty);
			Owner.RemoveStringProperty(ReceiptKeyProperty);
			Owner.RemoveIntProperty(ReceiptDeclarationProperty);
			Owner.RemoveStringProperty(ReceiptDeclarationProperty);
			Owner.RemoveIntProperty(ReceiptBeforeHashProperty);
			Owner.RemoveStringProperty(ReceiptBeforeHashProperty);
			Owner.RemoveIntProperty(ReceiptAfterHashProperty);
			Owner.RemoveStringProperty(ReceiptAfterHashProperty);
			Owner.RemoveIntProperty(ReceiptJobProperty);
			Owner.RemoveStringProperty(ReceiptJobProperty);
		}

		private static void InvalidateReceipt(GameObject Owner)
		{
			try { Owner.RemoveIntProperty(ReceiptSchemaProperty); } catch { }
			try { Owner.RemoveStringProperty(ReceiptSchemaProperty); } catch { }
		}

		private static bool TryInvalidateReceipt(GameObject Owner)
		{
			try
			{
				Owner.RemoveIntProperty(ReceiptSchemaProperty);
				Owner.RemoveStringProperty(ReceiptSchemaProperty);
			}
			catch
			{
				return false;
			}
			return !Owner.HasIntProperty(ReceiptSchemaProperty)
				&& !Owner.HasStringProperty(ReceiptSchemaProperty);
		}

		private static bool HasAnyReceiptField(GameObject Owner)
		{
			return Owner.HasIntProperty(ReceiptSchemaProperty)
				|| Owner.HasStringProperty(ReceiptSchemaProperty)
				|| Owner.HasIntProperty(ReceiptKeyProperty)
				|| Owner.HasStringProperty(ReceiptKeyProperty)
				|| Owner.HasIntProperty(ReceiptDeclarationProperty)
				|| Owner.HasStringProperty(ReceiptDeclarationProperty)
				|| Owner.HasIntProperty(ReceiptBeforeHashProperty)
				|| Owner.HasStringProperty(ReceiptBeforeHashProperty)
				|| Owner.HasIntProperty(ReceiptAfterHashProperty)
				|| Owner.HasStringProperty(ReceiptAfterHashProperty)
				|| Owner.HasIntProperty(ReceiptJobProperty)
				|| Owner.HasStringProperty(ReceiptJobProperty);
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
