using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace MPBackend.UI.Pfd;

/// <summary>
/// Base for the PFD instruments: receives the frame (<see cref="S"/>) and profile (<see cref="P"/>) and only
/// re-renders when the values it actually draws changed (<see cref="Key"/>), so a 20 Hz feed does not redraw
/// static parts.
/// </summary>
public abstract class PfdPart : ComponentBase
{
    [Parameter] public PfdState S { get; set; } = PfdState.Empty;
    [Parameter] public VehicleProfile P { get; set; } = VehicleProfile.Default("multirotor");
    [Parameter] public bool Extreme { get; set; }

    private object? last;

    /// <summary>A value (tuple) that captures everything this part renders.</summary>
    protected abstract object Key();

    protected override bool ShouldRender()
    {
        var key = Key();
        if (Equals(key, last))
            return false;
        last = key;
        return true;
    }

    protected static double R1(double v) => Math.Round(v, 1);
    protected static double R2(double v) => Math.Round(v, 2);

    /// <summary>Control authority from the FMA table: AP = autopilot drives the targets (bugs solid, FD shown).</summary>
    protected bool Autopilot => FmaTable.Resolve(P.FixedWing, S.Mode).Authority == "AP";
}

/// <summary>
/// SVG text element. Razor reserves the <c>&lt;text&gt;</c> tag, so instruments emit text through this component:
/// <c>&lt;T X="10" Y="53" Size="16" Fill="var(--p-magenta)" W="700" V="MSN" /&gt;</c>.
/// </summary>
public sealed class T : ComponentBase
{
    [Parameter] public double X { get; set; }
    [Parameter] public double Y { get; set; }
    [Parameter] public string? V { get; set; }
    [Parameter] public string Fill { get; set; } = "#fff";
    [Parameter] public double Size { get; set; } = 16;
    [Parameter] public int W { get; set; } = 600;
    /// <summary>text-anchor: start | middle | end</summary>
    [Parameter] public string A { get; set; } = "start";
    [Parameter] public string? Class { get; set; }
    [Parameter] public string? Transform { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        b.OpenElement(0, "text");
        b.AddAttribute(1, "x", Svg.F(X));
        b.AddAttribute(2, "y", Svg.F(Y));
        b.AddAttribute(3, "fill", Fill);
        b.AddAttribute(4, "font-size", Svg.F(Size));
        b.AddAttribute(5, "font-weight", W);
        b.AddAttribute(6, "text-anchor", A);
        if (Class != null) b.AddAttribute(7, "class", Class);
        if (Transform != null) b.AddAttribute(8, "transform", Transform);
        if (V != null) b.AddContent(9, V);
        if (ChildContent != null) b.AddContent(10, ChildContent);
        b.CloseElement();
    }
}
