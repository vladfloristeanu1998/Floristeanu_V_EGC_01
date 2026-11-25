using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Egc;

/// <summary>
/// Tema acasă – Varianta A: Ceasul analogic procedural.
/// Tot desenul (cadran, ramă, 60 gradații, ace, temă zi/noapte) este calculat în Shaders/clock.frag.
/// </summary>
public sealed class ClockScene : Scene
{
    private const string ToggleTheme = "ceas: temă zi / noapte";
    private const string AdvanceHour = "ceas: +1 oră";

    private Material _clock = null!;

    // Pornim de la 10:09:30 (poziție clasică de prezentare, cu toate cele 3 ace vizibile separat)
    private float _clockSeconds = 10 * 3600f + 9 * 60f + 30f;

    // Tranziție lină între tema de zi (0.0) și tema de noapte (1.0)
    private bool _isNight;
    private Tween _nightTween = null!;

    public override string Name => "tema: ceas";

    public override IEnumerable<string> ActionNames => new[] { ToggleTheme, AdvanceHour };

    public override string Status
    {
        get
        {
            int total = (int)MathF.Floor(_clockSeconds) % 43200;
            if (total < 0) total += 43200;
            int h = total / 3600;
            if (h == 0) h = 12;
            int m = (total % 3600) / 60;
            int s = total % 60;
            return $"{h:D2}:{m:D2}:{s:D2} | {(_isNight ? "noapte" : "zi")}";
        }
    }

    protected override void Load(Input input)
    {
        CameraPosition = new Vector3(0.0f, 2.0f, 5.2f);
        CameraTarget = new Vector3(0.0f, 2.0f, 0.0f);

        input.Bind(ToggleTheme, Keys.Space);
        input.Bind(AdvanceHour, Keys.T);

        Mesh grid = AddMesh(Shapes.Grid(10, 1.0f, new Color4(0.30f, 0.33f, 0.38f, 1.0f)));
        Mesh quad = AddMesh(Shapes.Quad(3.2f, 3.2f));

        AddObject("podea", grid);

        // Materialul ceasului: transparent în afara discului exterior
        _clock = AddMaterial("ceas", "procedural.vert", "clock.frag")
            .Param("uBezelWidth", 0.02f, 0.12f, 0.065f)
            .Param("uTickScale", 0.5f, 1.6f, 1.0f)
            .Param("uSubticks", 0, 1, 1);

        _clock.Transparent = true;

        _clock.Uniforms.Set("uHourAngle", 0.0f);
        _clock.Uniforms.Set("uMinuteAngle", 0.0f);
        _clock.Uniforms.Set("uSecondAngle", 0.0f);
        _clock.Uniforms.Set("uNightMix", 0.0f);
        _clock.Uniforms.Set("uSecondColor", new Vector3(0.85f, 0.16f, 0.16f));

        _nightTween = Tween.Float(0.0f, 0.0f, 0.7f, Easing.EaseInOut);

        SceneObject face = AddObject("cadran", quad, _clock);
        face.Transform.Position = new Vector3(0.0f, 2.0f, 0.0f);
    }

    public override void Update(float dt, Input input)
    {
        base.Update(dt, input);

        if (input.WasPressed(AdvanceHour))
        {
            _clockSeconds += 3600f;
        }

        if (input.WasPressed(ToggleTheme))
        {
            _isNight = !_isNight;
            _nightTween.GoTo(_isNight ? 1.0f : 0.0f, 0.7f);
            ConsolePanel.PrintMessage($"ceas: temă {(_isNight ? "de noapte" : "de zi")}");
        }

        _clockSeconds += dt;
        _nightTween.Update(dt);

        // Pasul 2 va calcula aici unghiurile acelor (orar, minutar, secundar EaseOut)
        _clock.Uniforms.Set("uNightMix", _nightTween.Scalar);
        _clockSeconds += dt;
        _nightTween.Update(dt);

        // 1. Orarul: se misca continuu (12 ore = 43200 secunde -> 2 * PI radiani)
        float hours = (_clockSeconds / 3600.0f) % 12.0f;
        float hourAngle = hours * (MathF.PI * 2.0f / 12.0f);

        // 2. Minutarul: se misca continuu (60 minute = 3600 secunde -> 2 * PI radiani)
        float minutes = (_clockSeconds / 60.0f) % 60.0f;
        float minuteAngle = minutes * (MathF.PI * 2.0f / 60.0f);

        // 3. Secundarul: "sare" o data pe secunda cu o miscare scurta EaseOut (0.18 s)
        float wholeSeconds = MathF.Floor(_clockSeconds);
        float frac = _clockSeconds - wholeSeconds;
        float tickProgress = MathF.Min(frac / 0.18f, 1.0f);
        float smoothSeconds = (wholeSeconds + Easing.EaseOut(tickProgress)) % 60.0f;
        float secondAngle = smoothSeconds * (MathF.PI * 2.0f / 60.0f);

        _clock.Uniforms.Set("uHourAngle", hourAngle);
        _clock.Uniforms.Set("uMinuteAngle", minuteAngle);
        _clock.Uniforms.Set("uSecondAngle", secondAngle);
        _clock.Uniforms.Set("uNightMix", _nightTween.Scalar);
    }

    public override IEnumerable<string> Describe()
    {
        foreach (string line in base.Describe()) yield return line;
        yield return $"Ora afișată    : {Status}";
    }
}