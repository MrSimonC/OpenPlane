using OpenPlane.Core.Models;

namespace OpenPlane.Core.Abstractions;

public interface IAccessPolicyService
{
    bool CanRead(string path, WorkspacePolicy policy);
    bool CanWrite(string path, WorkspacePolicy policy);
    bool CanCreate(string path, WorkspacePolicy policy);
}
