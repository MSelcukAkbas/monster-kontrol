// GPU yük üreticisi: NVIDIA bağdaştırıcısını DXGI ile açıkça seçer (Optimus tercihine bakmaz),
// D3D11 compute shader'ı verilen saniye boyunca durmadan çalıştırır. Ek paket yok: COM vtable çağrıları.
// Kullanım: gpu_yuk <saniye>
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

unsafe
{
    int saniye = args.Length > 0 ? int.Parse(args[0]) : 30;

    var iidFactory1 = new Guid("770aae78-f26f-4dba-a829-253c83d1b387");
    Hr(Yerel.CreateDXGIFactory1(&iidFactory1, out var fabrika), "CreateDXGIFactory1");

    IntPtr nvidia = IntPtr.Zero;
    var desc = stackalloc byte[512];
    for (uint i = 0; ; i++)
    {
        IntPtr a;
        if (((delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr*, int>)V(fabrika)[12])(fabrika, i, &a) < 0) break; // EnumAdapters1
        ((delegate* unmanaged[Stdcall]<IntPtr, byte*, int>)V(a)[10])(a, desc); // GetDesc1
        string ad = new string((char*)desc).TrimEnd('\0');
        uint vendor = *(uint*)(desc + 256);
        Console.WriteLine($"bağdaştırıcı {i}: {ad} (0x{vendor:X4})");
        if (vendor == 0x10DE && nvidia == IntPtr.Zero) nvidia = a;
    }
    if (nvidia == IntPtr.Zero) { Console.WriteLine("NVIDIA bulunamadı"); return 2; }

    // D3D_DRIVER_TYPE_UNKNOWN (bağdaştırıcı verildiğinde zorunlu), SDK sürümü 7.
    Hr(Yerel.D3D11CreateDevice(nvidia, 0, IntPtr.Zero, 0, null, 0, 7, out var cihaz, out _, out var bag), "D3D11CreateDevice");

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
        hr = Yerel.D3DCompile(s, (nuint)src.Length, null, IntPtr.Zero, IntPtr.Zero, "main", "cs_5_0", 0, 0, out blob, out hata);
    if (hr < 0) { Console.WriteLine(Marshal.PtrToStringAnsi(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr>)V(hata)[3])(hata))); return 3; }
    var kod = ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr>)V(blob)[3])(blob);
    var boy = ((delegate* unmanaged[Stdcall]<IntPtr, nuint>)V(blob)[4])(blob);

    IntPtr cs, tampon, uav;
    Hr(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, nuint, IntPtr, IntPtr*, int>)V(cihaz)[18])(cihaz, kod, boy, IntPtr.Zero, &cs), "CreateComputeShader");

    const uint grup = 1024, eleman = grup * 256;
    // D3D11_BUFFER_DESC: ByteWidth, Usage=DEFAULT, BindFlags=UNORDERED_ACCESS, CPUAccess=0, Misc=STRUCTURED, Stride=16
    uint* bd = stackalloc uint[] { eleman * 16, 0, 0x80, 0, 0x40, 16 };
    Hr(((delegate* unmanaged[Stdcall]<IntPtr, uint*, IntPtr, IntPtr*, int>)V(cihaz)[3])(cihaz, bd, IntPtr.Zero, &tampon), "CreateBuffer");
    // D3D11_UNORDERED_ACCESS_VIEW_DESC: Format=UNKNOWN, Dimension=BUFFER, FirstElement, NumElements, Flags
    uint* ud = stackalloc uint[] { 0, 1, 0, eleman, 0 };
    Hr(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint*, IntPtr*, int>)V(cihaz)[8])(cihaz, tampon, ud, &uav), "CreateUnorderedAccessView");
    uint* qd = stackalloc uint[] { 0, 0 }; // D3D11_QUERY_EVENT
    var sorgular = stackalloc IntPtr[2];
    for (int k = 0; k < 2; k++)
        Hr(((delegate* unmanaged[Stdcall]<IntPtr, uint*, IntPtr*, int>)V(cihaz)[24])(cihaz, qd, &sorgular[k]), "CreateQuery");

    ((delegate* unmanaged[Stdcall]<IntPtr, uint, uint, IntPtr*, uint*, void>)V(bag)[68])(bag, 0, 1, &uav, null); // CSSetUnorderedAccessViews
    ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr*, uint, void>)V(bag)[69])(bag, cs, null, 0);          // CSSetShader

    var dispatch = (delegate* unmanaged[Stdcall]<IntPtr, uint, uint, uint, void>)V(bag)[41];
    var end = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, void>)V(bag)[28];
    var getData = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, uint, uint, int>)V(bag)[29];

    var sure = Stopwatch.StartNew();
    long toplam = 0, sonRapor = 0;
    Console.WriteLine($"yük başladı: {saniye} sn");
    for (int p = 0; sure.Elapsed.TotalSeconds < saniye; p ^= 1)
    {
        // Her dispatch ~5 ms sürer (TDR sınırı 2 sn). Çift tampon: yeni paketi kuyruğa koy, sonra bir öncekini bekle;
        // böylece CPU beklerken GPU'da hep bir paket iş vardır (tek paketle kullanım %50–95 zıplıyordu).
        for (int k = 0; k < 60; k++) dispatch(bag, grup, 1, 1);
        end(bag, sorgular[p]);
        if (toplam > 0)
            while (getData(bag, sorgular[p ^ 1], IntPtr.Zero, 0, 0) == 1) Thread.Sleep(1); // S_FALSE = henüz bitmedi
        toplam += 60;
        if (sure.ElapsedMilliseconds - sonRapor >= 5000)
        {
            sonRapor = sure.ElapsedMilliseconds;
            Console.WriteLine($"{sure.Elapsed.TotalSeconds:F0} sn: {toplam / sure.Elapsed.TotalSeconds:F0} dispatch/sn");
        }
    }
    Console.WriteLine("yük bitti");
    return 0;

    static IntPtr* V(IntPtr o) => *(IntPtr**)o;
    static void Hr(int hr, string ne) { if (hr < 0) throw new Exception($"{ne} 0x{hr:X8}"); }
}

static unsafe class Yerel
{
    [DllImport("dxgi.dll")] public static extern int CreateDXGIFactory1(Guid* riid, out IntPtr factory);
    [DllImport("d3d11.dll")]
    public static extern int D3D11CreateDevice(IntPtr adapter, int driverType, IntPtr software, uint flags,
        int* featureLevels, uint levels, uint sdk, out IntPtr device, out int level, out IntPtr context);
    [DllImport("d3dcompiler_47.dll", CharSet = CharSet.Ansi)]
    public static extern int D3DCompile(byte* src, nuint size, string? name, IntPtr defines, IntPtr include,
        string entry, string target, uint f1, uint f2, out IntPtr code, out IntPtr errors);
}
