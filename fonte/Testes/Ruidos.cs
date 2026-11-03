// 31/10 noise bench (user: claps, table knocks, phone music, motorbikes pass the suppressor). Synthetic noises alone and over a
// voice, through the suppressor alone and the whole mic chain (saved defaults). Per noise: level in/out with no voice, noise left
// while talking (mix output minus voice-only output) and voice kept. Run: FAH.Testes.exe --ruidos [voz.wav] [pasta de saída].
using System.Text.Json;
using FAH.Audio;
using FAH.Contrato;

static class Ruidos
{
    const int T = 48000;

    static float[] Ler(string arq)
    {
        using var r0 = new NAudio.Wave.WaveFileReader(arq); var r = NAudio.Wave.WaveExtensionMethods.ToSampleProvider(r0);
        var l = new List<float>(); var b = new float[r.WaveFormat.Channels * 4800]; int n;
        while ((n = r.Read(b, 0, b.Length)) > 0) for (int i = 0; i < n; i += r.WaveFormat.Channels) l.Add(b[i]);
        var x = l.ToArray(); int sr = r.WaveFormat.SampleRate;
        return sr == T ? x : [.. Enumerable.Range(0, (int)((long)x.Length * T / sr)).Select(i => x[Math.Min(x.Length - 1, (int)((long)i * sr / T))])];
    }

    static double Rms(float[] x, int a, int b) { double e = 0; for (int i = a; i < b; i++) e += x[i] * x[i]; return 10 * Math.Log10(e / Math.Max(1, b - a) + 1e-12); }
    static double Pico(float[] x, int a, int b) { float p = 0; for (int i = a; i < b; i++) p = Math.Max(p, Math.Abs(x[i])); return 20 * Math.Log10(p + 1e-9); }
    static void Escala(float[] x, double dbRms) { double g = Math.Pow(10, (dbRms - Rms(x, 0, x.Length)) / 20); for (int i = 0; i < x.Length; i++) x[i] = (float)(x[i] * g); }

    // noises, n samples each
    static float[] Palmas(int n, Random r)
    {
        var x = new float[n]; float lp = 0;
        for (int p = T / 4; p < n; p += T / 2 + r.Next(T / 5))
            for (int i = 0; i < T / 8 && p + i < n; i++) { float w = (float)(r.NextDouble() * 2 - 1), h = w - lp; lp = w; x[p + i] += h * MathF.Exp(-i / (0.011f * T)) * 0.7f; }
        return x;
    }
    static float[] Mesa(int n, Random r)
    {
        var x = new float[n];
        for (int p = T / 3; p < n; p += (int)(T * 0.7) + r.Next(T / 4))
            for (int i = 0; i < T / 4 && p + i < n; i++)
            {
                float t = (float)i / T;
                x[p + i] += 0.6f * MathF.Sin(2 * MathF.PI * 105 * t) * MathF.Exp(-t / 0.05f) + 0.35f * MathF.Sin(2 * MathF.PI * 340 * t) * MathF.Exp(-t / 0.015f)
                    + 0.3f * (float)(r.NextDouble() * 2 - 1) * MathF.Exp(-t / 0.002f);
            }
        return x;
    }
    static float[] Moto(int n, Random r)
    {
        var x = new float[n]; double fase = 0; float br = 0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / T, f = 75 + 45 * Math.Sin(2 * Math.PI * 0.3 * t) + 8 * Math.Sin(2 * Math.PI * 3 * t);
            fase += f / T; double s = 2 * (fase - Math.Floor(fase)) - 1;
            br = br * 0.995f + (float)(r.NextDouble() * 2 - 1) * 0.05f;
            double am = 0.7 + 0.3 * Math.Sin(2 * Math.PI * f / 4 * t), pas = 0.3 + 0.7 * Math.Sin(Math.PI * Math.Min(1, t / (n / (double)T)));
            x[i] = (float)((s * 0.5 * am + br * 3) * pas);
        }
        return x;
    }

    sealed record Caso(string Nome, float[] Ruido, double Db);

    // model's lsnr per 480 frame (suppressor alone); Silero prob per 32 ms chunk
    static float[] Snr(float[] x) => Snr(x, out _);
    static float[] Snr(float[] x, out float[] vad)
    {
        var m = new FAH.Microfone.Modulo(); m.Receber("ligado", JsonDocument.Parse("{}").RootElement); m.Supressor.Carregar(); m.Supressor.Ligado = true; m.Supressor.Forca = 1;
        var c = new Cadeia(); c.Definir([m.Supressor]); var buf = new float[960]; var l = new List<float>();
        for (int p = 0; p + 480 <= x.Length; p += 480) { for (int i = 0; i < 480; i++) buf[2 * i] = buf[2 * i + 1] = x[p + i]; c.Processar(buf, 480); l.Add(m.Supressor.Lsnr); }
        var v = new FAH.Microfone.Vad(); v.Carregar(); if (v.Erro != null) throw new Exception("vad: " + v.Erro);
        var lv = new List<float>(); foreach (var a in x) if (v.Empurrar(a)) lv.Add(v.Prob);
        if (v.Falha != null) Console.WriteLine($"vad falhou: {v.Falha} | {v.Formato}");
        vad = [.. lv]; return [.. l];
    }
    static string Dist(float[] s, int a, int b) { var l = s[a..b].OrderBy(v => v).ToArray(); return $"p10 {l[l.Length / 10],5:F1} p50 {l[l.Length / 2],5:F1} p90 {l[l.Length * 9 / 10],5:F1} max {l[^1],5:F1}"; }

    public static int Rodar(string[] args)
    {
        string raiz = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var voz = Ler(args.Length > 1 && args[1] != "-" ? args[1] : Path.Combine(raiz, "docs", "teste-real", "amostras-mic", "2-so-supressor.wav"));
        string? sai = args.Length > 2 ? args[2] : null; if (sai != null) Directory.CreateDirectory(sai);
        int nv = Math.Min(voz.Length, 8 * T), so = 4 * T, n = so + nv;
        voz = voz[..nv]; Escala(voz, -26);
        var r = new Random(7);
        var musica = Ler(Path.Combine(raiz, "amostras", "Music_1_original.wav"));
        var casos = new List<Caso> { new("palmas", Palmas(n, r), -24), new("mesa", Mesa(n, r), -24), new("moto", Moto(n, r), -22),
            new("musica", [.. Enumerable.Range(0, n).Select(i => musica[i % musica.Length])], -26),
            new("moto-32", Moto(n, new Random(3)), -32), new("musc-34", [.. Enumerable.Range(0, n).Select(i => musica[(i + 5 * T) % musica.Length])], -34) };
        var vazio = JsonDocument.Parse("{}").RootElement;

        // fresh module each run (model state), voice passes in blocks of 1024 like the engine
        // modes: 0 dfn, 1 gate on lsnr (old), 2 gate on Silero, 3 Silero + impact softener, 4 whole chain (saved defaults)
        long golpes = 0;
        float[] Passa(float[] x, int modo)
        {
            var m = new FAH.Microfone.Modulo(); m.Receber("ligado", vazio); m.Supressor.Carregar();
            if (m.Supressor.Erro != null) throw new Exception(m.Supressor.Erro);
            if (m.Supressor.Vad.Erro != null) throw new Exception("vad: " + m.Supressor.Vad.Erro);
            var c = new Cadeia();
            c.Definir(modo == 4 ? m.Efeitos.Where(e => e.Id != "ruido").Append(m.Supressor) : [m.Mono, m.Supressor]);
            m.Supressor.Ligado = true; m.Supressor.Forca = 1;
            if (modo < 4) { m.Supressor.Portao = modo > 0; m.Supressor.Ouvido = modo == 1 ? 0 : 1; m.Supressor.Impacto = modo == 3; }
            if (Environment.GetEnvironmentVariable("FAH_HZ") is { } hz) m.Supressor.ImpactoHz = float.Parse(hz);
            if (Environment.GetEnvironmentVariable("FAH_K") is { } kk) m.Supressor.ImpactoK = float.Parse(kk, System.Globalization.CultureInfo.InvariantCulture);
            var y = new float[x.Length]; var buf = new float[2048];
            for (int p = 0; p < x.Length; p += 1024)
            {
                int q = Math.Min(1024, x.Length - p);
                for (int i = 0; i < q; i++) buf[2 * i] = buf[2 * i + 1] = x[p + i];
                c.Processar(buf.AsSpan(0, q * 2), q);
                for (int i = 0; i < q; i++) y[p + i] = buf[2 * i];
            }
            golpes = m.Supressor.Golpes;
            return y;
        }
        void Grava(string nome, float[] m) { if (sai == null) return; using var w = new NAudio.Wave.WaveFileWriter(Path.Combine(sai, nome), NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(T, 1)); w.WriteSamples(m, 0, m.Length); }

        if (Environment.GetEnvironmentVariable("FAH_MODOS") == null) { var sv0 = new float[n]; Array.Copy(voz, 0, sv0, so, nv); var sn = Snr(sv0, out var vv); int v0 = so / 1536;
          Console.WriteLine($"lsnr voz sozinha: silêncio {Dist(sn, 10, so / 480)} | fala {Dist(sn, so / 480 + 10, n / 480)}");
          Console.WriteLine($"vad  voz sozinha: silêncio {Dist(vv, 3, v0)} | fala {Dist(vv, v0 + 3, vv.Length)}");
          foreach (var c in casos) { var ru = c.Ruido[..n]; Escala(ru, c.Db); var mx = new float[n]; for (int i = 0; i < n; i++) mx[i] = sv0[i] + ru[i]; var s2 = Snr(mx, out var v2);
            Console.WriteLine($"lsnr {c.Nome,-7}: só ruído {Dist(s2, 10, so / 480)} | com voz {Dist(s2, so / 480 + 10, n / 480)}");
            Console.WriteLine($"vad  {c.Nome,-7}: só ruído {Dist(v2, 3, v0)} | com voz {Dist(v2, v0 + 3, v2.Length)}"); } }
        const int D = 1919;                                   // suppressor delay (frame 480 - 1 + model 1440)
        foreach (int modo in (Environment.GetEnvironmentVariable("FAH_MODOS") ?? "01234").Select(ch => ch - '0'))
        {
            string nm = new[] { "dfn", "porta-lsnr", "porta-silero", "silero-impacto", "cadeia" }[modo]; const int dd = 1919;
            Console.WriteLine(new[] { "== só o supressor (força 100%) ==", "== portão de voz antigo (lsnr) ==", "== portão de voz Silero ==", "== Silero + abafador de impacto ==", "== cadeia inteira (padrões) ==" }[modo]);
            var sv = new float[n]; Array.Copy(voz, 0, sv, so, nv);
            var yv = Passa(sv, modo); long gv = golpes;
            if (modo >= 3) Console.WriteLine($"golpes falsos na voz sozinha: {gv}");
            foreach (var c in casos)
            {
                var ru = c.Ruido[..n]; Escala(ru, c.Db);
                var mix = new float[n]; for (int i = 0; i < n; i++) mix[i] = sv[i] + ru[i];
                var y = Passa(mix, modo);
                var resto = new float[n]; for (int i = 0; i < n; i++) resto[i] = y[i] - yv[i];
                int a0 = T / 2, a1 = so - T / 4, b0 = so + T / 4 + dd, b1 = n;
                int fr = 0, cort = 0; for (int p = b0; p + 960 <= b1; p += 960) { double ev = Rms(yv, p, p + 960); if (ev < -45) continue; fr++; if (Rms(y, p, p + 960) < ev - 10) cort++; }
                Console.Write($"cortes {100.0 * cort / Math.Max(1, fr),4:F1}% " + (modo >= 3 ? $"golpes {golpes,3} " : ""));
                Console.WriteLine($"{c.Nome,-7} sem voz: rms {Rms(ru, a0, a1),6:F1} -> {Rms(y, a0 + dd, a1 + dd),6:F1} | pico {Pico(ru, a0, a1),6:F1} -> {Pico(y, a0 + dd, a1 + dd),6:F1} dB" +
                    $" || com voz: ruído {Rms(ru, b0 - dd, b1 - dd),6:F1} -> {Rms(resto, b0, b1),6:F1} | voz {Rms(sv, b0 - dd, b1 - dd),6:F1} -> {Rms(yv, b0, b1),6:F1} dB");
                if (modo == 0) Grava($"{c.Nome}-entra.wav", mix); Grava($"{c.Nome}-{nm}.wav", y);
            }
        }
        return 0;
    }
}
