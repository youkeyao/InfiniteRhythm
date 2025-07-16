using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;

// Cooley–Tukey FFT vectorized/parallelized with the Burst compiler

public sealed class BurstFFT : System.IDisposable
{
    #region Public properties and methods

    public NativeArray<float> Spectrum => _O;

    public BurstFFT(int width)
    {
        _N = width;
        _logN = (int)math.log2(width);

        BuildPermutationTable();
        BuildTwiddleFactors();

        _O = new NativeArray<float>(_N, Allocator.Persistent);
    }

    public void Dispose()
    {
        if (_P.IsCreated) _P.Dispose();
        if (_T.IsCreated) _T.Dispose();
        if (_O.IsCreated) _O.Dispose();
    }

    public void Transform(NativeArray<float> input)
    {
        var X = new NativeArray<float4>(_N / 2, Allocator.TempJob);

        // Bit-reversal permutation and first DFT pass
        var handle = new FirstPassJob { I = input, P = _P, X = X }
          .Schedule(_N / 2, 32);

        // 2nd and later DFT passes
        for (var i = 0; i < _logN - 1; i++)
        {
            var T_slice = new NativeSlice<TFactor>(_T, _N / 4 * i);
            handle = new DftPassJob { T = T_slice, X = X }
              .Schedule(_N / 4, 32, handle);
        }

        // Postprocess (power spectrum calculation)
        var O2 = _O.Reinterpret<float2>(sizeof(float));
        handle = new PostprocessJob { X = X, O = O2, s = 2.0f / _N }
          .Schedule(_N / 2, 32, handle);

        handle.Complete();
        X.Dispose();
    }

    #endregion

    #region Private members

    readonly int _N;
    readonly int _logN;
    NativeArray<float> _O;

    #endregion

    #region Bit-reversal permutation table

    NativeArray<int2> _P;

    void BuildPermutationTable()
    {
        _P = new NativeArray<int2>(_N / 2, Allocator.Persistent);
        for (var i = 0; i < _N; i += 2)
            _P[i / 2] = math.int2(Permutate(i), Permutate(i + 1));
    }

    int Permutate(int x)
      => Enumerable.Range(0, _logN)
         .Aggregate(0, (acc, i) => acc += ((x >> i) & 1) << (_logN - 1 - i));

    #endregion

    #region Precalculated twiddle factors

    struct TFactor
    {
        public int2 I;
        public float2 W;

        public int i1 => I.x;
        public int i2 => I.y;

        public float4 W4
          => math.float4(W.x, math.sqrt(1 - W.x * W.x),
                         W.y, math.sqrt(1 - W.y * W.y));
    }

    NativeArray<TFactor> _T;

    void BuildTwiddleFactors()
    {
        _T = new NativeArray<TFactor>((_logN - 1) * (_N / 4), Allocator.Persistent);

        var i = 0;
        for (var m = 4; m <= _N; m <<= 1)
            for (var k = 0; k < _N; k += m)
                for (var j = 0; j < m / 2; j += 2)
                    _T[i++] = new TFactor
                    {
                        I = math.int2((k + j) / 2, (k + j + m / 2) / 2),
                        W = math.cos(-2 * math.PI / m * math.float2(j, j + 1))
                    };
    }

    #endregion

    #region First pass job

    [BurstCompile(CompileSynchronously = true)]
    struct FirstPassJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> I;
        [ReadOnly] public NativeArray<int2> P;
        [WriteOnly] public NativeArray<float4> X;

        public void Execute(int i)
        {
            var a1 = I[P[i].x];
            var a2 = I[P[i].y];
            X[i] = math.float4(a1 + a2, 0, a1 - a2, 0);
        }
    }

    #endregion

    #region DFT pass job

    [BurstCompile(CompileSynchronously = true)]
    struct DftPassJob : IJobParallelFor
    {
        [ReadOnly] public NativeSlice<TFactor> T;
        [NativeDisableParallelForRestriction] public NativeArray<float4> X;

        static float4 Mulc(float4 a, float4 b)
          => a.xxzz * b.xyzw + math.float4(-1, 1, -1, 1) * a.yyww * b.yxwz;

        public void Execute(int i)
        {
            var t = T[i];
            var e = X[t.i1];
            var o = Mulc(t.W4, X[t.i2]);
            X[t.i1] = e + o;
            X[t.i2] = e - o;
        }
    }

    #endregion

    #region Postprocess Job

    [BurstCompile(CompileSynchronously = true)]
    struct PostprocessJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float4> X;
        [WriteOnly] public NativeArray<float2> O;
        public float s;

        public void Execute(int i)
        {
            var x = X[i];
            O[i] = math.float2(math.length(x.xy), math.length(x.zw)) * s;
        }
    }

    #endregion
}