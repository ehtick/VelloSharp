using System.Threading;
using System.Threading.Tasks;

namespace VelloSharp.Composition.Telemetry;

public interface ICommandHandler
{
    ValueTask<CommandResult> HandleAsync(CommandRequest request, CancellationToken cancellationToken);
}
