namespace OpenPlane.Core.Services;

public sealed class PolicyViolationException(string message) : InvalidOperationException(message)
{
}
