namespace AutoContext.Engine.Core.Tests.Support.Rpc.Policies;

using AutoContext.Engine.Core.Rpc;

/// <summary>
/// Invokes one of the three <see cref="IRpcConnectionPolicy"/> log-fault
/// hooks by hook-name, so a single parameterised test can cover all
/// three on any policy implementation.
/// </summary>
internal static class PolicyTestHookInvoker
{
    public static void InvokeHook(IRpcConnectionPolicy policy, string hook, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(policy);
        switch (hook)
        {
            case nameof(IRpcConnectionPolicy.LogFrameReadFault):
                policy.LogFrameReadFault(exception);
                break;
            case nameof(IRpcConnectionPolicy.LogFrameWriteFault):
                policy.LogFrameWriteFault(exception);
                break;
            default:
                policy.LogFrameParseFault(exception);
                break;
        }
    }
}
