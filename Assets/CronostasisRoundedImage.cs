using System.Collections.Generic;
using UnityEngine;

public sealed class CronostasisRoundedImage : UnityEngine.UI.Image
{
    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        Rect rect = GetPixelAdjustedRect();
        float radius = Mathf.Min(rect.height * 0.5f, rect.width * 0.5f);
        if (radius <= 0f)
            return;

        const int segmentsPerCorner = 12;
        List<Vector2> perimeter = new List<Vector2>(segmentsPerCorner * 4 + 4);
        AddArc(perimeter, new Vector2(rect.xMax - radius, rect.yMin + radius), radius, -90f, 0f, segmentsPerCorner);
        AddArc(perimeter, new Vector2(rect.xMax - radius, rect.yMax - radius), radius, 0f, 90f, segmentsPerCorner);
        AddArc(perimeter, new Vector2(rect.xMin + radius, rect.yMax - radius), radius, 90f, 180f, segmentsPerCorner);
        AddArc(perimeter, new Vector2(rect.xMin + radius, rect.yMin + radius), radius, 180f, 270f, segmentsPerCorner);

        Color32 vertexColor = color;
        vertexHelper.AddVert(new Vector3(rect.center.x, rect.center.y), vertexColor, new Vector2(0.5f, 0.5f));
        for (int i = 0; i < perimeter.Count; i++)
        {
            Vector2 point = perimeter[i];
            vertexHelper.AddVert(new Vector3(point.x, point.y), vertexColor, new Vector2(0.5f, 0.5f));
        }

        for (int i = 1; i <= perimeter.Count; i++)
        {
            int next = i == perimeter.Count ? 1 : i + 1;
            vertexHelper.AddTriangle(0, i, next);
        }
    }

    private static void AddArc(List<Vector2> points, Vector2 center, float radius, float startAngle, float endAngle, int segments)
    {
        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.Lerp(startAngle, endAngle, i / (float)segments) * Mathf.Deg2Rad;
            points.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }
    }
}