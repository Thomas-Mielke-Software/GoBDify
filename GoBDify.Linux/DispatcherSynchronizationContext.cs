using System.Threading;
using Microsoft.Maui.Dispatching;

namespace GoBDify;

/// <summary>
/// SynchronizationContext, der Callbacks über den MAUI-Dispatcher auf den
/// GTK-UI-Thread postet.
///
/// Das experimentelle GTK4-Backend setzt selbst keinen SynchronizationContext
/// auf dem UI-Thread. Ohne ihn fangen <see cref="System.Progress{T}"/>-Callbacks
/// (Live-Audit-Events) und await-Fortsetzungen keinen UI-Kontext ein und laufen
/// auf ThreadPool-Threads — das Anfassen von MAUI-Controls dort wirft
/// NullReferenceExceptions (z. B. Element.SetParent). Auf Windows existiert der
/// Kontext, daher tritt das nur unter GTK auf.
/// </summary>
internal sealed class DispatcherSynchronizationContext : SynchronizationContext
{
    private readonly IDispatcher _dispatcher;

    public DispatcherSynchronizationContext(IDispatcher dispatcher) => _dispatcher = dispatcher;

    public override void Post(SendOrPostCallback d, object? state)
        => _dispatcher.Dispatch(() => d(state));

    public override void Send(SendOrPostCallback d, object? state)
    {
        if (_dispatcher.IsDispatchRequired)
            _dispatcher.Dispatch(() => d(state));
        else
            d(state);
    }

    public override SynchronizationContext CreateCopy() => this;
}
