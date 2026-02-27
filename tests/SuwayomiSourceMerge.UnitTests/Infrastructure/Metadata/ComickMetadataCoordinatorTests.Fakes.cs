namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using SuwayomiSourceMerge.Infrastructure.Metadata;
using SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Additional test doubles for <see cref="ComickMetadataCoordinatorTests"/>.
/// </summary>
public sealed partial class ComickMetadataCoordinatorTests
{
	/// <summary>
	/// Recording API gateway fake for coordinator tests.
	/// </summary>
	private sealed class RecordingComickApiGateway : IComickApiGateway
	{
		/// <summary>
		/// Search callback dependency.
		/// </summary>
		private readonly Func<string, CancellationToken, ComickDirectApiResult<ComickSearchResponse>> _searchHandler;

		/// <summary>
		/// Initializes a new instance of the <see cref="RecordingComickApiGateway"/> class.
		/// </summary>
		/// <param name="searchHandler">Search callback dependency.</param>
		public RecordingComickApiGateway(Func<string, CancellationToken, ComickDirectApiResult<ComickSearchResponse>> searchHandler)
		{
			_searchHandler = searchHandler ?? throw new ArgumentNullException(nameof(searchHandler));
		}

		/// <summary>
		/// Gets the number of search calls.
		/// </summary>
		public int SearchCallCount
		{
			get;
			private set;
		}

		/// <inheritdoc />
		public Task<ComickDirectApiResult<ComickSearchResponse>> SearchAsync(
			string query,
			CancellationToken cancellationToken = default)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(query);
			SearchCallCount++;
			return Task.FromResult(_searchHandler(query, cancellationToken));
		}

		/// <inheritdoc />
		public Task<ComickDirectApiResult<ComickComicResponse>> GetComicAsync(
			string slug,
			CancellationToken cancellationToken = default)
		{
			throw new InvalidOperationException("Comic detail requests are not expected for these coordinator test scenarios.");
		}
	}

	/// <summary>
	/// Recording candidate matcher fake for coordinator tests.
	/// </summary>
	private sealed class RecordingComickCandidateMatcher : IComickCandidateMatcher
	{
		/// <summary>
		/// Gets or sets the next match result returned by the fake.
		/// </summary>
		public ComickCandidateMatchResult NextMatchResult
		{
			get;
			set;
		} = new ComickCandidateMatchResult(
			ComickCandidateMatchOutcome.NoHighConfidenceMatch,
			matchedCandidate: null,
			ComickCandidateMatchResult.NoMatchCandidateIndex,
			hadTopTie: false,
			matchScore: 0);

		/// <summary>
		/// Gets or sets a value indicating whether the matcher should throw <see cref="OperationCanceledException"/>.
		/// </summary>
		public bool ThrowOperationCanceledOnCall
		{
			get;
			set;
		}

		/// <summary>
		/// Gets or sets an optional callback executed immediately before throwing cancellation.
		/// </summary>
		public Action? BeforeThrowOperationCanceled
		{
			get;
			set;
		}

		/// <summary>
		/// Gets the number of match calls.
		/// </summary>
		public int MatchCallCount
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets the most recent expected-title list passed to the matcher.
		/// </summary>
		public IReadOnlyList<string> LastExpectedTitles
		{
			get;
			private set;
		} = [];

		/// <inheritdoc />
		public Task<ComickCandidateMatchResult> MatchAsync(
			IReadOnlyList<ComickSearchComic> candidates,
			IReadOnlyList<string> expectedTitles,
			CancellationToken cancellationToken = default)
		{
			MatchCallCount++;
			LastExpectedTitles = expectedTitles.ToArray();
			if (ThrowOperationCanceledOnCall)
			{
				BeforeThrowOperationCanceled?.Invoke();
				throw new OperationCanceledException(cancellationToken);
			}

			return Task.FromResult(NextMatchResult);
		}
	}

	/// <summary>
	/// Recording cover-service fake for coordinator tests.
	/// </summary>
	private sealed class RecordingOverrideCoverService : IOverrideCoverService
	{
		/// <summary>
		/// Gets or sets the next cover-service result.
		/// </summary>
		public OverrideCoverResult? NextResult
		{
			get;
			set;
		}

		/// <summary>
		/// Gets the number of cover ensure calls.
		/// </summary>
		public int CallCount
		{
			get;
			private set;
		}

		/// <inheritdoc />
		public Task<OverrideCoverResult> EnsureCoverJpgAsync(
			OverrideCoverRequest request,
			CancellationToken cancellationToken = default)
		{
			CallCount++;
			if (NextResult is not null)
			{
				return Task.FromResult(NextResult);
			}

			return Task.FromResult(
				new OverrideCoverResult(
					OverrideCoverOutcome.WriteFailed,
					Path.Combine(request.PreferredOverrideDirectoryPath, "cover.jpg"),
					coverJpgExists: false,
					existingCoverPath: null,
					coverUri: null,
					diagnostic: "not expected"));
		}
	}
}
