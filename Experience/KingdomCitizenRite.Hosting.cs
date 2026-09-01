using Qud.API;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomCitizenRite
	{
		/// <summary>
		/// Makes one citizen a host of the rite.
		/// <para>
		/// Preconditions: a founded realm. Side effects: as <see cref="Observe"/>. Failure mode:
		/// returns the verdict that stopped it, having changed nothing.
		/// </para>
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="Citizen">The settler.</param>
		/// <param name="Liquid">The ritual liquid that could not be poured, when the verdict is
		/// <see cref="CitizenRiteVerdict.UnknownLiquid"/>; null otherwise. Reported rather than
		/// re-derived, because the faction that was judged is the SETTLER'S base allegiance and
		/// need not be the seated realm's.</param>
		/// <returns>What the judgment was. <see cref="CitizenRiteVerdict.Host"/> means they are
		/// now one, whether or not this call is what made them one.</returns>
		public static CitizenRiteVerdict Host(KingdomSystem System, GameObject Citizen, out string Liquid)
		{
			Liquid = null;
			bool founded = System != null && System.Founded;
			bool citizen = Citizen != null && KingdomCitizenship.BelongsTo(System, Citizen)
				&& !Citizen.IsPlayer();
			bool body = Citizen != null && Citizen.Brain != null;
			string faction = (citizen && body) ? Citizen.GetPrimaryFaction(Base: true) : null;
			bool known = !string.IsNullOrEmpty(faction) && Factions.Exists(faction);
			string liquid = known ? Factions.Get(faction).WaterRitualLiquid : null;
			// An empty ritual liquid is safe and is NOT a refusal: Brain only layers the faction's
			// value over the event when it is non-empty (D/XRL/World/Parts/Brain.cs:2102-2109), so
			// the engine's own "water" default stands. Only a liquid that was NAMED and does not
			// exist is fatal.
			bool pourable = string.IsNullOrEmpty(liquid) || LiquidVolume.GetLiquid(liquid) != null;
			CitizenRiteVerdict verdict = KingdomCitizenRiteRules.Judge(founded, citizen, body, known, pourable);
			if (verdict != CitizenRiteVerdict.Host)
			{
				if (verdict == CitizenRiteVerdict.UnknownLiquid)
				{
					Liquid = liquid;
				}
				return verdict;
			}
			if (!TryProjection(System, Citizen,
				out r_KingdomCitizenRiteProjection projection, out string projectionFailure))
			{
				KingdomLog.Log("citizen rite: exact host provenance refused ("
					+ (projectionFailure ?? "unknown failure") + ")");
				return CitizenRiteVerdict.NoBody;
			}
			GivesRep rep = Citizen.GetPart<GivesRep>();
			bool addedRep = rep == null;
			if (addedRep)
			{
				rep = Citizen.AddPart<GivesRep>();
				// The related-faction table is what carries the rite's secondary awards and its
				// hates (D/XRL/World/Conversations/Parts/WaterRitual.cs:174-209). Filled the way
				// the engine fills a village warden's (D/XRL/World/ZoneBuilders/VillageCoda.cs:558).
				rep.FillInRelatedFactions(Initial: true);
			}
			if (!ObserveGivesRep(projection, rep, addedRep, out projectionFailure)
				|| !Speak(System, Citizen, projection, out projectionFailure))
			{
				KingdomLog.Log("citizen rite: exact native host projection refused ("
					+ (projectionFailure ?? "unknown failure") + ")");
				return CitizenRiteVerdict.NoBody;
			}
			if (Citizen.GetIntProperty(HostProperty) != 1)
			{
				Citizen.SetIntProperty(HostProperty, 1);
				KingdomLog.Log("citizen rite: " + (Citizen.GetStringProperty("KingdomName") ?? Citizen.ShortDisplayName)
					+ " hosts the rite for " + faction);
			}
			return CitizenRiteVerdict.Host;
		}
	}
}
