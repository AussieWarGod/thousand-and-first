using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	internal sealed class KingdomSealReceipt
	{
		private const string Kind = "receipt";

		private static readonly string[] StateNames = new string[3] { "reserved", "committed", "declined" };

		// Same law as KingdomSealRecord: parsed receipts preserve their canonical wire envelope,
		// while every newly-created receipt starts at the current schema.
		private int WireSchema = KingdomSealRecord.CurrentSchema;

		public string LineageId = "";

		public string LegacyId = "";

		public string TargetGameId = "";

		public KingdomSealReceiptState State = KingdomSealReceiptState.Reserved;

		public long WrittenTick;

		internal string Compose()
		{
			KingdomSealBody body = new KingdomSealBody();
			body.Put("kind", Kind);
			body.Put("lineage", LineageId);
			body.Put("legacy", LegacyId);
			body.Put("target", TargetGameId);
			body.Put("state", StateName(State));
			body.Put("written", WrittenTick);
			return KingdomSealFormat.Compose(WireSchema, body);
		}

		internal static bool TryParse(string FileText, out KingdomSealReceipt Receipt)
		{
			Receipt = null;
			try
			{
				int schema;
				KingdomSealBody body;
				KingdomSealFault fault;
				string detail;
				if (!KingdomSealFormat.TryParse(FileText, KingdomSealRecord.FirstSchema,
					KingdomSealRecord.CurrentSchema, out schema, out body, out fault, out detail))
				{
					return false;
				}
				if (body.Count != 6 || !body.Has("kind") || !body.Has("lineage") || !body.Has("legacy")
					|| !body.Has("target") || !body.Has("state") || !body.Has("written"))
				{
					return false;
				}
				if (body.KindOf("kind") != KingdomSealKind.Text || body.Text("kind") != Kind
					|| body.KindOf("lineage") != KingdomSealKind.Text
					|| body.KindOf("legacy") != KingdomSealKind.Text
					|| body.KindOf("target") != KingdomSealKind.Text
					|| body.KindOf("state") != KingdomSealKind.Text
					|| body.KindOf("written") != KingdomSealKind.Number)
				{
					return false;
				}
				string lineage = body.Text("lineage");
				string legacy = body.Text("legacy");
				string target = body.Text("target");
				if (!ValidId(lineage) || !ValidId(legacy) || !ValidId(target))
				{
					return false;
				}
				int state = StateIndex(body.Text("state"));
				long written = body.Number("written", -1L);
				if (state < 0 || written < 0L)
				{
					return false;
				}
				Receipt = new KingdomSealReceipt
				{
					LineageId = lineage,
					LegacyId = legacy,
					TargetGameId = target,
					State = (KingdomSealReceiptState)state,
					WrittenTick = written,
					WireSchema = schema
				};
				return true;
			}
			catch (Exception)
			{
				Receipt = null;
				return false;
			}
		}

		internal static bool ValidId(string Value)
		{
			if (string.IsNullOrEmpty(Value) || Value.Length > KingdomSealRecord.MaxIdChars)
			{
				return false;
			}
			for (int i = 0; i < Value.Length; i++)
			{
				char c = Value[i];
				if ((c < 'a' || c > 'z') && (c < 'A' || c > 'Z')
					&& (c < '0' || c > '9') && c != '_' && c != '-')
				{
					return false;
				}
			}
			return true;
		}

		private static string StateName(KingdomSealReceiptState State)
		{
			int index = (int)State;
			if (index < 0 || index >= StateNames.Length)
			{
				throw new InvalidOperationException("The receipt state is not known.");
			}
			return StateNames[index];
		}

		private static int StateIndex(string Value)
		{
			for (int i = 0; i < StateNames.Length; i++)
			{
				if (StateNames[i] == Value)
				{
					return i;
				}
			}
			return -1;
		}
	}
}
