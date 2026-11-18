using OpenTK.Mathematics;

namespace Egc;

/// <summary>
/// NOU: o pistă de cadre-cheie (secțiunea 4): o listă de valori-cheie, la intervale EGALE de timp
/// (SecondsPerKey). Între două chei valoarea se interpolează. După ultima cheie pista revine
/// la prima și o ia de la capăt (bucla se închide singură).
/// </summary>
public sealed class KeyframeTrack
{
    private readonly List<Vector3> _keys = new();

    /// <summary>Timpul dintre două chei consecutive, în secunde.</summary>
    public float SecondsPerKey { get; }

    /// <summary>Easing-ul aplicat între două chei (implicit liniar).</summary>
    public EasingFunction Ease = Easing.Linear;

    public KeyframeTrack(float secondsPerKey)
    {
        SecondsPerKey = MathF.Max(secondsPerKey, 0.001f);
    }

    public int Count => _keys.Count;
    public IReadOnlyList<Vector3> Keys => _keys;

    /// <summary>Durata unui tur complet (inclusiv drumul de la ultima cheie înapoi la prima).</summary>
    public float Duration => _keys.Count * SecondsPerKey;

    /// <summary>Adaugă o cheie la sfârșitul pistei.</summary>
    public KeyframeTrack Add(Vector3 value)
    {
        _keys.Add(value);
        return this;   // permite scrierea în lanț: track.Add(...).Add(...)
    }

    public void Clear() => _keys.Clear();

    /// <summary>Valoarea pistei la momentul <paramref name="time"/> (în secunde).</summary>
    /// <summary>Valoarea pistei la momentul <paramref name="time"/> (în secunde).</summary>
    public Vector3 Sample(float time)
    {
        if (_keys.Count == 0) return Vector3.Zero;
        if (_keys.Count == 1) return _keys[0];

        // 1. Aducem timpul în intervalul [0, Duration) pentru buclare continuă
        float t = time % Duration;
        if (t < 0f) t += Duration;

        // 2. Găsim segmentul [i0, i1] și fracțiunea locală în [0, 1)
        // (i1 folosește % _keys.Count ca ultima cheie să revină lin la prima)
        float pos = t / SecondsPerKey;
        int i0 = (int)pos % _keys.Count;
        int i1 = (i0 + 1) % _keys.Count;
        float localT = pos - MathF.Floor(pos);

        // 3. Aplicăm funcția de easing pe fracțiunea locală și interpolăm liniar
        return Vector3.Lerp(_keys[i0], _keys[i1], Ease(localT));
    }
}
