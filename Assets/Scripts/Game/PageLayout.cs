using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One row of panels on the lane, read left to right.</summary>
[Serializable]
public class TierLayout
{
    public float minX, maxX, y0;
    public List<PanelLayout> panels = new();
    public float CentreY => y0 + PageDef.PanelHeight / 2f;
}

/// <summary>
/// The root of a drawn page (Art/Pages/PageNN, linked from its <see cref="PageDef"/>): its tiers of
/// panels and where Max starts. Every actor lives on the z = 0 lane; backdrops sit behind it for a
/// little parallax.
/// </summary>
public class PageLayout : MonoBehaviour
{
    public List<TierLayout> tiers = new();
    public Vector2 start;                     // Max's start, on the lane

    public Transform root => this != null ? transform : null;
}
