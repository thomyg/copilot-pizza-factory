namespace PizzaFactory.FrontOfHouse;

/// <summary>
/// The kill switch for public ordering. Flip it closed to stop the audience submitting orders
/// (e.g. between demo runs, or if things get rowdy) and open it when you want them in. Enforced
/// in <see cref="GuestOrderService"/> so it holds regardless of UI.
/// </summary>
public sealed class OrderingGate
{
    private volatile bool _isOpen = true;

    public bool IsOpen => _isOpen;

    public void Open() => _isOpen = true;

    public void Close() => _isOpen = false;
}
