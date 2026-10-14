using MPBackend.UI.Pages;

namespace MPBackend.UI.Displays;

/// <summary>An MFD page: component type plus the title shown in the MFD bottom bar.</summary>
public sealed record PageDef(string Id, string Title, Type Component);

/// <summary>An MFD page group (large-knob position): MAP, MSN, VEH, AUX.</summary>
public sealed record PageGroup(string Id, IReadOnlyList<PageDef> Pages);

/// <summary>
/// MFD page groups from the design spec (behaviours.pagesAndMenus.pageGroups). Pages without an implementation
/// yet render <see cref="PlaceholderPage"/> so the navigation structure is complete from the start.
/// </summary>
public static class PageRegistry
{
    private static PageDef P(string id, string title, Type? component = null) =>
        new(id, title, component ?? typeof(PlaceholderPage));

    public static readonly IReadOnlyList<PageGroup> Groups = new[]
    {
        new PageGroup("MAP", new[]
        {
            P("map1", "NAV MAP 1", typeof(NavMapPage)),
            P("map2", "NAV MAP 2"),
            P("split", "SPLIT"),
            P("vprofile", "VERTICAL PROFILE"),
            P("traffic", "TRAFFIC"),
            P("terrain", "TERRAIN"),
            P("airspace", "AIRSPACE")
        }),
        new PageGroup("MSN", new[]
        {
            P("mission", "ACTIVE MISSION", typeof(MissionPage)),
            P("checklists", "CHECKLISTS"),
            P("wpinfo", "WAYPOINT INFO"),
            P("fence", "FENCE & RALLY"),
            P("survey", "SURVEY STATUS"),
            P("planner", "MISSION PLANNER")
        }),
        new PageGroup("VEH", new[]
        {
            P("power", "POWER / ENGINE"),
            P("powersyn", "POWER SYNOPTIC"),
            P("fuel", "FUEL SYNOPTIC"),
            P("relays", "RELAYS"),
            P("link", "LINK", typeof(LinkPage)),
            P("payload", "PAYLOAD"),
            P("exceed", "EXCEEDANCES"),
            P("vstatus", "STATUS")
        }),
        new PageGroup("AUX", new[]
        {
            P("setup", "SETUP"),
            P("sysstatus", "SYSTEM STATUS", typeof(SystemStatusPage)),
            P("video", "VIDEO"),
            P("params", "PARAMETERS", typeof(ParametersPage)),
            P("logs", "LOGS")
        })
    };

    public static PageGroup Get(string groupId) =>
        Groups.FirstOrDefault(g => g.Id == groupId) ?? Groups[0];

    public static (string Group, int Index) Find(string pageId)
    {
        foreach (var group in Groups)
        {
            var i = group.Pages.ToList().FindIndex(p => p.Id == pageId);
            if (i >= 0)
                return (group.Id, i);
        }

        return (Groups[0].Id, 0);
    }
}
