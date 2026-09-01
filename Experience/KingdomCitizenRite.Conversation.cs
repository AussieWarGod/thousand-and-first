using Qud.API;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomCitizenRite
	{
		/// <summary>How many of this ground's citizens the rite stands open on. Read by the city
		/// book's people chapter.</summary>
		public static string DumpLine(KingdomSystem System, Zone Z)
		{
			if (!Enabled || System == null || Z == null)
			{
				return "";
			}
			int hosts = 0;
			int citizens = 0;
			foreach (GameObject item in Z.GetObjects())
			{
				if (!KingdomCitizenship.BelongsTo(System, item))
				{
					continue;
				}
				citizens++;
				if (item.HasPart<GivesRep>())
				{
					hosts++;
				}
			}
			return (citizens == 0) ? "" : ("rite: " + hosts + " of " + citizens + " citizens here will share water");
		}

		/// <summary>
		/// Gives a settler with no conversation at all one, and keeps ours current as they settle
		/// in.
		/// <para>
		/// A conversation built here is a fixed string on the object. Stamped once on the pass that
		/// first saw them &mdash; when they have lived here no days at all &mdash; a settler would
		/// greet their founder as a newcomer for the rest of their life, and two thirds of the
		/// greetings would be unreachable. So the band is stamped beside it and re-read: crossing
		/// into a different one rebuilds OUR conversation, and only ever ours.
		/// </para>
		/// </summary>
		private static bool Speak(KingdomSystem system, GameObject citizen,
			r_KingdomCitizenRiteProjection projection, out string Failure)
		{
			Failure = null;
			int band = KingdomCitizenRiteRules.Band(KingdomWaterRite.SharedDaysOf(citizen));
			ConversationScript current = citizen.GetPart<ConversationScript>();
			if (current != null && !projection.AddedConversation)
			{
				return true;
			}
			if (current != null)
			{
				if (!KingdomCitizenRiteProjectionRules.TryConversationDigest(current,
					out string currentDigest))
				{
					Failure = "owned citizen greeting is not exactly representable"; return false;
				}
				if (currentDigest != projection.ConversationDigest)
				{
					projection.Fault = "TAF-added conversation changed; native graph is preserved";
					return true;
				}
				if (projection.GreetingBand == band + 1)
				{
					citizen.SetIntProperty(ConversationProperty, 1);
					citizen.SetIntProperty(GreetingBandProperty, band + 1); return true;
				}
			}
			ConversationsAPI.addSimpleConversationToObject(citizen,
				KingdomCitizenRiteRules.Greeting(KingdomPresentation.Rich(system.SeatName), KingdomWaterRite.SharedDaysOf(citizen)),
				KingdomCitizenRiteRules.Farewell());
			citizen.SetIntProperty(ConversationProperty, 1);
			citizen.SetIntProperty(GreetingBandProperty, band + 1);
			return ObserveConversation(projection, citizen.GetPart<ConversationScript>(),
				band + 1, Added: true, out Failure);
		}
	}
}
