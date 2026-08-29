using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomSocketTransitions
	{
		private sealed class Raw
		{
			public string Key;
			public readonly Dictionary<string, string> Values =
				new Dictionary<string, string>(StringComparer.Ordinal);
		}

		public static void Reload()
		{
			loaded = false;
			byRoute.Clear();
			EnsureLoaded();
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
