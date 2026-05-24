using Microsoft.Extensions.Logging.Abstractions;
using PizzaFactory.Domain;
using PizzaFactory.Domain.Entities;
using PizzaFactory.Factory;
using PizzaFactory.Infrastructure.InMemory;

namespace PizzaFactory.Factory.Tests;

public class SupplierSelfHealTests
{
    private static readonly DateTimeOffset T0 = new(2026, 5, 23, 12, 0, 0, TimeSpan.Zero);

    private sealed class FakeGateway(int grams, bool confirmed = true) : ISupplierGateway
    {
        public int Calls { get; private set; }

        public Task<RestockQuote> RequestRestockAsync(Ingredient ingredient, int requested, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new RestockQuote("Test Supplier", ingredient, grams, EtaSeconds: 5, Confirmed: confirmed));
        }
    }

    private sealed class SpySink : IEscalationSink
    {
        public int Calls { get; private set; }
        public Task RaiseAsync(Escalation escalation, CancellationToken cancellationToken = default) { Calls++; return Task.CompletedTask; }
    }

    [Fact]
    public async Task supplier_sink_orders_and_restocks_on_escalation()
    {
        var stock = new InMemoryStockRepository();
        await stock.SaveAsync(Stock.Empty); // pineapple at 0
        var gateway = new FakeGateway(grams: 1000);
        var sink = new SupplierEscalationSink(gateway, stock, new FactoryOptions(), NullLogger<SupplierEscalationSink>.Instance);

        await sink.RaiseAsync(new Escalation(Ingredient.Pineapple, 0, "low", T0));

        Assert.Equal(1, gateway.Calls);
        Assert.Equal(1000, (await stock.GetAsync()).GramsOf(Ingredient.Pineapple));
    }

    [Fact]
    public async Task supplier_sink_leaves_stock_alone_when_declined()
    {
        var stock = new InMemoryStockRepository();
        await stock.SaveAsync(Stock.Empty);
        var sink = new SupplierEscalationSink(new FakeGateway(1000, confirmed: false), stock, new FactoryOptions(), NullLogger<SupplierEscalationSink>.Instance);

        await sink.RaiseAsync(new Escalation(Ingredient.Pineapple, 0, "low", T0));

        Assert.Equal(0, (await stock.GetAsync()).GramsOf(Ingredient.Pineapple));
    }

    [Fact]
    public async Task composite_fans_out_to_every_sink()
    {
        var a = new SpySink();
        var b = new SpySink();
        var composite = new CompositeEscalationSink([a, b]);

        await composite.RaiseAsync(new Escalation(Ingredient.Mozzarella, 10, "low", T0));

        Assert.Equal(1, a.Calls);
        Assert.Equal(1, b.Calls);
    }
}
