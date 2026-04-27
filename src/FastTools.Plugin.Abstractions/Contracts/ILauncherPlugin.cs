namespace FastTools.Plugin.Abstractions.Contracts;

public interface ILauncherPlugin
{
    string Id { get; }

    string DisplayName { get; }

    string Description { get; }

    PluginConfiguration GetConfiguration();

    Task<IReadOnlyList<PluginSearchItem>> QueryAsync(PluginQuery query, CancellationToken cancellationToken);

    Task ExecuteAsync(PluginExecutionRequest request, CancellationToken cancellationToken);
}
