using System;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>The visible, attended-only verb a posted resident performs at a work.</summary>
	internal enum KingdomStationActivity : byte
	{
		None = 0,
		Tend = 1,
		Sort = 2,
		Craft = 3,
		Maintain = 4,
		Build = 5,
		Watch = 6,
		Pray = 7
	}

	/// <summary>Small immutable presentation returned by the pure activity rules.</summary>
	internal struct KingdomStationActivityCue
	{
		internal readonly KingdomStationActivity Activity;
		internal readonly string Text;
		internal readonly char Color;

		internal KingdomStationActivityCue(KingdomStationActivity activity, string text, char color)
		{
			Activity = activity;
			Text = text;
			Color = color;
		}

		internal bool Exists
		{
			get { return Activity != KingdomStationActivity.None && !string.IsNullOrEmpty(Text); }
		}
	}

	/// <summary>
	/// Maps an authoritative post to one cosmetic act. The rule grants no stock, progress,
	/// standing, skill, experience, or other state: its result is presentation only.
	/// </summary>
	internal static class KingdomStationActivityRules
	{
		internal static KingdomStationActivity For(KingdomWorkKind kind, KingdomDayShape shape)
		{
			// These two shapes are reserved by the standing-policy vocabulary. Their verbs make the
			// presentation seam total whenever the current classifier supplies either shape.
			if (shape == KingdomDayShape.Watch)
			{
				return KingdomStationActivity.Watch;
			}
			if (shape == KingdomDayShape.Shrine)
			{
				return KingdomStationActivity.Pray;
			}

			switch (kind)
			{
			case KingdomWorkKind.Growing:
				return KingdomStationActivity.Tend;
			case KingdomWorkKind.Store:
				return KingdomStationActivity.Sort;
			case KingdomWorkKind.Producer:
			case KingdomWorkKind.Refiner:
				return KingdomStationActivity.Craft;
			case KingdomWorkKind.Power:
				return KingdomStationActivity.Maintain;
			case KingdomWorkKind.Construction:
				return KingdomStationActivity.Build;
			default:
				return KingdomStationActivity.None;
			}
		}

		internal static KingdomStationActivityCue Cue(KingdomStationActivity activity)
		{
			switch (activity)
			{
			case KingdomStationActivity.Tend:
				return new KingdomStationActivityCue(activity, "*tending the rows*", 'g');
			case KingdomStationActivity.Sort:
				return new KingdomStationActivityCue(activity, "*sorting stores*", 'y');
			case KingdomStationActivity.Craft:
				return new KingdomStationActivityCue(activity, "*plying the craft*", 'W');
			case KingdomStationActivity.Maintain:
				return new KingdomStationActivityCue(activity, "*maintaining the works*", 'C');
			case KingdomStationActivity.Build:
				return new KingdomStationActivityCue(activity, "*setting a piece*", 'Y');
			case KingdomStationActivity.Watch:
				return new KingdomStationActivityCue(activity, "*keeping watch*", 'w');
			case KingdomStationActivity.Pray:
				return new KingdomStationActivityCue(activity, "*attending the shrine*", 'M');
			default:
				return new KingdomStationActivityCue(KingdomStationActivity.None, null, ' ');
			}
		}
	}
}
