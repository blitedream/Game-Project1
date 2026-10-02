using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;

public static class ZacatonPointCloud
{
    public static Vector3[] CreateVertices(TextAsset source, float horizontalScale, float verticalScale)
    {
        if (source == null || string.IsNullOrEmpty(source.text))
            return null;

        List<float> numbers = ReadNumbers(source.text);
        int pointCount = numbers.Count / 3;

        if (pointCount <= 0)
            return null;

        Vector3[] vertices = new Vector3[pointCount];

        for (int i = 0; i < pointCount; i++)
        {
            float x = numbers[i * 3];
            float y = numbers[i * 3 + 1];
            float z = numbers[i * 3 + 2];

            vertices[i] = new Vector3(
                x * horizontalScale,
                -z * verticalScale,
                y * horizontalScale
            );
        }

        return vertices;
    }

    public static Mesh CreateMesh(TextAsset source, float horizontalScale, float verticalScale)
    {
        Vector3[] vertices = CreateVertices(source, horizontalScale, verticalScale);
        if (vertices == null || vertices.Length == 0)
            return null;

        int[] indices = new int[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
            indices[i] = i;

        Mesh mesh = new Mesh
        {
            name = "Zacaton Point Cloud",
            indexFormat = vertices.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16
        };

        mesh.SetVertices(vertices);
        mesh.SetIndices(indices, MeshTopology.Points, 0);
        mesh.RecalculateBounds();

        return mesh;
    }

    private static List<float> ReadNumbers(string text)
    {
        List<float> values = new List<float>(75000 * 3);
        bool inPointList = false;
        int i = 0;

        while (i < text.Length)
        {
            if (!inPointList)
            {
                int pointStart = text.IndexOf("point [", i, System.StringComparison.Ordinal);
                if (pointStart < 0)
                    break;

                i = pointStart + 7;
                inPointList = true;
                continue;
            }

            char c = text[i];

            if (c == ']')
                break;

            if (IsNumberStart(c))
            {
                int start = i;
                i++;

                while (i < text.Length && IsNumberChar(text[i]))
                    i++;

                string token = text.Substring(start, i - start);
                if (float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                    values.Add(value);

                continue;
            }

            i++;
        }

        return values;
    }

    private static bool IsNumberStart(char c)
    {
        return c == '-' || c == '+' || c == '.' || char.IsDigit(c);
    }

    private static bool IsNumberChar(char c)
    {
        return c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E' || char.IsDigit(c);
    }
}
