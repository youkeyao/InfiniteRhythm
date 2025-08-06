using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

public struct ControlPoint
{
    public Vector3 position;
    public Vector3 direction;
}

public struct BezierCurve
{
    public Vector3 A;
    public Vector3 B;
    public Vector3 C;
    public Vector3 D;
    public float length;

    private static float[][] gaussWX =
    {
        new []{ 0.2955242247147529f, -0.1488743389816312f },
        new []{ 0.2955242247147529f, 0.1488743389816312f },
        new []{ 0.2692667193099963f, -0.4333953941292472f },
        new []{ 0.2692667193099963f, 0.4333953941292472f },
        new []{ 0.2190863625159820f, -0.6794095682990244f },
        new []{ 0.2190863625159820f, 0.6794095682990244f },
        new []{ 0.1494513491505806f, -0.8650633666889845f },
        new []{ 0.1494513491505806f, 0.8650633666889845f },
        new []{ 0.0666713443086881f, -0.9739065285171717f },
        new []{ 0.0666713443086881f, 0.9739065285171717f },
    };

    public BezierCurve(in Vector3 P0, in Vector3 P1, in Vector3 P2, in Vector3 P3)
    {
        A = -P0 + 3 * P1 - 3 * P2 + P3;
        B = 3 * P0 - 6 * P1 + 3 * P2;
        C = -3 * P0 + 3 * P1;
        D = P0;
        length = 0;
        length = CalcLength(1f);
    }

    public Vector3 CalcDerivative(float t)
    {
        return 3f * A * t * t + 2f * B * t + C;
    }

    public float CalcLength(float t)
    {
        var halfT = t / 2f;

        float sum = 0f;
        foreach (float[] wx in gaussWX)
        {
            var w = wx[0];
            var x = wx[1];
            sum += w * CalcDerivative(halfT * x + halfT).magnitude;
        }
        sum *= halfT;
        return sum;
    }

    public float T2S(float t)
    {
        return CalcLength(t) / length;
    }

    public float T2SDer(float t)
    {
        return CalcDerivative(t).magnitude / length;
    }

    public float S2T(float s)
    {
        const int NEWTON_SEGMENT = 4;
        const float NEWTON_EPSILON = 1e-4f;

        s = Mathf.Clamp01(s);
        float t = s;
        int i;
        for (i = 0; i < NEWTON_SEGMENT; i++)
        {
            float delta = (T2S(t) - s) / T2SDer(t);
            if (Mathf.Abs(delta) < NEWTON_EPSILON)
                break;
            t = t - delta;
        }
        return t;
    }

    public Matrix4x4 GetTransform(float s)
    {
        float t = S2T(s);
        Vector3 pos = A * t * t * t + B * t * t + C * t + D;
        Vector3 direction = CalcDerivative(t).normalized;
        return Matrix4x4.TRS(pos, Quaternion.LookRotation(direction), Vector3.one);
    }
};

public static class CurveGenerator
{
    const int ControlPointCapacity = 20;
    public const int ChildCol = 2;

    public static Vector2 curveSpacing = new Vector2(-5, 25);
    public static float baseSegmentLength = 100f;
    public static float slope = 5.0f;
    public static float rotateAngle = 20.0f;
    public static float rotateSpeed = 0.01f;

    static BezierCurve[,] s_curves = new BezierCurve[ChildCol * 2 + 1, ControlPointCapacity]; // curve i -> controlPoints[i] - > controlPoints[i + 1]
    static float[] s_lengths = new float[ChildCol * 2 + 1];
    static int s_controlPointsHead = 0;
    static int s_controlPointsSize = 0;
    static ControlPoint[] s_controlPoints = InitializeControlPoints();

    public static float GetLength(int col)
    {
        if (col < -ChildCol || col > ChildCol)
        {
            return 0;
        }
        return s_lengths[col + ChildCol];
    }

    public static void Clear()
    {
        s_controlPointsHead = 0;
        s_controlPointsSize = 2;
        for (int i = 0; i < ChildCol * 2 + 1; i++)
        {
            s_lengths[i] = 0;
        }
    }

    static ControlPoint[] InitializeControlPoints()
    {
        s_controlPoints = new ControlPoint[ControlPointCapacity];

        s_controlPoints[0] = new ControlPoint { position = Vector3.zero, direction = Vector3.forward };

        float L = baseSegmentLength / 3;
        Vector3 P0 = s_controlPoints[0].position;
        Vector3 P1 = P0 + s_controlPoints[0].direction * L;
        Vector3 P2 = P1 + Quaternion.Euler(0, UnityEngine.Random.Range(-15.0f, 15.0f), 0) * s_controlPoints[0].direction * L;
        Vector3 nextDirection = Quaternion.Euler(0, UnityEngine.Random.Range(-15.0f, 15.0f), 0) * (P2 - P1).normalized;
        Vector3 P3 = P2 + nextDirection * L;
        s_controlPoints[1] = new ControlPoint { position = P3, direction = nextDirection };
        s_controlPointsSize = 2;

        UpdateCurves(P0, P1, P2, P3);

        return s_controlPoints;
    }

    public static void GenerateNextControlPoint()
    {
        int prevprevIndex = (s_controlPointsHead + s_controlPointsSize - 2) % ControlPointCapacity;
        int prevIndex = (s_controlPointsHead + s_controlPointsSize - 1) % ControlPointCapacity;
        ControlPoint prevprevControlPoint = s_controlPoints[prevprevIndex];
        ControlPoint prevControlPoint = s_controlPoints[prevIndex];

        float L = baseSegmentLength / 3;
        // float avgE = 0;
        // foreach (float sample in samples)
        // {
        //     avgE += sample;
        // }
        // avgE /= samples.Length;
        Vector3 P0 = prevControlPoint.position;
        Vector3 P1 = P0 + prevControlPoint.direction * L;
        Vector3 P2 = prevprevControlPoint.position + prevprevControlPoint.direction * L + 4 * prevControlPoint.direction * L;
        Vector3 nextDirection = Quaternion.Euler(0, UnityEngine.Random.Range(-25.0f, 25.0f), 0) * (P2 - P1).normalized;
        Vector3 P3 = P2 + nextDirection * L;

        // remove head
        if (s_controlPointsSize == ControlPointCapacity)
        {
            s_controlPointsHead = (s_controlPointsHead + 1) % ControlPointCapacity;
            s_controlPointsSize--;
        }
        int nextIndex = (s_controlPointsHead + s_controlPointsSize) % ControlPointCapacity;
        s_controlPoints[nextIndex] = new ControlPoint { position = P3, direction = nextDirection };
        s_controlPointsSize++;

        // update curves
        UpdateCurves(P0, P1, P2, P3);
    }

    static void UpdateCurves(in Vector3 P0, in Vector3 P1, in Vector3 P2, in Vector3 P3)
    {
        int curveIndex = (s_controlPointsHead + s_controlPointsSize - 2) % ControlPointCapacity;
        BezierCurve curve = new BezierCurve(P0, P1, P2, P3);
        s_curves[ChildCol, curveIndex] = curve;
        s_lengths[ChildCol] += curve.length;
        Vector3 P0normal = Vector3.Cross(s_controlPoints[curveIndex].direction, Vector3.up).normalized;
        Vector3 P12normal = Vector3.Cross(P2 - P1, Vector3.up).normalized;
        Vector3 P3normal = Vector3.Cross(s_controlPoints[(curveIndex + 1) % ControlPointCapacity].direction, Vector3.up).normalized;
        for (int i = -ChildCol; i <= ChildCol; i++)
        {
            if (i == 0) continue;
            float X = curveSpacing[0] * Mathf.Sign(i) + i * curveSpacing[1];
            Vector3 P0_new = P0 + P0normal * X;
            Vector3 P3_new = P3 + P3normal * X;
            Vector3 P1_1 = P1 + P0normal * X;
            Vector3 P1_2 = P1 + P12normal * X;
            Vector3 P2_1 = P2 + P12normal * X;
            Vector3 P2_2 = P2 + P3normal * X;
            Vector3 P1_new = Intersection(P0_new, P1_1, P1_2, P2_1, Vector3.up);
            Vector3 P2_new = Intersection(P2_2, P3_new, P2_1, P1_2, Vector3.up);
            curve = new BezierCurve(P0_new, P1_new, P2_new, P3_new);
            s_curves[i + ChildCol, curveIndex] = curve;
            s_lengths[i + ChildCol] += curve.length;
        }
    }

    static Vector3 Intersection(Vector3 A, Vector3 B, Vector3 C, Vector3 D, Vector3 v)
    {
        Vector3 abDir = B - A;
        Vector3 cdDir = D - C;

        Vector3 planeNormal = Vector3.Cross(cdDir, v).normalized;
        float denominator = Vector3.Dot(planeNormal, abDir);

        // parallel
        if (Mathf.Approximately(denominator, 0f))
        {
            return B;
        }

        float t = Vector3.Dot(planeNormal, C - A) / denominator;
        return A + t * abDir;
    }

    public static Matrix4x4 GetRotation(float L)
    {
        return Matrix4x4.Rotate(Quaternion.Euler(0, 0, Mathf.Sin(L * rotateSpeed) * rotateAngle));
    }

    public static Matrix4x4 GetTransform(float L, int landID)
    {
        int colIndex = landID + ChildCol;
        L = Mathf.Min(L, s_lengths[colIndex]);
        // search segment
        float nowL = s_lengths[colIndex];
        for (int i = s_controlPointsSize - 2; i >= 0; i--)
        {
            int index = (s_controlPointsHead + i) % ControlPointCapacity;
            nowL -= s_curves[colIndex, index].length;
            if (nowL <= L || i == 0)
            {
                float s = (L - nowL) / s_curves[colIndex, index].length;
                return s_curves[colIndex, index].GetTransform(s);
            }
        }
        return Matrix4x4.identity;
    }
}