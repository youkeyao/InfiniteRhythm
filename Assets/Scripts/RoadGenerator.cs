// using System;
// using System.Collections.Generic;
// using Unity.Collections;
// using UnityEngine;

// public struct ControlPoint
// {
//     public Vector3 position;
//     public Vector3 direction;
// }

// public struct BezierCurve
// {
//     public Vector3 A;
//     public Vector3 B;
//     public Vector3 C;
//     public Vector3 D;
//     public float length;

//     private static float[][] gaussWX =
//     {
//         new []{ 0.2955242247147529f, -0.1488743389816312f },
//         new []{ 0.2955242247147529f, 0.1488743389816312f },
//         new []{ 0.2692667193099963f, -0.4333953941292472f },
//         new []{ 0.2692667193099963f, 0.4333953941292472f },
//         new []{ 0.2190863625159820f, -0.6794095682990244f },
//         new []{ 0.2190863625159820f, 0.6794095682990244f },
//         new []{ 0.1494513491505806f, -0.8650633666889845f },
//         new []{ 0.1494513491505806f, 0.8650633666889845f },
//         new []{ 0.0666713443086881f, -0.9739065285171717f },
//         new []{ 0.0666713443086881f, 0.9739065285171717f },
//     };

//     public BezierCurve(Vector3 P0, Vector3 P1, Vector3 P2, Vector3 P3)
//     {
//         A = -P0 + 3 * P1 - 3 * P2 + P3;
//         B = 3 * P0 - 6 * P1 + 3 * P2;
//         C = -3 * P0 + 3 * P1;
//         D = P0;
//         length = 0;
//         length = CalcLength(1f);
//     }

//     public Vector3 CalcDerivative(float t)
//     {
//         return 3f * A * t * t + 2f * B * t + C;
//     }

//     public float CalcLength(float t)
//     {
//         var halfT = t / 2f;

//         float sum = 0f;
//         foreach (float[] wx in gaussWX)
//         {
//             var w = wx[0];
//             var x = wx[1];
//             sum += w * CalcDerivative(halfT * x + halfT).magnitude;
//         }
//         sum *= halfT;
//         return sum;
//     }

//     public float T2S(float t)
//     {
//         return CalcLength(t) / length;
//     }

//     public float T2SDer(float t)
//     {
//         return CalcDerivative(t).magnitude / length;
//     }

//     public float S2T(float s)
//     {
//         const int NEWTON_SEGMENT = 4;

//         s = Mathf.Clamp01(s);
//         float t = s;
//         for (int i = 0; i < NEWTON_SEGMENT; i++)
//         {
//             t = t - (T2S(t) - s) / T2SDer(t);
//         }
//         return t;
//     }

//     public Matrix4x4 GetTransform(float s)
//     {
//         float t = S2T(s);
//         Vector3 pos = A * t * t * t + B * t * t + C * t + D;
//         Vector3 direction = CalcDerivative(t);
//         return Matrix4x4.TRS(pos, Quaternion.LookRotation(direction), Vector3.one);
//     }
// };

// public static class RoadGenerator
// {
//     const int ControlPointCapacity = 20;
//     public const int LandCol = 4;

//     public static Vector2 landSpacing = new Vector2(10, 15);
//     public static float slope = 5.0f;
//     public static float rotateAngle = 40.0f;
//     public static float rotateSpeed = 0.01f;

//     static ControlPoint[] s_controlPoints = InitializeControlPoints();
//     static int s_controlPointsHead = 0;
//     static int s_controlPointsSize = 1;
//     static BezierCurve[] s_curves = new BezierCurve[ControlPointCapacity]; // curve i -> controlPoints[i] - > controlPoints[i + 1]
//     static float[][] s_samples = new float[ControlPointCapacity][];
//     static BezierCurve[,] s_landCurves = new BezierCurve[LandCol, ControlPointCapacity];
//     static float s_totalLength = 0;
//     static float[] s_landLength = new float[LandCol];
//     static float s_lengthEpisilon = 1e-4f;

//     public static float GetRoadLength()
//     {
//         return s_totalLength;
//     }

//     public static float GetLandRatio(int col)
//     {
//         return s_landLength[col] / s_totalLength;
//     }

//     static ControlPoint[] InitializeControlPoints()
//     {
//         ControlPoint[] controlPoints = new ControlPoint[ControlPointCapacity];
//         for (int i = 0; i < ControlPointCapacity; i++)
//         {
//             controlPoints[i] = new ControlPoint { position = Vector3.zero, direction = Vector3.forward };
//         }
//         return controlPoints;
//     }

//     public static void GenerateNextControlPoint(float[] samples, float targetLength)
//     {
//         int prevIndex = (s_controlPointsHead + s_controlPointsSize - 1) % ControlPointCapacity;
//         s_samples[prevIndex] = samples;
//         ControlPoint prevControlPoint = s_controlPoints[prevIndex];

//         float L = targetLength;
//         float avgE = 0;
//         foreach (float sample in samples)
//         {
//             avgE += sample;
//         }
//         avgE /= samples.Length;
//         Vector3 nextPosDirection = Quaternion.Euler(slope * (avgE - 0.5f), UnityEngine.Random.Range(-15.0f, 15.0f), 0) * prevControlPoint.direction;
//         Vector3 nextPosition = prevControlPoint.position + L * nextPosDirection;
//         Vector3 nextDirection = Quaternion.Euler(slope * (avgE - 0.5f), UnityEngine.Random.Range(-15.0f, 15.0f), 0) * new Vector3(nextPosDirection.x, 0, nextPosDirection.z).normalized;
//         Vector3 P0 = prevControlPoint.position;
//         Vector3 P3 = nextPosition;
//         Vector3 P1 = P0 + prevControlPoint.direction * L / 3;
//         Vector3 P2 = P3 - nextDirection * L / 3;
//         BezierCurve curve = new BezierCurve(P0, P1, P2, P3);
//         // iterate until curve length is close enough to target length
//         for (int i = 0; i < 10; i++)
//         {
//             if (Mathf.Abs(curve.length - targetLength) < s_lengthEpisilon)
//                 break;
//             L -= curve.length - targetLength;
//             nextPosition = prevControlPoint.position + L * nextPosDirection;
//             P3 = nextPosition;
//             P1 = P0 + prevControlPoint.direction * L / 3;
//             P2 = P3 - nextDirection * L / 3;
//             curve = new BezierCurve(P0, P1, P2, P3);
//         }

//         // remove head
//         if (s_controlPointsSize == ControlPointCapacity)
//         {
//             s_controlPointsHead = (s_controlPointsHead + 1) % ControlPointCapacity;
//             s_controlPointsSize--;
//         }
//         int nextIndex = (s_controlPointsHead + s_controlPointsSize) % ControlPointCapacity;
//         s_controlPoints[nextIndex] = new ControlPoint { position = nextPosition, direction = nextDirection };
//         s_controlPointsSize++;

//         // update curves
//         s_curves[prevIndex] = curve;
//         s_totalLength += targetLength;
//         Vector3 P0normal = Vector3.Cross(s_controlPoints[prevIndex].direction, Vector3.up).normalized;
//         Vector3 P12normal = Vector3.Cross(P2 - P1, Vector3.up).normalized;
//         Vector3 P3normal = Vector3.Cross(s_controlPoints[nextIndex].direction, Vector3.up).normalized;
//         for (int i = 0; i < LandCol; i++)
//         {
//             float landColOffset = i - (LandCol - 1) / 2.0f;
//             float X = landSpacing[0] * Mathf.Sign(landColOffset) + landColOffset * landSpacing[1];
//             Vector3 P0_new = P0 + P0normal * X;
//             Vector3 P3_new = P3 + P3normal * X;
//             Vector3 P1_1 = P1 + P0normal * X;
//             Vector3 P1_2 = P1 + P12normal * X;
//             Vector3 P2_1 = P2 + P12normal * X;
//             Vector3 P2_2 = P2 + P3normal * X;
//             Vector3 P1_new = Intersection(P0_new, P1_1, P1_2, P2_1, Vector3.up);
//             Vector3 P2_new = Intersection(P2_2, P3_new, P2_1, P1_2, Vector3.up);
//             s_landCurves[i, prevIndex] = new BezierCurve(P0_new, P1_new, P2_new, P3_new);
//             s_landLength[i] += s_landCurves[i, prevIndex].length;
//         }
//     }

//     static Vector3 Intersection(Vector3 A, Vector3 B, Vector3 C, Vector3 D, Vector3 v)
//     {
//         Vector3 abDir = B - A;
//         Vector3 cdDir = D - C;

//         Vector3 planeNormal = Vector3.Cross(cdDir, v).normalized;
//         float denominator = Vector3.Dot(planeNormal, abDir);

//         // parallel
//         if (Mathf.Approximately(denominator, 0f))
//         {
//             return B;
//         }

//         float t = Vector3.Dot(planeNormal, C - A) / denominator;
//         return A + t * abDir;
//     }

//     public static Matrix4x4 GetRotation(float L)
//     {
//         return Matrix4x4.Rotate(Quaternion.Euler(0, 0, Mathf.Sin(L * rotateSpeed) * rotateAngle));
//     }

//     public static Matrix4x4 GetTransform(float L)
//     {
//         L = Mathf.Min(L, s_totalLength);
//         // search segment
//         float nowL = s_totalLength;
//         for (int i = s_controlPointsSize - 2; i >= 0; i--)
//         {
//             int index = (s_controlPointsHead + i) % ControlPointCapacity;
//             nowL -= s_curves[index].length;
//             if (nowL <= L || nowL < s_lengthEpisilon)
//             {
//                 float s = (L - nowL) / s_curves[index].length;
//                 return s_curves[index].GetTransform(s);
//             }
//         }
//         return Matrix4x4.identity;
//     }

//     public static Matrix4x4 GetXTransform(float L, int landID)
//     {
//         L = Mathf.Min(L, s_landLength[landID]);
//         // search segment
//         float nowL = s_landLength[landID];
//         for (int i = s_controlPointsSize - 2; i >= 0; i--)
//         {
//             int index = (s_controlPointsHead + i) % ControlPointCapacity;
//             nowL -= s_landCurves[landID, index].length;
//             if (nowL <= L || nowL < s_lengthEpisilon)
//             {
//                 float s = (L - nowL) / s_landCurves[landID, index].length;
//                 return s_landCurves[landID, index].GetTransform(s);
//             }
//         }
//         return Matrix4x4.identity;
//     }

//     public static float GetSample(float L)
//     {
//         L = Mathf.Min(L, s_totalLength);
//         // search segment
//         float nowL = s_totalLength;
//         for (int i = s_controlPointsSize - 2; i >= 0; i--)
//         {
//             int index = (s_controlPointsHead + i) % ControlPointCapacity;
//             nowL -= s_curves[index].length;
//             if (nowL <= L || nowL < s_lengthEpisilon)
//             {
//                 float s = (L - nowL) / s_curves[index].length;
//                 return s_samples[index][(int)(s * s_samples[index].Length)];
//             }
//         }
//         return 0;
//     }

//     public static float GetXSample(float L, int landID)
//     {
//         L = Mathf.Min(L, s_landLength[landID]);
//         // search segment
//         float nowL = s_landLength[landID];
//         for (int i = s_controlPointsSize - 2; i >= 0; i--)
//         {
//             int index = (s_controlPointsHead + i) % ControlPointCapacity;
//             nowL -= s_landCurves[landID, index].length;
//             if (nowL <= L || nowL < s_lengthEpisilon)
//             {
//                 float s = (L - nowL) / s_landCurves[landID, index].length;
//                 return s_samples[index][(int)(s * s_samples[index].Length)];
//             }
//         }
//         return 0;
//     }
// }