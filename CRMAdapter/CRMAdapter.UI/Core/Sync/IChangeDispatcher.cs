// IChangeDispatcher.cs: Dispatches queued offline changes back to the API when connectivity is restored.
using System.Threading;
using System.Threading.Tasks;

namespace CRMAdapter.UI.Core.Sync;

public interface IChangeDispatcher
{
    Task<ChangeDispatchResult> DispatchAsync(ChangeEnvelope change, CancellationToken cancellationToken);
}
