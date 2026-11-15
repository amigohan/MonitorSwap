using System.Media;

namespace MonitorSwap;

enum Sound { KonsolaGecis, BilgisayaraDonus, OtomatikDonus, Hata, Acilis }

/// <summary>
/// Uygulamanın içine gömülü kısa efekt seslerini çalar. Sesler bu proje için kodla
/// üretilmiş özgün okarina ve çan sesleridir. Ses seviyesi, PCM örnekleri ölçeklenerek ayarlanır.
/// </summary>
static class Sfx
{
    public static bool Enabled { get; set; } = true;
    public static int Volume { get; set; } = 70; // 0–100

    static readonly SoundPlayer Player = new();
    static readonly Dictionary<Sound, byte[]> Cache = new();

    static string ResourceName(Sound s) => "MonitorSwap.Sounds." + s switch
    {
        Sound.KonsolaGecis => "konsola-gecis.wav",
        Sound.BilgisayaraDonus => "bilgisayara-donus.wav",
        Sound.OtomatikDonus => "otomatik-donus.wav",
        Sound.Hata => "hata.wav",
        _ => "acilis.wav",
    };

    static byte[] Load(Sound s)
    {
        if (Cache.TryGetValue(s, out var b)) return b;
        using var st = typeof(Sfx).Assembly.GetManifestResourceStream(ResourceName(s));
        if (st == null) return null;
        b = new byte[st.Length];
        st.ReadExactly(b);
        return Cache[s] = b;
    }

    /// <summary>16 bit PCM WAV'ın "data" bölümündeki örnekleri ses seviyesine göre ölçekler.</summary>
    static byte[] Scale(byte[] wav, float gain)
    {
        var copy = (byte[])wav.Clone();
        int pos = 12;
        while (pos + 8 <= copy.Length)
        {
            string id = System.Text.Encoding.ASCII.GetString(copy, pos, 4);
            int size = BitConverter.ToInt32(copy, pos + 4);
            if (id == "data")
            {
                int end = Math.Min(copy.Length, pos + 8 + size);
                for (int i = pos + 8; i + 1 < end; i += 2)
                {
                    int v = (int)(BitConverter.ToInt16(copy, i) * gain);
                    v = Math.Clamp(v, short.MinValue, short.MaxValue);
                    copy[i] = (byte)(v & 0xFF);
                    copy[i + 1] = (byte)((v >> 8) & 0xFF);
                }
                break;
            }
            pos += 8 + size + (size & 1);
        }
        return copy;
    }

    public static void Play(Sound s)
    {
        if (!Enabled || Volume <= 0) return;
        try
        {
            var wav = Load(s);
            if (wav == null) return;
            float gain = Volume / 100f;
            gain *= gain; // kulağa daha doğal gelen (logaritmik) seviye
            Player.Stop();
            Player.Stream = new MemoryStream(Volume >= 100 ? wav : Scale(wav, gain));
            Player.Play(); // arka planda çalar, bekletmez
        }
        catch (Exception ex) { Log.Write("Ses çalınamadı: " + ex.Message); }
    }
}
