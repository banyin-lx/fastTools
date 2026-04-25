namespace FastTools.App.Models;

public sealed class CustomCommandDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    public string Alias { get; set; } = string.Empty;

    public string Command { get; set; } = string.Empty;

    public string Arguments { get; set; } = string.Empty;

    public bool RequiresConfirmation { get; set; }

    public string ConfirmationMessage { get; set; } = "确定执行这个自定义命令？";
}
