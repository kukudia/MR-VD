using UnityEngine;

/// <summary>
/// Samples a bounded ring buffer of normalized spectrum frames and turns their history into a
/// receding wire landscape. RenderFrame never allocates after the configured grid is built.
/// </summary>
public sealed class AudioSpectrumTerrain : AudioVisualMesh
{
    [SerializeField, Range(16, 96)] private int bands = 48;
    [SerializeField, Range(16, 96)] private int historyRows = 48;
    [SerializeField, Range(5f, 40f)] private float sampleRate = 20f;
    [SerializeField, Min(1f)] private float width = 10f;
    [SerializeField, Min(1f)] private float depth = 7f;
    [SerializeField, Min(0f)] private float height = 0.75f;
    [SerializeField] private Vector3 origin = new Vector3(0f, -1.1f, 0.7f);
    private float[] history;
    private int builtBands;
    private int builtRows;
    private int newestRow;
    private float sampleAccumulator;
    public int SampleCount { get; private set; }

    public void Clear()
    {
        if (history != null)
        {
            System.Array.Clear(history, 0, history.Length);
        }
        sampleAccumulator = 0f;
        newestRow = 0;
    }

    public void RenderFrame(AudioVisualField.Frame frame, AudioVisualTheme theme, float[] spectrum)
    {
        int columns = Mathf.Clamp(bands, 16, 96);
        int rows = Mathf.Clamp(historyRows, 16, 96);
        if (mesh == null || builtBands != columns || builtRows != rows)
        {
            BuildGrid(columns, rows);
        }

        sampleAccumulator += frame.deltaTime;
        float interval = 1f / Mathf.Clamp(sampleRate, 5f, 40f);
        if (sampleAccumulator >= interval)
        {
            // A stalled frame should not fill the history with copies of the same FFT.
            sampleAccumulator %= interval;
            newestRow = (newestRow + 1) % rows;
            for (int x = 0; x < columns; x++)
            {
                float sourcePosition = x / (float)(columns - 1) * (spectrum.Length - 1);
                int lower = Mathf.FloorToInt(sourcePosition);
                int upper = Mathf.Min(lower + 1, spectrum.Length - 1);
                history[newestRow * columns + x] = Mathf.Lerp(spectrum[lower], spectrum[upper], sourcePosition - lower);
            }
            SampleCount++;
        }

        float scroll = Mathf.Clamp01(sampleAccumulator / interval);
        for (int row = 0; row < rows; row++)
        {
            float age = row / (float)(rows - 1);
            for (int x = 0; x < columns; x++)
            {
                float across = x / (float)(columns - 1);
                float value = InterpolatedValue(row, x, columns, rows, scroll, spectrum);
                int index = row * columns + x;
                vertices[index] = origin + new Vector3((across - 0.5f) * width, value * height, age * depth);
                coordinates[index] = new Vector2(x, row);
                float sideFade = Mathf.Sin(across * Mathf.PI);
                float alpha = Mathf.Pow(1f - age, 1.3f) * sideFade * frame.terrainWeight
                    * (0.08f + value * 0.8f);
                Color color = Palette(theme, across * 0.6f + age * 0.4f, alpha);
                float left = InterpolatedValue(row, Mathf.Max(0, x - 1), columns, rows, scroll, spectrum);
                float right = InterpolatedValue(row, Mathf.Min(columns - 1, x + 1), columns, rows, scroll, spectrum);
                float ridge = Mathf.Clamp01((value - (left + right) * 0.5f) * 5f);
                Color crest = Color.Lerp(color, theme.accent, ridge * theme.ridgeGlow);
                crest.a = alpha;
                colors[index] = crest;
            }
        }
        Upload(frame, theme, 2);
    }

    private float InterpolatedValue(int row, int column, int columns, int rows, float scroll, float[] spectrum)
    {
        int historyIndex = (newestRow - row + rows) % rows;
        float value = history[historyIndex * columns + column];
        // Advect between sample ticks. Buffer shifts preserve the previous tick's end state.
        float newer;
        if (row == 0)
        {
            float sourcePosition = column / (float)(columns - 1) * (spectrum.Length - 1);
            int lower = Mathf.FloorToInt(sourcePosition);
            newer = Mathf.Lerp(spectrum[lower], spectrum[Mathf.Min(lower + 1, spectrum.Length - 1)], sourcePosition - lower);
        }
        else
        {
            newer = history[((historyIndex + 1) % rows) * columns + column];
        }
        return Mathf.Lerp(value, newer, scroll);
    }

    private void BuildGrid(int columns, int rows)
    {
        int[] indices = new int[(columns - 1) * (rows - 1) * 6];
        int cursor = 0;
        for (int row = 0; row < rows - 1; row++)
        {
            for (int column = 0; column < columns - 1; column++)
            {
                int index = row * columns + column;
                indices[cursor++] = index;
                indices[cursor++] = index + columns;
                indices[cursor++] = index + 1;
                indices[cursor++] = index + 1;
                indices[cursor++] = index + columns;
                indices[cursor++] = index + columns + 1;
            }
        }
        Initialize(columns * rows, indices, MeshTopology.Triangles);
        history = new float[columns * rows];
        builtBands = columns;
        builtRows = rows;
        Clear();
    }

    private void OnDisable()
    {
        Clear();
    }
}
