using Xunit;

namespace RoEFactura.Tests.Live;

/// <summary>
/// A <see cref="TheoryAttribute"/> that only runs when the environment variable
/// <c>ANAF_LIVE_VALIDATION</c> is <c>1</c>; otherwise the theory is reported as skipped. Used for opt-in
/// calls to the public, stateless ANAF validator — never enabled in CI.
/// </summary>
public sealed class AnafLiveTheoryAttribute : TheoryAttribute
{
    public const string EnvironmentVariable = "ANAF_LIVE_VALIDATION";

    public AnafLiveTheoryAttribute()
    {
        if (Environment.GetEnvironmentVariable(EnvironmentVariable) != "1")
        {
            Skip = "Set ANAF_LIVE_VALIDATION=1 to call the public ANAF validator.";
        }
    }
}
