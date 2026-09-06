using System;
using System.Reflection;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	// Temporary synthetic corruption on the retained native fixture; never ordinary-save acceptance.
	internal static partial class KingdomFoundingHeartReservationNativeCases
	{
		internal static void TypedStates(XRLGame game, Zone zone, KingdomFoundingHeartPlan plan)
		{
			Frame frame = new Frame(game, zone, plan);
			for (int slot = 0; slot < 7; slot++)
				for (int mask = 0; mask < 32; mask++)
				{
					try
					{
						frame.Inject(slot, mask, frame.Canonical[slot]);
						bool accepted = frame.Ensure();
						if (mask == 0 && accepted) frame.AcceptCreated(slot);
						frame.Check();
						Require(accepted == (mask == 0 || mask == 1), "typed verdict slot=" + slot + " mask=" + mask);
						if (mask > 1) frame.RefuseAudit();
					}
					finally { frame.Restore(); }
				}
		}

		internal static void MalformedStrings(XRLGame game, Zone zone, KingdomFoundingHeartPlan plan)
		{
			Frame frame = new Frame(game, zone, plan);
			for (int slot = 0; slot < 7; slot++)
				foreach (string raw in new[] { null, "", "hr1|native-synthetic-malformed" })
				{
					try
					{
						frame.Inject(slot, 1, raw);
						bool accepted = frame.Ensure();
						frame.Check();
						Require(!accepted, "malformed string accepted slot=" + slot);
						frame.RefuseAudit();
					}
					finally { frame.Restore(); }
				}
		}

		internal static void PreflightAllSeven(XRLGame game, Zone zone, KingdomFoundingHeartPlan plan)
		{
			Frame frame = new Frame(game, zone, plan);
			try
			{
				frame.Inject(0, 0, frame.Canonical[0]);
				frame.Inject(6, 1, "hr1|native-synthetic-malformed-final");
				bool accepted = frame.Ensure();
				frame.Check();
				Require(!accepted, "all-seven preflight accepted malformed final reservation");
				frame.RefuseAudit();
			}
			finally { frame.Restore(); }
		}

		private static MethodInfo ExactEnsure()
		{
			MethodInfo method = typeof(KingdomPlots).GetMethod("EnsureFoundingHeartReservations",
				BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null,
				new[] { typeof(KingdomFoundingHeartPlan) }, null);
			Require(method != null && method.IsPrivate && method.ReturnType == typeof(bool)
				&& !method.ContainsGenericParameters && method.DeclaringType == typeof(KingdomPlots),
				"exact production EnsureFoundingHeartReservations signature unavailable");
			return method;
		}

		private static void Require(bool condition, string failure)
		{
			if (!condition) throw new InvalidOperationException("native reservation cases: " + failure);
		}
	}
}
