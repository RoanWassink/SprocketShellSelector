using System.Text;

namespace SprocketShellSelector;

// Decode locally without Unity's unsupported ReadOnlySpan / media-request wrappers.
internal sealed record PcmWave(int Frequency, float[] Samples, int Channels = 1)
{
    internal static PcmWave Read(Stream stream, bool preserveStereo = false)
    {
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
        string Tag() => Encoding.ASCII.GetString(reader.ReadBytes(4));
        if (Tag() != "RIFF") throw new InvalidDataException("Expected RIFF WAV.");
        long end = reader.ReadUInt32() + 8L;
        if (Tag() != "WAVE" || end > stream.Length) throw new InvalidDataException("Invalid WAV container.");
        int channels = 0, frequency = 0, bits = 0, encoding = 0;
        byte[]? pcm = null;
        while (stream.Position + 8 <= end)
        {
            var tag = Tag();
            var bytes = reader.ReadUInt32();
            long next = stream.Position + bytes;
            if (next > end) throw new InvalidDataException("Truncated WAV chunk.");
            if (tag == "fmt ")
            {
                if (bytes < 16) throw new InvalidDataException("Invalid WAV format chunk.");
                encoding = reader.ReadUInt16(); channels = reader.ReadUInt16(); frequency = reader.ReadInt32();
                reader.ReadUInt32(); reader.ReadUInt16(); bits = reader.ReadUInt16();
            }
            else if (tag == "data") pcm = reader.ReadBytes(checked((int)bytes));
            stream.Position = next + (bytes & 1);
        }
        if (encoding != 1 || channels is < 1 or > 2 || bits != 16 || frequency is < 8000 or > 192000 ||
            pcm == null || pcm.Length == 0 || pcm.Length % (channels * 2) != 0)
            throw new InvalidDataException("Expected mono/stereo 16-bit PCM WAV.");
        if (preserveStereo && channels == 2)
        {
            var stereo = new float[pcm.Length / 2];
            for (int i = 0; i < stereo.Length; i++) stereo[i] = (short)(pcm[i * 2] | pcm[i * 2 + 1] << 8) / 32768f;
            return new(frequency, stereo, 2);
        }
        var mono = new float[pcm.Length / (channels * 2)];
        for (int frame = 0; frame < mono.Length; frame++)
        {
            int sum = 0;
            for (int channel = 0; channel < channels; channel++)
            {
                int offset = (frame * channels + channel) * 2;
                sum += (short)(pcm[offset] | pcm[offset + 1] << 8);
            }
            mono[frame] = sum / (32768f * channels);
        }
        return new(frequency, mono);
    }
}
