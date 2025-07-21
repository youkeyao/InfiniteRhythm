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

    public BezierCurve(Vector3 P0, Vector3 P1, Vector3 P2, Vector3 P3)
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

        s = Mathf.Clamp01(s);
        float t = s;
        for (int i = 0; i < NEWTON_SEGMENT; i++)
        {
            t = t - (T2S(t) - s) / T2SDer(t);
        }
        return t;
    }

    public Matrix4x4 GetTransform(float s)
    {
        float t = S2T(s);
        Vector3 pos = A * t * t * t + B * t * t + C * t + D;
        Vector3 direction = CalcDerivative(t);
        return Matrix4x4.TRS(pos, Quaternion.LookRotation(direction), Vector3.one);
    }
};

public static class RoadGenerator
{
    const int ControlPointCapacity = 20;
    public static float baseSegmentLength = 100f;
    public static int samplesPerSegment = 100;

    static ControlPoint[] s_controlPoints = InitializeControlPoints();
    static int s_controlPointsHead = 0;
    static int s_controlPointsSize = 1;
    static BezierCurve[] s_curves = new BezierCurve[ControlPointCapacity]; // curve i -> controlPoints[i] - > controlPoints[i + 1]
    static float s_totalLength = 0;

    static ControlPoint[] InitializeControlPoints()
    {
        ControlPoint[] controlPoints = new ControlPoint[ControlPointCapacity];
        for (int i = 0; i < ControlPointCapacity; i++)
        {
            controlPoints[i] = new ControlPoint { position = Vector3.zero, direction = Vector3.forward };
        }
        return controlPoints;
    }

    static void GenerateNextControlPoint()
    {
        ControlPoint lastControlPoint = s_controlPoints[(s_controlPointsHead + s_controlPointsSize - 1) % ControlPointCapacity];

        Vector3 nextPosition = lastControlPoint.position + Quaternion.Euler(UnityEngine.Random.Range(-5.0f, 5.0f), UnityEngine.Random.Range(-45.0f, 45.0f), 0) * lastControlPoint.direction * baseSegmentLength;
        Vector3 nextDirection = Quaternion.Euler(0, UnityEngine.Random.Range(-45.0f, 45.0f), 0) * lastControlPoint.direction;

        // remove head
        if (s_controlPointsSize == ControlPointCapacity)
        {
            s_controlPointsHead = (s_controlPointsHead + 1) % ControlPointCapacity;
            s_controlPointsSize--;
        }
        s_controlPoints[(s_controlPointsHead + s_controlPointsSize) % ControlPointCapacity] = new ControlPoint { position = nextPosition, direction = nextDirection };
        s_controlPointsSize++;
    }

    static void UpdateCurves()
    {
        if (s_controlPointsSize < 2) return;

        int prevIndex = (s_controlPointsHead + s_controlPointsSize - 2) % ControlPointCapacity;
        int nextIndex = (s_controlPointsHead + s_controlPointsSize - 1) % ControlPointCapacity;
        Vector3 P0 = s_controlPoints[prevIndex].position;
        Vector3 P1 = P0 + s_controlPoints[prevIndex].direction * baseSegmentLength / 2;
        Vector3 P3 = s_controlPoints[nextIndex].position;
        Vector3 P2 = P3 - s_controlPoints[nextIndex].direction * baseSegmentLength / 2;

        s_curves[prevIndex] = new BezierCurve(P0, P1, P2, P3);
        s_totalLength += s_curves[prevIndex].length;
    }

    public static Matrix4x4 GetTransform(float L)
    {
        while (L >= s_totalLength)
        {
            GenerateNextControlPoint();
            UpdateCurves();
        }

        // search segment
        float nowL = s_totalLength;
        for (int i = s_controlPointsSize - 2; i >= 0; i--)
        {
            int index = (s_controlPointsHead + i) % ControlPointCapacity;
            nowL -= s_curves[index].length;
            if (nowL <= L)
            {
                return s_curves[index].GetTransform((L - nowL) / s_curves[index].length);
            }
        }
        return Matrix4x4.identity;
    }
}