using PizzaFactory.Domain.Floor;

namespace PizzaFactory.Domain.Tests;

public sealed class ServiceWindowFloorTests
{
    [Fact]
    public void opening_and_closing_a_service_is_reported_once_each()
    {
        var bus = new FloorEventBus(TimeProvider.System);
        var window = new ServiceWindow(new ServiceWindowOptions(), TimeProvider.System, bus);

        window.Open();
        window.Open();
        window.Close();
        window.Close();

        Assert.Equal(
            [FloorEventKinds.ServiceOpened, FloorEventKinds.ServiceClosed],
            bus.Since(0).Select(e => e.Kind));
    }

    [Fact]
    public void a_service_opened_from_sharepoint_says_so()
    {
        var bus = new FloorEventBus(TimeProvider.System);
        var window = new ServiceWindow(new ServiceWindowOptions(), TimeProvider.System, bus);

        using (FloorChannelScope.Enter(FloorChannel.SharePoint))
        {
            window.Open();
        }

        Assert.Equal(FloorChannel.SharePoint, bus.Since(0).Single().Channel);
    }
}
