using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace MonsterPerformans.Cekirdek;

/// <summary>"Ölçüm yap" yükü: her mantıksal çekirdekte bir iş parçacığı ve NVIDIA GPU'da D3D11 compute (prototip/gpu_yuk).</summary>
public static unsafe class Yuk
{
    /// <summary>İş parçacıkları düşük öncelikte: arayüz ve örnekleyici aksamasın, çekirdekler yine dolu kalır.</summary>
    public static Task Cpu(CancellationToken dur) => Task.WhenAll(Enumerable.Range(0, Environment.ProcessorCount).Select(_ =>
        Task.Factory.StartNew(() =>
        {
            Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
            var x = 1.0;
            while (!dur.IsCancellationRequested)
                for (var i = 0; i < 200_000; i++) x = Math.Sqrt(x + i);
            GC.KeepAlive(x);
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default))); // iptal döngüde: başlamadan iptal edilse de görev normal biter

    /// <summary>
    /// NVIDIA bağdaştırıcısını DXGI ile açıkça seçer (Optimus tercihine bakmaz). Yoksa hemen false döner.
    /// Her dispatch ~5 ms (TDR sınırı 2 sn); çift sorgu ile GPU'da hep bir paket iş bekler.
    /// </summary>
    public static Task<bool> Gpu(CancellationToken dur) => Task.Factory.StartNew(() => GpuCalistir(dur),
        CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    static bool GpuCalistir(CancellationToken dur)
    {
        var com = new List<IntPtr>(); // hepsi sonunda bırakılır: uygulama uzun yaşar, her ölçümde sızmasın
        try
        {
            var iid = new Guid("770aae78-f26f-4dba-a829-253c83d1b387"); // IDXGIFactory1
            Hr(CreateDXGIFactory1(&iid, out var fabrika), "CreateDXGIFactory1");
            com.Add(fabrika);

            IntPtr nvidia = IntPtr.Zero;
            var desc = stackalloc byte[512];
            for (uint i = 0; ; i++)
            {
                IntPtr a;
                if (((delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr*, int>)V(fabrika)[12])(fabrika, i, &a) < 0) break; // EnumAdapters1
                com.Add(a);
                ((delegate* unmanaged[Stdcall]<IntPtr, byte*, int>)V(a)[10])(a, desc); // GetDesc1
                if (*(uint*)(desc + 256) == 0x10DE && nvidia == IntPtr.Zero) nvidia = a;
            }
            if (nvidia == IntPtr.Zero) return false;

            // D3D_DRIVER_TYPE_UNKNOWN (bağdaştırıcı verildiğinde zorunlu), SDK sürümü 7
            Hr(D3D11CreateDevice(nvidia, 0, IntPtr.Zero, 0, null, 0, 7, out var cihaz, out _, out var bag), "D3D11CreateDevice");
            com.Add(cihaz);
            com.Add(bag);

            const string kaynak = """
                RWStructuredBuffer<float4> o : register(u0);
                [numthreads(256,1,1)]
                void main(uint3 id : SV_DispatchThreadID) {
                    float4 a = float4(id.x * 1e-6, 1.0001, 0.9999, 0.5);
                    float4 b = a.yxwz;
                    [loop] for (int i = 0; i < 4096; i++) { a = a * b + 0.0001; b = b * a - 0.0001; }
                    o[id.x] = a + b;
                }
                """;
            var src = Encoding.ASCII.GetBytes(kaynak);
            int hr;
            IntPtr blob, hata;
            fixed (byte* s = src)
                hr = D3DCompile(s, (nuint)src.Length, null, IntPtr.Zero, IntPtr.Zero, "main", "cs_5_0", 0, 0, out blob, out hata);
            if (hata != IntPtr.Zero) com.Add(hata);
            Hr(hr, "D3DCompile");
            com.Add(blob);
            var kod = ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr>)V(blob)[3])(blob);
            var boy = ((delegate* unmanaged[Stdcall]<IntPtr, nuint>)V(blob)[4])(blob);

            IntPtr cs, tampon, uav;
            Hr(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, nuint, IntPtr, IntPtr*, int>)V(cihaz)[18])(cihaz, kod, boy, IntPtr.Zero, &cs), "CreateComputeShader");
            com.Add(cs);
            const uint grup = 1024, eleman = grup * 256;
            // D3D11_BUFFER_DESC: ByteWidth, Usage=DEFAULT, BindFlags=UNORDERED_ACCESS, CPUAccess=0, Misc=STRUCTURED, Stride=16
            uint* bd = stackalloc uint[] { eleman * 16, 0, 0x80, 0, 0x40, 16 };
            Hr(((delegate* unmanaged[Stdcall]<IntPtr, uint*, IntPtr, IntPtr*, int>)V(cihaz)[3])(cihaz, bd, IntPtr.Zero, &tampon), "CreateBuffer");
            com.Add(tampon);
            // D3D11_UNORDERED_ACCESS_VIEW_DESC: Format=UNKNOWN, Dimension=BUFFER, FirstElement, NumElements, Flags
            uint* ud = stackalloc uint[] { 0, 1, 0, eleman, 0 };
            Hr(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint*, IntPtr*, int>)V(cihaz)[8])(cihaz, tampon, ud, &uav), "CreateUnorderedAccessView");
            com.Add(uav);
            uint* qd = stackalloc uint[] { 0, 0 }; // D3D11_QUERY_EVENT
            var sorgu = stackalloc IntPtr[2];
            for (var k = 0; k < 2; k++)
            {
                Hr(((delegate* unmanaged[Stdcall]<IntPtr, uint*, IntPtr*, int>)V(cihaz)[24])(cihaz, qd, &sorgu[k]), "CreateQuery");
                com.Add(sorgu[k]);
            }

            ((delegate* unmanaged[Stdcall]<IntPtr, uint, uint, IntPtr*, uint*, void>)V(bag)[68])(bag, 0, 1, &uav, null); // CSSetUnorderedAccessViews
            ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr*, uint, void>)V(bag)[69])(bag, cs, null, 0); // CSSetShader
            var dispatch = (delegate* unmanaged[Stdcall]<IntPtr, uint, uint, uint, void>)V(bag)[41];
            var end = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, void>)V(bag)[28];
            var getData = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, uint, uint, int>)V(bag)[29];

            var ilk = true;
            for (var p = 0; !dur.IsCancellationRequested; p ^= 1)
            {
                for (var k = 0; k < 60; k++) dispatch(bag, grup, 1, 1);
                end(bag, sorgu[p]);
                if (!ilk)
                    while (getData(bag, sorgu[p ^ 1], IntPtr.Zero, 0, 0) == 1) Thread.Sleep(1); // S_FALSE: henüz bitmedi
                ilk = false;
            }
            ((delegate* unmanaged[Stdcall]<IntPtr, void>)V(bag)[110])(bag); // ClearState: bağlı kaynaklar bırakılabilsin
            return true;
        }
        finally
        {
            for (var i = com.Count - 1; i >= 0; i--) ((delegate* unmanaged[Stdcall]<IntPtr, uint>)V(com[i])[2])(com[i]); // Release
        }
    }

    static IntPtr* V(IntPtr o) => *(IntPtr**)o;

    static void Hr(int hr, string ne)
    {
        if (hr < 0) throw new ExternalException($"{ne} 0x{hr:X8}", hr);
    }

    [DllImport("dxgi.dll")] static extern int CreateDXGIFactory1(Guid* riid, out IntPtr factory);

    [DllImport("d3d11.dll")]
    static extern int D3D11CreateDevice(IntPtr adapter, int driverType, IntPtr software, uint flags,
        int* featureLevels, uint levels, uint sdk, out IntPtr device, out int level, out IntPtr context);

    [DllImport("d3dcompiler_47.dll", CharSet = CharSet.Ansi)]
    static extern int D3DCompile(byte* src, nuint size, string? name, IntPtr defines, IntPtr include,
        string entry, string target, uint f1, uint f2, out IntPtr code, out IntPtr errors);
}
