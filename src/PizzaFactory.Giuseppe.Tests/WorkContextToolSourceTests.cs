using Microsoft.Extensions.AI;
using PizzaFactory.Giuseppe.Tools;
using PizzaFactory.Giuseppe.WorkContext;

namespace PizzaFactory.Giuseppe.Tests;

public class WorkContextToolSourceTests
{
    [Fact]
    public async Task exposes_a_single_find_meeting_function()
    {
        var source = new WorkContextToolSource(new RehearsalWorkContext());

        var tools = await source.GetToolsAsync();

        var tool = Assert.Single(tools);
        Assert.Equal("find_meeting", tool.Name);
        Assert.IsAssignableFrom<AIFunction>(tool);
    }

    [Fact]
    public async Task invoking_find_meeting_returns_the_meeting_as_json()
    {
        var source = new WorkContextToolSource(new RehearsalWorkContext());
        var tool = (AIFunction)(await source.GetToolsAsync())[0];

        var result = await tool.InvokeAsync(new AIFunctionArguments { ["query"] = "friday team retro" });

        var json = result?.ToString();
        Assert.NotNull(json);
        Assert.Contains("Team Retro", json);
        Assert.Contains("rehearsal", json);
    }
}
