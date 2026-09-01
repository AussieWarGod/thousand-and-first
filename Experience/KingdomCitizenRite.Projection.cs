using System;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomCitizenRite
	{
		private static bool TryProjection(KingdomSystem System, GameObject Body,
			out r_KingdomCitizenRiteProjection Projection, out string Failure)
		{
			Failure = null; Projection = Body?.GetPart<r_KingdomCitizenRiteProjection>();
			string realm = System?.CurrentRealmId;
			string objectId = Body?.IDIfAssigned;
			if (string.IsNullOrEmpty(realm) || string.IsNullOrEmpty(objectId))
			{
				Failure = "citizen host lacks exact realm or body identity"; return false;
			}
			if (Projection != null)
			{
				if (KingdomCitizenRiteProjectionRules.Valid(Projection, realm, objectId))
					return true;
				Failure = "citizen host provenance is malformed or belongs elsewhere"; return false;
			}
			Projection = new r_KingdomCitizenRiteProjection
			{
				RealmId = realm, BodyObjectId = objectId,
				// Existing host scalars predate exact provenance. Their native parts are
				// deliberately treated as foreign and preserved.
				GreetingBand = 0
			};
			Body.AddPart(Projection);
			if (Body.GetPart<r_KingdomCitizenRiteProjection>() != Projection)
			{
				Failure = "citizen host provenance did not attach exactly"; return false;
			}
			return true;
		}

		private static bool ObserveGivesRep(r_KingdomCitizenRiteProjection Projection,
			GivesRep Rep, bool Added, out string Failure)
		{
			Failure = null;
			if (Projection == null || Rep == null) return false;
			if (!Projection.AddedGivesRep && !Added) return true;
			if (!KingdomCitizenRiteProjectionRules.TryGivesRepDigest(Rep,
				out string digest))
			{
				Failure = "citizen host related-faction state is not exactly representable";
				return false;
			}
			if (Projection.AddedGivesRep && !Added
				&& Projection.GivesRepDigest != digest)
			{
				Projection.Fault = "TAF-added GivesRep changed; native state is preserved";
				return true;
			}
			Projection.AddedGivesRep = true;
			Projection.GivesRepDigest = digest; Projection.Fault = ""; return true;
		}

		private static bool ObserveConversation(r_KingdomCitizenRiteProjection Projection,
			ConversationScript Conversation, int Band, bool Added, out string Failure)
		{
			Failure = null;
			if (Projection == null || Conversation == null || Band < 1 || Band > 3)
				return false;
			if (!KingdomCitizenRiteProjectionRules.TryConversationDigest(Conversation,
				out string digest))
			{
				Failure = "citizen host conversation is not exactly representable"; return false;
			}
			if (Projection.AddedConversation && !Added
				&& Projection.ConversationDigest != digest)
			{
				Projection.Fault = "TAF-added conversation changed; native graph is preserved";
				return false;
			}
			Projection.AddedConversation = true;
			Projection.ConversationDigest = digest;
			Projection.GreetingBand = Band; Projection.Fault = ""; return true;
		}

		internal static bool CanRetireAccedingHost(KingdomSystem System, GameObject Body)
		{
			r_KingdomCitizenRiteProjection receipt =
				Body?.GetPart<r_KingdomCitizenRiteProjection>();
			return receipt == null || KingdomCitizenRiteProjectionRules.Valid(receipt,
				System?.CurrentRealmId, Body?.IDIfAssigned);
		}

		internal static bool TryRetireAccedingHost(KingdomSystem System, GameObject Body,
			out string Failure)
		{
			Failure = null;
			if (!CanRetireAccedingHost(System, Body))
			{
				Failure = "citizen host provenance cannot be retired exactly"; return false;
			}
			r_KingdomCitizenRiteProjection receipt =
				Body.GetPart<r_KingdomCitizenRiteProjection>();
			if (receipt != null && receipt.AddedGivesRep)
			{
				GivesRep rep = Body.GetPart<GivesRep>();
				bool exact = rep != null && KingdomCitizenRiteProjectionRules
					.TryGivesRepDigest(rep, out string digest)
					&& digest == receipt.GivesRepDigest;
				if (exact)
					Body.RemovePart(rep);
				if (exact && Body.GetPart<GivesRep>() == rep)
				{
					Failure = "exact TAF-added GivesRep resisted removal"; return false;
				}
			}
			if (receipt != null && receipt.AddedConversation)
			{
				ConversationScript conversation = Body.GetPart<ConversationScript>();
				bool exact = conversation != null && KingdomCitizenRiteProjectionRules
					.TryConversationDigest(conversation, out string digest)
					&& digest == receipt.ConversationDigest;
				if (exact) Body.RemovePart(conversation);
				if (exact && Body.GetPart<ConversationScript>() == conversation)
				{
					Failure = "exact TAF-added conversation resisted removal"; return false;
				}
			}
			Body.RemoveIntProperty(HostProperty);
			Body.RemoveIntProperty(ConversationProperty);
			Body.RemoveIntProperty(GreetingBandProperty);
			if (receipt != null) Body.RemovePart(receipt);
			if (Body.HasIntProperty(HostProperty) || Body.HasIntProperty(ConversationProperty)
				|| Body.HasIntProperty(GreetingBandProperty)
				|| Body.GetPart<r_KingdomCitizenRiteProjection>() == receipt && receipt != null)
			{
				Failure = "citizen host scalar or provenance cleanup resisted removal"; return false;
			}
			return true;
		}
	}
}
