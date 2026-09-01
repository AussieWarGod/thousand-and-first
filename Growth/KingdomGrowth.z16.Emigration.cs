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

		/// <param name="System">The realm.</param>
		/// <param name="Z">The zone they walk out of.</param>
		/// <param name="Survey">The pass's survey, or null.</param>
		/// <param name="Leaver">A particular settler, for a departure that is about THEM &mdash;
		/// Addendum 4b's settler who has no home they would live in. Null takes whoever the zone
		/// offers first, which is the drought's own indifference and is right for it.</param>
		/// <param name="Cause">The clause both registers name the departure by. Null is the
		/// drought, which is what this machinery was built for and reads exactly as it always
		/// did.</param>
		/// <param name="Chronicled">Whether this departure gets its own line in both registers and
		/// the ledger. True for every ordinary departure, and for the sampled ones of a long
		/// subsidence slide; false for the ones a slide is carrying in its summary line instead
		/// (<c>KingdomSubsidenceRules.TellsDeparture</c>). The person still leaves, the ledger's
		/// departure COUNT still rises, and the log still records it &mdash; what is saved is a
		/// chronicle entry, because a City falling to Camp would otherwise spend a quarter of the
		/// two-hundred-entry register on one event.</param>
		/// <param name="Note">The same departure in the ledger's shorter voice. Null falls back to
		/// <paramref name="Cause"/>, which is what a caller with only one phrasing wants, and
		/// what every caller written before the two registers wanted different lengths passed.</param>
		public static bool Emigrate(KingdomSystem System, Zone Z, KingdomSurvey Survey = null,
			GameObject Leaver = null, string Cause = null, bool Chronicled = true,
			string Note = null)
		{
			return EmigrateCore(System, Z, Survey, Leaver, Cause, Chronicled, Note,
				default(Simulation.City.KingdomResidentDestructionAuthorization));
		}

		internal static bool EmigrateAuthorized(KingdomSystem System, Zone Z,
			GameObject Leaver, string Cause,
			Simulation.City.KingdomResidentDestructionAuthorization Authorization)
		{
			return EmigrateCore(System, Z, null, Leaver, Cause, true, null, Authorization);
		}

		private static bool EmigrateCore(KingdomSystem System, Zone Z, KingdomSurvey Survey,
			GameObject Leaver, string Cause, bool Chronicled, string Note,
			Simulation.City.KingdomResidentDestructionAuthorization Authorization)
		{
			if (!KingdomMaster.NewWorkAllowed(System)) return false;
			if (Survey == null) Survey = KingdomSurvey.ActiveFor(Z);
			if (Simulation.City.KingdomResidents.OnRollCount(System)
				<= KingdomRules.LoyalCoreSettlers)
			{
				return false;
			}
			GameObject leaver = null;
			if (Leaver != null)
			{
				// A named departure still answers to the same law as any other: the settlement
				// never empties itself, and a settler the machinery would not take is one who
				// stays and is asked again next pass.
				if (KingdomCitizenship.BelongsTo(System, Leaver)
					&& Leaver.GetIntProperty("KingdomBorn") == 1 && Leaver.GetIntProperty("VillageMerchant") == 0 && !Leaver.IsPlayer() && !Leaver.IsPlayerLed()
					&& !Simulation.City.KingdomPhysicalHappenings.IsStaged(Leaver)
					&& !PreparedMarketHandoffParty(Leaver, Survey)
					&& CanPrepareGenericEmigrate(System, Leaver, Authorization))
				{
					leaver = Leaver;
				}
			}
			else
			{
				IEnumerable<GameObject> candidates = Survey != null
					? (IEnumerable<GameObject>)Survey.Settlers : KingdomSurvey.ObjectsFor(Z);
				foreach (GameObject item in candidates)
				{
					if (KingdomCitizenship.BelongsTo(System, item)
						&& item.GetIntProperty("KingdomBorn") == 1 && item.GetIntProperty("VillageMerchant") == 0 && !item.IsPlayer() && !item.IsPlayerLed()
						&& !Simulation.City.KingdomPhysicalHappenings.IsStaged(item)
						&& !PreparedMarketHandoffParty(item, Survey)
						&& CanPrepareGenericEmigrate(System, item, Authorization))
					{
						leaver = item;
						break;
					}
				}
			}
			if (leaver == null)
			{
				return false;
			}
			if (!KingdomResidentDepartureRuntime.TryBegin(System, leaver, Cause,
				Chronicled, Note, Authorization,
				out Simulation.City.KingdomResidentRow _, out string failure))
			{
				KingdomLog.Log("emigrate: exact resident departure refused ("
					+ (failure ?? "unknown failure") + ")");
				return false;
			}
			return true;
		}

		/// <summary>An exact open market handoff owns both bodies until commit or terminal abort.
		/// Emigration cannot erase either durable endpoint between retry cuts.</summary>
		internal static bool PreparedMarketHandoffParty(GameObject Body, KingdomSurvey Survey)
		{
			if (!GameObject.Validate(Body)) return false;
			string id = Body.IDIfAssigned;
			r_KingdomLegendaryMarketProjection own =
				Body.GetPart<r_KingdomLegendaryMarketProjection>();
			if (own?.HandoffPrepared == 1 && own.BodyObjectId == id) return true;
			r_KingdomMarketHandoffSourceProjection sourceOwn =
				Body.GetPart<r_KingdomMarketHandoffSourceProjection>();
			if (sourceOwn != null && (sourceOwn.SourceBodyObjectId == id
				|| sourceOwn.TargetBodyObjectId == id)) return true;
			for (int i = 0; Survey != null && i < Survey.Objects.Count; i++)
			{
				r_KingdomLegendaryMarketProjection marker =
					Survey.Objects[i]?.GetPart<r_KingdomLegendaryMarketProjection>();
				if (marker?.HandoffPrepared == 1
					&& (marker.BodyObjectId == id || marker.PriorBodyObjectId == id)) return true;
				r_KingdomMarketHandoffSourceProjection source = Survey.Objects[i]?
					.GetPart<r_KingdomMarketHandoffSourceProjection>();
				if (source != null && (source.SourceBodyObjectId == id
					|| source.TargetBodyObjectId == id)) return true;
			}
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Body.CurrentZone))
			{
				r_KingdomLegendaryMarketProjection marker =
					item?.GetPart<r_KingdomLegendaryMarketProjection>();
				if (marker?.HandoffPrepared == 1
					&& (marker.BodyObjectId == id || marker.PriorBodyObjectId == id)) return true;
				r_KingdomMarketHandoffSourceProjection source = item?
					.GetPart<r_KingdomMarketHandoffSourceProjection>();
				if (source != null && (source.SourceBodyObjectId == id
					|| source.TargetBodyObjectId == id)) return true;
			}
			return false;
		}

		internal static bool CanGenericEmigrate(KingdomSystem System, GameObject Body,
			Simulation.City.KingdomResidentDestructionAuthorization Authorization =
				default(Simulation.City.KingdomResidentDestructionAuthorization))
		{
			int residentId = Simulation.City.KingdomResidents.IdOf(Body);
			return Simulation.City.KingdomResidentTransitionAuthority
				.CanDestroyResidentBody(System, Body, residentId, Authorization);
		}

		internal static bool CanPrepareGenericEmigrate(KingdomSystem System, GameObject Body,
			Simulation.City.KingdomResidentDestructionAuthorization Authorization =
				default(Simulation.City.KingdomResidentDestructionAuthorization))
		{
			int residentId = Simulation.City.KingdomResidents.IdOf(Body);
			return Simulation.City.KingdomResidentTransitionAuthority
				.CanPrepareResidentBodyDestruction(System, Body, residentId, Authorization);
		}

		/// <summary>An open handoff temporarily freezes both resident identities. Completed native
		/// traders remain lawful heirs; accession retires only their TAF civic projection.</summary>
		internal static bool SuccessorMarketBlocked(GameObject Body, KingdomSurvey Survey)
		{
			return !GameObject.Validate(Body) || PreparedMarketHandoffParty(Body, Survey);
		}
	}
}
