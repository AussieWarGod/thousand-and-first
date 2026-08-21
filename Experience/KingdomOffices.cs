using System;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst;

namespace ThousandAndFirst
{
	/// <summary>
	/// The engine-coupled half of the settlement's memory of its own people (see
	/// <see cref="KingdomOfficeRules"/> for the arithmetic and the prose). Three jobs, all run
	/// from the one <c>ZoneActivatedEvent</c> pass like everything else in the mod:
	/// <list type="bullet">
	/// <item>tag newly grown settlers so the settlement learns of their death the moment it
	/// happens, not by guessing from who is no longer standing around;</item>
	/// <item>cut the next unhonoured name into a built, unlinked cairn;</item>
	/// <item>notice when the office &mdash; always whoever has served longest &mdash; has changed
	/// hands, and say so once.</item>
	/// </list>
	/// None of it is a job the founder must do. A settlement that never builds a cairn simply
	/// keeps its dead in the roll unhonoured; a settlement of one keeps no office at all. Neither
	/// changes what the settlement produces.
	/// </summary>
	public static class KingdomOffices
	{
		public static bool Enabled => Options.GetOption("r_TAF_OptionMemory") != "No";

		/// <summary>The one blueprint a completed cairn can be. Named here rather than inferred,
		/// the same way <see cref="r_KingdomScaffold.LarderBlueprint"/> is.</summary>
		private const string CairnBlueprint = "r_KingdomCairn";

		/// <summary>String property marking a cairn already cut with a name, so a second pass
		/// never relinks or overwrites one that already speaks for someone.</summary>
		private const string MemorialForProperty = "KingdomMemorialFor";

		public static void OnZoneActivated(KingdomSystem System, Zone Z)
		{
			if (!Enabled || !System.Founded || Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			TagCitizens(System, Z);
			HonourDead(System, Z);
			UpdateOffice(System, Z);
		}

		/// <summary>
		/// Called by every citizen death, from <see cref="r_KingdomCitizenLegacy"/>: the one place
		/// a settler is struck from the living roster and added to the permanent dead roll. Never
		/// called from a census &mdash; only from the engine's own report that this exact object
		/// died, which is the only account of a death this mod is willing to write into the
		/// chronicle as fact rather than inference.
		/// </summary>
		/// <param name="Citizen">The settler who died.</param>
		/// <param name="Killer">Whoever the engine reports killed them, or null if unwitnessed.</param>
		public static void RecordDeath(GameObject Citizen, GameObject Killer)
		{
			KingdomSystem.Guard("citizen death", delegate
			{
				if (!Enabled || Citizen == null)
				{
					return;
				}
				KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
				if (!system.Founded)
				{
					return;
				}
				string name = Citizen.GetStringProperty("KingdomName");
				if (string.IsNullOrEmpty(name))
				{
					return;
				}
				int at = system.RosterNames.IndexOf(name);
				if (at < 0)
				{
					// Not on the living roll of the seated city - already gone by some other path
					// (emigration, exile), or never a named settler at all. Nothing to record twice.
					return;
				}
				string origin = (at < system.RosterOrigins.Count) ? system.RosterOrigins[at] : "";
				string arrived = (at < system.RosterArrived.Count) ? system.RosterArrived[at] : "";
				system.RosterNames.RemoveAt(at);
				if (at < system.RosterOrigins.Count)
				{
					system.RosterOrigins.RemoveAt(at);
				}
				if (at < system.RosterArrived.Count)
				{
					system.RosterArrived.RemoveAt(at);
				}
				if (!string.IsNullOrEmpty(origin))
				{
					system.OriginCounts.TryGetValue(origin, out var count);
					if (count > 0)
					{
						system.OriginCounts[origin] = count - 1;
					}
				}
				KingdomCreed.Forget(system, Citizen);
				if (system.Population > 0)
				{
					system.Population--;
				}
				system.Dead++;
				KingdomOfficeRules.DeathCause cause = KingdomOfficeRules.ClassifyCause(
					KillerIsPlayer: Killer != null && Killer.IsPlayer(),
					KillerIsRaider: Killer != null && Killer.GetIntProperty("KingdomRaider") == 1,
					KillerKnown: Killer != null);
				system.DeadNames.Add(name);
				system.DeadOrigins.Add(origin);
				system.DeadArrived.Add(arrived);
				system.DeadCauses.Add(KingdomOfficeRules.CauseClause(cause));
				KingdomChronicle.Record(system, KingdomOfficeRules.MourningChronicle(name, origin, system.SeatName, cause));
				MessageQueue.AddPlayerMessage("{{r|" + KingdomOfficeRules.MourningMessage(name, cause) + "}}");
				KingdomLog.Log("death: " + name + " of " + (string.IsNullOrEmpty(origin) ? "-" : origin) + " cause=" + cause + " pop now " + system.Population);
			});
		}

		/// <summary>
		/// Marks every grown settler present with the part that reports their death. Idempotent
		/// (<c>RequirePart</c> never duplicates), and cheap enough to run on every pass the same
		/// way <c>KingdomGrowth.Emigrate</c> already scans the whole zone. Run before
		/// <see cref="HonourDead"/> and <see cref="UpdateOffice"/> so a settler spawned earlier in
		/// this very pass is already covered before the founder's next turn.
		/// </summary>
		private static void TagCitizens(KingdomSystem System, Zone Z)
		{
			foreach (GameObject item in Z.GetObjects())
			{
				if (item.GetIntProperty("KingdomBorn") == 1)
				{
					item.RequirePart<r_KingdomCitizenLegacy>();
				}
			}
		}

		/// <summary>
		/// Cuts the earliest unhonoured name into the first built, unlinked cairn standing in this
		/// zone. At most one cairn per pass: if several stand unlinked, the rest wait for a later
		/// visit rather than all changing at once off-screen.
		/// </summary>
		private static void HonourDead(KingdomSystem System, Zone Z)
		{
			if (!KingdomOfficeRules.TryNextToHonour(System.DeadNames.Count, System.MemorialsRaised, out int index))
			{
				return;
			}
			foreach (GameObject item in Z.GetObjects())
			{
				if (item.GetIntProperty("KingdomBuilt") != 1 || item.Blueprint != CairnBlueprint)
				{
					continue;
				}
				if (!string.IsNullOrEmpty(item.GetStringProperty(MemorialForProperty)))
				{
					continue;
				}
				string name = System.DeadNames[index];
				string origin = (index < System.DeadOrigins.Count) ? System.DeadOrigins[index] : "";
				string arrived = (index < System.DeadArrived.Count) ? System.DeadArrived[index] : "";
				string cause = (index < System.DeadCauses.Count) ? System.DeadCauses[index] : KingdomOfficeRules.CauseClause(KingdomOfficeRules.DeathCause.Unknown);
				Description description = item.GetPart<Description>();
				if (description != null)
				{
					description.Short = KingdomOfficeRules.Epitaph(name, origin, arrived, System.SeatName, cause);
				}
				item.DisplayName = "the cairn of " + name;
				item.SetStringProperty(MemorialForProperty, name);
				System.MemorialsRaised++;
				KingdomChronicle.Record(System, KingdomOfficeRules.MemorialChronicle(name, System.SeatName));
				MessageQueue.AddPlayerMessage("{{G|The cairn is cut with a name: " + name + ".}}");
				KingdomLog.Log("memorial: " + name + " honoured at " + System.SeatName);
				return;
			}
		}

		/// <summary>
		/// Notices when the office's holder &mdash; always <c>RosterNames[0]</c>, the settler who
		/// has served longest &mdash; has changed, and moves the title. The new holder must
		/// actually be found standing in this zone before anything is committed: a settlement
		/// claiming more than one zone can have its longest-serving settler standing in a claimed
		/// zone other than the one that just activated, and this mod would rather try again on a
		/// later visit than announce a title it could not actually place.
		/// </summary>
		private static void UpdateOffice(KingdomSystem System, Zone Z)
		{
			string head = (System.RosterNames.Count > 0) ? System.RosterNames[0] : null;
			KingdomOfficeRules.OfficeTransition transition = KingdomOfficeRules.ClassifyTransition(System.OfficeHolderName, head);
			if (transition == KingdomOfficeRules.OfficeTransition.None)
			{
				return;
			}
			string title = KingdomOfficeRules.ChooseTitle(System.SeatName);
			if (head != null)
			{
				GameObject holder = FindCitizen(Z, head);
				if (holder == null)
				{
					return;
				}
				holder.RequirePart<SocialRoles>().RequireRole(title + " of " + System.SeatName);
			}
			System.OfficeHolderName = head;
			string chronicle = KingdomOfficeRules.TransitionChronicle(transition, title, head, System.SeatName);
			if (string.IsNullOrEmpty(chronicle))
			{
				return;
			}
			KingdomChronicle.Record(System, chronicle);
			MessageQueue.AddPlayerMessage("{{W|" + XRL.Language.Grammar.InitCap(chronicle) + ".}}");
			KingdomLog.Log("office: " + transition + " title=" + title + " holder=" + (head ?? "-"));
			if (head != null)
			{
				KingdomCeremony.OnOfficeHolderNamed(System, Z, title, head);
			}
		}

		private static GameObject FindCitizen(Zone Z, string Name)
		{
			foreach (GameObject item in Z.GetObjects())
			{
				if (item.GetIntProperty("KingdomBorn") == 1 && item.GetStringProperty("KingdomName") == Name)
				{
					return item;
				}
			}
			return null;
		}
	}
}

// The engine resolves an XML <part Name="X"/> as the single type "XRL.World.Parts.X" (see
// Growth/KingdomScaffold.cs for the full citation). r_KingdomCitizenLegacy is never named from
// XML - it is attached in code via RequirePart<T> - but it lives here regardless, so it is never
// the exception a future XML reference trips over.
namespace XRL.World.Parts
{
	/// <summary>
	/// Attached to every grown settler by <see cref="ThousandAndFirst.KingdomOffices"/> so the
	/// settlement learns of a citizen's death the moment the engine reports it, rather than
	/// inferring it from absence &mdash; a census cannot tell a dead settler from one who simply
	/// wandered to another claimed zone of the same territory. Carries no state of its own.
	/// </summary>
	[Serializable]
	public class r_KingdomCitizenLegacy : IPart
	{
		public override bool WantEvent(int ID, int cascade)
		{
			if (base.WantEvent(ID, cascade))
			{
				return true;
			}
			return ID == BeforeDeathRemovalEvent.ID;
		}

		public override bool HandleEvent(BeforeDeathRemovalEvent E)
		{
			KingdomOffices.RecordDeath(ParentObject, E.Killer);
			return base.HandleEvent(E);
		}
	}
}
