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

		public static void OnZoneActivated(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			// Addendum 7: a grand work wants its yard headed by a named notable. KingdomMaterials
			// owns that build gate and must not decide who holds an office, so it asks us instead;
			// null there means "no office layer installed, do not enforce", and this assignment is
			// what turns the XL heading rule on. Installed before the option gate on purpose: the
			// memory option decides whether the settlement keeps a roll of its dead, not whether a
			// great work is judged by who heads it.
			KingdomMaterials.HeadedProbe = KingdomReach.IsHeaded;
			if (!System.Founded || Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			if (Survey == null) return;
			TagCitizens(System, Survey);
			if (!Enabled) return;
			HonourDead(System, Survey);
			UpdateOffice(System, Z, Survey);
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
				if (Citizen == null)
				{
					return;
				}
				KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
				if (!system.Founded)
				{
					return;
				}
				string expeditionFailure;
				if (!Simulation.City.KingdomExpeditions.TryPrepareResidentDeath(system, Citizen,
					The.Game == null ? 0L : The.Game.TimeTicks, out expeditionFailure))
				{
					KingdomLog.Log("expedition: dying resident terminal receipt waits ("
						+ (expeditionFailure ?? "unknown failure") + ")");
				}
				KingdomOfficeRules.DeathCause cause = KingdomOfficeRules.ClassifyCause(
					KillerIsPlayer: Killer != null && Killer.IsPlayer(),
					KillerIsRaider: Killer != null && Killer.GetIntProperty("KingdomRaider") == 1,
					KillerKnown: Killer != null);
				Simulation.City.KingdomStandingCause standingCause =
					(Simulation.City.KingdomStandingCause)((int)cause
						+ (int)Simulation.City.KingdomStandingCause.Unwitnessed);
				Simulation.City.KingdomResidentRow former;
				if (!Simulation.City.KingdomResidents.TryMarkDead(system, Citizen, standingCause,
					out former)) return;
				string citizenshipFailure;
				if (!KingdomCitizenship.TryRemove(system, Citizen,
					KingdomCitizenshipRemovalReason.Death, out citizenshipFailure))
				{
					// Death still belongs to the engine. A divergent civic slot is left untouched;
					// the receipt on the dying body records why no foreign value was overwritten.
					KingdomLog.Log("citizenship: death removal remained unresolved ("
						+ (citizenshipFailure ?? "unknown failure") + ")");
				}
				string name = former.Name;
				string origin = former.Origin;
				string arrived = former.Arrived;
				// Live identity and creed tallies are compatibility projections too, but still body-
				// keyed. Strike them only after exact resident-row transition commits.
				KingdomResidentIdentity.Forget(system, Citizen);
				KingdomCreed.Forget(system, Citizen);
				// Memory controls memorial/dead-history presentation. Living roster, binding,
				// identity, creed and citizenship authority must always observe exact death.
				if (!Enabled)
				{
					KingdomLog.Log("death: living authority retired " + former.Name
						+ " while settlement memory is disabled");
					return;
				}
				system.Dead++;
				system.DeadNames.Add(name);
				system.DeadOrigins.Add(origin);
				system.DeadArrived.Add(arrived);
				system.DeadCauses.Add(KingdomOfficeRules.CauseClause(cause));
				// Happenings owns the one semantic telling while enabled. It either stages exact
				// living mourners at a functional shrine, or persists a dated report with no proxy
				// rite. Disabled keeps the original immediate death line.
				bool owned = Simulation.City.KingdomHappenings.OwnDeathTelling(system, name,
					origin, cause, Citizen.CurrentZone,
					The.Game == null ? 0L : The.Game.TimeTicks);
				if (!owned)
				{
					KingdomChronicle.Record(system, KingdomOfficeRules.MourningChronicle(KingdomPresentation.Rich(name),
						KingdomPresentation.Rich(origin), KingdomPresentation.Rich(system.SeatName), cause));
					MessageQueue.AddPlayerMessage(KingdomVoices.Say(system,
						VoiceOccasion.CitizenLost,
						"{{r|" + KingdomOfficeRules.MourningMessage(KingdomPresentation.Rich(name), cause) + "}}"));
				}
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
		private static void TagCitizens(KingdomSystem System, KingdomSurvey Survey)
		{
			for (int i = 0; i < Survey.CitizenBodies.Count; i++)
			{
				GameObject item = Survey.CitizenBodies[i];
				if (item.GetIntProperty("KingdomBorn") == 1)
					item.RequirePart<r_KingdomCitizenLegacy>();
				if (item.GetPart<r_KingdomCitizenship>() == null)
				{
					string failure;
					KingdomCitizenship.ObserveLegacy(System, item, out failure);
				}
			}
		}

		/// <summary>
		/// Cuts the earliest unhonoured name into the first built, unlinked cairn standing in this
		/// zone. At most one cairn per pass: if several stand unlinked, the rest wait for a later
		/// visit rather than all changing at once off-screen.
		/// </summary>
		private static void HonourDead(KingdomSystem System, KingdomSurvey Survey)
		{
			if (!KingdomOfficeRules.TryNextToHonour(System.DeadNames.Count, System.MemorialsRaised, out int index))
			{
				return;
			}
			for (int i = 0; i < Survey.Cairns.Count; i++)
			{
				GameObject item = Survey.Cairns[i];
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
					description.Short = KingdomOfficeRules.Epitaph(KingdomPresentation.Rich(name), origin, arrived, KingdomPresentation.Rich(System.SeatName), cause);
				}
				item.DisplayName = "the cairn of " + KingdomPresentation.Rich(name);
				item.SetStringProperty(MemorialForProperty, name);
				System.MemorialsRaised++;
				KingdomChronicle.Record(System, KingdomOfficeRules.MemorialChronicle(KingdomPresentation.Rich(name), KingdomPresentation.Rich(System.SeatName)));
				MessageQueue.AddPlayerMessage("{{G|The cairn is cut with a name: " + KingdomPresentation.Rich(name) + ".}}");
				KingdomLog.Log("memorial: " + name + " honoured at " + System.SeatName);
				return;
			}
		}

		/// <summary>
		/// Notices when the office's holder &mdash; always oldest authoritative resident row &mdash;
		/// has changed, and moves the title. The new holder must
		/// actually be found standing in this zone before anything is committed: a settlement
		/// claiming more than one zone can have its longest-serving settler standing in a claimed
		/// zone other than the one that just activated, and this mod would rather try again on a
		/// later visit than announce a title it could not actually place.
		/// </summary>
		private static void UpdateOffice(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			Simulation.City.KingdomResidentRow headRow;
			bool hasHead = Simulation.City.KingdomResidents.TryHead(System, out headRow);
			string head = hasHead ? headRow.Name : null;
			int headId = hasHead ? headRow.ResidentId : 0;
			// Old saves remember only the holder's name. Adopt the exact head identity without
			// announcing a fictional succession on first load.
			if (System.OfficeHolderResidentId == 0 && hasHead
				&& string.Equals(System.OfficeHolderName, head, StringComparison.Ordinal))
			{
				System.OfficeHolderResidentId = headId;
				return;
			}
			if (System.OfficeHolderResidentId == headId
				&& (headId != 0 || string.IsNullOrEmpty(System.OfficeHolderName)))
			{
				System.OfficeHolderName = head;
				return;
			}
			KingdomOfficeRules.OfficeTransition transition = KingdomOfficeRules.ClassifyTransition(System.OfficeHolderName, head);
			if (transition == KingdomOfficeRules.OfficeTransition.None
				&& System.OfficeHolderResidentId != headId)
				transition = KingdomOfficeRules.OfficeTransition.Passed;
			if (transition == KingdomOfficeRules.OfficeTransition.None)
			{
				return;
			}
			string title = KingdomOfficeRules.ChooseTitle(System.SeatName);
			GameObject holder = null;
			if (head != null)
			{
				holder = Survey.FindCitizen(headRow.ResidentId);
				if (holder == null)
				{
					return;
				}
				holder.RequirePart<SocialRoles>().RequireRole(title + " of " + KingdomPresentation.Rich(System.SeatName));
				// W4 lane 5. The engine's own naming grammar, and only that: an epithet out of
				// Naming.xml under vanilla's Mayor scope, with none of HeroMaker's combat
				// statistics. See KingdomNotables for the survey and the ruling.
				KingdomNotables.Mint(System, holder);
			}
			System.OfficeHolderName = head;
			System.OfficeHolderResidentId = headId;
			string chronicle = KingdomOfficeRules.TransitionChronicle(transition, title,
				(head == null) ? null : KingdomPresentation.Rich(
					KingdomNotables.HolderName(System)),
				KingdomPresentation.Rich(System.SeatName));
			if (string.IsNullOrEmpty(chronicle))
			{
				return;
			}
			KingdomChronicle.Record(System, chronicle);
			MessageQueue.AddPlayerMessage("{{W|" + XRL.Language.Grammar.InitCap(chronicle) + ".}}");
			KingdomLog.Log("office: " + transition + " title=" + title + " holder=" + (head ?? "-"));
			if (head != null)
			{
				// The holder and where they sleep go with the name: Addendum 4 shades the
				// settlement's equilibrium by the Prefers their own quarters happen to meet, and
				// that half is nothing at all for a notable nobody has housed yet.
				KingdomCeremony.OnOfficeHolderNamed(System, Z, title, head, holder, KingdomLodging.HomeDesignKeyOf(Z, holder));
			}
		}

	}
}
