using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using XRL.World.Conversations;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	internal static class KingdomCitizenRiteProjectionRules
	{
		private const int MaximumRelatedFactions = 256;
		private const int MaximumConversationNodes = 512;
		private const int MaximumConversationDepth = 32;
		private const int MaximumAttributes = 512;
		private const int MaximumText = 32768;

		internal static bool Valid(r_KingdomCitizenRiteProjection Receipt,
			string RealmId, string BodyObjectId)
		{
			return Receipt != null && Receipt.Version
				== r_KingdomCitizenRiteProjection.CurrentVersion
				&& Text(Receipt.RealmId, true) && Receipt.RealmId == RealmId
				&& Text(Receipt.BodyObjectId, true) && Receipt.BodyObjectId == BodyObjectId
				&& Receipt.GreetingBand >= 0 && Receipt.GreetingBand <= 3
				&& (!Receipt.AddedGivesRep && string.IsNullOrEmpty(Receipt.GivesRepDigest)
					|| Receipt.AddedGivesRep && Digest(Receipt.GivesRepDigest))
				&& (!Receipt.AddedConversation
					&& string.IsNullOrEmpty(Receipt.ConversationDigest)
					|| Receipt.AddedConversation && Digest(Receipt.ConversationDigest))
				&& Text(Receipt.Fault, false) && Receipt.Fault.Length <= 512;
		}

		internal static bool TryGivesRepDigest(GivesRep Rep, out string DigestText)
		{
			DigestText = null;
			if (Rep == null) return false;
			try
			{
				return Hash(delegate(BinaryWriter writer)
				{
					writer.Write("taf-citizen-rite-gives-rep-v1");
					writer.Write(Rep.wasParleyed); writer.Write(Rep.repValue);
					if (Rep.relatedFactions == null) { writer.Write(-1); return true; }
					if (Rep.relatedFactions.Count > MaximumRelatedFactions) return false;
					writer.Write(Rep.relatedFactions.Count);
					for (int i = 0; i < Rep.relatedFactions.Count; i++)
					{
						FriendorFoe row = Rep.relatedFactions[i];
						if (row == null || !Write(writer, row.faction)
							|| !Write(writer, row.status) || !Write(writer, row.reason))
							return false;
					}
					return true;
				}, out DigestText);
			}
			catch { return false; }
		}

		internal static bool TryConversationDigest(ConversationScript Script,
			out string DigestText)
		{
			DigestText = null;
			if (Script == null) return false;
			try
			{
				return Hash(delegate(BinaryWriter writer)
				{
					writer.Write("taf-citizen-rite-conversation-v1");
					writer.Write(Script.RecordConversationAsProperty);
					if (!Write(writer, Script.ConversationID) || !Write(writer, Script.Quest)
						|| !Write(writer, Script.PreQuestConversationID)
						|| !Write(writer, Script.InQuestConversationID)
						|| !Write(writer, Script.PostQuestConversationID)) return false;
					writer.Write(Script.ClearLost); writer.Write(Script.ChargeUse);
					if (!Write(writer, Script.Filter) || !Write(writer, Script.FilterExtras)
						|| !Write(writer, Script.Color) || !Write(writer, Script.Append))
						return false;
					int remaining = MaximumConversationNodes;
					return WriteBlueprint(writer, Script.Blueprint, 0, ref remaining,
						new List<ConversationXMLBlueprint>());
				}, out DigestText);
			}
			catch { return false; }
		}

		private static bool WriteBlueprint(BinaryWriter Writer,
			ConversationXMLBlueprint Blueprint, int Depth, ref int Remaining,
			List<ConversationXMLBlueprint> Seen)
		{
			if (Blueprint == null) { Writer.Write((byte)0); return true; }
			if (Depth > MaximumConversationDepth || --Remaining < 0) return false;
			for (int i = 0; i < Seen.Count; i++)
				if (ReferenceEquals(Seen[i], Blueprint)) return false;
			Seen.Add(Blueprint); Writer.Write((byte)1);
			if (!Write(Writer, Blueprint.ID) || !Write(Writer, Blueprint.Name)
				|| !Write(Writer, Blueprint.Text) || !Write(Writer, Blueprint.Inherits)
				|| !Write(Writer, Blueprint.Distribute)) return false;
			Writer.Write(Blueprint.Cardinal); Writer.Write(Blueprint.References);
			Writer.Write(Blueprint.Qualifier); Writer.Write(Blueprint.Load);
			if (Blueprint.Attributes == null) Writer.Write(-1);
			else
			{
				if (Blueprint.Attributes.Count > MaximumAttributes) return false;
				List<string> keys = new List<string>(Blueprint.Attributes.Keys);
				keys.Sort(StringComparer.Ordinal); Writer.Write(keys.Count);
				for (int i = 0; i < keys.Count; i++)
					if (!Write(Writer, keys[i])
						|| !Write(Writer, Blueprint.Attributes[keys[i]])) return false;
			}
			if (Blueprint.Children == null) Writer.Write(-1);
			else
			{
				if (Blueprint.Children.Count > MaximumConversationNodes) return false;
				Writer.Write(Blueprint.Children.Count);
				for (int i = 0; i < Blueprint.Children.Count; i++)
					if (!WriteBlueprint(Writer, Blueprint.Children[i], Depth + 1,
						ref Remaining, Seen)) return false;
			}
			Seen.RemoveAt(Seen.Count - 1); return true;
		}

		private static bool Hash(Func<BinaryWriter, bool> WriteGraph, out string DigestText)
		{
			DigestText = null;
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true))
			{
				if (!WriteGraph(writer)) return false;
				writer.Flush();
				using (SHA256 hash = SHA256.Create())
				{
					byte[] bytes = hash.ComputeHash(stream.ToArray());
					StringBuilder text = new StringBuilder(bytes.Length * 2);
					for (int i = 0; i < bytes.Length; i++) text.Append(bytes[i].ToString("x2"));
					DigestText = text.ToString(); return true;
				}
			}
		}

		private static bool Write(BinaryWriter Writer, string Value)
		{
			if (Value != null && Value.Length > MaximumText) return false;
			Writer.Write(Value != null); if (Value != null) Writer.Write(Value); return true;
		}

		private static bool Text(string Value, bool Required)
		{
			return Value != null && Value.Length <= MaximumText
				&& (!Required || Value.Length > 0);
		}

		private static bool Digest(string Value)
		{
			if (Value == null || Value.Length != 64) return false;
			for (int i = 0; i < Value.Length; i++)
				if (!Uri.IsHexDigit(Value[i])) return false;
			return true;
		}
	}
}
