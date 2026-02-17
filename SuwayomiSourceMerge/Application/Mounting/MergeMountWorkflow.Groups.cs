using SuwayomiSourceMerge.Infrastructure.Mounts;

namespace SuwayomiSourceMerge.Application.Mounting;

/// <summary>
/// Grouping models for <see cref="MergeMountWorkflow"/>.
/// </summary>
internal sealed partial class MergeMountWorkflow
{
	/// <summary>
	/// Mutable builder for one grouped canonical title.
	/// </summary>
	private sealed class MergeTitleGroupBuilder
	{
		/// <summary>
		/// Candidate set used for source-path de-duplication.
		/// </summary>
		private readonly HashSet<string> _sourcePathSet = new(PathSafetyPolicy.GetPathComparer());

		/// <summary>
		/// Candidate list in deterministic insertion order.
		/// </summary>
		private readonly List<MergerfsSourceBranchCandidate> _sourceBranches = [];

		/// <summary>
		/// Initializes a new instance of the <see cref="MergeTitleGroupBuilder"/> class.
		/// </summary>
		/// <param name="groupKey">Group key.</param>
		/// <param name="canonicalTitle">Canonical title.</param>
		public MergeTitleGroupBuilder(string groupKey, string canonicalTitle)
		{
			GroupKey = groupKey;
			CanonicalTitle = canonicalTitle;
		}

		/// <summary>
		/// Gets group key.
		/// </summary>
		public string GroupKey
		{
			get;
		}

		/// <summary>
		/// Gets canonical title.
		/// </summary>
		public string CanonicalTitle
		{
			get;
		}

		/// <summary>
		/// Adds one source branch candidate when not already present.
		/// </summary>
		/// <param name="sourceName">Source name.</param>
		/// <param name="sourcePath">Source title path.</param>
		public void AddSourceBranch(string sourceName, string sourcePath)
		{
			if (!_sourcePathSet.Add(sourcePath))
			{
				return;
			}

			_sourceBranches.Add(new MergerfsSourceBranchCandidate(sourceName, sourcePath));
		}

		/// <summary>
		/// Builds immutable group state.
		/// </summary>
		/// <returns>Immutable group state.</returns>
		public MergeTitleGroup Build()
		{
			return new MergeTitleGroup(GroupKey, CanonicalTitle, _sourceBranches);
		}
	}

	/// <summary>
	/// Immutable grouped title state used during one merge pass.
	/// </summary>
	private sealed class MergeTitleGroup
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MergeTitleGroup"/> class.
		/// </summary>
		/// <param name="groupKey">Group key.</param>
		/// <param name="canonicalTitle">Canonical title.</param>
		/// <param name="sourceBranches">Source branches.</param>
		public MergeTitleGroup(
			string groupKey,
			string canonicalTitle,
			IReadOnlyList<MergerfsSourceBranchCandidate> sourceBranches)
		{
			GroupKey = groupKey;
			CanonicalTitle = canonicalTitle;
			SourceBranches = sourceBranches.ToArray();
		}

		/// <summary>
		/// Gets group key.
		/// </summary>
		public string GroupKey
		{
			get;
		}

		/// <summary>
		/// Gets canonical title.
		/// </summary>
		public string CanonicalTitle
		{
			get;
		}

		/// <summary>
		/// Gets source branches.
		/// </summary>
		public IReadOnlyList<MergerfsSourceBranchCandidate> SourceBranches
		{
			get;
		}
	}
}
