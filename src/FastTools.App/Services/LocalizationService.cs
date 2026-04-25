using FastTools.App.Infrastructure;
using FastTools.App.Models;

namespace FastTools.App.Services;

public sealed class LocalizationService : ObservableObject
{
    private static readonly IReadOnlyDictionary<string, string> ZhCn = new Dictionary<string, string>
    {
        ["Main.SearchPlaceholder"] = "搜索应用、文件、命令、网页...",
        ["Main.NoResults"] = "没有匹配结果",
        ["Main.HotKeyError.Title"] = "FastTools",
        ["Main.HotKeyError.Body"] = "程序将继续打开，你可以直接进入设置页修改快捷键。",
        ["Main.ActionConfirm"] = "确定执行该操作？",
        ["Tray.Open"] = "打开",
        ["Tray.Settings"] = "设置",
        ["Tray.Refresh"] = "刷新索引",
        ["Tray.Exit"] = "退出",
        ["Tray.MenuSubtitle"] = "快捷操作",
        ["Tray.HotKeyError"] = "全局快捷键注册失败，程序已正常打开。请从托盘菜单进入设置修改快捷键。",
        ["Settings.Title"] = "FastTools 设置",
        ["Settings.Header.Title"] = "偏好设置",
        ["Settings.Header.Subtitle"] = "主题、语言、索引、排序优先级与插件都在这里配置。",
        ["Settings.General.Title"] = "常规",
        ["Settings.General.Description"] = "基础行为、语言与视觉风格",
        ["Settings.HotKey"] = "全局快捷键",
        ["Settings.Theme"] = "主题",
        ["Settings.Language"] = "语言",
        ["Settings.DefaultSearchEngine"] = "默认搜索引擎",
        ["Settings.Indexing.Title"] = "索引",
        ["Settings.Indexing.Description"] = "一行一个目录",
        ["Settings.ApplicationDirs"] = "应用目录",
        ["Settings.IndexedDirs"] = "文件索引目录",
        ["Settings.CustomCommands.Title"] = "自定义命令",
        ["Settings.CustomCommands.Description"] = "让本地脚本和终端工具进入搜索结果",
        ["Settings.Add"] = "新增",
        ["Settings.Remove"] = "删除",
        ["Settings.Priority.Title"] = "结果优先级",
        ["Settings.Priority.Description"] = "数字越小优先级越高。默认 Applications 最前。",
        ["Settings.Priority.Help"] = "Applications=应用, Commands=命令, Files=文件, Web=网页",
        ["Settings.Plugins.Title"] = "插件",
        ["Settings.Plugins.Description"] = "本地插件启停与基础信息",
        ["Settings.Cancel"] = "取消",
        ["Settings.Save"] = "保存",
        ["Settings.Name"] = "名称",
        ["Settings.Alias"] = "别名",
        ["Settings.Command"] = "命令",
        ["Settings.Arguments"] = "参数",
        ["Settings.Confirm"] = "确认",
        ["Settings.Group"] = "分组",
        ["Settings.Priority"] = "优先级",
        ["Settings.InvalidHotKey"] = "快捷键格式不正确，请使用如 Alt+Space 的格式。",
        ["Theme.Dark"] = "深色",
        ["Theme.Light"] = "浅色",
        ["Language.ZhCn"] = "简体中文",
        ["Language.EnUs"] = "English",
        ["Group.Applications"] = "应用",
        ["Group.Commands"] = "命令",
        ["Group.Files"] = "文件",
        ["Group.Web"] = "网页",
    };

    private static readonly IReadOnlyDictionary<string, string> EnUs = new Dictionary<string, string>
    {
        ["Main.SearchPlaceholder"] = "Search apps, files, commands, web...",
        ["Main.NoResults"] = "No results",
        ["Main.HotKeyError.Title"] = "FastTools",
        ["Main.HotKeyError.Body"] = "The app will stay open so you can update the hotkey in Settings.",
        ["Main.ActionConfirm"] = "Run this action?",
        ["Tray.Open"] = "Open",
        ["Tray.Settings"] = "Settings",
        ["Tray.Refresh"] = "Refresh Index",
        ["Tray.Exit"] = "Exit",
        ["Tray.MenuSubtitle"] = "Quick actions",
        ["Tray.HotKeyError"] = "Global hotkey registration failed. Open Settings from the tray menu to update it.",
        ["Settings.Title"] = "FastTools Settings",
        ["Settings.Header.Title"] = "Preferences",
        ["Settings.Header.Subtitle"] = "Theme, language, indexing, priority, and plugins are configured here.",
        ["Settings.General.Title"] = "General",
        ["Settings.General.Description"] = "Core behavior, language, and visual style",
        ["Settings.HotKey"] = "Global Hotkey",
        ["Settings.Theme"] = "Theme",
        ["Settings.Language"] = "Language",
        ["Settings.DefaultSearchEngine"] = "Default Search Engine",
        ["Settings.Indexing.Title"] = "Indexing",
        ["Settings.Indexing.Description"] = "One directory per line",
        ["Settings.ApplicationDirs"] = "Application Directories",
        ["Settings.IndexedDirs"] = "Indexed Directories",
        ["Settings.CustomCommands.Title"] = "Custom Commands",
        ["Settings.CustomCommands.Description"] = "Expose local scripts and terminal tools in search",
        ["Settings.Add"] = "Add",
        ["Settings.Remove"] = "Remove",
        ["Settings.Priority.Title"] = "Result Priority",
        ["Settings.Priority.Description"] = "Lower numbers rank higher. Applications is first by default.",
        ["Settings.Priority.Help"] = "Applications=apps, Commands=commands, Files=files, Web=web",
        ["Settings.Plugins.Title"] = "Plugins",
        ["Settings.Plugins.Description"] = "Local plugin status and metadata",
        ["Settings.Cancel"] = "Cancel",
        ["Settings.Save"] = "Save",
        ["Settings.Name"] = "Name",
        ["Settings.Alias"] = "Alias",
        ["Settings.Command"] = "Command",
        ["Settings.Arguments"] = "Arguments",
        ["Settings.Confirm"] = "Confirm",
        ["Settings.Group"] = "Group",
        ["Settings.Priority"] = "Priority",
        ["Settings.InvalidHotKey"] = "Invalid hotkey format. Use a value like Alt+Space.",
        ["Theme.Dark"] = "Dark",
        ["Theme.Light"] = "Light",
        ["Language.ZhCn"] = "Simplified Chinese",
        ["Language.EnUs"] = "English",
        ["Group.Applications"] = "Applications",
        ["Group.Commands"] = "Commands",
        ["Group.Files"] = "Files",
        ["Group.Web"] = "Web",
    };

    private AppLanguage _currentLanguage = AppLanguage.ZhCn;

    public AppLanguage CurrentLanguage
    {
        get => _currentLanguage;
        private set => SetProperty(ref _currentLanguage, value);
    }

    public string this[string key] => Get(key);

    public void Apply(AppLanguage language)
    {
        CurrentLanguage = language;
        OnPropertyChanged("Item[]");
    }

    public string Get(string key)
    {
        var dictionary = CurrentLanguage == AppLanguage.EnUs ? EnUs : ZhCn;
        return dictionary.TryGetValue(key, out var value) ? value : key;
    }
}
