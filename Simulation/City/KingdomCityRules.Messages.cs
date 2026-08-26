using System;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomCityRules
	{
		/// <summary>
		/// The realm's simulation seed, minted once at founding.
		/// <para>
		/// The kernel is explicit that it never generates one and that "whatever mints it must
		/// domain-separate on realm incarnation" (<c>KernelSeed128</c>). So the mint is a pure
		/// function of the world seed, the realm's name and the tick the water was poured: two
		/// realms in one world differ, the same realm across a reload does not, and a test can
		/// assert both without a clock or a random source in the room.
		/// </para>
		/// <para>
		/// FNV-1a over a canonical byte order, with the two halves separated by their own offset
		/// basis. This is an identity mint, never a cryptographic one, and the kernel's counter
		/// mode is what actually shapes the draws.
		/// </para>
		/// </summary>
		internal static bool TryMintSeed(int worldSeed, string realmName, long foundedTick, out KernelSeed128 seed, out KingdomCityFault fault)
		{
			seed = default(KernelSeed128);
			if (realmName == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			if (foundedTick < 0L)
			{
				fault = KingdomCityFault.InvalidTick;
				return false;
			}
			seed = new KernelSeed128(
				Mint(0xCBF29CE484222325UL, worldSeed, realmName, foundedTick),
				Mint(0x9E3779B97F4A7C15UL, worldSeed, realmName, foundedTick));
			fault = KingdomCityFault.None;
			return true;
		}

		/// <summary>
		/// What the founder is told when the city carries its own stock to where they are standing.
		/// Plain, in the register the ledger already uses: this is news about drams, not a rule.
		/// </summary>
		internal static string CarryNote(KingdomStockKind kind, long amount, string realmName)
		{
			if (amount <= 0L)
			{
				return null;
			}
			string realm = string.IsNullOrEmpty(realmName) ? "the city" : realmName;
			if (kind == KingdomStockKind.Water)
			{
				return amount + " drams came in from " + realm + "'s other quarters, out of the oldest casks first.";
			}
			if (kind == KingdomStockKind.Food)
			{
				return amount + ((amount == 1L) ? " serving was" : " servings were") + " carried in from " + realm + "'s other pantries.";
			}
			return null;
		}

		/// <summary>
		/// The difference between what the model expected and what the ground actually holds,
		/// attributed rather than repaired (LIVING-CITY-ARCHITECTURE &sect;3.1 step 4). A cask with
		/// less in it than the book says means the founder poured some, and that is a story rather
		/// than a bug. Null when the two agree, which is the ordinary case.
		/// </summary>
		internal static string ReconcileNote(long water, long food)
		{
			if (water == 0L && food == 0L)
			{
				return null;
			}
			string clause = null;
			if (water < 0L)
			{
				clause = Join(clause, (-water) + " drams fewer than the books had");
			}
			else if (water > 0L)
			{
				clause = Join(clause, water + " drams more than the books had");
			}
			if (food < 0L)
			{
				clause = Join(clause, (-food) + " fewer servings than the books had");
			}
			else if (food > 0L)
			{
				clause = Join(clause, food + " more servings than the books had");
			}
			return "The stores hold " + clause + ". The stores are right and the books have been corrected.";
		}

		/// <summary>
		/// What is still owed after the containers have paid what they can. Never silently
		/// forgiven: LIVING-CITY-ARCHITECTURE &sect;3.9 rules that a mismatch is named.
		/// </summary>
		internal static string ShortfallNote(int waterOwed, int foodOwed)
		{
			if (waterOwed == 0 && foodOwed == 0)
			{
				return null;
			}
			if (waterOwed < 0 || foodOwed < 0)
			{
				string clause = null;
				if (waterOwed < 0)
				{
					clause = Join(clause, (-waterOwed) + " drams");
				}
				if (foodOwed < 0)
				{
					clause = Join(clause, (-foodOwed) + " servings");
				}
				return "The city drew " + clause + " it did not have here. The debt stands against these stores.";
			}
			string held = null;
			if (waterOwed > 0)
			{
				held = Join(held, waterOwed + " drams");
			}
			if (foodOwed > 0)
			{
				held = Join(held, foodOwed + " servings");
			}
			return "The books hold " + held + " these stores have no room for. It waits.";
		}

		/// <summary>
		/// What the founder is told when a porter puts a load down beside them. Addendum 12(c)'s
		/// canonical image, in the register the ledger already uses.
		/// </summary>
		internal static string PorterNote(int servings, string store)
		{
			if (servings <= 0)
			{
				return null;
			}
			string where = string.IsNullOrEmpty(store) ? "the store" : ("the " + store);
			return "A porter set " + servings + ((servings == 1) ? " serving" : " servings")
				+ " down in " + where + ", nodded, and went back the way they came.";
		}

		/// <summary>
		/// What the founder is told when a carrier could not finish. LIVING-CITY-ARCHITECTURE
		/// &sect;3.7: a job whose elapsed exceeds twice its projected duration <b>fails and is
		/// told</b>, and the cargo is real items that stay where they fell &mdash; so a founder who
		/// blocks a doorway forever produces a story rather than an unbounded job set.
		/// </summary>
		internal static string PorterFailedNote(int servings)
		{
			if (servings <= 0)
			{
				return "A carrier gave up on the road and turned back.";
			}
			return "A carrier could not get through, and set " + servings
				+ ((servings == 1) ? " serving" : " servings") + " down where they stood.";
		}

		/// <summary>
		/// The one ledger line the stale-transient sweep owes when it fires
		/// (LIVING-CITY-ARCHITECTURE &sect;3.8). <b>Deduplication, not destruction of property</b>,
		/// and the register says exactly that: the load reached the store by another hand.
		/// </summary>
		internal static string SweptNote(int carriers)
		{
			if (carriers <= 0)
			{
				return null;
			}
			return ((carriers == 1) ? "The load" : "The loads") + " you left on the road reached the store by another hand.";
		}

		/// <summary>
		/// The heartbeat's one line an hour. LIVING-CITY-ARCHITECTURE &sect;3.6 caps a slice at one
		/// told line city-wide, so a shortfall that has just begun says itself once and then lives
		/// in the status report.
		/// </summary>
		internal static string SliceNote(string cityName, int thirds)
		{
			if (thirds <= 0)
			{
				return null;
			}
			string city = string.IsNullOrEmpty(cityName) ? "the city" : cityName;
			return "Word from " + city + ": its stores are being drawn down faster than they are filling.";
		}

		/// <summary>
		/// The audit of LIVING-CITY-ARCHITECTURE &sect;3.9, as one greppable line: model total,
		/// what of it is still owed to real containers, ground total, and whether the three agree.
		/// <para>
		/// I1 in full is <c>model total == ground total + counter-owed</c>, per stock kind, at
		/// every instant. Before W6 the two owed figures were always zero on the seated row by the
		/// time this ran, so the line compared <c>model</c> to <c>ground</c> directly and was right
		/// by accident. W6 gives the model a producing rate, which means a seated row can carry a
		/// real claim that the containers have not taken yet — so the line now states the whole
		/// identity and MISMATCHes on the whole identity.
		/// </para>
		/// <para>
		/// <c>debt</c> is the signed per-kind claim (positive: made and not yet poured; negative:
		/// drunk and not yet drawn). <c>owed=n/3</c> is the catch-up counter's weighted thirds and
		/// is unchanged — it says how much of a turn's budget the backlog wants, not how many
		/// drams it is.
		/// </para>
		/// </summary>
		internal static string AuditNote(long modelWater, long debtWater, long groundWater, long modelFood, long debtFood, long groundFood, int owedThirds)
		{
			return "audit water model=" + modelWater + " debt=" + debtWater + " ground=" + groundWater
				+ " food model=" + modelFood + " debt=" + debtFood + " ground=" + groundFood
				+ " owed=" + owedThirds + "/3"
				+ ((modelWater - debtWater == groundWater && modelFood - debtFood == groundFood) ? "" : " MISMATCH");
		}

		/// <summary>
		/// A stable identifier for a string, for a work row that has to survive a save.
		/// <para>
		/// FNV-1a, written out rather than taken from the runtime, for the reason the kernel gives
		/// about hashing at all: a runtime hash is not stable across processes, and an id that
		/// changes when the game restarts is not an id.
		/// </para>
		/// </summary>
		internal static int StableId(string value)
		{
			if (string.IsNullOrEmpty(value))
			{
				return 0;
			}
			uint hash = 2166136261u;
			for (int i = 0; i < value.Length; i++)
			{
				hash ^= (uint)(value[i] & 0xFF);
				hash *= 16777619u;
				hash ^= (uint)((value[i] >> 8) & 0xFF);
				hash *= 16777619u;
			}
			return (int)(hash & 0x7FFFFFFFu);
		}

		private static string Join(string standing, string clause)
		{
			return (standing == null) ? clause : (standing + " and " + clause);
		}

	}
}
