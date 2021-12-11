using NAudio.Dsp;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AudioChatClient
{
    internal class FilteredProvider : BufferedWaveProvider
    {
        readonly BiQuadFilter _filter;
        public FilteredProvider(WaveFormat waveFormat) : base(waveFormat)
        {
            _filter = BiQuadFilter
                .HighPassFilter(waveFormat.SampleRate, 50, 1);
        }
        public new int Read(byte[] buffer, int offset, int count)
        {
            int samplesRead = base.Read(buffer, offset, count);

            for (int i = 0; i < samplesRead; i += 4)
            {
                byte[] transformed = BitConverter.GetBytes(_filter.Transform(BitConverter.ToSingle(buffer, i)));
                Buffer.BlockCopy(transformed, 0, buffer, i, 4);
            }

            return samplesRead;
        }
    }
}
