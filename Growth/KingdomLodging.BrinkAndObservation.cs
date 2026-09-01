using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomLodging
	{
		private static void RunRoofBrink(KingdomSystem System, Zone Z, GameObject Resident,
			string ResidentName, string Spoken = null)
		{
			if (string.IsNullOrEmpty(ResidentName))
			{
				// Somebody the roll does not carry: a founding citizen, or a person the settlement
				// never named. The brink names its subject, so an unnamed resident simply never
				// enters it and never leaves for want of a roof. Staying is the safe answer to a
				// question the registers cannot record, and it is the one taken here.
				return;
			}
			long now = (The.Game != null) ? The.Game.TimeTicks : 0L;
			// Recorded at the tick the roof was lost. Usually that is this pass; when a slide
			// condemned the house days back it is that breakpoint's own tick, pre-recorded by
			// RecordCondemnedRoofBrink, and the announcement quotes the honest elapsed either way.
			KingdomBrink.Record(Resident, BrinkKind.Roof, now, null, 0);
			bool here = KingdomWord.StandsIn(Z);
			if (KingdomBrink.MarkWarned(Resident, BrinkKind.Roof, now))
			{
				// The day the word goes out is never the day they go: the window starts here, and
				// the whole of it is still in front of the founder.
				KingdomBrink.Announce(System, BrinkKind.Roof, ResidentName, null,
					KingdomBrink.Of(Resident, BrinkKind.Roof), now, here, System.SeatName, Spoken);
				return;
			}
			BrinkRecord brink = KingdomBrink.Of(Resident, BrinkKind.Roof);
			if (!KingdomBrinkRules.WindowSpent(BrinkKind.Roof, brink.WarnedTick, now))
			{
				return;
			}
			long went = KingdomBrinkRules.ExpiryTick(BrinkKind.Roof, brink.WarnedTick);
			string leaving = KingdomLodgingRules.LeavingLine(
				KingdomPresentation.Rich(ResidentName),
				KingdomBrinkRules.DaysStood(brink.ReachedTick, went))
				+ KingdomBrinkRules.FiredClause(KingdomBrinkRules.DaysStood(went, now));
			int residentId = Simulation.City.KingdomResidents.IdOf(Resident);
			if (!KingdomLabCivicRuntime.TryAuthorizeDeparture(System, Z, Resident,
				out Simulation.City.KingdomResidentDestructionAuthorization authorization)) return;
			if (KingdomGrowth.EmigrateAuthorized(System, Z, Resident,
				KingdomLodgingRules.DepartureCause, authorization))
			{
				KingdomLabCivicRuntime.ObserveDeparture(System, Z, Resident, residentId);
				KingdomWord.Aftermath(System, System.SeatName, here, leaving);
				KingdomBrink.Lift(Resident, BrinkKind.Roof);
				return;
			}
			// The settlement would not let them go &mdash; they are the last of the loyal core, or
			// the emigration machinery could not take them. The window stays spent and is tried
			// again on the next resolve rather than being reset, so nothing is lost and nobody is
			// told they are going by a settlement that then kept them.
		}

		// The per-city LodgingGrace map this file used to keep is RETIRED. A settler's window now
		// lives on the settler (KingdomBrink), which fixes two things at once: two settlers of the
		// same name in two cities no longer share one entry, and a departed settler's window
		// cannot be inherited by a later settler of the same name, because it walks out of the
		// settlement inside them. Nothing needs pruning, so nothing prunes. The field itself is
		// KingdomSystem's and KingdomSettlement's to remove.

		// --- Facts about people and places ------------------------------------------------

		private static List<string> SelfTagsOf(QolProfile Profile)
		{
			return new List<string>(KingdomQolRules.SelfTags(Profile));
		}

		/// <summary>Purely projects the ordinary settlement pass: standing assignments keep
		/// their beds, then every unassigned or stale-home resident is seated in normal resident
		/// order. No property, brink, Chronicle, ledger, or cohabitation state is changed.</summary>
		private static Dictionary<string, List<GameObject>> ProjectedOccupancy(Zone Z,
			KingdomBenefitIndex Benefits)
		{
			Dictionary<string, List<GameObject>> result =
				new Dictionary<string, List<GameObject>>(StringComparer.Ordinal);
			HashSet<string> standing = new HashSet<string>(StringComparer.Ordinal);
			List<GameObject> homes = HousingIn(Z, Benefits);
			for (int i = 0; i < homes.Count; i++)
			{
				string plot = homes[i].GetStringProperty(KingdomPlots.PlotIdProperty);
				if (!string.IsNullOrEmpty(plot) && !IsCondemned(homes[i])) standing.Add(plot);
			}
			List<GameObject> residents = ResidentsIn(Z);
			List<GameObject> unassigned = new List<GameObject>();
			for (int i = 0; i < residents.Count; i++)
			{
				string plot = residents[i].GetStringProperty(HomePlotIdProperty);
				if (standing.Contains(plot)) AddOccupant(result, plot, residents[i]);
				else unassigned.Add(residents[i]);
			}
			for (int i = 0; i < unassigned.Count; i++)
			{
				GameObject ignoredHome;
				KingdomLodgingRules.UnhousedReason ignoredReason;
				KingdomLodgingRules.Closeness ignoredRefusal;
				List<string> ignoredNeeds;
				string plot = ChooseHome(Z, unassigned[i], homes, result, Benefits, out ignoredHome,
					out ignoredReason, out ignoredRefusal, out ignoredNeeds);
				if (plot != null) AddOccupant(result, plot, unassigned[i]);
			}
			return result;
		}

		private static bool ObserveOccupantConflicts(List<string> Refuses,
			List<string> SelfTags, string Creed, List<GameObject> Occupants,
			KingdomLodgingRules.Closeness Quarters, out List<string> Evidence)
		{
			Evidence = new List<string>();
			bool any = false;
			if (Occupants == null) return false;
			for (int i = 0; i < Occupants.Count; i++)
			{
				GameObject occupant = Occupants[i];
				string occupantCreed = occupant.GetStringProperty(KingdomCreed.CreedProperty);
				int hostility = KingdomCreed.HostilityBetween(Creed, occupantCreed);
				QolProfile profile = KingdomQol.ProfileOf(occupant);
				List<string> needs = new List<string>(profile.Needs);
				List<string> prefers = new List<string>(profile.Prefers);
				List<string> refuses = new List<string>(profile.Refuses);
				List<string> selfTags = SelfTagsOf(profile);
				bool conflict = KingdomLodgingRules.Conflicts(Refuses, SelfTags,
					refuses, selfTags, hostility, Quarters);
				any |= conflict;
				needs.Sort(StringComparer.Ordinal); prefers.Sort(StringComparer.Ordinal);
				refuses.Sort(StringComparer.Ordinal); selfTags.Sort(StringComparer.Ordinal);
				Evidence.Add(ArrivalObservationHash(delegate(BinaryWriter writer)
				{
					WriteObservationString(writer, occupant.IDIfAssigned);
					WriteObservationString(writer, occupant.Blueprint);
					WriteObservationString(writer, occupantCreed);
					WriteObservationList(writer, needs); WriteObservationList(writer, prefers);
					WriteObservationList(writer, refuses); WriteObservationList(writer, selfTags);
					writer.Write(hostility); writer.Write((int)Quarters); writer.Write(conflict);
				}));
			}
			return any;
		}

		private static bool AnyOccupantConflicts(List<string> Refuses, List<string> SelfTags, string Creed, List<GameObject> Occupants, KingdomLodgingRules.Closeness Quarters)
		{
			for (int i = 0; i < Occupants.Count; i++)
			{
				GameObject occupant = Occupants[i];
				string occupantCreed = occupant.GetStringProperty(KingdomCreed.CreedProperty);
				// Addendum 4c: which creed feelings break a household is a question about the
				// household's own quarters, so the raw engine feeling is handed straight down and
				// the ladder in KingdomLodgingRules decides. The single floor that used to be
				// applied here -- only the flat -100 fault lines, never the standing -50 -- is
				// still exactly what a home at Closeness.Private asks, and is now the top rung of
				// four rather than the rule for every roof in the settlement.
				int hostility = KingdomCreed.HostilityBetween(Creed, occupantCreed);
				QolProfile theirs = KingdomQol.ProfileOf(occupant);
				List<string> occupantSelfTags = SelfTagsOf(theirs);
				List<string> occupantRefuses = new List<string>(theirs.Refuses);
				if (KingdomLodgingRules.Conflicts(Refuses, SelfTags, occupantRefuses, occupantSelfTags, hostility, Quarters))
				{
					return true;
				}
			}
			return false;
		}

		private static string ArrivalObservationHash(Action<BinaryWriter> Write)
		{
			if (Write == null) return null;
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = new BinaryWriter(stream,
				new UTF8Encoding(false, true), true))
			{
				WriteObservationString(writer, "taf:lodging-arrival-observation:v1");
				Write(writer); writer.Flush();
				using (SHA256 sha = SHA256.Create())
				{
					byte[] digest = sha.ComputeHash(stream.ToArray());
					StringBuilder text = new StringBuilder(64);
					for (int i = 0; i < digest.Length; i++)
						text.Append(digest[i].ToString("x2", CultureInfo.InvariantCulture));
					return text.ToString();
				}
			}
		}

		private static void WriteObservationString(BinaryWriter Writer, string Value)
		{
			if (Value == null) { Writer.Write(-1); return; }
			byte[] bytes = new UTF8Encoding(false, true).GetBytes(Value);
			Writer.Write(bytes.Length); Writer.Write(bytes);
		}

		private static void WriteObservationList(BinaryWriter Writer, List<string> Values)
		{
			Writer.Write(Values == null ? -1 : Values.Count);
			if (Values != null) for (int i = 0; i < Values.Count; i++)
				WriteObservationString(Writer, Values[i]);
		}

		private static void AnnounceUnhoused(KingdomSystem System, GameObject Resident, string ResidentName, KingdomLodgingRules.UnhousedReason Reason, KingdomLodgingRules.Closeness RoomiestRefused)
		{
			if (Resident.GetIntProperty(UnhousedAnnouncedProperty) == 1)
			{
				return;
			}
			Resident.SetIntProperty(UnhousedAnnouncedProperty, 1);
			// Addendum 4c names the quarters, so a founder hearing this once (7b) hears what to
			// build rather than only that somebody is outside.
			string line = KingdomLodgingRules.UnhousedLine(
				KingdomPresentation.Rich(ResidentName), Reason, RoomiestRefused);
			KingdomChronicle.Record(System, line);
			System.Ledger.Note("{{r|" + line + "}}");
		}

		private static void AddOccupant(Dictionary<string, List<GameObject>> Occupancy, string PlotId, GameObject Resident)
		{
			List<GameObject> list;
			if (!Occupancy.TryGetValue(PlotId, out list))
			{
				list = new List<GameObject>();
				Occupancy[PlotId] = list;
			}
			list.Add(Resident);
		}

		// The name the roll carries this person under, which is the key the grace is filed by and
		// the name the registers will write when they leave. Null for anybody the roll does not
		// carry.
		private static string RollNameOf(GameObject Resident)
		{
			string name = (Resident == null) ? null : Resident.GetStringProperty("KingdomName");
			return string.IsNullOrEmpty(name) ? null : name;
		}

		private static string NameOf(GameObject Resident)
		{
			string name = (Resident == null) ? null : Resident.GetStringProperty("KingdomName");
			if (!string.IsNullOrEmpty(name))
			{
				return name;
			}
			return (Resident == null) ? "" : Resident.BaseDisplayNameStripped;
		}

	}
}
