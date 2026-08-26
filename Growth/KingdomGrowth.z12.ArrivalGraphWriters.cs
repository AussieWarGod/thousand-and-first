using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.Rules;
using XRL.UI;
using XRL.World;
using XRL.World.AI;
using XRL.World.Conversations;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomGrowth
	{

		private static void WriteExactAllegianceGraph(BinaryWriter writer,
			AllegianceSet allegiance, bool projectedAfter, string kingdomFaction)
		{
			List<AllegianceSet> chain = new List<AllegianceSet>();
			for (AllegianceSet cursor = allegiance; cursor != null; cursor = cursor.Previous)
				chain.Add(cursor);
			writer.Write(chain.Count);
			bool projectedBase = false;
			for (int layer = 0; layer < chain.Count; layer++)
			{
				AllegianceSet current = chain[layer];
				writer.Write(current.SourceID);
				writer.Write(current.Flags);
				WriteExactAllyReason(writer, current.Reason);
				Dictionary<string, int> memberships =
					new Dictionary<string, int>(StringComparer.Ordinal);
				foreach (KeyValuePair<string, int> membership in current)
					memberships[membership.Key] = membership.Value;
				if (projectedAfter && !projectedBase && current.SourceID == 0)
				{
					memberships[kingdomFaction] = KingdomCitizenshipRules.RealmMembership;
					projectedBase = true;
				}
				List<string> keys = new List<string>(memberships.Keys);
				keys.Sort(StringComparer.Ordinal);
				writer.Write(keys.Count);
				for (int i = 0; i < keys.Count; i++)
				{
					WriteString(writer, keys[i]);
					writer.Write(memberships[keys[i]]);
				}
			}
		}

		private static void WriteExactAllyReason(BinaryWriter writer, IAllyReason reason)
		{
			if (reason == null) { writer.Write(-1); return; }
			SerializationWriter exact = SerializationWriter.Get();
			try
			{
				exact.Write(reason);
				byte[] bytes = exact.ToArray();
				WriteString(writer, reason.GetType().AssemblyQualifiedName);
				writer.Write(bytes.Length);
				writer.Write(bytes);
			}
			finally
			{
				SerializationWriter.Release(exact);
			}
		}

		private static void WriteCitizenshipReceiptGraph(BinaryWriter writer,
			KingdomSystem system, GameObject settler, bool projectedAfter,
			long frozenAppliedTick)
		{
			r_KingdomCitizenship receipt = projectedAfter
				? null : settler?.GetPart<r_KingdomCitizenship>();
			writer.Write(projectedAfter || receipt != null);
			if (!projectedAfter && receipt == null) return;
			if (projectedAfter)
			{
				AllegianceSet baseSet = settler?.Brain?.GetBaseAllegiance();
				int priorValue = 0;
				bool priorPresent = baseSet != null
					&& baseSet.TryGetValue(system.KingdomFactionName, out priorValue);
				writer.Write(KingdomCitizenshipRules.CurrentReceiptVersion);
				writer.Write((int)KingdomCitizenshipPhase.Applied);
				writer.Write((int)(priorPresent ? KingdomCitizenshipPriorKind.Present
					: KingdomCitizenshipPriorKind.Absent));
				writer.Write(priorPresent ? priorValue : 0);
				writer.Write(KingdomCitizenshipRules.RealmMembership);
				WriteString(writer, system.CurrentRealmId);
				WriteString(writer, system.CurrentSettlementId);
				WriteString(writer, system.KingdomFactionName);
				WriteString(writer, settler?.IDIfAssigned);
				writer.Write((int)KingdomCitizenshipEnrollmentReason.Arrival);
				writer.Write(0);
				writer.Write(frozenAppliedTick);
				writer.Write(0L);
				writer.Write(false);
				WriteString(writer, "");
				return;
			}
			writer.Write(receipt.ReceiptVersion);
			writer.Write((int)receipt.Phase);
			writer.Write((int)receipt.PriorKind);
			writer.Write(receipt.PriorValue);
			writer.Write(receipt.AppliedValue);
			WriteString(writer, receipt.OwnerRealmId);
			WriteString(writer, receipt.OwnerSettlementId);
			WriteString(writer, receipt.FactionId);
			WriteString(writer, receipt.BodyObjectId);
			writer.Write(receipt.EnrollmentReason);
			writer.Write(receipt.RemovalReason);
			writer.Write(receipt.AppliedTick);
			writer.Write(receipt.RemovedTick);
			writer.Write(receipt.NoticePublished);
			WriteString(writer, receipt.Fault);
		}

		private static void WriteAllegianceGraph(BinaryWriter writer, AllegianceSet allegiance,
			bool projectedAfter, string kingdomFaction, bool top, int depth,
			ref bool baseReplaced)
		{
			if (allegiance == null) { writer.Write((byte)0); return; }
			writer.Write((byte)1);
			writer.Write(allegiance.SourceID);
			int flags = allegiance.Flags;
			if (projectedAfter && top) flags = (flags | 2) & -2;
			writer.Write(flags);
			WriteString(writer, allegiance.Reason?.GetType().FullName);
			if (allegiance.Reason != null)
			{
				writer.Write(allegiance.Reason.Time);
				IAllyReasonSourced sourced = allegiance.Reason as IAllyReasonSourced;
				writer.Write(sourced != null);
				if (sourced != null) WriteString(writer, sourced.Name);
			}
			bool replace = projectedAfter && !baseReplaced && allegiance.SourceID == 0;
			if (replace)
			{
				baseReplaced = true;
				writer.Write(1); WriteString(writer, kingdomFaction); writer.Write(100);
			}
			else
			{
				List<KeyValuePair<string, int>> memberships =
					new List<KeyValuePair<string, int>>(allegiance);
				memberships.Sort(delegate(KeyValuePair<string, int> a,
					KeyValuePair<string, int> b)
				{
					int byName = string.CompareOrdinal(a.Key, b.Key);
					return byName != 0 ? byName : a.Value.CompareTo(b.Value);
				});
				writer.Write(memberships.Count);
				for (int i = 0; i < memberships.Count; i++)
				{
					WriteString(writer, memberships[i].Key);
					writer.Write(memberships[i].Value);
				}
			}
			WriteAllegianceGraph(writer, allegiance.Previous, projectedAfter, kingdomFaction,
				false, depth + 1, ref baseReplaced);
		}

		private static void WriteArrivalConversationGraph(BinaryWriter writer,
			GameObject settler, bool projectedAfter, string origin)
		{
			ConversationScript conversation = projectedAfter
				? new ConversationScript
				{
					Blueprint = ExpectedArrivalConversationBlueprint(settler?.ID, origin)
				}
				: settler?.GetPart<ConversationScript>();
			writer.Write(conversation != null);
			if (conversation == null) return;
			writer.Write(conversation.RecordConversationAsProperty);
			WriteString(writer, conversation.ConversationID);
			WriteString(writer, conversation.Quest);
			WriteString(writer, conversation.PreQuestConversationID);
			WriteString(writer, conversation.InQuestConversationID);
			WriteString(writer, conversation.PostQuestConversationID);
			writer.Write(conversation.ClearLost); writer.Write(conversation.ChargeUse);
			WriteString(writer, conversation.Filter);
			WriteString(writer, conversation.FilterExtras);
			WriteString(writer, conversation.Color);
			WriteString(writer, conversation.Append);
			WriteString(writer, projectedAfter ? "1"
				: settler?.GetStringProperty("SuppressPowerSwitchTwiddle"));
			int remaining = MaxArrivalConversationNodes;
			WriteConversationBlueprint(writer, conversation.Blueprint, 0, ref remaining);
		}

		private static ConversationXMLBlueprint ExpectedArrivalConversationBlueprint(
			string objectId, string origin)
		{
			ConversationXMLBlueprint blueprint = new ConversationXMLBlueprint
			{
				ID = "CustomConversation::" + objectId,
				Name = "Conversation"
			};
			Qud.API.ConversationsAPI.AddChoice(
				Qud.API.ConversationsAPI.AddStart(blueprint, ArrivalConversationText),
				null, "End", ArrivalConversationGoodbye);
			ConversationXMLBlueprint answer = Qud.API.ConversationsAPI.AddNode(blueprint,
				null, ArrivalConversationAnswerPrefix + origin + ArrivalConversationAnswerSuffix);
			Qud.API.ConversationsAPI.AddChoice(answer, null, "End", ArrivalConversationGoodbye);
			Qud.API.ConversationsAPI.AddChoice(blueprint.GetChild("Start"), null, answer,
				ArrivalConversationQuestion);
			return blueprint;
		}
	}
}
