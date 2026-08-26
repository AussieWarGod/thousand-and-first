using System.Text;

using ThousandAndFirst.Simulation.City;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomCropRules
	{
		// ==================================================================================
		// The draw. Whether a gathering hands back sowable seed is asked once per field per
		// cycle, counter-based on a key naming the settlement, the field and that cycle's own
		// ordinal - so a reload asks the same question and gets the same answer, and a founder
		// cannot save-scum a seed out of a harvest they have already seen.
		// ==================================================================================

		private const int CropRulesVersion = 1;

		private const uint CropDrawIndex = 0u;

		/// <summary>Fixed, all-zero seed. Domain separation comes entirely from the settlement id,
		/// the stream and the ordinal baked into the key; whether a row went to seed is not a
		/// question that needs to be unguessable.</summary>
		private static readonly KernelSeed128 CropSeed = default(KernelSeed128);

		private const string StreamPrefix = "taf:crop:";

		private const string StreamSuffix = ":v1";

		/// <summary>The byte budget <c>KernelSemanticId</c> allows an id. Stated here rather than
		/// reached for, the same way <c>KingdomSubsidenceRules</c> states it.</summary>
		private const int KernelSemanticIdBudget = 128;

		/// <summary>Which question a draw answers. Frozen: never zero, never renumbered.</summary>
		public enum CropChannel
		{
			/// <summary>Whether this gathering also returned sowable seed.</summary>
			SeedReturn = 1
		}

		/// <summary>Folds one field's own id into the frozen <c>taf:</c> semantic-id grammar, so
		/// two fields asked about the same cycle are not forced to share one answer.</summary>
		/// <param name="FieldId">The field's persistent <c>GameObject.id</c>. Null and blank yield
		/// the lane an unidentified field would draw on.</param>
		internal static string FieldStream(string FieldId)
		{
			StringBuilder builder = new StringBuilder(StreamPrefix);
			int room = KernelSemanticIdBudget - StreamPrefix.Length - StreamSuffix.Length;
			if (!string.IsNullOrEmpty(FieldId))
			{
				foreach (char c in FieldId)
				{
					if (builder.Length - StreamPrefix.Length >= room)
					{
						break;
					}
					if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
					{
						builder.Append(c);
					}
					else if (c >= 'A' && c <= 'Z')
					{
						builder.Append((char)(c + 32));
					}
					else
					{
						builder.Append('-');
					}
				}
			}
			if (builder.Length == StreamPrefix.Length)
			{
				builder.Append("unidentified");
			}
			builder.Append(StreamSuffix);
			return builder.ToString();
		}

		/// <summary>
		/// Whether this gathering hands back sowable seed. False (never faulting) for a malformed
		/// settlement id, which returns nothing and is the safe answer &mdash; a seed the rules
		/// could not decide on is a seed the founder simply did not get.
		/// </summary>
		/// <param name="SettlementId">The settlement's kernel id.</param>
		/// <param name="FieldId">The field's persistent object id.</param>
		/// <param name="Ordinal">This cycle's own ordinal, counted from the field's first
		/// gathering, so every cycle asks fresh and none is ever re-rolled.</param>
		public static bool RollSeedReturn(string SettlementId, string FieldId, ulong Ordinal)
		{
			if (!SemanticEventKey.TryCreate(CropRulesVersion, SettlementId, FieldStream(FieldId), (uint)CropChannel.SeedReturn, Ordinal, out var key, out var _))
			{
				return false;
			}
			if (!CounterRandom.TryDrawBelow(CropSeed, key, CropDrawIndex, 100uL, out var value, out var _))
			{
				return false;
			}
			return (int)value < SeedReturnChancePercent;
		}

		/// <summary>
		/// Seed a whole reckoning returns: one draw per cycle, capped at
		/// <see cref="MaxSeedsPerResolve"/>. Nothing is returned by a gathering that yielded
		/// nothing &mdash; a field nobody worked does not go to seed either.
		/// </summary>
		public static int SeedReturned(string SettlementId, string FieldId, ulong FirstOrdinal, int Cycles, int Yield)
		{
			if (Cycles <= 0 || Yield <= 0)
			{
				return 0;
			}
			int seeds = 0;
			for (int i = 0; i < Cycles && seeds < MaxSeedsPerResolve; i++)
			{
				if (RollSeedReturn(SettlementId, FieldId, FirstOrdinal + (ulong)i))
				{
					seeds++;
				}
			}
			return seeds;
		}
	}
}
