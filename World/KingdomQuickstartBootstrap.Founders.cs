using System;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// The founding cohort, in two stages split at the reversibility boundary.
	/// <para>
	/// Stage A is physical and reversible: four bodies and every piece of gear they arrived with
	/// are allocated inside ONE grant scope, so a failure anywhere unwinds all four and their gear
	/// in reverse order and leaves the world byte-identical to before it ran. Stage B is model-only
	/// and irreversible &#8212; citizenship, the roll, the origin tally &#8212; so it runs only
	/// after the four are published by identity, and every step of it is idempotent per founder.
	/// </para>
	/// <para>
	/// A Raising receipt fences the attempt before any factory runs. The second publish names
	/// the exact four before enrollment. A recovered attempt may adopt four exact marked bodies,
	/// but an empty zone after Raising never proves that no bodies were committed elsewhere.
	/// </para>
	/// </summary>
	public static partial class KingdomQuickstartBootstrap
	{
		/// <summary>
		/// The authored cast, in index order: a hand, a drifter, a tinker and a physicker. Drawn
		/// from nothing &#8212; these are the shipped settler bodies, not minted replacements.
		/// </summary>
		private static readonly string[] FounderBlueprints =
		{
			"r_KingdomSettlerHand",
			"r_KingdomSettlerDrifter",
			"r_KingdomSettlerTinker",
			"r_KingdomSettlerPhysicker"
		};

		/// <summary>One culture per profile, four names each, in the same index order.</summary>
		private static readonly string[][] FounderNames =
		{
			new string[4] { "Sedge-at-Furrow", "Rushlight-of-Roads", "Gutter-at-Anvil",
				"Willow-at-Bedside" },
			new string[4] { "Terrace-at-Furrow", "Scree-of-Roads", "Flint-at-Anvil",
				"Cairn-at-Bedside" },
			new string[4] { "Chaff-at-Furrow", "Dunelee-of-Roads", "Slag-at-Anvil",
				"Brinewort-at-Bedside" }
		};

		/// <summary>
		/// The one entry point. Reads the receipt, not the branch that called it, so the boot pass
		/// and every later wake run exactly the same code.
		/// <para>
		/// It CANNOT fail the bootstrap. Every store the quickstart grants is already standing and
		/// verified before a founder is raised, so a refused cohort must not cost the founder the
		/// once-only completion notice, nor be reported as "stopped before granting any further
		/// stock" &#8212; which would not be true. A refusal announces itself once, here, and the
		/// caller carries on. <paramref name="SeededNow"/> is four only when THIS call closed the
		/// cohort, so the arrival is announced exactly once whichever wake does it.
		/// </para>
		/// </summary>
		private static void RunFounders(XRLGame Game, KingdomSystem System, Zone Zone,
			ref KingdomQuickstartReceipt Receipt, out int SeededNow)
		{
			SeededNow = 0;
			string failure;
			switch (Receipt.FoundersDisposition)
			{
			case KingdomQuickstartFoundersDisposition.Omitted:
			case KingdomQuickstartFoundersDisposition.Faulted:
				// Terminal. Omitted never owed a cohort; Faulted said so once and is done.
				return;
			case KingdomQuickstartFoundersDisposition.Seeded:
				if (!VerifyFounders(Zone, Receipt, out failure))
					AnnounceFoundersOnce(Game, Receipt, failure);
				return;
			case KingdomQuickstartFoundersDisposition.Pending:
			case KingdomQuickstartFoundersDisposition.Raising:
				GameObject[] cohort;
				// READ THE GROUND BEFORE MAKING ANYTHING. The scope commits four placed bodies
				// before the fence publishes their ids, so a lost write or a save cut across that
				// instant leaves four live, marked, unnamed bodies. Their reservations are minted
				// from the receipt's own frozen ground and never needed publishing, so they are a
				// positive witness only: absence after Raising cannot authorize replacement.
				GameObject[] standing;
				int found;
				if (!TryObserveFounders(Zone, Receipt, out standing, out found, out failure)
					|| (found != 0 && found != KingdomQuickstartRules.FounderCount)
					|| (found == 0 && !KingdomQuickstartRules.CanRaiseFounderCohort(Receipt.FoundersDisposition, found))
					|| (found != 0 && !VerifyFounderCohort(Zone, standing, Receipt, out failure)))
				{
					// Zero after Raising, one to three bodies, a duplicated reservation, or a cohort that no longer
					// proves itself: custody cannot be settled either way. Fence the world so no
					// replacement can ever be minted, and never stage a second cohort over an
					// unproved one.
					QuarantineGrant(Game, Guid.NewGuid().ToString("N"));
					AnnounceFoundersOnce(Game, Receipt,
						"The founding cohort could not be proved on the ground: "
							+ (string.IsNullOrEmpty(failure) ? "an incomplete party" : failure));
					return;
				}
				if (found == KingdomQuickstartRules.FounderCount) cohort = standing;
				else
				{
					if (!Restate(Game, ref Receipt, KingdomQuickstartFoundersDisposition.Raising, null, out failure))
					{
						AnnounceFoundersOnce(Game, Receipt, "The founding attempt could not be fenced: " + failure);
						return;
					}
					string staged;
					if (!TryStageFounderBodies(Game, Zone, Receipt, out cohort, out staged))
					{
						// Only the scope's exact completed unwind clears quarantine. Preserve Raising
						// whenever cleanup is uncertain; a later empty-zone scan cannot clear it.
						string cleared;
						if (!GrantQuarantined(Game) && !Restate(Game, ref Receipt,
							KingdomQuickstartFoundersDisposition.Pending, null, out cleared))
							staged = staged + "; " + cleared;
						AnnounceFoundersOnce(Game, Receipt,
							"The founding cohort could not be raised whole: " + staged);
						return;
					}
				}
				string[] ids = new string[KingdomQuickstartRules.FounderCount];
				for (int i = 0; i < ids.Length; i++) ids[i] = cohort[i].IDIfAssigned;
				if (!Restate(Game, ref Receipt,
					KingdomQuickstartFoundersDisposition.Seeding, ids, out failure))
				{
					// The four are committed and placed but unnamed. They are NOT unwound: they
					// wear their own reservations, so the observation above adopts these exact four
					// on the next wake instead of making four more. Destroying them here would
					// throw away a recoverable cohort for a transient write.
					AnnounceFoundersOnce(Game, Receipt,
						"The founding cohort could not be published: " + failure);
					return;
				}
				break;
			case KingdomQuickstartFoundersDisposition.Seeding:
				break;
			default:
				AnnounceFoundersOnce(Game, Receipt,
					"The quickstart receipt carried no lawful founders disposition.");
				return;
			}
			if (TryEnrolFounders(Game, System, Zone, ref Receipt, out failure))
				SeededNow = KingdomQuickstartRules.FounderCount;
			else AnnounceFoundersOnce(Game, Receipt, failure);
		}

		/// <summary>
		/// Stage A. ONE scope for the whole cohort, because the scope's rollback walks its
		/// allocations in reverse: each body is allocated before its own gear, so reverse order
		/// takes a body's gear off before the body, which exact-custody removal requires. Any
		/// failure anywhere therefore unwinds all four bodies and all their gear, behind the
		/// quarantine fence, which is exact zero-or-four for the physical half.
		/// </summary>
		private static bool TryStageFounderBodies(XRLGame Game, Zone Zone,
			KingdomQuickstartReceipt Receipt, out GameObject[] Cohort, out string Failure)
		{
			string failure = "";
			GameObject[] cohort = new GameObject[KingdomQuickstartRules.FounderCount];
			bool created = TryCreateFreshGrant(Game, scope =>
			{
				for (int index = 0; index < cohort.Length; index++)
				{
					int slot = index;
					GameObject body = scope.Create(() => GameObject.Create(
						FounderBlueprints[slot]));
					if (!GameObject.Validate(body) || !body.IsCreature || body.IsPlayer()
						|| body.CurrentCell != null)
					{
						failure = "a founding body was not a fresh, unplaced creature";
						return null;
					}
					// Their own gear, registered before the next body is allocated, so the unwind
					// order stays gear-then-body for every founder in the cohort.
					if (!TryRegisterFounderGear(scope, body))
					{
						failure = "a founding body's gear could not be taken into custody";
						return null;
					}
					int x, y;
					if (!KingdomQuickstartRules.TryFounderCell(slot, out x, out y)
						|| !TryPrepareMarked(body, KingdomQuickstartRules.FounderMarker(
							Receipt, slot), out failure)
						|| !TryPlaceGrant(Zone, body, x, y, out failure)) return null;
					cohort[slot] = body;
				}
				return cohort[0];
			}, candidate => ReferenceEquals(candidate, cohort[0])
				&& VerifyFounderPlacement(Zone, cohort, Receipt, out failure),
				out GameObject grant);
			Cohort = created ? cohort : null;
			Failure = created ? "" : FreshGrantFailure(failure);
			return created;
		}

		/// <summary>
		/// Takes every object the body arrived carrying or wearing into the same scope. Nothing is
		/// created here: the factory hands back what the blueprint already made, which is what
		/// makes the reverse unwind able to prove exact custody of each piece.
		/// </summary>
		private static bool TryRegisterFounderGear(
			KingdomQuickstartGrantScope<GameObject> Scope, GameObject Body)
		{
			if (Body.Inventory != null)
			{
				GameObject[] carried = Body.Inventory.Objects.ToArray();
				for (int i = 0; i < carried.Length; i++)
				{
					GameObject item = carried[i];
					if (!GameObject.Validate(item)) return false;
					if (!ReferenceEquals(Scope.Create(() => item), item)) return false;
				}
			}
			if (Body.Body == null) return true;
			foreach (GameObject worn in Body.Body.GetEquippedObjects())
			{
				GameObject item = worn;
				if (!GameObject.Validate(item)) return false;
				if (!ReferenceEquals(Scope.Create(() => item), item)) return false;
			}
			return true;
		}

		/// <summary>
		/// Stage A's own verification: four exact bodies, each on its own reserved cell, each
		/// wearing its own indexed reservation, each with an identity, and no two the same. The
		/// cell is pinned here and ONLY here, because this runs in the same call that placed them.
		/// </summary>
		private static bool VerifyFounderPlacement(Zone Zone, GameObject[] Cohort,
			KingdomQuickstartReceipt Receipt, out string Failure)
		{
			if (!VerifyFounderCohort(Zone, Cohort, Receipt, out Failure)) return false;
			for (int i = 0; i < Cohort.Length; i++)
			{
				int x, y;
				if (!KingdomQuickstartRules.TryFounderCell(i, out x, out y)
					|| !ExactRole(Zone, Cohort[i], FounderBlueprints[i], x, y))
				{
					Failure = "founder " + i + " was not on its own reserved cell";
					return false;
				}
			}
			return true;
		}

	}
}
