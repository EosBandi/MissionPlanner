using MPBackend.UI.Displays;

namespace MPBackend.UI.Shell;

/// <summary>Layout variants from the design spec (layouts.breakpoints / layouts.variants).</summary>
public enum LayoutMode
{
    /// <summary>&lt; 600 px: composite, one display at a time plus a single soft-key bar.</summary>
    Phone,
    /// <summary>600–1199 px, portrait: PFD over MFD, one soft-key bar following the last-touched display.</summary>
    TabletPortrait,
    /// <summary>600–1199 px, landscape: PFD | MFD, two soft-key bars.</summary>
    TabletLandscape,
    /// <summary>&gt;= 1200 px: PFD | MFD with the MFD getting the larger share.</summary>
    Desktop
}

public enum DisplayKind
{
    Pfd,
    Mfd
}

/// <summary>
/// Shell navigation state: layout mode, which display is "active" (owns the single soft-key bar on phone /
/// portrait), the MFD page group + page, and the soft keys each display currently offers.
/// </summary>
public sealed class ShellState
{
    public const string HomeGroup = "MAP";

    public event Action? Changed;

    public LayoutMode Layout { get; private set; } = LayoutMode.Desktop;
    public DisplayKind ActiveDisplay { get; private set; } = DisplayKind.Mfd;
    public string GroupId { get; private set; } = HomeGroup;
    public int PageIndex { get; private set; }
    public IReadOnlyList<SoftKey> PfdKeys { get; private set; } = SoftKey.Bar();
    public IReadOnlyList<SoftKey> MfdKeys { get; private set; } = SoftKey.Bar();

    /// <summary>
    /// Keys the shell does not use itself (Up/Down/Enter/PageUp/PageDown) go to the current MFD page;
    /// the handler returns true when it consumed the key.
    /// </summary>
    public Func<string, bool>? PageKeys { get; set; }

    public PageGroup Group => PageRegistry.Get(GroupId);
    public PageDef Page => Group.Pages[Math.Clamp(PageIndex, 0, Group.Pages.Count - 1)];

    public bool TwoDisplays => Layout != LayoutMode.Phone;
    public bool TwoKeyBars => Layout is LayoutMode.Desktop or LayoutMode.TabletLandscape;

    public void SetViewport(double width, double height)
    {
        var mode = width < 600 ? LayoutMode.Phone
            : width < 1200 ? (height > width ? LayoutMode.TabletPortrait : LayoutMode.TabletLandscape)
            : LayoutMode.Desktop;
        if (mode == Layout)
            return;
        Layout = mode;
        Notify();
    }

    /// <summary>The display the operator last interacted with (phone / portrait soft-key bar follows it).</summary>
    public void Touch(DisplayKind display)
    {
        if (display == ActiveDisplay)
            return;
        ActiveDisplay = display;
        Notify();
    }

    public void ToggleDisplay() => Touch(ActiveDisplay == DisplayKind.Pfd ? DisplayKind.Mfd : DisplayKind.Pfd);

    public void GoTo(string groupId, int pageIndex = 0)
    {
        var group = PageRegistry.Get(groupId);
        GroupId = group.Id;
        PageIndex = Math.Clamp(pageIndex, 0, group.Pages.Count - 1);
        Notify();
    }

    public void GoToPage(string pageId)
    {
        var (group, index) = PageRegistry.Find(pageId);
        GoTo(group, index);
    }

    /// <summary>MFD large knob: next / previous page group (page resets to the group's first page).</summary>
    public void NavigateGroup(int delta)
    {
        var groups = PageRegistry.Groups;
        var i = groups.ToList().FindIndex(g => g.Id == GroupId);
        GoTo(groups[(i + delta + groups.Count) % groups.Count].Id);
    }

    /// <summary>MFD small knob: next / previous page within the group.</summary>
    public void NavigatePage(int delta)
    {
        var n = Group.Pages.Count;
        GoTo(GroupId, (PageIndex + delta + n) % n);
    }

    /// <summary>Hold ESC: back to Nav Map 1.</summary>
    public void Home() => GoTo(HomeGroup);

    public void SetPfdKeys(IReadOnlyList<SoftKey> keys)
    {
        PfdKeys = keys;
        Notify();
    }

    public void SetMfdKeys(IReadOnlyList<SoftKey> keys)
    {
        MfdKeys = keys;
        Notify();
    }

    private void Notify() => Changed?.Invoke();
}
