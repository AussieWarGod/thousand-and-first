#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public class KingdomShopStockSourceTests
	{
		[Test]
		public void StandingTransitionNeverCreatesDeletesOrRestocksWares()
		{
			string source = Read("Growth/KingdomGrowth.z18.StageAndShops.cs");
			string market = source.Substring(source.IndexOf("public static void RestockShops",
				StringComparison.Ordinal));
			StringAssert.Contains("NoAutomaticMarketStockAuthority", market);
			StringAssert.Contains("KingdomShopStockRules.SamePhysicalSet", market);
			StringAssert.Contains("RetireLegacyMarketOutputIntent", market);
			StringAssert.Contains("restocker.Chance = 0", market);
			StringAssert.Contains("restocker.RestockFrequency = long.MaxValue", market);
			StringAssert.DoesNotContain("PerformRestock", market);
			StringAssert.DoesNotContain("PopulationManager", market);
			StringAssert.DoesNotContain("AddObjectToInventory", market);
			StringAssert.DoesNotContain("Obliterate", market);
			StringAssert.DoesNotContain("NextAcknowledgementTier", market);
			int commit = market.IndexOf("System.ShopTier = Tier;",
				StringComparison.Ordinal);
			int publish = market.IndexOf("if (!PublishMarketTierAcknowledgement", commit,
				StringComparison.Ordinal);
			Assert.Greater(commit, 0); Assert.Greater(publish, commit);
			StringAssert.Contains("System.HasShopkeeper = false", market);
		}

		[Test]
		public void ServiceNeedsAcceptedStaffedPhysicalMarketAndExactOffice()
		{
			string office = Read("Growth/KingdomGrowth.z18b.MarketOffice.cs");
			string projection = Read("Growth/KingdomGrowth.z18c.MarketProjection.cs");
			string provider = Read("RuntimeData/ObjectBlueprints.xml");
			StringAssert.Contains("Survey.TryBenefits", office);
			StringAssert.Contains("KingdomBenefitCapabilities.Market", office);
			StringAssert.Contains("KingdomShopStockRules.EffectiveServiceTier", office);
			StringAssert.Contains("OfficeServiceEligible", office);
			StringAssert.Contains("KingdomMarketStockCustody.TryGather", office);
			StringAssert.Contains("Provides=\"taf:market\"", provider);
			StringAssert.Contains("Operation=\"Staffed\"", provider);
			StringAssert.Contains("KingdomMarketStockCustody.HasNativeStock", projection);
			StringAssert.DoesNotContain("RequirePart<GenericInventoryRestocker>", projection);
		}

		[Test]
		public void EmptyOfficeMarketAndNativeTradeHaveExactEventSeams()
		{
			string marker = Read("Experience/r_KingdomOfficeProjection.cs");
			StringAssert.Contains("AllowTradeWithNoInventoryEvent", marker);
			StringAssert.Contains("return false;", marker);
			StringAssert.Contains("TookEvent", marker);
			StringAssert.Contains("GetIntProperty(\"_stock\") == 1", marker);
			StringAssert.Contains("TryAdmitNativeTrade", marker);
			StringAssert.DoesNotContain("GiveDrams", marker);
			StringAssert.DoesNotContain("Create(", marker);
		}

		[Test]
		public void PlayerFacingOfficeIngressReprovesTheLivePhysicalProvider()
		{
			string marker = Read("Experience/r_KingdomOfficeProjection.cs");
			string authority = Read("Growth/KingdomMarketProviderAuthority.cs");
			string office = Read("Growth/KingdomGrowth.z18b.MarketOffice.cs");
			string projection = Read("Growth/KingdomGrowth.z18c.MarketProjection.cs");
			Assert.GreaterOrEqual(Occurrences(marker,
				"KingdomMarketProviderAuthority.TryProve"), 2);
			StringAssert.Contains("survey.InvalidateBenefits()", authority);
			StringAssert.Contains("TryMarketServiceStanding", authority);
			StringAssert.Contains("ExactLiveAuthority", authority);
			StringAssert.Contains("TryProveProjection", office);
			StringAssert.Contains("TryProveProjection", projection);
		}

		[Test]
		public void CustodySurvivesOfficeAndLegendaryWhileDetachedGoodsRetireMarks()
		{
			string custody = Read("Growth/KingdomMarketStockCustody.cs");
			string detachment = Read("Growth/KingdomMarketStockDetachment.cs");
			string projection = Read("Growth/KingdomMarketStockProjection.cs");
			string admission = Read("Growth/KingdomMarketStockAdmission.cs");
			string split = Read("Growth/KingdomMarketStockSplit.cs");
			string handoff = Read("Experience/KingdomGuestbook.z01b.MarketHandoff.cs");
			StringAssert.Contains("NativeStockProperty = \"_stock\"", custody);
			StringAssert.Contains("TrySealDeparting", custody);
			StringAssert.Contains("TryGather", custody);
			StringAssert.Contains("Rollback", custody);
			StringAssert.Contains("CanAdmitHeld", custody + admission);
			StringAssert.Contains("stale market custody blocks automatic admission", admission);
			StringAssert.Contains("KingdomMarketStockProtection.TryRetire(move.Item)", custody);
			StringAssert.Contains("TryCommitExternal", handoff);
			StringAssert.Contains("ExactTransferable", handoff);
			StringAssert.Contains("StockTransferTargetProperty", handoff);
			StringAssert.Contains("bool ours = OurCurrentMarketReceipt", handoff);
			StringAssert.Contains("if (ours || !string.IsNullOrEmpty(target)) return false", handoff);
			StringAssert.Contains("ItemSourceProperty", handoff + Read(
				"Experience/KingdomGuestbook.z01d.MarketHandoffSource.cs"));
			StringAssert.Contains("Item.GetIntProperty(\"_stock\") == 1", handoff + Read(
				"Experience/KingdomGuestbook.z01d.MarketHandoffSource.cs"));
			StringAssert.Contains("Survey.TryLoaded", detachment);
			StringAssert.Contains("KingdomShopStockRules.ClassifyLocation", detachment);
			StringAssert.Contains("KingdomMarketRemoval.TryPrepareTransaction", detachment);
			StringAssert.Contains("KingdomMarketRemoval.TryCommitTransaction", detachment);
			StringAssert.Contains("holder.IsAlive", detachment);
			StringAssert.Contains("!holder.IsPlayer()", detachment);
			StringAssert.Contains("KingdomCitizenship.BelongsTo(System, holder)", detachment);
			StringAssert.Contains("NeverStackProperty = \"NeverStack\"", detachment);
			StringAssert.Contains("StockOwnsNeverStackProperty", detachment);
			StringAssert.Contains("Item.GetIntProperty(Property) == 1", detachment);
			StringAssert.Contains("RequirePart<r_KingdomMarketStockProjection>", detachment);
			StringAssert.Contains("AddedToInventoryEvent", projection);
			StringAssert.Contains("TakenEvent", projection);
			StringAssert.Contains("DroppedEvent", projection);
			StringAssert.Contains("SameAs(IPart Part)", projection);
			StringAssert.Contains("CanGenerateStacked()", projection);
			StringAssert.Contains("SettlementIdForOwnedZone", projection);
			StringAssert.Contains("r_KingdomLegendaryMarketProjection", projection);
			StringAssert.Contains("PreparedTransferAuthority", projection);
			StringAssert.Contains("HarmonyPatch(typeof(GameObject), nameof(GameObject.SplitStack)", split);
			StringAssert.Contains("TryRepairNativeSplit", split);
			StringAssert.Contains("TryRetire(Remainder)", split);
			StringAssert.Contains("TryCommitLegendaryMarketProjection", handoff);
			StringAssert.DoesNotContain("RemoveIntProperty(\"_stock\")", detachment);
			StringAssert.DoesNotContain("Obliterate", custody + detachment + projection + handoff);
			Assert.Less(File.ReadAllLines(Path.Combine(TestMain.RepositoryRoot,
				"Growth/KingdomMarketStockCustody.cs")).Length, 300);
			Assert.Less(File.ReadAllLines(Path.Combine(TestMain.RepositoryRoot,
				"Growth/KingdomMarketStockDetachment.cs")).Length, 300);
			Assert.Less(File.ReadAllLines(Path.Combine(TestMain.RepositoryRoot,
				"Growth/KingdomMarketStockProjection.cs")).Length, 300);
			Assert.Less(File.ReadAllLines(Path.Combine(TestMain.RepositoryRoot,
				"Growth/KingdomMarketStockSplit.cs")).Length, 300);
			Assert.Less(File.ReadAllLines(Path.Combine(TestMain.RepositoryRoot,
				"Experience/KingdomGuestbook.z01b.MarketHandoff.cs")).Length, 300);
		}

		[Test]
		public void LegendaryHandoffAndRealmRemovalAreExplicitTransactions()
		{
			string handoff = Read("Experience/KingdomGuestbook.z01b.MarketHandoff.cs");
			string projection = Read("Growth/KingdomMarketStockProjection.cs");
			string removal = Read("Growth/KingdomMarketRemoval.Transaction.cs");
			string mutation = Read("Core/KingdomRealmRetirementGround.Mutation.cs");
			int seal = handoff.IndexOf("SealFiniteTrader(Trader, restocker, marketTier)",
				StringComparison.Ordinal);
			int prepare = handoff.IndexOf("TryPrepareLegendaryMarketProjection", seal,
				StringComparison.Ordinal);
			int transfer = handoff.IndexOf("TransferExactLocalMarketStock", prepare,
				StringComparison.Ordinal);
			int retirePrior = handoff.IndexOf("TryRetirePriorMarketAuthority", transfer,
				StringComparison.Ordinal);
			int complete = handoff.IndexOf("CompleteHandoff", retirePrior,
				StringComparison.Ordinal);
			Assert.Greater(seal, 0); Assert.Greater(prepare, seal);
			Assert.Greater(transfer, prepare); Assert.Greater(retirePrior, transfer);
			Assert.Greater(complete, retirePrior);
			StringAssert.Contains("baseBlank", projection);
			StringAssert.Contains("baseExact", projection);
			StringAssert.Contains("if (!baseBlank && !baseExact) return false", projection);
			StringAssert.Contains("TryPrepareTransaction", mutation);
			StringAssert.Contains("TryCommitTransaction", mutation);
			StringAssert.Contains("TryRollback(market", mutation);
			StringAssert.Contains("CaptureStock", removal);
			StringAssert.Contains("CaptureLegend", removal);
		}

		[Test]
		public void PreparedHandoffFreezesResidentsAndTerminalAbortIsTwoSided()
		{
			string lifecycle = Read("Experience/KingdomGuestbook.z02.Lifecycle.cs");
			string handoff = Read("Experience/KingdomGuestbook.z01b.MarketHandoff.cs");
			string projection = Read("Growth/KingdomMarketStockProjection.cs");
			string terminal = Read("Growth/KingdomMarketLegendaryTerminal.cs");
			string abort = Read("Growth/KingdomMarketLegendaryAbort.cs");
			string emigration = Read("Growth/KingdomGrowth.z16.Emigration.cs");
			int apply = lifecycle.IndexOf("internal static bool ApplyLifecycleLodge",
				StringComparison.Ordinal);
			int enrol = lifecycle.IndexOf("KingdomResidents.TryEnsureRow", apply,
				StringComparison.Ordinal);
			int configure = lifecycle.IndexOf("ConfigureLegendaryTraderShop", apply,
				StringComparison.Ordinal);
			Assert.Greater(enrol, apply); Assert.Greater(configure, enrol);
			StringAssert.Contains("HandoffResidentId", projection + handoff);
			StringAssert.Contains("PriorResidentId", projection + handoff);
			StringAssert.Contains("KingdomResidentStanding.Dead", terminal);
			StringAssert.Contains("OwnedCityBooks", terminal);
			StringAssert.DoesNotContain("KingdomCitizenship.BelongsTo", terminal);
			StringAssert.Contains("KingdomMarketHandoffGlobalIndex.TryLoaded", abort);
			StringAssert.Contains("atPrior && !priorDead", abort);
			StringAssert.Contains("TryPrepareTransaction", abort);
			StringAssert.Contains("TryCommitTransaction", abort);
			StringAssert.Contains("TryRollback(transaction", abort);
			StringAssert.Contains("PreparedMarketHandoffParty", emigration);
			StringAssert.Contains("marker.BodyObjectId == id", emigration);
			StringAssert.Contains("marker.PriorBodyObjectId == id", emigration);
			StringAssert.Contains("KingdomSurvey.ObjectsFor(Body.CurrentZone)", emigration);
		}

		[Test]
		public void DepartureRollbackAndRemovalRetainExactRecoveryEvidence()
		{
			string custody = Read("Growth/KingdomMarketStockCustody.cs");
			string stockTransfer = Read("Growth/KingdomMarketStockTransfer.cs");
			string projection = Read("Growth/KingdomGrowth.z18c.MarketProjection.cs");
			string removal = Read("Growth/KingdomMarketRemoval.Transaction.cs");
			StringAssert.Contains("bool frozenReceipt", custody);
			StringAssert.Contains("KingdomCivicOfficePhase.VacancyPrepared", custody);
			StringAssert.Contains("Marker.Matches(System, receipt, Body)", custody);
			StringAssert.Contains("TryRebindPhysical", stockTransfer);
			StringAssert.Contains("TryBind(System, SettlementId, move.Source", stockTransfer);
			int pending = projection.IndexOf("bool pendingStock", StringComparison.Ordinal);
			int clear = projection.IndexOf("TryRemoveMarketServiceForRealmRemoval",
				pending, StringComparison.Ordinal);
			int pendingResult = projection.IndexOf("if (!pendingStock) return true", clear,
				StringComparison.Ordinal);
			Assert.Greater(pending, 0); Assert.Greater(clear, pending);
			Assert.Greater(pendingResult, clear);
			StringAssert.Contains("HandoffPrepared = marker.HandoffPrepared", removal);
			StringAssert.Contains("HandoffResidentId = marker.HandoffResidentId", removal);
			StringAssert.Contains("PriorResidentId = marker.PriorResidentId", removal);
			StringAssert.Contains("BodyHandoffIntent", removal);
			StringAssert.Contains("marker.HandoffPrepared == Snapshot.HandoffPrepared", removal);
		}

		[Test]
		public void AccessionClosesOfficeAndDetachesStockBeforeResidentIdentityIsErased()
		{
			string accession = Read(
				"Simulation/City/KingdomResidents.04.ResidentTransitionsAndAccession.cs");
			string repair = Read("Simulation/City/KingdomResidents.05.AccessionRepair.cs");
			string office = Read("Experience/KingdomOfficeRuntime.Accession.cs");
			string stock = Read("Growth/KingdomMarketStockAccession.cs");
			int observe = accession.IndexOf("TryObserveAccessionLoss",
				StringComparison.Ordinal);
			int citizenship = accession.IndexOf("KingdomCitizenship.TryRemove", observe,
				StringComparison.Ordinal);
			int finish = accession.IndexOf("FinishAccessionBody", citizenship,
				StringComparison.Ordinal);
			Assert.Greater(observe, 0); Assert.Greater(citizenship, observe);
			Assert.Greater(finish, citizenship);
			StringAssert.Contains("TryObserveAccessionLoss", repair);
			StringAssert.Contains("SuccessorMarketBlocked", accession);
			StringAssert.Contains("SuccessorMarketBlocked", repair);
			int accedeStart = accession.IndexOf("internal static KingdomAccessionOutcome TryAccede",
				StringComparison.Ordinal);
			int repairStart = repair.IndexOf("internal static KingdomAccessionOutcome TryRepairAccession",
				StringComparison.Ordinal);
			Assert.Less(accession.IndexOf("SuccessorMarketBlocked", accedeStart,
				StringComparison.Ordinal), accession.IndexOf("KingdomResidentRules.TryRemove",
				accedeStart, StringComparison.Ordinal));
			Assert.Less(repair.IndexOf("SuccessorMarketBlocked", repairStart,
				StringComparison.Ordinal), repair.IndexOf("KingdomResidentRules.TryRemove",
				repairStart, StringComparison.Ordinal));
			StringAssert.Contains("KingdomAccessionOutcome.RepairRequired", accession + repair);
			int prepare = office.IndexOf("TryPrepareOfficeVacancy", StringComparison.Ordinal);
			int cleanup = office.IndexOf("CleanupProjection", prepare, StringComparison.Ordinal);
			int retire = office.IndexOf("TryRetireAccedingHolder", cleanup,
				StringComparison.Ordinal);
			int complete = office.IndexOf("TryCompleteOfficeVacancy", retire,
				StringComparison.Ordinal);
			Assert.Greater(prepare, 0); Assert.Greater(cleanup, prepare);
			Assert.Greater(retire, cleanup); Assert.Greater(complete, retire);
			StringAssert.Contains("KingdomCivicOfficeVacancyCause.AuthorityLost", office);
			StringAssert.Contains("TryPrepareTransaction", stock);
			StringAssert.Contains("TryCommitTransaction", stock);
			StringAssert.Contains("Exact(System, SettlementId, Body, item)", stock);
			StringAssert.DoesNotContain("RemoveIntProperty(\"_stock\")", office + stock);
			StringAssert.DoesNotContain("Obliterate", office + stock);
			StringAssert.Contains("ClassifyAccessionAuthority", office);
			StringAssert.Contains("RefusedCompetingOwners", office);
			StringAssert.Contains("IsCurrentLegendaryCivicAuthority", stock);
			StringAssert.Contains("System.CurrentSettlementId", stock);
			int preview = stock.IndexOf("TryPrepareTransaction", StringComparison.Ordinal);
			int legendCommit = stock.LastIndexOf("TryCommitTransaction",
				StringComparison.Ordinal);
			int failClosed = stock.IndexOf("System.HasShopkeeper = false", legendCommit,
				StringComparison.Ordinal);
			Assert.Greater(preview, 0); Assert.Greater(legendCommit, preview);
			Assert.Greater(failClosed, legendCommit);
		}

		[Test]
		public void PreparedEndpointsAreLockedButCompletedLegendaryHeirsNormalize()
		{
			string emigration = Read("Growth/KingdomGrowth.z16.Emigration.cs");
			string heirs = Read("Experience/KingdomSuccession.HeirsAndNews.cs");
			string death = Read("Experience/KingdomSuccession.DeathExecution.cs");
			string recovery = Read("Experience/KingdomSuccession.RiteRecovery.cs");
			string interop = Read("Experience/KingdomSuccession.MarketInterop.cs");
			string handoff = Read("Experience/KingdomGuestbook.z01b.MarketHandoff.cs");
			string office = Read("Experience/KingdomOfficeRuntime.Accession.cs");
			string stock = Read("Growth/KingdomMarketStockAccession.cs");
			StringAssert.Contains("SuccessorMarketBlocked", emigration);
			StringAssert.Contains("PreparedMarketHandoffParty", emigration);
			StringAssert.DoesNotContain("LegendaryTraderResidentProperty", emigration.Substring(
				emigration.IndexOf("internal static bool SuccessorMarketBlocked",
					StringComparison.Ordinal)));
			StringAssert.Contains("SuccessorMarketBlocked", heirs);
			int gate = death.IndexOf("SuccessorMarketBlocked", StringComparison.Ordinal);
			int transfer = death.IndexOf("SetPlayerBodyAndRebindAll(game, founder", gate,
				StringComparison.Ordinal);
			Assert.Greater(gate, 0); Assert.Greater(transfer, gate);
			int coldGate = recovery.IndexOf("SuccessorMarketBlocked", StringComparison.Ordinal);
			int coldTransfer = recovery.IndexOf("SetPlayerBodyAndRebindAll(game, founder",
				coldGate, StringComparison.Ordinal);
			Assert.Greater(coldGate, 0); Assert.Greater(coldTransfer, coldGate);
			StringAssert.Contains("PendingDeathToken", interop);
			StringAssert.Contains("PendingAccessionRepairResidentId", interop);
			StringAssert.Contains("DeathSelectionInProgress", interop);
			string succession = Read("Experience/KingdomSuccession.cs");
			int selectionLock = succession.IndexOf("DeathSelectionInProgress = true",
				StringComparison.Ordinal);
			int deathCall = succession.IndexOf("HandleFounderDeath(E)", selectionLock,
				StringComparison.Ordinal);
			int selectionUnlock = succession.IndexOf("DeathSelectionInProgress = false",
				deathCall, StringComparison.Ordinal);
			Assert.Greater(selectionLock, 0); Assert.Greater(deathCall, selectionLock);
			Assert.Greater(selectionUnlock, deathCall);
			StringAssert.Contains("TryResolveBoundBody(System, row.ResidentId",
				heirs);
			StringAssert.Contains("true, out GameObject body", heirs);
			StringAssert.Contains("if (!resuming && !KingdomSuccession.MarketHandoffMayStart())",
				handoff);
			StringAssert.Contains("TryRetireAccedingLegendary", office);
			StringAssert.Contains("new List<GameObject> { Body }", stock);
			StringAssert.Contains("TryPrepareTransaction", stock);
			StringAssert.Contains("TryCommitTransaction", stock);
		}

		[Test]
		public void InstalledQudGroundsPhysicalSourceMarketSinkAndEmptyTrade()
		{
			string trade = Native("XRL.UI/TradeUI.cs", "XRL/UI/TradeUI.cs");
			string allow = Native("XRL.World/AllowTradeWithNoInventoryEvent.cs",
				"XRL/World/AllowTradeWithNoInventoryEvent.cs");
			string inventory = Native("XRL.World.Parts/Inventory.cs",
				"XRL/World/Parts/Inventory.cs");
			string stacker = Native("XRL.World.Parts/Stacker.cs",
				"XRL/World/Parts/Stacker.cs");
			string restocker = Native("XRL.World.Parts/GenericInventoryRestocker.cs",
				"XRL/World/Parts/GenericInventoryRestocker.cs");
			string gameObject = Native("XRL.World/GameObject.cs", "XRL/World/GameObject.cs");
			if (trade == null || allow == null || inventory == null || stacker == null
				|| restocker == null || gameObject == null)
			{
				Assert.Ignore("Installed/decompiled Qud source is unavailable for market proof.");
				return;
			}
			int stockIn = trade.IndexOf("gO2.SetIntProperty(\"_stock\", 1)",
				StringComparison.Ordinal);
			int saleSplit = trade.LastIndexOf("gO2.SplitStack", stockIn,
				StringComparison.Ordinal);
			int take = trade.IndexOf("Trader.TakeObject(item2", stockIn,
				StringComparison.Ordinal);
			int stockOut = trade.IndexOf("gO.RemoveIntProperty(\"_stock\")",
				StringComparison.Ordinal);
			int buySplit = trade.LastIndexOf("gO.SplitStack", stockOut,
				StringComparison.Ordinal);
			int took = inventory.IndexOf("TookEvent.Send(gameObjectParameter",
				StringComparison.Ordinal);
			int stackCheck = inventory.IndexOf("CheckStacks();", took,
				StringComparison.Ordinal);
			Assert.Greater(stockIn, 0); Assert.Greater(saleSplit, 0);
			Assert.Greater(stockIn, saleSplit); Assert.Greater(take, stockIn);
			Assert.Greater(stockOut, 0); Assert.Greater(buySplit, 0);
			Assert.Greater(stockOut, buySplit); Assert.Greater(took, 0);
			Assert.Greater(stackCheck, took);
			StringAssert.Contains("Trader.UseDrams", trade);
			StringAssert.Contains("Trader.GiveDrams", trade);
			StringAssert.Contains("AssumeTradersHaveWater", trade);
			StringAssert.Contains("return !flag;", allow);
			StringAssert.Contains("ParentObject.DeepCopy(CopyEffects: true)", stacker);
			StringAssert.Contains("ParentObject.HasTag(\"AlwaysStack\")", stacker);
			StringAssert.Contains("GameObject.Create(ParentObject.Blueprint)", stacker);
			StringAssert.Contains("gameObject.Stacker.StackCount = num", stacker);
			StringAssert.Contains("HasPropertyOrTag(\"NeverStack\")", stacker);
			StringAssert.Contains("ParentObject.Obliterate(null, Silent: true)", stacker);
			StringAssert.Contains("!ParentObject.IsPlayerControlled()", restocker);
			StringAssert.Contains("!ParentObject.WasPlayer()", restocker);
			int sameStart = gameObject.IndexOf("public bool SameAs(GameObject GO)",
				StringComparison.Ordinal);
			int sameEnd = gameObject.IndexOf("public int GetBodyPartCountEquippedOn", sameStart,
				StringComparison.Ordinal);
			string sameAs = gameObject.Substring(sameStart, sameEnd - sameStart);
			StringAssert.Contains("GetIntProperty(\"Important\")", sameAs);
			StringAssert.DoesNotContain("norestock", sameAs);
			StringAssert.DoesNotContain("TAFLocalMarket", sameAs);
		}

		[Test]
		public void HandoffHasDurableSourceAuthorityAndForeignReceiptsFailClosed()
		{
			string source = Read("Growth/KingdomMarketHandoffSourceProjection.cs");
			string recovery = Read("Growth/KingdomMarketHandoffRecovery.cs");
			string handoff = Read("Experience/KingdomGuestbook.z01b.MarketHandoff.cs");
			string split = Read("Growth/KingdomMarketStockSplit.cs");
			string projection = Read("Growth/KingdomMarketStockProjection.cs");
			StringAssert.Contains("SourceBodyObjectId", source);
			StringAssert.Contains("TargetResidentId", source);
			StringAssert.Contains("LifecycleOperationId", source);
			StringAssert.Contains("LifecyclePlanHash", source);
			StringAssert.Contains("LifecycleSequence", source);
			StringAssert.Contains("LifecycleTerminalClosed", source);
			StringAssert.Contains("TargetTerminalDead", source);
			int graphPreflight = handoff.IndexOf("PreflightHandoffGraph",
				StringComparison.Ordinal);
			int prepareSource = handoff.IndexOf("PrepareSourceHandoff", StringComparison.Ordinal);
			int prepareTarget = handoff.IndexOf("TryPrepareLegendaryMarketProjection",
				StringComparison.Ordinal);
			Assert.Greater(graphPreflight, 0);
			Assert.Greater(prepareSource, graphPreflight);
			Assert.Greater(prepareTarget, prepareSource);
			StringAssert.Contains("ReproveResumingHandoff", handoff);
			StringAssert.Contains("TryProveLegendary", Read(
				"Experience/KingdomGuestbook.z01d.MarketHandoffSource.cs"));
			string handoffHelpers = Read(
				"Experience/KingdomGuestbook.z01c.MarketHandoffHelpers.cs");
			StringAssert.Contains("ExactCompletedHandoffTarget", handoffHelpers);
			StringAssert.Contains("SealedFiniteRestocker", handoffHelpers);
			StringAssert.Contains("!source.ExactLive(System, Prior)", handoffHelpers);
			StringAssert.Contains("source.Tier != Tier", handoffHelpers);
			StringAssert.Contains("heldTier == Tier", handoffHelpers);
			StringAssert.Contains("DeadResident(System, Settlement", recovery);
			StringAssert.Contains("missing handoff target lacks exact Dead resident proof", recovery);
			StringAssert.Contains("Marker.TargetTerminalDead = 1", recovery);
			StringAssert.Contains("marker.TargetTerminalDead != 1", recovery);
			StringAssert.Contains("ReferenceEquals(Loaded[i]?.InInventory, target)", recovery);
			StringAssert.Contains("KingdomMarketHandoffGlobalIndex.TryLoaded", recovery);
			StringAssert.DoesNotContain("Survey.Objects.Count", recovery);
			string globalIndex = Read("Growth/KingdomMarketHandoffGlobalIndex.cs");
			StringAssert.Contains("The.ZoneManager.Graveyard", globalIndex);
			StringAssert.Contains("The.Game.ObjectGameState", globalIndex);
			StringAssert.Contains("MaximumObjects", globalIndex);
			StringAssert.Contains("marker.LifecycleTerminalClosed != 1", recovery);
			StringAssert.Contains("book.NotableGuestRetiredThrough < Marker.LifecycleSequence",
				recovery);
			StringAssert.Contains("proof.PlanHash == Marker.LifecyclePlanHash", recovery);
			string lodgeTerminal = Read("Experience/KingdomGuestLifecycle.LodgeTerminal.cs");
			int sealOutcome = lodgeTerminal.IndexOf("TrySealCompletedDeadHandoffOutcome",
				StringComparison.Ordinal);
			int beginAbandon = lodgeTerminal.IndexOf("TryBeginLodgeAbandon", sealOutcome,
				StringComparison.Ordinal);
			Assert.Greater(sealOutcome, 0);
			Assert.Greater(beginAbandon, sealOutcome);
			StringAssert.Contains("return receipts == 1 && identities == 1", lodgeTerminal);
			StringAssert.Contains("return receipts == 0 || receipts == 1 && identities == 1",
				lodgeTerminal);
			StringAssert.Contains("identities > 1 || targetIdentities > 1", lodgeTerminal);
			string lifecycleDrive = Read("Experience/KingdomGuestLifecycle.Settlement.cs");
			int terminalCheckpoint = lifecycleDrive.IndexOf("CommitMarketTerminalClose",
				StringComparison.Ordinal);
			int targetDeadCheckpoint = lifecycleDrive.IndexOf("TargetTerminalDead != 1",
				terminalCheckpoint, StringComparison.Ordinal);
			int lifecycleRemoval = lifecycleDrive.IndexOf("TryRemoveReleasedLodge",
				targetDeadCheckpoint, StringComparison.Ordinal);
			Assert.Greater(terminalCheckpoint, 0);
			Assert.Greater(targetDeadCheckpoint, terminalCheckpoint);
			Assert.Greater(lifecycleRemoval, targetDeadCheckpoint);
			StringAssert.Contains("TryRetireCurrent", projection);
			StringAssert.Contains("override void FinalizeCopy", projection);
			StringAssert.Contains("KingdomMarketStockProtection.TryRetire(ParentObject)",
				projection);
			StringAssert.Contains("belongs to another or divergent realm",
				Read("Growth/KingdomMarketStockDetachment.cs"));
			StringAssert.Contains("Source.HasTag(\"AlwaysStack\")", split);
			StringAssert.Contains("Source.Blueprint == Remainder.Blueprint", split);
			StringAssert.Contains("!Remainder.HasIntProperty(\"_stock\")", split);
			StringAssert.Contains("if (!active) return true;", split);
			StringAssert.Contains("legend.Active(System, Holder)", split);
			StringAssert.Contains("Remainder.SetIntProperty(\"_stock\", 1)", split);
			StringAssert.DoesNotContain("Remainder.RemoveIntProperty(\"_stock\")", split);
			StringAssert.Contains("Item.Physics == null", Read(
				"Growth/KingdomMarketStockCustody.cs"));
			StringAssert.Contains("Item.IsTakeable()", handoff + Read(
				"Experience/KingdomGuestbook.z01d.MarketHandoffSource.cs"));
			int completed = handoff.IndexOf("CompleteHandoff()", StringComparison.Ordinal);
			int sourceClose = handoff.IndexOf("CompleteCommittedSourceResidue", completed,
				StringComparison.Ordinal);
			Assert.Greater(sourceClose, completed);
			int residue = handoff.IndexOf("CompleteCommittedSourceResidue",
				StringComparison.Ordinal);
			Assert.Greater(residue, 0);
			StringAssert.Contains("TryCommitLifecycleMarketSource", Read(
				"Experience/KingdomGuestbook.z01d.MarketHandoffSource.cs"));
			string sourceHelper = Read(
				"Experience/KingdomGuestbook.z01d.MarketHandoffSource.cs");
			StringAssert.Contains("if (!exact && created", sourceHelper);
			StringAssert.Contains("Source.RemovePart(marker)", sourceHelper);
			StringAssert.Contains("if (sources.Count > 1) return false", sourceHelper);
			StringAssert.Contains("if (sources.Count == 0)", sourceHelper);
			StringAssert.Contains("KingdomMarketHandoffGlobalIndex.TryLoaded", sourceHelper);
			StringAssert.Contains("ExactLifecycleNoMarketSourceCheckpoint", sourceHelper);
			StringAssert.Contains("MarketCommitted", sourceHelper);
			StringAssert.Contains("TryClearCommittedHandoff", sourceHelper);
			string committedClear = sourceHelper.Substring(sourceHelper.IndexOf(
				"private static bool TryClearCommittedHandoff", StringComparison.Ordinal));
			int committedStockClear = committedClear.IndexOf("StockTransferTargetProperty, null",
				StringComparison.Ordinal);
			int committedMarketClear = committedClear.IndexOf("MarketTransferTargetProperty, null",
				StringComparison.Ordinal);
			Assert.Greater(committedStockClear, 0);
			Assert.Greater(committedMarketClear, committedStockClear);
			int committedCleanupCall = sourceHelper.IndexOf("TryClearCommittedHandoff(Target",
				StringComparison.Ordinal);
			int committedCheckpoint = sourceHelper.IndexOf(
				"ExactLifecycleCommittedMarketSourceCheckpoint(System, Target)",
				committedCleanupCall, StringComparison.Ordinal);
			int committedSourceRemoval = sourceHelper.IndexOf("source.RemovePart(exact)",
				committedCheckpoint, StringComparison.Ordinal);
			Assert.Greater(committedCleanupCall, 0);
			Assert.Greater(committedCheckpoint, committedCleanupCall);
			Assert.Greater(committedSourceRemoval, committedCheckpoint);
			StringAssert.Contains("CompletedDeadSourceHandoff", sourceHelper);
			StringAssert.Contains("TryOpenLodgeForTarget", sourceHelper);
			StringAssert.Contains("lodge == Open.Id", sourceHelper);
			StringAssert.Contains("MarketSourceDead", sourceHelper);
			StringAssert.Contains("StockTransferTargetProperty", sourceHelper);
			StringAssert.DoesNotContain("KingdomSurvey.ObjectsFor(Target", sourceHelper);
			StringAssert.Contains("GameObject.Validate(Source)", source);
			StringAssert.Contains("!Source.IsPlayer()", source);
			StringAssert.Contains("!string.IsNullOrEmpty(TargetBodyObjectId)", source);
			StringAssert.Contains("Marker.ExactLive(System, Source)", recovery);
			StringAssert.Contains("catch (System.Exception error)", recovery);
			StringAssert.Contains("RollbackAbsentTarget", recovery);
			string abort = Read("Growth/KingdomMarketLegendaryAbort.cs");
			StringAssert.Contains("TryClearAbortItemIntents", abort);
			StringAssert.Contains("KingdomMarketHandoffIntentRules.Classify", abort);
			StringAssert.Contains("prepared handoff terminal cleanup threw", abort);
			int abortStockClear = abort.IndexOf("StockTransferTargetProperty, null",
				StringComparison.Ordinal);
			int abortMarketClear = abort.IndexOf("MarketTransferTargetProperty, null",
				StringComparison.Ordinal);
			Assert.Greater(abortStockClear, 0);
			Assert.Greater(abortMarketClear, abortStockClear,
				"durable cleanup clears stock intent first");
			string abortAuthority = Read("Growth/KingdomMarketLegendaryAbortAuthority.cs");
			StringAssert.Contains("MarketSourceDead", abortAuthority);
			StringAssert.Contains("ExactSourceDeadPreparedTarget", abortAuthority);
			StringAssert.Contains("sourceDeadResume", abort);
			StringAssert.Contains("TryClearAbortItemIntents", abort);
			StringAssert.Contains("KingdomMarketHandoffGraphAuthority.TryPreflight",
					abortAuthority);
			string graph = Read("Growth/KingdomMarketHandoffGraphAuthority.cs");
			StringAssert.Contains("duplicate or divergent identity authority", graph);
			StringAssert.Contains("torn or duplicate stock receipt", graph);
			StringAssert.Contains("cross-target intent pair", graph);
			StringAssert.Contains("KingdomMarketHandoffIntentRules.Classify",
					Read("Growth/KingdomMarketHandoffRecovery.cs"));
			StringAssert.Contains("KingdomMarketHandoffIntentRules.Classify", sourceHelper);
			StringAssert.Contains("KingdomMarketHandoffGraphAuthority.TryPreflight", sourceHelper);
			StringAssert.Contains("MarketTargets[i], RemoveIfNull: true", sourceHelper);
			StringAssert.Contains("bodyIntent,", sourceHelper);
			StringAssert.Contains("KingdomMarketHandoffGlobalIndex", Read(
					"Growth/KingdomMarketHandoffRecovery.cs"));
			string terminalAuthority = Read(
					"Growth/KingdomMarketHandoffTerminalAuthority.cs");
			string terminalMutation = Read(
					"Growth/KingdomMarketHandoffTerminalMutation.cs");
			string lifecycleMarketReceipt = Read(
				"Experience/KingdomLifecycleLodgeMarketCommitRules.cs");
			StringAssert.Contains("ExactLodgeMarketSourceReceipt", terminalAuthority
				+ abortAuthority);
			StringAssert.Contains("LodgeTerminalShape(Open, false)", lifecycleMarketReceipt);
			StringAssert.Contains("TryRetireCompletedDeadHandoffTarget", terminalMutation);
			StringAssert.Contains("TryClearMarkerlessDeadHandoffTarget", terminalMutation);
			StringAssert.Contains("TryFinalizeLiveSourceDeadHandoff", terminalMutation);
			StringAssert.Contains("ExactOrRecoverable", terminalAuthority + terminalMutation);
			StringAssert.Contains("SourceBody.RemovePart(Source)", terminalMutation);
			StringAssert.Contains("SealedFiniteRestocker", terminalMutation);
			StringAssert.Contains("Source.LifecycleTerminalClosed != 1", terminalMutation);
			StringAssert.Contains("ExactDeadHandoffTarget", terminalMutation);
			StringAssert.Contains("Target.GetIntProperty(\"InventoryTier\") != Source.Tier",
				terminalMutation);
			StringAssert.Contains("Target.GetPart<r_KingdomLegendaryMarketProjection>() != null",
				terminalMutation);
			Assert.Less(File.ReadAllLines(Path.Combine(TestMain.RepositoryRoot,
					"Growth/KingdomMarketHandoffTerminalAuthority.cs")).Length, 300);
			Assert.Less(File.ReadAllLines(Path.Combine(TestMain.RepositoryRoot,
					"Growth/KingdomMarketHandoffTerminalMutation.cs")).Length, 300);
			string officeContext = Read("Experience/KingdomOfficeRuntime.Context.cs");
			string officeCommands = Read("Experience/KingdomOfficeRuntime.Commands.cs");
			StringAssert.Contains("MarketOfficeCandidateBlocked(body, Context.Survey)",
				officeContext);
			StringAssert.Contains("!marketOwner", officeContext);
			StringAssert.Contains("MarketOfficeCandidateBlocked(body, Context.Survey)",
				officeCommands);
			string provider = Read("Growth/KingdomMarketProviderAuthority.cs");
			StringAssert.Contains("!Body.IsAlive || Body.IsPlayer()", provider);
			StringAssert.Contains("KingdomResidentRules.OnTheRoll(found)", provider);
			StringAssert.Contains("KingdomMarketProviderAuthority.LiveResident",
				Read("Growth/KingdomMarketStockCustody.cs"));
			string detachment = Read("Growth/KingdomMarketStockDetachment.cs");
			Assert.AreEqual(2, Occurrences(detachment, "TryPrepareTransaction(System"));
			Assert.AreEqual(2, Occurrences(detachment, "TryCommitTransaction(System"));
			string rollback = Read("Growth/KingdomMarketRemoval.Rollback.cs");
			StringAssert.Contains("catch (Exception) { restored = false; }", rollback);
			StringAssert.Contains("MatchesStock", rollback);
			StringAssert.Contains("MatchesLegend", rollback);
			string transaction = Read("Growth/KingdomMarketRemoval.Transaction.cs");
			StringAssert.Contains("Holder = Item.InInventory", transaction);
			StringAssert.Contains("Cell = Item.CurrentCell", transaction);
			StringAssert.Contains("Count = Item.Count", transaction);
			StringAssert.Contains("HadNativeStock", transaction);
			StringAssert.Contains("KingdomGuestbook.MarketTransferTargetProperty", transaction);
			StringAssert.Contains("TryAdmitLegacyHandoff", Read(
				"Experience/KingdomGuestbook.z01d.MarketHandoffSource.cs"));
			StringAssert.Contains("bool linked", projection);
				StringAssert.Contains("KingdomGuestbook.MarketTransferTargetProperty, null",
					projection);
				string legendTrade = Read("Growth/KingdomMarketLegendaryTrade.cs");
				StringAssert.Contains("AllowTradeWithNoInventoryEvent", legendTrade);
				StringAssert.Contains("Active(system, ParentObject)", legendTrade);
			string handoffSource = Read(
				"Experience/KingdomGuestbook.z01d.MarketHandoffSource.cs");
			StringAssert.Contains("TryRetireLegacyIntent", handoff);
			StringAssert.Contains("source.ExactLive(System, Prior)", handoffSource);
			StringAssert.Contains("legend.HandoffIntent != source.Intent", handoffSource);
			string emigration = Read("Growth/KingdomGrowth.z16.Emigration.cs");
			string transition = Read(
				"Simulation/City/KingdomResidentTransitionAuthority.cs") + Read(
				"Simulation/City/KingdomResidentTransitionAuthority.Durable.cs") + Read(
				"Simulation/City/KingdomResidentTransitionAuthority.Office.cs") + Read(
				"Simulation/City/KingdomResidentTransitionAuthority.ObjectGraph.cs");
			StringAssert.Contains("CanGenericEmigrate", emigration);
			StringAssert.Contains("CanDestroyResidentBody", emigration);
			StringAssert.Contains("r_KingdomNamedCook", transition);
			StringAssert.Contains("System.Experience.Offices", transition);
			StringAssert.Contains("MarketTransferTargetProperty", transition);
			StringAssert.Contains("KingdomResidentTransitionAuthority.CanContinueJournaledCarrierRemoval", Read(
				"Simulation/City/KingdomResidents.04.ResidentTransitionsAndAccession.cs"));
		}

		private static string Read(string relative)
		{
			return TestMain.ReadRepositoryText(relative);
		}

		private static int Occurrences(string source, string value)
		{
			int count = 0; int offset = 0;
			while ((offset = source.IndexOf(value, offset,
				StringComparison.Ordinal)) >= 0) { count++; offset += value.Length; }
			return count;
		}

		private static string Native(string dotted, string nested)
		{
			string supplied = Environment.GetEnvironmentVariable("TAF_QUD_DECOMPILED");
			string[] roots = new[] { supplied,
				"/home/r/coq/qud_helper/game_base/decompiled/6000.0.41.4645959",
				"/home/r/coq/qud_helper/game_base/decompiled/2.0.211.51-ilspy9.1" };
			for (int i = 0; i < roots.Length; i++)
			{
				if (string.IsNullOrWhiteSpace(roots[i])) continue;
				string path = Path.Combine(roots[i], dotted);
				if (!File.Exists(path)) path = Path.Combine(roots[i], nested);
				if (File.Exists(path)) return File.ReadAllText(path);
			}
			return null;
		}
	}
}
#endif
