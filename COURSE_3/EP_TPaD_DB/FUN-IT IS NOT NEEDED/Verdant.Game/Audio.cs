using System.Runtime.InteropServices;
using Verdant.Core;

namespace Verdant.Game;

/// <summary>Original procedural audio. Four queued PCM buffers mix ambient music and effects.</summary>
internal sealed class Audio : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct WaveFormat { public ushort Format, Channels; public uint Rate, BytesPerSecond; public ushort Align, Bits, Extra; }
    [StructLayout(LayoutKind.Sequential)]
    private struct Header { public IntPtr Data; public uint Length, Recorded; public UIntPtr User; public uint Flags, Loops; public IntPtr Next; public UIntPtr Reserved; }
    [DllImport("winmm.dll")] private static extern uint waveOutOpen(out IntPtr handle, uint device, ref WaveFormat format, IntPtr callback, IntPtr instance, uint flags);
    [DllImport("winmm.dll")] private static extern uint waveOutPrepareHeader(IntPtr handle, IntPtr header, uint size);
    [DllImport("winmm.dll")] private static extern uint waveOutUnprepareHeader(IntPtr handle, IntPtr header, uint size);
    [DllImport("winmm.dll")] private static extern uint waveOutWrite(IntPtr handle, IntPtr header, uint size);
    [DllImport("winmm.dll")] private static extern uint waveOutReset(IntPtr handle);
    [DllImport("winmm.dll")] private static extern uint waveOutClose(IntPtr handle);
    private sealed class Voice(double frequency, double duration, float volume, bool sweep)
    { public double Frequency = frequency, Duration = duration, Age; public float Volume = volume; public bool Sweep = sweep; }
    private readonly object gate = new();
    private readonly List<Voice> voices = [];
    private readonly List<(IntPtr Header, IntPtr Data)> buffers = [];
    private readonly short[] samples = new short[2205];
    private readonly uint headerSize = (uint)Marshal.SizeOf<Header>();
    private System.Threading.Timer? pump;
    private IntPtr device;
    private bool disposed;
    private long sampleIndex;
    private long lastShot;
    private static readonly double[] Melody = [329.63, 392, 440, 392, 293.66, 329.63, 261.63, 293.66, 329.63, 392, 493.88, 440, 392, 329.63, 293.66, 246.94];
    public bool Enabled { get; set; }
    public bool MusicEnabled { get; set; }
    public Audio()
    {
        var format = new WaveFormat { Format = 1, Channels = 1, Rate = 22050, BytesPerSecond = 44100, Align = 2, Bits = 16 };
        if (waveOutOpen(out device, uint.MaxValue, ref format, IntPtr.Zero, IntPtr.Zero, 0) != 0) { device = IntPtr.Zero; return; }
        for (int i = 0; i < 4; i++)
        {
            var data = Marshal.AllocHGlobal(samples.Length * 2); var header = Marshal.AllocHGlobal((int)headerSize);
            Marshal.Copy(samples, 0, data, samples.Length);
            Marshal.StructureToPtr(new Header { Data = data, Length = (uint)samples.Length * 2 }, header, false);
            if (waveOutPrepareHeader(device, header, headerSize) != 0) { Marshal.FreeHGlobal(data); Marshal.FreeHGlobal(header); continue; }
            buffers.Add((header, data)); waveOutWrite(device, header, headerSize);
        }
        pump = new System.Threading.Timer(_ => Pump(), null, 20, 20);
    }
    public void Play(EffectKind effect)
    {
        if (!Enabled || disposed) return;
        lock (gate)
        {
            long now = Environment.TickCount64;
            bool shot = effect is EffectKind.Arrow or EffectKind.Magic or EffectKind.Rock;
            if (shot && now - lastShot < 170) return;
            if (shot) lastShot = now;
            if (voices.Count > 14) return;
            voices.Add(effect switch
            {
                EffectKind.Arrow => new(830, .09, .045f, true), EffectKind.Magic => new(590, .19, .065f, true),
                EffectKind.Rock => new(95, .24, .1f, true), EffectKind.Burst => new(135, .48, .14f, true),
                EffectKind.Frost => new(1174, .4, .065f, false), EffectKind.Leak => new(156, .28, .13f, false),
                _ => new(440, .28, .08f, false)
            });
        }
    }
    public void Cue(bool victory)
    {
        if (!Enabled) return;
        lock (gate) foreach (double note in victory ? new[] { 392d, 493.88, 587.33 } : new[] { 146.83, 174.61, 207.65 }) voices.Add(new(note, 1.4, .08f, false));
    }
    private void Pump()
    {
        lock (gate)
        {
            if (disposed || device == IntPtr.Zero) return;
            foreach (var buffer in buffers)
            {
                if ((Marshal.PtrToStructure<Header>(buffer.Header).Flags & 1) == 0) continue;
                for (int i = 0; i < samples.Length; i++, sampleIndex++)
                {
                    double t = sampleIndex / 22050d, value = 0;
                    if (MusicEnabled)
                    {
                        double phase = t % .8 / .8, note = Melody[(int)(t / .8) % Melody.Length];
                        double envelope = Math.Sin(phase * Math.PI) * .035;
                        value = Math.Sin(t * Math.Tau * note) * envelope + Math.Sin(t * Math.Tau * 130.81) * .018;
                    }
                    if (Enabled)
                    foreach (var voice in voices)
                    {
                        double life = voice.Age / voice.Duration;
                        value += Math.Sin(Math.Tau * voice.Frequency * voice.Age * (voice.Sweep ? 1 - life * .35 : 1)) * voice.Volume * Math.Pow(Math.Max(0, 1 - life), 2);
                    }
                    foreach (var voice in voices) voice.Age += 1 / 22050d;
                    voices.RemoveAll(v => v.Age >= v.Duration);
                    samples[i] = (short)(Math.Clamp(value, -.8, .8) * short.MaxValue);
                }
                Marshal.Copy(samples, 0, buffer.Data, samples.Length); waveOutWrite(device, buffer.Header, headerSize);
            }
        }
    }
    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return; disposed = true; pump?.Dispose();
            if (device == IntPtr.Zero) return;
            waveOutReset(device);
            foreach (var b in buffers) { waveOutUnprepareHeader(device, b.Header, headerSize); Marshal.FreeHGlobal(b.Data); Marshal.FreeHGlobal(b.Header); }
            buffers.Clear(); waveOutClose(device); device = IntPtr.Zero;
        }
    }
}
