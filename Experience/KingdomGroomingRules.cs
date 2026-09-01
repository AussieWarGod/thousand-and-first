namespace ThousandAndFirst
{
	/// <summary>Pure evidence ladder for a groomed successor. Marks are small, monotonic, and
	/// explainable from existing resident/service/schooling facts.</summary>
	public static class KingdomGroomingRules
	{
		public const int MaxServiceMarks = 2;
		public const int MaxStudyMarks = 2;
		public const int RequiredServiceMarks = 2;
		public const int RequiredStudyMarks = 2;

		public static bool ValidMarks(int ServiceMarks, int StudyMarks)
		{
			return ServiceMarks >= 0 && ServiceMarks <= MaxServiceMarks
				&& StudyMarks >= 0 && StudyMarks <= MaxStudyMarks;
		}

		/// <summary>A post begins service; one completed month proves it. Civic-office titles are
		/// identity-only and cannot shortcut successor preparation.</summary>
		public static int ServiceEvidence(bool HasPost, int MonthsServed)
		{
			if (MonthsServed >= 1) return MaxServiceMarks;
			return HasPost ? 1 : 0;
		}

		/// <summary>Schooling must be held in the nominee's own city. Full proof additionally
		/// requires exact current work backed by live education capability or its observation.</summary>
		public static int StudyEvidence(bool SchoolingHeld, bool HasEducationPost)
		{
			if (!SchoolingHeld) return 0;
			return HasEducationPost ? MaxStudyMarks : 1;
		}

		public static bool Ready(int ServiceMarks, int StudyMarks)
		{
			return ValidMarks(ServiceMarks, StudyMarks)
				&& ServiceMarks >= RequiredServiceMarks
				&& StudyMarks >= RequiredStudyMarks;
		}

		public static string Progress(int ServiceMarks, int StudyMarks)
		{
			if (!ValidMarks(ServiceMarks, StudyMarks)) return "invalid grooming proof";
			string service = ServiceMarks >= RequiredServiceMarks ? "service proven"
				: (ServiceMarks > 0 ? "service begun" : "service unproven");
			string study = StudyMarks >= RequiredStudyMarks ? "schooling proven"
				: (StudyMarks > 0 ? "schooling available; no proved education post"
					: "schooling unavailable");
			return service + "; " + study;
		}
	}
}
