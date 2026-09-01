using System;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	[Serializable]
	public sealed partial class KingdomCharterPart : IPart
	{
		public const string COMMAND = "r_KingdomCharterMenu";

		public Guid ActivatedAbilityID = Guid.Empty;

		// Mid-session mod rebuilds mint a second assembly generation; the stale part shares
		// this name but not this Type, so RequirePart cannot see it. Purge by name.
		public override void Attach()
		{
			base.Attach();
			for (int num = ParentObject.PartsList.Count - 1; num >= 0; num--)
			{
				IPart part = ParentObject.PartsList[num];
				if (part != this && part.GetType().Name == "KingdomCharterPart")
				{
					ParentObject.PartsList.RemoveAt(num);
				}
			}
		}

		public override void Register(GameObject Object, IEventRegistrar Registrar)
		{
			Registrar.Register(COMMAND);
			base.Register(Object, Registrar);
		}

		public void EnsureAbility()
		{
			System.Collections.Generic.Dictionary<Guid, ActivatedAbilityEntry> abilities =
				ParentObject?.ActivatedAbilities?.AbilityByGuid;
			if (ActivatedAbilityID != Guid.Empty && abilities != null &&
				abilities.TryGetValue(ActivatedAbilityID, out var retained) &&
				retained != null && retained.Command == COMMAND)
			{
				return;
			}
			// Empty map entry, deleted GUID, or a GUID now naming another command is stale
			// serialized state. Clear only our pointer; never remove somebody else's ability.
			ActivatedAbilityID = Guid.Empty;
			if (abilities != null)
			{
				foreach (System.Collections.Generic.KeyValuePair<Guid, ActivatedAbilityEntry> item in abilities)
				{
					if (item.Value != null && item.Value.Command == COMMAND)
					{
						ActivatedAbilityID = item.Key;
						return;
					}
				}
			}
			ActivatedAbilityID = AddMyActivatedAbility("Charter", COMMAND, "Skills");
		}

		public void RemoveAbility()
		{
			RemoveMyActivatedAbility(ref ActivatedAbilityID);
		}

		public override bool FireEvent(Event E)
		{
			if (E.ID == COMMAND)
			{
				OpenMenu();
				string sealFailure;
				if (!KingdomSeal.TryStageSemanticSnapshot("charter actions", out sealFailure))
				{
					KingdomLog.Log("seal: charter actions remain pending (" + sealFailure + ")");
				}
			}
			return base.FireEvent(E);
		}

		public void OpenMenu()
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (!system.Founded)
			{
				Popup.Show("You rule nothing yet.");
				return;
			}
			while (true)
			{
				KingdomCharterMenuRoute[] routes = KingdomCharterMenuRules.RootEntries();
				int pick = Popup.PickOption(
					Title: KingdomPresentation.Rich(system.SeatName) + KingdomSettlement.VocationSuffix(system.Vocation),
					Intro: CharterAtAGlance(system), Options: RouteLabels(routes, system),
					Hotkeys: RouteHotkeys(routes), AllowEscape: true);
				if (pick < 0 || pick >= routes.Length)
				{
					return;
				}
				KingdomCharterMenuRoute route = routes[pick];
				if (route.Kind == KingdomCharterRouteKind.Chapter)
				{
					if (OpenChapter(system, route.Chapter))
					{
						return;
					}
				}
				else if (route.Kind == KingdomCharterRouteKind.Action && RunAction(system, route.Action))
				{
					return;
				}
			}
		}

		private string CharterAtAGlance(KingdomSystem System)
		{
			string need = KingdomReports.NextNeed(System, ParentObject?.CurrentZone);
			return "{{C|" + System.Stage + "}}  " + System.Population
				+ ((System.Population == 1) ? " settler" : " settlers")
				+ (string.IsNullOrEmpty(need) ? "" : ("\n{{W|Next need: " + need + "}}"));
		}

		private bool OpenChapter(KingdomSystem System, KingdomCharterChapter Chapter)
		{
			while (true)
			{
				KingdomCharterMenuRoute[] routes = KingdomCharterMenuRules.ChapterEntries(Chapter);
				int pick = Popup.PickOption(
					Title: KingdomCharterMenuRules.ChapterTitle(Chapter) + " of " + KingdomPresentation.Rich(System.SeatName),
					Options: RouteLabels(routes, System), Hotkeys: RouteHotkeys(routes),
					AllowEscape: true);
				if (pick < 0 || pick >= routes.Length || routes[pick].Kind == KingdomCharterRouteKind.Back)
				{
					return false;
				}
				if (routes[pick].Kind == KingdomCharterRouteKind.Action
					&& RunAction(System, routes[pick].Action))
				{
					return true;
				}
			}
		}

		private static char[] RouteHotkeys(KingdomCharterMenuRoute[] Routes)
		{
			char[] hotkeys = new char[Routes.Length];
			for (int i = 0; i < Routes.Length; i++) hotkeys[i] = Routes[i].Hotkey;
			return hotkeys;
		}

		private static string[] RouteLabels(KingdomCharterMenuRoute[] Routes, KingdomSystem System)
		{
			string[] labels = new string[Routes.Length];
			for (int i = 0; i < Routes.Length; i++)
			{
				KingdomCharterMenuRoute route = Routes[i];
				if (route.Kind == KingdomCharterRouteKind.Action
					&& route.Action == KingdomCharterAction.HearPetition)
				{
					string petitioner = KingdomPresentation.Rich(System.PetitionPetitioner);
					labels[i] = KingdomPetitions.IsAwaitingAnswer(System)
						? ("{{W|Hear " + petitioner + "}}")
						: (KingdomPetitions.IsAccepted(System)
							? ("{{G|Petition accepted: " + petitioner + "}}")
							: "{{K|No one is waiting to speak}}");
				}
				else if (route.Kind == KingdomCharterRouteKind.Action
					&& route.Action == KingdomCharterAction.FirstGuestCorrespondence)
				{
					labels[i] = KingdomFirstGuestRuntime.CharterLabel(System);
				}
				else if (route.Kind == KingdomCharterRouteKind.Action
					&& route.Action == KingdomCharterAction.GuestFeastRecord)
				{
					labels[i] = KingdomGuestFeastRuntime.CharterLabel(System);
				}
				else if (route.Kind == KingdomCharterRouteKind.Action
					&& route.Action == KingdomCharterAction.ManageCreed
					&& System.SettlementCount < 2 && System.Seceded == null)
				{
					labels[i] = "{{K|One city cannot fall out with itself}}";
				}
				else if (route.Kind == KingdomCharterRouteKind.Action
					&& !KingdomMaster.NewWorkAllowed(System)
					&& !KingdomCharterMenuRules.AvailableWhileSimulationPaused(route.Action))
				{
					labels[i] = route.Label + " {{K|[paused]}}";
				}
				else
				{
					labels[i] = route.Label;
				}
			}
			return labels;
		}

		/// <summary>Runs one old Charter verb inside exactly one governance scope.</summary>
		private bool RunAction(KingdomSystem System, KingdomCharterAction Action)
		{
			if (!ExternalOwnershipActionAllowed(System, Action,
				out string externalFailure))
			{
				Popup.Show("Civic work is paused on this ground: " + externalFailure);
				return false;
			}
			if (!KingdomMaster.NewWorkAllowed(System)
				&& !KingdomCharterMenuRules.AvailableWhileSimulationPaused(Action))
			{
				Popup.Show("Settlement simulation is paused by the master option. Records and committed recovery remain available; resume the realm before ordering new work.");
				return false;
			}
			KingdomGovernanceScope action = KingdomGovernanceScope.Begin(ParentObject);
			try
			{
				switch (Action)
				{
				case KingdomCharterAction.HearPetition: HearPetition(System); break;
				case KingdomCharterAction.Status: Popup.Show(KingdomReports.Status(System, ParentObject?.CurrentZone)); break;
				case KingdomCharterAction.Homecoming: ShowHomecoming(System); break;
				case KingdomCharterAction.ChronicleAndDynasty: OpenChronicleAndDynasty(System); break;
				case KingdomCharterAction.OutsiderChronicle: Popup.Show(KingdomReports.Chronicle(System, Outsider: true)); break;
				case KingdomCharterAction.Standings: Popup.Show(KingdomReports.Standings(System)); break;
				case KingdomCharterAction.SettlerRoll: Popup.Show(KingdomReports.Roll(System)); break;
				case KingdomCharterAction.StandingPolicy: SetPolicy(System); break;
				case KingdomCharterAction.DesignateDistrict: DesignateDistrict(System); break;
				case KingdomCharterAction.CommissionBuilding: CommissionBuilding(System); break;
				case KingdomCharterAction.AnswerThreat: AnswerThreat(System); break;
				case KingdomCharterAction.DedicateStores: DedicateVessel(System); break;
				case KingdomCharterAction.StrikeTradeCharter: StrikeTradeCharter(System); break;
				case KingdomCharterAction.SendManifest: LoadManifest(System); break;
				case KingdomCharterAction.ShareMeal: HoldSharedMeal(System); break;
				case KingdomCharterAction.CertifyMachine: CertifyMachine(System); break;
				case KingdomCharterAction.SetWaterDetail: SetWaterDetail(System); break;
				case KingdomCharterAction.ManagePlans: ManagePlans(System); break;
				case KingdomCharterAction.AdoptBuilding: AdoptBuilding(System); break;
				case KingdomCharterAction.ReleaseAdoption: ReleaseBuilding(System); break;
				case KingdomCharterAction.ManageCreed: ManageCreed(System); break;
				case KingdomCharterAction.KeepersKnowledge: KingdomZoning.ShowKeepers(System); break;
				case KingdomCharterAction.WorksAndTrades: KingdomYards.ShowWorksAndTrades(System); break;
				case KingdomCharterAction.NameBuilding: KingdomDesign.RenameBuilding(System, ParentObject); break;
				case KingdomCharterAction.GroundWork: GroundWork(System); break;
				case KingdomCharterAction.StrikeBuilding: StrikeBuilding(System); break;
				case KingdomCharterAction.PostPrice: KingdomBounty.OpenNotices(System, ParentObject); break;
				case KingdomCharterAction.ConvertPlot: KingdomSocket.OpenConvert(System, ParentObject); break;
				case KingdomCharterAction.RedressBuilding: KingdomSocket.OpenRedress(System, ParentObject); break;
				case KingdomCharterAction.ConsecrateShrine: KingdomFaith.OpenConsecration(System, ParentObject); break;
				case KingdomCharterAction.ShareWater: KingdomWaterRite.OpenRite(System, ParentObject); break;
				case KingdomCharterAction.ClaimGround: ClaimGround(System); break;
				case KingdomCharterAction.CityBook: Simulation.City.KingdomBookReport.Open(System); break;
				case KingdomCharterAction.TechMap: Popup.Show(KingdomTechMap.Draw(System)); break;
				case KingdomCharterAction.CityAsks: Popup.Show(KingdomAsks.Board(System)); break;
				case KingdomCharterAction.SalvageExpedition: Simulation.City.KingdomExpeditions.Open(System, ParentObject); break;
				case KingdomCharterAction.DesignateProperty: KingdomProperty.Open(System, ParentObject); break;
				case KingdomCharterAction.ManageNamedCook: KingdomNamedCook.Open(System, ParentObject); break;
				case KingdomCharterAction.ManageCivicOffice: KingdomOfficeRuntime.Open(System, ParentObject); break;
				case KingdomCharterAction.DedicateRemembrance: KingdomRemembranceRuntime.Open(System, ParentObject); break;
				case KingdomCharterAction.FirstGuestCorrespondence: KingdomFirstGuestRuntime.Open(System, ParentObject); break;
				case KingdomCharterAction.FirstFeastPractice: KingdomFirstFeastRuntime.Open(System, ParentObject); break;
				case KingdomCharterAction.PracticeAndVocation: KingdomCivicPracticeRuntime.OpenPracticeAndVocation(System, ParentObject); break;
				case KingdomCharterAction.CivicKnowledge: KingdomCivicKnowledgeRuntime.OpenCurrent(System, ParentObject); break;
				case KingdomCharterAction.BodyHistory: KingdomBodyHistoryRuntime.OpenCurrent(ParentObject, System); break;
				case KingdomCharterAction.GuestFeastRecord: KingdomGuestFeastRuntime.OpenRecord(System, ParentObject); break;
				case KingdomCharterAction.CivicCommitments: OpenCivicCommitments(System); break;
				case KingdomCharterAction.RecognizeArtifact: KingdomArtifactRecognitionCharterRuntime.Open(System, ParentObject); break;
				case KingdomCharterAction.FixedWitnessWorks: KingdomWitnessWorkCharterRuntime.Open(System, ParentObject); break;
				case KingdomCharterAction.InspectBuildingBenefits: InspectBuildingBenefits(System); break;
				case KingdomCharterAction.TrafficRecords: OpenPolityTrafficRecords(System); break;
				}
			}
			finally
			{
				action.Dispose();
			}
			return action.Committed;
		}

	}
}
