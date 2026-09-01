#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomNamedCookRulesTests
	{
		private const string Realm = "taf:realm:named-cook-tests";
		private const string City = "taf:settlement:named-cook-tests";

		[Test]
		public void PreparationFreezesDeterministicExactNativeGraphIdentity()
		{
			KingdomNamedCookReceipt first = Prepare(1, 17, "Ari", "body-17");
			KingdomNamedCookReceipt second = Prepare(1, 17, "Ari", "body-17");
			Assert.AreEqual(KingdomNamedCookPhase.Prepared, first.Phase);
			Assert.AreEqual(first.RecipeId, second.RecipeId);
			Assert.AreEqual(first.EffectId, second.EffectId);
			Assert.AreEqual(first.GraphFingerprint, second.GraphFingerprint);
			Assert.AreEqual("salt-crack of New Grit Gate", first.RecipeDisplayName);
			Assert.IsTrue(Guid.TryParseExact(first.EffectId, "D", out _));
			AssertValid(first);
		}

		[Test]
		public void GenerationAndResidentSeparateAppointmentIdentity()
		{
			Assert.AreNotEqual(Prepare(1, 17, "Ari", "body-17").RecipeId,
				Prepare(2, 17, "Ari", "body-17").RecipeId);
			Assert.AreNotEqual(Prepare(1, 17, "Ari", "body-17").RecipeId,
				Prepare(1, 18, "Ula", "body-18").RecipeId);
		}

		[Test]
		public void CandidateLawRefusesExistingVanillaRecipeBeforeAppointment()
		{
			Assert.AreEqual(KingdomNamedCookVerdict.Allowed, Judge());
			Assert.AreEqual(KingdomNamedCookVerdict.NativeRecipeAlreadyPresent,
				Judge(shares: true));
			Assert.AreEqual(KingdomNamedCookVerdict.NativeRecipeAlreadyPresent,
				Judge(teaches: true));
			Assert.AreEqual(KingdomNamedCookVerdict.ForeignCookMarker,
				Judge(marker: true));
			Assert.AreEqual(KingdomNamedCookVerdict.OpenReceipt,
				Judge(open: true));
		}

		[Test]
		public void PhaseTransitionsAreCopyOnWriteAndTerminalReleaseIsDated()
		{
			KingdomNamedCookReceipt prepared = Prepare(1, 17, "Ari", "body-17");
			KingdomNamedCookReceipt applied = KingdomNamedCookRules.Applied(prepared);
			KingdomNamedCookReceipt releasing = KingdomNamedCookRules.BeginRelease(applied);
			KingdomNamedCookReceipt released = KingdomNamedCookRules.Released(releasing, 900L);
			Assert.AreEqual(KingdomNamedCookPhase.Prepared, prepared.Phase);
			Assert.AreEqual(KingdomNamedCookPhase.Applied, applied.Phase);
			Assert.AreEqual(KingdomNamedCookPhase.ReleasePrepared, releasing.Phase);
			Assert.AreEqual(KingdomNamedCookPhase.Released, released.Phase);
			Assert.AreEqual(900L, released.ReleasedTick);
			Assert.IsNull(KingdomNamedCookRules.Released(releasing, 99L));
			AssertValid(released);
		}

		[Test]
		public void EveryWitnessedOrExplicitVacancyKeepsCauseAndServiceTruth()
		{
			KingdomNamedCookVacancyCause[] causes =
			{
				KingdomNamedCookVacancyCause.Released,
				KingdomNamedCookVacancyCause.Death,
				KingdomNamedCookVacancyCause.Departure,
				KingdomNamedCookVacancyCause.VoluntaryRetirement,
				KingdomNamedCookVacancyCause.Handoff
			};
			for (int i = 0; i < causes.Length; i++)
			{
				KingdomNamedCookReceipt applied = KingdomNamedCookRules.Applied(
					Prepare(i + 1, 17 + i, "Cook " + i, "body-" + i));
				Assert.AreEqual(KingdomNamedCookServiceState.Available,
					KingdomNamedCookRules.ServiceState(applied));
				KingdomNamedCookReceipt prepared = KingdomNamedCookRules.BeginVacancy(
					applied, causes[i]);
				Assert.IsNotNull(prepared, causes[i].ToString());
				Assert.AreEqual(causes[i], KingdomNamedCookRules.VacancyCause(prepared.Phase));
				Assert.AreEqual(KingdomNamedCookServiceState.RecoveryPending,
					KingdomNamedCookRules.ServiceState(prepared));
				KingdomNamedCookReceipt vacant = KingdomNamedCookRules.CompleteVacancy(
					prepared, 900L + i);
				Assert.IsNotNull(vacant, causes[i].ToString());
				Assert.AreEqual(causes[i], KingdomNamedCookRules.VacancyCause(vacant.Phase));
				Assert.AreEqual(KingdomNamedCookServiceState.Vacant,
					KingdomNamedCookRules.ServiceState(vacant));
				Assert.IsNotEmpty(KingdomNamedCookRules.VacancyClause(vacant));
				AssertValid(vacant);
			}
		}

		[Test]
		public void VacancyPreparationIsExactIdempotentAndCannotChangeCause()
		{
			KingdomNamedCookReceipt applied = KingdomNamedCookRules.Applied(
				Prepare(1, 17, "Ari", "body-17"));
			KingdomNamedCookReceipt death = KingdomNamedCookRules.BeginVacancy(applied,
				KingdomNamedCookVacancyCause.Death);
			KingdomNamedCookReceipt retry = KingdomNamedCookRules.BeginVacancy(death,
				KingdomNamedCookVacancyCause.Death);
			Assert.AreNotSame(death, retry);
			Assert.AreEqual(death.Phase, retry.Phase);
			Assert.IsNull(KingdomNamedCookRules.BeginVacancy(death,
				KingdomNamedCookVacancyCause.Departure));
			Assert.IsNull(KingdomNamedCookRules.CompleteVacancy(death, 99L));
			KingdomNamedCookReceipt departure = KingdomNamedCookRules.BeginVacancy(applied,
				KingdomNamedCookVacancyCause.Departure);
			KingdomNamedCookReceipt restored = KingdomNamedCookRules.CancelVacancy(departure,
				KingdomNamedCookVacancyCause.Departure);
			Assert.AreEqual(KingdomNamedCookPhase.Applied, restored.Phase);
			Assert.IsNull(KingdomNamedCookRules.CancelVacancy(departure,
				KingdomNamedCookVacancyCause.Death));
		}

		[Test]
		public void IdentityAndGraphTamperingFailClosedWhileQuarantineStaysBounded()
		{
			KingdomNamedCookReceipt receipt = Prepare(1, 17, "Ari", "body-17");
			receipt.RecipeDisplayName += " invented";
			AssertInvalid(receipt);
			KingdomNamedCookReceipt quarantined = KingdomNamedCookRules.Quarantined(
				receipt, "external graph diverged");
			Assert.AreEqual(KingdomNamedCookPhase.Quarantined, quarantined.Phase);
			AssertValid(quarantined);
		}

		[Test]
		public void RuntimeBuildsOnlyTheAuditedDirectBaseRecipeGraph()
		{
			string recipe = Read("Experience", "KingdomNamedCook.Recipe.cs");
			string runtime = recipe + Read("Experience", "KingdomNamedCook.Transactions.cs")
				+ Read("Experience", "KingdomNamedCook.Recovery.cs");
			StringAssert.Contains("new CookingRecipe", recipe);
			StringAssert.Contains("new PreparedCookingRecipieComponentBlueprint(", recipe);
			StringAssert.Contains("new CookingRecipeResultProceduralEffect(effect)", recipe);
			StringAssert.Contains("new CookingDomainTaste_UnitDoNothing()", recipe);
			StringAssert.Contains("new Renderable", recipe);
			StringAssert.DoesNotContain("CookingRecipe.FromIngredients", runtime);
			StringAssert.DoesNotContain("GenerateRecipeName", runtime);
			StringAssert.DoesNotContain("GenerateRecipeTile", runtime);
			StringAssert.DoesNotContain("CreateSpecific", runtime);
			StringAssert.DoesNotContain("CookingGameState.LearnRecipe", runtime);
			StringAssert.DoesNotContain("JournalAPI.AddRecipeNote", runtime);
			StringAssert.DoesNotContain("Recipe.Hidden", recipe);
			StringAssert.DoesNotContain("Recipe.Favorite", recipe);
		}

		[Test]
		public void WitnessedLossAndHandoffUseExactReceiptWithoutGiftsOrAutomaticSuccessor()
		{
			string lifecycle = Read("Experience", "KingdomNamedCook.Lifecycle.cs");
			string transactions = Read("Experience", "KingdomNamedCook.Transactions.cs");
			string menu = Read("Experience", "KingdomNamedCook.cs");
			string death = Read("Experience", "KingdomOffices.cs");
			// The journaled departure runtime owns the departing-cook transaction now: the
			// write-ahead prepare (with rollback) happens before any carrier is removed, and the
			// vacancy is observed in the effects phase.
			string preparation = Read("Simulation", "City",
				"KingdomResidentDeparturePreparation.cs");
			string effects = Read("Growth", "KingdomResidentDepartureRuntime.Effects.cs");
			string begin = Read("Growth", "KingdomResidentDepartureRuntime.Begin.cs");
			StringAssert.Contains("ObserveCookLoss(system, Citizen", death);
			StringAssert.Contains("KingdomNamedCookVacancyCause.Death", death);
			StringAssert.Contains("ObserveCookLoss(System, Body", effects);
			StringAssert.Contains("KingdomNamedCookVacancyCause.Departure", effects);
			int prepare = preparation.IndexOf("PrepareCookLoss(System, Body",
				StringComparison.Ordinal);
			Assert.Greater(prepare, 0);
			Assert.Less(begin.IndexOf("KingdomResidentDeparturePreparation.TryPrepare",
				StringComparison.Ordinal), begin.IndexOf("TryContinue(System, Body",
				StringComparison.Ordinal));
			StringAssert.Contains("CancelPreparedCookLoss(System, Body, PriorCook", preparation);
			StringAssert.Contains("StandingResident(Book, row.ResidentId)", lifecycle);
			StringAssert.Contains("KingdomNamedCookRules.CancelVacancy", lifecycle);
			StringAssert.Contains("One exact body is claimed by two named-cook receipts", lifecycle);
			StringAssert.Contains("KingdomChronicle.RecordOnce", lifecycle);
			StringAssert.Contains("retire; leave the hearth vacant", menu);
			StringAssert.Contains("deliberate handoff", menu);
			StringAssert.Contains("TryChooseResident(city.Book, city.SettlementName", menu);
			StringAssert.Contains("TellVacancy(System, prior", transactions);
			StringAssert.DoesNotContain("Inventory", lifecycle + transactions + menu);
			StringAssert.DoesNotContain("RequirePart<SocialRoles>", lifecycle + transactions + menu);
			StringAssert.DoesNotContain("AddRecipeNote", lifecycle + transactions + menu);
			StringAssert.DoesNotContain("LearnRecipe", lifecycle + transactions + menu);
		}

		[Test]
		public void RuntimePinsExactResidentCityReceiptAndReversibleNativePart()
		{
			string transaction = Read("Experience", "KingdomNamedCook.Transactions.cs");
			string marker = Read("Experience", "r_KingdomNamedCook.cs");
			string book = Read("Simulation", "City", "KingdomCityBook.02.MemosAndSidecars.cs");
			string menu = Read("Core", "KingdomCharterMenuRules.cs");
			StringAssert.Contains("KingdomResidents.TryResolveBoundBody", transaction);
			StringAssert.Contains("System.TryFindSettlement(Book", transaction);
			StringAssert.Contains("Body.AddPart(teaching)", transaction);
			StringAssert.Contains("Body.RemovePart(teaching)",
				Read("Experience", "KingdomNamedCook.Recovery.cs"));
			StringAssert.Contains("public ThousandAndFirst.KingdomNamedCookReceipt NamedCook", book);
			StringAssert.Contains("FinalizeCopy", marker);
			StringAssert.Contains("ManageNamedCook = 37", menu);
			Assert.AreEqual(37, (int)KingdomCharterAction.ManageNamedCook);
		}

		[Test]
		public void PhaseEnumIsAppendOnly()
		{
			Assert.AreEqual("0,1,2,3,4,5,6,7,8,9,10,11,12,13",
				JoinValues(typeof(KingdomNamedCookPhase)));
			Assert.AreEqual("0,1,2,3,4,5", JoinValues(typeof(KingdomNamedCookVacancyCause)));
		}

		private static KingdomNamedCookReceipt Prepare(int generation, int resident,
			string name, string body)
		{
			KingdomNamedCookReceipt receipt;
			string failure;
			Assert.IsTrue(KingdomNamedCookRules.TryPrepare(Realm, City,
				"  New   Grit Gate ", resident, name, body, generation, 100L,
				out receipt, out failure), failure);
			return receipt;
		}

		private static KingdomNamedCookVerdict Judge(bool shares = false,
			bool teaches = false, bool marker = false, bool open = false)
		{
			return KingdomNamedCookRules.JudgeCandidate(true, true, true, true, false,
				shares, teaches, marker, open, Realm, City, 17, "Ari", "body-17");
		}

		private static void AssertValid(KingdomNamedCookReceipt receipt)
		{
			string failure;
			Assert.IsTrue(KingdomNamedCookRules.Validate(receipt, out failure), failure);
		}

		private static void AssertInvalid(KingdomNamedCookReceipt receipt)
		{
			string failure;
			Assert.IsFalse(KingdomNamedCookRules.Validate(receipt, out failure));
			Assert.IsNotEmpty(failure);
		}

		private static string JoinValues(Type type)
		{
			Array values = Enum.GetValues(type);
			string[] rows = new string[values.Length];
			for (int i = 0; i < values.Length; i++)
				rows[i] = Convert.ToInt32(values.GetValue(i)).ToString();
			return string.Join(",", rows);
		}

		private static string Read(params string[] parts)
		{
			string path = TestMain.RepositoryRoot;
			for (int i = 0; i < parts.Length; i++) path = Path.Combine(path, parts[i]);
			return File.ReadAllText(path);
		}
	}
}
#endif
