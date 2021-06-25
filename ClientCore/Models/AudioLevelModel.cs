using DynamicStreamer.Queues;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Streamster.ClientCore.Models
{
    public enum AudioLevelState { Ok, Hi, VeryHi }

    public class AudioLevelModel
    {
        private const double MinValue = -55;

        float[] _buffer = new float[0];
        double[] _last = new double[] { 0, 0 };
        int _lastPointer = 0;
        double _lastValue = 0;


        public Property<double> Volume { get; } = new Property<double>(-55);

        public Property<SoundVolumeState> State { get; } = new Property<SoundVolumeState>(SoundVolumeState.Ok);

        public void OnAudioFrame(FrameOutputData data)
        {
            var frame = data.Frame.Item.Properties;

            if (frame.Samples > _buffer.Length)
                _buffer = new float[frame.Samples];

            Marshal.Copy(frame.DataPtr0, _buffer, 0, frame.Samples);
            var left = GetMax(_buffer, frame.Samples);
            bool leftEmpty = IsEmpty(_buffer, frame.Samples);

            Marshal.Copy(frame.DataPtr1, _buffer, 0, frame.Samples);
            var right = GetMax(_buffer, frame.Samples);
            bool rightEmpty = IsEmpty(_buffer, frame.Samples);

            bool empty = leftEmpty && rightEmpty;
            var volume = Math.Max(left, right);
            
            var averaged = ToDb(AddLast(volume));

            if (averaged < MinValue)
                averaged = MinValue;

            var state = averaged < -20 ? SoundVolumeState.Ok : (averaged < -10 ? SoundVolumeState.Hi : SoundVolumeState.VeryHi);

            var progressValue = GetProgressValue(averaged, empty);
            Volume.Value = progressValue;
            if (State.Value != state)
                State.Value = state;
        }

        private double GetProgressValue(double v, bool empty)
        {
            if (empty)
                return MinValue;
            if (v < -52)
                return -52;
            return v;
        }

        private double AddLast(double val)
        {
            var pos = _lastPointer % _last.Length;
            _last[pos] = val;

            var ave = _last.Average();

            if (ave > _lastValue)
            {
                _lastValue = ave;
            }
            else
            {
                _lastValue = Math.Max(_lastValue - 2.5, ave);
            }


            return _lastValue;
        }

        double ToDb(double l)
        {
            var res = 20 * Math.Log10(l);
            return res;
        }

        bool IsEmpty(float[] buff, int length) => buff.Take(length).All(s => s == 0.0f);

        float GetMax(float[] buff, int length)
        {
            float res = 0;

            for (int q = 0; q < length; q++)
            {
                res = Math.Max(res, Math.Abs(buff[q]));
            }
            return res;
        }
    }
}
