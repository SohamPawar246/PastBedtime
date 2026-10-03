/// <summary>
/// Dev tool: renders the current play-mode frame, overlay canvases included, into
/// a PNG at a fixed resolution, independent of the Game view's size or focus.
/// The work is done by <see cref="DevCapture"/>, which play-mode test code can call too.
/// </summary>
public static class ShellCapture
{
    public static string Capture(string path, int width = 1920, int height = 1080) => DevCapture.Capture(path, width, height);
}
