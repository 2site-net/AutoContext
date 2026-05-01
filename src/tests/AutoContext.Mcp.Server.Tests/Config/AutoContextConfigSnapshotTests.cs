namespace AutoContext.Mcp.Server.Tests.Config;

using AutoContext.Mcp.Server.Config;

public sealed class AutoContextConfigSnapshotTests
{
    [Fact]
    public void Should_start_with_empty_disabled_state()
    {
        var snapshot = new AutoContextConfigSnapshot();

        Assert.Multiple(
            () => Assert.Empty(snapshot.DisabledTools),
            () => Assert.Empty(snapshot.DisabledTasks),
            () => Assert.False(snapshot.IsToolDisabled("anything")),
            () => Assert.False(snapshot.IsTaskDisabled("anything", "any_task")));
    }

    [Fact]
    public void Should_apply_disabled_tools_from_first_update()
    {
        var snapshot = new AutoContextConfigSnapshot();

        var changed = snapshot.Update(new AutoContextConfigSnapshotDto
        {
            DisabledTools = ["alpha_tool", "beta_tool"],
        });

        Assert.Multiple(
            () => Assert.True(changed),
            () => Assert.True(snapshot.IsToolDisabled("alpha_tool")),
            () => Assert.True(snapshot.IsToolDisabled("beta_tool")),
            () => Assert.False(snapshot.IsToolDisabled("gamma_tool")));
    }

    [Fact]
    public void Should_apply_disabled_tasks_from_update()
    {
        var snapshot = new AutoContextConfigSnapshot();

        snapshot.Update(new AutoContextConfigSnapshotDto
        {
            DisabledTasks = new Dictionary<string, List<string>>
            {
                ["alpha_tool"] = ["task_one", "task_two"],
            },
        });

        Assert.Multiple(
            () => Assert.True(snapshot.IsTaskDisabled("alpha_tool", "task_one")),
            () => Assert.True(snapshot.IsTaskDisabled("alpha_tool", "task_two")),
            () => Assert.False(snapshot.IsTaskDisabled("alpha_tool", "task_three")),
            () => Assert.False(snapshot.IsTaskDisabled("beta_tool", "task_one")));
    }

    [Fact]
    public void Should_report_no_change_when_replay_matches_current_snapshot()
    {
        var snapshot = new AutoContextConfigSnapshot();
        snapshot.Update(new AutoContextConfigSnapshotDto
        {
            DisabledTools = ["alpha_tool"],
            DisabledTasks = new Dictionary<string, List<string>>
            {
                ["alpha_tool"] = ["task_one"],
            },
        });

        var changeCount = 0;
        snapshot.Changed += (_, _) => changeCount++;

        var changed = snapshot.Update(new AutoContextConfigSnapshotDto
        {
            DisabledTools = ["alpha_tool"],
            DisabledTasks = new Dictionary<string, List<string>>
            {
                ["alpha_tool"] = ["task_one"],
            },
        });

        Assert.Multiple(
            () => Assert.False(changed),
            () => Assert.Equal(0, changeCount));
    }

    [Fact]
    public void Should_raise_changed_event_when_disabled_tools_differ()
    {
        var snapshot = new AutoContextConfigSnapshot();
        snapshot.Update(new AutoContextConfigSnapshotDto { DisabledTools = ["alpha_tool"] });

        var changeCount = 0;
        snapshot.Changed += (_, _) => changeCount++;

        var changed = snapshot.Update(new AutoContextConfigSnapshotDto { DisabledTools = ["alpha_tool", "beta_tool"] });

        Assert.Multiple(
            () => Assert.True(changed),
            () => Assert.Equal(1, changeCount),
            () => Assert.True(snapshot.IsToolDisabled("beta_tool")));
    }

    [Fact]
    public void Should_treat_tool_order_as_irrelevant()
    {
        var snapshot = new AutoContextConfigSnapshot();
        snapshot.Update(new AutoContextConfigSnapshotDto { DisabledTools = ["alpha_tool", "beta_tool"] });

        var changeCount = 0;
        snapshot.Changed += (_, _) => changeCount++;

        var changed = snapshot.Update(new AutoContextConfigSnapshotDto { DisabledTools = ["beta_tool", "alpha_tool"] });

        Assert.Multiple(
            () => Assert.False(changed),
            () => Assert.Equal(0, changeCount));
    }

    [Fact]
    public void Should_clear_state_when_update_is_empty()
    {
        var snapshot = new AutoContextConfigSnapshot();
        snapshot.Update(new AutoContextConfigSnapshotDto
        {
            DisabledTools = ["alpha_tool"],
            DisabledTasks = new Dictionary<string, List<string>>
            {
                ["alpha_tool"] = ["task_one"],
            },
        });

        var changed = snapshot.Update(new AutoContextConfigSnapshotDto());

        Assert.Multiple(
            () => Assert.True(changed),
            () => Assert.Empty(snapshot.DisabledTools),
            () => Assert.Empty(snapshot.DisabledTasks));
    }
}
