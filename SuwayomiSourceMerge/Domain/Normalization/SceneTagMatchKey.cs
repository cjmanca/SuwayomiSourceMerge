namespace SuwayomiSourceMerge.Domain.Normalization;

/// <summary>
/// Represents one normalized scene-tag key used for deterministic matcher and duplicate detection behavior.
/// </summary>
internal readonly struct SceneTagMatchKey : IEquatable<SceneTagMatchKey>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="SceneTagMatchKey"/> struct.
	/// </summary>
	/// <param name="kind">Key classification that determines matching semantics.</param>
	/// <param name="value">Normalized key value for ordinal comparison.</param>
	/// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is null, empty, or whitespace.</exception>
	public SceneTagMatchKey(SceneTagMatchKeyKind kind, string value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(value);
		Kind = kind;
		Value = value;
	}

	/// <summary>
	/// Gets the key classification.
	/// </summary>
	public SceneTagMatchKeyKind Kind
	{
		get;
	}

	/// <summary>
	/// Gets the normalized key value.
	/// </summary>
	public string Value
	{
		get;
	}

	/// <inheritdoc />
	public bool Equals(SceneTagMatchKey other)
	{
		return Kind == other.Kind && string.Equals(Value, other.Value, StringComparison.Ordinal);
	}

	/// <inheritdoc />
	public override bool Equals(object? obj)
	{
		return obj is SceneTagMatchKey other && Equals(other);
	}

	/// <inheritdoc />
	public override int GetHashCode()
	{
		return HashCode.Combine((int)Kind, StringComparer.Ordinal.GetHashCode(Value));
	}

	/// <summary>
	/// Determines whether two keys are equal.
	/// </summary>
	/// <param name="left">First key.</param>
	/// <param name="right">Second key.</param>
	/// <returns><see langword="true"/> when keys are equal; otherwise, <see langword="false"/>.</returns>
	public static bool operator ==(SceneTagMatchKey left, SceneTagMatchKey right)
	{
		return left.Equals(right);
	}

	/// <summary>
	/// Determines whether two keys are not equal.
	/// </summary>
	/// <param name="left">First key.</param>
	/// <param name="right">Second key.</param>
	/// <returns><see langword="true"/> when keys are different; otherwise, <see langword="false"/>.</returns>
	public static bool operator !=(SceneTagMatchKey left, SceneTagMatchKey right)
	{
		return !left.Equals(right);
	}
}
