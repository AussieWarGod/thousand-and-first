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
		private static Dictionary<string, List<HouseholdMember>> ProjectedOccupancy(Zone Z,
			KingdomBenefitIndex Benefits)
		{
			List<GameObject> homes = HousingIn(Z, Benefits);
			var result = ReadHouseholds(Z, homes, out List<GameObject> unassigned);
			if (result == null) return null;
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
			List<string> SelfTags, string Creed, List<HouseholdMember> Occupants,
			KingdomLodgingRules.Closeness Quarters, out List<string> Evidence)
		{
			Evidence = new List<string>();
			bool any = false;
			if (Occupants == null) return false;
			for (int i = 0; i < Occupants.Count; i++)
			{
				HouseholdMember occupant = Occupants[i];
				bool conflict = MemberConflicts(Refuses, SelfTags, Creed, occupant, Quarters, out int hostility);
				any |= conflict;
				if (GameObject.Validate(occupant.Body))
				{
					Evidence.Add(PresentOccupantObservation(occupant.Body, hostility, Quarters, conflict));
					continue;
				}
				Evidence.Add(ArrivalObservationHash(delegate(BinaryWriter writer)
				{
					writer.Write(occupant.ResidentId);
					if (occupant.ResidentId <= 0) WriteObservationString(writer, occupant.BodyId);
					writer.Write(occupant.Known);
					if (occupant.Known)
					{
						WriteObservationString(writer, occupant.Facts.Creed);
						WriteObservationList(writer, new List<string>(occupant.Facts.Needs));
						WriteObservationList(writer, new List<string>(occupant.Facts.Refuses));
						WriteObservationList(writer, new List<string>(occupant.Facts.SelfTags));
						WriteObservationString(writer, occupant.Facts.BedId);
					}
					writer.Write(hostility); writer.Write((int)Quarters); writer.Write(conflict);
				}, "taf:lodging-reservation-observation:v1"));
			}
			return any;
		}

		private static bool AnyOccupantConflicts(List<string> Refuses, List<string> SelfTags, string Creed, List<HouseholdMember> Occupants, KingdomLodgingRules.Closeness Quarters)
		{
			foreach (HouseholdMember member in Occupants)
				if (MemberConflicts(Refuses, SelfTags, Creed, member, Quarters, out _)) return true;
			return false;
		}

		private static bool MemberConflicts(List<string> Refuses, List<string> SelfTags, string Creed,
			HouseholdMember Member, KingdomLodgingRules.Closeness Quarters, out int Hostility)
		{
			Hostility = Member.Known ? KingdomCreed.HostilityBetween(Creed, Member.Facts.Creed) : 0;
			return !Member.Known || KingdomLodgingRules.Conflicts(Refuses, SelfTags,
				new List<string>(Member.Facts.Refuses), new List<string>(Member.Facts.SelfTags), Hostility, Quarters);
		}

		private static string ArrivalObservationHash(Action<BinaryWriter> Write,
			string Domain = "taf:lodging-arrival-observation:v1")
		{
			if (Write == null) return null;
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = new BinaryWriter(stream,
				new UTF8Encoding(false, true), true))
			{
				WriteObservationString(writer, Domain);
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

		private static void AddOccupant(Dictionary<string, List<HouseholdMember>> Occupancy, string PlotId, GameObject Resident)
		{
			AddMember(Occupancy, PlotId, Member(Resident, PlotId));
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
